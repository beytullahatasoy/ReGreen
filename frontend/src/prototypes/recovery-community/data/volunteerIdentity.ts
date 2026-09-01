import { ApiError, communityService } from "../../../services";
import type { Volunteer } from "../../../types/community";

/**
 * There is no login. The first time this browser performs a volunteer action
 * (join an activity, submit an observation), the server mints a volunteer row
 * and this module remembers its id/alias in localStorage — the same identity
 * is reused on every later visit from this browser.
 */
const ANAHTAR = "regreen.volunteer.v1";

let bellek: Volunteer | null = null;
let bellekDogrulandi = false;
let kayitIstegi: Promise<Volunteer> | null = null;

function oku(): Volunteer | null {
  if (bellek) return bellek;
  try {
    const ham = window.localStorage.getItem(ANAHTAR);
    if (!ham) return null;
    bellek = JSON.parse(ham) as Volunteer;
    return bellek;
  } catch {
    return null;
  }
}

function yaz(volunteer: Volunteer) {
  bellek = volunteer;
  bellekDogrulandi = true;
  try {
    window.localStorage.setItem(ANAHTAR, JSON.stringify(volunteer));
  } catch {
    // Gizli sekme / site verisi kapalı: kimlik yalnızca bu oturumda kalır.
  }
}

function temizle() {
  bellek = null;
  bellekDogrulandi = false;
  try {
    window.localStorage.removeItem(ANAHTAR);
  } catch {
    // Depolama kullanılamıyorsa bellek kaydını temizlemek yeterli.
  }
}

function yeniGonulluOlustur(): Promise<Volunteer> {
  return communityService.createVolunteer()
    .then((volunteer) => { yaz(volunteer); return volunteer; });
}

/** Zaten kayıtlıysa onu döner; değilse sunucuda bir kayıt açar. */
export function ensureVolunteer(): Promise<Volunteer> {
  const mevcut = oku();
  if (mevcut && bellekDogrulandi) return Promise.resolve(mevcut);
  if (!kayitIstegi) {
    kayitIstegi = (mevcut
      ? communityService.getVolunteer(mevcut.id)
        .then((volunteer) => { yaz(volunteer); return volunteer; })
        .catch((reason: unknown) => {
          // Yerel DB sıfırlanmış veya kayıt silinmiş olabilir. Yalnızca kesin
          // 404 durumunda eski kimliği bırakıp yenisini üret; ağ hatalarında
          // sessizce ikinci bir gönüllü yaratma.
          if (reason instanceof ApiError && reason.problem.status === 404) {
            temizle();
            return yeniGonulluOlustur();
          }
          throw reason;
        })
      : yeniGonulluOlustur())
      .finally(() => { kayitIstegi = null; });
  }
  return kayitIstegi;
}

/** Senkron erişim: kayıt zaten yapılmışsa id, yapılmamışsa null. */
export function getCachedVolunteerId(): string | null {
  return oku()?.id ?? null;
}
