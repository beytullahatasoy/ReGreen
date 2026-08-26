# -*- coding: utf-8 -*-
"""
Turkiye veri setini modele hazir hale getirir.

Yaptigi iki sey:
  1. etiket_tam bayragi  - 2 yillik gozlem penceresi bugun itibariyle
     tam kapanmis mi? Kapanmadiysa NDVI_yil2 eksik pencereden geliyor,
     etiket guvenilir degil.
  2. Kullanilabilirlik ozeti - hangi yangin egitime girebilir.

Calistirma: python veri_son_hal.py
"""
import sys
import pathlib
import json
from datetime import datetime, timedelta

import numpy as np
import pandas as pd

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent))
import yollar
yollar.yol_ekle()

BUGUN = datetime.now()
PENCERE_SONU = 765            # yangin + 765 gun = yil2 penceresinin sonu
MIN_ETIKETLI = 30             # bu kadar etiketli hucresi olmayan yangin,
                              # GroupKFold'da bir kat olusturamaz

yollar.gerekli(yollar.ARA / "turkiye_grid.csv")
df = pd.read_csv(yollar.ARA / "turkiye_grid.csv", low_memory=False)

# yeniden calistirilabilir olmasi icin onceki bayraklari at
df = df.drop(columns=[c for c in ("etiket_tam", "egitime_uygun")
                      if c in df.columns])

# --- etiket penceresi tam kapanmis mi ---------------------------------
t = pd.to_datetime(df["yangin_tarihi"])
df["pencere_bitis"] = t + pd.Timedelta(days=PENCERE_SONU)
df["etiket_tam"] = df["pencere_bitis"] <= BUGUN
df.drop(columns=["pencere_bitis"], inplace=True)

# --- yangin bazinda kullanilabilirlik ---------------------------------
ozet = df.groupby("yangin_id").agg(
    bolge=("bolge", "first"), yil=("yil", "first"),
    tarih=("yangin_tarihi", "first"),
    hucre=("hucre_id", "size"), yanik=("yanik", "sum"),
    etiketli=("etiket_gecerli", "sum"), tam=("etiket_tam", "first"),
    uyari=("kalite_uyari", "first")).reset_index()
ozet["uyari"] = ozet["uyari"].fillna("")
ozet["egitime_uygun"] = (ozet["etiketli"] >= MIN_ETIKETLI) & ozet["tam"] & \
                        (~ozet["uyari"].str.contains("alan uyusmuyor"))

df = df.merge(ozet[["yangin_id", "egitime_uygun"]], on="yangin_id", how="left")
df.to_csv(yollar.ARA / "turkiye_grid.csv", index=False, encoding="utf-8-sig")
ozet.to_csv(yollar.ARA / "yangin_ozeti.csv", index=False, encoding="utf-8-sig")

# --- rapor -------------------------------------------------------------
eg = df[df["etiket_gecerli"]]
uyg = df[df["etiket_gecerli"] & df["egitime_uygun"]]
print("=" * 70)
print("TURKIYE VERI SETI - SON HAL")
print("=" * 70)
print(f"  Kesfedilen yangin      : {ozet.shape[0]}")
print(f"  Toplam hucre           : {len(df):,}")
print(f"  Yanik hucre            : {int(df['yanik'].sum()):,}")
print(f"  Etiketli hucre         : {len(eg):,}")
print()
print(f"  EGITIME UYGUN yangin   : {int(ozet['egitime_uygun'].sum())}")
print(f"  EGITIME UYGUN hucre    : {len(uyg):,}")
print(f"  Yil araligi (uygun)    : {uyg['yil'].min()} - {uyg['yil'].max()}")

print("\n  Elenme sebepleri:")
e = ozet[~ozet["egitime_uygun"]]
print(f"    etiketli hucre < {MIN_ETIKETLI:<3}      : "
      f"{int((e['etiketli'] < MIN_ETIKETLI).sum())}")
print(f"    2 yil penceresi kapanmamis : {int((~e['tam']).sum())}")
print(f"    alan uyusmazligi           : "
      f"{int(e['uyari'].str.contains('alan uyusmuyor').sum())}")

print("\n  Egitime uygun yanginlar, bolgeye gore:")
for b, g in ozet[ozet["egitime_uygun"]].groupby("bolge"):
    print(f"    {b:<14}{len(g):>3} yangin, {int(g['etiketli'].sum()):>6,} hucre")

print("\n  Egitime uygun yanginlar, yila gore:")
for y, g in ozet[ozet["egitime_uygun"]].groupby("yil"):
    print(f"    {y}: {len(g):>2} yangin, {int(g['etiketli'].sum()):>6,} hucre")

print("\n  Yigilma kontrolu:")
s = uyg.groupby("yangin_id").size().sort_values(ascending=False)
print(f"    en buyuk yangin payi: %{s.iloc[0]/len(uyg)*100:.1f} ({s.index[0]})")
print(f"    ilk 3 yanginin payi : %{s.head(3).sum()/len(uyg)*100:.1f}")
print(f"    medyan hucre/yangin : {int(s.median())}")

print("\n  -> turkiye_grid.csv (etiket_tam, egitime_uygun eklendi)")
print("  -> yangin_ozeti.csv (yangin bazinda tablo)")
