import { useMemo, useRef, useState } from "react";
import { aciliyeteGore, useRecoveryZones, type RecoveryZone } from "../../../hooks/useRecoveryZones";
import { activities } from "../data/demoData";
import { observationService } from "../data/observationService";
import { useObservations } from "../data/useObservations";
import type { FieldActivityDemo } from "../types";
import { Icon } from "../components/Icons";
import { PrototypeModal } from "../components/PrototypeModal";
import { PrototypeShell } from "../components/PrototypeShell";
import { RecoveryUpdateCard } from "../components/RecoveryUpdateCard";
import {
  DemoNotice, VerdictBar, ZoneError, ZoneLoading, hektar,
} from "../components/RealZone";

/**
 * The community screen has one job:
 *
 *     "Where can I go, and what can I do?"
 *
 * The previous version opened with a decorative 5-step strip, 4 invented
 * counters, 4 filters, 3 zone cards, 3 activity cards AND a 6-field
 * observation form all at once. Nobody knew where to start.
 *
 * Now there are four steps and each one opens only after the previous:
 *   1  What is near me   (province filter + real zones)
 *   2  Join              (appears once an area is chosen)
 *   3  Observation form  (appears once you join an activity)
 *   4  What happened     (appears once you have submitted something)
 *
 * Step 4 is the part that used to be missing entirely: the form ended in a
 * modal saying "nothing was submitted", so a volunteer's contribution had no
 * visible consequence. Submissions now go to the observation store, appear in
 * the Organisation review queue, and the decision comes back here.
 */
export function VolunteerWorkspace() {
  const { zones, yukleniyor, hata } = useRecoveryZones();
  const [il, setIl] = useState("All provinces");
  const [seciliZoneId, setSeciliZoneId] = useState<string | null>(null);
  const [activity, setActivity] = useState<FieldActivityDemo | null>(null);
  const [katildi, setKatildi] = useState(false);
  const [action, setAction] = useState<string | null>(null);

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

  function zoneSec(zone: RecoveryZone) {
    setSeciliZoneId(zone.fireId);
    // Activities are tied to the area: the one opened for that fire is shown.
    setActivity(activities.find((a) => a.fireId === zone.fireId) ?? null);
    setKatildi(false);
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
                <h2>{activity ? `Open activity in ${secili.il}` : `${secili.il} · no open activity yet`}</h2>
              </div>
            </div>

            <div className="rc-join">
              <div className="rc-join__zone">
                <p>{secili.paragraf || "The summary for this area could not be loaded."}</p>
              </div>

              {!activity && (
                <div className="rc-join__activity rc-join__activity--bos">
                  <p>
                    No verified organisation has opened an activity here yet.
                    You can follow the area and join when one opens.
                  </p>
                  <button
                    type="button"
                    className="rc-button"
                    onClick={() => setAction("Follow area")}
                  >
                    Follow this area <Icon name="arrow" />
                  </button>
                  <DemoNotice>Activity data is demo. Area measurements are real.</DemoNotice>
                </div>
              )}

              {activity && (
                <div className="rc-join__activity">
                  <span className="rc-status">{activity.status}</span>
                  <h3>{activity.type}</h3>
                  <p>{activity.description}</p>
                  <dl>
                    <div><dt>Date</dt><dd>{activity.date}</dd></div>
                    <div><dt>Meeting point</dt><dd>{activity.location}</dd></div>
                    <div><dt>Organiser</dt><dd>{activity.organisation}</dd></div>
                    <div><dt>Capacity</dt><dd>{activity.joined} / {activity.capacity}</dd></div>
                  </dl>
                  <button
                    type="button"
                    className="rc-button rc-button--primary"
                    onClick={() => setKatildi(true)}
                  >
                    {katildi ? "Joined" : "Join activity"} <Icon name="arrow" />
                  </button>
                  <DemoNotice>
                    Activity and capacity data is demo. Area measurements are real.
                  </DemoNotice>
                </div>
              )}
            </div>
          </section>
        )}

        {katildi && secili && activity && (
          <section className="rc-section">
            <div className="rc-section-head">
              <div>
                <span className="rc-kicker">03 · Record your contribution</span>
                <h2>Note what you see <span>Gördüğünü not et</span></h2>
              </div>
            </div>
            <FieldObservationForm zone={secili} activity={activity} />
          </section>
        )}

        <MySubmissions zones={zones} />

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

      {action && (
        <PrototypeModal
          title={action}
          message="This part is a prototype. Following an area is not recorded anywhere; area measurements, however, come from real data."
          onClose={() => setAction(null)}
        />
      )}
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
  zone, activity,
}: { zone: RecoveryZone; activity: FieldActivityDemo }) {
  const [cevaplar, setCevaplar] = useState<Record<string, string>>({});
  const [konum, setKonum] = useState(activity.location);
  const [not, setNot] = useState("");
  const [fotoAdi, setFotoAdi] = useState<string | null>(null);
  const [uyari, setUyari] = useState<string | null>(null);
  const [gonderildi, setGonderildi] = useState(false);
  const dosyaRef = useRef<HTMLInputElement>(null);

  const verilenCevaplar = SORULAR
    .filter((s) => cevaplar[s.alan])
    .map((s) => `${s.alan.replace(/\?$/, "")}: ${cevaplar[s.alan]}`);

  function gonder(e: React.FormEvent) {
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
    observationService.submit({
      fireId: zone.fireId,
      activityId: activity.id,
      location: konum.trim(),
      photoName: fotoAdi,
      answers: verilenCevaplar,
      note: not,
    });
    setUyari(null);
    setGonderildi(true);
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

      <button type="submit" className="rc-button rc-button--primary">
        Submit observation ({verilenCevaplar.length}/4 answered)
      </button>

      <p className="rc-integrity-note">
        Volunteer observations <strong>do not train the model.</strong> The model
        learns from satellite data; your observation is reviewed as supporting
        field evidence for the expert's decision. There is no observation backend
        yet, so your submission is kept in this browser only.
      </p>
    </form>
  );
}

/* ------------------------------------------------- step 4: what happened */

/**
 * The volunteer's own submissions and what the organisation decided.
 * Hidden until there is at least one, so it never sits there empty.
 */
function MySubmissions({ zones }: { zones: RecoveryZone[] }) {
  const hepsi = useObservations();
  const benimkiler = hepsi.filter((o) => o.origin === "local");
  if (benimkiler.length === 0) return null;

  return (
    <section className="rc-section">
      <div className="rc-section-head">
        <div>
          <span className="rc-kicker">04 · Follow-up</span>
          <h2>What happened to my observations? <span>Gözlemlerime ne oldu?</span></h2>
        </div>
        <button
          type="button"
          className="rc-button rc-button--ghost"
          onClick={() => observationService.clearLocal()}
        >
          Clear my submissions
        </button>
      </div>

      <div className="rc-my-observations">
        {benimkiler.map((o) => {
          const zone = zones.find((z) => z.fireId === o.fireId);
          return (
            <article key={o.id}>
              <div>
                <strong>{zone ? `${zone.il} · ${zone.fireId}` : o.fireId}</strong>
                <small>{o.location} · {o.displayDate}</small>
              </div>
              <p>{o.answers.join(" · ")}</p>
              <span className={`rc-status${o.status !== "Pending" ? " rc-status--complete" : ""}`}>
                {o.status === "Pending" ? "Waiting for review" : o.status}
              </span>
            </article>
          );
        })}
      </div>

      <p className="rc-section-intro">
        Stored in this browser only — there is no observation backend yet. Open
        the Organisation screen to see the review queue these records land in.
      </p>
    </section>
  );
}
