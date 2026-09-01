import { useSyncExternalStore } from "react";
import { observationService, type FieldObservation } from "./observationService";

/**
 * Subscribes a screen to the observation store.
 *
 * Both screens use this, which is what closes the loop: a submission on the
 * Community screen re-renders the Organisation review queue (and the other way
 * round for the accept / needs-clarification decision) without either screen
 * knowing the other exists.
 *
 * useSyncExternalStore requires getSnapshot to return the SAME reference until
 * something actually changes — the service caches its snapshot for exactly
 * that reason.
 */
export function useObservations(): FieldObservation[] {
  return useSyncExternalStore(
    (l) => observationService.subscribe(l),
    () => observationService.list(),
    () => observationService.list(),
  );
}
