import type { ObservationStatus } from "../types";
import { observations as seed } from "./demoData";

/**
 * Volunteer observation store.
 *
 * The loop this closes: a volunteer submits an observation on the Community
 * screen -> it lands in the Organisation review queue -> the organisation
 * accepts it or asks for clarification -> the volunteer sees what happened.
 * Before this, the form collected nothing at all and the loop had no step 5.
 *
 * There is NO BACKEND for observations yet, so records live in this browser
 * only (localStorage). That is stated on screen; nothing is claimed to be
 * submitted anywhere.
 *
 * Deliberately mirrors the shape of createFireService(mode: "mock" | "http")
 * so swapping to real endpoints later changes THIS FILE ONLY — the screens
 * keep calling the same four methods. Endpoint contract:
 * docs/topluluk_veri_sozlesmesi.md
 */

const ANAHTAR = "regreen.observations.v1";
const TAKMA_AD_ANAHTARI = "regreen.volunteer-alias.v1";

export interface FieldObservation {
  id: string;
  fireId: string;
  /** Which activity it was recorded during; null if none was joined. */
  activityId: string | null;
  submittedBy: string;
  /** ISO timestamp; demo records carry their original display date instead. */
  submittedAt: string | null;
  displayDate: string;
  location: string;
  /** File name only. The image never leaves the browser and is not stored. */
  photoName: string | null;
  answers: string[];
  note?: string;
  status: ObservationStatus;
  reviewedAt?: string;
  /** "demo" = seeded sample · "local" = submitted in this browser. */
  origin: "demo" | "local";
}

export interface ObservationInput {
  fireId: string;
  activityId: string | null;
  location: string;
  photoName: string | null;
  answers: string[];
  note?: string;
}

/* ------------------------------------------------------------- storage */

function guvenliOku(anahtar: string): string | null {
  // Gizli sekmede / site verisi kapaliyken localStorage ERISIMI BILE atar.
  try {
    return window.localStorage.getItem(anahtar);
  } catch {
    return null;
  }
}

function guvenliYaz(anahtar: string, deger: string) {
  try {
    window.localStorage.setItem(anahtar, deger);
    return true;
  } catch {
    return false;
  }
}

/** Depolama calismiyorsa (gizli sekme) en azindan oturum boyunca tutulsun. */
let bellekYedegi: FieldObservation[] | null = null;
let depolamaCalisiyor = true;

function yerelleriOku(): FieldObservation[] {
  if (bellekYedegi) return bellekYedegi;
  const ham = guvenliOku(ANAHTAR);
  if (!ham) return [];
  try {
    const cozulmus = JSON.parse(ham);
    return Array.isArray(cozulmus) ? (cozulmus as FieldObservation[]) : [];
  } catch {
    return [];
  }
}

function yerelleriYaz(kayitlar: FieldObservation[]) {
  bellekYedegi = kayitlar;
  depolamaCalisiyor = guvenliYaz(ANAHTAR, JSON.stringify(kayitlar));
  bildir();
}

/** Hesap yok; tarayici basina sabit bir takma ad uretiliyor. */
function takmaAd(): string {
  const mevcut = guvenliOku(TAKMA_AD_ANAHTARI);
  if (mevcut) return mevcut;
  const yeni = `Volunteer V-${String(Math.floor(Math.random() * 900) + 100)}`;
  guvenliYaz(TAKMA_AD_ANAHTARI, yeni);
  return yeni;
}

/* --------------------------------------------------------- seed kayitlar */

const demoKayitlar: FieldObservation[] = seed.map((o) => ({
  id: o.id,
  fireId: o.fireId,
  activityId: null,
  submittedBy: o.submittedBy,
  submittedAt: null,
  displayDate: o.date,
  location: o.location,
  photoName: null,
  answers: o.answers,
  note: o.note,
  status: o.status,
  origin: "demo",
}));

/* ------------------------------------------------------------ abonelik */

const dinleyiciler = new Set<() => void>();

function bildir() {
  anlikGoruntu = null;
  dinleyiciler.forEach((f) => f());
}

if (typeof window !== "undefined") {
  // Baska sekmede gonderilirse bu sekme de guncellensin.
  window.addEventListener("storage", (e) => {
    if (e.key === ANAHTAR) {
      bellekYedegi = null;
      bildir();
    }
  });
}

// useSyncExternalStore her cagrida AYNI referansi gormek zorunda, yoksa
// sonsuz render dongusune giriyor. O yuzden anlik goruntu onbellekleniyor.
let anlikGoruntu: FieldObservation[] | null = null;

/* -------------------------------------------------------------- servis */

export const observationService = {
  /** En yeni gonderim en ustte; demo kayitlar altta. */
  list(): FieldObservation[] {
    if (!anlikGoruntu) {
      const yerel = [...yerelleriOku()].sort(
        (a, b) => (b.submittedAt ?? "").localeCompare(a.submittedAt ?? ""),
      );
      anlikGoruntu = [...yerel, ...demoKayitlar];
    }
    return anlikGoruntu;
  },

  submit(girdi: ObservationInput): FieldObservation {
    const simdi = new Date();
    const kayit: FieldObservation = {
      id: `obs-local-${simdi.getTime()}`,
      fireId: girdi.fireId,
      activityId: girdi.activityId,
      submittedBy: takmaAd(),
      submittedAt: simdi.toISOString(),
      displayDate: simdi.toLocaleDateString("en-GB", { day: "numeric", month: "short" }),
      location: girdi.location,
      photoName: girdi.photoName,
      answers: girdi.answers,
      note: girdi.note?.trim() || undefined,
      status: "Pending",
      origin: "local",
    };
    yerelleriYaz([kayit, ...yerelleriOku()]);
    return kayit;
  },

  /** Kurum karari. Demo kayitlar degistirilemez — onlar sabit ornek. */
  review(id: string, status: ObservationStatus): boolean {
    const yerel = yerelleriOku();
    const i = yerel.findIndex((o) => o.id === id);
    if (i === -1) return false;
    const guncel = [...yerel];
    guncel[i] = { ...guncel[i]!, status, reviewedAt: new Date().toISOString() };
    yerelleriYaz(guncel);
    return true;
  },

  /** Bu tarayicidaki gonderimleri siler; demo kayitlar kalir. */
  clearLocal() {
    yerelleriYaz([]);
  },

  /** Bu tarayicida gonderilmis kayitlar. */
  mine(): FieldObservation[] {
    return this.list().filter((o) => o.origin === "local");
  },

  storageWorks(): boolean {
    return depolamaCalisiyor;
  },

  subscribe(dinleyici: () => void): () => void {
    dinleyiciler.add(dinleyici);
    return () => dinleyiciler.delete(dinleyici);
  },
};

export type ObservationService = typeof observationService;
