# -*- coding: utf-8 -*-
"""Topluluk kazanci gercek mi, secim yanliligi mi?

Deney 5'te 16 varyanti denedim, en iyisini SECTIM, sonra topluluk
agirligini da ayni LOGO skoruna bakarak sectim. Yani olctugum sinavin
cevaplarina bakarak secim yaptim. Rapor edilen +0,704 bu yuzden
sisirilmis olabilir.

Burada secimin KENDISINI dis katin icine aliyoruz:

  DIS kat (LOGO, 27)  -> sadece RAPOR eder
     ic kat (GroupKFold 4) -> 8 ozellik varyanti x 2 model kosulur
                              320 kombinasyon (varyant x varyant x agirlik)
                              icinden EN IYISI SECILIR
     secilen yapiyla 26 grupla egitilir
     disaridaki gruba tahmin verilir

Disaridaki grup ne varyant seciminde ne agirlik seciminde ne de egitimde
gorulur. Cikan sayi durusttur.

Ayrica karsilastirma icin:
  - HIC secim yapmayan sabit taban (Ridge A)
  - Deney 5'in yanli sayisi (ayni kod, secim disarida)
"""
import sys, time, itertools, warnings
import numpy as np, pandas as pd
warnings.filterwarnings("ignore")
sys.stdout.reconfigure(encoding="utf-8")

from scipy.stats import spearmanr
from scipy.spatial import cKDTree
from sklearn.model_selection import LeaveOneGroupOut, GroupKFold
from sklearn.ensemble import HistGradientBoostingRegressor
from sklearn.linear_model import Ridge
from sklearn.pipeline import make_pipeline
from sklearn.preprocessing import StandardScaler
from sklearn.impute import SimpleImputer

OZ = ["agac_orani", "agac_orani_y", "dnbr", "egim_derece",
      "yukselti_m", "ndvi_dusus", "yol_mesafe_km"]
HEDEF, GRUP, YANGIN, TOHUM = "kalan_acik", "grup_id", "yangin_id", 0
AGIRLIK = [0.0, 0.25, 0.5, 0.75, 1.0]      # 0 = sadece HistGB, 1 = sadece Ridge


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


def rho_tek(y, p, g):
    df = pd.DataFrame({"y": y, "p": p, "g": g}); r = []
    for _, s in df.groupby("g"):
        if len(s) < 8 or s["y"].nunique() < 3:
            continue
        v = spearmanr(s["y"], s["p"]).statistic
        if np.isfinite(v):
            r.append(v)
    return float(np.mean(r)) if r else -1.0


def grup_ici_sira(p, g):
    return pd.Series(p).groupby(pd.Series(g)).rank(pct=True).to_numpy()


# ------------------------------------------------------------ ozellikler
d = pd.read_csv("egitim_seti.csv")
y = d[HEDEF].to_numpy()
g = d[GRUP].to_numpy()

Z = pd.DataFrame({c + "_z": ((d[c] - d.groupby(YANGIN)[c].transform("mean")) /
                             d.groupby(YANGIN)[c].transform("std").replace(0, np.nan))
                  for c in OZ}, index=d.index)

KOM = ["dnbr", "ndvi_dusus", "egim_derece", "agac_orani_y"]
K = pd.DataFrame(index=d.index, columns=[c + "_komsu" for c in KOM], dtype=float)
for _, s in d.groupby(YANGIN):
    xy = np.c_[s["lat"].values, s["lon"].values]
    kk = min(9, len(s))
    if kk < 2:
        continue
    _, idx = cKDTree(xy).query(xy, k=kk)
    idx = idx[:, 1:] if idx.ndim > 1 else idx.reshape(-1, 1)
    for c in KOM:
        v = s[c].to_numpy()
        K.loc[s.index, c + "_komsu"] = np.nanmean(v[idx], axis=1)

gz = d.groupby(YANGIN)[HEDEF]
YZ = ((d[HEDEF] - gz.transform("mean")) /
      gz.transform("std").replace(0, np.nan)).fillna(0).to_numpy()

TAM = pd.concat([d[OZ], Z, K], axis=1)

VARYANT = {
    "A": (OZ, "ham"),
    "B": (OZ + list(Z.columns), "ham"),
    "C": (list(Z.columns), "ham"),
    "D": (OZ + list(K.columns), "ham"),
    "E": (OZ + list(Z.columns) + list(K.columns), "ham"),
    "F": (OZ, "z"),
    "G": (OZ + list(Z.columns), "z"),
    "H": (OZ + list(Z.columns) + list(K.columns), "z"),
}


def RIDGE():
    return make_pipeline(SimpleImputer(strategy="median"),
                         StandardScaler(), Ridge(alpha=1.0))


def HGB():
    return HistGradientBoostingRegressor(random_state=TOHUM)


MODEL = {"R": RIDGE, "H": HGB}


def egit_tahmin(model_ad, varyant, tr, te):
    sut, hed = VARYANT[varyant]
    hedef = y if hed == "ham" else YZ
    m = MODEL[model_ad]()
    m.fit(TAM[sut].iloc[tr], hedef[tr])
    return m.predict(TAM[sut].iloc[te])


bilgi("=" * 82)
bilgi("IC ICE DOGRULAMA - secim yanliligini olcuyoruz")
bilgi("=" * 82)
bilgi("  %d satir | %d grup | %d varyant x 2 model x %d agirlik" %
      (len(d), d[GRUP].nunique(), len(VARYANT), len(AGIRLIK)))
bilgi("  kombinasyon sayisi: %d" % (len(VARYANT) ** 2 * len(AGIRLIK)))

# --------------------------------------------------------- 1) SABIT TABAN
bilgi("\n[1/3] SABIT TABAN - hicbir secim yok, Ridge varyant A")
oof_sabit = np.full(len(y), np.nan)
for tr, te in LeaveOneGroupOut().split(TAM, y, g):
    oof_sabit[te] = egit_tahmin("R", "A", tr, te)
o_sabit = olc(y, oof_sabit, g)
bilgi("      rho %+0.4f | poz %d/%d | en_kotu %+0.4f | top20 %.1f%%" %
      (o_sabit["rho"], o_sabit["poz"], o_sabit["n"], o_sabit["kotu"],
       100 * o_sabit["top20"]))

# ------------------------------------------------------ 2) YANLI (deney 5)
bilgi("\n[2/3] YANLI OLCUM - secim disarida yapilmis hali (deney 5'teki gibi)")
tum_oof = {}
for mad in MODEL:
    for v in VARYANT:
        p = np.full(len(y), np.nan)
        for tr, te in LeaveOneGroupOut().split(TAM, y, g):
            p[te] = egit_tahmin(mad, v, tr, te)
        tum_oof[(mad, v)] = p

en_iyi_yanli, en_rho = None, -np.inf
for vr, vh, w in itertools.product(VARYANT, VARYANT, AGIRLIK):
    karisim = (w * grup_ici_sira(tum_oof[("R", vr)], g) +
               (1 - w) * grup_ici_sira(tum_oof[("H", vh)], g))
    r = rho_tek(y, karisim, g)
    if r > en_rho:
        en_iyi_yanli, en_rho = (vr, vh, w), r
vr, vh, w = en_iyi_yanli
karisim = (w * grup_ici_sira(tum_oof[("R", vr)], g) +
           (1 - w) * grup_ici_sira(tum_oof[("H", vh)], g))
o_yanli = olc(y, karisim, g)
bilgi("      secilen: Ridge=%s  HistGB=%s  agirlik=%.2f" % (vr, vh, w))
bilgi("      rho %+0.4f | poz %d/%d | en_kotu %+0.4f | top20 %.1f%%" %
      (o_yanli["rho"], o_yanli["poz"], o_yanli["n"], o_yanli["kotu"],
       100 * o_yanli["top20"]))

# ------------------------------------------------------- 3) DURUST OLCUM
bilgi("\n[3/3] DURUST OLCUM - secim her katin ICINDE yapiliyor")
t0 = time.time()
oof_durust = np.full(len(y), np.nan)
secimler = []

for i, (tr, te) in enumerate(LeaveOneGroupOut().split(TAM, y, g), 1):
    g_tr = g[tr]
    ic_kat = min(4, len(np.unique(g_tr)))
    # ic OOF: sadece egitim gruplari uzerinde
    ic = {}
    for mad in MODEL:
        for v in VARYANT:
            p = np.full(len(tr), np.nan)
            for a, b in GroupKFold(n_splits=ic_kat).split(TAM.iloc[tr], y[tr], g_tr):
                p[b] = egit_tahmin(mad, v, tr[a], tr[b])
            ic[(mad, v)] = p
    # ic skora gore en iyi kombinasyonu sec
    en, en_r = None, -np.inf
    for a_v, b_v, ww in itertools.product(VARYANT, VARYANT, AGIRLIK):
        km = (ww * grup_ici_sira(ic[("R", a_v)], g_tr) +
              (1 - ww) * grup_ici_sira(ic[("H", b_v)], g_tr))
        r = rho_tek(y[tr], km, g_tr)
        if r > en_r:
            en, en_r = (a_v, b_v, ww), r
    a_v, b_v, ww = en
    # secilen yapiyi 26 grupla egit, disaridakine uygula
    pr = egit_tahmin("R", a_v, tr, te)
    ph = egit_tahmin("H", b_v, tr, te)
    g_te = g[te]
    oof_durust[te] = (ww * grup_ici_sira(pr, g_te) +
                      (1 - ww) * grup_ici_sira(ph, g_te))
    secimler.append({"grup": g_te[0], "ridge_v": a_v, "hgb_v": b_v,
                     "agirlik": ww, "ic_rho": round(en_r, 4)})
    bilgi("      %2d/27  %-14s  R=%s H=%s w=%.2f  ic_rho %+0.4f  (%.1f dk)" %
          (i, g_te[0][:14], a_v, b_v, ww, en_r, (time.time() - t0) / 60))

o_durust = olc(y, oof_durust, g)
sec = pd.DataFrame(secimler)
sec.to_csv("sonuc_dogrulama_secimler.csv", index=False)

# ------------------------------------------------------------------ rapor
bilgi("\n" + "=" * 82)
bilgi("KARSILASTIRMA")
bilgi("=" * 82)
bilgi("%-34s %8s %7s %9s %8s" % ("", "rho", "poz", "en_kotu", "top20"))
bilgi("-" * 82)
for ad, o in [("1) sabit taban (secim yok)", o_sabit),
              ("2) YANLI olcum (deney 5)", o_yanli),
              ("3) DURUST olcum (ic ice secim)", o_durust)]:
    bilgi("%-34s %+8.4f %3d/%-3d %+9.4f %7.1f%%" %
          (ad, o["rho"], o["poz"], o["n"], o["kotu"], 100 * o["top20"]))
bilgi("-" * 82)
bilgi("  secim yanliligi (2 - 3) : %+0.4f rho, %+0.1f puan top20" %
      (o_yanli["rho"] - o_durust["rho"],
       100 * (o_yanli["top20"] - o_durust["top20"])))
bilgi("  GERCEK kazanc  (3 - 1)  : %+0.4f rho, %+0.1f puan top20" %
      (o_durust["rho"] - o_sabit["rho"],
       100 * (o_durust["top20"] - o_sabit["top20"])))

bilgi("\n27 KATTA SECILENLER (kararli mi?)")
bilgi("  Ridge varyanti : " + ", ".join("%s=%d" % (k, v) for k, v in
                                        sec.ridge_v.value_counts().items()))
bilgi("  HistGB varyanti: " + ", ".join("%s=%d" % (k, v) for k, v in
                                        sec.hgb_v.value_counts().items()))
bilgi("  agirlik        : " + ", ".join("%.2f=%d" % (k, v) for k, v in
                                        sec.agirlik.value_counts().items()))

pd.DataFrame([{"olcum": a, **o} for a, o in
              [("sabit", o_sabit), ("yanli", o_yanli), ("durust", o_durust)]]) \
    .to_csv("sonuc_dogrulama.csv", index=False)
np.save("sonuc_oof_durust.npy", oof_durust)
bilgi("\n  sure %.1f dk -> sonuc_dogrulama.csv, sonuc_dogrulama_secimler.csv"
      % ((time.time() - t0) / 60))
