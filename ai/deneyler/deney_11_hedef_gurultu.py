# -*- coding: utf-8 -*-
"""Hedefte ne kadar olcum gurultusu var?

Su an 2. yil NDVI'si, 70 gunluk pencerede EN AZ BULUTLU 6 GUNUN
medyani. Soru: 6 gun yetiyor mu, yoksa medyan hala oynak mi?

Test: ayni yanginin ayni penceresini
   - 6 gunle  (mevcut ayar)
   - 18 gunle (daha cok olcum)
   - ve 6 gunun IKI AYRI YARISIYLA (3+3)
hesaplayip karsilastir.

Kritik olcum SONUNCUSU: ayni pencerenin iki bagimsiz yarisi birbirini
ne kadar tutuyor? Bu, olcumun GUVENILIRLIGI. Eger iki yari r=0.95 ise
gurultu az; r=0.80 ise ciddi gurultu var ve tavanimizi o belirliyor.

Guvenilirlik r ise, o hedefi tahmin eden HICBIR model
sqrt(r)'nin ustune cikamaz - istatistikte klasik sonuc.
"""
import sys, json, time, warnings
import numpy as np
warnings.filterwarnings("ignore")
sys.stdout.reconfigure(encoding="utf-8")

import firerecover_pipeline as fp
import pipeline_turkiye as pt
from scipy.stats import spearmanr, pearsonr

TEST = ["EGE_2021_08", "AKD_2023_02"]      # kucuk bbox = hizli
HUCRE_M = pt.HUCRE_M


def kompozit(grid, period, gunler_listesi=None, maks_gun=6, atla=0):
    """s2_kompozit_mozaik'in gun secimi kontrol edilebilir hali."""
    from collections import defaultdict
    bos = lambda: np.full((grid.h, grid.w), np.nan, dtype="float32")
    items = list(fp.cat.search(collections=["sentinel-2-l2a"],
                               bbox=grid.bbox4326, datetime=period,
                               query={"eo:cloud_cover": {"lt": fp.BULUT_ESIK}}).items())
    if not items:
        return bos(), []
    gunler = defaultdict(list)
    for it in items:
        gunler[str(it.datetime.date())].append(it)
    sirali = sorted(gunler.items(), key=lambda kv: float(
        np.mean([i.properties["eo:cloud_cover"] for i in kv[1]])))
    sirali = sirali[atla:]

    yigin, kullanilan = [], []
    for gun, sahneler in sirali:
        if len(kullanilan) >= maks_gun:
            break
        nd = bos()
        for it in sahneler:
            try:
                b = {}
                for ad, asset in [("red", "B04"), ("nir", "B08"), ("scl", "SCL")]:
                    rs = fp.Resampling.nearest if ad == "scl" else fp.Resampling.average
                    r = fp.raster_grid(it.assets[asset].href, grid, rs)
                    if r is None:
                        raise ValueError
                    b[ad] = fp._grid_e_aktar(r[0], r[1], r[2], grid, rs)
                iyi = np.isin(np.rint(np.nan_to_num(b["scl"], nan=0)).astype("int16"),
                              [4, 5, 6, 7])
                n = np.where(iyi, (b["nir"] - b["red"]) /
                             (b["nir"] + b["red"] + 1e-9), np.nan)
                nd = np.where(np.isnan(nd), n, nd)
            except Exception:
                continue
        if np.isfinite(nd).mean() < 0.35:
            continue
        yigin.append(nd)
        kullanilan.append(gun)
    if not yigin:
        return bos(), []
    return np.nanmedian(np.dstack(yigin), axis=2), kullanilan


yanginlar = {y["id"]: y for y in
             json.loads(open("yanginlar.json", encoding="utf-8").read())}

print("=" * 84)
print("HEDEF GURULTUSU TESTI")
print("=" * 84)

for kod in TEST:
    y = yanginlar[kod]
    grid = fp.Grid.olustur(y["bbox"], HUCRE_M)
    pen = pt.pencereler(y["tarih"])["yil2"]
    print("\n--- %s   %s   %dx%d hucre   pencere %s"
          % (kod, y["tarih"], grid.h, grid.w, pen))

    t0 = time.time()
    a6, g6 = kompozit(grid, pen, maks_gun=6)
    print("    6 gun  : %d gun kullanildi %s  (%.0f sn)"
          % (len(g6), g6[:6], time.time() - t0))

    t0 = time.time()
    a18, g18 = kompozit(grid, pen, maks_gun=18)
    print("    18 gun : %d gun kullanildi  (%.0f sn)" % (len(g18), time.time() - t0))

    # iki bagimsiz yari: en iyi 3 gun vs sonraki 3 gun
    y1, gy1 = kompozit(grid, pen, maks_gun=3, atla=0)
    y2, gy2 = kompozit(grid, pen, maks_gun=3, atla=3)
    print("    yari-A : %s" % gy1)
    print("    yari-B : %s" % gy2)

    m = np.isfinite(a6) & np.isfinite(a18)
    print("\n    6 gun ile 18 gun karsilastirmasi (%d hucre):" % m.sum())
    if m.sum() > 50:
        print("      Pearson r   : %.4f" % pearsonr(a6[m], a18[m]).statistic)
        print("      Spearman rho: %.4f" % spearmanr(a6[m], a18[m]).statistic)
        d = a18[m] - a6[m]
        print("      fark: ortalama %+.4f | std %.4f | %%95 araligi %.4f"
              % (d.mean(), d.std(), np.percentile(np.abs(d), 95)))

    mm = np.isfinite(y1) & np.isfinite(y2)
    print("\n    IKI BAGIMSIZ YARI (guvenilirlik) (%d hucre):" % mm.sum())
    if mm.sum() > 50:
        r = pearsonr(y1[mm], y2[mm]).statistic
        rs = spearmanr(y1[mm], y2[mm]).statistic
        print("      Pearson r   : %.4f" % r)
        print("      Spearman rho: %.4f" % rs)
        d = y2[mm] - y1[mm]
        print("      fark std    : %.4f  (NDVI birimi)" % d.std())
        # Spearman-Brown: yarim testin guvenilirliginden tam testin
        tam = 2 * r / (1 + r) if r > 0 else np.nan
        print("      -> 3 gunluk olcumun guvenilirligi   : %.3f" % r)
        print("      -> 6 gunluk (Spearman-Brown tahmini): %.3f" % tam)
        if tam > 0:
            print("      -> hicbir modelin gecemeyecegi tavan: %.3f" % np.sqrt(tam))

print("\n" + "=" * 84)
print("YORUM")
print("=" * 84)
print("  Iki yari r > 0.95  -> gurultu az, daha cok gun almanin faydasi sinirli")
print("  Iki yari r < 0.90  -> ciddi gurultu, hedefi temizlemek kazandirir")
print("  'tavan' satiri: olcumun kendi guvenilirligi nedeniyle hicbir")
print("  modelin gecemeyecegi teorik sinir.")
