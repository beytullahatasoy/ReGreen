# -*- coding: utf-8 -*-
"""
FireRecover AI - Veri Pipeline'i (grid tabanli, olceklenebilir surum)
=====================================================================

Fizibilite testinden farki: artik TEK NOKTA degil, tum yangin alani
500 m'lik hucrelere bolunup her hucre icin ozellik vektoru uretiliyor.

DUZELTILEN NOKTALAR (fizibilite testindeki sorunlar):
  1. yanan_alan_ha (elle girilen sabit) ATILDI  -> dNBR yangin siddeti
     Hucre basina degisen gercek olcum. AFAD/EFFIS bagimliligi tamamen kalkti.
  2. OpenTopoData (1 istek/sn limiti)          -> Copernicus DEM 30m (COG)
     Binlerce hucre birkac istekte. Egim 100 m'de hesaplanip 500 m'ye toplaniyor.
  3. CORINE (25 ha min. haritalama birimi)     -> ESA WorldCover 10m
     500 m hucrede artik gercek sinif cogunlugu var.
  4. Overpass nokta-nokta sorgu                -> bolge basina 3 toplu sorgu
     + cKDTree ile yerel en-yakin-komsu. Hucre sayisindan bagimsiz.
  5. Tek uydu sahnesi                          -> MEDYAN KOMPOZIT (<=6 sahne)
     Ince sis / kacan bulut golgesi / BRDF gurultusu bastiriliyor.
  6. Yola mesafe EKLENDI - mudahalenin uygulanabilirligi (erisim) olcusu.
  7. Cok bolge destegi - GroupKFold icin gercek cografi grup anahtari.

CIKTI:
  firerecover_grid.csv        - hucre basina bir satir (ozellik tablosu)
  firerecover_haritalar.png   - katman haritalari (sunum icin)
  firerecover_ozet.json       - calisma meta verisi + kalite metrikleri

Calistirma:
    python firerecover_pipeline.py
"""

import json
import math
import time
import warnings
from dataclasses import dataclass

import numpy as np
import pandas as pd
import requests

import planetary_computer as pc
import rasterio
from affine import Affine
from pystac_client import Client
from rasterio.enums import Resampling
from rasterio.transform import xy
from rasterio.warp import reproject, transform_bounds
from rasterio.warp import transform as warp_transform
from rasterio.windows import from_bounds
from scipy.spatial import cKDTree

warnings.filterwarnings("ignore")

# ============================================================================
# KONFIG
# ============================================================================
HUCRE_M = 500          # grid cozunurlugu (metre)
MAKS_SAHNE = 8         # medyan kompozit icin sahne ust siniri
BULUT_ESIK = 30        # % - sahne on elemesi

# --- Maskeleme ve etiket gecerlilik esikleri --------------------------------
# Bu esikler keyfi degil, uretilen veriden olculdu (bkz. FEASIBILITY_README):
# dusuk siddetli hucrelerde etiketin std'si 0.52, yuksek siddetlilerde 0.10.
YANIK_ESIK_DNBR = 0.10    # USGS: altinda "yanmamis"
BITKI_ESIK = 0.20         # yangin oncesi NDVI - "yanacak bitki var miydi?"
                          #   su/kayalik/plaj yanlis pozitiflerini eler
ETIKET_ESIK_DNBR = 0.27   # guvenilir etiket icin en az "orta-dusuk" siddet
ETIKET_ESIK_PAYDA = 0.20  # NDVI dususu; kucuk paydada oran patliyor
SU_SINIFI = 80            # ESA WorldCover su sinifi

# GroupKFold icin grup anahtari = bolge_kodu.
# Mekansal capraz dogrulama ancak BIRDEN FAZLA bolgeyle anlamli olur;
# 3 grup = 3 fold. Adaylar dNBR sinyaline gore secildi (Marmaris ve Koycegiz
# elendi: sirasiyla yangin sonrasi bulutsuz sahne yok / sinyal cok zayif).
BOLGELER = {
    "MANAVGAT_2021": {
        "ad": "Antalya / Manavgat",
        "bbox": [31.30, 36.75, 31.90, 37.30],
        "yangin_tarihi": "2021-07-28",
        "pencereler": {
            "oncesi":  "2021-06-15/2021-07-27",
            "sonrasi": "2021-08-20/2021-09-25",
            "yil1":    "2022-06-15/2022-08-10",
            "yil2":    "2023-06-15/2023-08-10",
        },
    },
    "BODRUM_2021": {
        "ad": "Mugla / Bodrum-Mumcular",
        "bbox": [27.30, 36.95, 27.80, 37.25],
        "yangin_tarihi": "2021-07-30",
        "pencereler": {
            "oncesi":  "2021-06-15/2021-07-28",
            "sonrasi": "2021-08-25/2021-09-30",
            "yil1":    "2022-06-15/2022-08-10",
            "yil2":    "2023-06-15/2023-08-10",
        },
    },
    "MILAS_2021": {
        "ad": "Mugla / Milas",
        "bbox": [27.60, 37.05, 28.10, 37.45],
        "yangin_tarihi": "2021-08-03",
        "pencereler": {
            "oncesi":  "2021-06-15/2021-07-28",
            "sonrasi": "2021-08-25/2021-09-30",
            "yil1":    "2022-06-15/2022-08-10",
            "yil2":    "2023-06-15/2023-08-10",
        },
    },
}

UA = {"User-Agent": "FireRecoverAI/0.2 (Huawei ICT Competition student project)"}
STAC = "https://planetarycomputer.microsoft.com/api/stac/v1"
cat = Client.open(STAC, modifier=pc.sign_inplace)

WORLDCOVER = {10: "Agaclik", 20: "Calilik", 30: "Otlak", 40: "Tarim", 50: "Yerlesim",
              60: "Ciplak/seyrek", 70: "Kar/buz", 80: "Su", 90: "Sulak alan",
              95: "Mangrov", 100: "Liken/yosun"}


# ============================================================================
# HEDEF GRID
# ============================================================================
@dataclass
class Grid:
    crs: str
    transform: Affine
    h: int
    w: int
    bbox4326: list

    @staticmethod
    def olustur(bbox4326, hucre_m=HUCRE_M):
        """bbox'in orta boylamina gore UTM zonu secip metrik grid kurar."""
        orta_lon = (bbox4326[0] + bbox4326[2]) / 2
        orta_lat = (bbox4326[1] + bbox4326[3]) / 2
        zon = int((orta_lon + 180) // 6) + 1
        epsg = (32600 if orta_lat >= 0 else 32700) + zon
        crs = f"EPSG:{epsg}"
        l, b, r, t = transform_bounds("EPSG:4326", crs, *bbox4326)
        # hucre boyutuna hizala
        l = math.floor(l / hucre_m) * hucre_m
        b = math.floor(b / hucre_m) * hucre_m
        r = math.ceil(r / hucre_m) * hucre_m
        t = math.ceil(t / hucre_m) * hucre_m
        w = int((r - l) / hucre_m)
        h = int((t - b) / hucre_m)
        return Grid(crs, Affine(hucre_m, 0, l, 0, -hucre_m, t), h, w, bbox4326)

    def merkez_koordinatlar(self):
        rows, cols = np.mgrid[0:self.h, 0:self.w]
        xs, ys = xy(self.transform, rows.ravel(), cols.ravel(), offset="center")
        return np.asarray(xs), np.asarray(ys)

    def merkez_lonlat(self):
        xs, ys = self.merkez_koordinatlar()
        lon, lat = warp_transform(self.crs, "EPSG:4326", list(xs), list(ys))
        return np.asarray(lon), np.asarray(lat)


# ============================================================================
# RASTER OKUMA -> GRID
# ============================================================================
def _pencere_oku(href, bbox4326, hedef_hw, resampling):
    """
    COG'dan bbox penceresini decimated okur.
    KRITIK: pencere raster sinirlarina KIRPILIR. Aksi halde rasterio tasan
    pencereyi sessizce kirpip out_shape'e gerer ve KOORDINATLAR KAYAR
    (fizibilite asamasinda tam olarak bu hata yanmamis bir noktayi
     'en cok yanan yer' olarak gostermisti).
    """
    with rasterio.open(href) as src:
        b = transform_bounds("EPSG:4326", src.crs, *bbox4326)
        l = max(b[0], src.bounds.left)
        bo = max(b[1], src.bounds.bottom)
        r = min(b[2], src.bounds.right)
        t = min(b[3], src.bounds.top)
        if r <= l or t <= bo:
            return None, None, None
        w = from_bounds(l, bo, r, t, transform=src.transform)
        oh = max(1, min(int(round(w.height)), hedef_hw[0]))
        ow = max(1, min(int(round(w.width)), hedef_hw[1]))
        arr = src.read(1, window=w, out_shape=(oh, ow), resampling=resampling)
        tr = src.window_transform(w) * Affine.scale(w.width / ow, w.height / oh)
        return arr.astype("float32"), tr, src.crs


def _grid_e_aktar(arr, tr, crs, grid, resampling, hedef=None):
    if hedef is None:
        hedef = np.full((grid.h, grid.w), np.nan, dtype="float32")
    reproject(source=arr, destination=hedef,
              src_transform=tr, src_crs=crs,
              dst_transform=grid.transform, dst_crs=grid.crs,
              src_nodata=np.nan, dst_nodata=np.nan, resampling=resampling)
    return hedef


def raster_grid(href, grid, resampling=Resampling.average, olcek=1):
    """Tek bir COG'u hedef grid'e getirir. olcek>1 -> daha ince ara grid."""
    hedef_hw = (grid.h * olcek * 2, grid.w * olcek * 2)
    arr, tr, crs = _pencere_oku(href, grid.bbox4326, hedef_hw, resampling)
    if arr is None:
        return None
    return arr, tr, crs


# ============================================================================
# 1) SENTINEL-2 -> NDVI + NBR MEDYAN KOMPOZIT
# ============================================================================
def s2_kompozit(grid, period, nbr_de=False):
    """
    Pencere icindeki en temiz <=MAKS_SAHNE sahneden MEDYAN NDVI (ve istege
    bagli NBR) uretir. Tek sahneye gore sis/golge/BRDF gurultusune dayanikli.
    """
    items = list(cat.search(collections=["sentinel-2-l2a"], bbox=grid.bbox4326,
                            datetime=period,
                            query={"eo:cloud_cover": {"lt": BULUT_ESIK}}).items())
    items.sort(key=lambda i: i.properties["eo:cloud_cover"])
    items = items[:MAKS_SAHNE * 3]   # farkli karolar olabilir, bol al

    ndvi_yigin, nbr_yigin, kullanilan = [], [], []
    for it in items:
        if len(kullanilan) >= MAKS_SAHNE:
            break
        try:
            bantlar = {}
            for ad, asset, rs in [("red", "B04", Resampling.average),
                                  ("nir", "B08", Resampling.average),
                                  ("scl", "SCL", Resampling.nearest)] + \
                                 ([("nir2", "B8A", Resampling.average),
                                   ("swir", "B12", Resampling.average)] if nbr_de else []):
                r = raster_grid(it.assets[asset].href, grid, rs)
                if r is None:
                    bantlar = None
                    break
                bantlar[ad] = _grid_e_aktar(r[0], r[1], r[2], grid,
                                            Resampling.average if rs == Resampling.average
                                            else Resampling.nearest)
            if bantlar is None:
                continue

            # SCL 4=bitki 5=ciplak 6=su 7=sinifsiz ; bulut/golge/kar disarida
            iyi = np.isin(np.rint(np.nan_to_num(bantlar["scl"], nan=0)).astype("int16"),
                          [4, 5, 6, 7])
            if iyi.mean() < 0.25:
                continue

            nd = (bantlar["nir"] - bantlar["red"]) / (bantlar["nir"] + bantlar["red"] + 1e-9)
            ndvi_yigin.append(np.where(iyi, nd, np.nan))
            if nbr_de:
                nb = (bantlar["nir2"] - bantlar["swir"]) / (bantlar["nir2"] + bantlar["swir"] + 1e-9)
                nbr_yigin.append(np.where(iyi, nb, np.nan))
            kullanilan.append({"id": it.id, "tarih": str(it.datetime.date()),
                               "karo": it.properties.get("s2:mgrs_tile"),
                               "bulut": round(it.properties["eo:cloud_cover"], 1)})
        except Exception:
            continue

    if not ndvi_yigin:
        bos = np.full((grid.h, grid.w), np.nan, dtype="float32")
        return bos, bos, []

    ndvi = np.nanmedian(np.dstack(ndvi_yigin), axis=2)
    nbr = np.nanmedian(np.dstack(nbr_yigin), axis=2) if nbr_yigin else \
        np.full((grid.h, grid.w), np.nan, dtype="float32")
    return ndvi, nbr, kullanilan


# ============================================================================
# 2) COPERNICUS DEM -> YUKSELTI / EGIM / BAKI
# ============================================================================
def dem_katmanlari(grid):
    """
    Egim 500 m grid'de dogrudan hesaplanirsa ciddi sekilde HAFIFE ALINIR.
    Bu yuzden: DEM once 100 m'lik ara grid'e getirilir, egim orada
    hesaplanir, sonra 500 m'ye ortalanarak toplanir (blok ortalama).
    Copernicus DEM 1 derecelik karolara bolundugu icin mozaikleme sart.
    """
    ince = Grid.olustur(grid.bbox4326, hucre_m=100)
    items = list(cat.search(collections=["cop-dem-glo-30"], bbox=grid.bbox4326).items())
    if not items:
        bos = np.full((grid.h, grid.w), np.nan, dtype="float32")
        return bos, bos, bos, 0

    z = np.full((ince.h, ince.w), np.nan, dtype="float32")
    for it in items:                       # mozaik: karolari ust uste yaz
        r = raster_grid(it.assets["data"].href, ince, Resampling.bilinear)
        if r is None:
            continue
        parca = _grid_e_aktar(r[0], r[1], r[2], ince, Resampling.bilinear)
        z = np.where(np.isfinite(parca), parca, z)

    gy, gx = np.gradient(np.nan_to_num(z, nan=float(np.nanmean(z))), 100.0, 100.0)
    egim = np.degrees(np.arctan(np.hypot(gx, gy)))
    baki = (np.degrees(np.arctan2(gy, -gx)) + 360) % 360

    def kaba(a, rs=Resampling.average):
        return _grid_e_aktar(a.astype("float32"), ince.transform, ince.crs, grid, rs)

    return kaba(z), kaba(egim), kaba(baki, Resampling.nearest), len(items)


# ============================================================================
# 3) ESA WORLDCOVER -> ARAZI TIPI
# ============================================================================
def arazi_ortusu(grid):
    items = list(cat.search(collections=["esa-worldcover"], bbox=grid.bbox4326).items())
    items = [i for i in items if "2021" in i.id] or items
    if not items:
        return np.full((grid.h, grid.w), np.nan, dtype="float32"), \
               np.full((grid.h, grid.w), np.nan, dtype="float32"), 0

    # Cogunluk sinifi (mode) + agac orani ayri ayri
    lc = np.full((grid.h, grid.w), np.nan, dtype="float32")
    agac = np.full((grid.h, grid.w), np.nan, dtype="float32")
    for it in items:
        r = raster_grid(it.assets["map"].href, grid, Resampling.mode, olcek=2)
        if r is None:
            continue
        p = _grid_e_aktar(r[0], r[1], r[2], grid, Resampling.mode)
        lc = np.where(np.isfinite(p), p, lc)
        # agac orani: 10 sinifinin oransal kapsami (average ile)
        r2 = raster_grid(it.assets["map"].href, grid, Resampling.nearest, olcek=2)
        ikili = (np.abs(r2[0] - 10) < 0.5).astype("float32")
        pa = _grid_e_aktar(ikili, r2[1], r2[2], grid, Resampling.average)
        agac = np.where(np.isfinite(pa), pa, agac)
    return lc, agac, len(items)


# ============================================================================
# 4) OSM -> YERLESIM / SU / YOL MESAFESI  (toplu sorgu + KDTree)
# ============================================================================
OVERPASS_UCLARI = [
    "https://overpass-api.de/api/interpreter",
    "https://maps.mail.ru/osm/tools/overpass/api/interpreter",
]


def _overpass(query, deneme=3):
    son = None
    for tur in range(deneme):
        for url in OVERPASS_UCLARI:
            try:
                r = requests.post(url, data={"data": query}, headers=UA, timeout=300)
                r.raise_for_status()
                return r.json()
            except Exception as e:
                son = e
        time.sleep(5 * (tur + 1))
    raise son


def _noktalar(js):
    out = []
    for el in js.get("elements", []):
        if "lat" in el:
            out.append((el["lat"], el["lon"]))
        elif "center" in el:
            out.append((el["center"]["lat"], el["center"]["lon"]))
    return out


def osm_mesafeler(grid, tampon_deg=0.25):
    """
    Hucre basina HTTP cagrisi YAPILMAZ. Tum bolge icin 3 toplu sorgu atilir,
    sonuclar grid CRS'ine yansitilip cKDTree ile en yakin komsu bulunur.
    Boylece maliyet hucre sayisindan tamamen bagimsizdir.
    """
    b = grid.bbox4326
    kutu = f"{b[1]-tampon_deg},{b[0]-tampon_deg},{b[3]+tampon_deg},{b[2]+tampon_deg}"

    sorgular = {
        "yerlesim": f'[out:json][timeout:280];(node({kutu})["place"~"^(city|town|village)$"];);out body;',
        "su":       f'[out:json][timeout:280];(way({kutu})["natural"="water"];'
                    f'way({kutu})["waterway"~"^(river|stream|canal)$"];'
                    f'way({kutu})["landuse"="reservoir"];);out center;',
        "yol":      f'[out:json][timeout:280];(way({kutu})'
                    f'["highway"~"^(motorway|trunk|primary|secondary|tertiary|unclassified|residential|track)$"];);'
                    f'out center;',
    }

    gx, gy = grid.merkez_koordinatlar()
    hedef = np.column_stack([gx, gy])
    sonuc, sayim = {}, {}

    for ad, q in sorgular.items():
        try:
            pts = _noktalar(_overpass(q))
            sayim[ad] = len(pts)
            if not pts:
                sonuc[ad] = np.full(grid.h * grid.w, np.nan, dtype="float32")
                continue
            lats = [p[0] for p in pts]
            lons = [p[1] for p in pts]
            X, Y = warp_transform("EPSG:4326", grid.crs, lons, lats)
            d, _ = cKDTree(np.column_stack([X, Y])).query(hedef, k=1)
            sonuc[ad] = (d / 1000.0).astype("float32")   # km
        except Exception as e:
            print(f"    ! OSM {ad} basarisiz: {type(e).__name__}")
            sonuc[ad] = np.full(grid.h * grid.w, np.nan, dtype="float32")
            sayim[ad] = 0
    return sonuc, sayim


# ============================================================================
# BOLGE ISLEME
# ============================================================================
def bolge_isle(kod, cfg):
    print("\n" + "=" * 74)
    print(f"BOLGE: {cfg['ad']}  ({kod})")
    print("=" * 74)
    grid = Grid.olustur(cfg["bbox"])
    print(f"  Grid: {grid.h} x {grid.w} = {grid.h*grid.w:,} hucre @ {HUCRE_M} m  [{grid.crs}]")

    meta = {"bolge": cfg["ad"], "grid": f"{grid.h}x{grid.w}", "hucre_m": HUCRE_M,
            "crs": grid.crs, "sahneler": {}}

    # --- Sentinel-2 ---------------------------------------------------
    ndvi, nbr = {}, {}
    for etiket, period in cfg["pencereler"].items():
        nbr_ister = etiket in ("oncesi", "sonrasi")
        t0 = time.time()
        nd, nb, kul = s2_kompozit(grid, period, nbr_de=nbr_ister)
        ndvi[etiket], nbr[etiket] = nd, nb
        meta["sahneler"][etiket] = kul
        kapsam = float(np.isfinite(nd).mean())
        print(f"  S2 {etiket:<8}: {len(kul)} sahne medyani, kapsam %{kapsam*100:.0f}, "
              f"{time.time()-t0:.0f} sn")

    dnbr = nbr["oncesi"] - nbr["sonrasi"]
    dndvi = ndvi["oncesi"] - ndvi["sonrasi"]
    print(f"  -> dNBR ort={np.nanmean(dnbr):.3f}, maks={np.nanmax(dnbr):.2f}")

    # --- DEM ----------------------------------------------------------
    t0 = time.time()
    z, egim, baki, n_dem = dem_katmanlari(grid)
    print(f"  DEM: {n_dem} karo mozaik, yukselti {np.nanmin(z):.0f}-{np.nanmax(z):.0f} m, "
          f"egim ort {np.nanmean(egim):.1f} der, {time.time()-t0:.0f} sn")

    # --- Arazi ortusu -------------------------------------------------
    t0 = time.time()
    lc, agac_orani, n_lc = arazi_ortusu(grid)
    print(f"  WorldCover: {n_lc} karo, agac orani ort %{np.nanmean(agac_orani)*100:.0f}, "
          f"{time.time()-t0:.0f} sn")

    # --- OSM ----------------------------------------------------------
    t0 = time.time()
    mes, sayim = osm_mesafeler(grid)
    print(f"  OSM: yerlesim {sayim.get('yerlesim',0)} / su {sayim.get('su',0)} / "
          f"yol {sayim.get('yol',0)} nesne -> 3 sorgu, {time.time()-t0:.0f} sn")

    # --- Tablo --------------------------------------------------------
    lon, lat = grid.merkez_lonlat()
    d = lambda a: a.ravel()

    df = pd.DataFrame({
        "bolge_kodu": kod,                      # <- GroupKFold grup anahtari
        "bolge_adi": cfg["ad"],
        "hucre_id": [f"{kod}_{i:06d}" for i in range(grid.h * grid.w)],
        "lat": np.round(lat, 5), "lon": np.round(lon, 5),
        "yangin_tarihi": cfg["yangin_tarihi"],
        # topografya
        "yukselti_m": d(z), "egim_derece": d(egim), "baki_derece": d(baki),
        # arazi
        "arazi_kodu": d(lc), "agac_orani": d(agac_orani),
        # erisim / maruziyet
        "yerlesim_mesafe_km": mes["yerlesim"], "su_mesafe_km": mes["su"],
        "yol_mesafe_km": mes["yol"],
        # yangin siddeti
        "dnbr": d(dnbr), "dndvi": d(dndvi),
        # ndvi serisi
        "ndvi_oncesi": d(ndvi["oncesi"]), "ndvi_sonrasi": d(ndvi["sonrasi"]),
        "ndvi_yil1": d(ndvi["yil1"]), "ndvi_yil2": d(ndvi["yil2"]),
    })

    df["arazi_tipi"] = df["arazi_kodu"].map(
        lambda v: WORLDCOVER.get(int(v), None) if np.isfinite(v) else None)

    # --- MASKELEME -----------------------------------------------------
    # Butun katmanlarin veri getirdigi hucreler
    df["gecerli_hucre"] = df[["dnbr", "arazi_kodu", "egim_derece",
                              "ndvi_oncesi", "ndvi_yil2"]].notna().all(axis=1)

    # "Yanacak bitki var miydi?" - fiziksel tutarlilik kontrolu.
    # Bu olmadan deniz/gol hucreleri yanlis pozitif olarak yanik cikiyor:
    # su uzerinde NBR gunes parlamasi yuzunden gurultulu, dNBR sahte yukseliyor.
    df["bitki_vardi"] = (df["ndvi_oncesi"] >= BITKI_ESIK) & \
                        (df["arazi_kodu"] != SU_SINIFI)

    df["yanik"] = (df["dnbr"] >= YANIK_ESIK_DNBR) & df["bitki_vardi"] & df["gecerli_hucre"]

    # USGS yangin siddeti siniflari
    df["siddet_sinifi"] = pd.cut(
        df["dnbr"], [-np.inf, 0.10, 0.27, 0.44, 0.66, np.inf],
        labels=["yanmamis", "dusuk", "orta-dusuk", "orta-yuksek", "yuksek"])
    df.loc[~df["yanik"], "siddet_sinifi"] = "yanmamis"

    # --- HEDEF DEGISKEN ------------------------------------------------
    # iyilesme = (NDVI_t - NDVI_sonrasi) / (NDVI_oncesi - NDVI_sonrasi)
    #
    # ETIKET HER YANIK HUCREDE GUVENILIR DEGIL. Uretilen veriden olculdu:
    #   payda 0.10-0.15 -> etiket std 0.53  (kullanilamaz)
    #   payda 0.40-1.00 -> etiket std 0.11  (temiz)
    # Payda kucukken bolme islemi gurultuyu buyutuyor. Bu yuzden egitim seti
    # sadece siddeti "orta-dusuk ve ustu" VE NDVI dususu belirgin hucrelerle
    # sinirlandirilir. Diger yanik hucreler tabloda kalir ama etiketsizdir.
    payda = df["ndvi_oncesi"] - df["ndvi_sonrasi"]
    df["ndvi_dusus"] = payda
    df["etiket_gecerli"] = (df["yanik"] &
                            (df["dnbr"] >= ETIKET_ESIK_DNBR) &
                            (payda >= ETIKET_ESIK_PAYDA))
    for y in ("yil1", "yil2"):
        oran = (df[f"ndvi_{y}"] - df["ndvi_sonrasi"]) / payda
        df[f"iyilesme_{y}"] = np.where(df["etiket_gecerli"], oran.clip(-1, 2), np.nan)

    print(f"  -> yanik: {int(df['yanik'].sum()):,} hucre | "
          f"guvenilir etiket: {int(df['etiket_gecerli'].sum()):,} hucre")

    meta["yanik_hucre"] = int(df["yanik"].sum())
    meta["etiketli_hucre"] = int(df["etiket_gecerli"].sum())

    sekil = (grid.h, grid.w)
    iy_harita = np.where(df["etiket_gecerli"].values.reshape(sekil),
                         df["iyilesme_yil2"].values.reshape(sekil), np.nan)
    dnbr_harita = np.where(df["bitki_vardi"].values.reshape(sekil), dnbr, np.nan)
    return df, meta, grid, {"dnbr": dnbr_harita, "ndvi_oncesi": ndvi["oncesi"],
                            "egim": egim, "agac": agac_orani, "iyilesme": iy_harita}


# ============================================================================
# ANA
# ============================================================================
def main():
    print("=" * 74)
    print("FireRecover AI - VERI PIPELINE (grid tabanli)")
    print("=" * 74)
    t_bas = time.time()

    tablolar, metalar, gorseller = [], {}, {}
    for kod, cfg in BOLGELER.items():
        try:
            df, meta, grid, katman = bolge_isle(kod, cfg)
            tablolar.append(df)
            metalar[kod] = meta
            gorseller[kod] = (grid, katman, cfg["ad"])
        except Exception as e:
            print(f"  !! {kod} basarisiz: {type(e).__name__}: {e}")

    if not tablolar:
        print("Hicbir bolge islenemedi.")
        return

    tam = pd.concat(tablolar, ignore_index=True)
    tam.to_csv("firerecover_grid.csv", index=False, encoding="utf-8-sig")

    # --- ozet ---------------------------------------------------------
    print("\n" + "=" * 74)
    print("OZET")
    print("=" * 74)
    egitim = tam[tam["etiket_gecerli"]]
    print(f"  Toplam hucre       : {len(tam):,}")
    print(f"  Gecerli hucre      : {int(tam['gecerli_hucre'].sum()):,}")
    print(f"  Yanik hucre        : {int(tam['yanik'].sum()):,}")
    print(f"  EGITIM SETI        : {len(egitim):,}  (guvenilir etiketli)")
    print(f"  Cografi grup       : {tam['bolge_kodu'].nunique()}  -> GroupKFold {tam['bolge_kodu'].nunique()} fold")
    print(f"  Sure               : {time.time()-t_bas:.0f} sn")

    print("\n  Bolge bazinda:")
    ozet = tam.groupby("bolge_kodu").agg(
        hucre=("hucre_id", "size"), yanik=("yanik", "sum"),
        egitim=("etiket_gecerli", "sum"), dnbr_maks=("dnbr", "max"))
    for k, r in ozet.iterrows():
        print(f"    {k:<16} {int(r['hucre']):>6,} hucre | {int(r['yanik']):>5,} yanik | "
              f"{int(r['egitim']):>5,} etiketli | dNBR maks {r['dnbr_maks']:.2f}")

    yanikdf = tam[tam["yanik"]]
    print("\n  Yangin siddeti dagilimi (yanik hucreler):")
    for k, v in yanikdf["siddet_sinifi"].value_counts().items():
        if str(k) == "yanmamis":
            continue
        print(f"    {str(k):<12} {v:>7,}  (%{v/len(yanikdf)*100:.1f})")

    print("\n  Yanik alanda arazi tipi:")
    for k, v in yanikdf["arazi_tipi"].value_counts().head(5).items():
        print(f"    {str(k):<14} {v:>7,}  (%{v/len(yanikdf)*100:.1f})")

    print("\n  Hedef degisken (2 yillik iyilesme orani, egitim seti):")
    iy = egitim["iyilesme_yil2"].dropna()
    print(f"    ortalama={iy.mean():.3f}  medyan={iy.median():.3f}  "
          f"std={iy.std():.3f}  min={iy.min():.2f}  maks={iy.max():.2f}")

    print("\n  Ornek satir:")
    ornek = egitim.iloc[len(egitim) // 2]
    for k in ["hucre_id", "lat", "lon", "yukselti_m", "egim_derece", "arazi_tipi",
              "yerlesim_mesafe_km", "su_mesafe_km", "yol_mesafe_km", "dnbr",
              "siddet_sinifi", "ndvi_oncesi", "ndvi_sonrasi", "ndvi_yil2",
              "iyilesme_yil2"]:
        v = ornek[k]
        print(f"    {k:<20} = {v if not isinstance(v, float) else round(v, 4)}")

    with open("firerecover_ozet.json", "w", encoding="utf-8") as f:
        json.dump({"hucre_m": HUCRE_M, "toplam_hucre": len(tam),
                   "gecerli_hucre": int(tam["gecerli_hucre"].sum()),
                   "yanik_hucre": int(tam["yanik"].sum()),
                   "egitim_seti": int(tam["etiket_gecerli"].sum()),
                   "cografi_grup": int(tam["bolge_kodu"].nunique()),
                   "esikler": {"yanik_dnbr": YANIK_ESIK_DNBR, "bitki_ndvi": BITKI_ESIK,
                               "etiket_dnbr": ETIKET_ESIK_DNBR,
                               "etiket_payda": ETIKET_ESIK_PAYDA},
                   "hedef_istatistik": {
                       "ortalama": float(tam.loc[tam["etiket_gecerli"], "iyilesme_yil2"].mean()),
                       "medyan": float(tam.loc[tam["etiket_gecerli"], "iyilesme_yil2"].median()),
                       "std": float(tam.loc[tam["etiket_gecerli"], "iyilesme_yil2"].std())},
                   "bolgeler": metalar}, f, ensure_ascii=False, indent=2, default=str)

    haritalar(gorseller)
    print("\n  Dosyalar: firerecover_grid.csv, firerecover_haritalar.png, "
          "firerecover_ozet.json")
    return tam


def haritalar(gorseller):
    """Sunum icin katman haritalari."""
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt

    n = len(gorseller)
    if n == 0:
        return
    fig, axes = plt.subplots(n, 5, figsize=(21, 4.4 * n), squeeze=False)
    duzen = [("dnbr", "Yangin siddeti (dNBR)", "inferno_r", None),
             ("ndvi_oncesi", "Yangin oncesi NDVI", "YlGn", (0, .8)),
             ("egim", "Egim (derece)", "cividis", None),
             ("agac", "Agac orani", "Greens", (0, 1)),
             ("iyilesme", "2 yillik iyilesme orani", "RdYlGn", (-.2, 1))]

    for r, (kod, (grid, kat, ad)) in enumerate(gorseller.items()):
        for c, (anahtar, baslik, cmap, lim) in enumerate(duzen):
            ax = axes[r][c]
            a = kat[anahtar]
            kw = {"cmap": cmap}
            if lim:
                kw["vmin"], kw["vmax"] = lim
            im = ax.imshow(a, **kw)
            ax.set_title(f"{ad}\n{baslik}" if c == 0 else baslik, fontsize=10)
            ax.set_xticks([]); ax.set_yticks([])
            fig.colorbar(im, ax=ax, fraction=0.046, shrink=.85)

    fig.suptitle("FireRecover AI - ozellik katmanlari (500 m grid)", fontsize=14)
    fig.tight_layout()
    fig.savefig("firerecover_haritalar.png", dpi=115)


if __name__ == "__main__":
    main()
