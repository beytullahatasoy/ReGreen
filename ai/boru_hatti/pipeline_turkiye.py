# -*- coding: utf-8 -*-
"""
Turkiye geneli veri uretimi
===========================

yangin_kesif.py'nin urettigi yanginlar.json listesini alir, her yangin icin
oznitelik tablosu uretir ve hepsini tek bir veri setinde birlestirir.

firerecover_pipeline.py'deki katman fonksiyonlarini yeniden kullanir; buradaki
ek isler olcege ozgu:

  · Tarihe gore pencere hesabi   - her yanginin kendi tarihi var, sabit degil
  · Parca parca kaydetme         - 40 yanginin 37.'sinde cokerse bastan baslama
  · Overpass hiz sinirlamasi     - 120+ sorgu, engellenmemek icin
  · Yagis ozniteligi             - Open-Meteo, bolgeler arasi iklim farki
  · Otomatik kalite kontrolu     - 40 bolgeyi gozle denetleyemeyiz

Calistirma:
    python pipeline_turkiye.py            # hepsi
    python pipeline_turkiye.py 5          # ilk 5 yangin (deneme)

Cikti: turkiye_grid.csv, turkiye_ozet.json, parcalar/
"""

import json
import pathlib
import sys
import time
import warnings
from collections import defaultdict
from datetime import datetime, timedelta

import numpy as np
import pandas as pd
import requests

warnings.filterwarnings("ignore")

from scipy.spatial import cKDTree

import firerecover_pipeline as fp

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent))
import yollar
yollar.yol_ekle()

# Izgara cozunurlugu. 500 m'de kucuk yanginlar cozulemiyordu: 300 ha'lik bir
# yangin 12 kare ediyor, filtrelerden sonra 7 kaliyor ve grup olusturamiyor.
# 250 m'de ayni yangin 48 kare eder. Olcum: uygun yangin 17 -> ~34.
HUCRE_M = 250
HUCRE_HA = (HUCRE_M ** 2) / 10_000      # bir hucrenin alani (hektar)

KOK = yollar.ARA          # ortak veri koku - ai/yollar.py
PARCALAR = KOK / f"parcalar_{HUCRE_M}m"
PARCALAR.mkdir(exist_ok=True)
YAGIS_ONBELLEK = KOK / "onbellek" / "yagis.json"
YAGIS_ONBELLEK.parent.mkdir(exist_ok=True)

OVERPASS_BEKLE = 8       # saniye, sorgular arasi
UA = {"User-Agent": "FireRecoverAI/0.3 (Huawei ICT Competition student project)"}


# --------------------------------------------------------------- pencereler
def pencereler(tarih_str):
    """Yangin tarihinden dort gozlem penceresi turet.

    Yil1 ve yil2 pencereleri AYNI MEVSIME denk gelecek sekilde secilir;
    aksi halde NDVI farkina mevsim degisimi karisir.
    """
    t = datetime.fromisoformat(tarih_str)
    g = lambda d: (t + timedelta(days=d)).strftime("%Y-%m-%d")
    return {
        "oncesi":  f"{g(-70)}/{g(-3)}",
        "sonrasi": f"{g(20)}/{g(95)}",
        "yil1":    f"{g(330)}/{g(400)}",
        "yil2":    f"{g(695)}/{g(765)}",
    }


# -------------------------------------------------------- S2 mozaik kompozit
def s2_kompozit_mozaik(grid, period, nbr_de=False, maks_gun=6):
    """Ayni gunun komsu karolarini mozaikler, gunler arasi medyan alir.

    firerecover_pipeline.s2_kompozit tek bir MGRS karosunu tercih ediyordu;
    bu, tek nokta calisirken dogruydu ama genis kutularda kapsam dusuruyor:
    kutu iki karoya yayilinca yarisi bos kaliyor. Sentinel-2 komsu karolari
    ayni gun cektigi icin once gun icinde mozaikleyip sonra medyan almak
    hem kapsami hem gurultu direncini koruyor.
    """
    bos = lambda: np.full((grid.h, grid.w), np.nan, dtype="float32")
    items = list(fp.cat.search(
        collections=["sentinel-2-l2a"], bbox=grid.bbox4326, datetime=period,
        query={"eo:cloud_cover": {"lt": fp.BULUT_ESIK}}).items())
    if not items:
        return bos(), bos(), []

    gunler = defaultdict(list)
    for it in items:
        gunler[str(it.datetime.date())].append(it)
    sirali = sorted(gunler.items(), key=lambda kv: float(
        np.mean([i.properties["eo:cloud_cover"] for i in kv[1]])))

    bantlar = [("red", "B04"), ("nir", "B08"), ("scl", "SCL")]
    if nbr_de:
        bantlar += [("nir2", "B8A"), ("swir", "B12")]

    nd_yigin, nb_yigin, kullanilan = [], [], []
    for gun, sahneler in sirali:
        if len(kullanilan) >= maks_gun:
            break
        nd, nb = bos(), bos()
        for it in sahneler:
            try:
                b = {}
                for ad, asset in bantlar:
                    rs = fp.Resampling.nearest if ad == "scl" else fp.Resampling.average
                    r = fp.raster_grid(it.assets[asset].href, grid, rs)
                    if r is None:
                        raise ValueError("karo kutuyu kesmiyor")
                    b[ad] = fp._grid_e_aktar(r[0], r[1], r[2], grid, rs)
                iyi = np.isin(np.rint(np.nan_to_num(b["scl"], nan=0)).astype("int16"),
                              [4, 5, 6, 7])
                n = np.where(iyi, (b["nir"] - b["red"]) /
                             (b["nir"] + b["red"] + 1e-9), np.nan)
                nd = np.where(np.isnan(nd), n, nd)
                if nbr_de:
                    q = np.where(iyi, (b["nir2"] - b["swir"]) /
                                 (b["nir2"] + b["swir"] + 1e-9), np.nan)
                    nb = np.where(np.isnan(nb), q, nb)
            except Exception:
                continue
        if np.isfinite(nd).mean() < 0.35:
            continue
        nd_yigin.append(nd)
        if nbr_de:
            nb_yigin.append(nb)
        kullanilan.append({"tarih": gun, "karo": len(sahneler)})

    if not nd_yigin:
        return bos(), bos(), []
    ndvi = np.nanmedian(np.dstack(nd_yigin), axis=2)
    nbr = np.nanmedian(np.dstack(nb_yigin), axis=2) if nb_yigin else bos()
    return ndvi, nbr, kullanilan


# ------------------------------------------------------------------- yagis
def yagis_ozellikleri(yangin):
    """Yangin sonrasi 2 yil yagisi ve uzun donem ortalamasina gore anomali.

    Ham milimetre yaniltici: 600 mm, normalde 1200 alan yerde kuraklik,
    normalde 500 alan yerde bolluk. Model anomaliyi gormeli.
    """
    onbellek = json.loads(YAGIS_ONBELLEK.read_text()) if YAGIS_ONBELLEK.exists() else {}
    anahtar = yangin["id"]
    if anahtar in onbellek:
        return onbellek[anahtar]

    lat, lon, t = yangin["lat"], yangin["lon"], yangin["tarih"]

    def cek(bas, bit):
        r = requests.get("https://archive-api.open-meteo.com/v1/archive", params={
            "latitude": lat, "longitude": lon, "start_date": bas, "end_date": bit,
            "daily": "precipitation_sum", "timezone": "Europe/Istanbul"},
            headers=UA, timeout=90)
        r.raise_for_status()
        p = r.json()["daily"]["precipitation_sum"]
        return float(np.nansum([x if x is not None else 0.0 for x in p]))

    try:
        bit = (datetime.fromisoformat(t) + timedelta(days=730)).strftime("%Y-%m-%d")
        sonrasi = cek(t, bit)
        time.sleep(1.5)
        uzun = cek("2010-01-01", "2020-12-31") / 11.0
        time.sleep(1.5)
        sonuc = {
            "yagis_sonrasi_2yil_mm": round(sonrasi, 1),
            "yagis_uzun_ort_mm": round(uzun, 1),
            "yagis_anomali": round((sonrasi / 2) / uzun, 3) if uzun > 0 else None,
        }
    except Exception as e:
        print(f"    ! yagis alinamadi: {type(e).__name__}")
        sonuc = {"yagis_sonrasi_2yil_mm": None, "yagis_uzun_ort_mm": None,
                 "yagis_anomali": None}

    onbellek[anahtar] = sonuc
    YAGIS_ONBELLEK.write_text(json.dumps(onbellek, ensure_ascii=False, indent=1))
    return sonuc


# ------------------------------------------------------------ kalite kontrol
def kalite_kontrol(df, yangin):
    """40 bolgeyi gozle denetleyemeyiz; sessiz hatalari otomatik yakala."""
    uyari = []
    n = len(df)
    gecerli = df["gecerli_hucre"].mean()
    yanik = int(df["yanik"].sum())
    etiketli = int(df["etiket_gecerli"].sum())

    if gecerli < 0.80:
        uyari.append(f"kapsam dusuk (%{gecerli*100:.0f})")
    if yanik < 15:
        uyari.append(f"yanik hucre az ({yanik})")
    if etiketli < 10:
        uyari.append(f"etiketli hucre az ({etiketli})")

    # su yanamaz: fiziksel tutarlilik
    yd = df[df["yanik"]]
    if len(yd):
        su = (yd["arazi_tipi"] == "Su").mean()
        if su > 0.02:
            uyari.append(f"yanik alanda su %{su*100:.0f}")
        if yd["dnbr"].max() > 1.4:
            uyari.append(f"dNBR asiri yuksek ({yd['dnbr'].max():.2f})")
        # MODIS'in bildirdigi alanla bizim buldugumuz tutuyor mu
        bizim = int(yanik * HUCRE_HA)
        oran = bizim / max(yangin["alan_ha"], 1)
        if not 0.15 < oran < 4.0:
            uyari.append(f"alan uyusmuyor (MODIS {yangin['alan_ha']:,} ha, "
                         f"biz {bizim:,} ha)")
    return uyari


# --------------------------------------------------------------- tek yangin
def yangin_isle(yangin):
    kod = yangin["id"]
    grid = fp.Grid.olustur(yangin["bbox"], HUCRE_M)
    pen = pencereler(yangin["tarih"])

    print(f"  grid {grid.h}x{grid.w} = {grid.h*grid.w:,} hucre  [{grid.crs}]")

    ndvi, nbr = {}, {}
    for etiket, period in pen.items():
        nd, nb, kul = s2_kompozit_mozaik(
            grid, period, nbr_de=etiket in ("oncesi", "sonrasi"))
        ndvi[etiket], nbr[etiket] = nd, nb
        karo = sum(k["karo"] for k in kul)
        print(f"    S2 {etiket:<8}: {len(kul)} gun / {karo} karo, kapsam "
              f"%{np.isfinite(nd).mean()*100:.0f}")

    dnbr = nbr["oncesi"] - nbr["sonrasi"]
    z, egim, baki, _ = fp.dem_katmanlari(grid)
    lc, agac, _ = fp.arazi_ortusu(grid)
    mes, sayim = fp.osm_mesafeler(grid)
    print(f"    OSM: yerlesim {sayim.get('yerlesim',0)} / su {sayim.get('su',0)}"
          f" / yol {sayim.get('yol',0)}")

    lon, lat = grid.merkez_lonlat()
    d = lambda a: a.ravel()
    yag = yagis_ozellikleri(yangin)

    df = pd.DataFrame({
        "yangin_id": kod,
        "bolge": yangin["bolge"],
        "yil": yangin["yil"],
        "yangin_tarihi": yangin["tarih"],
        "modis_alan_ha": yangin["alan_ha"],
        "hucre_id": [f"{kod}_{i:06d}" for i in range(grid.h * grid.w)],
        "lat": np.round(lat, 5), "lon": np.round(lon, 5),
        "yukselti_m": d(z), "egim_derece": d(egim), "baki_derece": d(baki),
        "arazi_kodu": d(lc), "agac_orani": d(agac),
        "yerlesim_mesafe_km": mes["yerlesim"], "su_mesafe_km": mes["su"],
        "yol_mesafe_km": mes["yol"],
        "dnbr": d(dnbr), "dndvi": d(ndvi["oncesi"] - ndvi["sonrasi"]),
        "ndvi_oncesi": d(ndvi["oncesi"]), "ndvi_sonrasi": d(ndvi["sonrasi"]),
        "ndvi_yil1": d(ndvi["yil1"]), "ndvi_yil2": d(ndvi["yil2"]),
        **{k: v for k, v in yag.items()},
    })

    df["arazi_tipi"] = df["arazi_kodu"].map(
        lambda v: fp.WORLDCOVER.get(int(v), None) if np.isfinite(v) else None)

    df["gecerli_hucre"] = df[["dnbr", "arazi_kodu", "egim_derece",
                              "ndvi_oncesi", "ndvi_yil2"]].notna().all(axis=1)
    df["bitki_vardi"] = ((df["ndvi_oncesi"] >= fp.BITKI_ESIK) &
                         (df["arazi_kodu"] != fp.SU_SINIFI))
    df["yanik"] = ((df["dnbr"] >= fp.YANIK_ESIK_DNBR) & df["bitki_vardi"]
                   & df["gecerli_hucre"])

    df["siddet_sinifi"] = pd.cut(
        df["dnbr"], [-np.inf, 0.10, 0.27, 0.44, 0.66, np.inf],
        labels=["yanmamis", "dusuk", "orta-dusuk", "orta-yuksek", "yuksek"])
    df.loc[~df["yanik"], "siddet_sinifi"] = "yanmamis"

    payda = df["ndvi_oncesi"] - df["ndvi_sonrasi"]
    df["ndvi_dusus"] = payda
    df["etiket_gecerli"] = (df["yanik"] & (df["dnbr"] >= fp.ETIKET_ESIK_DNBR)
                            & (payda >= fp.ETIKET_ESIK_PAYDA))

    # Hedef degisken: kalan acik. Oranli tanim yangin siddetiyle matematiksel
    # olarak bagli oldugu icin birincil hedef bu.
    df["kalan_acik"] = np.where(df["etiket_gecerli"],
                                df["ndvi_oncesi"] - df["ndvi_yil2"], np.nan)
    df["iyilesme_yil2"] = np.where(
        df["etiket_gecerli"],
        ((df["ndvi_yil2"] - df["ndvi_sonrasi"]) / payda).clip(-1, 2), np.nan)

    return df


# ------------------------------------------------------------------- ana
def main():
    yollar.gerekli(KOK / "yanginlar.json")
    limit = int(sys.argv[1]) if len(sys.argv) > 1 else None
    yanginlar = json.loads((KOK / "yanginlar.json").read_text(encoding="utf-8"))
    if limit:
        yanginlar = yanginlar[:limit]

    print("=" * 72)
    print(f"TURKIYE GENELI VERI URETIMI  ·  {len(yanginlar)} yangin")
    print("=" * 72)

    t0 = time.time()
    rapor = {}
    for i, y in enumerate(yanginlar, 1):
        parca = PARCALAR / f"{y['id']}.csv"
        print(f"\n[{i}/{len(yanginlar)}] {y['id']}  {y['tarih']}  "
              f"{y['alan_ha']:,} ha  {y['bolge']}")

        if parca.exists():
            print("  zaten var, atlandi")
            continue

        try:
            ty = time.time()
            df = yangin_isle(y)
            uyari = kalite_kontrol(df, y)
            df["kalite_uyari"] = "; ".join(uyari) if uyari else ""
            df.to_csv(parca, index=False, encoding="utf-8-sig")
            rapor[y["id"]] = {"hucre": len(df), "yanik": int(df["yanik"].sum()),
                              "etiketli": int(df["etiket_gecerli"].sum()),
                              "uyari": uyari}
            durum = "UYARI: " + "; ".join(uyari) if uyari else "temiz"
            print(f"  -> {int(df['yanik'].sum()):,} yanik, "
                  f"{int(df['etiket_gecerli'].sum()):,} etiketli, "
                  f"{time.time()-ty:.0f} sn  [{durum}]")
        except Exception as e:
            print(f"  !! BASARISIZ: {type(e).__name__}: {str(e)[:140]}")
            rapor[y["id"]] = {"hata": f"{type(e).__name__}: {e}"}

        time.sleep(OVERPASS_BEKLE)

    # ------------------------------------------------------ birlestir
    print("\n" + "=" * 72)
    parcalar = sorted(PARCALAR.glob("*.csv"))
    if not parcalar:
        print("Hic parca uretilemedi.")
        return
    tam = pd.concat([pd.read_csv(p) for p in parcalar], ignore_index=True)
    tam.to_csv(KOK / f"turkiye_grid.csv", index=False, encoding="utf-8-sig")

    temiz = tam[tam["kalite_uyari"].fillna("") == ""]
    print("OZET")
    print("=" * 72)
    print(f"  Islenen yangin      : {tam['yangin_id'].nunique()}")
    print(f"  Uyarisiz yangin     : {temiz['yangin_id'].nunique()}")
    print(f"  Toplam hucre        : {len(tam):,}")
    print(f"  Yanik hucre         : {int(tam['yanik'].sum()):,}")
    print(f"  Etiketli hucre      : {int(tam['etiket_gecerli'].sum()):,}")
    print(f"  Cografi grup        : {tam['yangin_id'].nunique()}")
    print(f"  Yil araligi         : {tam['yil'].min()} - {tam['yil'].max()}")
    print(f"  Sure                : {(time.time()-t0)/60:.0f} dk")

    print("\n  Bolgeye gore etiketli hucre:")
    for b, g in tam.groupby("bolge"):
        print(f"    {b:<14}{int(g['etiket_gecerli'].sum()):>7,}  "
              f"({g['yangin_id'].nunique()} yangin)")

    uyarililar = tam[tam["kalite_uyari"].fillna("") != ""]["yangin_id"].unique()
    if len(uyarililar):
        print(f"\n  Kalite uyarisi olan {len(uyarililar)} yangin:")
        for u in uyarililar:
            m = tam[tam["yangin_id"] == u]["kalite_uyari"].iloc[0]
            print(f"    {u:<16}{m}")

    (KOK / "turkiye_ozet.json").write_text(
        json.dumps(rapor, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"\n  -> turkiye_grid.csv, turkiye_ozet.json")


if __name__ == "__main__":
    main()
