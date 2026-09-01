import { useEffect, useMemo, useRef, useState } from "react";
import { aciliyeteGore, useRecoveryZones, type RecoveryZone } from "../../../hooks/useRecoveryZones";
import { communityService } from "../../../services";
import type { FieldActivity } from "../../../types/community";
import { getCachedVolunteerId, ensureVolunteer } from "../data/volunteerIdentity";
import { useObservations } from "../data/useObservations";
import { useActivities } from "../data/useActivities";
import { Icon } from "../components/Icons";
import { PrototypeShell } from "../components/PrototypeShell";
import { RecoveryUpdateCard } from "../components/RecoveryUpdateCard";
import {
  ACTIVITY_KIND_LABEL, VerdictBar, ZoneError, ZoneLoading, hektar, tarih,
} from "../components/RealZone";

/**
 * The community screen has one job:
 *
 *     "Where can I go, and what can I do?"
 *
 * There are four steps and each one opens only after the previous:
 *   1  What is near me   (province filter + real zones)
 *   2  Join              (appears once an area is chosen)
 *   3  Observation form  (appears once you join an activity)
 *   4  What happened     (appears once you have submitted something)
 *
 * All four now talk to the real backend (backend/ReGreen.Api/Endpoints/CommunityEndpoints.cs):
 * activities are ones an organisation actually opened, joining registers a
 * real participant row, and a submitted observation lands in the
 * Organisation review queue for real.
 */
export function VolunteerWorkspace() {
  const { zones, yukleniyor, hata } = useRecoveryZones();
  const [il, setIl] = useState("All provinces");
  const [seciliZoneId, setSeciliZoneId] = useState<string | null>(null);
  const [activity, setActivity] = useState<FieldActivity | null>(null);
  const [katilHata, setKatilHata] = useState<string | null>(null);
  const [katiliyor, setKatiliyor] = useState(false);
  const [volunteerId, setVolunteerId] = useState<string | null>(getCachedVolunteerId());
  const [gonderimSayaci, setGonderimSayaci] = useState(0);

  const iller = useMemo(
    () => [...new Set(zones.map((z) => z.il))].sort((a, b) => a.localeCompare(b, "tr")),
    [zones],
  );

  // A citizen sees places they could go, not a catalogue: areas that actually
  // need action come first, at most six.
  const gosterilen = useMemo(() => {
    const suzulmus = zones.filter((z) => (il === "All provinces" || z.il === il) && z.mudahaleHucre > 0);
    return aciliyeteGore(suzulmus).slice(0, 6);
  }, [zones, il]);

  const secili = gosterilen.find((z) => z.fireId === seciliZoneId) ?? null;
  const bolgeEtkinlikleri = useActivities({ fire_id: secili?.fireId });
  const acikEtkinlik = bolgeEtkinlikleri.activities.find((a) => a.status === "open" || a.status === "scheduled") ?? null;
  const katildi = activity !== null;

  function zoneSec(zone: RecoveryZone) {
    setSeciliZoneId(zone.fireId);
    setActivity(null);
    setKatilHata(null);
  }

  async function katil() {
    if (!acikEtkinlik) return;
    setKatiliyor(true);
    setKatilHata(null);
    try {
      const gonullu = await ensureVolunteer();
      setVolunteerId(gonullu.id);
      const guncel = await communityService.joinActivity(acikEtkinlik.id, gonullu.id);
      setActivity(guncel);
    } catch (reason: unknown) {
      setKatilHata(reason instanceof Error ? reason.message : "Etkinliğe katılınamadı.");
    } finally {
      setKatiliyor(false);
    }
  }

  return (
    <PrototypeShell mode="volunteer">
      <main className="rc-main">
        <section className="rc-volunteer-hero rc-hero--compact">
          <span className="rc-kicker rc-kicker--orange">Community · field contribution</span>
          <h1>Where can I go?</h1>
          <p>
            Pick an area that has been through expert assessment, join an activity
            opened by a verified organisation, and record what you see.
          </p>
        </section>

        {hata && <div className="rc-section"><ZoneError mesaj={hata} /></div>}
        {yukleniyor && <div className="rc-section"><ZoneLoading mesaj="Loading areas…" /></div>}

        {!yukleniyor && !hata && (
          <section className="rc-section">
            <div className="rc-section-head">
              <div>
                <span className="rc-kicker">01 · Choose an area</span>
                <h2>What is near me? <span>Yakınımda ne var?</span></h2>
              </div>
              <label className="rc-il-secim">
                Province
                <select value={il} onChange={(e) => { setIl(e.target.value); setSeciliZoneId(null); }}>
                  <option>All provinces</option>
                  {iller.map((x) => <option key={x}>{x}</option>)}
                </select>
              </label>
            </div>

            {gosterilen.length === 0 ? (
              <div className="rc-empty">No area in this province currently needs intervention.</div>
            ) : (
              <div className="rc-zone-picks">
                {gosterilen.map((zone) => (
                  <button
                    key={zone.fireId}
                    type="button"
                    className={`rc-zone-pick${zone.fireId === seciliZoneId ? " is-selected" : ""}`}
                    onClick={() => zoneSec(zone)}
                    aria-pressed={zone.fireId === seciliZoneId}
                  >
                    <span className="rc-zone-pick__head">
                      <strong>{zone.il}</strong>
                      <small>{zone.bolge}</small>
                    </span>
                    <span className="rc-zone-pick__figure">
                      <b>{hektar(zone.mudahaleHa)}</b>
                      <small>needs direct action</small>
                    </span>
                    <VerdictBar zone={zone} />
                    {zone.guven === "dusuk" && (
                      <em className="rc-zone-pick__warn">Prediction uncertainty is high in this region</em>
                    )}
                  </button>
                ))}
              </div>
            )}
          </section>
        )}

        {secili && (
          <section className="rc-section">
            <div className="rc-section-head">
              <div>
                <span className="rc-kicker">02 · Join</span>
                <h2>{acikEtkinlik ? `Open activity in ${secili.il}` : `${secili.il} · no open activity yet`}</h2>
              </div>
            </div>

            <div className="rc-join">
              <div className="rc-join__zone">
                <p>{secili.paragraf || "The summary for this area could not be loaded."}</p>
              </div>

              {bolgeEtkinlikleri.yukleniyor && <p className="rc-section-intro">Loading activities…</p>}
              {bolgeEtkinlikleri.hata && <p className="rc-form-warning" role="alert">{bolgeEtkinlikleri.hata}</p>}

              {!bolgeEtkinlikleri.yukleniyor && !acikEtkinlik && (
                <div className="rc-join__activity rc-join__activity--bos">
                  <p>
                    No verified organisation has opened an activity here yet.
                    Check back later, or record an observation on your own below.
                  </p>
                </div>
              )}

              {acikEtkinlik && (
                <div className="rc-join__activity">
                  <span className="rc-status">{acikEtkinlik.status}</span>
                  <h3>{ACTIVITY_KIND_LABEL[acikEtkinlik.kind]}</h3>
                  <p>{acikEtkinlik.description}</p>
                  <dl>
                    <div><dt>Date</dt><dd>{acikEtkinlik.scheduled_for}</dd></div>
                    <div><dt>Meeting point</dt><dd>{acikEtkinlik.meeting_point}</dd></div>
                    <div><dt>Organiser</dt><dd>{acikEtkinlik.organisation}</dd></div>
                    <div><dt>Capacity</dt><dd>{(activity ?? acikEtkinlik).joined} / {acikEtkinlik.capacity}</dd></div>
                  </dl>
                  <button
                    type="button"
                    className="rc-button rc-button--primary"
                    onClick={katil}
                    disabled={katildi || katiliyor}
                  >
                    {katildi ? "Joined" : katiliyor ? "Joining…" : "Join activity"} <Icon name="arrow" />
                  </button>
                  {katilHata && <p className="rc-form-warning" role="alert">{katilHata}</p>}
                </div>
              )}
            </div>
          </section>
        )}

        {secili && (
          <section className="rc-section">
            <div className="rc-section-head">
              <div>
                <span className="rc-kicker">03 · Record your contribution</span>
                <h2>Note what you see <span>Gördüğünü not et</span></h2>
              </div>
            </div>
            <FieldObservationForm
              key={`${secili.fireId}:${acikEtkinlik?.id ?? "none"}`}
              zone={secili}
              activityId={katildi ? acikEtkinlik?.id ?? null : null}
              meetingPoint={acikEtkinlik?.meeting_point ?? ""}
              onVolunteerRegistered={setVolunteerId}
              onSubmitted={() => setGonderimSayaci((n) => n + 1)}
            />
          </section>
        )}

        <MySubmissions zones={zones} volunteerId={volunteerId} refreshToken={gonderimSayaci} />

        <section className="rc-section">
          <div className="rc-section-head">
            <div>
              <span className="rc-kicker">What your contribution leads to</span>
              <h2>Twelve months on</h2>
            </div>
          </div>
          <RecoveryUpdateCard zones={zones} />
        </section>
      </main>
    </PrototypeShell>
  );
}

/* ------------------------------------------------------------------ form */

/** Question -> options. The submitted answer is stored as "Question: value". */
const SORULAR = [
  { alan: "Vegetation visible?", secenekler: ["Not visible", "Sparse", "Mixed", "Dense"] },
  { alan: "Signs of erosion?", secenekler: ["Not observed", "Possible", "Visible"] },
  { alan: "Ground condition", secenekler: ["Dry", "Moist", "Loose surface", "Other"] },
  { alan: "Access issue", secenekler: ["None", "Path blocked", "Unsafe access", "Other"] },
] as const;

function FieldObservationForm({
  zone, activityId, meetingPoint, onVolunteerRegistered, onSubmitted,
}: {
  zone: RecoveryZone;
  activityId: number | null;
  meetingPoint: string;
  onVolunteerRegistered: (id: string) => void;
  onSubmitted: () => void;
}) {
  const [cevaplar, setCevaplar] = useState<Record<string, string>>({});
  const [konum, setKonum] = useState(meetingPoint);
  const [not, setNot] = useState("");
  const [fotoAdi, setFotoAdi] = useState<string | null>(null);
  const [uyari, setUyari] = useState<string | null>(null);
  const [gonderiliyor, setGonderiliyor] = useState(false);
  const [gonderildi, setGonderildi] = useState(false);
  const dosyaRef = useRef<HTMLInputElement>(null);

  const verilenCevaplar = SORULAR
    .filter((s) => cevaplar[s.alan])
    .map((s) => `${s.alan.replace(/\?$/, "")}: ${cevaplar[s.alan]}`);

  async function gonder(e: React.FormEvent) {
    e.preventDefault();
    // An observation with no answer at all is not evidence — it is noise in
    // the organisation's queue. This is the only hard requirement.
    if (verilenCevaplar.length === 0) {
      setUyari("Answer at least one question before submitting.");
      return;
    }
    if (!konum.trim()) {
      setUyari("Say where you made the observation.");
      return;
    }
    setUyari(null);
    setGonderiliyor(true);
    try {
      const gonullu = await ensureVolunteer();
      onVolunteerRegistered(gonullu.id);
      await communityService.createObservation(zone.fireId, {
        volunteer_id: gonullu.id,
        activity_id: activityId,
        location: konum.trim(),
        photo_name: fotoAdi,
        answers: verilenCevaplar,
        note: not.trim() || undefined,
      });
      setGonderildi(true);
      onSubmitted();
    } catch (reason: unknown) {
      setUyari(reason instanceof Error ? reason.message : "Gözlem gönderilemedi.");
    } finally {
      setGonderiliyor(false);
    }
  }

  function yenidenDoldur() {
    setCevaplar({});
    setNot("");
    setFotoAdi(null);
    setUyari(null);
    setGonderildi(false);
    if (dosyaRef.current) dosyaRef.current.value = "";
  }

  if (gonderildi) {
    return (
      <div className="rc-observation rc-observation--done">
        <span className="rc-status rc-status--complete">Submitted</span>
        <h3>Your observation is in the review queue</h3>
        <p>
          It is now waiting for the organisation running this area. You can follow
          its status under <strong>“What happened to my observations?”</strong> below.
        </p>
        <button type="button" className="rc-button" onClick={yenidenDoldur}>
          Record another observation
        </button>
      </div>
    );
  }

  return (
    <form className="rc-observation" onSubmit={gonder}>
      <p>You record what you see; experts interpret what it means.</p>

      <label>
        Geotagged field photo
        {/* The file never leaves the browser: only the name is kept, so the
            organisation can ask for it. Storing the image itself would blow
            the browser storage quota within a few submissions. */}
        <input
          ref={dosyaRef}
          type="file"
          accept="image/*"
          className="rc-file-input"
          onChange={(e) => setFotoAdi(e.target.files?.[0]?.name ?? null)}
        />
        <small>{fotoAdi ? `Selected: ${fotoAdi}` : "The file is not uploaded — only its name is recorded."}</small>
      </label>

      <div className="rc-form-grid">
        {SORULAR.map((soru) => (
          <label key={soru.alan}>
            {soru.alan}
            <select
              value={cevaplar[soru.alan] ?? ""}
              onChange={(e) => setCevaplar((o) => ({ ...o, [soru.alan]: e.target.value }))}
            >
              <option value="">Select what you observe</option>
              {soru.secenekler.map((secenek) => <option key={secenek}>{secenek}</option>)}
            </select>
          </label>
        ))}
      </div>

      <label>
        Where exactly?
        <input
          type="text"
          value={konum}
          onChange={(e) => setKonum(e.target.value)}
          placeholder="Meeting point, path name, landmark…"
        />
      </label>

      <label>
        Additional observation
        <textarea
          value={not}
          onChange={(e) => setNot(e.target.value)}
          placeholder="Describe only what you can directly see…"
        />
      </label>

      {uyari && <p className="rc-form-warning" role="alert">{uyari}</p>}

      <button type="submit" className="rc-button rc-button--primary" disabled={gonderiliyor}>
        {gonderiliyor ? "Submitting…" : `Submit observation (${verilenCevaplar.length}/4 answered)`}
      </button>

      <p className="rc-integrity-note">
        Volunteer observations <strong>do not train the model.</strong> The model
        learns from satellite data; your observation is reviewed as supporting
        field evidence for the expert's decision.
      </p>
    </form>
  );
}

/* ------------------------------------------------- step 4: what happened */

/**
 * The volunteer's own submissions and what the organisation decided.
 * Hidden until there is a registered identity, and again until it has at
 * least one submission, so it never sits there empty.
 */
function MySubmissions({
  zones, volunteerId, refreshToken,
}: { zones: RecoveryZone[]; volunteerId: string | null; refreshToken: number }) {
  const { observations, yukleniyor, hata, yenile } = useObservations({ volunteer_id: volunteerId ?? undefined }, volunteerId !== null);
  const ilkYenileme = useRef(refreshToken);
  useEffect(() => {
    if (refreshToken !== ilkYenileme.current) { ilkYenileme.current = refreshToken; yenile(); }
  }, [refreshToken, yenile]);
  if (volunteerId === null || (!yukleniyor && !hata && observations.length === 0)) return null;

  return (
    <section className="rc-section">
      <div className="rc-section-head">
        <div>
          <span className="rc-kicker">04 · Follow-up</span>
          <h2>What happened to my observations? <span>Gözlemlerime ne oldu?</span></h2>
        </div>
      </div>

      {hata && <p className="rc-form-warning" role="alert">{hata}</p>}
      {yukleniyor && <p className="rc-section-intro">Loading…</p>}

      {!yukleniyor && !hata && (
        <div className="rc-my-observations">
          {observations.map((o) => {
            const zone = zones.find((z) => z.fireId === o.fire_id);
            return (
              <article key={o.id}>
                <div>
                  <strong>{zone ? `${zone.il} · ${zone.fireId}` : o.fire_id}</strong>
                  <small>{o.location} · {tarih(o.submitted_at)}</small>
                </div>
                <p>{o.answers.join(" · ")}</p>
                <span className={`rc-status${o.status !== "pending" ? " rc-status--complete" : ""}`}>
                  {o.status === "pending" ? "Waiting for review" : o.status.replace(/_/g, " ")}
                </span>
              </article>
            );
          })}
        </div>
      )}
    </section>
  );
}
