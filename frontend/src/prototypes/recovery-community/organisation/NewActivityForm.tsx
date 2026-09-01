import { useState } from "react";
import { communityService } from "../../../services";
import type { ActivityKind, FieldActivity } from "../../../types/community";
import { ACTIVITY_KIND_LABEL } from "../components/RealZone";

const TURLER: ActivityKind[] = [
  "planting", "cleanup", "erosion_observation", "vegetation_monitoring", "field_assessment",
];

/**
 * Kurumun seçili alan için saha günü açması — POST /api/activities.
 *
 * Bu form olmadan zincir kopuyordu: uç nokta vardı ama arayüzden etkinlik
 * açmanın yolu yoktu, dolayısıyla Community ekranı hiç dolmuyordu. Kurum
 * ekranının asıl işi "bugün nereye ekip göndereceğim" olduğu için, günü
 * açmak da burada olmalı.
 */
export function NewActivityForm({
  fireId, il, onCreated,
}: { fireId: string; il: string; onCreated: (activity: FieldActivity) => void }) {
  const [acik, setAcik] = useState(false);
  const [kind, setKind] = useState<ActivityKind>("planting");
  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [tarih, setTarih] = useState("");
  const [yer, setYer] = useState("");
  const [kontenjan, setKontenjan] = useState("20");
  const [kosullar, setKosullar] = useState("");
  const [hata, setHata] = useState<string | null>(null);
  const [gonderiliyor, setGonderiliyor] = useState(false);

  function sifirla() {
    setTitle(""); setDescription(""); setTarih(""); setYer("");
    setKontenjan("20"); setKosullar(""); setHata(null);
  }

  async function gonder(e: React.FormEvent) {
    e.preventDefault();
    const sayi = Number(kontenjan);
    if (!title.trim()) return setHata("Give the day a title volunteers will recognise.");
    if (!tarih) return setHata("Pick a date.");
    if (!yer.trim()) return setHata("Volunteers need a meeting point.");
    if (!Number.isInteger(sayi) || sayi < 1 || sayi > 1000) return setHata("Capacity must be between 1 and 1000.");

    setHata(null);
    setGonderiliyor(true);
    try {
      const olusan = await communityService.createActivity({
        fire_id: fireId,
        kind,
        title: title.trim(),
        description: description.trim(),
        scheduled_for: tarih,
        meeting_point: yer.trim(),
        capacity: sayi,
        // Virgülle ayrılan liste; boşlar atılır.
        requirements: kosullar.split(",").map((r) => r.trim()).filter(Boolean),
      });
      onCreated(olusan);
      sifirla();
      setAcik(false);
    } catch (reason: unknown) {
      setHata(reason instanceof Error ? reason.message : "Could not open the activity.");
    } finally {
      setGonderiliyor(false);
    }
  }

  if (!acik) {
    return (
      <button type="button" className="rc-button rc-button--primary" onClick={() => setAcik(true)}>
        Open a field day here
      </button>
    );
  }

  return (
    <form className="rc-new-activity" onSubmit={gonder}>
      <h4>New field day · {il}</h4>

      <label>
        What kind of work
        <select value={kind} onChange={(e) => setKind(e.target.value as ActivityKind)}>
          {TURLER.map((t) => <option key={t} value={t}>{ACTIVITY_KIND_LABEL[t]}</option>)}
        </select>
      </label>

      <label>
        Title
        <input
          type="text" value={title} maxLength={150}
          onChange={(e) => setTitle(e.target.value)}
          placeholder="Planting day above Manavgat"
        />
      </label>

      <label>
        What volunteers will do
        <textarea
          value={description} maxLength={1000}
          onChange={(e) => setDescription(e.target.value)}
          placeholder="Written for someone who has never joined one before."
        />
      </label>

      <div className="rc-new-activity__row">
        <label>
          Date
          <input type="date" value={tarih} onChange={(e) => setTarih(e.target.value)} />
        </label>
        <label>
          Places
          <input
            type="number" min={1} max={1000} value={kontenjan}
            onChange={(e) => setKontenjan(e.target.value)}
          />
        </label>
      </div>

      <label>
        Meeting point
        <input
          type="text" value={yer} maxLength={200}
          onChange={(e) => setYer(e.target.value)}
          placeholder="Forest station car park"
        />
      </label>

      <label>
        What to bring <small>comma separated</small>
        <input
          type="text" value={kosullar} maxLength={500}
          onChange={(e) => setKosullar(e.target.value)}
          placeholder="Closed shoes, Water bottle, Age 16+"
        />
      </label>

      {hata && <p className="rc-form-warning" role="alert">{hata}</p>}

      <div className="rc-new-activity__actions">
        <button type="submit" className="rc-button rc-button--primary" disabled={gonderiliyor}>
          {gonderiliyor ? "Opening…" : "Open the day"}
        </button>
        <button type="button" className="rc-button" onClick={() => { sifirla(); setAcik(false); }}>
          Cancel
        </button>
      </div>
    </form>
  );
}
