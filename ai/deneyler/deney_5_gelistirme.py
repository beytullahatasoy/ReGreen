# -*- coding: utf-8 -*-
"""Sonuclari yukseltme denemeleri.

Ana fikir: metrigimiz YANGIN ICI siralama, ama model MUTLAK degerler
goruyor. Manavgat'ta 30 derece dik bir yamac o yangin icinde ortalama
olabilir; kucuk bir yanginda ayni 30 derece en dik yer olabilir. Model
bunu ayirt edemiyor.

Denenecekler:
  B  yangin ici z-skor sutunlari EKLE
  C  ozellikleri yangin ici z-skora CEVIR
  D  komsu hucre ortalamalari ekle (mekansal baglam)
  E  B + D
  F  HEDEFI yangin ici z-skora cevir
  G  F + B
  H  Ridge + HistGB toplulugu

Sizinti kontrolu: yangin ici istatistikler tahmin aninda hesaplanabilir,
cunku bir yangini butun olarak isliyoruz. Komsu ortalamalari da oyle.
Hicbiri gelecek bilgisi degil.
"""
import pathlib
import sys, time, warnings
import numpy as np, pandas as pd
warnings.filterwarnings("ignore")
sys.stdout.reconfigure(encoding="utf-8")

from scipy.stats import spearmanr
from scipy.spatial import cKDTree
from sklearn.model_selection import LeaveOneGroupOut
from sklearn.ensemble import HistGradientBoostingRegressor
from sklearn.linear_model import Ridge
from sklearn.pipeline import make_pipeline
from sklearn.preprocessing import StandardScaler
from sklearn.impute import SimpleImputer

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent))
import yollar
yollar.yol_ekle()

OZ = ["agac_orani", "agac_orani_y", "dnbr", "egim_derece",
      "yukselti_m", "ndvi_dusus", "yol_mesafe_km"]
HEDEF, GRUP, YANGIN, TOHUM = "kalan_acik", "grup_id", "yangin_id", 0


def bilgi(*a):
    print(*a, flush=True)


# ---------------------------------------------------------------- metrik
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


# ------------------------------------------------------------- ozellikler
def yangin_ici_z(d, sutunlar):
    """Her ozelligi kendi yangininin ortalamasina gore z-skora cevirir."""
    out = {}
    for c in sutunlar:
        g = d.groupby(YANGIN)[c]
        out[c + "_z"] = ((d[c] - g.transform("mean")) /
                         g.transform("std").replace(0, np.nan))
    return pd.DataFrame(out, index=d.index)


def komsu_ort(d, sutunlar, k=8):
    """Her hucrenin kendi yangini icindeki en yakin k komsusunun ortalamasi.

    Mekansal baglam: 'bu hucre cevresine gore nasil' bilgisi.
    """
    out = pd.DataFrame(index=d.index, columns=[c + "_komsu" for c in sutunlar],
                       dtype=float)
    for _, s in d.groupby(YANGIN):
        xy = np.c_[s["lat"].values, s["lon"].values]
        kk = min(k + 1, len(s))
        if kk < 2:
            continue
        _, idx = cKDTree(xy).query(xy, k=kk)
        idx = idx[:, 1:] if idx.ndim > 1 else idx.reshape(-1, 1)
        for c in sutunlar:
            v = s[c].to_numpy()
            out.loc[s.index, c + "_komsu"] = np.nanmean(v[idx], axis=1)
    return out


def hedef_z(d):
    """Hedefi yangin ici z-skora cevirir.

    Urun icin sorun degil: oncelik skoru zaten recovery_gap_pred'i
    yangin icinde normalize ediyor. Siralama ayni kaliyor.
    """
    g = d.groupby(YANGIN)[HEDEF]
    return ((d[HEDEF] - g.transform("mean")) /
            g.transform("std").replace(0, np.nan)).fillna(0)


# --------------------------------------------------------------- modeller
def RIDGE():
    return make_pipeline(SimpleImputer(strategy="median"),
                         StandardScaler(), Ridge(alpha=1.0))


def HGB():
    return HistGradientBoostingRegressor(random_state=TOHUM)


def logo(kur, X, y, g, y_gercek=None):
    oof = np.full(len(y), np.nan)
    for tr, te in LeaveOneGroupOut().split(X, y, g):
        m = kur(); m.fit(X.iloc[tr], y[tr]); oof[te] = m.predict(X.iloc[te])
    return oof


# ------------------------------------------------------------------ veri
d = pd.read_csv(yollar.ARA / "egitim_seti.csv")
y = d[HEDEF].to_numpy()
g = d[GRUP].to_numpy()

bilgi("=" * 84)
bilgi("SONUCLARI YUKSELTME DENEMELERI")
bilgi("=" * 84)
bilgi("  %d satir | %d grup | %d yangin" %
      (len(d), d[GRUP].nunique(), d[YANGIN].nunique()))

t = time.time()
Z = yangin_ici_z(d, OZ)
K = komsu_ort(d, ["dnbr", "ndvi_dusus", "egim_derece", "agac_orani_y"])
YZ = hedef_z(d).to_numpy()
bilgi("  ozellik uretimi: %.0f sn" % (time.time() - t))

TAM = pd.concat([d[OZ], Z, K], axis=1)

VARYANT = [
    ("A  taban (7 oznitelik)",          OZ,                                y),
    ("B  + yangin ici z",               OZ + list(Z.columns),              y),
    ("C  sadece yangin ici z",          list(Z.columns),                   y),
    ("D  + komsu ortalamalari",         OZ + list(K.columns),              y),
    ("E  + z + komsu",                  OZ + list(Z.columns) + list(K.columns), y),
    ("F  hedef z-skor",                 OZ,                                YZ),
    ("G  hedef z + yangin ici z",       OZ + list(Z.columns),              YZ),
    ("H  hedef z + z + komsu",          OZ + list(Z.columns) + list(K.columns), YZ),
]

bilgi("\n%-30s %8s %8s %6s %8s %8s" %
      ("varyant", "rho", "medyan", "poz", "en_kotu", "top20"))

sonuc, saklanan = [], {}
for model_ad, kur in [("RIDGE", RIDGE), ("HistGB", HGB)]:
    bilgi("\n--- %s %s" % (model_ad, "-" * (74 - len(model_ad))))
    taban_rho = None
    for ad, sut, hedef in VARYANT:
        t = time.time()
        oof = logo(kur, TAM[sut], hedef, g)
        o = olc(y, oof, g)            # DAIMA gercek y'ye karsi olculuyor
        if taban_rho is None:
            taban_rho = o["rho"]
        fark = o["rho"] - taban_rho
        sonuc.append({"model": model_ad, "varyant": ad, **o,
                      "fark": fark, "sn": round(time.time() - t, 1)})
        saklanan[(model_ad, ad)] = oof
        bilgi("%-30s %+8.4f %+8.4f %3d/%-2d %+8.4f %7.1f%%  %+0.4f" %
              (ad, o["rho"], o["med"], o["poz"], o["n"], o["kotu"],
               100 * o["top20"], fark))

# ------------------------------------------------------------- topluluk
bilgi("\n--- TOPLULUK " + "-" * 70)
tab = pd.DataFrame(sonuc)


def sirala_ici(p, g):
    """Tahminleri grup icinde siraya cevirir - farkli olcekleri birlestirmek icin."""
    return pd.Series(p).groupby(pd.Series(g)).rank(pct=True).to_numpy()


en_r = tab[tab.model == "RIDGE"].sort_values("rho").iloc[-1]["varyant"]
en_h = tab[tab.model == "HistGB"].sort_values("rho").iloc[-1]["varyant"]
pr = saklanan[("RIDGE", en_r)]
ph = saklanan[("HistGB", en_h)]
bilgi("  en iyi RIDGE  : %s" % en_r)
bilgi("  en iyi HistGB : %s" % en_h)

for w in [0.3, 0.5, 0.7]:
    karisim = w * sirala_ici(pr, g) + (1 - w) * sirala_ici(ph, g)
    o = olc(y, karisim, g)
    sonuc.append({"model": "TOPLULUK", "varyant": "ridge %.0f%% + hgb %.0f%%"
                  % (100 * w, 100 - 100 * w), **o, "fark": np.nan, "sn": 0})
    bilgi("%-30s %+8.4f %+8.4f %3d/%-2d %+8.4f %7.1f%%" %
          ("ridge %.0f%% + hgb %.0f%%" % (100 * w, 100 - 100 * w),
           o["rho"], o["med"], o["poz"], o["n"], o["kotu"], 100 * o["top20"]))

son = pd.DataFrame(sonuc).sort_values("rho", ascending=False)
son.to_csv(yollar.CIKTI / "sonuc_deney5.csv", index=False)

bilgi("\n" + "=" * 84)
bilgi("EN IYI 8")
bilgi("=" * 84)
bilgi(son.head(8)[["model", "varyant", "rho", "med", "poz", "kotu", "top20"]]
      .to_string(index=False))
bilgi("\n-> sonuc_deney5.csv")
