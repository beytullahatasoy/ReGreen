# -*- coding: utf-8 -*-
"""Butun AI scriptleri icin ortak yol tanimlari.

SORUN: her script kendi bulundugu klasoru veri koku sayiyordu. Boru hatti
ciktiyi bir yere yaziyor, temizlik scripti baska yerde ariyor, model scripti
calisma dizinine bakiyor, teslim scripti bir baskasina. Zincir kopuyordu.

COZUM: tek bir veri koku. Butun scriptler buradan okur, buraya yazar.
Konum degistirmek isteyen REGREEN_AI_DATA ortam degiskenini kullanir.

    from yollar import VERI, GIRDI, CIKTI, yol_ekle

Not: bu modul ai/ kokunde. Alt klasorlerden (veri/, model/, teslim/...)
calisan scriptler once sys.path'e ai/ kokunu eklemeli - yol_ekle() bunu
yapiyor, dosyanin en ustunde cagrilmali.
"""
import os
import pathlib
import sys

# ai/ klasorunun kendisi
AI_KOK = pathlib.Path(__file__).resolve().parent

# repo koku (ReGreen/)
REPO_KOK = AI_KOK.parent

# Veri koku. Buyuk ara dosyalar (turkiye_grid.csv ~106 MB, parcalar_250m/)
# burada durur ve git'e GIRMEZ - .gitignore'da.
VERI = pathlib.Path(os.environ.get("REGREEN_AI_DATA", AI_KOK / "data")).resolve()

# Alt dizinler
GIRDI = VERI / "ham"          # boru hattinin urettigi ham izgara ve parcalar
ARA = VERI / "ara"            # temizlenmis grid, egitim seti
CIKTI = VERI / "cikti"        # model dosyalari, olcum sonuclari
ONBELLEK = VERI / "onbellek"  # uydu/yagis onbellegi

# Teslim paketi repoya girer - sample-data/backend-data/
TESLIM = REPO_KOK / "sample-data" / "backend-data"
TESLIM_FRONTEND = REPO_KOK / "sample-data" / "frontend-data"


def yol_ekle():
    """ai/ kokunu ve butun alt klasorlerini sys.path'e ekler.

    Neden gerekli: scriptler alt klasorlere bolundu (boru_hatti/, veri/,
    model/, teslim/) ama birbirlerini duz isimle import ediyorlar
    (`import oncelik`, `import firerecover_pipeline`). Python varsayilan
    olarak sadece CALISAN scriptin kendi klasorunu path'e koyar, kardes
    klasorleri koymaz.

    Her scriptin en ustunde, kardes importlardan ONCE cagrilmali.
    """
    for d in [AI_KOK] + [p for p in sorted(AI_KOK.iterdir())
                         if p.is_dir() and not p.name.startswith((".", "_"))
                         and p.name != "data"]:
        k = str(d)
        if k not in sys.path:
            sys.path.insert(0, k)
    return AI_KOK


def hazirla():
    """Veri dizinlerini olusturur. Zaten varsa dokunmaz."""
    for d in (VERI, GIRDI, ARA, CIKTI, ONBELLEK):
        d.mkdir(parents=True, exist_ok=True)
    return VERI


def gerekli(*yollar):
    """Girdi dosyalarini kontrol eder, eksikse ANLASILIR hata verir.

    Eskiden eksik dosya FileNotFoundError ile patliyordu ve kullanici
    dosyayi nereden bulacagini bilmiyordu.
    """
    eksik = [p for p in yollar if not pathlib.Path(p).exists()]
    if not eksik:
        return
    satir = ["Gerekli girdi dosyalari bulunamadi:", ""]
    for p in eksik:
        satir.append("   %s" % p)
    satir += [
        "",
        "Veri koku : %s" % VERI,
        "",
        "Bu dosyalar boru hatti ve temizlik asamalarinda uretilir:",
        "   ai/boru_hatti/yangin_kesif.py       -> yanginlar.json",
        "   ai/boru_hatti/pipeline_turkiye.py   -> turkiye_grid.csv, parcalar_250m/",
        "   ai/veri/veri_temizle.py             -> turkiye_grid.csv (temiz)",
        "   ai/veri/veri_son_hal.py             -> etiketler",
        "   ai/veri/egitim_seti.py              -> egitim_seti.csv",
        "",
        "Baska bir konumda duruyorlarsa:",
        "   set REGREEN_AI_DATA=D:\\regreen_veri     (Windows)",
        "   export REGREEN_AI_DATA=/veri/regreen     (Linux/macOS)",
    ]
    raise SystemExit("\n".join(satir))


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    print("AI kok    :", AI_KOK)
    print("repo kok  :", REPO_KOK)
    print("veri kok  :", VERI, "" if VERI.exists() else "  (henuz yok)")
    print("  ham     :", GIRDI)
    print("  ara     :", ARA)
    print("  cikti   :", CIKTI)
    print("teslim    :", TESLIM)
