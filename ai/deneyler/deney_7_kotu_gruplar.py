# -*- coding: utf-8 -*-
"""Kotu gruplar neden kotu?

En kotu ucu: MAR_2023_01 (rho 0.14), AKD_2021_06 (0.24), AKD_2023_02 (0.41)
En iyiler  : EGE_2019_03, EGE_2021_08, AKD_2021_05 (~0.88)

Uc hipotez test ediliyor:
  H1  Hedef cesitliligi dusuk - siralanacak bir sey yok
  H2  Ozellikler egitim dagiliminin disinda - model ekstrapole ediyor
  H3  Bolge egitimde temsil edilmiyor (Marmara'da tek grup var,
      disari cikinca egitimde hic Marmara kalmiyor)
"""
import pathlib
import sys, warnings
import numpy as np, pandas as pd
warnings.filterwarnings("ignore")
sys.stdout.reconfigure(encoding="utf-8")

from scipy.stats import spearmanr

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent))
import yollar
yollar.yol_ekle()

OZ = ["agac_orani", "agac_orani_y", "dnbr", "egim_derece",
      "yukselti_m", "ndvi_dusus", "yol_mesafe_km"]
HEDEF, GRUP = "kalan_acik", "grup_id"

d = pd.read_csv(yollar.ARA / "egitim_seti.csv")
skor = pd.read_csv(yollar.CIKTI / "sonuc_grup_skorlari.csv").set_index("grup")
d["rho_grup"] = d[GRUP].map(skor["rho"])

ozet = (d.groupby(GRUP)
        .agg(n=(HEDEF, "size"), rho=("rho_grup", "first"),
             bolge=("bolge", "first"), yil=("yil", "first"),
             hedef_std=(HEDEF, "std"), hedef_ort=(HEDEF, "mean"),
             hedef_aralik=(HEDEF, lambda s: s.quantile(.9) - s.quantile(.1)),
             dnbr_std=("dnbr", "std"), egim_std=("egim_derece", "std"),
             ndvi_dusus_std=("ndvi_dusus", "std"),
             agac_ort=("agac_orani_y", "mean"))
        .sort_values("rho"))

print("=" * 96)
print("H1 - HEDEF CESITLILIGI: siralanacak bir sey var mi?")
print("=" * 96)
print(ozet[["n", "rho", "bolge", "hedef_std", "hedef_aralik",
            "ndvi_dusus_std", "dnbr_std"]].round(4).to_string())

r = spearmanr(ozet["rho"], ozet["hedef_std"])
print("\n  rho ile hedef_std korelasyonu     : %+0.3f  (p=%.3f)"
      % (r.statistic, r.pvalue))
r2 = spearmanr(ozet["rho"], ozet["hedef_aralik"])
print("  rho ile hedef_aralik korelasyonu  : %+0.3f  (p=%.3f)"
      % (r2.statistic, r2.pvalue))
r3 = spearmanr(ozet["rho"], ozet["ndvi_dusus_std"])
print("  rho ile ndvi_dusus_std korelasyonu: %+0.3f  (p=%.3f)"
      % (r3.statistic, r3.pvalue))
r4 = spearmanr(ozet["rho"], ozet["n"])
print("  rho ile grup boyutu korelasyonu   : %+0.3f  (p=%.3f)"
      % (r4.statistic, r4.pvalue))

# ---------------------------------------------------------------- H2
print("\n" + "=" * 96)
print("H2 - DAGILIM DISI MI? her grup, kalan 26 grubun araligina gore")
print("=" * 96)
print("%-14s %6s %8s   %s" % ("grup", "rho", "disarida", "en cok tasan ozellik"))
print("-" * 96)
sat = []
for gid, s in d.groupby(GRUP):
    kalan = d[d[GRUP] != gid]
    pay, en_kotu_oz, en_kotu_pay = [], None, 0
    for c in OZ:
        a, b = kalan[c].quantile(.01), kalan[c].quantile(.99)
        p = ((s[c] < a) | (s[c] > b)).mean()
        pay.append(p)
        if p > en_kotu_pay:
            en_kotu_pay, en_kotu_oz = p, c
    sat.append({"grup": gid, "rho": ozet.loc[gid, "rho"],
                "disarida": float(np.mean(pay)),
                "oz": en_kotu_oz, "oz_pay": en_kotu_pay})
h2 = pd.DataFrame(sat).sort_values("rho")
for _, x in h2.iterrows():
    print("%-14s %+6.3f %7.1f%%   %s (%.0f%%)" %
          (x["grup"], x["rho"], 100 * x["disarida"], x["oz"], 100 * x["oz_pay"]))
r5 = spearmanr(h2["rho"], h2["disarida"])
print("\n  rho ile dagilim disilik korelasyonu: %+0.3f  (p=%.3f)"
      % (r5.statistic, r5.pvalue))

# ---------------------------------------------------------------- H3
print("\n" + "=" * 96)
print("H3 - BOLGE TEMSILI: grup disari cikinca egitimde ayni bolgeden kac grup kaliyor?")
print("=" * 96)
bol = d.groupby("bolge")[GRUP].nunique()
print("  bolgedeki grup sayisi:")
for k, v in bol.items():
    print("     %-16s %d" % (k, v))
print()
h3 = ozet.reset_index()[["grup_id", "rho", "bolge", "n"]].copy()
h3["ayni_bolge_kalan"] = h3["bolge"].map(bol) - 1
print("%-14s %6s %-16s %s" % ("grup", "rho", "bolge", "egitimde kalan ayni bolge"))
print("-" * 96)
for _, x in h3.iterrows():
    isaret = "  <-- YALNIZ" if x["ayni_bolge_kalan"] == 0 else ""
    print("%-14s %+6.3f %-16s %d%s" %
          (x["grup_id"], x["rho"], x["bolge"], x["ayni_bolge_kalan"], isaret))
yalniz = h3[h3.ayni_bolge_kalan == 0]
digeri = h3[h3.ayni_bolge_kalan > 0]
print("\n  bolgesinde YALNIZ olanlar (%d grup): rho ortalama %+0.3f"
      % (len(yalniz), yalniz["rho"].mean()))
print("  destegi olanlar        (%d grup): rho ortalama %+0.3f"
      % (len(digeri), digeri["rho"].mean()))

# --------------------------------------------------- en kotu 3 yakin plan
print("\n" + "=" * 96)
print("EN KOTU 3 GRUBUN YAKIN PLANI")
print("=" * 96)
for gid in ozet.index[:3]:
    s = d[d[GRUP] == gid]
    print("\n--- %s   rho %+0.3f   %d hucre   %s   %d"
          % (gid, ozet.loc[gid, "rho"], len(s), s["bolge"].iloc[0],
             s["yil"].iloc[0]))
    print("    %-16s %8s %8s | %8s %8s  (tum veri)"
          % ("ozellik", "ortalama", "std", "ortalama", "std"))
    for c in OZ + [HEDEF]:
        print("    %-16s %8.3f %8.3f | %8.3f %8.3f"
              % (c, s[c].mean(), s[c].std(), d[c].mean(), d[c].std()))
    print("    grup ici ham korelasyonlar (hedef ile):")
    for c in OZ:
        v = spearmanr(s[HEDEF], s[c]).statistic
        tum = spearmanr(d[HEDEF], d[c]).statistic
        isaret = "  TERS!" if np.sign(v) != np.sign(tum) and abs(v) > .1 else ""
        print("       %-16s %+0.3f   (tum veride %+0.3f)%s" % (c, v, tum, isaret))

ozet.to_csv(yollar.CIKTI / "sonuc_grup_tanilama.csv")
print("\n-> sonuc_grup_tanilama.csv")
