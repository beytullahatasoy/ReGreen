# -*- coding: utf-8 -*-
"""
Oncelik skoru - TEK DOGRU TANIM
===============================

Bu dosya oncelik hesabinin REFERANS uygulamasidir. Backend ayni hesabi
kendi tarafinda tekrar yapacak (kullanici agirlik kaydiricisini oynattiginda
modeli yeniden calistirmamak icin). Iki tarafin ayni sonucu uretmesi sart,
o yuzden formul burada tek yerde tanimli.

MIMARI
------
    KATMAN 1  model      -> recovery_gap_pred   (pahali, bir kez hesaplanir)
    KATMAN 2  oncelik    -> priority_score      (ucuz, istek aninda)
    KATMAN 3  sinif      -> priority_class      (gosterim)

AI tarafi dosyalara VARSAYILAN agirliklarla hesaplanmis bir skor koyar ki
sistem kutudan cikar cikmaz calissin. Kullanici agirligi degistirdiginde
backend ayni fonksiyonu yeni agirliklarla cagirir.

NORMALIZASYON
-------------
Normalizasyon YANGIN ICINDE yapilir, Turkiye genelinde degil. Sebep: butce
yangin bazinda ayriliyor, soru "bu yanginda nereden baslayalim". Yanginlar
arasi karsilastirma yapilmiyor.

Referans kume: yalnizca prediction_status == "predicted" hucreler.

DURUMLARA GORE
--------------
    predicted     -> skor hesaplanir, sinif esikten atanir
    low_severity  -> skor 0.0, sinif DUSUK   (backend yorumlamasin diye
                     bos birakilmiyor; hafif yanmis alan operasyonel olarak
                     en dusuk onceliktir)
    no_data       -> skor None, sinif None   (gercekten bilmiyoruz)
"""

import numpy as np
import pandas as pd

# Varsayilan agirliklar. Arayuzdeki kaydiricilarin baslangic degeri.
VARSAYILAN_AGIRLIK = {
    "recovery": 0.50,   # modelin tahmin ettigi iyilesme acigi
    "erosion": 0.30,    # egim - toprak kaybi riski
    "access": 0.20,     # yola yakinlik - mudahale maliyeti
}

# Sinif esikleri. Normalize skor 0-1 arasinda oldugu icin sabit esik
# kullaniliyor; boylece backend birebir ayni sonucu uretebilir.
ESIKLER = [(0.75, "COK_YUKSEK"), (0.50, "YUKSEK"), (0.25, "ORTA")]
EN_DUSUK = "DUSUK"


NORM_ALANLARI = ["recovery_gap_pred", "slope_deg", "road_distance_km"]


def _normalize(seri, lo, hi):
    """Verilen min/max'a gore 0-1'e olcekle, disini kirp."""
    gecersiz = (lo is None or hi is None
                or not np.isfinite(lo) or not np.isfinite(hi)
                or hi - lo < 1e-9)
    if gecersiz:
        return pd.Series(np.full(len(seri), 0.5), index=seri.index)
    return ((seri - lo) / (hi - lo)).clip(0, 1)


def referans_cikar(df):
    """Normalizasyonda kullanilan min/max degerlerini cikar.

    Yalnizca prediction_status == "predicted" hucreler referans alinir.

    Bu degerler metadata.json'a yazilir. Boylece backend ALT KUME uzerinde
    calissa bile ayni sonucu uretebilir - kendi min/max'ini hesaplamaz,
    buradaki sabit degerleri kullanir.
    """
    ref = df[df["prediction_status"] == "predicted"]
    cikti = {}
    for k in NORM_ALANLARI:
        if k not in ref.columns or ref[k].notna().sum() == 0:
            cikti[k] = {"min": None, "max": None}
            continue
        cikti[k] = {"min": float(np.nanmin(ref[k])),
                    "max": float(np.nanmax(ref[k]))}
    return cikti


def sinif_ata(skor):
    if skor is None or not np.isfinite(skor):
        return None
    for esik, ad in ESIKLER:
        if skor >= esik:
            return ad
    return EN_DUSUK


def oncelik_hesapla(df, agirlik=None, referans=None):
    """Bir YANGININ hucre tablosuna priority_score ve priority_class ekler.

    Beklenen sutunlar:
        prediction_status, recovery_gap_pred, slope_deg, road_distance_km

    referans : dict veya None
        None ise min/max df'in kendisinden hesaplanir (tam yangin verisi
        elinizdeyse dogru olan budur).
        ALT KUME uzerinde calisiyorsaniz metadata.json'daki
        "normalization_reference" degerlerini gecin - aksi halde farkli
        min/max cikar ve skorlar tutmaz.

    Not: df tek bir yangina ait olmali. Coklu yangin icin yangin bazinda
    gruplayip ayri ayri cagirin.
    """
    a = dict(VARSAYILAN_AGIRLIK)
    if agirlik:
        a.update(agirlik)
    toplam = sum(a.values())
    if toplam <= 0:
        raise ValueError("agirliklarin toplami pozitif olmali")
    a = {k: v / toplam for k, v in a.items()}       # her zaman 1'e normalize

    d = df.copy()
    tahminli = d["prediction_status"] == "predicted"
    veriyok = d["prediction_status"] == "no_data"

    d["priority_score"] = np.nan
    d["priority_class"] = None

    if referans is None:
        referans = referans_cikar(d)

    if tahminli.any():
        def n(k, seri):
            r = referans.get(k, {})
            return _normalize(seri, r.get("min"), r.get("max"))

        n_iyilesme = n("recovery_gap_pred", d["recovery_gap_pred"])
        n_egim = n("slope_deg", d["slope_deg"])
        n_yol = n("road_distance_km", d["road_distance_km"])

        skor = (a["recovery"] * n_iyilesme
                + a["erosion"] * n_egim
                + a["access"] * (1 - n_yol))     # yola YAKIN olan avantajli
        d.loc[tahminli, "priority_score"] = skor[tahminli].round(4)

    # hafif yanmis: operasyonel olarak en dusuk oncelik. Bos birakilmiyor.
    hafif = d["prediction_status"] == "low_severity"
    d.loc[hafif, "priority_score"] = 0.0
    d.loc[hafif, "priority_class"] = EN_DUSUK

    # veri yok: gercekten bilmiyoruz, null kalir
    d.loc[veriyok, ["priority_score", "priority_class"]] = None

    d.loc[tahminli, "priority_class"] = [
        sinif_ata(s) for s in d.loc[tahminli, "priority_score"]]
    return d


def durum_belirle(d, dnbr_esik=0.27, dusus_esik=0.20):
    """prediction_status uret.

    no_data      : modelin ZORUNLU girdilerinden biri eksik
                   (agac_orani_y eksikse agac_orani'ndan doldurulabilir,
                    o yuzden zorunlu degil)
    low_severity : girdi tam ama yanma siddeti egitim araliginin altinda
    predicted    : geri kalan
    """
    zorunlu = ["tree_cover", "burn_severity_dnbr", "slope_deg",
               "elevation_m", "road_distance_km"]
    var = [c for c in zorunlu if c in d.columns]
    eksik = d[var].isna().any(axis=1)
    kapsamda = (d["burn_severity_dnbr"] >= dnbr_esik) & (d["ndvi_drop"] >= dusus_esik)
    return np.where(eksik, "no_data",
                    np.where(kapsamda, "predicted", "low_severity"))
