# -*- coding: utf-8 -*-
"""
Arazi ortusu zamanlama hatasinin duzeltmesi
===========================================

SORUN: ESA WorldCover tek bir yila ait (2021). Biz onu butun yanginlar icin
kullaniyorduk. Sonuc:

  · 2017-2020 yanginlari  -> WorldCover yangindan SONRAKI hali gosteriyor.
    Orman yanmis, 2021'de calilik gorunuyor, "agac orani" yanlis dusuk cikiyor.
  · 2022-2024 yanginlari  -> yangindan ONCEKI hali gosteriyor, dogru.

Olcum bunu dogruladi. agac_orani ile kalan_acik iliskisi:
    2017-2020 : rho = -0.035  (iliski YOK, oznitelik bozuk)
    2021      : rho = +0.726  (guclu)
    2022-2024 : rho = +0.207

Yani en guclu ozniteligimiz, orneklerin bir kisminda anlamsiz sayi uretiyordu.

COZUM: io-lulc-annual-v02 (Impact Observatory, yillik 2017-2023, 10 m).
Her yangin icin YANGINDAN ONCEKI YILIN haritasini kullaniyoruz. Boylece
oznitelik tanim geregi dogru zamana ait oluyor.

Calistirma:  python arazi_duzelt.py
Cikti: parcalar_250m/*.csv dosyalarina agac_orani_y ve arazi_kodu_y eklenir,
       turkiye_grid.csv yeniden birlestirilir.
"""

import json
import pathlib
import time
import warnings

import numpy as np
import pandas as pd

warnings.filterwarnings("ignore")

import firerecover_pipeline as fp
import pipeline_turkiye as pt
from rasterio.enums import Resampling

KOK = pathlib.Path(__file__).parent

# Impact Observatory sinif kodlari
IO = {1: "Su", 2: "Agaclik", 4: "Sulak alan", 5: "Tarim", 7: "Yerlesim",
      8: "Ciplak", 9: "Kar/buz", 10: "Bulut", 11: "Otlak/calilik"}
IO_AGAC = 2

# Yillik harita 2017-2023 arasi var. Yangindan onceki yili istiyoruz;
# 2017 yanginlari icin oncesi yok, 2017'nin kendisiyle yetinmek zorundayiz
# (o yil icin oznitelik kismen kirli kalir, 4 yangini etkiliyor).
IO_MIN, IO_MAX = 2017, 2023


def lulc_yili(yangin_yili):
    return max(IO_MIN, min(IO_MAX, yangin_yili - 1))


def arazi_yillik(grid, yil):
    """Verilen yila ait arazi ortusu: (baskin sinif, agac orani)."""
    items = list(fp.cat.search(collections=["io-lulc-annual-v02"],
                               bbox=grid.bbox4326,
                               datetime=f"{yil}-01-01/{yil}-12-31").items())
    if not items:
        bos = np.full((grid.h, grid.w), np.nan, dtype="float32")
        return bos, bos, 0

    sinif = np.full((grid.h, grid.w), np.nan, dtype="float32")
    agac = np.full((grid.h, grid.w), np.nan, dtype="float32")
    for it in items:
        try:
            # baskin sinif: cogunluk ile ornekle
            r = fp.raster_grid(it.assets["data"].href, grid, Resampling.mode, olcek=2)
            if r is None:
                continue
            p = fp._grid_e_aktar(r[0], r[1], r[2], grid, Resampling.mode)
            sinif = np.where(np.isfinite(p), p, sinif)

            # agac orani: ikili maskeyi ortalama ile ornekle
            r2 = fp.raster_grid(it.assets["data"].href, grid, Resampling.nearest, olcek=2)
            ikili = (np.abs(r2[0] - IO_AGAC) < 0.5).astype("float32")
            pa = fp._grid_e_aktar(ikili, r2[1], r2[2], grid, Resampling.average)
            agac = np.where(np.isfinite(pa), pa, agac)
        except Exception:
            continue
    return sinif, agac, len(items)


def main():
    yanginlar = {y["id"]: y for y in
                 json.loads((KOK / "yanginlar.json").read_text(encoding="utf-8"))}
    parcalar = sorted((KOK / f"parcalar_{pt.HUCRE_M}m").glob("*.csv"))
    print("=" * 74)
    print(f"ARAZI ORTUSU ZAMANLAMA DUZELTMESI  ·  {len(parcalar)} yangin")
    print("=" * 74)

    t0 = time.time()
    for i, yol in enumerate(parcalar, 1):
        kod = yol.stem
        y = yanginlar.get(kod)
        if y is None:
            print(f"[{i}/{len(parcalar)}] {kod}: listede yok, atlandi")
            continue

        df = pd.read_csv(yol, low_memory=False)
        if "agac_orani_y" in df.columns:
            print(f"[{i}/{len(parcalar)}] {kod}: zaten var, atlandi")
            continue

        lyil = lulc_yili(y["yil"])
        grid = fp.Grid.olustur(y["bbox"], pt.HUCRE_M)
        if grid.h * grid.w != len(df):
            print(f"[{i}/{len(parcalar)}] {kod}: izgara uyusmuyor "
                  f"({grid.h*grid.w} vs {len(df)}), atlandi")
            continue

        sinif, agac, nk = arazi_yillik(grid, lyil)
        df["arazi_kodu_y"] = sinif.ravel()
        df["agac_orani_y"] = agac.ravel()
        df["arazi_tipi_y"] = df["arazi_kodu_y"].map(
            lambda v: IO.get(int(v)) if np.isfinite(v) else None)
        df["lulc_yili"] = lyil
        df["lulc_yangindan_once"] = lyil < y["yil"]
        df.to_csv(yol, index=False, encoding="utf-8-sig")

        eski = df.loc[df["etiket_gecerli"], "agac_orani"].mean()
        yeni = df.loc[df["etiket_gecerli"], "agac_orani_y"].mean()
        print(f"[{i}/{len(parcalar)}] {kod:<16} yangin {y['yil']} -> lulc {lyil} "
              f"({nk} karo)  agac orani: {eski:.3f} -> {yeni:.3f}")

    print(f"\nBirlestiriliyor... ({time.time()-t0:.0f} sn)")
    tam = pd.concat([pd.read_csv(p, low_memory=False) for p in parcalar],
                    ignore_index=True)
    tam.to_csv(KOK / "turkiye_grid.csv", index=False, encoding="utf-8-sig")
    print(f"turkiye_grid.csv yeniden yazildi: {len(tam):,} satir, "
          f"{len(tam.columns)} sutun")


if __name__ == "__main__":
    main()
