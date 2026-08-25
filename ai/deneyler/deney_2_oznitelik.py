# -*- coding: utf-8 -*-
"""2. asama: sorun veride mi modelde mi?

Asama 1'de 8 model ailesi 0,04 araligina sikisti ve dogrusal model
en iyiden sadece 0,03 geride kaldi. Bu, tavani MODELIN degil VERININ
belirledigi anlamina gelir. Oyleyse yeni oznitelik denemek gerekiyor.

Ham parcalar_250m dosyalarinda egitim setine hic girmemis sutunlar var.
Burada hepsini tek tek olcuyoruz.

Onemli kural: sadece YANGINDAN 2 HAFTA SONRA elde olabilecek seyler
aday olabilir. Gelecek bilgisi (2 yillik yagis gibi) sizintidir.
"""
import sys, glob, time, warnings
import numpy as np, pandas as pd
warnings.filterwarnings("ignore")
sys.stdout.reconfigure(encoding="utf-8")

from sklearn.ensemble import HistGradientBoostingRegressor
from sklearn.model_selection import LeaveOneGroupOut
from scipy.stats import spearmanr

TEMEL = ["agac_orani", "agac_orani_y", "dnbr",
         "egim_derece", "yukselti_m", "yol_mesafe_km"]
HEDEF, GRUP, TOHUM = "kalan_acik", "grup_id", 0


# ------------------------------------------------------------------ metrik
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
    return {"rho_ort": r.mean(), "rho_med": np.median(r),
            "poz": int((r > 0).sum()), "n_grup": len(r),
            "en_kotu": r.min(), "top20": np.nanmean(t), "rho_grup": r}


def kos(d, sutunlar):
    X, y = d[sutunlar], d[HEDEF].to_numpy()
    g = d[GRUP].to_numpy()
    oof = np.full(len(y), np.nan)
    for tr, te in LeaveOneGroupOut().split(X, y, g):
        m = HistGradientBoostingRegressor(random_state=TOHUM)
        m.fit(X.iloc[tr], y[tr])
        oof[te] = m.predict(X.iloc[te])
    return olc(y, oof, g), oof


# -------------------------------------------------------------------- veri
print("veri birlestiriliyor...")
d = pd.read_csv("egitim_seti.csv")

ham = pd.concat([pd.read_csv(f) for f in sorted(glob.glob("parcalar_250m/*.csv"))],
                ignore_index=True)
ham = ham.drop_duplicates(subset="hucre_id", keep="first").set_index("hucre_id")

EK = ["baki_derece", "su_mesafe_km", "yerlesim_mesafe_km",
      "ndvi_oncesi", "ndvi_sonrasi", "ndvi_dusus", "dndvi",
      "yagis_uzun_ort_mm", "yagis_sonrasi_2yil_mm", "yagis_anomali"]
for c in EK:
    d[c] = d["hucre_id"].map(ham[c])

# baki dairesel: 359 ile 1 derece komsu ama sayica cok uzak.
# sin/cos'a acmak dogrusu. kuzeylik = nem tutan yamac.
rad = np.radians(d["baki_derece"])
d["baki_kuzey"] = np.cos(rad)      # +1 kuzey, -1 guney
d["baki_dogu"] = np.sin(rad)

print("eslesen satir:")
for c in EK:
    print("   %-24s %5d / %d" % (c, d[c].notna().sum(), len(d)))

# grup ici degisim - metrigimiz grup ici oldugu icin
# yangin icinde sabit olan sutun HICBIR SEY katmaz
print("\ngrup ici degisim katsayisi (0 = yangin icinde sabit, ise yaramaz):")
for c in EK + ["baki_kuzey"]:
    if d[c].notna().sum() == 0:
        continue
    ic = d.groupby(GRUP)[c].std().mean()
    tum = d[c].std()
    print("   %-24s %.3f" % (c, ic / tum if tum else 0))

# -------------------------------------------------------------------- taban
print("\n" + "=" * 74)
print("TABAN (6 oznitelik, HistGB)")
t0 = time.time()
taban, _ = kos(d, TEMEL)
print("   rho_ort %+0.4f | med %+0.4f | poz %d/%d | en_kotu %+0.4f | top20 %.1f%%  (%.0f sn)"
      % (taban["rho_ort"], taban["rho_med"], taban["poz"], taban["n_grup"],
         taban["en_kotu"], 100 * taban["top20"], time.time() - t0))

# ------------------------------------------------------------ tek tek ekle
ADAY = [
    ("+ baki (sin/cos)",       ["baki_kuzey", "baki_dogu"], "mesru"),
    ("+ baki (ham derece)",    ["baki_derece"],             "mesru"),
    ("+ su mesafe",            ["su_mesafe_km"],            "mesru"),
    ("+ yerlesim mesafe",      ["yerlesim_mesafe_km"],      "mesru"),
    ("+ uzun donem yagis",     ["yagis_uzun_ort_mm"],       "mesru"),
    ("+ ndvi sonrasi",         ["ndvi_sonrasi"],            "mesru"),
    ("+ ndvi dusus",           ["ndvi_dusus"],              "mesru"),
    ("+ dndvi",                ["dndvi"],                   "mesru"),
    ("+ ndvi oncesi",          ["ndvi_oncesi"],             "SUPHELI"),
    ("+ 2 yillik yagis",       ["yagis_sonrasi_2yil_mm"],   "SIZINTI"),
    ("+ yagis anomali",        ["yagis_anomali"],           "SIZINTI"),
]

print("\n" + "=" * 74)
print("TEK TEK EKLEME  (fark = tabana gore)")
print("%-24s %8s %8s %7s %9s %8s  %s" %
      ("oznitelik", "rho_ort", "fark", "poz", "en_kotu", "top20", "durum"))
print("-" * 74)

sonuc = []
for ad, sut, durum in ADAY:
    if any(d[c].notna().sum() == 0 for c in sut):
        print("%-24s  --- veri yok ---" % ad)
        continue
    o, _ = kos(d, TEMEL + sut)
    fark = o["rho_ort"] - taban["rho_ort"]
    sonuc.append({"ad": ad, "sutun": sut, "durum": durum,
                  "rho_ort": o["rho_ort"], "fark": fark,
                  "poz": o["poz"], "en_kotu": o["en_kotu"], "top20": o["top20"]})
    print("%-24s %+8.4f %+8.4f %3d/%-3d %+9.4f %7.1f%%  %s" %
          (ad, o["rho_ort"], fark, o["poz"], o["n_grup"],
           o["en_kotu"], 100 * o["top20"], durum))

pd.DataFrame(sonuc).to_csv("deney_2_oznitelik.csv", index=False)
print("\n-> deney_2_oznitelik.csv")
