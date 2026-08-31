# -*- coding: utf-8 -*-
"""
Hucre hukmu - kural tabanli oneri katmani
=========================================

Oncelik skoru "NE ZAMAN gidilecek" sorusunu cevapliyor. Bu dosya ikinci
soruyu cevapliyor: "GIDINCE NE YAPILACAK".

    oncelik  ->  siralama    ->  agirliklara bagli, kullanici degistiriyor
    hukum    ->  eylem turu  ->  yalnizca olcumlere bagli, SABIT

Hukmun agirliktan bagimsiz olmasi bilincli bir tasarim karari: kullanici
agirlik kaydiricisini oynattiginda sira degisir ama hukum degismez. Bu
sayede hukum bir kez hesaplanip dosyaya yazilabiliyor, backend'in calisma
aninda bir sey hesaplamasi gerekmiyor.

NEDEN KURAL, NEDEN DIL MODELI DEGIL
-----------------------------------
Ayni hucreye iki kez bakan uzman ayni cumleyi gormeli, ve "bunu neden
soyledin" sorusunun cevabi bir kurala kadar izlenebilmeli. Uretilen her
satirda hangi kuralin tetiklendigi ayrica yaziliyor (tetikleyen sutunu).

Cumlelerin metni burada degil, cumleler.json icinde. Metin degistirmek
icin kod degistirmek gerekmiyor.

ESIKLER NEREDEN GELDI
---------------------
Ilke: FIZIKSEL iddia mutlak esik, SIRALAMA iddiasi goreli esik.

  "Burada erozyon riski var"  fiziksel  -> mutlak 25 derece
  "Bu yanginda once buraya"   siralama  -> zaten oncelik skoru yapiyor

Goreli esik kullanmadik cunku yanginlarin egim dagilimlari birbirine hic
benzemiyor: yanginlarin 75. yuzdelik egimi 4 ile 27 derece arasinda
degisiyor. Goreli olsaydi duz bir yanginda 5 derecelik kareye "dik yamac,
erozyon riski" derdik. Duz bir yanginda hic erozyon hukmu cikmamasi
hata degil, dogru cevaptir.

TOPARLANMA ORANI
----------------
Ham "iyilesme acigi" (recovery_gap_pred) tek basina yorumlanamiyor: 0,30
acik, yangin oncesi 0,40 olan yerde felaket, 0,80 olan yerde normal.
O yuzden orani kullaniyoruz:

    toparlanma_orani = (ndvi_before - recovery_gap_pred) / ndvi_before

Yani "yangin oncesi ortunun yuzde kaci iki yilda geri geliyor". Teslim
edilen 17.274 tahminli hucrede medyani 0,40; dagilimi dar (%25 = 0,33,
%75 = 0,46). Esikler bu dagilima gore secildi.
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

BURASI = pathlib.Path(__file__).resolve().parent

# ---------------------------------------------------------------- esikler

# Toparlanma orani esikleri. Teslim verisindeki dagilimdan secildi.
#   < 0,35  zayif   -> tahminli hucrelerin %33'u
#   < 0,50  orta    -> %52
#   >= 0,50 iyi     -> %15
TOPARLANMA_ZAYIF = 0.35
TOPARLANMA_IYI = 0.50

# Mutlak, fiziksel esikler
EGIM_DIK = 25.0          # derece - erozyon riski
YOL_UZAK = 2.0           # km - erisim darbogazi
ORTU_SEYREK = 0.30       # yangin oncesi agac ortusu orani

# Agaclandirma hedefi olmayan arazi siniflari
KAPSAM_DISI_ARAZI = {"Tarim", "Yerlesim", "Su", "Sulak alan"}

# Egitim setinde referans yangini az olan bolgeler. Egitilebilir 34 yangin
# Ege'de 20, Akdeniz'de 13, Marmara'da 1; digerlerinde hic yok.
DUSUK_GUVEN_BOLGE = {"Marmara", "Ic Anadolu", "Dogu Anadolu", "Karadeniz"}

AGIR_SIDDET = {"yuksek", "orta-yuksek"}

# Hukumler oncelik sirasina gore denenir, ILK uyan kazanir.
HUKUM_SIRASI = ["KAPSAM_DISI", "SAHA_KONTROL", "IZLE",
                "EROZYON_ONCE", "DIKIM_ADAYI", "ONCELIGE_GORE", "GENCLESME_IZLE"]

ARAZI_ADI = {
    "Agaclik": "ağaçlık alan",
    "Otlak/calilik": "otlak ve çalılık",
    "Tarim": "tarım alanı",
    "Yerlesim": "yerleşim alanı",
    "Su": "su yüzeyi",
    "Sulak alan": "sulak alan",
    "Ciplak": "çıplak/seyrek bitkili alan",
}

SIDDET_ADI = {
    "dusuk": "düşük şiddet",
    "orta-dusuk": "orta-düşük şiddet",
    "orta-yuksek": "orta-yüksek şiddet",
    "yuksek": "yüksek şiddet",
}


def metinleri_yukle(klasor=None):
    """cumleler.json ve yetisme_ortami.json dosyalarini okur."""
    k = pathlib.Path(klasor) if klasor else BURASI
    with open(k / "cumleler.json", encoding="utf-8") as f:
        cumleler = json.load(f)
    with open(k / "yetisme_ortami.json", encoding="utf-8") as f:
        ortam = json.load(f)
    return cumleler, ortam


# ------------------------------------------------------------------ olcum

def toparlanma_orani(ndvi_before, gap):
    """Yangin oncesi ortunun yuzde kaci iki yilda geri geliyor.

    ndvi_before cok kucukse oran anlamsiz buyur, o yuzden 0,05 altinda
    hesaplamiyoruz (teslim verisinde boyle hucre yok, yine de koruma).
    """
    if not np.isfinite(ndvi_before) or not np.isfinite(gap) or ndvi_before < 0.05:
        return None
    return float(np.clip((ndvi_before - gap) / ndvi_before, 0.0, 1.0))


def _sayi(x, basamak=0):
    """Turkce ondalik ayraciyla, yoksa None."""
    if x is None or not np.isfinite(x):
        return None
    s = ("%%.%df" % basamak) % x
    return s.replace(".", ",")


def _slotlar(s, oran, bolge):
    """Cumle sablonlarindaki {yer_tutucu} degerleri."""
    egim = s.get("slope_deg")
    return {
        "egim": _sayi(egim, 1),
        "yukselti": _sayi(s.get("elevation_m"), 0),
        "yol": _sayi(s.get("road_distance_km"), 2),
        "dnbr": _sayi(s.get("burn_severity_dnbr"), 2),
        "toparlanma": _sayi(oran * 100, 0) if oran is not None else None,
        "agac_ortusu": _sayi(s.get("tree_cover", 0) * 100, 0),
        "arazi_adi": ARAZI_ADI.get(s.get("land_cover"), s.get("land_cover")),
        "siddet_adi": SIDDET_ADI.get(s.get("severity_class"), s.get("severity_class")),
        "bolge": bolge or "-",
        "egim_sinifi": _egim_sinifi(egim),
    }


def _egim_sinifi(egim):
    if egim is None or not np.isfinite(egim):
        return None
    if egim < 10:
        return "duz"
    if egim < EGIM_DIK:
        return "orta"
    return "dik"


# ------------------------------------------------------------------ kural

def hukum_sec(s, oran):
    """Tek hucre icin hukum kodu dondurur. Sira onemli, ilk uyan kazanir."""
    if s.get("land_cover") in KAPSAM_DISI_ARAZI:
        return "KAPSAM_DISI"

    durum = s.get("prediction_status")
    if durum == "no_data":
        return "SAHA_KONTROL"
    if durum == "low_severity":
        return "IZLE"

    if oran is None:
        return "SAHA_KONTROL"

    egim = s.get("slope_deg")
    dik = np.isfinite(egim) and egim >= EGIM_DIK if egim is not None else False

    if oran < TOPARLANMA_ZAYIF:
        return "EROZYON_ONCE" if dik else "DIKIM_ADAYI"
    if oran < TOPARLANMA_IYI:
        return "ONCELIGE_GORE"
    return "GENCLESME_IZLE"


def ek_kosullar(s, hukum, bolge):
    """Hukmun ustune binen ek kosullar. Sirali liste doner."""
    if hukum in ("KAPSAM_DISI", "SAHA_KONTROL"):
        return []

    k = []
    egim = s.get("slope_deg")
    yol = s.get("road_distance_km")
    ortu = s.get("tree_cover")
    arazi = s.get("land_cover")

    if yol is not None and np.isfinite(yol) and yol >= YOL_UZAK:
        k.append("ERISIM_ZOR")
    if arazi and arazi != "Agaclik":
        k.append("ESKIDEN_ORMAN_DEGIL")
    elif ortu is not None and np.isfinite(ortu) and ortu < ORTU_SEYREK:
        k.append("SEYREK_ORTU")
    # dik yamac zaten hukum olduysa tekrar etme
    if (hukum != "EROZYON_ONCE" and egim is not None
            and np.isfinite(egim) and egim >= EGIM_DIK):
        k.append("DIK_YAMAC")
    if hukum != "IZLE" and s.get("severity_class") in AGIR_SIDDET:
        k.append("AGIR_YANMIS")
    if bolge in DUSUK_GUVEN_BOLGE:
        k.append("DUSUK_GUVEN")
    return k


def tur_bul(s, bolge, ortam):
    """Yetisme ortami tablosundan tur onerisi. Ilk uyan kayit kullanilir.

    Tabloya uyan kayit yoksa None doner - uydurmuyoruz.
    """
    yuk = s.get("elevation_m")
    egim = s.get("slope_deg")
    arazi = s.get("land_cover")
    if yuk is None or not np.isfinite(yuk):
        return None

    for kural in ortam.get("kurallar", []):
        b = kural.get("bolge") or []
        if b and bolge not in b:
            continue
        a = kural.get("arazi") or []
        if a and arazi not in a:
            continue
        y0, y1 = kural.get("yukselti", [0, 1e9])
        if not (y0 <= yuk < y1):
            continue
        e0, e1 = kural.get("egim", [0, 90])
        if egim is not None and np.isfinite(egim) and not (e0 <= egim <= e1):
            continue
        return {"ad": kural.get("ad"), "turler": kural.get("turler", [])}
    return None


# ------------------------------------------------------------------ cumle

def _doldur(sablon, slot):
    """Sablonu doldurur. Eksik slot varsa cumleyi tamamen atlar."""
    if not sablon:
        return None
    try:
        return sablon.format(**slot)
    except (KeyError, IndexError):
        return None


def cumle_kur(s, hukum, kosullar, oran, bolge, cumleler, ortam, tur=None):
    """Uc ayri metin uretir: ozet, ayrinti, not.

    ozet    panelin ustunde gorunecek kisa hukum. Uzman once bunu okur.
    ayrinti detay bolumu - gerekce, ek kosullar, yetisme ortami.
    not     her hucrede ayni olan zamanlama uyarisi. Ayri tutuluyor cunku
            37 bin hucrede tekrarlanan bir metni cumleye gomerse gurultu
            oluyor; arayuz bunu dipnot olarak bir kez gosterebilir.
    """
    slot = _slotlar(s, oran, bolge)
    slot["egim_sinifi"] = (cumleler["olcu_adlari"]["egim_sinifi"]
                           .get(slot["egim_sinifi"]) or "")

    h = cumleler["hukum"][hukum]

    # --- ozet
    ozet = [h["baslik"] + "."]
    p = _doldur(h.get("cumle"), slot)
    if p:
        ozet.append(p)

    # --- ayrinti
    ayrinti = []
    p = _doldur(h.get("gerekce"), slot)
    if p:
        ayrinti.append(p)
    for k in kosullar:
        p = _doldur(cumleler["ek_kosul"].get(k), slot)
        if p:
            ayrinti.append(p)

    # yetisme ortami ve tur onerisi - yalnizca mudahale hukumlerinde
    if hukum in ("EROZYON_ONCE", "DIKIM_ADAYI"):
        yo = cumleler["yetisme_ortami"]
        p = _doldur(yo.get("giris"), slot)
        if p:
            ayrinti.append(p)
        if tur and tur.get("turler"):
            slot2 = dict(slot, turler=", ".join(tur["turler"]),
                         kaynak=ortam.get("kaynak", "yetişme ortamı"))
            for anahtar in ("oneri", "kaynak_notu"):
                p = _doldur(yo.get(anahtar), slot2)
                if p:
                    ayrinti.append(p)
        else:
            ayrinti.append(yo.get("tablo_yok"))

    # --- not (zamanlama) - yalnizca yorumlanabilir hucrelerde
    notu = None
    if hukum not in ("KAPSAM_DISI", "SAHA_KONTROL"):
        notu = cumleler["zamanlama"].get("varsayilan")

    return (" ".join(x for x in ozet if x),
            " ".join(x for x in ayrinti if x),
            notu)


# ------------------------------------------------------------------ toplu

def hukum_uret(df, bolge, cumleler=None, ortam=None):
    """Bir yangının hucre tablosuna hukum sutunlarini ekler.

    Beklenen sutunlar:
        prediction_status, recovery_gap_pred, ndvi_before, slope_deg,
        elevation_m, road_distance_km, tree_cover, land_cover, severity_class

    Doner: cell_id, hukum, ek_kosullar, toparlanma_orani, tur_onerisi,
           tetikleyen, ozet, ayrinti, zamanlama_notu_var
    """
    if cumleler is None or ortam is None:
        cumleler, ortam = metinleri_yukle()

    satirlar = []
    for _, r in df.iterrows():
        s = r.to_dict()
        oran = toparlanma_orani(s.get("ndvi_before", np.nan),
                                s.get("recovery_gap_pred", np.nan))
        h = hukum_sec(s, oran)
        k = ek_kosullar(s, h, bolge)
        tur = tur_bul(s, bolge, ortam) if h in ("EROZYON_ONCE", "DIKIM_ADAYI") else None
        ozet, ayrinti, notu = cumle_kur(s, h, k, oran, bolge, cumleler, ortam, tur)
        satirlar.append({
            "cell_id": s.get("cell_id"),
            # hukum_basligi ve tur_kaynagi BILEREK yazilmiyor: ikisi de
            # her satirda ayni, karsiliklari hukum_sozlugu.json icinde.
            "hukum": h,
            "ek_kosullar": "|".join(k),
            "toparlanma_orani": None if oran is None else round(oran, 4),
            "tur_onerisi": ", ".join(tur["turler"]) if tur else None,
            "tetikleyen": _tetikleyen(s, h, oran),
            "ozet": ozet,
            "ayrinti": ayrinti,
            # zamanlama notu hucreye YAZILMIYOR: 32 binden fazla satirda
            # birebir ayni metin, ~10 MB saf tekrar ederdi. Sozlukte tek
            # kez duruyor (hukum_sozlugu.json -> zamanlama_notu), hangi
            # hucrelerde gosterilecegini "zamanlama_notu_var" soyluyor.
            "zamanlama_notu_var": notu is not None,
        })
    return pd.DataFrame(satirlar)


def sozluk_uret(cumleler, ortam):
    """Arayuzun ihtiyac duydugu sabit metinler ve tanimlar.

    Hucre dosyalarinda yalnizca KOD tutuluyor; insanin okuyacagi karsiliklar
    ve her hucrede ayni olan metinler burada, tek kopya halinde.
    """
    return {
        "surum": cumleler.get("surum"),
        "dil": cumleler.get("dil", "tr"),
        "hukumler": [
            {"kod": k,
             "baslik": cumleler["hukum"][k]["baslik"],
             "sira": i}
            for i, k in enumerate(HUKUM_SIRASI)
        ],
        "ek_kosullar": {k: v for k, v in cumleler["ek_kosul"].items()},
        "zamanlama_notu": cumleler["zamanlama"].get("varsayilan"),
        "esikler": {
            "toparlanma_zayif": TOPARLANMA_ZAYIF,
            "toparlanma_iyi": TOPARLANMA_IYI,
            "egim_dik_derece": EGIM_DIK,
            "yol_uzak_km": YOL_UZAK,
            "ortu_seyrek": ORTU_SEYREK,
            "kapsam_disi_arazi": sorted(KAPSAM_DISI_ARAZI),
            "dusuk_guven_bolge": sorted(DUSUK_GUVEN_BOLGE),
        },
        "tur_tablosu": {
            "kaynak": ortam.get("kaynak"),
            "surum": ortam.get("surum"),
            "onaylandi": ortam.get("onaylandi", False),
        },
        "aciklama": (
            "Hukum, oncelik skorundan BAGIMSIZDIR: agirlik kaydiricisi "
            "oynatildiginda sira degisir, hukum degismez. Oncelik 'ne zaman "
            "gidilecek', hukum 'gidince ne yapilacak' sorusunu cevaplar."
        ),
    }


def _tetikleyen(s, hukum, oran):
    """Hukmu hangi olcumun tetikledigini insan okuyabilir bicimde yazar.

    Denetlenebilirlik icin: kullanici "bunu neden soyledin" diyebilmeli.
    """
    if hukum == "KAPSAM_DISI":
        return "land_cover=%s" % s.get("land_cover")
    if hukum == "SAHA_KONTROL":
        return "prediction_status=%s" % s.get("prediction_status")
    if hukum == "IZLE":
        return "prediction_status=low_severity"
    p = ["toparlanma=%.2f" % oran] if oran is not None else []
    if hukum == "EROZYON_ONCE":
        p.append("egim=%.1f>=%.0f" % (s.get("slope_deg", 0), EGIM_DIK))
    if hukum in ("EROZYON_ONCE", "DIKIM_ADAYI"):
        p.append("<%.2f" % TOPARLANMA_ZAYIF)
    elif hukum == "ONCELIGE_GORE":
        p.append("%.2f..%.2f" % (TOPARLANMA_ZAYIF, TOPARLANMA_IYI))
    elif hukum == "GENCLESME_IZLE":
        p.append(">=%.2f" % TOPARLANMA_IYI)
    return " ".join(p)


# -------------------------------------------------------------------- CLI

def main():
    ap = argparse.ArgumentParser(
        description="Teslim paketindeki her yangin icin hukum dosyasi uretir.")
    ap.add_argument("paket", nargs="?", default=None,
                    help="teslim klasoru (varsayilan: sample-data/backend-data)")
    ap.add_argument("--yaz", action="store_true",
                    help="{fire_id}_hukumler.csv dosyalarini yaz")
    ap.add_argument("--kompakt", action="store_true",
                    help="kurulmus metni yazma, yalnizca kod ve olcu "
                         "(dosya ~10 kat kucuk; metni tuketen taraf "
                         "hukum_sozlugu.json sablonlariyla kurar)")
    a = ap.parse_args()

    kok = pathlib.Path(a.paket).resolve() if a.paket else yollar.TESLIM
    with open(kok / "manifest.json", encoding="utf-8") as f:
        man = json.load(f)
    bolgeler = {y["fire_id"]: y.get("region") for y in man["fires"]}

    cumleler, ortam = metinleri_yukle()
    if not ortam.get("onaylandi", False):
        print("UYARI: yetisme_ortami.json ornek tablo, onaylanmadi.\n"
              "       kaynak = %s\n" % ortam.get("kaynak"))

    toplam, sayim, kosul_sayim = 0, {}, {}
    for fid in sorted(bolgeler):
        f = kok / ("%s_hucreler.csv" % fid)
        if not f.exists():
            continue
        d = pd.read_csv(f)
        h = hukum_uret(d, bolgeler[fid], cumleler, ortam)
        toplam += len(h)
        for k, v in h.hukum.value_counts().items():
            sayim[k] = sayim.get(k, 0) + int(v)
        for satir in h.ek_kosullar:
            for k in (satir.split("|") if satir else []):
                kosul_sayim[k] = kosul_sayim.get(k, 0) + 1
        if a.yaz:
            cikti = h.drop(columns=["ozet", "ayrinti"]) if a.kompakt else h
            cikti.to_csv(kok / ("%s_hukumler.csv" % fid), index=False,
                         encoding="utf-8-sig")

    if a.yaz:
        with open(kok / "hukum_sozlugu.json", "w", encoding="utf-8") as f:
            json.dump(sozluk_uret(cumleler, ortam), f,
                      ensure_ascii=False, indent=2)

    print("=" * 66)
    print("HUKUM DAGILIMI  -  %d hucre" % toplam)
    print("=" * 66)
    for k in HUKUM_SIRASI:
        n = sayim.get(k, 0)
        isaret = "" if n else "   <- HIC TETIKLENMEDI"
        print("  %-16s %7d  %5.1f%%%s" % (k, n, 100 * n / max(toplam, 1), isaret))
    print("-" * 66)
    print("  %-16s %7d" % ("TOPLAM", sum(sayim.values())))
    print()
    print("EK KOSULLAR")
    for k, n in sorted(kosul_sayim.items(), key=lambda x: -x[1]):
        print("  %-22s %7d  %5.1f%%" % (k, n, 100 * n / max(toplam, 1)))
    if a.yaz:
        print("\n%d yangin icin *_hukumler.csv yazildi -> %s" % (len(bolgeler), kok))


if __name__ == "__main__":
    main()
