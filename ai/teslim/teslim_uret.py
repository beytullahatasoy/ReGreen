# -*- coding: utf-8 -*-
"""
Backend'e TAM TESLIM paketi uretir
==================================

Ornek paket degil - 55 yanginin tamami, gercek model ciktisiyla.

DURUSTLUK NOTU
--------------
Egitimde kullanilan yanginlara dogrudan model tahmini vermek IYIMSER olur
(model o hucreleri gormus). Bu yuzden:

    egitimde olan hucreler  -> KAT DISI (out-of-fold) tahmin
                               GroupKFold ile, kendi grubu disarida
                               birakilarak egitilmis modelden
    egitimde olmayan        -> tum veriyle egitilmis modelden
                               (gercek dis ornek)

Boylece backend'in gordugu sayilar sahada gorecegi sayilarla ayni
zorlukta.

Calistirma:  python teslim_uret.py
Cikti: teslim/ klasoru + model.joblib
"""

import json
import pathlib
import shutil
import warnings
from datetime import datetime, timezone

import joblib
import numpy as np
import pandas as pd

warnings.filterwarnings("ignore")

import firerecover_pipeline as fp
import oncelik
import pipeline_turkiye as pt
from rasterio import features
from rasterio.warp import transform_geom
from shapely.geometry import shape, mapping
from shapely.ops import unary_union
from sklearn.linear_model import Ridge
from sklearn.pipeline import make_pipeline
from sklearn.preprocessing import StandardScaler
from sklearn.impute import SimpleImputer
from sklearn.model_selection import LeaveOneGroupOut

KOK = pathlib.Path(__file__).parent
TESLIM = KOK / "teslim"

MODEL_SURUM = "ridge_v2"
SEMA_SURUM = "1.1"          # CSV sutunlari DEGISMEDI
TOHUM = 0

# 7 oznitelik. ndvi_dusus 21.08.2026'da eklendi:
#   grup ici Spearman +0.573 -> +0.659, top-%20 %45.9 -> %52.8
# Model ailesi olcumle secildi (7 aile, 20+ yontem denendi):
#   Ridge +0.686 | HistGB +0.659 | LightGBM +0.655 | RF +0.653 | XGB +0.631
# Ayrinti: MODEL_GUNLUGU.md
IC = ["agac_orani", "agac_orani_y", "dnbr", "egim_derece", "yukselti_m",
      "ndvi_dusus", "yol_mesafe_km"]

ALANLAR = ["fire_id", "cell_id", "lat", "lon",
           "tree_cover", "tree_cover_annual", "burn_severity_dnbr",
           "slope_deg", "elevation_m", "road_distance_km",
           "ndvi_before", "ndvi_after", "ndvi_drop", "severity_class",
           "land_cover", "prediction_status", "recovery_gap_pred",
           "priority_score", "priority_class"]

YENIDEN_ADLANDIR = {
    "yangin_id": "fire_id", "hucre_id": "cell_id",
    "agac_orani": "tree_cover", "agac_orani_y": "tree_cover_annual",
    "dnbr": "burn_severity_dnbr", "egim_derece": "slope_deg",
    "yukselti_m": "elevation_m", "yol_mesafe_km": "road_distance_km",
    "ndvi_oncesi": "ndvi_before", "ndvi_sonrasi": "ndvi_after",
    "ndvi_dusus": "ndvi_drop", "siddet_sinifi": "severity_class",
    "arazi_tipi_y": "land_cover"}


def hazirla(X):
    X = X.copy()
    # agac_orani_y bazi hucrelerde nodata; cok yillik ortalamayla dolduruyoruz.
    # Kalan eksikler boru hattindaki SimpleImputer'a birakiliyor.
    X["agac_orani_y"] = X["agac_orani_y"].fillna(X["agac_orani"])
    return X[IC].astype(float).values


def yeni_model():
    """Ridge. Neden agac degil: 7 model ailesi ayni LOGO ile karsilastirildi,
    dogrusal model kazandi. Sebebi gorulmemis yangina genelleme - agaclar
    egitim yanginlarinin deger araliklarina gore bolme ogreniyor, yeni yangin
    o araliklarin disina cikinca tikaniyorlar. Ridge duzgun ekstrapole ediyor.
    Ek fayda: katsayilar dogrudan okunabiliyor, aciklanabilirlik urun geregi."""
    return make_pipeline(SimpleImputer(strategy="median"),
                         StandardScaler(), Ridge(alpha=1.0))


def performans(y, tahmin, gruplar, dnbr):
    """Kat disi tahminlerden performans ozeti.

    Metrik GRUP ICI: gruplarin hedef ortalamalari 0,12-0,41 arasinda
    degisiyor, hepsini tek torbaya atarsak model "hangi yangin bu"
    sorusunu cozunce odul alir. Karar vericinin sorusu ise "bu yanginin
    icinde neresi kotu".
    """
    from scipy.stats import spearmanr

    def top20(a, b):
        n = len(a); k = max(1, int(round(n * 0.20)))
        if k >= n:
            return np.nan
        ger = set(np.argsort(-a)[:k])
        return sum(1 for i in np.argsort(-b)[:k] if i in ger) / k

    df = pd.DataFrame({"y": y, "p": tahmin, "g": gruplar, "d": dnbr})
    rho, t20, t20d, ikili = [], [], [], []
    rng = np.random.default_rng(0)
    for _, s in df.groupby("g"):
        if len(s) < 8 or s["y"].nunique() < 3:
            continue
        v = spearmanr(s["y"], s["p"]).statistic
        if not np.isfinite(v):
            continue
        rho.append(v)
        a, b, c = s["y"].to_numpy(), s["p"].to_numpy(), s["d"].to_numpy()
        t20.append(top20(a, b)); t20d.append(top20(a, c))
        i, j = rng.integers(0, len(a), 20000), rng.integers(0, len(a), 20000)
        m = a[i] != a[j]
        ikili.append(float(np.mean(np.sign(a[i] - a[j])[m] ==
                                   np.sign(b[i] - b[j])[m])))
    rho = np.array(rho)
    return {
        "metric_note": ("Butun degerler out-of-fold: model o yangini hic "
                        "gormeden tahmin etti. LeaveOneGroupOut, mekansal grup."),
        "group_count": len(rho),
        "within_fire_spearman_mean": round(float(rho.mean()), 4),
        "within_fire_spearman_median": round(float(np.median(rho)), 4),
        "positive_groups": int((rho > 0).sum()),
        "worst_group": round(float(rho.min()), 4),
        "best_group": round(float(rho.max()), 4),
        "top20_hit_rate": round(float(np.nanmean(t20)), 4),
        "top20_hit_rate_dnbr_baseline": round(float(np.nanmean(t20d)), 4),
        "top20_hit_rate_random": 0.20,
        "pairwise_accuracy": round(float(np.mean(ikili)), 4),
    }


def sinir_poligonu(y, df):
    grid = fp.Grid.olustur(y["bbox"], pt.HUCRE_M)
    # DIKKAT: reshape KULLANMA. veri_temizle.py yinelenen hucreleri attigi
    # icin satir sayisi izgara boyutundan kucuk olabiliyor (18 yanginda oyle).
    # hucre_id'nin sonundaki duz indeksten yerine koyuyoruz.
    try:
        idx = df["hucre_id"].str.rsplit("_", n=1).str[-1].astype(int).to_numpy()
    except (ValueError, AttributeError):
        return None
    if idx.max() >= grid.h * grid.w:
        return None
    maske = np.zeros(grid.h * grid.w, dtype="uint8")
    maske[idx] = df["yanik"].fillna(False).to_numpy().astype("uint8")
    maske = maske.reshape(grid.h, grid.w)
    if maske.sum() == 0:
        return None
    parcalar = [shape(g) for g, v in features.shapes(
        maske, mask=maske.astype(bool), transform=grid.transform) if v == 1]
    if not parcalar:
        return None
    b = unary_union(parcalar)
    b = b.buffer(pt.HUCRE_M * 0.6).buffer(-pt.HUCRE_M * 0.6)
    b = b.simplify(pt.HUCRE_M * 0.3)
    if b.is_empty:
        return None
    return transform_geom(grid.crs, "EPSG:4326", mapping(b))


def temiz(v):
    if v is None:
        return None
    if isinstance(v, (np.integer,)):
        return int(v)
    if isinstance(v, (np.floating,)):
        v = float(v)
    if isinstance(v, float) and not np.isfinite(v):
        return None
    try:
        if pd.isna(v):
            return None
    except (TypeError, ValueError):
        pass
    return v


def main():
    if TESLIM.exists():
        shutil.rmtree(TESLIM)
    TESLIM.mkdir()

    ham = pd.read_csv(KOK / "turkiye_grid.csv", low_memory=False)
    eg = pd.read_csv(KOK / "egitim_seti.csv", encoding="utf-8-sig")
    ozet = pd.read_csv(KOK / "yangin_ozeti.csv")
    ozet["uyari"] = ozet["uyari"].fillna("")
    yanginlar = {k["id"]: k for k in
                 json.loads((KOK / "yanginlar.json").read_text(encoding="utf-8"))}

    print("=" * 76)
    print("TAM TESLIM PAKETI")
    print("=" * 76)
    print("  yangin: {}  |  ham hucre: {:,}  |  egitim: {:,} satir / {} grup"
          .format(len(yanginlar), len(ham), len(eg), eg["grup_id"].nunique()))

    # ------------------------------------------------- 1) modeli egit
    print("\n[1/4] Model egitiliyor ({})".format(MODEL_SURUM))
    Xe, ye = hazirla(eg), eg["kalan_acik"].values
    model = yeni_model().fit(Xe, ye)
    joblib.dump({"model": model, "oznitelikler": IC, "surum": MODEL_SURUM,
                 "egitim_satir": len(eg),
                 "egitim_grup": int(eg["grup_id"].nunique()),
                 "egitim_tarihi": datetime.now(timezone.utc).isoformat()},
                KOK / "model.joblib")
    print("      model.joblib yazildi ({:,} satir ile egitildi)".format(len(eg)))

    # ------------------------------------- 2) kat disi tahminler (durustluk)
    print("\n[2/4] Kat disi tahminler (egitimdeki hucreler icin)")
    # LeaveOneGroupOut: her mekansal grup sirayla disarida. GroupKFold(5)
    # yerine bu, cunku raporladigimiz metrik de grup bazinda ve en buyuk
    # grup (Manavgat) verinin %37'si - 5 katta katlar cok dengesiz oluyordu.
    g = eg["grup_id"].values
    oof = np.full(len(eg), np.nan)
    for tr, te in LeaveOneGroupOut().split(Xe, ye, g):
        oof[te] = yeni_model().fit(Xe[tr], ye[tr]).predict(Xe[te])
    oof_harita = dict(zip(eg["hucre_id"], oof))
    print("      {:,} hucre icin kat disi tahmin hazir".format(len(oof_harita)))

    # kat disi tahminlerden performans: manifest'e ve sunuma tek kaynak
    olcum = performans(ye, oof, g, eg["dnbr"].to_numpy())
    print("      grup ici Spearman {:+.3f} | {}/{} grup pozitif | top-%20 {:.1%}"
          .format(olcum["within_fire_spearman_mean"],
                  olcum["positive_groups"], olcum["group_count"],
                  olcum["top20_hit_rate"]))

    # ------------------------------------------------- 3) yangin yangin
    print("\n[3/4] Yanginlar isleniyor")
    kayitlar, uretim = [], datetime.now(timezone.utc).replace(
        microsecond=0).isoformat()

    for i, (yid, y) in enumerate(sorted(yanginlar.items()), 1):
        df = ham[ham["yangin_id"] == yid].reset_index(drop=True)
        t = df[df["yanik"].fillna(False)].copy()
        if len(t) == 0:
            print("  [{:>2}] {:<14} yanik hucre yok, atlandi".format(i, yid))
            continue

        X = hazirla(t)
        tahmin = model.predict(X)
        # egitimde olan hucrelerde kat disi tahmini kullan
        oof_var = t["hucre_id"].map(oof_harita)
        tahmin = np.where(oof_var.notna(), oof_var.fillna(0).values, tahmin)
        n_oof = int(oof_var.notna().sum())

        t = t.rename(columns=YENIDEN_ADLANDIR)
        t["prediction_status"] = oncelik.durum_belirle(t)
        t["recovery_gap_pred"] = np.round(tahmin, 4)
        t.loc[t["prediction_status"] != "predicted", "recovery_gap_pred"] = np.nan
        norm_ref = oncelik.referans_cikar(t)
        t = oncelik.oncelik_hesapla(t, referans=norm_ref)

        satir = ozet[ozet["yangin_id"] == yid]
        uyari = satir["uyari"].iloc[0] if len(satir) else ""
        bayrak = "check" if "alan uyusmuyor" in uyari else "ok"

        sinir = sinir_poligonu(y, df)
        if sinir:
            (TESLIM / "{}_sinir.geojson".format(yid)).write_text(json.dumps({
                "type": "Feature",
                "properties": {"fire_id": yid, "fire_date": y["tarih"],
                               "province": y.get("il"), "region": y.get("bolge"),
                               "modis_area_ha": y["alan_ha"]},
                "geometry": sinir}, ensure_ascii=False), encoding="utf-8")

        t[ALANLAR].to_csv(TESLIM / "{}_hucreler.csv".format(yid),
                          index=False, encoding="utf-8-sig")

        durum = t["prediction_status"].value_counts().to_dict()
        meta = {
            "fire_id": yid, "fire_date": y["tarih"],
            "province": y.get("il"), "region": y.get("bolge"),
            "modis_area_ha": y["alan_ha"],
            "cell_size_m": pt.HUCRE_M, "cell_count": len(t),
            "burned_area_ha": round(len(t) * (pt.HUCRE_M ** 2) / 10_000, 1),
            "crs": "EPSG:4326",
            "schema_version": SEMA_SURUM, "model_version": MODEL_SURUM,
            "generated_at": uretim,
            "quality_flag": bayrak,
            "quality_note": uyari or None,
            "in_training_set": n_oof > 0,
            "out_of_fold_cells": n_oof,
            "status_counts": {k: int(v) for k, v in durum.items()},
            "priority_weights": oncelik.VARSAYILAN_AGIRLIK,
            "priority_thresholds": {ad: e for e, ad in oncelik.ESIKLER},
            # Normalizasyonda kullanilan min/max. Alt kume uzerinde calisan
            # taraf bunlari kullanmali, kendi min/max'ini hesaplamamali.
            "normalization_reference": norm_ref,
            "has_perimeter": sinir is not None,
        }
        (TESLIM / "{}_metadata.json".format(yid)).write_text(
            json.dumps(meta, ensure_ascii=False, indent=2), encoding="utf-8")
        kayitlar.append(meta)

        print("  [{:>2}] {:<14} {:>5} hucre  {:<6} {}".format(
            i, yid, len(t), bayrak,
            "  ".join("{} {}".format(k[:4], v) for k, v in durum.items())))

    # ------------------------------------------------------ 4) manifest
    print("\n[4/4] manifest.json")
    manifest = {
        "project": "ReGreen / FireRecover",
        "generated_at": uretim,
        "model_version": MODEL_SURUM,
        "schema_version": SEMA_SURUM,
        "cell_size_m": pt.HUCRE_M,
        "crs": "EPSG:4326",
        "fire_count": len(kayitlar),
        "total_cells": int(sum(k["cell_count"] for k in kayitlar)),
        "training_rows": len(eg),
        "training_groups": int(eg["grup_id"].nunique()),
        "features": IC,
        # Kat disi (out-of-fold) olcum: LeaveOneGroupOut, 27 mekansal grup.
        # Her sayi, modelin O YANGINI HIC GORMEDIGI durumda uretildi.
        # Ayrinti ve nasil olculdugu: MODEL_GUNLUGU.md
        "model_performance": olcum,
        "priority_weights": oncelik.VARSAYILAN_AGIRLIK,
        "priority_thresholds": {ad: e for e, ad in oncelik.ESIKLER},
        "files_per_fire": ["{fire_id}_hucreler.csv",
                           "{fire_id}_sinir.geojson",
                           "{fire_id}_metadata.json"],
        "fires": [{"fire_id": k["fire_id"], "fire_date": k["fire_date"],
                   "province": k["province"], "region": k["region"],
                   "cell_count": k["cell_count"],
                   "burned_area_ha": k["burned_area_ha"],
                   "quality_flag": k["quality_flag"],
                   "has_perimeter": k["has_perimeter"],
                   "status_counts": k["status_counts"]} for k in kayitlar],
    }
    (TESLIM / "manifest.json").write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")

    for ek, hedef in (("alan_eslesme.json", "alan_eslesme.json"),
                      ("oncelik.py", "oncelik.py"),
                      ("teslim_okubeni.md", "OKUBENI.md")):
        if (KOK / ek).exists():
            shutil.copy(KOK / ek, TESLIM / hedef)

    boyut = sum(f.stat().st_size for f in TESLIM.iterdir()) / 1024 / 1024
    print("\n" + "=" * 76)
    print("SONUC")
    print("=" * 76)
    print("  yangin        : {}".format(len(kayitlar)))
    print("  toplam hucre  : {:,}".format(manifest["total_cells"]))
    print("  dosya         : {}".format(len(list(TESLIM.iterdir()))))
    print("  paket boyutu  : {:.1f} MB".format(boyut))
    print("  quality check : {}".format(
        sum(1 for k in kayitlar if k["quality_flag"] == "check")))
    print("  sinirsiz      : {}".format(
        sum(1 for k in kayitlar if not k["has_perimeter"])))
    t = {}
    for k in kayitlar:
        for d, n in k["status_counts"].items():
            t[d] = t.get(d, 0) + n
    print("  durum dagilimi: " + "  ".join(
        "{} {:,} (%{:.0f})".format(d, n, n / manifest["total_cells"] * 100)
        for d, n in sorted(t.items())))
    print("\n  -> teslim/ klasoru hazir")


if __name__ == "__main__":
    main()
