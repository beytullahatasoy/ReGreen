# -*- coding: utf-8 -*-
"""
Yangin ozeti - Katman 1
=======================

Hucre hukmu (hukum.py) tek bir 250 m karesini anlatiyor. Bu dosya bir
ustteki olcegi uretiyor: kullanici bir yangin bolgesi sectiginde okunacak
GENEL DURUM metni.

Iki cikti veriyor:

    yangin_ozetleri.json    her yangin icin SAYI BLOGU - hesaplanmis olgular
    yangin_metinleri.json   her yangin icin PARAGRAF - okunacak metin

NEDEN IKI DOSYA
---------------
Sayi blogu makinenin urettigi olgu; paragraf onun insan diline cevrilmis
hali. Ayri tutuyoruz cunku paragraf ISTEGE BAGLI olarak bir dil modeliyle
yeniden yazilabilsin, ama bunu yaparken uydurma olgu giremesin: dil modeli
yalnizca sayi blogunu gorur, baska hicbir seye erisemez.

Bu script'in urettigi paragraf SABLON tabanlidir - deterministik ve hemen
kullanilabilir. Dil modeliyle guzellestirme ADIM 2'dir ve zorunlu degildir:

    1) python ai/teslim/yangin_ozeti_uret.py --yaz
       -> sablon paragraflar hazir, sistem calisir durumda

    2) (istege bagli) sayi bloklarini bir dil modeline verip paragraflari
       yeniden yazdir, OKU-ONAYLA, yangin_metinleri.json icine koy ve
       "onaylandi" alanini true yap

Urunde calisma aninda dil modeli YOK. Metin statik dosyadan okunuyor.

GUVEN NOTU
----------
Egitilebilir 34 yanginin bolge dagilimi: Ege 20, Akdeniz 13, Marmara 1,
digerlerinde hic yok. Bir yangin, bolgesinde referansi olmayan bir yerdeyse
bunu metinde soyluyoruz - sistem kendi belirsizligini gizlemiyor.
"""

import argparse
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

# Bolge basina egitilebilir yangin sayisi. ai/data/ara/yangin_ozeti.csv
# icindeki "egitime_uygun" sutunundan olculdu; ara veri repoda olmadigi
# icin buraya sabitlendi. Yeni yangin eklendiginde guncellenmeli.
BOLGE_REFERANS = {
    "Ege": 20, "Akdeniz": 13, "Marmara": 1,
    "Ic Anadolu": 0, "Dogu Anadolu": 0, "Karadeniz": 0,
}
REFERANS_KAYNAGI = "ai/data/ara/yangin_ozeti.csv -> egitime_uygun"

# Manifest bolge adlarini ASCII tasiyor; metinde duzgun yazilmali.
BOLGE_ADI = {"Ic Anadolu": "İç Anadolu", "Dogu Anadolu": "Doğu Anadolu",
             "Ege": "Ege", "Akdeniz": "Akdeniz", "Marmara": "Marmara",
             "Karadeniz": "Karadeniz"}

# Guven esikleri: kac referans yangini "yeterli" sayiliyor
REFERANS_YUKSEK = 10
REFERANS_ORTA = 3


def _g(x, basamak=1):
    """Turkce ondalik ayraci."""
    if x is None or not np.isfinite(x):
        return None
    return (("%%.%df" % basamak) % x).replace(".", ",")


def _bin(x):
    """Binlik ayracli tam sayi: 9048 -> 9.048"""
    if x is None or not np.isfinite(x):
        return None
    return ("{:,}".format(int(round(x)))).replace(",", ".")


AY = ["Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
      "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"]


def _tarih(iso):
    """2021-07-29 -> 29 Temmuz 2021"""
    if not iso:
        return None
    try:
        y, a, g = str(iso)[:10].split("-")
        return "%d %s %s" % (int(g), AY[int(a) - 1], y)
    except (ValueError, IndexError):
        return str(iso)


# --------------------------------------------------------- Turkce sayi eki
# Sayidan sonra gelen ek, sayinin OKUNUSUNA gore degisiyor:
#   %36 -> "otuz alti" -> ...alti'SINDA      (unluyle bitiyor, 'ı' uyumu)
#   %70 -> "yetmis"    -> ...yetmis'INDE     (unsuzle bitiyor, 'i' uyumu)
# Her kayit: (uyum unlusu, son kelime unluyle mi bitiyor)
_BIRLER = {1: ("i", False), 2: ("i", True), 3: ("ü", False), 4: ("ü", False),
           5: ("i", False), 6: ("ı", True), 7: ("i", True), 8: ("i", False),
           9: ("u", False)}
_ONLAR = {10: ("u", False), 20: ("i", True), 30: ("u", False), 40: ("ı", False),
          50: ("i", True), 60: ("ı", False), 70: ("i", False), 80: ("i", False),
          90: ("ı", False)}
_YUZ = ("ü", False)


def _son_parca(n):
    """Sayinin okunusundaki SON kelimenin uyum bilgisi."""
    n = int(n)
    if n == 0:
        return ("ı", False)          # sifir
    if n % 10:
        return _BIRLER[n % 10]
    if n % 100:
        return _ONLAR[n % 100]
    if n == 100:
        return _YUZ
    return ("i", False)              # bin


def yuzde_si(n):
    """Iyelik: '%70'i ormanlik', '%36'si mudahale adayi'"""
    unlu, unluyle_bitiyor = _son_parca(n)
    return "%%%d'%s%s" % (int(n), "s" if unluyle_bitiyor else "", unlu)


def yuzde_de(n):
    """Bulunma: '%36'sinda yangin hafif kalmis'"""
    unlu, unluyle_bitiyor = _son_parca(n)
    ek = "nde" if unlu in "ieü" else "nda"
    return "%%%d'%s%s%s" % (int(n), "s" if unluyle_bitiyor else "", unlu, ek)


def guven_seviyesi(bolge):
    n = BOLGE_REFERANS.get(bolge, 0)
    if n >= REFERANS_YUKSEK:
        return "yuksek", n
    if n >= REFERANS_ORTA:
        return "orta", n
    return "dusuk", n


def sayi_blogu(yangin, hucre, hukumler, tum_yangin_medyani):
    """Bir yangin icin hesaplanmis olgular. Dil modeline verilecek olan bu.

    Icinde YORUM yok, yalnizca olculmus deger var. Yorumu paragraf katmani
    yapiyor, o da yalnizca buradaki sayilara bakarak.
    """
    d = hucre
    p = d[d["prediction_status"] == "predicted"]
    h = hukumler

    siddet = d["severity_class"].value_counts()
    arazi = d["land_cover"].value_counts()
    hukum_sayim = h["hukum"].value_counts()
    seviye, referans = guven_seviyesi(yangin.get("region"))

    oran = h["toparlanma_orani"].dropna()

    return {
        "fire_id": yangin["fire_id"],
        "il": yangin.get("province"),
        "bolge": yangin.get("region"),
        "tarih": yangin.get("fire_date"),
        "kalite": yangin.get("quality_flag"),

        "buyukluk": {
            "hucre": int(len(d)),
            "alan_ha": float(yangin.get("burned_area_ha") or 0),
            "tahminli_hucre": int(len(p)),
        },

        "siddet_dagilimi": {k: int(v) for k, v in siddet.items()},
        "arazi_dagilimi": {k: int(v) for k, v in arazi.items()},
        "hukum_dagilimi": {k: int(hukum_sayim.get(k, 0)) for k in H.HUKUM_SIRASI},

        "arazi": {
            "egim_medyan": float(d["slope_deg"].median()),
            "egim_p90": float(d["slope_deg"].quantile(0.90)),
            "yukselti_min": float(d["elevation_m"].min()),
            "yukselti_maks": float(d["elevation_m"].max()),
            "agaclik_orani": float((d["land_cover"] == "Agaclik").mean()),
            "ortu_medyan": float(d["tree_cover"].median()),
        },

        "erisim": {
            "yol_medyan_km": float(d["road_distance_km"].median()),
            "yol_uzak_orani": float((d["road_distance_km"] >= H.YOL_UZAK).mean()),
        },

        "gorunum": {
            "acik_ortalama": float(p["recovery_gap_pred"].mean()) if len(p) else None,
            "tum_yanginlar_medyani": tum_yangin_medyani,
            "toparlanma_medyan": float(oran.median()) if len(oran) else None,
        },

        "guven": {
            "seviye": seviye,
            "bolgedeki_referans_yangini": referans,
            "kaynak": REFERANS_KAYNAGI,
        },
    }


# --------------------------------------------------------------- paragraf

def profil(b):
    """Yanginin anlatisini belirleyen profil.

    53 yangini ayni cumle sirasiyla anlatmak yanlis olurdu: her birinin
    ASIL MESAJI farkli. Cogu kendi toparlanan bir yanginda okuyucuya
    once "buraya kaynak ayirma" denmeli; bolgesel destegi olmayan bir
    yanginda ise once belirsizlik soylenmeli. Profil bunu seciyor.
    """
    n = max(b["buyukluk"]["hucre"], 1)
    hd = b["hukum_dagilimi"]
    mudahale = (hd["EROZYON_ONCE"] + hd["DIKIM_ADAYI"]) / n
    izle = hd["IZLE"] / n
    kapsam = hd["KAPSAM_DISI"] / n

    if b["guven"]["seviye"] == "dusuk":
        return "belirsiz"
    if kapsam >= 0.35:
        return "kapsam_dar"
    if izle >= 0.60:
        return "kendi_toparlaniyor"
    if mudahale >= 0.25:
        return "yogun_mudahale"
    if b["arazi"]["egim_medyan"] >= 15:
        return "dik_arazi"
    return "karisik"


def _kiyas(g):
    """Modelin gorunumunu 53 yanginin medyaniyla kiyaslar."""
    med = g["tum_yanginlar_medyani"]
    fark = g["acik_ortalama"] - med
    if abs(fark) < 0.02:
        return "53 yangının medyanıyla (%s) aynı seviyede" % _g(med, 3)
    if fark > 0:
        return ("53 yangının medyanının (%s) üstünde, yani ortalamadan daha "
                "zorlu bir toparlanma bekleniyor" % _g(med, 3))
    return ("53 yangının medyanının (%s) altında, yani ortalamadan daha "
            "kolay bir toparlanma bekleniyor" % _g(med, 3))


def paragraf_kur(b):
    """Sayi blogundan paragraf. Deterministik, uydurma olgu yok.

    Anlati profile gore degisiyor (bkz. profil()); sayilarin hepsi sayi
    blogundan geliyor, hicbiri elle yazilmadi.
    """
    y = b["buyukluk"]
    a = b["arazi"]
    g = b["gorunum"]
    hd = b["hukum_dagilimi"]
    n = max(y["hucre"], 1)
    pr = profil(b)

    mudahale = hd["EROZYON_ONCE"] + hd["DIKIM_ADAYI"]
    c = []

    # ---------------------------------------------------------- 1 kimlik
    c.append("%s, %s. %s hektarlık alan, %s hücre."
             % (b["il"] or b["fire_id"], _tarih(b["tarih"]),
                _bin(y["alan_ha"]), _bin(n)))

    # ------------------------------------------------------- 2 arazi
    if pr == "dik_arazi":
        c.append("Arazi sert: medyan eğim %s°, hücrelerin onda biri %s°'nin "
                 "üstünde, yükselti %s ile %s metre arasında değişiyor."
                 % (_g(a["egim_medyan"]), _g(a["egim_p90"]),
                    _bin(a["yukselti_min"]), _bin(a["yukselti_maks"])))
    else:
        c.append("Yanan yerin %s ormanlık, medyan eğim %s°, %s ile %s metre "
                 "arasında uzanıyor."
                 % (yuzde_si(round(100 * a["agaclik_orani"])),
                    _g(a["egim_medyan"]), _bin(a["yukselti_min"]),
                    _bin(a["yukselti_maks"])))

    # --------------------------------------------------- 3 ASIL MESAJ
    if pr == "belirsiz":
        c.append("Bu yangını okurken önce şunu bilmek gerekiyor: modelin bu "
                 "bölgede öğrendiği bir örnek yok, aşağıdaki değerler başka "
                 "bölgelerden genellenerek üretildi.")

    elif pr == "kapsam_dar":
        c.append("Yanan alanın büyük bölümü orman değil: %s hücre tarım, "
                 "yerleşim ya da su yüzeyi olduğu için ağaçlandırma "
                 "kapsamının dışında kalıyor. Rehabilitasyon kararı geri "
                 "kalan %s hücre üzerinden veriliyor."
                 % (_bin(hd["KAPSAM_DISI"]), _bin(n - hd["KAPSAM_DISI"])))

    elif pr == "kendi_toparlaniyor":
        c.append("Bu yangının asıl bulgusu müdahale değil, müdahale "
                 "gerekmemesi: hücrelerin %s yangın hafif kalmış ve doğal "
                 "toparlanma bekleniyor. Kaynak buraya değil, daha zayıf "
                 "yangınlara ayrılmalı."
                 % yuzde_de(round(100 * hd["IZLE"] / n)))

    elif pr == "yogun_mudahale":
        c.append("Alanın önemli bir bölümü kendi başına kapanmayacak: %s "
                 "hücre doğrudan müdahale adayı, bu da yanan karelerin %s "
                 "demek." % (_bin(mudahale),
                             yuzde_si(round(100 * mudahale / n))))

    elif pr == "dik_arazi":
        # Erozyon hucre sayisi cok kucukse onu basa cikarmak yanlis olur:
        # "1 hucrede erozyon kontrolu" bir yanginin ana mesaji olamaz.
        if hd["EROZYON_ONCE"] >= max(10, 0.03 * n):
            c.append("Eğim burada belirleyici etken: %s hücrede dikimden önce "
                     "erozyon kontrolü gerekiyor, çünkü dik yamaçta çıplak "
                     "toprak ilk sağanakta taşınıyor."
                     % _bin(hd["EROZYON_ONCE"]))
        else:
            c.append("Eğim burada belirleyici etken: müdahale gereken kare "
                     "sayısı az olsa da dik yamaçlarda makineli çalışma "
                     "sınırlı, dikim öncesi toprak stabilizasyonu gündeme "
                     "gelecek.")

    else:  # karisik
        c.append("Baskın bir örüntü yok: alan hem kendi toparlanacak hem de "
                 "müdahale isteyen kareler barındırıyor. Burada kararı "
                 "öncelik sıralaması veriyor.")

    # ------------------------------------------------- 4 modelin gorunumu
    if g["acik_ortalama"] is not None:
        c.append("Modelin iki yıllık iyileşme görünümü %s — %s."
                 % (_g(g["acik_ortalama"], 3), _kiyas(g)))

    # ------------------------------------------------------ 5 bilesim
    # "N tanesinde" ancak ONCESINDE mudahale sayisi gectiyse anlamli;
    # gecmediyse ozne olmadan kaliyor ve cumle bozuluyor.
    p = []
    mudahale_yazildi = False
    if pr != "yogun_mudahale" and mudahale:
        p.append("%s hücre doğrudan müdahale adayı" % _bin(mudahale))
        mudahale_yazildi = True
    if hd["EROZYON_ONCE"] and pr != "dik_arazi":
        p.append(("%s tanesinde" if mudahale_yazildi else "%s hücrede")
                 % _bin(hd["EROZYON_ONCE"])
                 + " dikimden önce erozyon kontrolü gerekiyor")
    if hd["ONCELIGE_GORE"]:
        p.append("%s hücre öncelik sırasına göre değerlendirilecek"
                 % _bin(hd["ONCELIGE_GORE"]))
    if pr != "kendi_toparlaniyor" and hd["GENCLESME_IZLE"]:
        p.append("%s hücrede doğal gençleşme izlenmesi yeterli"
                 % _bin(hd["GENCLESME_IZLE"]))
    if p:
        c.append("Dağılım: " + ", ".join(p) + ".")

    # kapsam disi, ana mesajda gecmediyse
    if pr != "kapsam_dar" and hd["KAPSAM_DISI"]:
        c.append("%s hücre tarım, yerleşim ya da su yüzeyi olduğu için "
                 "ağaçlandırma kapsamı dışında." % _bin(hd["KAPSAM_DISI"]))

    # 7 - erisim
    if b["erisim"]["yol_uzak_orani"] >= 0.15:
        c.append("Alanın %s en yakın yola 2 kilometreden uzak; erişim bu yangında belirleyici olacak."
                 % yuzde_si(round(100 * b["erisim"]["yol_uzak_orani"])))

    # 8 - guven
    # Olculmus rakam: bolgesinde yalniz olan grup rho +0,140, bolgesel
    # destegi olanlar +0,707 (deney_7_kotu_gruplar.py). Belirsizligi
    # gizlemiyoruz, sayisiyla soyluyoruz.
    gv = b["guven"]
    n_ref = gv["bolgedeki_referans_yangini"]
    if gv["seviye"] == "yuksek":
        c.append("%s bölgesinde %d referans yangınımız olduğu için buradaki tahminlerin güveni yüksek."
                 % (BOLGE_ADI.get(b["bolge"], b["bolge"]), n_ref))
    elif gv["seviye"] == "orta":
        c.append("%s bölgesinde %d referans yangınımız var; tahminler kullanılabilir ama saha doğrulaması önerilir."
                 % (BOLGE_ADI.get(b["bolge"], b["bolge"]), n_ref))
    elif n_ref == 0:
        c.append("%s bölgesinde eğitim setimizde hiç referans yangını yok — buradaki tahminler tamamen diğer bölgelerden öğrenilenlere dayanıyor. Belirsizlik yüksek, saha doğrulaması gerekli."
                 % BOLGE_ADI.get(b["bolge"], b["bolge"]))
    else:
        c.append("%s bölgesindeki tek referans yangını bu — doğrulamada bu yangın dışarıda bırakıldığında eğitimde bölgeden hiç örnek kalmıyor. Ölçtüğümüz sıralama başarısı burada +0,140'a düşüyor (bölgesel desteği olan yangınlarda +0,707). Tahminler yön gösterici, saha doğrulaması gerekli."
                 % BOLGE_ADI.get(b["bolge"], b["bolge"]))

    # 9 - veri kalitesi
    if b["kalite"] != "ok":
        c.append("Bu yangının veri kalitesi bayrağı \"%s\" — alan ölçümünde tutarsızlık var, sonuçlar temkinli okunmalı."
                 % b["kalite"])

    return " ".join(c)


# -------------------------------------------------------------------- CLI

def main():
    ap = argparse.ArgumentParser(
        description="53 yangin icin sayi blogu ve ozet paragrafi uretir.")
    ap.add_argument("paket", nargs="?", default=None)
    ap.add_argument("--yaz", action="store_true",
                    help="yangin_ozetleri.json ve yangin_metinleri.json yaz")
    ap.add_argument("--goster", metavar="FIRE_ID",
                    help="tek bir yanginin paragrafini bas")
    a = ap.parse_args()

    kok = pathlib.Path(a.paket).resolve() if a.paket else yollar.TESLIM
    with open(kok / "manifest.json", encoding="utf-8") as f:
        man = json.load(f)

    cumleler, ortam = H.metinleri_yukle()

    # tum yanginlarin medyani - kiyas icin, once hesaplanmali
    ortalamalar = []
    hucreler = {}
    for y in man["fires"]:
        f = kok / ("%s_hucreler.csv" % y["fire_id"])
        if not f.exists():
            continue
        d = pd.read_csv(f)
        hucreler[y["fire_id"]] = d
        p = d[d["prediction_status"] == "predicted"]
        if len(p):
            ortalamalar.append(float(p["recovery_gap_pred"].mean()))
    medyan = float(np.median(ortalamalar)) if ortalamalar else None

    bloklar, metinler = {}, {}
    for y in man["fires"]:
        fid = y["fire_id"]
        if fid not in hucreler:
            continue
        d = hucreler[fid]
        hf = kok / ("%s_hukumler.csv" % fid)
        h = pd.read_csv(hf) if hf.exists() else H.hukum_uret(d, y.get("region"),
                                                             cumleler, ortam)
        b = sayi_blogu(y, d, h, medyan)
        bloklar[fid] = b
        metinler[fid] = {"paragraf": paragraf_kur(b), "profil": profil(b),
                         "kaynak": "sablon", "onaylandi": True}

    if a.goster:
        fid = a.goster
        if fid not in bloklar:
            print("bulunamadi: %s" % fid)
            return 1
        print(json.dumps(bloklar[fid], ensure_ascii=False, indent=2))
        print()
        print(metinler[fid]["paragraf"])
        return 0

    if a.yaz:
        with open(kok / "yangin_ozetleri.json", "w", encoding="utf-8") as f:
            json.dump({"surum": "1.0", "referans_kaynagi": REFERANS_KAYNAGI,
                       "yanginlar": bloklar}, f, ensure_ascii=False, indent=2)
        with open(kok / "yangin_metinleri.json", "w", encoding="utf-8") as f:
            json.dump({"surum": "1.0", "dil": "tr",
                       "uretim": "sablon (deterministik)",
                       "yanginlar": metinler}, f, ensure_ascii=False, indent=2)
        print("yazildi -> yangin_ozetleri.json, yangin_metinleri.json")

    uz = [len(m["paragraf"]) for m in metinler.values()]
    print("=" * 66)
    print("YANGIN OZETI  -  %d yangin" % len(metinler))
    print("=" * 66)
    print("  paragraf uzunlugu: en kisa %d, medyan %d, en uzun %d karakter"
          % (min(uz), int(np.median(uz)), max(uz)))
    gv = {}
    for b in bloklar.values():
        gv[b["guven"]["seviye"]] = gv.get(b["guven"]["seviye"], 0) + 1
    print("  guven seviyesi   : " + " · ".join("%s %d" % (k, v) for k, v in sorted(gv.items())))
    kt = sum(1 for b in bloklar.values() if b["kalite"] != "ok")
    print("  kalite bayragi ok olmayan: %d" % kt)
    pr = {}
    for m in metinler.values():
        pr[m["profil"]] = pr.get(m["profil"], 0) + 1
    print("  anlati profili   : " + " · ".join(
        "%s %d" % (k, v) for k, v in sorted(pr.items(), key=lambda x: -x[1])))
    return 0


if __name__ == "__main__":
    sys.exit(main())
