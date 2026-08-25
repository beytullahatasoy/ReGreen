# -*- coding: utf-8 -*-
"""rho +0,686 HANGI OLCEKTE gecerli? Karar hangi olcekte veriliyor?

Su ana kadar her seyi TEK HUCRE (250 m, 6,25 ha) duzeyinde olctuk.
Ama rehabilitasyon 6,25 hektarlik parseller halinde planlanmiyor -
bolme/bolmecik duzeyinde, onlarca/yuzlerce hektarlik bloklar halinde
planlaniyor.

Soru: hucreleri bloklar halinde toplarsak model daha mi guvenilir olur?

Istatistiksel beklenti: evet. Bagimsiz hatalar ortalama alinca sonuyor.
Ama ne kadar? Olcelim.
"""
import sys, warnings
import numpy as np, pandas as pd
warnings.filterwarnings("ignore")
sys.stdout.reconfigure(encoding="utf-8")
from scipy.stats import spearmanr

HEDEF, GRUP, YANGIN = "kalan_acik", "grup_id", "yangin_id"

t = pd.read_csv("sonuc_oof_tahminler.csv")
d = pd.read_csv("egitim_seti.csv")
t["lat"], t["lon"], t["dnbr"] = d["lat"], d["lon"], d["dnbr"]

DER = 0.00225           # ~250 m enlemde


def top_isabet(y, p, oran=0.20):
    n = len(y); k = max(1, int(round(n * oran)))
    if k >= n:
        return np.nan
    ger = set(np.argsort(-np.asarray(y))[:k])
    return sum(1 for i in np.argsort(-np.asarray(p))[:k] if i in ger) / k


def blok_olc(k):
    """k x k hucrelik bloklara topla, blok ortalamalari uzerinden olc."""
    r, t20, t20d, n_blok = [], [], [], []
    for gid, s in t.groupby(GRUP):
        s = s.copy()
        adim = DER * k
        s["bi"] = np.floor((s["lat"] - s["lat"].min()) / adim).astype(int)
        s["bj"] = np.floor((s["lon"] - s["lon"].min()) /
                           (adim / np.cos(np.radians(s["lat"].mean())))).astype(int)
        b = s.groupby([YANGIN, "bi", "bj"]).agg(
            y=(HEDEF, "mean"), p=("oof_tahmin", "mean"),
            db=("dnbr", "mean"), n=(HEDEF, "size")).reset_index()
        b = b[b["n"] >= max(1, k * k // 2)]        # yarisi dolu bloklar
        if len(b) < 8 or b["y"].nunique() < 3:
            continue
        v = spearmanr(b["y"], b["p"]).statistic
        if not np.isfinite(v):
            continue
        r.append(v)
        t20.append(top_isabet(b["y"].values, b["p"].values))
        t20d.append(top_isabet(b["y"].values, b["db"].values))
        n_blok.append(len(b))
    r = np.array(r)
    return {"rho": r.mean(), "med": np.median(r), "poz": int((r > 0).sum()),
            "grup": len(r), "kotu": r.min(), "top20": np.nanmean(t20),
            "top20_dnbr": np.nanmean(t20d), "ort_blok": np.mean(n_blok)}


print("=" * 88)
print("OLCEK ANALIZI - karar hangi olcekte veriliyor?")
print("=" * 88)
print("  Hucre = 250 x 250 m = 6,25 hektar\n")
print("  %-26s %7s %7s %6s %8s %8s %9s %7s" %
      ("olcek", "rho", "medyan", "poz", "en_kotu", "top20", "dNBR", "blok"))
print("  " + "-" * 84)

for k, ad in [(1, "1 hucre      6,25 ha"),
              (2, "2x2 blok      25 ha"),
              (3, "3x3 blok      56 ha"),
              (4, "4x4 blok     100 ha"),
              (6, "6x6 blok     225 ha"),
              (8, "8x8 blok     400 ha")]:
    o = blok_olc(k)
    print("  %-26s %+7.3f %+7.3f %3d/%-2d %+8.3f %7.1f%% %8.1f%% %7.0f" %
          (ad, o["rho"], o["med"], o["poz"], o["grup"], o["kotu"],
           100 * o["top20"], 100 * o["top20_dnbr"], o["ort_blok"]))

print("\n" + "=" * 88)
print("HUCRE DUZEYINDE BELIRSIZLIK")
print("=" * 88)
sat = []
for gid, s in t.groupby(GRUP):
    if len(s) < 40:
        continue
    y, p = s[HEDEF].to_numpy(), s["oof_tahmin"].to_numpy()
    n = len(s)
    gs = pd.Series(-y).rank(pct=True).to_numpy()
    ts = pd.Series(-p).rank(pct=True).to_numpy()
    hata = np.abs(gs - ts) * 100
    # gercekten en kotu %20'de olanlar, tahminde nereye dusuyor?
    kotu = gs <= 0.20
    sat.append({"grup": gid, "n": n,
                "ort_kayma": hata.mean(),
                "medyan_kayma": np.median(hata),
                "kotu20_tahmin_yuzdelik": (ts[kotu] * 100).mean(),
                "kotu20_kacan": float((ts[kotu] > 0.5).mean() * 100)})
h = pd.DataFrame(sat)
print("  Yuzdelik dilim cinsinden (0 = en kotu, 100 = en iyi)\n")
print("  ortalama sira kaymasi              : %.0f puan" % h["ort_kayma"].mean())
print("  medyan sira kaymasi                : %.0f puan" % h["medyan_kayma"].mean())
print("  gercekten en kotu %%20'dekiler")
print("     tahminde ortalama %%%.0f'lik dilimde" % h["kotu20_tahmin_yuzdelik"].mean())
print("     kac %%'i ust yariya dusuyor      : %%%.0f" % h["kotu20_kacan"].mean())

print("\n" + "=" * 88)
print("BLOK OLCEGINDE AYNI SORU (4x4 = 100 ha)")
print("=" * 88)
sat = []
for gid, s in t.groupby(GRUP):
    s = s.copy(); adim = DER * 4
    s["bi"] = np.floor((s["lat"] - s["lat"].min()) / adim).astype(int)
    s["bj"] = np.floor((s["lon"] - s["lon"].min()) /
                       (adim / np.cos(np.radians(s["lat"].mean())))).astype(int)
    b = s.groupby([YANGIN, "bi", "bj"]).agg(
        y=(HEDEF, "mean"), p=("oof_tahmin", "mean"), n=(HEDEF, "size")).reset_index()
    b = b[b["n"] >= 8]
    if len(b) < 12:
        continue
    gs = b["y"].rank(ascending=False, pct=True).to_numpy()
    ts = b["p"].rank(ascending=False, pct=True).to_numpy()
    kotu = gs <= 0.20
    if kotu.sum() == 0:
        continue
    sat.append({"ort_kayma": (np.abs(gs - ts) * 100).mean(),
                "kotu20_tahmin": (ts[kotu] * 100).mean(),
                "kotu20_kacan": float((ts[kotu] > 0.5).mean() * 100)})
hb = pd.DataFrame(sat)
print("  ortalama sira kaymasi              : %.0f puan  (hucrede %.0f)"
      % (hb["ort_kayma"].mean(), h["ort_kayma"].mean()))
print("  gercekten en kotu %%20'deki bloklar")
print("     tahminde ortalama %%%.0f'lik dilimde  (hucrede %%%.0f)"
      % (hb["kotu20_tahmin"].mean(), h["kotu20_tahmin_yuzdelik"].mean()))
print("     kac %%'i ust yariya dusuyor      : %%%.0f  (hucrede %%%.0f)"
      % (hb["kotu20_kacan"].mean(), h["kotu20_kacan"].mean()))
