import type { Feature, MultiPolygon, Polygon } from "geojson";

export type QualityFlag = "ok" | "check";
export type PredictionStatus = "predicted" | "low_severity" | "no_data";
export type PriorityClass = "COK_YUKSEK" | "YUKSEK" | "ORTA" | "DUSUK";
export type SeverityClass = "dusuk" | "orta-dusuk" | "orta-yuksek" | "yuksek";
export type LandCover =
  | "Agaclik"
  | "Ciplak"
  | "Otlak/calilik"
  | "Su"
  | "Sulak alan"
  | "Tarim"
  | "Yerlesim";

export interface FireSummary {
  fire_id: string;
  fire_date: string;
  province: string;
  region: string;
  modis_area_ha: number;
  burned_area_ha: number;
  cell_count: number;
  has_perimeter: boolean;
  marker_lat: number;
  marker_lon: number;
  quality_flag: QualityFlag;
  quality_note: string | null;
}

export interface FirePerimeterProperties {
  fire_id: string;
  fire_date: string;
  province: string;
  region: string;
  modis_area_ha: number;
}

export interface FirePerimeter {
  fire_id: string;
  marker: { lat: number; lon: number };
  perimeter: Feature<Polygon | MultiPolygon, FirePerimeterProperties>;
}

export interface Cell {
  cell_id: string;
  lat: number;
  lon: number;
  tree_cover: number;
  tree_cover_annual: number | null;
  burn_severity_dnbr: number;
  slope_deg: number;
  elevation_m: number | null;
  road_distance_km: number;
  ndvi_before: number;
  ndvi_after: number;
  ndvi_drop: number;
  severity_class: SeverityClass;
  land_cover: LandCover | null;
  prediction_status: PredictionStatus;
  recovery_gap_pred: number | null;
  priority_score: number | null;
  priority_class: PriorityClass | null;
}

export interface PriorityWeights {
  recovery: number;
  erosion: number;
  access: number;
}

export interface CellsResponse {
  fire_id: string;
  model_run_id: number;
  generated_at: string;
  crs: "EPSG:4326";
  cell_size_m: number;
  applied_weights: PriorityWeights;
  count: number;
  items: Cell[];
}

export type ApiErrorCode =
  | "FIRE_NOT_FOUND"
  | "MODEL_RUN_NOT_FOUND"
  | "INVALID_PRIORITY_WEIGHTS"
  | "INVALID_BOUNDING_BOX"
  | "INVALID_QUERY_PARAMETER"
  | "PERIMETER_DATA_CORRUPT"
  | "DB_UNAVAILABLE"
  | "UNEXPECTED_ERROR";

export interface ApiProblem {
  type: string;
  title: string;
  status: number;
  code: ApiErrorCode;
  detail: string;
}

export interface BoundingBox {
  min_lon: number;
  min_lat: number;
  max_lon: number;
  max_lat: number;
}

export interface FireListQuery {
  quality_flag?: QualityFlag;
}

export interface CellsQuery {
  prediction_status?: PredictionStatus;
  priority_class?: PriorityClass;
  weights?: PriorityWeights;
  bbox?: BoundingBox;
}
