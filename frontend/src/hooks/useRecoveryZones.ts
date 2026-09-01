import { useEffect, useMemo, useState } from "react";
import { fireService } from "../services";
import type { FireNarrative, FireSummary, HukumKod } from "../types";

/**
 * Organisation ve Community ekranlari icin GERCEK veri katmani.
 *
 * Bu ekranlar daha once demoData.ts icindeki uydurma uc bolgeyle calisiyordu
 * ("Mugla — Zone 07" gibi). Oysa Expert ekraninin kullandigi 53 gercek yangin
 * ve her hucre icin uretilmis hukum ayni API'de duruyor. Burasi o veriyi
 * ekranlarin isine yarayacak sekle sokuyor.
 *
 * Etkinlik / gonullu / gozlem verisi HALA demo - onlar icin ne backend ne de
 * uretilmis veri var. Ekranlarda acikca isaretli kalacak.
 */

const HUCRE_HA = 6.25;

/** Kac yangin icin ayni anda ozet istegi acilsin. */
const ES_ZAMANLI = 8;

export interface RecoveryZone {
  fireId: string;
  ad: string;
  il: string;
  bolge: string;
  tarih: string;
  /** Yanan toplam alan. */
  alanHa: number;
  hucre: number;
  /** Tahmin uretilen hucre sayisi. */
  tahminliHucre: number;
  /** EROZYON_ONCE + DIKIM_ADAYI — dogrudan mudahale adayi kare sayisi. */
  mudahaleHucre: number;
  /** Ayni sayinin hektar karsiligi. */
  mudahaleHa: number;
  /** Once erozyon kontrolu gereken kare sayisi. */
  erozyonHucre: number;
  /** Oncelik sirasina gore degerlendirilecek kare sayisi. */
  siradaHucre: number;
  /** Kendi toparlanmasi beklenen kare sayisi (IZLE + GENCLESME_IZLE). */
  izlemeHucre: number;
  /** Agaclandirma kapsami disindaki kare sayisi. */
  kapsamDisiHucre: number;
  /** Modelin bu bolgedeki tahmin guveni. */
  guven: "yuksek" | "orta" | "dusuk";
  bolgedekiReferans: number;
  /** Yangin ozeti paragrafi — hukum katmanindan geliyor. */
  paragraf: string;
  profil: FireNarrative["profil"] | null;
  /** Siralama olcutu: dogrudan mudahale gereken kare sayisi. */
  aciliyet: number;
  /** Ozeti alinamadi — kart yalnizca liste bilgisiyle gosterilir. */
  ozetEksik: boolean;
}

function sayim(dagilim: Partial<Record<HukumKod, number>> | undefined, kod: HukumKod) {
  return dagilim?.[kod] ?? 0;
}

function guvenSeviyesi(ham: string | undefined): RecoveryZone["guven"] {
  return ham === "yuksek" || ham === "orta" ? ham : "dusuk";
}

function zoneKur(fire: FireSummary, ozet: FireNarrative | null): RecoveryZone {
  const d = ozet?.sayi_blogu?.hukum_dagilimi;
  const b = ozet?.sayi_blogu?.buyukluk;
  const g = ozet?.sayi_blogu?.guven;

  const erozyon = sayim(d, "EROZYON_ONCE");
  const dikim = sayim(d, "DIKIM_ADAYI");
  const mudahale = erozyon + dikim;

  return {
    fireId: fire.fire_id,
    ad: `${fire.province} · ${fire.fire_id}`,
    il: fire.province,
    bolge: fire.region,
    tarih: fire.fire_date,
    alanHa: fire.burned_area_ha,
    hucre: b?.hucre ?? fire.cell_count,
    tahminliHucre: b?.tahminli_hucre ?? 0,
    mudahaleHucre: mudahale,
    mudahaleHa: mudahale * HUCRE_HA,
    erozyonHucre: erozyon,
    siradaHucre: sayim(d, "ONCELIGE_GORE"),
    izlemeHucre: sayim(d, "IZLE") + sayim(d, "GENCLESME_IZLE"),
    kapsamDisiHucre: sayim(d, "KAPSAM_DISI"),
    guven: guvenSeviyesi(g?.seviye),
    bolgedekiReferans: g?.bolgedeki_referans_yangini ?? 0,
    paragraf: ozet?.paragraf ?? "",
    profil: ozet?.profil ?? null,
    // Siralama dogrudan mudahale kare sayisi. Erozyona ek agirlik
    // VERILMIYOR: kullanicinin ekranda goremedigi bir olcute gore
    // siralamak, listeyi aciklanamaz hale getiriyordu.
    aciliyet: mudahale,
    ozetEksik: ozet === null,
  };
}

/** Istekleri sinirli es zamanlilikla calistirir; 53 istegi ayni anda acmamak icin. */
async function sirayla<T, R>(girdi: T[], limit: number, is: (x: T) => Promise<R>): Promise<R[]> {
  const sonuc = new Array<R>(girdi.length);
  let sonraki = 0;
  const isci = async () => {
    while (sonraki < girdi.length) {
      const i = sonraki++;
      sonuc[i] = await is(girdi[i]!);
    }
  };
  await Promise.all(Array.from({ length: Math.min(limit, girdi.length) }, isci));
  return sonuc;
}

/** Modul kapsaminda onbellek: ekranlar arasi gecerken yeniden yuklenmesin. */
let onbellek: RecoveryZone[] | null = null;

export function temizleRecoveryZoneOnbellegi() {
  onbellek = null;
}

export interface RecoveryZonesState {
  zones: RecoveryZone[];
  yukleniyor: boolean;
  hata: string | null;
  /** Ozeti alinamayan yangin sayisi — kismi basarisizlik gorunur olsun. */
  eksikOzet: number;
}

export function useRecoveryZones(): RecoveryZonesState {
  const [zones, setZones] = useState<RecoveryZone[]>(onbellek ?? []);
  const [yukleniyor, setYukleniyor] = useState(onbellek === null);
  const [hata, setHata] = useState<string | null>(null);

  useEffect(() => {
    if (onbellek) return;
    let aktif = true;

    (async () => {
      try {
        const fires = await fireService.getFires();
        if (!aktif) return;

        // Ozetler tek tek geliyor. Biri patlarsa digerleri devam etsin:
        // o yangin listede kalir, yalnizca hukum sayimlari bos gorunur.
        const ozetler = await sirayla(fires, ES_ZAMANLI, async (f) => {
          try {
            return await fireService.getFireNarrative(f.fire_id);
          } catch {
            return null;
          }
        });
        if (!aktif) return;

        const kurulan = fires.map((f, i) => zoneKur(f, ozetler[i] ?? null));
        onbellek = kurulan;
        setZones(kurulan);
      } catch (reason: unknown) {
        if (!aktif) return;
        setHata(reason instanceof Error ? reason.message : "Recovery Zone verisi yüklenemedi.");
      } finally {
        if (aktif) setYukleniyor(false);
      }
    })();

    return () => { aktif = false; };
  }, []);

  const eksikOzet = useMemo(() => zones.filter((z) => z.ozetEksik).length, [zones]);

  return { zones, yukleniyor, hata, eksikOzet };
}

/** Mudahale yuku en agir olanlar once. */
export function aciliyeteGore(zones: RecoveryZone[]): RecoveryZone[] {
  return [...zones].sort((a, b) => b.aciliyet - a.aciliyet);
}

/** Ekranlarda tekrar eden toplamlar. */
export function toplamlar(zones: RecoveryZone[]) {
  return zones.reduce(
    (t, z) => ({
      mudahaleHa: t.mudahaleHa + z.mudahaleHa,
      mudahaleHucre: t.mudahaleHucre + z.mudahaleHucre,
      erozyonHucre: t.erozyonHucre + z.erozyonHucre,
      siradaHucre: t.siradaHucre + z.siradaHucre,
      dusukGuven: t.dusukGuven + (z.guven === "dusuk" ? 1 : 0),
    }),
    { mudahaleHa: 0, mudahaleHucre: 0, erozyonHucre: 0, siradaHucre: 0, dusukGuven: 0 },
  );
}
