import type { PredictionStatus, PriorityClass } from "../types";

export const priorityClassLabels: Record<PriorityClass, string> = {
  COK_YUKSEK: "Very High",
  YUKSEK: "High",
  ORTA: "Medium",
  DUSUK: "Low",
};

export const predictionStatusLabels: Record<PredictionStatus, string> = {
  predicted: "Prediction Available",
  low_severity: "Not Prioritized",
  no_data: "Insufficient Data",
};

export function formatDecimal(value: number | null, digits = 2): string {
  return value === null ? "Missing in source data" : value.toLocaleString(undefined, { maximumFractionDigits: digits });
}

export function formatDegrees(value: number): string {
  return `${value.toLocaleString(undefined, { maximumFractionDigits: 1 })}°`;
}

export function formatMeters(value: number | null): string {
  return value === null ? "Missing in source data" : `${Math.round(value).toLocaleString()} m`;
}

export function formatKilometers(value: number): string {
  return `${value.toLocaleString(undefined, { maximumFractionDigits: 2 })} km`;
}
