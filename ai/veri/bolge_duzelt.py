# -*- coding: utf-8 -*-
"""
Bolge etiketlerinin duzeltmesi
==============================

SORUN: bolgeler kaba dikdortgen kurallarla atanıyordu ve "Ic Anadolu" da
geri kalan her seyi toplayan bir cop kutusuydu. Sonuc yanlisti:

    Adana / Karaisali (37.63, 35.63)  -> "Ic Anadolu"   (dogrusu Akdeniz)
    Ankara / Kizilcahamam             -> "Karadeniz"    (dogrusu Ic Anadolu)
    Denizli / Honaz                   -> "Ic Anadolu"   (dogrusu Ege)

Akdeniz kuralı "lat < 37.6" idi; Adana'daki yangin 37.63'te, yani sinirin
3 km kuzeyinde kaldigi icin Ic Anadolu sayilmisti.

COZUM: her yanginin merkezi icin Nominatim'den GERCEK ILI soruluyor, il ->
bolge eslemesi (Turkiye'nin 7 cografi bolgesi) uygulaniyor. 55 yangin, tek
seferlik. Sonuc onbellege yaziliyor, tekrar sorgu yapilmıyor.

NOT: bolge modele GIRDI DEGILDIR. Yalnizca raporlama ve harita etiketi.
Bu duzeltme egitim verisini degistirmez, sadece dogru anlatmamizi saglar.

Calistirma:  python bolge_duzelt.py
"""

import sys
import json
import pathlib
import time

import pandas as pd
import requests

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent.parent))
import yollar
yollar.yol_ekle()

KOK = yollar.ARA          # ortak veri koku - ai/yollar.py
ONBELLEK = KOK / "onbellek" / "il_bolge.json"

UA = {"User-Agent": "FireRecoverAI/0.3 (Huawei ICT Competition student project)"}

# Turkiye'nin 7 cografi bolgesi, il bazinda
IL_BOLGE = {
    "Akdeniz": ["Adana", "Antalya", "Burdur", "Hatay", "Isparta",
                "Kahramanmaraş", "Mersin", "Osmaniye"],
    "Ege": ["Afyonkarahisar", "Aydın", "Denizli", "İzmir", "Kütahya",
            "Manisa", "Muğla", "Uşak"],
    "Marmara": ["Balıkesir", "Bilecik", "Bursa", "Çanakkale", "Edirne",
                "İstanbul", "Kırklareli", "Kocaeli", "Sakarya", "Tekirdağ",
                "Yalova"],
    "Karadeniz": ["Amasya", "Artvin", "Bartın", "Bayburt", "Bolu", "Çorum",
                  "Düzce", "Giresun", "Gümüşhane", "Karabük", "Kastamonu",
                  "Ordu", "Rize", "Samsun", "Sinop", "Tokat", "Trabzon",
                  "Zonguldak"],
    "Ic Anadolu": ["Aksaray", "Ankara", "Çankırı", "Eskişehir", "Karaman",
                   "Kayseri", "Kırıkkale", "Kırşehir", "Konya", "Nevşehir",
                   "Niğde", "Sivas", "Yozgat"],
    "Dogu Anadolu": ["Ağrı", "Ardahan", "Bingöl", "Bitlis", "Elazığ",
                     "Erzincan", "Erzurum", "Hakkâri", "Iğdır", "Kars",
                     "Malatya", "Muş", "Tunceli", "Van"],
    "Guneydogu": ["Adıyaman", "Batman", "Diyarbakır", "Gaziantep", "Kilis",
                  "Mardin", "Siirt", "Şanlıurfa", "Şırnak"],
}
IL2BOLGE = {il: b for b, iller in IL_BOLGE.items() for il in iller}


def il_bul(lat, lon, onbellek):
    """Nominatim ters cografi kodlama ile il adi."""
    anahtar = "{:.4f},{:.4f}".format(lat, lon)
    if anahtar in onbellek:
        return onbellek[anahtar]
    try:
        r = requests.get(
            "https://nominatim.openstreetmap.org/reverse",
            params={"lat": lat, "lon": lon, "format": "json",
                    "zoom": 8, "accept-language": "tr"},
            headers=UA, timeout=30)
        a = r.json().get("address", {})
        il = (a.get("province") or a.get("state") or a.get("city") or "")
        il = il.replace(" ili", "").replace(" İli", "").strip()
    except Exception as e:
        print("    sorgu hatasi: {}".format(e))
        il = ""
    onbellek[anahtar] = il
    time.sleep(1.1)          # Nominatim kullanim kurali: saniyede 1 istek
    return il


def main():
    yollar.gerekli(KOK / "yanginlar.json")
    yanginlar = json.loads((KOK / "yanginlar.json").read_text(encoding="utf-8"))
    onbellek = (json.loads(ONBELLEK.read_text(encoding="utf-8"))
                if ONBELLEK.exists() else {})

    print("=" * 78)
    print("BOLGE ETIKETI DUZELTMESI  ·  {} yangin".format(len(yanginlar)))
    print("=" * 78)

    degisen = []
    for i, y in enumerate(yanginlar, 1):
        il = il_bul(y["lat"], y["lon"], onbellek)
        yeni = IL2BOLGE.get(il, "Bilinmiyor")
        eski = y.get("bolge", "")
        y["il"] = il
        y["bolge_eski"] = eski
        y["bolge"] = yeni
        isaret = ""
        if yeni != eski:
            degisen.append((y["id"], eski, yeni, il))
            isaret = "  <-- DEGISTI"
        print("  [{:>2}/{}] {:<16}{:<16}{:<14}{}{}".format(
            i, len(yanginlar), y["id"], il or "?", yeni, eski, isaret))

    ONBELLEK.write_text(json.dumps(onbellek, ensure_ascii=False, indent=2),
                        encoding="utf-8")

    print("\n" + "-" * 78)
    print("DEGISENLER: {} / {}".format(len(degisen), len(yanginlar)))
    print("-" * 78)
    for kid, eski, yeni, il in degisen:
        print("  {:<16}{:<14} -> {:<14}({})".format(kid, eski, yeni, il))

    # ID'ler bolge kisaltmasi iceriyor; ID DEGISTIRMIYORUZ.
    # Butun cikti dosyalari ve parca dosyalari yangin_id ile bagli.
    print("\n  NOT: yangin_id'ler DEGISTIRILMEDI (AKD_/IAN_ onekleri kaldi).")
    print("  Kimlik degistirmek butun parca dosyalarini ve turkiye_grid.csv'yi")
    print("  kirardi. Dogru bolge artik 'bolge' ve 'il' alanlarinda.")

    (KOK / "yanginlar.json").write_text(
        json.dumps(yanginlar, ensure_ascii=False, indent=2), encoding="utf-8")

    # --- turkiye_grid.csv ve egitim_seti.csv'deki bolge sutununu tazele ---
    harita = {y["id"]: y["bolge"] for y in yanginlar}
    ilharita = {y["id"]: y["il"] for y in yanginlar}
    for ad in ("turkiye_grid.csv", "egitim_seti.csv"):
        yol = KOK / ad
        if not yol.exists():
            continue
        df = pd.read_csv(yol, low_memory=False,
                         encoding="utf-8-sig" if ad == "egitim_seti.csv" else "utf-8")
        df["bolge"] = df["yangin_id"].map(harita).fillna(df["bolge"])
        df["il"] = df["yangin_id"].map(ilharita)
        df.to_csv(yol, index=False, encoding="utf-8-sig")
        print("  -> {} guncellendi".format(ad))

    print("\n" + "=" * 78)
    print("YENI BOLGE DAGILIMI")
    print("=" * 78)
    d = pd.DataFrame(yanginlar)
    print(d.groupby("bolge").agg(yangin=("id", "size"),
                                 alan_ha=("alan_ha", "sum")).to_string())
    ozet = KOK / "yangin_ozeti.csv"
    if ozet.exists():
        o = pd.read_csv(ozet)
        o["bolge"] = o["yangin_id"].map(harita).fillna(o["bolge"])
        o["il"] = o["yangin_id"].map(ilharita)
        o.to_csv(ozet, index=False, encoding="utf-8-sig")
        print("\nEGITIME UYGUN OLANLAR:")
        print(o[o["egitime_uygun"]].groupby("bolge").size().to_string())


if __name__ == "__main__":
    main()
