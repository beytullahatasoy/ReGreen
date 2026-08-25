import type { Feature, MultiPolygon, Polygon, Position } from "geojson";
import akd01Cells from "../../../sample-data/frontend-data/AKD_2021_01_hucreler.csv?raw";
import akd01Metadata from "../../../sample-data/frontend-data/AKD_2021_01_metadata.json?raw";
import akd01Perimeter from "../../../sample-data/frontend-data/AKD_2021_01_sinir.geojson?raw";
import akd05Cells from "../../../sample-data/frontend-data/AKD_2021_05_hucreler.csv?raw";
import akd05Metadata from "../../../sample-data/frontend-data/AKD_2021_05_metadata.json?raw";
import akd05Perimeter from "../../../sample-data/frontend-data/AKD_2021_05_sinir.geojson?raw";
import ege10Cells from "../../../sample-data/frontend-data/EGE_2024_10_hucreler.csv?raw";
import ege10Metadata from "../../../sample-data/frontend-data/EGE_2024_10_metadata.json?raw";
import ege10Perimeter from "../../../sample-data/frontend-data/EGE_2024_10_sinir.geojson?raw";
import type {
  Cell,
  CellsQuery,
  CellsResponse,
  FireListQuery,
  FirePerimeter,
  FirePerimeterProperties,
  FireSummary,
  LandCover,
  PredictionStatus,
  PriorityClass,
  SeverityClass,
} from "../types";
import type { FireService } from "./FireService";

interface Metadata {
  fire_id: string;
  fire_date: string;
  province: string;
  region: string;
  modis_area_ha: number;
  burned_area_ha: number;
  cell_count: number;
  cell_size_m: number;
  crs: "EPSG:4326";
  generated_at: string;
  quality_flag: "ok" | "check";
  quality_note: string | null;
  has_perimeter: boolean;
  priority_weights: { recovery: number; erosion: number; access: number };
  priority_thresholds: { COK_YUKSEK: number; YUKSEK: number; ORTA: number };
  normalization_reference: {
    recovery_gap_pred: { min: number; max: number };
    slope_deg: { min: number; max: number };
    road_distance_km: { min: number; max: number };
  };
}

interface MockSource { metadata: string; perimeter: string; cells: string }

const sources: MockSource[] = [
  { metadata: akd01Metadata, perimeter: akd01Perimeter, cells: akd01Cells },
  { metadata: akd05Metadata, perimeter: akd05Perimeter, cells: akd05Cells },
  { metadata: ege10Metadata, perimeter: ege10Perimeter, cells: ege10Cells },
];

const numberOrNull = (value: string): number | null => value === "" ? null : Number(value);
const normalize = (value: number, range: { min: number; max: number }) =>
  range.max > range.min ? Math.min(1, Math.max(0, (value - range.min) / (range.max - range.min))) : 0;

function parseCells(csv: string): Cell[] {
  const [headerLine, ...lines] = csv.trim().split(/\r?\n/);
  if (!headerLine) return [];
  const headers = headerLine.split(",");
  return lines.filter(Boolean).map((line) => {
    const values = line.split(",");
    const row = Object.fromEntries(headers.map((header, index) => [header.trim(), values[index]?.trim() ?? ""]));
    return {
      cell_id: row.cell_id ?? "",
      lat: Number(row.lat), lon: Number(row.lon), tree_cover: Number(row.tree_cover),
      tree_cover_annual: numberOrNull(row.tree_cover_annual ?? ""),
      burn_severity_dnbr: Number(row.burn_severity_dnbr), slope_deg: Number(row.slope_deg),
      elevation_m: numberOrNull(row.elevation_m ?? ""), road_distance_km: Number(row.road_distance_km),
      ndvi_before: Number(row.ndvi_before), ndvi_after: Number(row.ndvi_after), ndvi_drop: Number(row.ndvi_drop),
      severity_class: row.severity_class as SeverityClass,
      land_cover: (row.land_cover || null) as LandCover | null,
      prediction_status: row.prediction_status as PredictionStatus,
      recovery_gap_pred: numberOrNull(row.recovery_gap_pred ?? ""),
      priority_score: numberOrNull(row.priority_score ?? ""),
      priority_class: (row.priority_class || null) as PriorityClass | null,
    };
  });
}

function outerRings(feature: Feature<Polygon | MultiPolygon>): Position[][] {
  return feature.geometry.type === "Polygon"
    ? [feature.geometry.coordinates[0] ?? []]
    : feature.geometry.coordinates.map((polygon) => polygon[0] ?? []);
}

function ringArea(ring: Position[]): number {
  return Math.abs(ring.reduce((sum, point, index) => {
    const next = ring[(index + 1) % ring.length] ?? point;
    return sum + Number(point[0]) * Number(next[1]) - Number(next[0]) * Number(point[1]);
  }, 0) / 2);
}

function markerFor(feature: Feature<Polygon | MultiPolygon>): { lat: number; lon: number } {
  const ring = outerRings(feature).sort((a, b) => ringArea(b) - ringArea(a))[0] ?? [];
  const points = ring.slice(0, -1);
  const lon = points.reduce((sum, point) => sum + Number(point[0]), 0) / Math.max(points.length, 1);
  const lat = points.reduce((sum, point) => sum + Number(point[1]), 0) / Math.max(points.length, 1);
  return { lat, lon };
}

export class MockFireService implements FireService {
  private readonly records = sources.map((source) => ({
    metadata: JSON.parse(source.metadata) as Metadata,
    perimeter: JSON.parse(source.perimeter) as Feature<Polygon | MultiPolygon, FirePerimeterProperties>,
    cells: parseCells(source.cells),
  }));

  async getFires(query: FireListQuery = {}): Promise<FireSummary[]> {
    return this.records
      .filter(({ metadata }) => !query.quality_flag || metadata.quality_flag === query.quality_flag)
      .map(({ metadata, perimeter }) => ({
        fire_id: metadata.fire_id, fire_date: metadata.fire_date, province: metadata.province,
        region: metadata.region, modis_area_ha: metadata.modis_area_ha,
        burned_area_ha: metadata.burned_area_ha, cell_count: metadata.cell_count,
        has_perimeter: metadata.has_perimeter, marker_lat: markerFor(perimeter).lat,
        marker_lon: markerFor(perimeter).lon, quality_flag: metadata.quality_flag,
        quality_note: metadata.quality_note,
      }));
  }

  async getPerimeter(fireId: string): Promise<FirePerimeter> {
    const record = this.find(fireId);
    return { fire_id: fireId, marker: markerFor(record.perimeter), perimeter: record.perimeter };
  }

  async getCells(fireId: string, query: CellsQuery = {}): Promise<CellsResponse> {
    const { metadata, cells } = this.find(fireId);
    const weights = query.weights ?? metadata.priority_weights;
    const total = weights.recovery + weights.erosion + weights.access;
    const applied_weights = {
      recovery: weights.recovery / total, erosion: weights.erosion / total, access: weights.access / total,
    };
    let items = cells.map((cell) => {
      if (!query.weights || cell.prediction_status !== "predicted" || cell.recovery_gap_pred === null) return cell;
      const ref = metadata.normalization_reference;
      const priority_score = Number((
        applied_weights.recovery * normalize(cell.recovery_gap_pred, ref.recovery_gap_pred) +
        applied_weights.erosion * normalize(cell.slope_deg, ref.slope_deg) +
        applied_weights.access * (1 - normalize(cell.road_distance_km, ref.road_distance_km))
      ).toFixed(4));
      const t = metadata.priority_thresholds;
      const priority_class: PriorityClass = priority_score >= t.COK_YUKSEK ? "COK_YUKSEK" : priority_score >= t.YUKSEK ? "YUKSEK" : priority_score >= t.ORTA ? "ORTA" : "DUSUK";
      return { ...cell, priority_score, priority_class };
    });
    if (query.prediction_status) items = items.filter((cell) => cell.prediction_status === query.prediction_status);
    if (query.priority_class) items = items.filter((cell) => cell.priority_class === query.priority_class);
    if (query.bbox) items = items.filter((cell) => cell.lon >= query.bbox!.min_lon && cell.lon <= query.bbox!.max_lon && cell.lat >= query.bbox!.min_lat && cell.lat <= query.bbox!.max_lat);
    return { fire_id: fireId, model_run_id: 0, generated_at: metadata.generated_at, crs: metadata.crs, cell_size_m: metadata.cell_size_m, applied_weights, count: items.length, items };
  }

  private find(fireId: string) {
    const record = this.records.find(({ metadata }) => metadata.fire_id === fireId);
    if (!record) throw new Error(`Fire not found: ${fireId}`);
    return record;
  }
}
