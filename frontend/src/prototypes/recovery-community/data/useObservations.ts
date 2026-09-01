import { useCallback, useEffect, useState } from "react";
import { communityService } from "../../../services";
import type { FieldObservation, ObservationQuery } from "../../../types/community";

export interface ObservationsState {
  observations: FieldObservation[];
  yukleniyor: boolean;
  hata: string | null;
  yenile: () => void;
}

/**
 * Fetches the observation queue from the real backend (GET /api/observations).
 * Re-runs whenever `query` changes by value, and exposes `yenile()` so a
 * screen can refetch right after it submits or reviews an observation.
 *
 * `enabled=false` skips the fetch entirely — for "my submissions" before a
 * volunteer identity exists, there is nothing to ask the server for yet.
 */
export function useObservations(query: ObservationQuery = {}, enabled = true): ObservationsState {
  const { fire_id, status, volunteer_id, limit } = query;
  const [observations, setObservations] = useState<FieldObservation[]>([]);
  const [yukleniyor, setYukleniyor] = useState(enabled);
  const [hata, setHata] = useState<string | null>(null);
  const [tetik, setTetik] = useState(0);

  useEffect(() => {
    if (!enabled) { setObservations([]); setYukleniyor(false); return; }
    let aktif = true;
    setYukleniyor(true);
    communityService.getObservations({ fire_id, status, volunteer_id, limit })
      .then((data) => { if (aktif) { setObservations(data); setHata(null); } })
      .catch((reason: unknown) => {
        if (aktif) setHata(reason instanceof Error ? reason.message : "Gözlemler yüklenemedi.");
      })
      .finally(() => { if (aktif) setYukleniyor(false); });
    return () => { aktif = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [enabled, fire_id, status, volunteer_id, limit, tetik]);

  const yenile = useCallback(() => setTetik((n) => n + 1), []);

  return { observations, yukleniyor, hata, yenile };
}
