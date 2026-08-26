# -*- coding: utf-8 -*-
"""Arazi ortusu SINIFI (orman / calilik / otlak) oznitelik olarak ise yarar mi?

Soru mesru: makilik yaninca dipten surer, kizilcam ormani surmez.
Ekolojik olarak fark var. Ama bizde zaten agac_orani ve agac_orani_y
var - bunlar SUREKLI agac ortusu yuzdesi. Sinif bilgisi bunlarin
ustune yeni bir sey katiyor mu, yoksa ayni seyi mi tekrarliyor?

Guncel kurulumla (7 oznitelik, HistGB, LOGO 27 grup) olculuyor.
"""
import pathlib
import sys, warnings
import numpy as np, pandas as pd
warnings.filterwarnings("ignore")
sys.stdout.reconfigure(encoding="utf-8")

from sklearn.ensemble import HistGradientBoostingRegressor
from sklearn.model_selection import LeaveOneGroupOut
from scipy.stats import spearmanr

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent))
import yollar
yollar.yol_ekle()

TEMEL = ["agac_orani", "agac_orani_y", "dnbr", "egim_derece",
         "yukselti_m", "ndvi_dusus", "yol_mesafe_km"]
HEDEF, GRUP, TOHUM = "kalan_acik", "grup_id", 0


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
    return {"rho": r.mean(), "poz": int((r > 0).sum()), "n": len(r),
            "kotu": r.min(), "top20": np.nanmean(t)}


def kos(d, sut, kategorik=None):
    X, y, g = d[sut], d[HEDEF].to_numpy(), d[GRUP].to_numpy()
    oof = np.full(len(y), np.nan)
    for tr, te in LeaveOneGroupOut().split(X, y, g):
        kw = {} if kategorik is None else {"categorical_features": kategorik}
        m = HistGradientBoostingRegressor(random_state=TOHUM, **kw)
        m.fit(X.iloc[tr], y[tr]); oof[te] = m.predict(X.iloc[te])
    return olc(y, oof, g)


d = pd.read_csv(yollar.ARA / "egitim_seti.csv")
y, g = d[HEDEF].to_numpy(), d[GRUP].to_numpy()

# ---- arazi sinifi: yangin ICINDE ne kadar degisiyor? --------------------
# metrigimiz grup ici oldugu icin yangin icinde sabit olan sey ise yaramaz
print("ARAZI SINIFININ YANGIN ICI CESITLILIGI")
cesit = d.groupby(GRUP)["arazi_kodu_y"].nunique()
print("   yangin basina ortalama sinif sayisi : %.1f" % cesit.mean())
print("   tek sinifli grup sayisi             : %d / %d" %
      ((cesit <= 1).sum(), len(cesit)))
hak = d.groupby(GRUP)["arazi_kodu_y"].apply(
    lambda s: s.value_counts(normalize=True).max())
print("   baskin sinifin ortalama payi        : %%%.1f" % (100 * hak.mean()))

# ---- agac_orani zaten sinifi ayirt ediyor mu? --------------------------
print("\nAGAC ORANI SINIFA GORE (sinif bilgisi zaten burada mi?)")
print(d.groupby("arazi_tipi_y")[["agac_orani", "agac_orani_y"]]
       .mean().round(3).to_string())

# ---- olcum ------------------------------------------------------------
print("\n" + "=" * 76)
print("%-38s %7s %7s %8s %8s" % ("", "rho", "poz", "en_kotu", "top20"))
print("-" * 76)

t = kos(d, TEMEL)
print("%-38s %+7.4f %3d/%-3d %+8.4f %7.1f%%" %
      ("7 oznitelik (taban)", t["rho"], t["poz"], t["n"], t["kotu"],
       100 * t["top20"]))

for ad, sut, kat in [
    ("+ arazi ikili (agaclik/otlak/diger)",
     TEMEL + ["arazi_agaclik", "arazi_otlak", "arazi_diger"], None),
    ("+ arazi sinifi (kategorik)",
     TEMEL + ["arazi_kodu_y"], [False] * 7 + [True]),
    ("+ sadece 'agaclik mi' (tek ikili)",
     TEMEL + ["arazi_agaclik"], None),
]:
    o = kos(d, sut, kat)
    print("%-38s %+7.4f %3d/%-3d %+8.4f %7.1f%%   (%+0.4f)" %
          (ad, o["rho"], o["poz"], o["n"], o["kotu"], 100 * o["top20"],
           o["rho"] - t["rho"]))

# ---- agac_orani olmadan sinif ne kadar is goruyor? ---------------------
print("\n" + "=" * 76)
print("KONTROL: agac_orani sutunlarini CIKARIP yerine sinifi koyarsak?")
print("-" * 76)
suz = [c for c in TEMEL if not c.startswith("agac_orani")]
a = kos(d, suz)
b = kos(d, suz + ["arazi_agaclik", "arazi_otlak", "arazi_diger"])
print("   agac_orani YOK, sinif YOK   : %+0.4f" % a["rho"])
print("   agac_orani YOK, sinif VAR   : %+0.4f   (%+0.4f)" %
      (b["rho"], b["rho"] - a["rho"]))
print("   agac_orani VAR, sinif YOK   : %+0.4f" % t["rho"])
