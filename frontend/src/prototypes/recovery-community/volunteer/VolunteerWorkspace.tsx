import { useEffect, useMemo, useRef, useState } from "react";
import { communityService } from "../../../services";
import type { FieldActivity } from "../../../types/community";
import { ensureVolunteer, getCachedVolunteerId } from "../data/volunteerIdentity";
import { useActivities } from "../data/useActivities";
import { useObservations } from "../data/useObservations";
import { Icon } from "../components/Icons";
import { WorkspaceShell } from "../components/WorkspaceShell";
import { ObservationForm } from "./ObservationForm";
import {
  GOZLEM_DURUMU, NE_YAPILACAK, YENI_BASLAYANA_UYGUN,
  ayKisa, gunSayisi, kalanYer, tamTarih,
} from "./volunteerText";

/**
 * Community — vatandaşın ekranı.
 *
 * <b>Buraya gelen kişi karar vermeye değil katılmaya geliyor.</b> Bu yüzden
 * ekranda tek bir uzman verisi yok: hektar, hüküm dağılımı, hücre sayısı,
 * tahmin güveni ve model paragrafı Expert ile Organisation ekranlarında
 * kalır. Bu dosya `useRecoveryZones`'u ve hüküm bileşenlerini BİLEREK hiç
 * import etmez — kural kodun kendisinde görünsün diye.
 *
 * Önceki sürüm bir "alan seçici" ile başlıyordu: 4.706 ha müdahale adayı,
 * hüküm çubuğu, "bu bölgede tahmin belirsizliği yüksek". Ormanı hiç
 * bilmeyen biri için bunların hiçbiri bir sonraki adımı göstermiyordu.
 *
 * Şimdi ekranın omurgası tek bir liste: <b>gidebileceğin günler.</b>
 *   1  Katılabileceğin etkinlikler   (gerçek /api/activities)
 *   2  Katıldıysan: ne getireceğin + gördüğünü anlatma formu
 *   3  Senin katkın               (gerçek gözlemlerin ve sonuçları)
 *
 * Alan bilgisi kartta yalnızca "nerede" olarak geçer (il + buluşma noktası);
 * ölçüm olarak değil.
 */
export function VolunteerWorkspace() {
  const [volunteerId, setVolunteerId] = useState<string | null>(getCachedVolunteerId());
  // Kimlik varsa sunucu her gun icin "bu kisi kayitli mi"yi de dondurur; sayfa
  // yenilendiginde katildigin gun yeniden "Count me in" gorunmesin diye.
  const { activities, yukleniyor, hata, yenile } = useActivities({ volunteer_id: volunteerId ?? undefined });
  const [il, setIl] = useState<string | null>(null);
  const [acilanId, setAcilanId] = useState<number | null>(null);
  const [katildiklarim, setKatildiklarim] = useState<Record<number, FieldActivity>>({});
  const [gonderimSayaci, setGonderimSayaci] = useState(0);

  // Katılıma açık olanlar önce; kapananlar listeyi tıkamasın.
  const acikOlanlar = useMemo(
    () => activities.filter((a) => a.status === "open" || a.status === "scheduled"),
    [activities],
  );

  const iller = useMemo(
    () => [...new Set(acikOlanlar.map((a) => a.province))].sort((a, b) => a.localeCompare(b, "tr")),
    [acikOlanlar],
  );

  const gosterilen = useMemo(
    () => (il === null ? acikOlanlar : acikOlanlar.filter((a) => a.province === il)),
    [acikOlanlar, il],
  );

  return (
    <WorkspaceShell mode="volunteer">
      <main className="rc-main">
        <section className="rc-volunteer-hero rc-hero--compact">
          <h1>Give a burnt forest a hand</h1>
          <p>
            Forestry teams decide where the work is needed. You pick a day, turn
            up, and they show you what to do when you get there. No experience
            and no equipment of your own required.
          </p>
        </section>

        <HowItWorks />

        <section className="rc-section">
          <div className="rc-section-head">
            <div>
              <h2>Days you can join <span>Katılabileceğin günler</span></h2>
            </div>
            {iller.length > 1 && (
              <div className="rc-place-filter" role="group" aria-label="Filter by province">
                <button
                  type="button"
                  className={`rc-chip${il === null ? " is-selected" : ""}`}
                  aria-pressed={il === null}
                  onClick={() => setIl(null)}
                >
                  Everywhere
                </button>
                {iller.map((x) => (
                  <button
                    key={x}
                    type="button"
                    className={`rc-chip${il === x ? " is-selected" : ""}`}
                    aria-pressed={il === x}
                    onClick={() => setIl(x)}
                  >
                    {x}
                  </button>
                ))}
              </div>
            )}
          </div>

          {hata && (
            <div className="rc-empty rc-empty--error" role="alert">
              <strong>We could not load the list of days.</strong>
              <p>{hata}</p>
              <button type="button" className="rc-button" onClick={yenile}>Try again</button>
            </div>
          )}

          {!hata && yukleniyor && (
            <div className="rc-empty" role="status">Looking for days near you…</div>
          )}

          {!hata && !yukleniyor && gosterilen.length === 0 && (
            <div className="rc-empty">
              <strong>Nothing open here at the moment.</strong>
              <p>
                {il === null
                  ? "Organisations post new days as areas become ready to work on. It is worth checking back."
                  : "Try “Everywhere” — there may be a day in a neighbouring province."}
              </p>
              {il !== null && (
                <button type="button" className="rc-button" onClick={() => setIl(null)}>
                  Show everywhere
                </button>
              )}
            </div>
          )}

          {!hata && !yukleniyor && gosterilen.length > 0 && (
            <ol className="rc-days">
              {gosterilen.map((activity) => (
                <ActivityRow
                  key={activity.id}
                  activity={katildiklarim[activity.id] ?? activity}
                  acik={acilanId === activity.id}
                  katildi={katildiklarim[activity.id]?.joined_by_me ?? activity.joined_by_me}
                  onToggle={() => setAcilanId((id) => (id === activity.id ? null : activity.id))}
                  onJoined={(guncel) => setKatildiklarim((o) => ({ ...o, [guncel.id]: guncel }))}
                  onVolunteerRegistered={setVolunteerId}
                  onSubmitted={() => setGonderimSayaci((n) => n + 1)}
                />
              ))}
            </ol>
          )}
        </section>

        <MyReports volunteerId={volunteerId} refreshToken={gonderimSayaci} />
      </main>
    </WorkspaceShell>
  );
}

/* ------------------------------------------------------------ nasıl işliyor */

/**
 * Hiç bilmeyen biri için üç cümle. Dekoratif bir "akış şeridi" değil:
 * ekranın gerçekten yaptığı üç şeyi sırayla söylüyor.
 */
function HowItWorks() {
  const adimlar = [
    { icon: "zone", baslik: "Pick a day", metin: "Every day below is run by a forestry organisation on an area that has already been surveyed." },
    { icon: "people", baslik: "Turn up", metin: "You are shown what to do on arrival. Bring what the day asks for — usually just closed shoes and water." },
    { icon: "observe", baslik: "Tell us what you saw", metin: "Four short questions at the end. It goes to the team looking after that hillside." },
  ] as const;

  return (
    <section className="rc-how" aria-label="How it works">
      {adimlar.map((adim, i) => (
        <div key={adim.baslik}>
          <span className="rc-how__mark"><Icon name={adim.icon} /></span>
          <h2>{adim.baslik}</h2>
          <p>{adim.metin}</p>
          {i < adimlar.length - 1 && <i className="rc-how__link" aria-hidden="true" />}
        </div>
      ))}
    </section>
  );
}

/* ------------------------------------------------------------------ bir gün */

function ActivityRow({
  activity, acik, katildi, onToggle, onJoined, onVolunteerRegistered, onSubmitted,
}: {
  activity: FieldActivity;
  acik: boolean;
  katildi: boolean;
  onToggle: () => void;
  onJoined: (guncel: FieldActivity) => void;
  onVolunteerRegistered: (id: string) => void;
  onSubmitted: () => void;
}) {
  const [katiliyor, setKatiliyor] = useState(false);
  const [katilHata, setKatilHata] = useState<string | null>(null);
  const yer = kalanYer(activity);
  const dolu = activity.capacity - activity.joined <= 0;

  async function katil() {
    setKatiliyor(true);
    setKatilHata(null);
    try {
      const gonullu = await ensureVolunteer();
      onVolunteerRegistered(gonullu.id);
      onJoined(await communityService.joinActivity(activity.id, gonullu.id));
    } catch (reason: unknown) {
      setKatilHata(reason instanceof Error ? reason.message : "Could not add you to this day. Try again.");
    } finally {
      setKatiliyor(false);
    }
  }

  return (
    <li className={`rc-day${acik ? " is-open" : ""}${katildi ? " is-joined" : ""}`}>
      <button
        type="button"
        className="rc-day__summary"
        onClick={onToggle}
        aria-expanded={acik}
      >
        <time className="rc-day__date" dateTime={activity.scheduled_for}>
          <span>{ayKisa(activity.scheduled_for)}</span>
          <b>{gunSayisi(activity.scheduled_for)}</b>
        </time>

        <span className="rc-day__what">
          <strong>{NE_YAPILACAK[activity.kind]}</strong>
          <small>{activity.province} · {activity.meeting_point}</small>
          {YENI_BASLAYANA_UYGUN[activity.kind] && !katildi && (
            <em className="rc-day__badge">Fine for a first time</em>
          )}
          {katildi && <em className="rc-day__badge rc-day__badge--joined">You are on the list</em>}
        </span>

        <span className={`rc-day__places${yer.acil ? " is-urgent" : ""}`}>{yer.metin}</span>
        <span className="rc-day__chevron" aria-hidden="true"><Icon name="arrow" /></span>
      </button>

      {acik && (
        <div className="rc-day__detail">
          <div className="rc-day__brief">
            <h3>{activity.title}</h3>
            <p>{activity.description}</p>

            <dl>
              <div><dt>When</dt><dd>{tamTarih(activity.scheduled_for)}</dd></div>
              <div><dt>Where to meet</dt><dd>{activity.meeting_point}, {activity.province}</dd></div>
              <div>
                <dt>Run by</dt>
                <dd>
                  {activity.organisation}
                  {activity.organisation_verified && (
                    <span className="rc-verified" title="Checked by ReGreen">verified</span>
                  )}
                </dd>
              </div>
            </dl>

            {activity.requirements.length > 0 && (
              <div className="rc-bring">
                <h4>What to bring</h4>
                <ul>{activity.requirements.map((r) => <li key={r}>{r}</li>)}</ul>
              </div>
            )}

            {!katildi && (
              <>
                <button
                  type="button"
                  className="rc-button rc-button--primary"
                  onClick={katil}
                  disabled={katiliyor || dolu}
                >
                  {dolu ? "This day is full" : katiliyor ? "Adding you…" : "Count me in"}
                  {!dolu && !katiliyor && <Icon name="arrow" />}
                </button>
                {dolu && (
                  <p className="rc-day__note">
                    Everyone has signed up for this one. The organisations post new
                    days regularly.
                  </p>
                )}
                {katilHata && <p className="rc-form-warning" role="alert">{katilHata}</p>}
              </>
            )}
          </div>

          {katildi && (
            <div className="rc-day__report">
              <h3>Back from the field?</h3>
              <p className="rc-day__note">
                Tell the team what you saw. It is read by the people looking after
                this hillside — it does not change the satellite model, it helps
                them check it against the ground.
              </p>
              <ObservationForm
                fireId={activity.fire_id}
                yer={activity.meeting_point}
                activityId={activity.id}
                onVolunteerRegistered={onVolunteerRegistered}
                onSubmitted={onSubmitted}
              />
            </div>
          )}
        </div>
      )}
    </li>
  );
}

/* -------------------------------------------------------------- senin katkın */

/**
 * Gönüllünün kendi kayıtları ve kurumun ne yaptığı.
 *
 * Buradaki her satır gerçek: GET /api/observations?volunteer_id=… Eskiden bu
 * bölümün yerinde uydurma bir "12 ay sonra" kartı vardı (sahte uydu
 * karşılaştırması ve "Assessment result is demo" rozetiyle); üretmediğimiz
 * bir sonucu göstermektense insanın gerçekten yaptığı şeyi gösteriyoruz.
 *
 * Hiç gönderim yoksa hiç görünmez — boş bir bölüm kimseyi motive etmez.
 */
function MyReports({
  volunteerId, refreshToken,
}: { volunteerId: string | null; refreshToken: number }) {
  const { observations, yukleniyor, hata, yenile } = useObservations(
    { volunteer_id: volunteerId ?? undefined }, volunteerId !== null,
  );

  const sonYenileme = useRef(refreshToken);
  useEffect(() => {
    if (refreshToken !== sonYenileme.current) {
      sonYenileme.current = refreshToken;
      yenile();
    }
  }, [refreshToken, yenile]);

  if (volunteerId === null) return null;
  if (!yukleniyor && !hata && observations.length === 0) return null;

  const kullanilan = observations.filter((o) => o.status === "accepted").length;

  return (
    <section className="rc-section">
      <div className="rc-section-head">
        <div>
          <h2>What you sent in <span>Senin gönderdiklerin</span></h2>
        </div>
        {kullanilan > 0 && (
          <p className="rc-tally">
            <b>{kullanilan}</b> of your {observations.length} used as field evidence
          </p>
        )}
      </div>

      {hata && <p className="rc-form-warning" role="alert">{hata}</p>}
      {yukleniyor && <div className="rc-empty" role="status">Loading…</div>}

      {!yukleniyor && !hata && (
        <ul className="rc-mine">
          {observations.map((o) => (
            <li key={o.id} className={o.status === "accepted" ? "is-accepted" : undefined}>
              <div>
                <strong>{o.province}</strong>
                <small>{o.activity_title ?? o.location}</small>
              </div>
              <p>{o.answers.join(" · ")}</p>
              <span className={`rc-outcome rc-outcome--${o.status}`}>{GOZLEM_DURUMU[o.status]}</span>
              {o.review_note && <q>{o.review_note}</q>}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
