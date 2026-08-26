# -*- coding: utf-8 -*-
"""
Egitim setinin saglik kontrolu
==============================

Temizlikten sonra veri setinin gercekten duzeldigini bagimsiz olarak
dogrular. Model KURMAZ; sadece tanisal bir taban model calistirip
verinin ogrenilebilir olup olmadigini ve sizintinin kapanip kapanmadigini
olcer.

Calistirma:  python veri_dogrula.py
"""

import sys
import json
import pathlib

import numpy as np
import pandas as pd
from scipy import stats
from sklearn.ensemble import HistGradientBoostingRegressor
from sklearn.model_selection import GroupKFold, KFold
from sklearn.metrics import r2_score, mean_absolute_error

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent))
import yollar
yollar.yol_ekle()

KOK = yollar.ARA          # ortak veri koku - ai/yollar.py
TOHUM = 0


def capraz(X, y, gruplar, katlar=5):
    """Grup bazli capraz dogrulama tahminleri."""
    pr = np.zeros(len(y))
    for tr, te in GroupKFold(n_splits=katlar).split(X, y, gruplar):
        m = HistGradientBoostingRegressor(max_iter=300, random_state=TOHUM)
        m.fit(X[tr], y[tr])
        pr[te] = m.predict(X[te])
    return pr


def grup_ici_spearman(pr, y, gruplar, en_az=20):
    """Projenin asil hedefi: her yangin icinde dogru siralama."""
    s = []
    for k in np.unique(gruplar):
        m = gruplar == k
        if m.sum() >= en_az:
            r = stats.spearmanr(pr[m], y[m]).statistic
            if np.isfinite(r):
                s.append(r)
    return np.array(s)


def main():
    d = pd.read_csv(KOK / "egitim_seti.csv", encoding="utf-8-sig")
    meta = json.loads((KOK / "egitim_seti_meta.json").read_text(encoding="utf-8"))
    OZ = meta["oznitelikler"]
    H = meta["hedef"]

    print("=" * 78)
    print("EGITIM SETI SAGLIK KONTROLU")
    print("=" * 78)
    print("  {:,} satir | {} oznitelik | {} grup | {} yangin".format(
        len(d), len(OZ), d["grup_id"].nunique(), d["yangin_id"].nunique()))

    # ------------------------------------------------------------ butunluk
    print("\n" + "-" * 78)
    print("1. BUTUNLUK")
    print("-" * 78)
    sorun = []
    if (d["arazi_agaclik"].dropna() == 0).all():
        sorun.append("arazi_agaclik hep 0")
    kod0 = 0
    if "arazi_kodu_y" in d.columns:
        kod0 = int((d["arazi_kodu_y"] == 0).sum())
    print("  nodata artigi (kod 0)     : {}".format(kod0))
    print("  tam yinelenen satir       : {}".format(int(d.duplicated().sum())))
    print("  ayni yer iki grupta       : ", end="")
    snap = ((d["lat"] / 0.00225).round().astype(int).astype(str) + "_"
            + (d["lon"] / 0.00281).round().astype(int).astype(str))
    cakisan = d.assign(s=snap).groupby("s")["grup_id"].nunique()
    print(int((cakisan > 1).sum()))
    print("  hedefte NaN               : {}".format(int(d[H].isna().sum())))
    SIZ = list(meta["sizinti"])
    kacak = [k for k in SIZ if k in d.columns]
    print("  sizinti sutunu            : {}".format(kacak if kacak else "yok"))
    print("  -> {}".format("TEMIZ" if not sorun and not kacak and kod0 == 0
                           and (cakisan > 1).sum() == 0 else "KONTROL ET"))

    X = d[OZ].astype(float).values
    y = d[H].values
    grp = d["grup_id"].values
    yng = d["yangin_id"].values

    # -------------------------------------------------------- sizinti testi
    print("\n" + "-" * 78)
    print("2. SIZINTI TESTI - bolme sekli sonuca ne yapiyor")
    print("-" * 78)
    pr_g = capraz(X, y, grp)
    pr_y = capraz(X, y, yng)
    pr_r = np.zeros(len(y))
    for tr, te in KFold(5, shuffle=True, random_state=TOHUM).split(X):
        pr_r[te] = HistGradientBoostingRegressor(
            max_iter=300, random_state=TOHUM).fit(X[tr], y[tr]).predict(X[te])

    print("  {:<38}{:>8}{:>9}".format("bolme", "R2", "MAE"))
    for ad, pr in [("grup_id (DOGRU - mekansal grup)", pr_g),
                   ("yangin_id (eski, sizintili)", pr_y),
                   ("rastgele KFold (tamamen sizintili)", pr_r)]:
        print("  {:<38}{:>+8.3f}{:>9.4f}".format(
            ad, r2_score(y, pr), mean_absolute_error(y, pr)))
    print("  {:<38}{:>+8.3f}{:>9.4f}".format(
        "sadece kuresel ortalama (taban)", r2_score(y, np.full(len(y), y.mean())),
        mean_absolute_error(y, np.full(len(y), y.mean()))))

    # ------------------------------------------------------ birincil metrik
    print("\n" + "-" * 78)
    print("3. BIRINCIL METRIK - grup ici siralama")
    print("-" * 78)
    sp = grup_ici_spearman(pr_g, y, grp)
    print("  ortalama Spearman : {:+.3f}".format(np.mean(sp)))
    print("  medyan            : {:+.3f}".format(np.median(sp)))
    print("  pozitif olan      : {}/{}".format((sp > 0).sum(), len(sp)))
    print("  en dusuk 3        : {}".format(
        ", ".join("{:+.2f}".format(v) for v in np.sort(sp)[:3])))

    # -------------------------------------------------------------- ablasyon
    print("\n" + "-" * 78)
    print("4. ABLASYON - her oznitelik ne katiyor")
    print("-" * 78)
    tam_r2 = r2_score(y, pr_g)
    tam_sp = np.mean(sp)
    print("  {:<22}{:>10}{:>12}".format("cikarilan", "R2", "grup ici rho"))
    print("  {:<22}{:>+10.3f}{:>+12.3f}   <- tam model".format(
        "(yok)", tam_r2, tam_sp))
    satirlar = []
    for k in OZ:
        kalan = [c for c in OZ if c != k]
        p = capraz(d[kalan].astype(float).values, y, grp)
        r2 = r2_score(y, p)
        s2 = np.mean(grup_ici_spearman(p, y, grp))
        satirlar.append((k, r2 - tam_r2, s2 - tam_sp))
        print("  {:<22}{:>+10.3f}{:>+12.3f}".format(k, r2, s2))
    print("\n  (deger DUSTUYSE oznitelik faydali, YUKSELDIYSE gurultu)")

    # --------------------------------------- elenen adaylari geri test etme
    print("\n" + "-" * 78)
    print("5. ELENEN ADAYLARI GERI KOYSAK NE OLUR")
    print("-" * 78)
    ham = pd.read_csv(KOK / "turkiye_grid.csv", low_memory=False)
    anahtar = ["yangin_id", "hucre_id"]
    for aday in ["yerlesim_mesafe_km", "su_mesafe_km", "baki_derece"]:
        if aday not in ham.columns:
            continue
        e = d.merge(ham[anahtar + [aday]], on=anahtar, how="left")
        if e[aday].isna().all():
            continue
        Xe = e[OZ + [aday]].astype(float).values
        p = capraz(Xe, y, grp)
        r2 = r2_score(y, p)
        s2 = np.mean(grup_ici_spearman(p, y, grp))
        print("  +{:<21}R2 {:+.3f} ({:+.3f})   rho {:+.3f} ({:+.3f})".format(
            aday, r2, r2 - tam_r2, s2, s2 - tam_sp))
    print("\n  (parantez ici tam modele gore fark; ~0 ise katkisi yok)")

    print("\n" + "=" * 78)
    print("OZET")
    print("=" * 78)
    print("  Egitim seti kullanilabilir durumda.")
    print("  Raporlanacak birincil metrik: grup ici Spearman = {:+.3f}".format(tam_sp))
    print("  GroupKFold anahtari: grup_id  (yangin_id KULLANMA)")


if __name__ == "__main__":
    main()
