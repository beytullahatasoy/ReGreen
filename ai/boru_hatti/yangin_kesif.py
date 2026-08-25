# -*- coding: utf-8 -*-
"""
Turkiye genelinde orman yangini kesfi
=====================================

Amac: "hangi yanginlari isleyecegiz" listesini OTOMATIK uretmek. AFAD ve EFFIS
calismadigi icin yangin envanterini kendimiz cikariyoruz.

Yontem:
  1. MODIS MCD64A1 aylik yanmis alan urunu (500 m, kuresel, ucretsiz)
  2. Turkiye sinirina kirp  -> Suriye, Irak, Yunanistan disarida kalsin
  3. Arazi ortusune gore ele -> anız yakma ve tarim yanginlarini at
  4. Boyut esigi           -> 500 m hucrede anlamli olacak kadar buyuk olsun
  5. Bagli bilesenleri kumele, her kume bir yangin

Cikti: yanginlar.json  ->  pipeline'in okuyacagi bolge listesi

Calistirma:
    python yangin_kesif.py

Ara ciktilar onbellege alinir (onbellek/ klasoru); ikinci calistirma cok daha
hizli olur. Onbellegi silmek icin klasoru silin.
"""

import json
import os
import pathlib
import warnings

import numpy as np
import requests

warnings.filterwarnings("ignore")

import planetary_computer as pc
import rasterio
from pystac_client import Client
from rasterio.enums import Resampling
from rasterio.features import rasterize
from rasterio.transform import from_origin
from rasterio.warp import reproject
from scipy import ndimage
from shapely.geometry import shape

# ----------------------------------------------------------------- konfig
YILLAR = list(range(2017, 2025))   # Sentinel-2 2017'de basliyor.
                                   # Ust sinir 2024: etiket icin yangindan 2 yil
                                   # sonraki goruntu lazim, bugun 2026.
AYLAR = list(range(5, 12))         # Mayis-Kasim, Turkiye yangin mevsimi

COZUNURLUK = 0.005                 # ~500 m calisma izgarasi (derece)
TR_BBOX = [25.5, 35.7, 45.0, 42.4]

MIN_HEKTAR = 300                   # 300 ha = 12 hucre. Sinirda ama kullanilabilir.
                                   # Daha asagisi (200 ha = 8 hucre) yalnizca kenar
                                   # piksel getirir, gurultuden baska sey katmaz.
                                   # Esigi 500'den 300'e indirmek grup sayisini
                                   # %41 artiriyor, alani ise sadece %3.
MIN_ORMANLIK = 0.45                # kume piksellerinin en az bu kadari
                                   # agaclik veya calilik olmali
MAKS_TARIM = 0.35                  # bu orandan cok tarimsa anız yakmadir

# ESA WorldCover sinif kodlari
AGAC, CALI, OT, TARIM, YERLESIM, CIPLAK, SU = 10, 20, 30, 40, 50, 60, 80

KOK = pathlib.Path(__file__).parent
ONBELLEK = KOK / "onbellek"
ONBELLEK.mkdir(exist_ok=True)

UA = {"User-Agent": "FireRecoverAI/0.3 (Huawei ICT Competition student project)"}
cat = Client.open("https://planetarycomputer.microsoft.com/api/stac/v1",
                  modifier=pc.sign_inplace)

# Cografi bolgeler. Modele GIRDI OLARAK VERILMEZ (ayni bolge icinde yagis
# 4 kat degisebiliyor); yalnizca haritada filtre ve raporlama etiketi.
BOLGELER = [
    ("Akdeniz",        lambda la, lo: la < 37.6 and 29.0 <= lo < 36.5),
    ("Ege",            lambda la, lo: 36.5 <= la < 39.5 and lo < 29.0),
    ("Marmara",        lambda la, lo: la >= 39.5 and lo < 30.5),
    ("Karadeniz",      lambda la, lo: la >= 40.3 and lo >= 30.5),
    ("Guneydogu",      lambda la, lo: la < 38.5 and lo >= 36.5),
    ("Dogu Anadolu",   lambda la, lo: la >= 38.5 and lo >= 38.0),
    ("Ic Anadolu",     lambda la, lo: True),          # geri kalan
]


def bolge_bul(lat, lon):
    for ad, kural in BOLGELER:
        if kural(lat, lon):
            return ad
    return "Bilinmiyor"


# ------------------------------------------------------------- calisma izgarasi
class Izgara:
    """Turkiye'yi kaplayan duz enlem-boylam izgarasi."""

    def __init__(self):
        l, b, r, t = TR_BBOX
        self.w = int(round((r - l) / COZUNURLUK))
        self.h = int(round((t - b) / COZUNURLUK))
        self.transform = from_origin(l, t, COZUNURLUK, COZUNURLUK)
        self.crs = "EPSG:4326"
        self.bbox = TR_BBOX

    def lonlat(self, satir, sutun):
        l, _, _, t = TR_BBOX
        return (l + (sutun + 0.5) * COZUNURLUK,
                t - (satir + 0.5) * COZUNURLUK)

    @property
    def sekil(self):
        return (self.h, self.w)


IZGARA = Izgara()


# ------------------------------------------------------------- Turkiye maskesi
def turkiye_maskesi():
    yol = ONBELLEK / "turkiye_maske.npy"
    if yol.exists():
        return np.load(yol)

    print("  Turkiye sinir poligonu indiriliyor (Nominatim)...")
    gj = ONBELLEK / "turkiye.geojson"
    if gj.exists():
        geo = json.loads(gj.read_text())
    else:
        r = requests.get("https://nominatim.openstreetmap.org/search", params={
            "country": "Turkey", "format": "json",
            "polygon_geojson": 1, "limit": 1}, headers=UA, timeout=90)
        r.raise_for_status()
        geo = r.json()[0]["geojson"]
        gj.write_text(json.dumps(geo))

    poly = shape(geo)
    maske = rasterize([(poly, 1)], out_shape=IZGARA.sekil,
                      transform=IZGARA.transform, dtype="uint8")
    np.save(yol, maske)
    print(f"  maske hazir: %{maske.mean()*100:.1f} kara")
    return maske


# ------------------------------------------------------------- arazi ortusu
def arazi_ortusu():
    """Turkiye geneli baskin arazi sinifi, 500 m izgarada."""
    yol = ONBELLEK / "arazi_ortusu.npy"
    if yol.exists():
        return np.load(yol)

    print("  ESA WorldCover indiriliyor...")
    items = list(cat.search(collections=["esa-worldcover"], bbox=TR_BBOX).items())
    items = [i for i in items if "2021" in i.id] or items
    print(f"    {len(items)} karo")

    lc = np.zeros(IZGARA.sekil, dtype="uint8")
    for k, it in enumerate(items, 1):
        try:
            with rasterio.open(it.assets["map"].href) as src:
                hedef = np.zeros(IZGARA.sekil, dtype="uint8")
                reproject(source=rasterio.band(src, 1), destination=hedef,
                          dst_transform=IZGARA.transform, dst_crs=IZGARA.crs,
                          resampling=Resampling.mode, num_threads=4)
            lc = np.where(hedef > 0, hedef, lc)
        except Exception as e:
            print(f"    karo {k} atlandi: {type(e).__name__}")
        if k % 5 == 0:
            print(f"    {k}/{len(items)}")
    np.save(yol, lc)
    return lc


# ------------------------------------------------------------- MODIS yanik
def yil_yanik(yil):
    """Bir yilin yanmis alani: 0 = yanmadi, 1-366 = yanma gunu."""
    yol = ONBELLEK / f"yanik_{yil}.npy"
    if yol.exists():
        return np.load(yol)

    toplam = np.zeros(IZGARA.sekil, dtype="uint16")
    items = list(cat.search(
        collections=["modis-64A1-061"], bbox=TR_BBOX,
        datetime=f"{yil}-{AYLAR[0]:02d}-01/{yil}-{AYLAR[-1]:02d}-28").items())
    print(f"    {yil}: {len(items)} karo-ay", end="", flush=True)

    for it in items:
        try:
            with rasterio.open(it.assets["Burn_Date"].href) as src:
                hedef = np.zeros(IZGARA.sekil, dtype="int16")
                reproject(source=rasterio.band(src, 1), destination=hedef,
                          dst_transform=IZGARA.transform, dst_crs=IZGARA.crs,
                          resampling=Resampling.nearest, num_threads=4)
            g = hedef > 0
            toplam = np.where(g & (toplam == 0), hedef.astype("uint16"), toplam)
        except Exception:
            continue
    np.save(yol, toplam)
    print(f"  ->  {int((toplam > 0).sum()):,} yanik piksel")
    return toplam


# ------------------------------------------------------------- kumeleme
def hucre_alani_ha(lat):
    """Bu enlemde bir izgara hucresinin alani (hektar)."""
    m_lat = COZUNURLUK * 111_320
    m_lon = COZUNURLUK * 111_320 * np.cos(np.radians(lat))
    return m_lat * m_lon / 10_000


def yanginlari_bul(yil, yanik, maske, lc):
    """Bir yildaki yanik alani kumeleyip filtreleri uygular."""
    g = (yanik > 0) & (maske == 1)
    if not g.any():
        return []

    etiket, n = ndimage.label(g, structure=np.ones((3, 3)))
    if n == 0:
        return []

    nesneler = ndimage.find_objects(etiket)
    sonuc = []
    for i, dilim in enumerate(nesneler, 1):
        if dilim is None:
            continue
        alt_e = etiket[dilim] == i
        piksel = int(alt_e.sum())
        if piksel < 8:                       # cok kucuk, hesaplamaya deger
            continue

        satirlar, sutunlar = np.nonzero(alt_e)
        r0, c0 = dilim[0].start, dilim[1].start
        merkez_lon, merkez_lat = IZGARA.lonlat(r0 + satirlar.mean(),
                                               c0 + sutunlar.mean())
        alan = piksel * hucre_alani_ha(merkez_lat)
        if alan < MIN_HEKTAR:
            continue

        # arazi ortusu filtresi
        alt_lc = lc[dilim][alt_e]
        ormanlik = float(np.isin(alt_lc, [AGAC, CALI]).mean())
        tarim = float((alt_lc == TARIM).mean())
        if ormanlik < MIN_ORMANLIK or tarim > MAKS_TARIM:
            continue

        # yanma tarihi: kumedeki medyan gun
        gunler = yanik[dilim][alt_e]
        gun = int(np.median(gunler[gunler > 0]))
        tarih = (np.datetime64(f"{yil}-01-01") + np.timedelta64(gun - 1, "D"))

        # bbox: kumeyi cevrele, sonra yanmamis baglam icin genislet
        lon0, lat1 = IZGARA.lonlat(r0, c0)
        lon1, lat0 = IZGARA.lonlat(r0 + alt_e.shape[0], c0 + alt_e.shape[1])
        pay = max(0.045, (lon1 - lon0) * 0.28, (lat1 - lat0) * 0.28)
        bbox = [round(lon0 - pay, 4), round(lat0 - pay, 4),
                round(lon1 + pay, 4), round(lat1 + pay, 4)]

        sonuc.append({
            "yil": yil,
            "tarih": str(tarih),
            "lat": round(float(merkez_lat), 4),
            "lon": round(float(merkez_lon), 4),
            "alan_ha": int(alan),
            "bolge": bolge_bul(merkez_lat, merkez_lon),
            "ormanlik_oran": round(ormanlik, 2),
            "bbox": bbox,
        })
    return sonuc


def id_ver(yanginlar):
    """Okunabilir kimlik: BOLGE_YIL_SIRA."""
    sayac = {}
    kisalt = {"Akdeniz": "AKD", "Ege": "EGE", "Marmara": "MAR",
              "Karadeniz": "KAR", "Guneydogu": "GDA", "Dogu Anadolu": "DAN",
              "Ic Anadolu": "IAN", "Bilinmiyor": "BIL"}
    for y in sorted(yanginlar, key=lambda x: (-x["alan_ha"],)):
        k = f"{kisalt.get(y['bolge'], 'XXX')}_{y['yil']}"
        sayac[k] = sayac.get(k, 0) + 1
        y["id"] = f"{k}_{sayac[k]:02d}"
    return yanginlar


# ------------------------------------------------------------- ana
def main():
    print("=" * 72)
    print("TURKIYE GENELI YANGIN KESFI")
    print(f"Yillar {YILLAR[0]}-{YILLAR[-1]}  ·  en az {MIN_HEKTAR} ha  ·  "
          f"izgara {IZGARA.h} x {IZGARA.w}")
    print("=" * 72)

    print("\n[1/4] Turkiye maskesi")
    maske = turkiye_maskesi()

    print("\n[2/4] Arazi ortusu")
    lc = arazi_ortusu()
    for ad, kod in [("agaclik", AGAC), ("calilik", CALI), ("otlak", OT),
                    ("tarim", TARIM)]:
        print(f"    %{(lc[maske == 1] == kod).mean()*100:5.1f}  {ad}")

    print("\n[3/4] MODIS yanmis alan")
    hepsi = []
    for yil in YILLAR:
        yanik = yil_yanik(yil)
        bulunan = yanginlari_bul(yil, yanik, maske, lc)
        hepsi.extend(bulunan)
        print(f"        -> {len(bulunan)} yangin (filtrelerden gecen)")

    print("\n[4/4] Ozet")
    hepsi = id_ver(hepsi)
    hepsi.sort(key=lambda x: (-x["alan_ha"],))

    yol = KOK / "yanginlar.json"
    yol.write_text(json.dumps(hepsi, ensure_ascii=False, indent=2),
                   encoding="utf-8")

    print(f"\n  Toplam: {len(hepsi)} yangin, "
          f"{sum(y['alan_ha'] for y in hepsi):,} ha")

    print("\n  Yila gore:")
    for yil in YILLAR:
        y = [x for x in hepsi if x["yil"] == yil]
        if y:
            print(f"    {yil}: {len(y):>3} yangin, {sum(k['alan_ha'] for k in y):>9,} ha")

    print("\n  Bolgeye gore:")
    for ad, _ in BOLGELER:
        y = [x for x in hepsi if x["bolge"] == ad]
        if y:
            print(f"    {ad:<14}{len(y):>4} yangin, {sum(k['alan_ha'] for k in y):>9,} ha")

    print("\n  En buyuk 12:")
    for y in hepsi[:12]:
        print(f"    {y['id']:<16}{y['tarih']}  {y['alan_ha']:>7,} ha  "
              f"{y['lat']:.2f},{y['lon']:.2f}  {y['bolge']}")

    print(f"\n  -> {yol.name} yazildi")


if __name__ == "__main__":
    main()
