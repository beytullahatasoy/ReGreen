import { useRef, useState } from "react";
import { communityService } from "../../../services";
import { ensureVolunteer } from "../data/volunteerIdentity";
import { SORULAR } from "./volunteerText";

/**
 * "Gördüğünü anlat" formu.
 *
 * Bu ekranın tek yazma işlemi. Uzman diline hiç girmez: gönüllü gördüğünü
 * söyler, ne anlama geldiğini kurum ve uzman yorumlar.
 *
 * Tek zorunlu kural: en az bir cevap. Cevapsız gözlem kanıt değil, kurumun
 * kuyruğunda gürültüdür — aynı kural backend'de de var
 * (CommunityEndpoints.CreateObservation + CK_FieldObservations_Answers).
 */
export function ObservationForm({
  fireId, yer, activityId, onVolunteerRegistered, onSubmitted,
}: {
  fireId: string;
  /** Buluşma noktası — konum alanına hazır gelir, kullanıcı değiştirebilir. */
  yer: string;
  activityId: number | null;
  onVolunteerRegistered: (id: string) => void;
  onSubmitted: () => void;
}) {
  const [cevaplar, setCevaplar] = useState<Record<string, string>>({});
  const [konum, setKonum] = useState(yer);
  const [not, setNot] = useState("");
  const [fotoAdi, setFotoAdi] = useState<string | null>(null);
  const [uyari, setUyari] = useState<string | null>(null);
  const [gonderiliyor, setGonderiliyor] = useState(false);
  const [gonderildi, setGonderildi] = useState(false);
  const dosyaRef = useRef<HTMLInputElement>(null);

  const verilen = SORULAR
    .filter((s) => cevaplar[s.alan])
    .map((s) => `${s.alan.replace(/\?$/, "")}: ${cevaplar[s.alan]}`);

  async function gonder(e: React.FormEvent) {
    e.preventDefault();
    if (verilen.length === 0) {
      setUyari("Answer at least one question — even one helps.");
      return;
    }
    if (!konum.trim()) {
      setUyari("Add roughly where you were standing.");
      return;
    }

    setUyari(null);
    setGonderiliyor(true);
    try {
      const gonullu = await ensureVolunteer();
      onVolunteerRegistered(gonullu.id);
      await communityService.createObservation(fireId, {
        volunteer_id: gonullu.id,
        activity_id: activityId,
        location: konum.trim(),
        photo_name: fotoAdi,
        answers: verilen,
        note: not.trim() || undefined,
      });
      setGonderildi(true);
      onSubmitted();
    } catch (reason: unknown) {
      setUyari(reason instanceof Error ? reason.message : "Could not send your report. Try again.");
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
      <div className="rc-report rc-report--done" role="status">
        <h3>Thank you — that is on its way</h3>
        <p>
          Someone from the organisation running this area will read it. You can
          see what they decided further down this page.
        </p>
        <button type="button" className="rc-button" onClick={yenidenDoldur}>
          Add another report
        </button>
      </div>
    );
  }

  return (
    <form className="rc-report" onSubmit={gonder}>
      <div className="rc-report__questions">
        {SORULAR.map((soru) => (
          <fieldset key={soru.alan} className="rc-choice">
            <legend>{soru.alan}</legend>
            <p className="rc-choice__hint">{soru.ipucu}</p>
            <div className="rc-choice__options">
              {soru.secenekler.map((secenek) => {
                const secili = cevaplar[soru.alan] === secenek;
                return (
                  <button
                    key={secenek}
                    type="button"
                    className={`rc-chip${secili ? " is-selected" : ""}`}
                    aria-pressed={secili}
                    // Aynı seçeneğe ikinci kez basmak cevabı geri alır: yanlış
                    // dokunan biri formu sıfırlamak zorunda kalmasın.
                    onClick={() => setCevaplar((o) => ({
                      ...o, [soru.alan]: secili ? "" : secenek,
                    }))}
                  >
                    {secenek}
                  </button>
                );
              })}
            </div>
          </fieldset>
        ))}
      </div>

      <div className="rc-report__details">
        <label>
          Where were you standing?
          <input
            type="text"
            value={konum}
            onChange={(e) => setKonum(e.target.value)}
            placeholder="The meeting point, a path, a landmark…"
          />
        </label>

        <label>
          Anything else you noticed?
          <textarea
            value={not}
            onChange={(e) => setNot(e.target.value)}
            placeholder="Only what you saw with your own eyes. Optional."
          />
        </label>

        <label>
          A photo, if you took one
          {/* Dosya tarayıcıdan çıkmaz; yalnızca adı kaydedilir ki kurum
              isterse sizden isteyebilsin. Gerçek dosya yükleme ayrı iş. */}
          <input
            ref={dosyaRef}
            type="file"
            accept="image/*"
            className="rc-file-input"
            onChange={(e) => setFotoAdi(e.target.files?.[0]?.name ?? null)}
          />
          <small>
            {fotoAdi
              ? `Ready to send the name: ${fotoAdi}`
              : "We record the file name so the team can ask you for it. The photo itself stays on your phone."}
          </small>
        </label>
      </div>

      {uyari && <p className="rc-form-warning" role="alert">{uyari}</p>}

      <div className="rc-report__submit">
        <button type="submit" className="rc-button rc-button--primary" disabled={gonderiliyor}>
          {gonderiliyor ? "Sending…" : "Send my report"}
        </button>
        <small>{verilen.length} of {SORULAR.length} answered</small>
      </div>
    </form>
  );
}
