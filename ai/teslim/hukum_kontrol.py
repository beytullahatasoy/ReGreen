# -*- coding: utf-8 -*-
"""
Hukum ve ozet katmani kontrolu
==============================

Kural tabanli katmani secmemizin gerekcesi test edilebilir olmasiydi; bu
dosya o sozu tutuyor. Dis bagimlilik yok, teslim_onkontrol.py ile ayni
kalipta calisiyor:

    python ai/teslim/hukum_kontrol.py

Kontroller:
  1   kapsama          her hucre tam olarak bir hukum aliyor mu
  2   olu kural        her hukum gercek veride en az bir kez tetikleniyor mu
  3   bos slot         doldurulmamis {yer_tutucu} kalmis mi
  3b  notun yeri       zamanlama notu sozlukte tek kopya mi
  4   tekrarlanabilir  ayni girdi iki kez -> ayni cikti
  5   sinir            esik degerlerinde dogru tarafa dusuyor mu
  6   sira             KAPSAM_DISI digerlerini yeniyor mu
  7   altin ornek      elle secilmis hucreler beklenen hukmu aliyor mu
  8   tur kapsami      tur onerisi yalnizca mudahale hukumlerinde cikiyor mu
  9   oran araligi     toparlanma orani 0-1 disina cikmiyor mu
  10  ozet kapsami     her yangin icin paragraf var mi
  11  paragraf bicimi  None / nan / bos slot / cift bosluk var mi
  12  sayi tutarliligi paragraftaki sayilar sayi bloguyla ayni mi
  13  guven uyarisi    dusuk guvenli yanginlar uyari tasiyor mu
  14  uydurma sayi     paragraftaki sayimlar sayi blogundan turuyor mu
"""

import json
import pathlib
import sys

import numpy as np
import pandas as pd

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent))
import yollar
yollar.yol_ekle()
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import hukum as H

sys.stdout.reconfigure(encoding="utf-8")

SONUC = []


def kontrol(no, ad, gecti, notu=""):
    SONUC.append((no, ad, bool(gecti), notu))


def _temel(**ek):
    """Varsayilan saglikli bir hucre; testler uzerine yazar."""
    s = {
        "cell_id": "TEST_000001", "land_cover": "Agaclik",
        "prediction_status": "predicted", "ndvi_before": 0.60,
        "recovery_gap_pred": 0.30, "slope_deg": 12.0, "elevation_m": 400.0,
        "road_distance_km": 0.5, "tree_cover": 0.90,
        "burn_severity_dnbr": 0.40, "severity_class": "orta-dusuk",
    }
    s.update(ek)
    return s


def _hukum(s):
    oran = H.toparlanma_orani(s["ndvi_before"], s["recovery_gap_pred"])
    return H.hukum_sec(s, oran), oran


def main():
    kok = yollar.TESLIM
    with open(kok / "manifest.json", encoding="utf-8") as f:
        man = json.load(f)
    bolge = {y["fire_id"]: y.get("region") for y in man["fires"]}
    cumleler, ortam = H.metinleri_yukle()

    # ---------------------------------------------------- gercek veri uzerinde
    parcalar, kaynak = [], []
    for fid in sorted(bolge):
        f = kok / ("%s_hucreler.csv" % fid)
        if not f.exists():
            continue
        d = pd.read_csv(f)
        parcalar.append(H.hukum_uret(d, bolge[fid], cumleler, ortam))
        kaynak.append(d)
    h = pd.concat(parcalar, ignore_index=True)
    ham = pd.concat(kaynak, ignore_index=True)
    n = len(ham)

    # 1 kapsama
    eksik = int(h.hukum.isna().sum() + (h.hukum == "").sum())
    bos_ozet = int(h.ozet.isna().sum() + (h.ozet.str.strip() == "").sum())
    kontrol(1, "Her hucre tam olarak bir hukum aliyor",
            len(h) == n and eksik == 0 and bos_ozet == 0,
            "hucre=%d hukum=%d eksik=%d bos_ozet=%d" % (n, len(h), eksik, bos_ozet))

    # 2 olu kural
    olu = [k for k in H.HUKUM_SIRASI if (h.hukum == k).sum() == 0]
    kontrol(2, "Her hukum en az bir kez tetikleniyor", not olu,
            "tetiklenmeyen: %s" % olu)

    # 3 bos slot - hucre metinleri ve sozlukteki sabit metinler
    kalan = []
    for kol in ("ozet", "ayrinti"):
        s = h[kol].dropna().astype(str)
        kotu = s[s.str.contains("{", regex=False)]
        if len(kotu):
            kalan.append("%s:%d" % (kol, len(kotu)))
    sozluk = H.sozluk_uret(cumleler, ortam)
    if "{" in (sozluk.get("zamanlama_notu") or ""):
        kalan.append("sozluk:zamanlama_notu")
    kontrol(3, "Doldurulmamis yer tutucu yok", not kalan, "; ".join(kalan))

    # 3b zamanlama notu sozlukte tek kopya, hucrede bayrak olarak
    bayrak = h.zamanlama_notu_var
    kontrol("3b", "Zamanlama notu sozlukte, hucrede bayrak",
            bool(sozluk.get("zamanlama_notu")) and bayrak.any() and not bayrak.all(),
            "notu olan hucre: %d / %d" % (int(bayrak.sum()), len(h)))

    # 4 tekrarlanabilirlik
    d0 = pd.read_csv(kok / ("%s_hucreler.csv" % sorted(bolge)[0]))
    a = H.hukum_uret(d0, bolge[sorted(bolge)[0]], cumleler, ortam)
    b = H.hukum_uret(d0, bolge[sorted(bolge)[0]], cumleler, ortam)
    kontrol(4, "Ayni girdi iki kez -> ayni cikti", a.equals(b))

    # 5 sinir degerleri
    # toparlanma orani = (ndvi_before - gap) / ndvi_before
    # ndvi_before = 1.0 secersek oran = 1 - gap, esiklere dogrudan oturuyor
    sinir = []
    for gap, bekle in [(1 - H.TOPARLANMA_ZAYIF + 0.01, "DIKIM_ADAYI"),   # oran < 0.35
                       (1 - H.TOPARLANMA_ZAYIF, "ONCELIGE_GORE"),        # oran = 0.35
                       (1 - H.TOPARLANMA_IYI + 0.01, "ONCELIGE_GORE"),   # oran < 0.50
                       (1 - H.TOPARLANMA_IYI, "GENCLESME_IZLE")]:        # oran = 0.50
        k, oran = _hukum(_temel(ndvi_before=1.0, recovery_gap_pred=gap))
        if k != bekle:
            sinir.append("oran=%.3f -> %s (beklenen %s)" % (oran, k, bekle))
    # egim esigi: tam 25 derece dik sayilmali
    k, _ = _hukum(_temel(ndvi_before=1.0, recovery_gap_pred=0.70,
                         slope_deg=H.EGIM_DIK))
    if k != "EROZYON_ONCE":
        sinir.append("egim=%.0f -> %s (beklenen EROZYON_ONCE)" % (H.EGIM_DIK, k))
    k, _ = _hukum(_temel(ndvi_before=1.0, recovery_gap_pred=0.70,
                         slope_deg=H.EGIM_DIK - 0.1))
    if k != "DIKIM_ADAYI":
        sinir.append("egim=%.1f -> %s (beklenen DIKIM_ADAYI)" % (H.EGIM_DIK - 0.1, k))
    kontrol(5, "Esik sinirlarinda dogru tarafa dusuyor", not sinir, "; ".join(sinir))

    # 6 sira: kapsam disi arazi her seyi yener
    sira = []
    for durum in ("predicted", "low_severity", "no_data"):
        k, _ = _hukum(_temel(land_cover="Tarim", prediction_status=durum))
        if k != "KAPSAM_DISI":
            sira.append("Tarim+%s -> %s" % (durum, k))
    k, _ = _hukum(_temel(prediction_status="no_data"))
    if k != "SAHA_KONTROL":
        sira.append("no_data -> %s" % k)
    k, _ = _hukum(_temel(prediction_status="low_severity"))
    if k != "IZLE":
        sira.append("low_severity -> %s" % k)
    kontrol(6, "Hukum sirasi dogru uygulaniyor", not sira, "; ".join(sira))

    # 7 altin ornekler - gercek hucreler, elle dogrulanmis
    birlesik = ham.merge(h, on="cell_id")
    altin = {
        "AKD_2021_01_025260": "EROZYON_ONCE",   # egim 35.4, toparlanma 0.22
        "AKD_2021_01_025751": "ONCELIGE_GORE",  # toparlanma 0.45
        "AKD_2021_01_011955": "SAHA_KONTROL",   # no_data
        "AKD_2020_01_003751": "KAPSAM_DISI",    # yerlesim
        "AKD_2017_01_000884": "IZLE",           # low_severity
    }
    yanlis = []
    for cid, bekle in altin.items():
        r = birlesik[birlesik.cell_id == cid]
        if not len(r):
            yanlis.append("%s bulunamadi" % cid)
        elif r.hukum.iloc[0] != bekle:
            yanlis.append("%s -> %s (beklenen %s)" % (cid, r.hukum.iloc[0], bekle))
    kontrol(7, "Altin ornekler beklenen hukmu aliyor", not yanlis, "; ".join(yanlis))

    # 8 tur onerisi kapsami
    turlu = h[h.tur_onerisi.notna()]
    disari = set(turlu.hukum.unique()) - {"EROZYON_ONCE", "DIKIM_ADAYI"}
    kontrol(8, "Tur onerisi yalnizca mudahale hukumlerinde", not disari,
            "sizan hukumler: %s" % disari)

    # 9 oran araligi
    o = h.toparlanma_orani.dropna()
    kontrol(9, "Toparlanma orani 0-1 araliginda",
            len(o) and o.min() >= 0 and o.max() <= 1,
            "min=%.3f maks=%.3f" % (o.min(), o.max()) if len(o) else "hic deger yok")

    # ------------------------------------------------- Katman 1: yangin ozeti
    oz_f = kok / "yangin_ozetleri.json"
    mt_f = kok / "yangin_metinleri.json"
    if oz_f.exists() and mt_f.exists():
        with open(oz_f, encoding="utf-8") as f:
            ozetler = json.load(f)["yanginlar"]
        with open(mt_f, encoding="utf-8") as f:
            metinler = json.load(f)["yanginlar"]

        eksik = [f for f in bolge if f not in metinler]
        kontrol(10, "Her yangin icin ozet paragrafi var",
                not eksik and len(metinler) == len(bolge),
                "eksik: %s" % eksik[:5])

        import re as _re
        bozuk = []
        for fid, v in metinler.items():
            p = v.get("paragraf") or ""
            if len(p) < 100:
                bozuk.append("%s:kisa" % fid)
            elif _re.search(r"\bNone\b|\bnan\b|[{}]|%%|\s\.|  ", p):
                bozuk.append("%s:bicim" % fid)
            elif not p.endswith("."):
                bozuk.append("%s:nokta" % fid)
        kontrol(11, "Paragraflarda bicim hatasi yok", not bozuk,
                "; ".join(bozuk[:5]))

        # paragraftaki hucre sayisi, sayi blogundakiyle ayni mi
        tutmaz = []
        for fid, v in metinler.items():
            hucre_sayisi = ozetler[fid]["buyukluk"]["hucre"]
            if ("{:,}".format(hucre_sayisi)).replace(",", ".") not in v["paragraf"]:
                tutmaz.append(fid)
        kontrol(12, "Paragraf sayilari, sayi bloguyla tutuyor", not tutmaz,
                "; ".join(tutmaz[:5]))

        # dusuk guvenli bolgeler paragrafta uyari tasiyor mu
        uyarisiz = []
        for fid, b in ozetler.items():
            if b["guven"]["seviye"] == "dusuk":
                p = metinler[fid]["paragraf"]
                if "saha doğrulaması" not in p:
                    uyarisiz.append(fid)
        kontrol(13, "Dusuk guvenli yanginlar uyari tasiyor", not uyarisiz,
                "; ".join(uyarisiz[:5]))

        # 14 - UYDURMA SAYI KORUMASI
        # Paragrafta gecen her SAYIM degeri (binlik ayracli tam sayi) sayi
        # blogundan tureyebilmeli. Metni ileride bir dil modeli yazarsa
        # uydurdugu rakam buradan gecemez.
        uydurma = []
        for fid, v in metinler.items():
            b = ozetler[fid]
            hd = b["hukum_dagilimi"]
            # DIKKAT: 'n' disaridaki toplam hucre sayisi. Burada golgelenirse
            # rapor basligi son yanginin sayisini basiyor - iki kez dustuk.
            hucre_n = b["buyukluk"]["hucre"]
            izinli = set(hd.values())
            izinli |= {hucre_n, b["buyukluk"]["tahminli_hucre"],
                       hd["EROZYON_ONCE"] + hd["DIKIM_ADAYI"],
                       hucre_n - hd["KAPSAM_DISI"],
                       int(round(b["buyukluk"]["alan_ha"])),
                       int(round(b["arazi"]["yukselti_min"])),
                       int(round(b["arazi"]["yukselti_maks"])),
                       len(ozetler),                    # "53 yangin"
                       b["guven"]["bolgedeki_referans_yangini"]}
            if b["tarih"]:
                izinli.add(int(str(b["tarih"])[:4]))     # tarihteki yil
            # Yalnizca SAYIM degerleri: virgulden sonra gelen ondalik
            # basamaklar (0,350 -> 350) ve sayi icine gomulu rakamlar
            # disarida kalsin diye lookbehind/lookahead kullaniliyor.
            kalip = r"(?<![\d,])\d{1,3}(?:\.\d{3})+(?![\d,])|(?<![\d,])\d{3,}(?![\d,])"
            for x in _re.findall(kalip, v["paragraf"]):
                deger = int(x.replace(".", ""))
                if deger not in izinli:
                    uydurma.append("%s:%s" % (fid, x))
        kontrol(14, "Paragraftaki sayimlar sayi blogundan tureyebiliyor",
                not uydurma, "; ".join(uydurma[:6]))
    else:
        kontrol(10, "Yangin ozetleri uretilmis", False,
                "yangin_ozetleri.json / yangin_metinleri.json yok - "
                "once: python ai/teslim/yangin_ozeti_uret.py --yaz")

    # ------------------------------------------------------------------ rapor
    print("=" * 74)
    print("HUKUM MOTORU KONTROLU  -  %d hucre, %d yangin"
          % (len(ham), len(bolge)))
    print("=" * 74)
    gecen = 0
    for no, ad, ok, notu in SONUC:
        print("  %2s) %-44s %s%s" % (no, ad, "GECTI" if ok else "KALDI",
                                    "" if ok else "  <- " + notu))
        gecen += ok
    print("-" * 74)
    print("  %d / %d kontrol gecti" % (gecen, len(SONUC)))
    if not ortam.get("onaylandi", False):
        print()
        print("  NOT: yetisme_ortami.json ornek tablo (onaylandi=false).")
        print("       Tur onerileri gecici, OGM rehberiyle degistirilmeli.")
    return 0 if gecen == len(SONUC) else 1


if __name__ == "__main__":
    sys.exit(main())
