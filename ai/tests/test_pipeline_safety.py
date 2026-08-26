# -*- coding: utf-8 -*-
"""AI veri yolu ve teslim paketi guvenligi regresyon testleri."""
import pathlib
import sys
import tempfile
import unittest
from unittest import mock


AI_KOK = pathlib.Path(__file__).resolve().parents[1]
if str(AI_KOK) not in sys.path:
    sys.path.insert(0, str(AI_KOK))

import yollar


class YolGuvenligiTests(unittest.TestCase):
    def test_hazirla_tum_veri_dizinlerini_olusturur(self):
        with tempfile.TemporaryDirectory() as tmp:
            veri = pathlib.Path(tmp) / "data"
            degerler = {
                "VERI": veri,
                "GIRDI": veri / "ham",
                "ARA": veri / "ara",
                "CIKTI": veri / "cikti",
                "ONBELLEK": veri / "onbellek",
            }
            with mock.patch.multiple(yollar, **degerler):
                yollar.hazirla()
                for dizin in degerler.values():
                    self.assertTrue(dizin.is_dir(), dizin)

    def test_eksik_girdi_mevcut_pakete_dokunmaz(self):
        with tempfile.TemporaryDirectory() as tmp:
            kok = pathlib.Path(tmp)
            mevcut = kok / "backend-data"
            mevcut.mkdir()
            (mevcut / "koru.txt").write_text("eski", encoding="utf-8")

            with self.assertRaises(SystemExit):
                yollar.gerekli(kok / "eksik.csv")

            self.assertEqual((mevcut / "koru.txt").read_text(encoding="utf-8"),
                             "eski")

    def test_basarili_degisim_yeniyi_koyar_ve_artik_birakmaz(self):
        with tempfile.TemporaryDirectory() as tmp:
            kok = pathlib.Path(tmp)
            mevcut = kok / "backend-data"
            yeni = kok / "backend-data.yeni"
            mevcut.mkdir(); yeni.mkdir()
            (mevcut / "surum.txt").write_text("eski", encoding="utf-8")
            (yeni / "surum.txt").write_text("yeni", encoding="utf-8")

            yollar.dizini_guvenle_degistir(yeni, mevcut)

            self.assertEqual((mevcut / "surum.txt").read_text(encoding="utf-8"),
                             "yeni")
            self.assertFalse(yeni.exists())
            self.assertFalse((kok / "backend-data.eski").exists())

    def test_yeni_rename_hatasi_eski_paketi_geri_yukler(self):
        with tempfile.TemporaryDirectory() as tmp:
            kok = pathlib.Path(tmp)
            mevcut = kok / "backend-data"
            yeni = kok / "backend-data.yeni"
            mevcut.mkdir(); yeni.mkdir()
            (mevcut / "surum.txt").write_text("eski", encoding="utf-8")
            (yeni / "surum.txt").write_text("yeni", encoding="utf-8")
            gercek_rename = pathlib.Path.rename

            def kontrollu_rename(kaynak, hedef):
                if pathlib.Path(kaynak).resolve() == yeni.resolve():
                    raise OSError("test icin rename hatasi")
                return gercek_rename(kaynak, hedef)

            with mock.patch.object(pathlib.Path, "rename", new=kontrollu_rename):
                with self.assertRaises(OSError):
                    yollar.dizini_guvenle_degistir(yeni, mevcut)

            self.assertEqual((mevcut / "surum.txt").read_text(encoding="utf-8"),
                             "eski")
            self.assertTrue(yeni.exists())
            self.assertFalse((kok / "backend-data.eski").exists())

    def test_yalniz_kalan_yedek_once_geri_yuklenir(self):
        with tempfile.TemporaryDirectory() as tmp:
            kok = pathlib.Path(tmp)
            mevcut = kok / "backend-data"
            yedek = kok / "backend-data.eski"
            yeni = kok / "backend-data.yeni"
            yedek.mkdir(); yeni.mkdir()
            (yedek / "surum.txt").write_text("eski", encoding="utf-8")
            (yeni / "surum.txt").write_text("yeni", encoding="utf-8")

            yollar.dizini_guvenle_degistir(yeni, mevcut)

            self.assertEqual((mevcut / "surum.txt").read_text(encoding="utf-8"),
                             "yeni")
            self.assertFalse(yedek.exists())


if __name__ == "__main__":
    unittest.main()
