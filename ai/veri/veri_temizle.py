# -*- coding: utf-8 -*-
"""
Veri seti temizligi - denetimde bulunan sorunlarin duzeltmesi
=============================================================

1. NODATA        arazi_kodu_y == 0 gercek bir sinif degil, "veri yok" demek.
                 Impact Observatory kodlari 1,2,4,5,7,8,9,10,11'dir.
                 agac_orani_y bu hucrelerde 0.000 yaziyordu; model bunu
                 "hic agac yok" diye okuyor, dogrusu "bilmiyoruz".
                 AKD_2020_01'in %83'u boyle -> rho -0.341 cikiyordu,
                 nodata atilinca +0.513.

2. AYNI OLAY     Cok yakin tarihli ve agir cakisan yangin kayitlari tek
                 olaydir. AKD_2021_07 (28.07.2021) ve IAN_2021_01
                 (29.07.2021) merkezleri 0.1 km, %62 cakisiyor: ayni
                 yanginin bolge sinirindan ikiye bolunmus hali.

3. YENIDEN YANMA Bir hucre, ilk yangindan sonraki 2 yillik iyilesme
                 penceresi icinde tekrar yandiysa NDVI_yil2 iyilesmeyi
                 degil ikinci yangini olcer. Etiket gecersizdir.

4. MEKANSAL GRUP Cakisan ya da bitisik yanginlar GroupKFold'da ayri grup
                 sayilirsa model ayni araziyi hem egitimde hem testte
                 gorur. Birlesik grup kimligi uretiyoruz.

5. YINELENEN     Ayni olaya birlesen kayitlarda ayni yer zemini iki satir
                 olarak duruyor. Tekillestiriliyor.

Calistirma:  python veri_temizle.py
Cikti: turkiye_grid.csv (temizlenmis), temizlik_raporu.json
"""

import sys
import json
import pathlib

import numpy as np
import pandas as pd
from scipy.spatial import cKDTree

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent))
import yollar
yollar.yol_ekle()

KOK = yollar.ARA          # ortak veri koku - ai/yollar.py
ESLESME_M = 180          # iki hucre merkezi bu kadar yakinsa ayni yer sayilir
                         # (250 m hucrenin yari kosegeni 177 m)
AYNI_OLAY_GUN = 30       # bu kadar yakin tarihli + agir cakisan = tek olay
AYNI_OLAY_ORAN = 0.30
YENIDEN_MIN_GUN = 60     # ayni olayin artciklarini yeniden yanma sanmamak icin
PENCERE_GUN = 765        # 2. yil gozlem penceresinin sonu
YAKIN_GRUP_KM = 10       # cakismasa bile bu kadar yakin yanginlar tek grup


def metre_bin(lat, lon, lat0):
    """Yerel duzlemde yaklasik metre koordinati."""
    return np.column_stack([
        lon * 111_320.0 * np.cos(np.radians(lat0)),
        lat * 110_540.0,
    ])


class Birlestir:
    """union-find"""

    def __init__(self, ogeler):
        self.p = {o: o for o in ogeler}

    def bul(self, a):
        while self.p[a] != a:
            self.p[a] = self.p[self.p[a]]
            a = self.p[a]
        return a

    def birlesir(self, a, b):
        ra, rb = self.bul(a), self.bul(b)
        if ra != rb:
            self.p[max(ra, rb)] = min(ra, rb)


def main():
    df = pd.read_csv(KOK / "turkiye_grid.csv", low_memory=False)
    rapor = {"baslangic_satir": len(df)}
    print("=" * 78)
    print("VERI SETI TEMIZLIGI")
    print("=" * 78)
    print("  Girdi: {:,} satir, {} sutun\n".format(len(df), len(df.columns)))

    # ---------------------------------------------------------------- 1
    print("-" * 78)
    print("1. NODATA DUZELTMESI (arazi_kodu_y == 0)")
    print("-" * 78)
    nod = df["arazi_kodu_y"] == 0
    print("  Etkilenen satir: {:,} (%{:.1f})".format(nod.sum(), nod.mean() * 100))
    for yid, g in df[nod].groupby("yangin_id"):
        pay = len(g) / (df["yangin_id"] == yid).sum() * 100
        print("    {:<16}{:>6} satir  (%{:.1f})".format(yid, len(g), pay))
    df.loc[nod, ["arazi_kodu_y", "agac_orani_y"]] = np.nan
    df.loc[nod, "arazi_tipi_y"] = None
    rapor["nodata_satir"] = int(nod.sum())

    # --------------------------------------------------- yangin meta bilgi
    meta = (df.groupby("yangin_id")
              .agg(tarih=("yangin_tarihi", "first"),
                   lat=("lat", "mean"), lon=("lon", "mean"))
              .reset_index())
    meta["tarih"] = pd.to_datetime(meta["tarih"])
    yid_ler = meta["yangin_id"].tolist()
    tarih = dict(zip(meta["yangin_id"], meta["tarih"]))

    # her yangin icin metre koordinatli KD agaci
    lat0 = float(df["lat"].mean())
    agac, koord = {}, {}
    for yid, g in df.groupby("yangin_id"):
        xy = metre_bin(g["lat"].values, g["lon"].values, lat0)
        koord[yid] = xy
        agac[yid] = cKDTree(xy)

    kutu = {yid: (g.lat.min(), g.lat.max(), g.lon.min(), g.lon.max())
            for yid, g in df.groupby("yangin_id")}

    def kesisir(a, b):
        a1, a2, a3, a4 = kutu[a]
        b1, b2, b3, b4 = kutu[b]
        return not (a2 < b1 or b2 < a1 or a4 < b3 or b4 < a3)

    # ---------------------------------------------------- cakisma matrisi
    print("\n" + "-" * 78)
    print("2. CAKISMA ANALIZI")
    print("-" * 78)
    cakisma = {}
    for i in range(len(yid_ler)):
        for j in range(i + 1, len(yid_ler)):
            a, b = yid_ler[i], yid_ler[j]
            if not kesisir(a, b):
                continue
            d, _ = agac[b].query(koord[a], distance_upper_bound=ESLESME_M)
            ort = int(np.isfinite(d).sum())
            if ort > 0:
                oran = ort / min(len(koord[a]), len(koord[b]))
                cakisma[(a, b)] = (ort, oran)
    print("  Cakisan yangin cifti: {}".format(len(cakisma)))
    for (a, b), (o, r) in sorted(cakisma.items(), key=lambda x: -x[1][1])[:12]:
        gun = abs((tarih[a] - tarih[b]).days)
        print("    {} <-> {}: {:>5} hucre (%{:3.0f})  {} gun ara".format(
            a, b, o, r * 100, gun))

    # ---------------------------------------------------------------- 3
    print("\n" + "-" * 78)
    print("3. AYNI OLAY BIRLESTIRME")
    print("-" * 78)
    uf = Birlestir(yid_ler)
    ayni = []
    for (a, b), (o, r) in cakisma.items():
        gun = abs((tarih[a] - tarih[b]).days)
        if gun <= AYNI_OLAY_GUN and r >= AYNI_OLAY_ORAN:
            uf.birlesir(a, b)
            ayni.append((a, b, gun, round(r, 2)))
    if ayni:
        for a, b, gun, r in ayni:
            print("    {} + {}  ({} gun ara, %{:.0f} cakisma) -> tek olay".format(
                a, b, gun, r * 100))
    else:
        print("    yok")
    df["olay_id"] = df["yangin_id"].map(uf.bul)
    rapor["ayni_olay"] = [list(x) for x in ayni]
    print("  {} kayit -> {} olay".format(
        df["yangin_id"].nunique(), df["olay_id"].nunique()))

    # ---------------------------------------------------------------- 4
    print("\n" + "-" * 78)
    print("4. YENIDEN YANMA TESPITI")
    print("-" * 78)
    df["yeniden_yandi"] = False
    yanik_var = df["yanik"].fillna(False).astype(bool)
    for a in yid_ler:
        for b in yid_ler:
            if a == b or uf.bul(a) == uf.bul(b):
                continue
            gun = (tarih[b] - tarih[a]).days
            if not (YENIDEN_MIN_GUN < gun <= PENCERE_GUN):
                continue
            if not kesisir(a, b):
                continue
            mb = (df["yangin_id"] == b) & yanik_var
            if mb.sum() == 0:
                continue
            xy_b = metre_bin(df.loc[mb, "lat"].values,
                             df.loc[mb, "lon"].values, lat0)
            t = cKDTree(xy_b)
            ma = df["yangin_id"] == a
            d, _ = t.query(koord[a], distance_upper_bound=ESLESME_M)
            vur = np.isfinite(d)
            if vur.sum():
                idx = df.index[ma][vur]
                df.loc[idx, "yeniden_yandi"] = True
                print("    {} -> {} ({} gun sonra): {:>5} hucre".format(
                    a, b, gun, int(vur.sum())))
    kirli = df["yeniden_yandi"] & df["etiket_gecerli"].fillna(False)
    print("  Toplam isaretlenen: {:,} hucre".format(int(df["yeniden_yandi"].sum())))
    print("  Bunlardan ETIKETLI olan (etiketi iptal edilecek): {:,}".format(
        int(kirli.sum())))
    df.loc[df["yeniden_yandi"], "etiket_gecerli"] = False
    rapor["yeniden_yanan_etiketli"] = int(kirli.sum())

    # ---------------------------------------------------------------- 5
    print("\n" + "-" * 78)
    print("5. MEKANSAL GRUP KIMLIGI (GroupKFold icin)")
    print("-" * 78)
    for (a, b) in cakisma:
        uf.birlesir(a, b)
    mrk = dict(zip(meta["yangin_id"], zip(meta["lat"], meta["lon"])))
    for i in range(len(yid_ler)):
        for j in range(i + 1, len(yid_ler)):
            a, b = yid_ler[i], yid_ler[j]
            la, lo = mrk[a]
            lb, lb2 = mrk[b]
            dk = np.hypot((la - lb) * 110.54,
                          (lo - lb2) * 111.32 * np.cos(np.radians(la)))
            if dk < YAKIN_GRUP_KM:
                uf.birlesir(a, b)
    df["grup_id"] = df["yangin_id"].map(uf.bul)
    print("  {} yangin -> {} mekansal grup".format(
        df["yangin_id"].nunique(), df["grup_id"].nunique()))
    cok = df.groupby("grup_id")["yangin_id"].nunique()
    for gid, n in cok[cok > 1].items():
        uy = sorted(df.loc[df.grup_id == gid, "yangin_id"].unique())
        print("    {}: {}".format(gid, ", ".join(uy)))

    # ---------------------------------------------------------------- 6
    print("\n" + "-" * 78)
    print("6. AYNI OLAY ICINDE YINELENEN HUCRE")
    print("-" * 78)
    once = len(df)
    df["_snap"] = ((df["lat"] / 0.00225).round().astype(int).astype(str) + "_"
                   + (df["lon"] / 0.00281).round().astype(int).astype(str))
    # ayni olayda ayni yer: dnbr'si yuksek olani tut (asil yanan kayit)
    df = (df.sort_values("dnbr", ascending=False)
            .drop_duplicates(subset=["olay_id", "_snap"], keep="first")
            .drop(columns=["_snap"])
            .sort_values(["yangin_id", "hucre_id"])
            .reset_index(drop=True))
    print("  {:,} -> {:,} satir  ({:,} yinelenen atildi)".format(
        once, len(df), once - len(df)))
    rapor["yinelenen_atilan"] = once - len(df)

    df.to_csv(KOK / "turkiye_grid.csv", index=False, encoding="utf-8-sig")
    rapor["bitis_satir"] = len(df)
    rapor["olay"] = int(df["olay_id"].nunique())
    rapor["grup"] = int(df["grup_id"].nunique())
    (KOK / "temizlik_raporu.json").write_text(
        json.dumps(rapor, ensure_ascii=False, indent=2), encoding="utf-8")

    print("\n" + "=" * 78)
    print("SONUC")
    print("=" * 78)
    print("  Satir      : {:,} -> {:,}".format(rapor["baslangic_satir"], len(df)))
    print("  Etiketli   : {:,}".format(
        int(df["etiket_gecerli"].fillna(False).sum())))
    print("  Yangin     : {}".format(df["yangin_id"].nunique()))
    print("  Olay       : {}".format(df["olay_id"].nunique()))
    print("  Grup       : {}".format(df["grup_id"].nunique()))
    print("\n  -> turkiye_grid.csv, temizlik_raporu.json")
    print("  -> siradaki: python veri_son_hal.py  sonra  python egitim_seti.py")


if __name__ == "__main__":
    main()
