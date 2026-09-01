import type { ActivityKind, FieldActivity, ObservationStatus } from "../../../types/community";

/**
 * Community ekranının dili.
 *
 * Bu ekrana gelen kişi karar vermeye değil KATILMAYA geliyor. Bu yüzden
 * burada hüküm kodu, hektar, hücre sayısı, tahmin güveni gibi hiçbir uzman
 * verisi geçmez — onlar Expert ve Organisation ekranlarının işi. Buradaki
 * her metin, ormanı hiç bilmeyen birinin ilk okuyuşta anlayacağı şekilde
 * yazılmıştır.
 */

/** Etkinlik türünün "ne yapacağım" karşılığı — sınıflandırma değil, eylem. */
export const NE_YAPILACAK: Record<ActivityKind, string> = {
  planting: "Plant saplings",
  cleanup: "Clear burnt debris",
  erosion_observation: "Walk and report on the soil",
  vegetation_monitoring: "Check what has grown back",
  field_assessment: "Join a field survey",
};

/** Yeni başlayan biri için uygun mu — "ilk kez geliyorum" korkusunu azaltır. */
export const YENI_BASLAYANA_UYGUN: Record<ActivityKind, boolean> = {
  planting: true,
  cleanup: false,
  erosion_observation: true,
  vegetation_monitoring: true,
  field_assessment: false,
};

const gunAy = new Intl.DateTimeFormat("en-GB", { weekday: "short", day: "numeric", month: "long" });
const ay = new Intl.DateTimeFormat("en-GB", { month: "short" });

/** "Sat 17 October" — tam tarih, kısaltma bulmacası değil. */
export function tamTarih(isoGun: string): string {
  return gunAy.format(yerelGun(isoGun));
}

export function ayKisa(isoGun: string): string {
  return ay.format(yerelGun(isoGun)).toUpperCase();
}

export function gunSayisi(isoGun: string): string {
  return String(yerelGun(isoGun).getDate());
}

/**
 * "2026-10-17" tarihini YEREL gün olarak okur.
 *
 * new Date("2026-10-17") bunu UTC gece yarısı sayar; UTC'nin gerisindeki bir
 * saat diliminde tarih bir gün geriye kayar ve kullanıcı yanlış güne gelir.
 */
function yerelGun(isoGun: string): Date {
  const [yil, ay_, gun] = isoGun.split("-").map(Number);
  return new Date(yil ?? 1970, (ay_ ?? 1) - 1, gun ?? 1);
}

/** Kalan kontenjan — sayı değil, karar verdiren cümle. */
export function kalanYer(activity: FieldActivity): { metin: string; acil: boolean } {
  const kalan = activity.capacity - activity.joined;
  if (kalan <= 0) return { metin: "Fully booked", acil: false };
  if (kalan <= 5) return { metin: `Only ${kalan} ${kalan === 1 ? "place" : "places"} left`, acil: true };
  return { metin: `${kalan} places left`, acil: false };
}

/** Gönüllünün kendi gözleminin durumu — kurum jargonu değil, ona söylenen şey. */
export const GOZLEM_DURUMU: Record<ObservationStatus, string> = {
  pending: "Waiting to be read",
  accepted: "Used as field evidence",
  needs_clarification: "They asked for more detail",
  rejected: "Not used this time",
};

/**
 * Gözlem formunun soruları.
 *
 * Gönüllü gördüğünü yazar, ne anlama geldiğini uzman yorumlar — o yüzden
 * sorular ölçüm değil gözlem soruyor ("toprak nemli mi", "kaç santim büyümüş"
 * değil). Cevap "Soru: değer" olarak saklanır ve kurumun kuyruğunda aynen
 * görünür.
 */
export const SORULAR = [
  {
    alan: "Is anything growing?",
    ipucu: "Look at the ground, not the tall trees.",
    secenekler: ["Nothing yet", "A few green patches", "Growing in places", "Covered in green"],
  },
  {
    alan: "Is soil washing away?",
    ipucu: "Channels cut by rain, bare soil sliding downhill.",
    secenekler: ["I can't tell", "Maybe", "Yes, clearly"],
  },
  {
    alan: "How does the ground feel?",
    ipucu: "Under your own feet where you are standing.",
    secenekler: ["Dry and hard", "Damp", "Loose underfoot", "Something else"],
  },
  {
    alan: "Any trouble getting there?",
    ipucu: "Only what would matter for the next group.",
    secenekler: ["None", "Path was blocked", "It felt unsafe", "Something else"],
  },
] as const;
