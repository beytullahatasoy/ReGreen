# -*- coding: utf-8 -*-
"""
Son denetim - veri setinin ucdan uca bagimsiz kontrolu
======================================================

Hicbir seyi varsaymaz, her seyi yeniden hesaplar. Her kontrol GECTI/KALDI
olarak isaretlenir. Sunum oncesi ve veriye her dokunuldugunda calistirilmali.

Calistirma:  python son_denetim.py
"""

import json
import pathlib
import sys

import numpy as np
import pandas as pd

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent))
import yollar
yollar.yol_ekle()

KOK = yollar.ARA          # ortak veri koku - ai/yollar.py
TR_SINIR = (25.5, 35.7, 45.0, 42.4)     # lon_min, lat_min, lon_max, lat_max

gecti, kaldi, uyari = [], [], []


def kontrol(ad, sonuc, detay=""):
    (gecti if sonuc else kaldi).append(ad)
    print("  [{}] {:<52}{}".format("OK" if sonuc else "!!", ad, detay))


def not_et(ad, detay=""):
    uyari.append(ad)
    print("  [ i] {:<52}{}".format(ad, detay))


def baslik(t):
    print("\n" + "-" * 78)
    print(t)
    print("-" * 78)


def main():
    yollar.gerekli(KOK / "turkiye_grid.csv", KOK / "egitim_seti.csv",
                   KOK / "egitim_seti_meta.json", KOK / "yangin_ozeti.csv")
    print("=" * 78)
    print("SON DENETIM")
    print("=" * 78)

    ham = pd.read_csv(KOK / "turkiye_grid.csv", low_memory=False)
    eg = pd.read_csv(KOK / "egitim_seti.csv", encoding="utf-8-sig")
    meta = json.loads((KOK / "egitim_seti_meta.json").read_text(encoding="utf-8"))
    ozet = pd.read_csv(KOK / "yangin_ozeti.csv")
    OZ, H, G = meta["oznitelikler"], meta["hedef"], meta["grup_anahtari"]

    # ------------------------------------------------------------ 1 dosya
    baslik("1. DOSYA VE OKUNABILIRLIK")
    kontrol("turkiye_grid.csv okunuyor", True, "{:,} satir".format(len(ham)))
    kontrol("egitim_seti.csv okunuyor", True, "{:,} satir".format(len(eg)))
    # BOM tuzagi: varsayilan encoding ile ilk sutun adi bozuluyor mu
    yalin = pd.read_csv(KOK / "egitim_seti.csv", nrows=1)
    bom = yalin.columns[0].startswith("﻿")
    kontrol("BOM'suz okunabiliyor (pd.read_csv varsayilan)", not bom,
            "ilk sutun: {!r}".format(yalin.columns[0]))

    # ------------------------------------------------------- 2 alt kume mi
    baslik("2. EGITIM SETI HAM VERININ ALT KUMESI MI")
    a = set(zip(eg["yangin_id"], eg["hucre_id"]))
    b = set(zip(ham["yangin_id"], ham["hucre_id"]))
    kontrol("her egitim satiri ham veride var", a <= b,
            "{} eslesmeyen".format(len(a - b)))
    uygun = ham[ham["etiket_gecerli"].fillna(False)
                & ham["egitime_uygun"].fillna(False)]
    kontrol("egitim seti = etiketli & uygun kumesi", len(eg) == len(uygun),
            "{:,} vs {:,}".format(len(eg), len(uygun)))

    # ---------------------------------------------------------- 3 kimlikler
    baslik("3. KIMLIK VE BENZERSIZLIK")
    kontrol("hucre_id benzersiz (ham)",
            not ham.duplicated(["yangin_id", "hucre_id"]).any())
    kontrol("tam yinelenen satir yok (egitim)", not eg.duplicated().any())
    snap = ((eg["lat"] / 0.00225).round().astype(int).astype(str) + "_"
            + (eg["lon"] / 0.00281).round().astype(int).astype(str))
    cok = eg.assign(s=snap).groupby("s")[G].nunique()
    kontrol("ayni yer birden fazla grupta degil", (cok > 1).sum() == 0,
            "{} yer".format(int((cok > 1).sum())))
    ic = eg.assign(s=snap).duplicated(["s", G]).sum()
    kontrol("grup icinde ayni yer tekrar etmiyor", ic == 0, "{} tekrar".format(ic))

    # ------------------------------------------------------ 4 hedef dogrulugu
    baslik("4. HEDEF DEGISKENIN YENIDEN HESABI")
    u = ham[ham["etiket_gecerli"].fillna(False)]
    yeniden = u["ndvi_oncesi"] - u["ndvi_yil2"]
    fark = (yeniden - u[H]).abs().max()
    kontrol("kalan_acik == ndvi_oncesi - ndvi_yil2", fark < 1e-6,
            "maks fark {:.2e}".format(fark))
    kontrol("hedefte NaN yok (egitim)", eg[H].isna().sum() == 0)
    kontrol("hedef makul aralikta (-0.2, 1.5)",
            eg[H].between(-0.2, 1.5).all(),
            "min {:.3f} maks {:.3f}".format(eg[H].min(), eg[H].max()))
    neg = (eg[H] < 0).sum()
    if neg:
        not_et("hedefi negatif olan hucre", "{} adet - yangindan sonra "
               "ONCEKINDEN yesil; nadir ama fiziksel olarak mumkun".format(neg))

    # -------------------------------------------------- 5 etiket kurallari
    baslik("5. ETIKET KURALLARININ TUTARLILIGI")
    kontrol("etiketli hucrelerin hepsi yanik",
            bool(u["yanik"].fillna(False).all()))
    kontrol("etiketli hucrelerde dnbr >= 0.27", (u["dnbr"] >= 0.27).all(),
            "min {:.3f}".format(u["dnbr"].min()))
    kontrol("etiketli hucrelerde ndvi_dusus >= 0.20",
            (u["ndvi_dusus"] >= 0.20 - 1e-9).all(),
            "min {:.3f}".format(u["ndvi_dusus"].min()))
    kontrol("yeniden yanan hucre etiketli degil",
            not (ham["yeniden_yandi"] & ham["etiket_gecerli"].fillna(False)).any())
    kontrol("etiketli hucrelerde bitki vardi",
            bool(u["bitki_vardi"].fillna(False).all()))

    # ------------------------------------------------------- 6 fiziksel akil
    baslik("6. FIZIKSEL AKIL KONTROLU")
    lo1, la1, lo2, la2 = TR_SINIR
    kontrol("tum hucreler Turkiye kutusunda",
            bool(eg["lon"].between(lo1, lo2).all()
                 and eg["lat"].between(la1, la2).all()))
    kontrol("yukselti 0-3000 m", bool(eg["yukselti_m"].dropna()
                                      .between(0, 3000).all()),
            "maks {:.0f} m".format(eg["yukselti_m"].max()))
    kontrol("egim 0-70 derece", bool(eg["egim_derece"].between(0, 70).all()),
            "maks {:.1f}".format(eg["egim_derece"].max()))
    kontrol("agac orani 0-1", bool(eg["agac_orani"].between(0, 1).all()))
    kontrol("dnbr makul (0.27-2.0)", bool(eg["dnbr"].between(0.27, 2.0).all()),
            "maks {:.2f}".format(eg["dnbr"].max()))
    kontrol("mesafeler pozitif", bool((eg["yol_mesafe_km"] > 0).all()))
    t = pd.to_datetime(ham["yangin_tarihi"])
    kontrol("yangin tarihleri 2017-2024", bool(t.dt.year.between(2017, 2024).all()))
    kontrol("yangin tarihleri yangin mevsiminde (4-12. ay)",
            bool(t.dt.month.between(4, 12).all()),
            "aylar {}".format(sorted(t.dt.month.unique())))

    # ------------------------------------------------------ 7 oznitelikler
    baslik("7. OZNITELIK SAGLIGI")
    kontrol("meta'daki oznitelikler CSV'de var",
            all(k in eg.columns for k in OZ))
    siz = [k for k in meta["sizinti"] if k in eg.columns]
    kontrol("sizinti sutunu egitim setinde yok", not siz, str(siz))
    for k in ("lat", "lon", "yil", "bolge"):
        if k in OZ:
            kaldi.append("konum/yil oznitelik olmus: " + k)
    kontrol("lat/lon/yil/bolge oznitelik degil",
            not any(k in OZ for k in ("lat", "lon", "yil", "bolge")))
    sabit = [k for k in OZ if eg[k].nunique() <= 1]
    kontrol("sabit oznitelik yok", not sabit, str(sabit))
    kor = eg[OZ].corr(method="spearman").abs()
    np.fill_diagonal(kor.values, 0)
    kontrol("oznitelikler arasi |rho| < 0.9", bool((kor < 0.9).all().all()),
            "maks {:.3f}".format(kor.max().max()))

    # ------------------------------------------------------------ 8 gruplar
    baslik("8. GRUP YAPISI")
    g = eg.groupby(G).size()
    kontrol("grup sayisi >= 10 (5 katli CV icin)", eg[G].nunique() >= 10,
            "{} grup".format(eg[G].nunique()))
    kontrol("her grupta >= 20 satir", bool((g >= 20).all()),
            "min {}".format(g.min()))
    tekil = [k for k, gg in eg.groupby(G) if gg[H].nunique() < 5]
    kontrol("her grupta hedef degisken", not tekil, str(tekil[:3]))
    w = eg.groupby(G)["grup_agirlik"].sum()
    kontrol("grup agirliklari esitleniyor",
            bool(np.allclose(w, w.iloc[0], rtol=1e-6)),
            "her grup {:.1f}".format(w.iloc[0]))
    kontrol("agirlik toplami = satir sayisi",
            abs(eg["grup_agirlik"].sum() - len(eg)) < 1,
            "{:.0f}".format(eg["grup_agirlik"].sum()))

    # --------------------------------------------------------- 9 tutarlilik
    baslik("9. BELGE / META TUTARLILIGI")
    kontrol("meta satir sayisi CSV ile ayni", meta["satir"] == len(eg),
            "{} vs {}".format(meta["satir"], len(eg)))
    kontrol("meta grup sayisi CSV ile ayni", meta["grup"] == eg[G].nunique())
    kontrol("ozet dosyasindaki uygun yangin sayisi tutuyor",
            int(ozet["egitime_uygun"].sum()) == eg["yangin_id"].nunique(),
            "{} vs {}".format(int(ozet["egitime_uygun"].sum()),
                              eg["yangin_id"].nunique()))
    sz = KOK / "VERI_SOZLUGU.md"
    if sz.exists():
        m = sz.read_text(encoding="utf-8")
        kontrol("veri sozlugunde guncel satir sayisi yaziyor",
                "{:,}".format(len(eg)).replace(",", ".") in m,
                "{:,}".format(len(eg)).replace(",", "."))

    # ---------------------------------------------------------- 10 dagilim
    baslik("10. DAGILIM (bilgi - kontrol degil)")
    not_et("2021 payi", "%{:.0f}".format((eg["yil"] == 2021).mean() * 100))
    not_et("en buyuk grup payi",
           "%{:.1f} ({})".format(g.max() / len(eg) * 100, g.idxmax()))
    not_et("bolge dagilimi", ", ".join(
        "{} {}".format(b, gg[G].nunique()) for b, gg in eg.groupby("bolge")))
    ss_b = ((eg.groupby(G)[H].mean() - eg[H].mean()) ** 2 * g).sum()
    ss_t = ((eg[H] - eg[H].mean()) ** 2).sum()
    not_et("gruplar arasi varyans payi", "%{:.0f}".format(ss_b / ss_t * 100))

    # ------------------------------------------------------------- sonuc
    print("\n" + "=" * 78)
    print("SONUC")
    print("=" * 78)
    print("  Gecen kontrol : {}".format(len(gecti)))
    print("  Kalan kontrol : {}".format(len(kaldi)))
    print("  Bilgi notu    : {}".format(len(uyari)))
    if kaldi:
        print("\n  BASARISIZ:")
        for k in kaldi:
            print("    - {}".format(k))
        print("\n  >>> VERI SETI HAZIR DEGIL")
        return 1
    print("\n  >>> BUTUN KONTROLLER GECTI - veri seti kullanima hazir")
    return 0


if __name__ == "__main__":
    sys.exit(main())
