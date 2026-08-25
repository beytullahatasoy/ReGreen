# -*- coding: utf-8 -*-
"""Farkli YONTEMLER - ayni modelin ayarini degil, ogrenme bicimini degistir.

Su ana kadarki kor nokta: SIRALAMA olcuyoruz (Spearman, top-%20) ama
modeli KARE HATA ile egitiyoruz. Model "0,31 mi 0,34 mu" dogru bilmeye
calisiyor; bizim umursadigimiz "hangisi daha kotu".

Denenecek dort farkli yaklasim:

  1) SIRALAMA HEDEFI   hedefi yangin ici yuzdelik siraya cevir
  2) SIRALAMA KAYBI    LambdaRank - dogrudan siralama optimize eder
  3) SINIFLANDIRMA     "en kotu %20 icinde mi" ikili problemi.
                       Urun metrigi zaten bu.
  4) MONOTONLUK KISITI Iliskilerin yonunu BILIYORUZ (egim artinca kotu,
                       agac ortusu artinca kotu...). Modele bunu dayatmak
                       gorulmemis yangina genellemede yardim edebilir -
                       LOGO'daki asil zorluk bu.
  5) SAGLAM DOGRUSAL   Ridge kazandigi icin Huber / kantil / ElasticNet

Kural: hepsini olc, hepsini raporla. En iyisini secip "kazandik" DEME -
deney 6'da secim yanliliginin +0,016 oldugunu gorduk. Bir sey one
cikarsa ic ice CV ile ayrica dogrulanacak.
"""
import sys, time, warnings
import numpy as np, pandas as pd
warnings.filterwarnings("ignore")
sys.stdout.reconfigure(encoding="utf-8")

from scipy.stats import spearmanr
from sklearn.model_selection import LeaveOneGroupOut
from sklearn.ensemble import HistGradientBoostingRegressor
from sklearn.linear_model import (Ridge, HuberRegressor, QuantileRegressor,
                                  ElasticNet, LogisticRegression)
from sklearn.pipeline import make_pipeline
from sklearn.preprocessing import StandardScaler
from sklearn.impute import SimpleImputer
from lightgbm import LGBMRegressor, LGBMRanker, LGBMClassifier

OZ = ["agac_orani", "agac_orani_y", "dnbr", "egim_derece",
      "yukselti_m", "ndvi_dusus", "yol_mesafe_km"]
HEDEF, GRUP, YANGIN, TOHUM = "kalan_acik", "grup_id", "yangin_id", 0

# Iliskilerin bilinen yonu: hepsi POZITIF (artinca kalan acik artar).
# Grup bazinda tutarlilik testinden geliyor, uydurma degil.
YON = [1] * len(OZ)


def bilgi(*a):
    print(*a, flush=True)


def top_isabet(y, p, oran=0.20):
    n = len(y); k = max(1, int(round(n * oran)))
    if k >= n:
        return np.nan
    ger = set(np.argsort(-np.asarray(y))[:k])
    return sum(1 for i in np.argsort(-np.asarray(p))[:k] if i in ger) / k


def olc(y, p, g):
    df = pd.DataFrame({"y": y, "p": p, "g": g}); r, t = [], []
    for _, s in df.groupby("g"):
        if len(s) < 8 or s["y"].nunique() < 3:
            continue
        v = spearmanr(s["y"], s["p"]).statistic
        if np.isfinite(v):
            r.append(v); t.append(top_isabet(s["y"].values, s["p"].values))
    r = np.array(r)
    return {"rho": r.mean(), "med": np.median(r), "poz": int((r > 0).sum()),
            "n": len(r), "kotu": r.min(), "top20": np.nanmean(t)}


# ------------------------------------------------------------------ veri
d = pd.read_csv("egitim_seti.csv").reset_index(drop=True)
y = d[HEDEF].to_numpy()
g = d[GRUP].to_numpy()
yang = d[YANGIN].to_numpy()
X = d[OZ].copy()
Xd = X.copy()                                   # doldurulmus (dogrusal icin)
Xd["agac_orani_y"] = Xd["agac_orani_y"].fillna(Xd["agac_orani"])
Xd = Xd.fillna(Xd.median())

# yangin ici yuzdelik sira hedefi (0-1)
YS = d.groupby(YANGIN)[HEDEF].rank(pct=True).to_numpy()
# yangin ici onlu kademe (LambdaRank icin tamsayi etiket)
YK = (d.groupby(YANGIN)[HEDEF].rank(pct=True) * 9.999).astype(int).to_numpy()
# en kotu %20 mi (ikili)
YB = (d.groupby(YANGIN)[HEDEF].rank(pct=True) > 0.80).astype(int).to_numpy()

bilgi("=" * 88)
bilgi("FARKLI YONTEMLER")
bilgi("=" * 88)
bilgi("  %d satir | %d grup | %d yangin" % (len(d), len(set(g)), len(set(yang))))
bilgi("  en kotu %%20 etiketi: %d pozitif (%.1f%%)" % (YB.sum(), 100 * YB.mean()))


# ------------------------------------------------------------- kosucular
def kos_duz(kur, Xi, hedef):
    oof = np.full(len(y), np.nan)
    for tr, te in LeaveOneGroupOut().split(Xi, hedef, g):
        m = kur(); m.fit(Xi.iloc[tr], hedef[tr]); oof[te] = m.predict(Xi.iloc[te])
    return oof


def kos_sinif(kur, Xi, hedef):
    oof = np.full(len(y), np.nan)
    for tr, te in LeaveOneGroupOut().split(Xi, hedef, g):
        m = kur(); m.fit(Xi.iloc[tr], hedef[tr])
        oof[te] = m.predict_proba(Xi.iloc[te])[:, 1]
    return oof


def kos_sirala(Xi, etiket):
    """LambdaRank: her YANGIN bir sorgu. Satirlar yangin bazinda bitisik olmali."""
    oof = np.full(len(y), np.nan)
    for tr, te in LeaveOneGroupOut().split(Xi, etiket, g):
        s = np.argsort(yang[tr], kind="stable")       # yanginlari bitisik yap
        tr_s = tr[s]
        _, boy = np.unique(yang[tr_s], return_counts=True)
        m = LGBMRanker(objective="lambdarank", random_state=TOHUM,
                       n_jobs=-1, verbose=-1, n_estimators=300,
                       label_gain=[2 ** i - 1 for i in range(11)])
        m.fit(Xi.iloc[tr_s], etiket[tr_s], group=boy)
        oof[te] = m.predict(Xi.iloc[te])
    return oof


def RIDGE():
    return make_pipeline(SimpleImputer(strategy="median"),
                         StandardScaler(), Ridge(alpha=1.0))


DENEY = []


def ekle(ad, fn):
    t = time.time()
    try:
        p = fn()
        o = olc(y, p, g)
        o.update({"ad": ad, "sn": round(time.time() - t, 1)})
        DENEY.append(o)
        bilgi("%-38s %+8.4f %+8.4f %3d/%-2d %+8.4f %7.1f%%  %5.0fs" %
              (ad, o["rho"], o["med"], o["poz"], o["n"], o["kotu"],
               100 * o["top20"], o["sn"]))
    except Exception as e:
        bilgi("%-38s HATA: %s: %s" % (ad, type(e).__name__, str(e)[:40]))


bilgi("\n%-38s %8s %8s %6s %8s %8s %6s" %
      ("yontem", "rho", "medyan", "poz", "en_kotu", "top20", "sure"))
bilgi("-" * 88)

bilgi("\n--- referans " + "-" * 74)
ekle("Ridge ham hedef (TABAN)", lambda: kos_duz(RIDGE, Xd, y))
ekle("HistGB ham hedef", lambda: kos_duz(
    lambda: HistGradientBoostingRegressor(random_state=TOHUM), X, y))

bilgi("\n--- 1) SIRALAMA HEDEFI " + "-" * 64)
ekle("Ridge  <- yangin ici sira", lambda: kos_duz(RIDGE, Xd, YS))
ekle("HistGB <- yangin ici sira", lambda: kos_duz(
    lambda: HistGradientBoostingRegressor(random_state=TOHUM), X, YS))
ekle("LightGBM <- yangin ici sira", lambda: kos_duz(
    lambda: LGBMRegressor(random_state=TOHUM, n_jobs=-1, verbose=-1), X, YS))

bilgi("\n--- 2) SIRALAMA KAYBI (LambdaRank) " + "-" * 52)
ekle("LGBMRanker lambdarank", lambda: kos_sirala(X, YK))

bilgi("\n--- 3) SINIFLANDIRMA: en kotu %20 mi " + "-" * 50)
ekle("Lojistik regresyon", lambda: kos_sinif(
    lambda: make_pipeline(SimpleImputer(strategy="median"), StandardScaler(),
                          LogisticRegression(max_iter=2000)), Xd, YB))
ekle("LightGBM siniflandirici", lambda: kos_sinif(
    lambda: LGBMClassifier(random_state=TOHUM, n_jobs=-1, verbose=-1), X, YB))

bilgi("\n--- 4) MONOTONLUK KISITI " + "-" * 62)
ekle("HistGB monoton", lambda: kos_duz(
    lambda: HistGradientBoostingRegressor(random_state=TOHUM,
                                          monotonic_cst=YON), X, y))
ekle("HistGB monoton <- sira", lambda: kos_duz(
    lambda: HistGradientBoostingRegressor(random_state=TOHUM,
                                          monotonic_cst=YON), X, YS))
ekle("LightGBM monoton", lambda: kos_duz(
    lambda: LGBMRegressor(random_state=TOHUM, n_jobs=-1, verbose=-1,
                          monotone_constraints=YON), X, y))
ekle("LightGBM monoton <- sira", lambda: kos_duz(
    lambda: LGBMRegressor(random_state=TOHUM, n_jobs=-1, verbose=-1,
                          monotone_constraints=YON), X, YS))

bilgi("\n--- 5) SAGLAM DOGRUSAL " + "-" * 64)
ekle("Huber", lambda: kos_duz(
    lambda: make_pipeline(SimpleImputer(strategy="median"), StandardScaler(),
                          HuberRegressor(max_iter=500)), Xd, y))
ekle("Kantil %80 (kotu kuyruga odak)", lambda: kos_duz(
    lambda: make_pipeline(SimpleImputer(strategy="median"), StandardScaler(),
                          QuantileRegressor(quantile=0.8, alpha=0.01,
                                            solver="highs")), Xd, y))
ekle("ElasticNet", lambda: kos_duz(
    lambda: make_pipeline(SimpleImputer(strategy="median"), StandardScaler(),
                          ElasticNet(alpha=0.001, l1_ratio=0.5)), Xd, y))
ekle("Ridge <- sira, monoton yok", lambda: kos_duz(RIDGE, Xd, YS))

son = pd.DataFrame(DENEY).sort_values("rho", ascending=False)
son.to_csv("sonuc_deney8.csv", index=False)
taban = son[son.ad.str.contains("TABAN")]["rho"].iloc[0]
taban20 = son[son.ad.str.contains("TABAN")]["top20"].iloc[0]

bilgi("\n" + "=" * 88)
bilgi("SIRALAMA (taban: rho %+0.4f, top20 %.1f%%)" % (taban, 100 * taban20))
bilgi("=" * 88)
bilgi("%-38s %8s %8s %8s %9s" % ("yontem", "rho", "fark", "top20", "top20 fark"))
bilgi("-" * 88)
for _, x in son.iterrows():
    bilgi("%-38s %+8.4f %+8.4f %7.1f%% %+9.1f" %
          (x["ad"], x["rho"], x["rho"] - taban, 100 * x["top20"],
           100 * (x["top20"] - taban20)))
bilgi("\n-> sonuc_deney8.csv")
