# -*- coding: utf-8 -*-
"""Iki soru:

A) "Kopya ceken model neden 1.00 vermiyor?"
   Deney 10'daki 'tam ezber' testi VARSAYILAN HistGB ile yapilmisti.
   Varsayilan HistGB duzenlilestirilmis (max_iter=100, max_leaf_nodes=31),
   yani ezberlemeye calismiyor. Gercek ezber testi: kapasitesi sinirsiz
   modeller. Burada duzeltiyoruz.

B) "En kucuk kac hektarlik yangini alabiliyoruz, neden altina inemiyoruz?"
   Esigi dusurmenin bedelini sayilarla gosteriyoruz.
"""
import sys, json, glob, warnings
import numpy as np, pandas as pd
warnings.filterwarnings("ignore")
sys.stdout.reconfigure(encoding="utf-8")

from scipy.stats import spearmanr
from sklearn.ensemble import RandomForestRegressor, HistGradientBoostingRegressor
from sklearn.neighbors import KNeighborsRegressor
from sklearn.tree import DecisionTreeRegressor
from sklearn.linear_model import Ridge
from sklearn.pipeline import make_pipeline
from sklearn.preprocessing import StandardScaler
from sklearn.impute import SimpleImputer

OZ = ["agac_orani", "agac_orani_y", "dnbr", "egim_derece",
      "yukselti_m", "ndvi_dusus", "yol_mesafe_km"]
HEDEF, GRUP, TOHUM = "kalan_acik", "grup_id", 0


def olc(y, p, g):
    r = []
    df = pd.DataFrame({"y": y, "p": p, "g": g})
    for _, s in df.groupby("g"):
        if len(s) < 8 or s["y"].nunique() < 3:
            continue
        v = spearmanr(s["y"], s["p"]).statistic
        if np.isfinite(v):
            r.append(v)
    return float(np.mean(r))


d = pd.read_csv("egitim_seti.csv")
y, g = d[HEDEF].to_numpy(), d[GRUP].to_numpy()
X = d[OZ]
Xd = X.copy()
Xd["agac_orani_y"] = Xd["agac_orani_y"].fillna(Xd["agac_orani"])
Xd = Xd.fillna(Xd.median())

print("=" * 80)
print("A) EZBER TESTI - kapasiteyi acinca ne oluyor?")
print("=" * 80)
print("   Ayni veriyle egit, AYNI veride test et (in-sample).")
print("   Model kapasitesi yeterse 1.00'a cikmali.\n")
print("   %-46s %8s" % ("model", "grup ici rho"))
print("   " + "-" * 56)

for ad, kur, veri in [
    ("HistGB varsayilan (deney 10'da kullanilan)",
     lambda: HistGradientBoostingRegressor(random_state=TOHUM), X),
    ("HistGB, kapasite acik (2000 iter, 255 yaprak)",
     lambda: HistGradientBoostingRegressor(
         random_state=TOHUM, max_iter=2000, max_leaf_nodes=255,
         min_samples_leaf=1, l2_regularization=0.0, early_stopping=False), X),
    ("RandomForest, min_samples_leaf=1",
     lambda: RandomForestRegressor(n_estimators=300, min_samples_leaf=1,
                                   random_state=TOHUM, n_jobs=-1), Xd),
    ("Tek karar agaci, sinirsiz derinlik",
     lambda: DecisionTreeRegressor(random_state=TOHUM), Xd),
    ("1-en yakin komsu (saf ezber)",
     lambda: KNeighborsRegressor(n_neighbors=1), Xd),
    ("Ridge (dogrusal - ezberleyemez)",
     lambda: make_pipeline(SimpleImputer(strategy="median"),
                           StandardScaler(), Ridge(alpha=1.0)), X),
]:
    m = kur(); m.fit(veri, y)
    print("   %-46s %+8.4f" % (ad, olc(y, m.predict(veri), g)))

print("\n   ANLAM: kapasite acilinca 1.00'a cikiyor -> 'ezber tavani' diye")
print("   bir sey YOK. Ezber bilgi olcmez, sadece kapasite olcer.")
print("   Anlamli tavan, deney 10'daki AYNI YANGIN ICI BOLME testi:")
print("   ayni yanginin %80'iyle egit, gormedigi %20'sini tahmin et.")

# ---------------------------------------------------------------- B) esik
print("\n" + "=" * 80)
print("B) YANGIN BUYUKLUGU ESIGI")
print("=" * 80)

yang = json.loads(open("yanginlar.json", encoding="utf-8").read())
ya = pd.DataFrame(yang)[["id", "alan_ha", "tarih"]]

ham = pd.concat([pd.read_csv(f)[["yangin_id", "hucre_id", "yanik",
                                 "etiket_gecerli"]]
                 for f in sorted(glob.glob("parcalar_250m/*.csv"))],
                ignore_index=True).drop_duplicates("hucre_id")
oz = ham.groupby("yangin_id").agg(
    yanik_hucre=("yanik", lambda s: int(s.fillna(False).sum())),
    etiketli=("etiket_gecerli", lambda s: int(s.fillna(False).sum())))
ya = ya.merge(oz, left_on="id", right_index=True, how="left").sort_values("alan_ha")

print("\n  HUCRE MATEMATIGI (250 m hucre = 6,25 hektar)")
for ha in [3000, 1000, 500, 300, 200, 100, 50, 10]:
    print("     %6d ha  ->  %5.0f hucre" % (ha, ha / 6.25))

print("\n  MODIS COZUNURLUGU (yangin kesfi buradan)")
print("     1 MODIS pikseli = 500 x 500 m = 25 hektar")
for ha in [300, 100, 50, 25, 10, 4]:
    print("     %4d ha yangin  ->  %5.1f piksel" % (ha, ha / 25))

print("\n  EN KUCUK 12 YANGINIMIZ")
print("  %-14s %9s %12s %10s" % ("yangin", "alan_ha", "yanik hucre", "etiketli"))
print("  " + "-" * 50)
for _, r in ya.head(12).iterrows():
    print("  %-14s %9.0f %12.0f %10.0f" %
          (r["id"], r["alan_ha"], r["yanik_hucre"], r["etiketli"]))

egitimde = set(d["yangin_id"].unique())
ya["egitimde"] = ya["id"].isin(egitimde)
print("\n  ESIK DUSURULSE NE OLURDU?")
print("  Elimizdeki en kucuk yangin: %.0f ha (%s)"
      % (ya["alan_ha"].min(), ya.iloc[0]["id"]))
print("  Egitim setine giren en kucuk: %.0f ha"
      % ya[ya.egitimde]["alan_ha"].min())
kucuk = ya[ya["alan_ha"] < 500]
print("\n  500 ha altindaki %d yangin:" % len(kucuk))
print("     ortalama yanik hucre : %.0f" % kucuk["yanik_hucre"].mean())
print("     ortalama etiketli    : %.0f" % kucuk["etiketli"].mean())
print("     egitime giren        : %d/%d" % (kucuk["egitimde"].sum(), len(kucuk)))

print("\n  ETIKETLI HUCRE / TOPLAM ALAN ILISKISI")
b = pd.cut(ya["alan_ha"], [0, 500, 1000, 5000, 1e9],
           labels=["300-500", "500-1.000", "1.000-5.000", "5.000+"])
print(ya.groupby(b).agg(yangin=("id", "size"),
                        ort_etiketli=("etiketli", "mean"),
                        egitime_giren=("egitimde", "sum")).to_string())

# grup ici rho ile grup boyutu iliskisi
sk = pd.read_csv("sonuc_grup_skorlari.csv")
print("\n  KUCUK GRUPLARDA MODEL NASIL?")
for alt, ust in [(0, 60), (60, 150), (150, 500), (500, 1e9)]:
    s = sk[(sk["n"] >= alt) & (sk["n"] < ust)]
    if len(s):
        print("     %5d-%-5s hucre : %2d grup, rho ortalama %+0.3f" %
              (alt, "%d" % ust if ust < 1e8 else "ust", len(s), s["rho"].mean()))
