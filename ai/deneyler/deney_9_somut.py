# -*- coding: utf-8 -*-
"""rho +0,686 pratikte ne demek? Anlasilir metriklere ceviriyoruz.

Spearman soyut bir sayi. Karar vericiye "0,686" demek bir sey anlatmaz.
Ayni tahminleri su sorulara cevap olacak sekilde olcuyoruz:

  1) Iki hucreyi karsilastirinca hangisinin daha kotu oldugunu
     yuzde kac dogru biliyoruz?
  2) Butcemiz alanin %20'sine yetiyor. Gercekten en kotu %20'nin
     ne kadarini yakaliyoruz?
  3) O %20'yi secince toplam hasarin yuzde kacini kapsiyoruz?
  4) Gercekte en kotu olan hucreyi kacinci sirada tahmin ediyoruz?
"""
import sys, warnings
import numpy as np, pandas as pd
warnings.filterwarnings("ignore")
sys.stdout.reconfigure(encoding="utf-8")
rng = np.random.default_rng(0)

HEDEF, GRUP = "kalan_acik", "grup_id"

t = pd.read_csv("sonuc_oof_tahminler.csv")
d = pd.read_csv("egitim_seti.csv")
t["dnbr"] = d["dnbr"]
y_ad, p_ad = HEDEF, "oof_tahmin"

print("=" * 84)
print("rho +0,686 PRATIKTE NE DEMEK?")
print("=" * 84)
print("  %d hucre, %d mekansal grup uzerinden" % (len(t), t[GRUP].nunique()))
print("  Butun tahminler out-of-fold: model o yangini hic gormemisti.\n")


# ----------------------------------------------- 1) ikili karsilastirma
def ikili_dogruluk(y, p, n=200000):
    """Ayni yangindan rastgele iki hucre: hangisi daha kotu, dogru bildi mi?"""
    m = len(y)
    if m < 2:
        return np.nan
    i = rng.integers(0, m, n)
    j = rng.integers(0, m, n)
    ge = y[i] - y[j]
    ta = p[i] - p[j]
    v = ge != 0
    return float(np.mean(np.sign(ge[v]) == np.sign(ta[v])))


sat = []
for gid, s in t.groupby(GRUP):
    y, p, db = s[y_ad].to_numpy(), s[p_ad].to_numpy(), s["dnbr"].to_numpy()
    sat.append({"grup": gid, "n": len(s),
                "model": ikili_dogruluk(y, p, 40000),
                "dnbr": ikili_dogruluk(y, db, 40000)})
ik = pd.DataFrame(sat)
w = ik["n"] / ik["n"].sum()

print("1) IKILI KARSILASTIRMA")
print("   'Ayni yangindan iki hucre verdim - hangisi daha kotu?'\n")
print("   rastgele tahmin (yazi-tura) : %5.1f%%" % 50.0)
print("   dNBR (saha yontemi)         : %5.1f%%" % (100 * (ik["dnbr"] * w).sum()))
print("   ReGreen modeli              : %5.1f%%" % (100 * (ik["model"] * w).sum()))
print("   mukemmel bilgi              : %5.1f%%" % 100.0)


# --------------------------------------------------- 2) butce senaryosu
def kapsam(y, p, oran):
    n = len(y); k = max(1, int(round(n * oran)))
    ger = set(np.argsort(-y)[:k])
    sec = np.argsort(-p)[:k]
    return sum(1 for i in sec if i in ger) / k


def hasar_payi(y, p, oran):
    """Secilen %X'in, toplam iyilesme aciginin yuzde kacini kapsadigi."""
    n = len(y); k = max(1, int(round(n * oran)))
    sec = np.argsort(-p)[:k]
    return y[sec].sum() / y.sum()


print("\n\n2) BUTCE SENARYOSU")
print("   'Butce alanin %X'ine yetiyor. En kotu %X'i ne kadar yakaliyoruz?'\n")
print("   %-8s %10s %10s %10s %10s" %
      ("butce", "rastgele", "dNBR", "ReGreen", "mukemmel"))
print("   " + "-" * 52)
for oran in [0.10, 0.20, 0.30, 0.50]:
    mo = da = 0.0
    for gid, s in t.groupby(GRUP):
        y, p, db = s[y_ad].to_numpy(), s[p_ad].to_numpy(), s["dnbr"].to_numpy()
        ww = len(s) / len(t)
        mo += ww * kapsam(y, p, oran)
        da += ww * kapsam(y, db, oran)
    print("   %-8s %9.0f%% %9.0f%% %9.0f%% %9.0f%%" %
          ("%%%d" % (100 * oran), 100 * oran, 100 * da, 100 * mo, 100))

print("\n\n3) HASAR KAPSAMA")
print("   'Sectigimiz alan, toplam iyilesme aciginin yuzde kacini iceriyor?'\n")
print("   %-8s %10s %10s %10s %10s" %
      ("butce", "rastgele", "dNBR", "ReGreen", "mukemmel"))
print("   " + "-" * 52)
for oran in [0.10, 0.20, 0.30, 0.50]:
    mo = da = mu = 0.0
    for gid, s in t.groupby(GRUP):
        y, p, db = s[y_ad].to_numpy(), s[p_ad].to_numpy(), s["dnbr"].to_numpy()
        ww = len(s) / len(t)
        mo += ww * hasar_payi(y, p, oran)
        da += ww * hasar_payi(y, db, oran)
        mu += ww * hasar_payi(y, y, oran)
    print("   %-8s %9.0f%% %9.0f%% %9.0f%% %9.0f%%" %
          ("%%%d" % (100 * oran), 100 * oran, 100 * da, 100 * mo, 100 * mu))


# ------------------------------------------------------- 4) sira hatasi
print("\n\n4) SIRA HATASI")
print("   'Gercekte en kotu hucreyi kacinci sirada tahmin ediyoruz?'\n")
sat = []
for gid, s in t.groupby(GRUP):
    if len(s) < 20:
        continue
    y, p = s[y_ad].to_numpy(), s[p_ad].to_numpy()
    ger_s = pd.Series(-y).rank().to_numpy()          # 1 = en kotu
    tah_s = pd.Series(-p).rank().to_numpy()
    n = len(s)
    en_kotu = np.argmin(ger_s)
    sat.append({"n": n,
                "en_kotu_tahmin_yuzdelik": 100 * tah_s[en_kotu] / n,
                "ort_sira_hatasi_yuzde": 100 * np.mean(np.abs(ger_s - tah_s)) / n})
sh = pd.DataFrame(sat)
print("   gercekte EN KOTU hucre, tahmin siralamasinda ortalama")
print("      en ustteki %%%.0f'lik dilimde cikiyor" % sh["en_kotu_tahmin_yuzdelik"].mean())
print("   ortalama sira kaymasi: butun listenin %%%.0f'i kadar"
      % sh["ort_sira_hatasi_yuzde"].mean())


# --------------------------------------------- 5) somut yangin ornegi
print("\n\n5) SOMUT ORNEK - AKD_2021_05 (Antalya, 442 hucre = 2.762 hektar)")
print("   " + "-" * 70)
s = t[t[GRUP] == "AKD_2021_05"].copy()
y, p = s[y_ad].to_numpy(), s["oof_tahmin"].to_numpy()
s["gercek_sira"] = pd.Series(-y).rank().values
s["tahmin_sira"] = pd.Series(-p).rank().values
n = len(s)
k = int(0.2 * n)
sec = set(np.argsort(-p)[:k])
ger = set(np.argsort(-y)[:k])
ortak = len(sec & ger)

print("   Butce: 88 hucre (550 hektar, alanin %%20'si)")
print()
print("   Model bu 88 hucreyi sectiginde:")
print("      gercekten en kotu 88'den %d tanesi icinde   (%%%.0f isabet)"
      % (ortak, 100 * ortak / k))
print("      dNBR ile secilseydi          : %d tane"
      % len(set(np.argsort(-s['dnbr'].to_numpy())[:k]) & ger))
print("      rastgele secilseydi          : ~%d tane" % int(0.2 * k))
print()
kacan = ger - sec
if kacan:
    kacan_y = y[list(kacan)]
    icinde_y = y[list(sec & ger)]
    print("   Kacirdigimiz %d hucrenin ortalama acigi : %.3f" % (len(kacan), kacan_y.mean()))
    print("   Yakaladigimiz %d hucrenin ortalamasi    : %.3f" % (ortak, icinde_y.mean()))
    print("   -> kacirdiklarimiz sinirdakiler, en kotuler degil")
print()
print("   Secilen 88 hucre toplam iyilesme aciginin %%%.0f'ini kapsiyor"
      % (100 * y[np.argsort(-p)[:k]].sum() / y.sum()))
print("   (rastgele secim %20 kapsardi)")
