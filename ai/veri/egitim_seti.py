# -*- coding: utf-8 -*-
"""
Model icin hazir egitim seti
============================

turkiye_grid.csv ham veridir: filtresiz, butun sutunlar.
Bu script ondan MODELE HAZIR bir alt kume uretir ve secimlerin gerekcesini
belgeler.

Uc is yapar:

  1. FILTRE   - sadece guvenilir etiketli ve egitime uygun hucreler
  2. AGIRLIK  - grup dengesizligini veri ATARAK degil, agirlik vererek
                cozuyoruz. Tavan denendi ve zarar verdigi olculdu (asagida).
  3. OZNITELIK SECIMI - olculmus tutarliliga gore. Sizinti yapan sutunlar
                dislaniyor, katkisi olculemeyenler eleniyor.

ONEMLI - CAPRAZ DOGRULAMA:
    Grup anahtari yangin_id DEGIL grup_id'dir. Cakisan/bitisik yanginlar
    ayni gruba konur.

    Bolme sekli sonucu belirliyor (olculdu):
        grup_id ile        R2 +0.147   <- dogru, raporlanacak olan
        yangin_id ile      R2 +0.142
        rastgele KFold ile R2 +0.529   <- SAHTE, hucreler komsusuyla korele

    Rastgele KFold uc kat abartili sonuc verir. Kullanmayin.

Calistirma:  python egitim_seti.py
Cikti: egitim_seti.csv, egitim_seti_meta.json
"""

import json
import pathlib

import numpy as np
import pandas as pd

KOK = pathlib.Path(__file__).parent

# TAVAN - grup basina hucre siniri. VARSAYILAN: yok.
#
# Onceden 400'du. Amaci tek grubun veriyi ezmesini onlemekti (en buyuk grup
# tavansiz %37). Ama olctuk ve TERSI cikti - tavan zarar veriyor:
#
#   kurulum                     grup ici rho    en kotu grup    satir
#   tavan 400                      +0.468          -0.069        5.522
#   tavansiz                       +0.569          -0.007       16.074
#   tavansiz + grup agirligi       +0.490          +0.067       16.074
#
#   (3 tohum x 3 kat tekrarli, std 0.011-0.016)
#
# Sebep: degerlendirme zaten GRUP BASINA ayri ayri yapiliyor, yani buyuk
# grubun hakimiyeti metrigi sisiremiyor. Tavan sadece egitim verisini
# kirpiyordu. Dengeleme bir MODEL karari, veri karari degil - bu yuzden
# veriden satir atmak yerine grup_agirlik sutununu veriyoruz.
#
# Not: agirlik ortalamayi dusuruyor ama en kotu grubu duzeltiyor. Her yerde
# calismasi onemliyse agirligi kullanin, ortalama onemliyse kullanmayin.
TAVAN = None
TOHUM = 42

# --------------------------------------------------------------- oznitelikler
# Yanindaki sayilar temizlenmis veride, GRUP bazinda yapilan tutarlilik
# testinden. "24/27" = iliski 27 mekansal grubun 24'unde ayni yonde cikti.
SAYISAL = [
    # --- dogrulanmis: grup bazinda tutarli, p < 0.01 -------------------
    "agac_orani",        # WorldCover 2021 agac ortusu.   24/27, rho +0.542
    "agac_orani_y",      # Impact Observatory, yangindan onceki yil.
                         #   24/27, rho +0.556. Nodata duzeltmesinden once
                         #   +0.510 idi.
                         #   Ikisi TAMAMLAYICI: kismi korelasyon 0.303/0.218.
                         #   WorldCover makiyi agac sayiyor, IO saymiyor.
    "dnbr",              # yangin siddeti.                24/27, rho +0.371
    "egim_derece",       # 21/27, rho +0.262
    "yukselti_m",        # 20/27, rho +0.272

    "ndvi_dusus",        # = ndvi_oncesi - ndvi_sonrasi. Yangin hemen
                         #   ardindaki bitki kaybi. Tek basina grup ici
                         #   rho +0.594.
                         #
                         #   SIZINTI DEGIL: iki bilesen de yangindan ~2 hafta
                         #   sonra elde. Bilinmeyen ndvi_yil2 ve o hedefte.
                         #
                         #   Olculdu (HistGB, LOGO 27 grup):
                         #     6 oznitelik        +0.573  27/27  top20 45.9%
                         #     6 + ndvi_dusus     +0.659  27/27  top20 52.8%
                         #   en kotu grup da +0.104 -> +0.174 yukseldi.
                         #
                         #   dnbr ile ayni seyi olcuyor gibi durur ama
                         #   ustune +0.086 katiyor: dnbr kizilotesi yanik
                         #   izini, bu ise fiili bitki kaybini olcuyor.

    # --- zayif ama isaret tutarli: modele birakiyoruz ------------------
    "yol_mesafe_km",     # 20/27, rho +0.105
]

# Arazi sinif kodu ONCE oznitelik olarak dusunuldu, sonra ELENDI.
# Sayi olarak birakmak yanlisti (Otlak=11 > Agaclik=2 diye yapay siralama).
# Ikili sutunlara acip olctuk: 3 kat x 3 tohum tekrarli degerlendirmede
# katkisi TAM SIFIR cikti (R2 0.172 -> 0.173, grup ici rho 0.484 -> 0.484).
# Sebep: tasidigi bilgi zaten agac_orani ve agac_orani_y icinde.
# Sutunlar veride referans olarak duruyor, modele girmiyor.
ARAZI_IKILI = ["arazi_agaclik", "arazi_otlak", "arazi_diger"]

OZNITELIKLER = SAYISAL

# Olculup ELENEN oznitelikler ve sebebi. Veride duruyorlar, modele girmiyorlar.
ELENEN = {
    "arazi_kodu_y / arazi ikili sutunlari":
        "Bitki ortusu SINIFI (orman / calilik / otlak / tarim). Uc yoldan "
        "olculdu, ucu de sifir ya da eksi (7 oznitelik + HistGB, LOGO):\n"
        "      taban                          +0.6588\n"
        "      + ikili sutunlar               +0.6554  (-0.0034)\n"
        "      + kategorik sinif              +0.6556  (-0.0032)\n"
        "      + sadece 'agaclik mi'          +0.6593  (+0.0005)\n"
        "    Sebep: agac_orani_y zaten sinifin SUREKLI hali. Sinif bazinda "
        "ortalamasi Agaclik 0.933, Otlak/calilik 0.166, Tarim 0.066 - yani "
        "sinif, elimizde zaten olan bir degiskenin kaba kutulanmis hali.\n"
        "    KESIN KANIT: agac_orani sutunlarini tamamen cikarip yerine "
        "sinifi koyunca da hicbir sey degismiyor (+0.6346 -> +0.6329). "
        "Sinif tek basina bile bilgi tasimiyor; agac_orani ise +0.024 katiyor.\n"
        "    Ek sebep: yangin ICINDE baskin sinifin payi ortalama %85.8, "
        "yani sinif yangin icinde neredeyse sabit. Metrigimiz grup ici "
        "oldugu icin sabit sutun katki veremez.\n"
        "    Ekolojik sezgi ('makilik dipten surer, kizilcam surmez') DOGRU "
        "ama o bilgi zaten agac_orani icinde yakalaniyor.\n"
        "    Veride referans olarak duruyor, arayuzde land_cover diye "
        "gosteriliyor; modele girmiyor.",
    "ndvi_oncesi":
        "REDDEDILDI - taftolojik. Hedef = ndvi_oncesi - ndvi_yil2, yani "
        "oznitelik hedefin icinde. Tek basina grup ici rho +0.679; modelle "
        "birlikte +0.675, yani MODELIN KATKISI -0.004. Modeli silip hucreleri "
        "yangin oncesi NDVI'ye gore siralasak ayni sonuc cikiyor. "
        "'Once cok bitki vardi, o yuzden acik buyuk' bir tahmin degil aritmetik. "
        "Ayrica ndvi_dusus ile birlikte konunca en kotu grup +0.174 -> -0.030 "
        "duserek 27/27 tutarliligi bozuyor.",
    "baki_derece": "Ham derece +0.006, sin/cos donusumuyle -0.005. Ikisi de "
                   "gurultu araliginda. Dairesel olma sorunu degilmis, "
                   "gercekten sinyal yok.",
    "yagis_uzun_ort_mm": "Zararli: -0.110. Sebep olculdu - yangin ICINDEKI "
                         "degisimi 0.076, yani neredeyse sabit. Metrik grup "
                         "ici oldugu icin sabit sutun bilgi tasimaz, sadece "
                         "modele ezberlenecek gurultu verir.",
    "yagis_sonrasi_2yil_mm / yagis_anomali":
        "SIZINTI - yangindan sonraki 2 yilin yagisi tahmin aninda bilinmiyor. "
        "Olculdu ve zaten zarar veriyor (-0.085 / -0.030), ama esas sebep "
        "operasyonel: 2 hafta sonra karar verecegiz, gelecegi bilmiyoruz.",
    "yerlesim_mesafe_km": "Temiz veride 19/27, p=0.052, rho=+0.126. Sinirda ve "
                          "yol_mesafe_km ile %32 korele. Geri konarak olculdu: "
                          "R2 -0.017, grup ici rho +0.009. Katki yok.",
    "su_mesafe_km": "13/27, p=1.00. Yazi-tura. Geri konarak olculdu: "
                    "R2 -0.044, rho +0.006.",
    "baki_derece": "rho=-0.061. Isaret tutarli ama buyukluk yok. Geri konarak "
                   "olculdu: R2 +0.014, rho +0.000 - gurultu sinirinda.",
    "yagis_anomali": "Yangin duzeyinde test edildi (n=35): rho=+0.087, p=0.62. "
                     "Agac orani sabitlenince de +0.095. Hicbir sey katmiyor.",
    "yagis_uzun_ort_mm": "rho=+0.466 ama agac oraninin vekili: yagisli yerde "
                         "daha cok orman var.",
    "modis_alan_ha": "Yangin basina sabit. Grid'de bilgi tasimaz.",
    "bolge": "Ayni bolge icinde yagis 4 kat degisebiliyor (Rize 2.077 mm, "
             "Corum 511 mm). Kategorik bolge kotu bir vekil. Haritada filtre "
             "olarak kullanilir, modele girmez.",
    "yil": "Modele KONMAMALI. 2021 verinin %70'i; model yili ezberler. "
           "Yil etkisi degerlendirmede ele alinir, oznitelik olarak degil.",
}

# SIZINTI: hedefin kendisinin parcasi ya da tahmin aninda mevcut olmayan
# sutunlar. Kesinlikle oznitelik olarak kullanilmamali.
# Olcut TEK: tahmin aninda (yangindan ~2 hafta sonra) bu deger elimizde
# olur mu? Olmuyorsa sizinti. "Hedefle ortak terim tasimak" tek basina
# sizinti degildir - ndvi_dusus bunun ornegi, asagida ayrica anlatiliyor.
SIZINTI = {
    "ndvi_yil2": "Hedefin bileseni. Tahmin edilecek gelecegin ta kendisi.",
    "ndvi_yil1": "Yangindan 1 yil sonra olculuyor; tahmin aninda henuz yok.",
    "iyilesme_yil2": "Alternatif hedef tanimi, ayni gelecek bilgisi.",
    "yagis_sonrasi_2yil_mm": "Yangindan sonraki 2 yilin yagisi; tahmin aninda "
                             "bilinmiyor.",
    "yagis_anomali": "Ayni sebep - gelecek yagisa dayaniyor.",
    "siddet_sinifi": "dnbr'den turetilmis, ayni bilgi. Sizinti degil ama "
                     "gereksiz.",
}

# ndvi_oncesi tahmin aninda ELIMIZDE, yani teknik olarak sizinti degil.
# Yine de reddedildi: taftolojik oldugu icin. Gerekcesi ELENEN sozlugunde.
#
# ndvi_sonrasi ve dndvi de tahmin aninda elimizde. Kullanmiyoruz cunku
# ndvi_dusus ikisinin tasidigi bilgiyi zaten iceriyor (dndvi ile ndvi_dusus
# olculdugunde birebir ayni sonucu verdi: +0.6588 / 27-27 / top20 %52.8).

HEDEF = "kalan_acik"       # yangin oncesi NDVI - 2. yil NDVI
GRUP = "grup_id"           # GroupKFold anahtari - yangin_id DEGIL


def arazi_ikili(df):
    """Arazi sinif kodunu ikili sutunlara ac. Nodata NaN olarak kalir."""
    k = df["arazi_kodu_y"]
    bilinmiyor = k.isna()
    df["arazi_agaclik"] = np.where(bilinmiyor, np.nan, (k == 2).astype(float))
    df["arazi_otlak"] = np.where(bilinmiyor, np.nan, (k == 11).astype(float))
    df["arazi_diger"] = np.where(bilinmiyor, np.nan,
                                 (~k.isin([2, 11])).astype(float))
    return df


def main():
    ham = pd.read_csv(KOK / "turkiye_grid.csv", low_memory=False)
    print("=" * 78)
    print("EGITIM SETI URETIMI")
    print("=" * 78)
    print("  Ham veri: {:,} satir, {} sutun".format(len(ham), len(ham.columns)))

    if GRUP not in ham.columns:
        raise SystemExit("grup_id yok. Once 'python veri_temizle.py' calistir.")

    # 1) filtre
    s = ham[ham["etiket_gecerli"].fillna(False)
            & ham["egitime_uygun"].fillna(False)].copy()
    print("  Etiketli + uygun: {:,} satir, {} yangin, {} grup".format(
        len(s), s["yangin_id"].nunique(), s[GRUP].nunique()))

    # 2) arazi ikili sutunlari (referans; oznitelik listesinde yok)
    s = arazi_ikili(s)

    # 3) tavan (varsayilan: yok) + grup agirligi
    if TAVAN is not None:
        once_pay = s.groupby(GRUP).size().max() / len(s)
        s = (s.groupby(GRUP, group_keys=False)[s.columns]
              .apply(lambda g: g.sample(min(len(g), TAVAN), random_state=TOHUM)))
        print("  Tavan {} uygulandi: {:,} satir (en buyuk grup %{:.1f} -> %{:.1f})"
              .format(TAVAN, len(s), once_pay * 100,
                      s.groupby(GRUP).size().max() / len(s) * 100))
    else:
        print("  Tavan yok - butun uygun hucreler kullaniliyor")

    # her grup toplam agirlikta esit olsun: w = N / (grup_sayisi * grup_boyutu)
    n_grup = s[GRUP].nunique()
    boyut = s.groupby(GRUP)[HEDEF].transform("size")
    s["grup_agirlik"] = len(s) / (n_grup * boyut)
    print("    en buyuk grup payi : %{:.1f}  (agirlikla %{:.1f})".format(
        s.groupby(GRUP).size().max() / len(s) * 100, 100 / n_grup))
    print("    2021 payi          : %{:.1f}".format((s["yil"] == 2021).mean() * 100))

    # 4) eksik deger
    kullan = [k for k in OZNITELIKLER if k in s.columns]
    eksik = s[kullan + [HEDEF]].isna().sum()
    if eksik.any():
        print("\n  Eksik deger (NaN olarak birakiliyor, agac modelleri kaldirir):")
        for k, v in eksik[eksik > 0].items():
            print("    {:<22}{:>6}".format(k, v))
    # hedefi eksik olan satir ise kullanilamaz
    s = s.dropna(subset=[HEDEF])

    # kimlik + referans sutunlari (modele girmez, harita/analiz icin durur)
    kimlik = [GRUP, "yangin_id", "olay_id", "hucre_id", "bolge", "yil",
              "yangin_tarihi", "lat", "lon", "arazi_kodu_y", "arazi_tipi_y"]
    kolonlar = ([c for c in kimlik if c in s.columns] + kullan
                + [c for c in ARAZI_IKILI if c in s.columns]
                + ["grup_agirlik", HEDEF])
    egitim = s[kolonlar].reset_index(drop=True)
    egitim.to_csv(KOK / "egitim_seti.csv", index=False, encoding="utf-8-sig")

    # ------------------------------------------------------------- rapor
    print("\n" + "=" * 78)
    print("SONUC")
    print("=" * 78)
    print("  Satir      : {:,}".format(len(egitim)))
    print("  Oznitelik  : {}".format(len(kullan)))
    print("  Grup       : {} mekansal grup ({} yangin)".format(
        egitim[GRUP].nunique(), egitim["yangin_id"].nunique()))
    print("  Hedef      : {}  ort={:.3f} std={:.3f}".format(
        HEDEF, egitim[HEDEF].mean(), egitim[HEDEF].std()))

    g = egitim.groupby(GRUP).size()
    print("\n  Grup buyuklugu: min {} | medyan {} | maks {}".format(
        g.min(), int(g.median()), g.max()))

    print("\n  Yila gore:")
    for y, gg in egitim.groupby("yil"):
        print("    {}: {:>2} grup, {:>5,} satir (%{:.0f})".format(
            y, gg[GRUP].nunique(), len(gg), len(gg) / len(egitim) * 100))

    print("\n  Bolgeye gore:")
    for b, gg in egitim.groupby("bolge"):
        print("    {:<14}{:>3} grup, {:>5,} satir".format(
            b, gg[GRUP].nunique(), len(gg)))

    print("\n  Oznitelikler ({}):".format(len(kullan)))
    for k in kullan:
        print("    {}".format(k))
    print("\n  Elenen ({}): {}".format(len(ELENEN), ", ".join(ELENEN)))
    print("  Sizinti sayilan ({}): {}".format(len(SIZINTI), ", ".join(SIZINTI)))

    meta = {
        "satir": len(egitim),
        "grup": int(egitim[GRUP].nunique()),
        "yangin": int(egitim["yangin_id"].nunique()),
        "hedef": HEDEF, "grup_anahtari": GRUP,
        "tavan": TAVAN, "tohum": TOHUM,
        "agirlik_sutunu": "grup_agirlik",
        "oznitelikler": kullan,
        "elenen": ELENEN, "sizinti": SIZINTI,
        "hedef_istatistik": {
            "ortalama": float(egitim[HEDEF].mean()),
            "std": float(egitim[HEDEF].std()),
            "min": float(egitim[HEDEF].min()),
            "maks": float(egitim[HEDEF].max())},
        "yil_dagilimi": {int(k): int(v) for k, v in
                         egitim.groupby("yil").size().items()},
        "birincil_metrik": "grup ici Spearman (siralama), R2 ikincil",
    }
    (KOK / "egitim_seti_meta.json").write_text(
        json.dumps(meta, ensure_ascii=False, indent=2), encoding="utf-8")
    print("\n  -> egitim_seti.csv, egitim_seti_meta.json")


if __name__ == "__main__":
    main()
