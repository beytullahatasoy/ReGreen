# -*- coding: utf-8 -*-
"""Push oncesi kontrol: Beytullah'in dogrulayicisi bu veriyi kabul eder mi?

Kurallar birebir backend/ImportTool/Validation/FireValidator.cs ve
ManifestValidator.cs'ten alindi. Amac: import calistirildiginda patlamasin.
"""
import sys, json, glob, pathlib, warnings
import numpy as np, pandas as pd
warnings.filterwarnings("ignore")
sys.stdout.reconfigure(encoding="utf-8")

KOK = pathlib.Path(r"C:\Users\obugr\OneDrive\Masaüstü\ReGreen\sample-data\backend-data")
SINIFLAR = {"COK_YUKSEK", "YUKSEK", "ORTA", "DUSUK"}
DURUMLAR = {"predicted", "low_severity", "no_data"}

hata, uyari = [], []


def H(kod, mesaj):
    hata.append((kod, mesaj))


def U(mesaj):
    uyari.append(mesaj)


man = json.loads((KOK / "manifest.json").read_text(encoding="utf-8"))
print("=" * 82)
print("TESLIM ON KONTROLU")
print("=" * 82)
print("  model_version : %s" % man["model_version"])
print("  schema_version: %s" % man["schema_version"])
print("  yangin        : %d" % man["fire_count"])
print("  hucre         : %s" % "{:,}".format(man["total_cells"]))

# ------------------------------------------------- manifest esik sirasi
e = man["priority_thresholds"]
if not (0 <= e["ORTA"] < e["YUKSEK"] < e["COK_YUKSEK"] <= 1):
    H("THRESHOLDS_INVALID", "manifest.priority_thresholds sirasi bozuk: %s" % e)

w = man["priority_weights"]
if any(v < 0 or not np.isfinite(v) for v in w.values()) or sum(w.values()) <= 0:
    H("WEIGHTS_INVALID", "manifest.priority_weights gecersiz: %s" % w)

manifest_yangin = {f["fire_id"] for f in man["fires"]}

# ----------------------------------------------------------- dosya seti
csvler = sorted(glob.glob(str(KOK / "*_hucreler.csv")))
print("  csv dosyasi   : %d" % len(csvler))
if len(csvler) != man["fire_count"]:
    H("FILE_COUNT_MISMATCH", "csv %d, manifest %d" % (len(csvler), man["fire_count"]))

tum_hucre_id, toplam = set(), 0
dagilim = {"predicted": 0, "low_severity": 0, "no_data": 0}

print("\n%-16s %7s %10s %11s %8s  %s" %
      ("yangin", "satir", "predicted", "low_sev", "no_data", "durum"))
print("-" * 82)

for f in csvler:
    fid = pathlib.Path(f).name.replace("_hucreler.csv", "")
    d = pd.read_csv(f)
    meta_p = KOK / ("%s_metadata.json" % fid)
    geo_p = KOK / ("%s_sinir.geojson" % fid)
    if not meta_p.exists():
        H("METADATA_MISSING", fid); continue
    meta = json.loads(meta_p.read_text(encoding="utf-8"))
    sat = []

    if fid not in manifest_yangin:
        H("FIRE_NOT_IN_MANIFEST", fid)
    if not geo_p.exists() and meta.get("has_perimeter"):
        H("PERIMETER_MISSING", "%s: has_perimeter=true ama geojson yok" % fid)

    # --- aralik kontrolleri
    r = d["recovery_gap_pred"]
    kotu = r.notna() & ((r < -0.5) | (r > 1.5))
    if kotu.any():
        H("RECOVERY_GAP_OUT_OF_RANGE", "%s: %d satir (min %.3f maks %.3f)"
          % (fid, kotu.sum(), r.min(), r.max()))
    p = d["priority_score"]
    kotu = p.notna() & ((p < 0) | (p > 1))
    if kotu.any():
        H("PRIORITY_SCORE_OUT_OF_RANGE", "%s: %d satir" % (fid, kotu.sum()))

    # --- durum enum
    bilinmeyen = set(d["prediction_status"].dropna().unique()) - DURUMLAR
    if bilinmeyen:
        H("STATUS_INVALID", "%s: %s" % (fid, bilinmeyen))
    bilinmeyen = set(d["priority_class"].dropna().unique()) - SINIFLAR
    if bilinmeyen:
        H("PRIORITY_CLASS_INVALID", "%s: %s" % (fid, bilinmeyen))

    # --- §6.2 KESIN TABLO
    st = d["prediction_status"]
    rn, pn, cn = r.notna(), p.notna(), d["priority_class"].notna()

    m = st == "predicted"
    k = m & ~(rn & pn & cn)
    if k.any():
        H("STATUS_CONSISTENCY_VIOLATION",
          "%s: predicted ama alan bos - %d satir" % (fid, k.sum()))

    m = st == "low_severity"
    k = m & ~((~rn) & (p == 0.0) & (d["priority_class"] == "DUSUK"))
    if k.any():
        ornek = d[k].iloc[0]
        H("STATUS_CONSISTENCY_VIOLATION",
          "%s: low_severity kurali - %d satir (rgp=%s ps=%s pc=%s)"
          % (fid, k.sum(), ornek["recovery_gap_pred"],
             ornek["priority_score"], ornek["priority_class"]))

    m = st == "no_data"
    k = m & ~((~rn) & (~pn) & (~cn))
    if k.any():
        ornek = d[k].iloc[0]
        H("STATUS_CONSISTENCY_VIOLATION",
          "%s: no_data kurali - %d satir (rgp=%s ps=%s pc=%s)"
          % (fid, k.sum(), ornek["recovery_gap_pred"],
             ornek["priority_score"], ornek["priority_class"]))

    # --- dagilim metadata ile uyusuyor mu
    vc = st.value_counts().to_dict()
    ms = meta.get("status_counts", {})
    for durum in DURUMLAR:
        a, b = int(vc.get(durum, 0)), int(ms.get(durum, 0))
        if a != b:
            H("CSV_STATUS_DISTRIBUTION_MISMATCH",
              "%s: %s csv=%d metadata=%d" % (fid, durum, a, b))
        dagilim[durum] += a

    # --- hucre id kuresel benzersiz
    ids = set(d["cell_id"])
    if len(ids) != len(d):
        H("CELL_ID_DUPLICATE_IN_FILE", "%s: %d tekrar" % (fid, len(d) - len(ids)))
    cakisma = ids & tum_hucre_id
    if cakisma:
        H("CELL_ID_DUPLICATE_GLOBAL", "%s: %d cakisma" % (fid, len(cakisma)))
    tum_hucre_id |= ids
    toplam += len(d)

    # --- metadata esikleri
    me = meta.get("priority_thresholds", {})
    if me and not (0 <= me["ORTA"] < me["YUKSEK"] < me["COK_YUKSEK"] <= 1):
        H("THRESHOLDS_INVALID", "%s metadata" % fid)

    # --- normalizasyon referansi
    nr = meta.get("normalization_reference", {})
    for alan in ["recovery_gap_pred", "slope_deg", "road_distance_km"]:
        if alan not in nr:
            H("NORM_REF_MISSING", "%s: %s" % (fid, alan))
        elif nr[alan].get("min") is not None and nr[alan].get("max") is not None:
            if nr[alan]["min"] > nr[alan]["max"]:
                H("NORM_REF_INVALID", "%s: %s min>max" % (fid, alan))

    # --- oncelik skoru esikle tutarli mi
    pr = d[st == "predicted"]
    if len(pr):
        bek = np.where(pr["priority_score"] >= me.get("COK_YUKSEK", .75), "COK_YUKSEK",
              np.where(pr["priority_score"] >= me.get("YUKSEK", .5), "YUKSEK",
              np.where(pr["priority_score"] >= me.get("ORTA", .25), "ORTA", "DUSUK")))
        uy = (bek != pr["priority_class"].values).sum()
        if uy:
            H("PRIORITY_CLASS_MISMATCH", "%s: %d satir esikle uyusmuyor" % (fid, uy))

    print("%-16s %7d %10d %11d %8d  %s" %
          (fid, len(d), vc.get("predicted", 0), vc.get("low_severity", 0),
           vc.get("no_data", 0), "ok" if not sat else "!"))

# ------------------------------------------------------------- toplamlar
print("-" * 82)
print("%-16s %7d %10d %11d %8d" % ("TOPLAM", toplam, dagilim["predicted"],
                                   dagilim["low_severity"], dagilim["no_data"]))
if toplam != man["total_cells"]:
    H("TOTAL_CELLS_MISMATCH", "csv %d, manifest %d" % (toplam, man["total_cells"]))
if len(tum_hucre_id) != toplam:
    H("CELL_ID_NOT_UNIQUE", "benzersiz %d, toplam %d" % (len(tum_hucre_id), toplam))

# ------------------------------------------------------------- sonuc
print("\n" + "=" * 82)
if hata:
    print("HATA: %d" % len(hata))
    for kod, m in hata[:25]:
        print("  [%s] %s" % (kod, m))
else:
    print("HATA YOK - Beytullah'in dogrulayicisi bu veriyi kabul eder")
if uyari:
    print("\nUYARI: %d" % len(uyari))
    for m in uyari[:10]:
        print("  %s" % m)
print("=" * 82)
sys.exit(1 if hata else 0)
