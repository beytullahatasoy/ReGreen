# -*- coding: utf-8 -*-
"""TAVAN NEREDE? 0,8-0,9 mumkun mu, yoksa fizik mi engelliyor?

Simdiki durum: rho +0,686 (LOGO, gorulmemis yangin).
Peki tavan ne? Uc ayri ust sinir olcuyoruz:

  1) AYNI YANGIN ICI TAVAN
     Yangini rastgele bolup ayni yangin uzerinde egit-test yap.
     Genelleme yok, sadece "bu iliskiler ogrenilebilir mi".
     Bu MEKANSAL SIZINTILI bir olcum - kasten. Ust sinir veriyor.

  2) KAHIN OZNITELIKLER (gelecegi bilsek)
     Yangindan sonraki 2 yilin yagisini ve 1. yil NDVI'sini
     ekleyelim. Gercekte bilemeyiz ama "bilseydik ne olurdu"
     sorusu tahmin edilebilirligin sinirini gosterir.

  3) TAM EZBER (in-sample)
     Test grubunu egitime de koy. Mutlak ust sinir.

Aradaki farklar bize nerede kayip verdigimizi soyler:
    3 - 1  =  ezber payi
    1 - simdi = GENELLEME acigi   (daha cok yangin / daha iyi oznitelik kapatir)
    kahin - simdi = BILINEMEZ olanin payi (hava, kuraklik...)

Ayrica yeni bir oznitelik deneniyor:
    YANMAMIS ALANA MESAFE - tohum kaynagi yakinligi.
"""
import pathlib
import sys, glob, time, warnings
import numpy as np, pandas as pd
warnings.filterwarnings("ignore")
sys.stdout.reconfigure(encoding="utf-8")

from scipy.stats import spearmanr
from scipy.spatial import cKDTree
from sklearn.model_selection import LeaveOneGroupOut, KFold
from sklearn.linear_model import Ridge
from sklearn.ensemble import HistGradientBoostingRegressor
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


def RIDGE():
    return make_pipeline(SimpleImputer(strategy="median"),
                         StandardScaler(), Ridge(alpha=1.0))


def HGB():
    return HistGradientBoostingRegressor(random_state=TOHUM)


d = pd.read_csv(yollar.ARA / "egitim_seti.csv").reset_index(drop=True)
y = d[HEDEF].to_numpy()
g = d[GRUP].to_numpy()

# --------------------------------------------- kahin + tohum oznitelikleri
bilgi("veri hazirlaniyor...")
ham = pd.concat([pd.read_csv(f) for f in sorted(glob.glob("parcalar_250m/*.csv"))],
                ignore_index=True)

for c in ["ndvi_yil1", "yagis_sonrasi_2yil_mm", "yagis_anomali"]:
    d[c] = d["hucre_id"].map(ham.drop_duplicates("hucre_id").set_index("hucre_id")[c])

# YANMAMIS ALANA MESAFE - tohum kaynagi
# Her yangin icin: yanmamis hucrelerin agacini kur, yanik hucrelerin
# en yakin yanmamis komsuya mesafesini olc.
t0 = time.time()
mesafe = pd.Series(np.nan, index=d.index)
derinlik = pd.Series(np.nan, index=d.index)
for fid, s in ham.groupby("yangin_id"):
    yanik = s["yanik"].fillna(False).astype(bool)
    temiz = s[~yanik]
    hedef_s = s[yanik]
    if len(temiz) < 3 or len(hedef_s) == 0:
        continue
    # dereceyi kabaca kilometreye cevir (enlem ~111 km, boylam cos ile)
    lat0 = np.radians(s["lat"].mean())
    tx = np.c_[temiz["lat"] * 111.0, temiz["lon"] * 111.0 * np.cos(lat0)]
    hx = np.c_[hedef_s["lat"] * 111.0, hedef_s["lon"] * 111.0 * np.cos(lat0)]
    uz, _ = cKDTree(tx).query(hx, k=1)
    eslesme = pd.Series(uz, index=hedef_s["hucre_id"].values)
    m = d["hucre_id"].map(eslesme)
    mesafe = mesafe.fillna(m)
d["tohum_mesafe_km"] = mesafe
bilgi("  tohum mesafesi: %d/%d hucre, %.0f sn" %
      (d["tohum_mesafe_km"].notna().sum(), len(d), time.time() - t0))
bilgi("  dagilim: medyan %.2f km, %%90 %.2f km, maks %.2f km" %
      (d["tohum_mesafe_km"].median(), d["tohum_mesafe_km"].quantile(.9),
       d["tohum_mesafe_km"].max()))

X = d[OZ]
KAHIN = OZ + ["ndvi_yil1", "yagis_sonrasi_2yil_mm", "yagis_anomali"]
TOHUM_OZ = OZ + ["tohum_mesafe_km"]


def logo(kur, Xi, hedef=None):
    hedef = y if hedef is None else hedef
    oof = np.full(len(y), np.nan)
    for tr, te in LeaveOneGroupOut().split(Xi, hedef, g):
        m = kur(); m.fit(Xi.iloc[tr], hedef[tr]); oof[te] = m.predict(Xi.iloc[te])
    return oof


def ayni_yangin(kur, Xi):
    """Her yanginin kendi icinde 5 katli bolme. Mekansal sizinti VAR - kasten."""
    oof = np.full(len(y), np.nan)
    for _, s in d.groupby(YANGIN):
        idx = s.index.to_numpy()
        if len(idx) < 25:
            continue
        for a, b in KFold(5, shuffle=True, random_state=TOHUM).split(idx):
            m = kur(); m.fit(Xi.loc[idx[a]], y[idx[a]])
            oof[idx[b]] = m.predict(Xi.loc[idx[b]])
    return oof


def ezber(kur, Xi):
    m = kur(); m.fit(Xi, y)
    return m.predict(Xi)


bilgi("\n" + "=" * 86)
bilgi("TAVAN ANALIZI")
bilgi("=" * 86)
bilgi("%-44s %8s %8s %8s %8s" % ("olcum", "rho", "medyan", "en_kotu", "top20"))
bilgi("-" * 86)

sonuc = []


def ekle(ad, p, not_=""):
    o = olc(y, p, g); o["ad"] = ad; sonuc.append(o)
    bilgi("%-44s %+8.4f %+8.4f %+8.4f %7.1f%% %s" %
          (ad, o["rho"], o["med"], o["kotu"], 100 * o["top20"], not_))


bilgi("\n--- SIMDIKI DURUM " + "-" * 66)
ekle("Ridge, gorulmemis yangin (LOGO)", logo(RIDGE, X))
ekle("HistGB, gorulmemis yangin (LOGO)", logo(HGB, X))

bilgi("\n--- 1) AYNI YANGIN ICI TAVAN (mekansal sizinti VAR) " + "-" * 32)
ekle("Ridge,  ayni yangin icinde", ayni_yangin(RIDGE, X), "<- ust sinir")
ekle("HistGB, ayni yangin icinde", ayni_yangin(HGB, X), "<- ust sinir")

bilgi("\n--- 2) KAHIN: gelecegi bilseydik " + "-" * 51)
ekle("Ridge  + gelecek yagis + 1.yil NDVI", logo(RIDGE, d[KAHIN]))
ekle("HistGB + gelecek yagis + 1.yil NDVI", logo(HGB, d[KAHIN]))

bilgi("\n--- 3) TAM EZBER (in-sample, mutlak ust sinir) " + "-" * 37)
ekle("HistGB ezber", ezber(HGB, X))

bilgi("\n--- 4) YENI OZNITELIK: yanmamis alana mesafe " + "-" * 39)
ekle("Ridge  + tohum_mesafe_km", logo(RIDGE, d[TOHUM_OZ]))
ekle("HistGB + tohum_mesafe_km", logo(HGB, d[TOHUM_OZ]))

r = spearmanr(d[HEDEF], d["tohum_mesafe_km"], nan_policy="omit")
ic = [spearmanr(s[HEDEF], s["tohum_mesafe_km"], nan_policy="omit").statistic
      for _, s in d.groupby(GRUP) if s["tohum_mesafe_km"].notna().sum() > 10]
bilgi("\n  tohum_mesafe_km ile hedef: genel rho %+0.3f | grup ici ortalama %+0.3f"
      % (r.statistic, np.nanmean(ic)))
bilgi("  grup ici isaret tutarliligi: %d/%d pozitif"
      % (int(np.sum(np.array(ic) > 0)), len(ic)))

# ------------------------------------------------------------------ ozet
s = pd.DataFrame(sonuc).set_index("ad")
simdi = s.loc["Ridge, gorulmemis yangin (LOGO)", "rho"]
ayni = s.loc["Ridge,  ayni yangin icinde", "rho"]
kahin = s.loc["HistGB + gelecek yagis + 1.yil NDVI", "rho"]
ezb = s.loc["HistGB ezber", "rho"]

bilgi("\n" + "=" * 86)
bilgi("KAYIP NEREDE?")
bilgi("=" * 86)
bilgi("  simdiki durum (gorulmemis yangin)     %+0.4f" % simdi)
bilgi("  ayni yangin ici tavan                 %+0.4f   fark %+0.4f  <- GENELLEME acigi"
      % (ayni, ayni - simdi))
bilgi("  gelecegi bilseydik                    %+0.4f   fark %+0.4f  <- BILINEMEZ olan"
      % (kahin, kahin - simdi))
bilgi("  tam ezber (in-sample)                 %+0.4f" % ezb)
bilgi("")
if ayni < 0.80:
    bilgi("  YORUM: ayni yangin icinde bile %.2f'in ustune cikilamiyor." % ayni)
    bilgi("         Genelleme sorunu degil - bu ozniteliklerle olculebilecek")
    bilgi("         iliskinin siniri bu. 0,9 icin YENI BILGI kaynagi gerekir.")
else:
    bilgi("  YORUM: ayni yangin icinde %.2f cikiyor ama gorulmemis yanginda %.2f."
          % (ayni, simdi))
    bilgi("         Aradaki fark GENELLEME acigi -> daha cok yangin ve daha")
    bilgi("         genellenebilir oznitelikler bu bosluğu kapatabilir.")

s.to_csv(yollar.CIKTI / "sonuc_tavan.csv")
bilgi("\n-> sonuc_tavan.csv")
