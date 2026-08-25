# -*- coding: utf-8 -*-
"""3. asama: ndvi_dusus ve ndvi_oncesi mesru mu, yoksa hile mi?

HEDEF  = ndvi_oncesi - ndvi_yil2
ADAY-1 = ndvi_dusus  = ndvi_oncesi - ndvi_sonrasi
ADAY-2 = ndvi_oncesi

Ikisi de hedefle ORTAK TERIM tasiyor (ndvi_oncesi). Bu kendi basina
sizinti degil - tahmin aninda (yangindan 2 hafta sonra) ikisi de
elimizde, ndvi_yil2 degil. Ama iki soruyu cevaplamak sart:

  1) Model gercekten katki mi veriyor, yoksa oznitelik tek basina
     ayni siralamayi mi veriyor? (o zaman modele gerek yok)
  2) Kazanc mekanik mi? "Once cok bitki vardi, o yuzden acik buyuk"
     demek yeni bilgi degil.

Sinama: ham oznitelikleri MODELSIZ siralayici olarak kullanip
modelle karsilastiriyoruz.
"""
import sys, glob, warnings
import numpy as np, pandas as pd
warnings.filterwarnings("ignore")
sys.stdout.reconfigure(encoding="utf-8")

from sklearn.ensemble import HistGradientBoostingRegressor
from sklearn.model_selection import LeaveOneGroupOut
from scipy.stats import spearmanr

TEMEL = ["agac_orani", "agac_orani_y", "dnbr",
         "egim_derece", "yukselti_m", "yol_mesafe_km"]
HEDEF, GRUP, TOHUM = "kalan_acik", "grup_id", 0


def top_isabet(y, p, oran=0.20):
    n = len(y); k = max(1, int(round(n * oran)))
    if k >= n:
        return np.nan
    gercek = set(np.argsort(-np.asarray(y))[:k])
    return sum(1 for i in np.argsort(-np.asarray(p))[:k] if i in gercek) / k


def olc(y, p, g):
    df = pd.DataFrame({"y": y, "p": p, "g": g})
    r, t = [], []
    for _, s in df.groupby("g"):
        if len(s) < 8 or s["y"].nunique() < 3:
            continue
        v = spearmanr(s["y"], s["p"]).statistic
        if np.isfinite(v):
            r.append(v); t.append(top_isabet(s["y"].values, s["p"].values))
    r = np.array(r)
    return {"rho_ort": r.mean(), "poz": int((r > 0).sum()), "n": len(r),
            "en_kotu": r.min(), "top20": np.nanmean(t)}


def kos(d, sutunlar):
    X, y, g = d[sutunlar], d[HEDEF].to_numpy(), d[GRUP].to_numpy()
    oof = np.full(len(y), np.nan)
    for tr, te in LeaveOneGroupOut().split(X, y, g):
        m = HistGradientBoostingRegressor(random_state=TOHUM)
        m.fit(X.iloc[tr], y[tr]); oof[te] = m.predict(X.iloc[te])
    return olc(y, oof, g)


def yaz(ad, o, taban=None):
    f = "" if taban is None else "  (%+0.4f)" % (o["rho_ort"] - taban)
    print("%-34s %+8.4f %3d/%-3d %+9.4f %7.1f%%%s" %
          (ad, o["rho_ort"], o["poz"], o["n"], o["en_kotu"], 100 * o["top20"], f))


# -------------------------------------------------------------------- veri
d = pd.read_csv("egitim_seti.csv")
ham = pd.concat([pd.read_csv(f) for f in sorted(glob.glob("parcalar_250m/*.csv"))],
                ignore_index=True).drop_duplicates("hucre_id").set_index("hucre_id")
for c in ["ndvi_oncesi", "ndvi_sonrasi", "ndvi_dusus"]:
    d[c] = d["hucre_id"].map(ham[c])

y, g = d[HEDEF].to_numpy(), d[GRUP].to_numpy()

print("=" * 82)
print("A) MODELSIZ SIRALAYICILAR - ham sutunu dogrudan skor gibi kullan")
print("%-34s %8s %7s %9s %8s" % ("", "rho_ort", "poz", "en_kotu", "top20"))
print("-" * 82)
yaz("dnbr (saha tabani)",        olc(y, d["dnbr"].values, g))
yaz("ndvi_oncesi tek basina",    olc(y, d["ndvi_oncesi"].values, g))
yaz("ndvi_dusus tek basina",     olc(y, d["ndvi_dusus"].values, g))
yaz("ndvi_sonrasi tek basina",   olc(y, -d["ndvi_sonrasi"].values, g))

print("\n" + "=" * 82)
print("B) MODELLI")
print("-" * 82)
t = kos(d, TEMEL); yaz("6 oznitelik (mevcut taban)", t)
tb = t["rho_ort"]
yaz("6 + ndvi_dusus",            kos(d, TEMEL + ["ndvi_dusus"]), tb)
yaz("6 + ndvi_oncesi",           kos(d, TEMEL + ["ndvi_oncesi"]), tb)
yaz("6 + ndvi_dusus + ndvi_oncesi",
    kos(d, TEMEL + ["ndvi_dusus", "ndvi_oncesi"]), tb)
yaz("6 + ndvi_oncesi + ndvi_sonrasi",
    kos(d, TEMEL + ["ndvi_oncesi", "ndvi_sonrasi"]), tb)

print("\n" + "=" * 82)
print("C) MODEL GERCEKTEN KATKI VERIYOR MU?")
print("   ham sutunun tek basina skoru ile modelin skorunu karsilastir")
print("-" * 82)
ham_dusus = olc(y, d["ndvi_dusus"].values, g)["rho_ort"]
mod_dusus = kos(d, TEMEL + ["ndvi_dusus"])["rho_ort"]
ham_once = olc(y, d["ndvi_oncesi"].values, g)["rho_ort"]
mod_once = kos(d, TEMEL + ["ndvi_oncesi"])["rho_ort"]
print("   ndvi_dusus : ham %+0.4f -> modelli %+0.4f   katki %+0.4f" %
      (ham_dusus, mod_dusus, mod_dusus - ham_dusus))
print("   ndvi_oncesi: ham %+0.4f -> modelli %+0.4f   katki %+0.4f" %
      (ham_once, mod_once, mod_once - ham_once))

print("\n" + "=" * 82)
print("D) MEKANIK MI? hedef ile adayin ham korelasyonu (grup ici)")
print("-" * 82)
for c in ["ndvi_oncesi", "ndvi_dusus", "ndvi_sonrasi", "dnbr"]:
    r = [spearmanr(s[HEDEF], s[c]).statistic
         for _, s in d.groupby(GRUP) if len(s) >= 8]
    print("   %-14s grup ici rho ortalama %+0.4f" % (c, np.nanmean(r)))
