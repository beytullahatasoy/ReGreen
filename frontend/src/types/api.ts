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

export interface NormRange {
  min: number;
  max: number;
}

export interface NormalizationReference {
  recovery_gap_pred: NormRange;
  slope_deg: NormRange;
  road_distance_km: NormRange;
}

export interface PriorityThresholds {
  COK_YUKSEK: number;
  YUKSEK: number;
  ORTA: number;
}

export interface CellsResponse {
  fire_id: string;
  model_run_id: number;
  model_version: string;
  generated_at: string;
  crs: "EPSG:4326";
  cell_size_m: number;
  applied_weights: PriorityWeights;
  normalization_reference: NormalizationReference;
  priority_thresholds: PriorityThresholds;
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
  | "UNEXPECTED_ERROR"
  | "CELL_NOT_FOUND"
  | "CELL_VERDICT_NOT_FOUND"
  | "FIRE_NARRATIVE_NOT_FOUND"
  | "HUKUM_SOZLUGU_NOT_FOUND";

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

export type HukumKod =
  | "KAPSAM_DISI"
  | "SAHA_KONTROL"
  | "IZLE"
  | "EROZYON_ONCE"
  | "DIKIM_ADAYI"
  | "ONCELIGE_GORE"
  | "GENCLESME_IZLE";

export type EkKosulKod =
  | "AGIR_YANMIS"
  | "ESKIDEN_ORMAN_DEGIL"
  | "ERISIM_ZOR"
  | "DUSUK_GUVEN"
  | "DIK_YAMAC"
  | "SEYREK_ORTU";

export interface CellVerdict {
  cell_id: string;
  model_run_id: number;
  model_version: string;
  generated_at: string;
  hukum_version: string;
  hukum: HukumKod;
  ek_kosullar: EkKosulKod[];
  toparlanma_orani: number | null;
  tur_onerisi: string | null;
  tetikleyen: string;
  ozet: string;
  ayrinti: string;
  zamanlama_notu_var: boolean;
}

export type FireNarrativeProfile =
  | "yogun_mudahale"
  | "karisik"
  | "kendi_toparlaniyor"
  | "dik_arazi"
  | "belirsiz"
  | "kapsam_dar";

export interface FireNarrative {
  fire_id: string;
  model_run_id: number;
  model_version: string;
  generated_at: string;
  narrative_version: string;
  paragraf: string;
  profil: FireNarrativeProfile;
  onaylandi: boolean;
  uretim: string;
  sayi_blogu: FireNarrativeNumbers;
}

export interface FireNarrativeNumbers {
  buyukluk?: {
    hucre: number;
    alan_ha: number;
    tahminli_hucre: number;
  };
  hukum_dagilimi?: Partial<Record<HukumKod, number>>;
  guven?: {
    seviye: string;
    bolgedeki_referans_yangini: number;
    kaynak: string;
  };
}

export interface HukumTanimi {
  kod: HukumKod;
  baslik: string;
  sira: number;
}

export interface HukumEsikler {
  toparlanma_zayif: number;
  toparlanma_iyi: number;
  egim_dik_derece: number;
  yol_uzak_km: number;
  ortu_seyrek: number;
  kapsam_disi_arazi: LandCover[];
  dusuk_guven_bolge: string[];
}

export interface TurTablosu {
  kaynak: string;
  surum: string;
  onaylandi: boolean;
}

export interface HukumSozlugu {
  surum: string;
  dil: string;
  hukumler: HukumTanimi[];
  ek_kosullar: Record<EkKosulKod, string>;
  zamanlama_notu: string;
  esikler: HukumEsikler;
  tur_tablosu: TurTablosu;
  aciklama: string;
}
