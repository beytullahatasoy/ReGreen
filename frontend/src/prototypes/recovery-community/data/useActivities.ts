import { useCallback, useEffect, useState } from "react";
import { communityService } from "../../../services";
import type { ActivityQuery, FieldActivity } from "../../../types/community";

export interface ActivitiesState {
  activities: FieldActivity[];
  yukleniyor: boolean;
  hata: string | null;
  yenile: () => void;
}

/** Fetches field activities from the real backend (GET /api/activities). */
export function useActivities(query: ActivityQuery = {}): ActivitiesState {
  const { fire_id, status, limit } = query;
  const [activities, setActivities] = useState<FieldActivity[]>([]);
  const [yukleniyor, setYukleniyor] = useState(true);
  const [hata, setHata] = useState<string | null>(null);
  const [tetik, setTetik] = useState(0);

  useEffect(() => {
    if (!fire_id) { setActivities([]); setYukleniyor(false); return; }
    let aktif = true;
    setYukleniyor(true);
    communityService.getActivities({ fire_id, status, limit })
      .then((data) => { if (aktif) { setActivities(data); setHata(null); } })
      .catch((reason: unknown) => {
        if (aktif) setHata(reason instanceof Error ? reason.message : "Etkinlikler yüklenemedi.");
      })
      .finally(() => { if (aktif) setYukleniyor(false); });
    return () => { aktif = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [fire_id, status, limit, tetik]);

  const yenile = useCallback(() => setTetik((n) => n + 1), []);

  return { activities, yukleniyor, hata, yenile };
}
