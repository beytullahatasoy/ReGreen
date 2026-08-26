# -*- coding: utf-8 -*-
"""ReGreen model egitimi - defterin (ReGreen_model_egitimi.ipynb) yerel hali.

Ayni mantik, ayni metrikler. Fark: 16 cekirdek kullaniyor ve arka planda
kesintisiz kosuyor.

  Asama 1  model ailesi karsilastirmasi   (LOGO, ayar yok)
  Asama 2  hiperparametre aramasi         (IC ICE CV - durust olcum)
  Asama 3  final model + kayit

Butun olcumler LeaveOneGroupOut ve GRUP ICI. Gruplar arasi kayma var
(grup ortalamalari 0,123-0,405), gruplari karistiran metrik yaniltir.
"""
import pathlib
import sys, time, json, warnings
import numpy as np, pandas as pd
warnings.filterwarnings("ignore")
sys.stdout.reconfigure(encoding="utf-8")

from scipy.stats import spearmanr
from sklearn.model_selection import LeaveOneGroupOut, GroupKFold
from sklearn.ensemble import RandomForestRegressor, HistGradientBoostingRegressor
from sklearn.linear_model import Ridge
from sklearn.pipeline import make_pipeline
from sklearn.preprocessing import StandardScaler
from sklearn.impute import SimpleImputer
from sklearn.inspection import permutation_importance
from lightgbm import LGBMRegressor
from xgboost import XGBRegressor

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent))
import yollar
yollar.yol_ekle()

OZNITELIK = ["agac_orani", "agac_orani_y", "dnbr", "egim_derece",
             "yukselti_m", "ndvi_dusus", "yol_mesafe_km"]
HEDEF, GRUP, TOHUM = "kalan_acik", "grup_id", 0
N_DENEME = 40          # yerelde bol butce
IC_KAT = 4


def bilgi(*a):
    print(*a, flush=True)


# ------------------------------------------------------------- oznitelik
def X_hazirla(d, nan="yerel"):
    """Oznitelik matrisini hazirlar.

    SIZINTI NOTU: burada SADECE satir ici islem yapiyoruz. Eskiden
    yukselti_m'in eksikleri TUM VERININ medyaniyla dolduruluyordu - bu,
    LOGO ayriminden once yapildigi icin test grubunun bilgisini egitime
    sizdiriyordu. Etkisi kucuktu (16.074 satirin 3'u) ama ilke olarak
    yanlisti.

    Duzeltme: medyan doldurma boru hattina tasindi (SimpleImputer), boylece
    her katta SADECE o katin egitim verisinden hesaplaniyor.

    agac_orani_y -> agac_orani doldurmasi satir ici; baska satirdan bilgi
    almiyor, sizinti degil, burada kalabilir.
    """
    X = d[OZNITELIK].copy()
    if nan == "yerel":
        return X
    bayrak = X["agac_orani_y"].isna().astype("int8")
    X["agac_orani_y"] = X["agac_orani_y"].fillna(X["agac_orani"])
    if nan == "doldur_bayrak":
        X["agac_orani_y_eksik"] = bayrak
    return X


def agirlik_hesapla(gruplar, sema="yok"):
    if sema == "yok":
        return None
    s = pd.Series(gruplar)
    ham = len(s) / (s.nunique() * s.map(s.value_counts()))
    a = {"ham": ham, "kok": np.sqrt(ham), "tavan": ham.clip(upper=3.0)}[sema]
    return (a / a.mean()).to_numpy()


# ---------------------------------------------------------------- metrik
def top_isabet(y, p, oran=0.20):
    n = len(y); k = max(1, int(round(n * oran)))
    if k >= n:
        return np.nan
    ger = set(np.argsort(-np.asarray(y))[:k])
    return sum(1 for i in np.argsort(-np.asarray(p))[:k] if i in ger) / k


def grup_metrik(y, p, g, taban=None):
    df = pd.DataFrame({"y": y, "p": p, "g": g})
    if taban is not None:
        df["t"] = taban
    satir = []
    for gr, s in df.groupby("g"):
        if len(s) < 8 or s["y"].nunique() < 3:
            continue
        r = {"grup": gr, "n": len(s),
             "rho": spearmanr(s["y"], s["p"]).statistic,
             "top20": top_isabet(s["y"].values, s["p"].values)}
        if taban is not None:
            r["top20_taban"] = top_isabet(s["y"].values, s["t"].values)
        satir.append(r)
    return pd.DataFrame(satir)


def ozetle(gm):
    p = gm["rho"].dropna()
    o = {"n_grup": len(gm), "rho_ort": p.mean(), "rho_med": p.median(),
         "poz": int((p > 0).sum()), "en_kotu": p.min(), "en_iyi": p.max(),
         "top20": gm["top20"].mean()}
    if "top20_taban" in gm:
        o["top20_taban"] = gm["top20_taban"].mean()
    return o


def yaz(ad, o, fark=None):
    e = "" if fark is None else "  (%+0.4f)" % fark
    bilgi("%-26s %+7.4f %+7.4f %3d/%-3d %+8.4f %7.1f%%%s" %
          (ad, o["rho_ort"], o["rho_med"], o["poz"], o["n_grup"],
           o["en_kotu"], 100 * o["top20"], e))


BASLIK = "%-26s %7s %7s %7s %8s %8s" % ("model", "rho_ort", "rho_med",
                                        "poz", "en_kotu", "top20")


def _agirlikli_fit(m, X, y, w):
    """sample_weight'i modele gecirir.

    Modeller artik Pipeline icinde (SimpleImputer + tahminci). Pipeline.fit
    dogrudan sample_weight kabul etmiyor, son adima yonlendirmek gerekiyor:
    `pipeline.fit(X, y, sonadim__sample_weight=w)`.
    """
    if hasattr(m, "steps"):
        m.fit(X, y, **{"%s__sample_weight" % m.steps[-1][0]: w})
    else:
        m.fit(X, y, sample_weight=w)


def logo_tahmin(kur, X, y, g, sema="yok"):
    oof = np.full(len(y), np.nan)
    for tr, te in LeaveOneGroupOut().split(X, y, g):
        m = kur()
        w = agirlik_hesapla(g[tr], sema)
        if w is None:
            m.fit(X.iloc[tr], y[tr])
        else:
            _agirlikli_fit(m, X.iloc[tr], y[tr], w)
        oof[te] = m.predict(X.iloc[te])
    return oof


# ------------------------------------------------------------------ veri
T0 = time.time()
yollar.gerekli(yollar.ARA / "egitim_seti.csv")
d = pd.read_csv(yollar.ARA / "egitim_seti.csv")
y, g = d[HEDEF].to_numpy(), d[GRUP].to_numpy()
taban_dnbr = d["dnbr"].to_numpy()

bilgi("=" * 78)
bilgi("REGREEN MODEL EGITIMI")
bilgi("=" * 78)
bilgi("  %d satir | %d yangin | %d grup | %d oznitelik" %
      (len(d), d.yangin_id.nunique(), d[GRUP].nunique(), len(OZNITELIK)))
bilgi("  oznitelikler: " + ", ".join(OZNITELIK))
bilgi("  N_DENEME=%d  IC_KAT=%d  TOHUM=%d" % (N_DENEME, IC_KAT, TOHUM))


# -------------------------------------------------------------- ASAMA 1
def RF(**k):
    """RandomForest. SimpleImputer boru hattin ICINDE - medyan her katta
    sadece o katin egitim verisinden hesaplansin diye (sizinti onlemi)."""
    v = dict(n_estimators=400, min_samples_leaf=5, random_state=TOHUM, n_jobs=-1)
    v.update(k)
    return make_pipeline(SimpleImputer(strategy="median"),
                         RandomForestRegressor(**v))


def RIDGE(**k):
    """Ridge. Imputer + olcekleme boru hattin icinde, ayni sizinti onlemi."""
    return make_pipeline(SimpleImputer(strategy="median"),
                         StandardScaler(), Ridge(alpha=k.get("alpha", 1.0)))


DENEY = [
    ("RF taban",           "doldur",        "yok", RF),
    ("RF + eksik bayragi", "doldur_bayrak", "yok", RF),
    ("RF + kok agirlik",   "doldur",        "kok", RF),
    ("HistGB",             "yerel",         "yok",
     lambda: HistGradientBoostingRegressor(random_state=TOHUM)),
    ("LightGBM",           "yerel",         "yok",
     lambda: LGBMRegressor(random_state=TOHUM, n_jobs=-1, verbose=-1)),
    ("XGBoost",            "yerel",         "yok",
     lambda: XGBRegressor(random_state=TOHUM, n_jobs=-1, tree_method="hist")),
    ("Ridge (dogrusal)",   "doldur",        "yok", RIDGE),
]

bilgi("\n" + "=" * 78)
bilgi("ASAMA 1 - MODEL AILESI (LeaveOneGroupOut, ayar yok)")
bilgi("=" * 78)
bilgi(BASLIK)
bilgi("-" * 78)

a1, oof_saklanan = [], {}
for ad, nan, sema, kur in DENEY:
    t = time.time()
    oof = logo_tahmin(kur, X_hazirla(d, nan), y, g, sema)
    o = ozetle(grup_metrik(y, oof, g, taban_dnbr))
    o.update({"ad": ad, "nan": nan, "agirlik": sema, "sn": round(time.time() - t, 1)})
    a1.append(o); oof_saklanan[ad] = oof
    yaz(ad, o)

bilgi("-" * 78)
o_dnbr = ozetle(grup_metrik(y, taban_dnbr, g))
yaz("dNBR (SAHA TABANI)", o_dnbr)

asama1 = pd.DataFrame(a1).sort_values("rho_ort", ascending=False)
asama1.to_csv(yollar.CIKTI / "sonuc_asama1.csv", index=False)
np.savez(yollar.CIKTI / "sonuc_oof_asama1.npz", **oof_saklanan)
bilgi("\nSIRALAMA")
bilgi(asama1[["ad", "rho_ort", "rho_med", "poz", "en_kotu", "top20", "sn"]]
      .to_string(index=False))
bilgi("\n  gecen sure: %.1f dk" % ((time.time() - T0) / 60))


# -------------------------------------------------------------- ASAMA 2
def grup_ici_rho(y, p, g):
    df = pd.DataFrame({"y": y, "p": p, "g": g}); r = []
    for _, s in df.groupby("g"):
        if len(s) < 8 or s["y"].nunique() < 3:
            continue
        v = spearmanr(s["y"], s["p"]).statistic
        if np.isfinite(v):
            r.append(v)
    return float(np.mean(r)) if r else -1.0


def ic_skor(kur, p, X, y, g, kat=IC_KAT):
    kat = min(kat, len(np.unique(g)))
    oof = np.full(len(y), np.nan)
    for tr, te in GroupKFold(n_splits=kat).split(X, y, g):
        m = kur(**p); m.fit(X.iloc[tr], y[tr]); oof[te] = m.predict(X.iloc[te])
    return grup_ici_rho(y, oof, g)


def rastgele_ara(kur, uzay, X, y, g, n=N_DENEME, tohum=TOHUM):
    rng = np.random.default_rng(tohum)
    en_iyi, en_skor = None, -np.inf
    for _ in range(n):
        p = {k: (v[int(rng.integers(len(v)))] if isinstance(v, list) else v(rng))
             for k, v in uzay.items()}
        s = ic_skor(kur, p, X, y, g)
        if s > en_skor:
            en_iyi, en_skor = p, s
    return en_iyi, en_skor


AILE = asama1.iloc[0]["ad"]
# RF ic ice CV'de cok yavas (27 kat x 40 deneme x 4 ic kat). Kazanirsa
# ayar icin en iyi boostinge geciyoruz. Ridge hizli, dogrudan ayarlanabilir.
if AILE.startswith("RF"):
    boost = asama1[asama1.ad.isin(["HistGB", "LightGBM", "XGBoost"])]
    AILE = boost.iloc[0]["ad"]
    bilgi("\n  not: Asama 1'i RF kazandi ama ic ice CV'de cok yavas kaliyor."
          "\n       Ayar icin en iyi boosting secildi: %s" % AILE)

if AILE.startswith("Ridge"):
    # Dogrusal model kazandiysa onu ayarlamak lazim, boostinge kacmak degil.
    # Iki eksende ariyoruz: ceza gucu (alpha) ve ETKILESIM terimleri.
    # Etkilesim onemli bir soru: "dik yamac + agir yangin" birlikte,
    # ayri ayri toplamlarindan daha mi kotu? Duz Ridge bunu goremez.
    from sklearn.preprocessing import PolynomialFeatures
    NAN = "doldur_bayrak"

    def kur(alpha=1.0, etkilesim=False, derece=2):
        adim = [SimpleImputer(strategy="median"), StandardScaler()]
        if etkilesim:
            adim.append(PolynomialFeatures(derece, interaction_only=True,
                                           include_bias=False))
            adim.append(StandardScaler())
        adim.append(Ridge(alpha=alpha))
        return make_pipeline(*adim)

    UZAY = {"alpha": lambda r: float(10 ** r.uniform(-3, 3)),
            "etkilesim": [False, True, True],      # etkilesime daha cok sans
            "derece": [2]}
elif AILE == "HistGB":
    NAN = "yerel"
    kur = lambda **k: HistGradientBoostingRegressor(random_state=TOHUM, **k)
    UZAY = {"max_iter": [200, 400, 700, 1000],
            "learning_rate": lambda r: float(10 ** r.uniform(-2.2, -0.7)),
            "max_leaf_nodes": [15, 31, 63, 127],
            "min_samples_leaf": [10, 20, 40, 80],
            "l2_regularization": lambda r: float(10 ** r.uniform(-3, 1)),
            "max_features": lambda r: float(r.uniform(0.5, 1.0))}
elif AILE == "LightGBM":
    NAN = "yerel"
    kur = lambda **k: LGBMRegressor(random_state=TOHUM, n_jobs=-1, verbose=-1, **k)
    UZAY = {"n_estimators": [200, 400, 700, 1000],
            "learning_rate": lambda r: float(10 ** r.uniform(-2.2, -0.7)),
            "num_leaves": [15, 31, 63, 127],
            "min_child_samples": [10, 20, 40, 80],
            "subsample": lambda r: float(r.uniform(0.6, 1.0)),
            "subsample_freq": [1],
            "colsample_bytree": lambda r: float(r.uniform(0.6, 1.0)),
            "reg_lambda": lambda r: float(10 ** r.uniform(-2, 1.5))}
else:
    NAN = "yerel"
    kur = lambda **k: XGBRegressor(random_state=TOHUM, n_jobs=-1,
                                   tree_method="hist", **k)
    UZAY = {"n_estimators": [200, 400, 700],
            "learning_rate": lambda r: float(10 ** r.uniform(-2.2, -0.7)),
            "max_depth": [3, 4, 6, 8],
            "min_child_weight": [1, 5, 20],
            "subsample": lambda r: float(r.uniform(0.6, 1.0)),
            "colsample_bytree": lambda r: float(r.uniform(0.6, 1.0)),
            "reg_lambda": lambda r: float(10 ** r.uniform(-2, 1.5))}

X = X_hazirla(d, NAN)
bilgi("\n" + "=" * 78)
bilgi("ASAMA 2 - IC ICE CV ile HIPERPARAMETRE ARAMASI")
bilgi("=" * 78)
bilgi("  aile: %s | nan: %s | %d kat x %d deneme x %d ic kat = %d egitim"
      % (AILE, NAN, d[GRUP].nunique(), N_DENEME, IC_KAT,
         d[GRUP].nunique() * N_DENEME * IC_KAT))
bilgi("  DIS kat sadece RAPOR eder, arama sirasinda hic gorulmez.\n")

t2 = time.time()
oof_ayarli = np.full(len(y), np.nan)
secilen = []
for i, (tr, te) in enumerate(LeaveOneGroupOut().split(X, y, g), 1):
    p, s = rastgele_ara(kur, UZAY, X.iloc[tr], y[tr], g[tr])
    m = kur(**p); m.fit(X.iloc[tr], y[tr])
    oof_ayarli[te] = m.predict(X.iloc[te])
    secilen.append({"grup": g[te][0], **p, "ic_rho": round(s, 4)})
    bilgi("  %2d/%d  %-14s ic_rho %+0.4f   (%.1f dk)" %
          (i, d[GRUP].nunique(), g[te][0][:14], s, (time.time() - t2) / 60))

ayar = pd.DataFrame(secilen)
ayar.to_csv(yollar.CIKTI / "sonuc_asama2_parametreler.csv", index=False)

o_ayar = ozetle(grup_metrik(y, oof_ayarli, g, taban_dnbr))
o_taban = ozetle(grup_metrik(y, oof_saklanan[asama1.iloc[0]["ad"]], g, taban_dnbr))

bilgi("\n" + BASLIK)
bilgi("-" * 78)
yaz("Asama 1 en iyisi", o_taban)
yaz(AILE + " ayarli", o_ayar, o_ayar["rho_ort"] - o_taban["rho_ort"])
bilgi("-" * 78)
bilgi("\nKAZANC")
for k, ad in [("rho_ort", "rho ortalama"), ("rho_med", "rho medyan"),
              ("en_kotu", "en kotu grup"), ("top20", "top-%20")]:
    bilgi("  %-16s %+0.4f -> %+0.4f   (%+0.4f)" %
          (ad, o_taban[k], o_ayar[k], o_ayar[k] - o_taban[k]))
bilgi("  %-16s %d/%d -> %d/%d" % ("pozitif grup", o_taban["poz"],
      o_taban["n_grup"], o_ayar["poz"], o_ayar["n_grup"]))


# -------------------------------------------------------------- ASAMA 3
KAZANC = o_ayar["rho_ort"] - o_taban["rho_ort"]
KULLAN = "ayarli" if KAZANC >= 0.02 else "taban"
bilgi("\n" + "=" * 78)
bilgi("ASAMA 3 - FINAL MODEL")
bilgi("=" * 78)
bilgi("  kazanc %+0.4f -> secim: %s  (esik 0.02)" % (KAZANC, KULLAN.upper()))

if KULLAN == "ayarli":
    en_p, en_s = rastgele_ara(kur, UZAY, X, y, g)
    final, final_nan, final_oof = kur(**en_p), NAN, oof_ayarli
else:
    sat = asama1.iloc[0]
    en_p = {"not": "Asama 1 varsayilanlari"}
    final_nan, final_oof = sat["nan"], oof_saklanan[sat["ad"]]
    kurucu = {ad: k for ad, _n, _s, k in DENEY}
    final = kurucu[sat["ad"]]()

Xf = X_hazirla(d, final_nan)
final.fit(Xf, y)
bilgi("  %s | %d satir | %d oznitelik" %
      (type(final).__name__, len(Xf), Xf.shape[1]))
if isinstance(en_p, dict) and "not" not in en_p:
    for k, v in en_p.items():
        bilgi("     %-20s %s" % (k, v))

# oznitelik onemi
# Modeller artik Pipeline icinde sarili (SimpleImputer + ...), o yuzden
# once son adima bakiyoruz. HistGB'nin feature_importances_ ozelligi yok,
# o permutasyona dusuyor.
_son = final[-1] if hasattr(final, "__getitem__") else final
if hasattr(_son, "feature_importances_"):
    onem = pd.Series(_son.feature_importances_, index=Xf.columns)
    yontem = "dahili (impurity)"
elif hasattr(_son, "coef_"):
    onem = pd.Series(np.abs(_son.coef_), index=Xf.columns)
    yontem = "katsayi buyuklugu"
else:
    pi = permutation_importance(final, Xf, y, n_repeats=10,
                                random_state=TOHUM, n_jobs=-1)
    onem = pd.Series(np.clip(pi.importances_mean, 0, None), index=Xf.columns)
    yontem = "permutasyon"
onem = (100 * onem / onem.sum()).sort_values(ascending=False)

bilgi("\nOZNITELIK ONEMI (%%) - %s" % yontem)
for k, v in onem.items():
    bilgi("  %-24s %5.1f  %s" % (k, v, "#" * int(round(v / 2))))

# kayit
import joblib
son = ozetle(grup_metrik(y, final_oof, g, taban_dnbr))
joblib.dump({"model": final, "oznitelik": list(Xf.columns), "nan": final_nan,
             "parametre": en_p, "hedef": HEDEF, "aile": AILE,
             "olcum": {k: float(v) for k, v in son.items()}},
            yollar.CIKTI / "regreen_model.joblib")
d[[GRUP, "yangin_id", "hucre_id", HEDEF]].assign(oof_tahmin=final_oof) \
    .to_csv(yollar.CIKTI / "sonuc_oof_tahminler.csv", index=False)
grup_metrik(y, final_oof, g, taban_dnbr).to_csv(yollar.CIKTI / "sonuc_grup_skorlari.csv", index=False)
onem.to_csv(yollar.CIKTI / "sonuc_oznitelik_onemi.csv")

bilgi("\n" + "=" * 78)
bilgi("SONUC")
bilgi("=" * 78)
bilgi("  rho ortalama    %+0.4f" % son["rho_ort"])
bilgi("  rho medyan      %+0.4f" % son["rho_med"])
bilgi("  pozitif grup    %d/%d" % (son["poz"], son["n_grup"]))
bilgi("  en kotu grup    %+0.4f" % son["en_kotu"])
bilgi("  en iyi grup     %+0.4f" % son["en_iyi"])
bilgi("  top-%%20 isabet  %.1f%%   (dNBR tabani %.1f%%)" %
      (100 * son["top20"], 100 * son["top20_taban"]))
bilgi("\n  toplam sure: %.1f dk" % ((time.time() - T0) / 60))
bilgi("  dosyalar: regreen_model.joblib, sonuc_*.csv")
