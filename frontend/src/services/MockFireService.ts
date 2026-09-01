import type { Feature, MultiPolygon, Polygon, Position } from "geojson";
import { cellLoaders, hukumLoaders, metadataLoaders, perimeterLoaders } from "virtual:mock-data-loaders";
import mockFireSummaries from "virtual:mock-fire-summaries";
import mockHukumSozlugu from "virtual:mock-hukum-sozlugu";
import mockYanginMetinleri from "virtual:mock-yangin-metinleri";
import mockYanginOzetleri from "virtual:mock-yangin-ozetleri";
import type {
  Cell,
  CellsQuery,
  CellsResponse,
  CellVerdict,
  EkKosulKod,
  FireListQuery,
  FireNarrative,
  FirePerimeter,
  FirePerimeterProperties,
  FireSummary,
  HukumKod,
  HukumSozlugu,
  LandCover,
  PredictionStatus,
  PriorityClass,
  SeverityClass,
} from "../types";
import { ApiError } from "./ApiError";
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
  model_version: string;
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

type RawLoader = () => Promise<string>;
type RawCellVerdict = Omit<CellVerdict, "model_run_id" | "model_version" | "generated_at" | "hukum_version">;

const numberOrNull = (value: string): number | null => value === "" ? null : Number(value);
const normalize = (value: number, range: { min: number; max: number }) =>
  range.max - range.min >= 1e-9 ? Math.min(1, Math.max(0, (value - range.min) / (range.max - range.min))) : 0.5;

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

function parseCsvLine(line: string): string[] {
  const values: string[] = [];
  let current = "";
  let inQuotes = false;
  for (let i = 0; i < line.length; i++) {
    const char = line[i];
    if (inQuotes) {
      if (char === '"' && line[i + 1] === '"') { current += '"'; i++; }
      else if (char === '"') { inQuotes = false; }
      else current += char;
    } else if (char === '"') {
      inQuotes = true;
    } else if (char === ",") {
      values.push(current);
      current = "";
    } else {
      current += char;
    }
  }
  values.push(current);
  return values;
}

function parseVerdicts(csv: string): RawCellVerdict[] {
  const [headerLine, ...lines] = csv.trim().split(/\r?\n/);
  if (!headerLine) return [];
  const headers = parseCsvLine(headerLine);
  return lines.filter(Boolean).map((line) => {
    const values = parseCsvLine(line);
    const row = Object.fromEntries(headers.map((header, index) => [header, values[index] ?? ""]));
    return {
      cell_id: row.cell_id ?? "",
      hukum: row.hukum as HukumKod,
      ek_kosullar: row.ek_kosullar ? row.ek_kosullar.split("|").filter(Boolean) as EkKosulKod[] : [],
      toparlanma_orani: numberOrNull(row.toparlanma_orani ?? ""),
      tur_onerisi: row.tur_onerisi || null,
      tetikleyen: row.tetikleyen ?? "",
      ozet: row.ozet ?? "",
      ayrinti: row.ayrinti ?? "",
      zamanlama_notu_var: (row.zamanlama_notu_var ?? "").toLowerCase() === "true",
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
  private readonly metadataCache = new Map<string, Promise<Metadata>>();
  private readonly perimeterCache = new Map<string, Promise<Feature<Polygon | MultiPolygon, FirePerimeterProperties>>>();
  private readonly cellsCache = new Map<string, Promise<Cell[]>>();
  private readonly verdictsCache = new Map<string, Promise<RawCellVerdict[]>>();

  async getFires(query: FireListQuery = {}): Promise<FireSummary[]> {
    return mockFireSummaries.filter((fire) => !query.quality_flag || fire.quality_flag === query.quality_flag);
  }

  async getPerimeter(fireId: string): Promise<FirePerimeter> {
    this.assertKnownFire(fireId);
    const perimeter = await this.loadPerimeter(fireId);
    return { fire_id: fireId, marker: markerFor(perimeter), perimeter };
  }

  async getCells(fireId: string, query: CellsQuery = {}): Promise<CellsResponse> {
    this.assertKnownFire(fireId);
    if (query.weights) validatePriorityWeights(query.weights);
    const [metadata, cells] = await Promise.all([this.loadMetadata(fireId), this.loadCells(fireId)]);
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
    return {
      fire_id: fireId,
      model_run_id: 0,
      model_version: metadata.model_version,
      generated_at: metadata.generated_at,
      crs: metadata.crs,
      cell_size_m: metadata.cell_size_m,
      applied_weights,
      normalization_reference: metadata.normalization_reference,
      priority_thresholds: metadata.priority_thresholds,
      count: items.length,
      items,
    };
  }

  async getCellVerdict(fireId: string, cellId: string): Promise<CellVerdict> {
    this.assertKnownFire(fireId);
    const [verdicts, metadata] = await Promise.all([this.loadVerdicts(fireId), this.loadMetadata(fireId)]);
    const verdict = verdicts.find((item) => item.cell_id === cellId);
    if (!verdict) throw new Error(`Cell verdict not found: ${cellId} (fire ${fireId})`);
    return {
      ...verdict,
      model_run_id: 0,
      model_version: metadata.model_version,
      generated_at: metadata.generated_at,
      hukum_version: mockHukumSozlugu.surum,
    };
  }

  async getFireNarrative(fireId: string): Promise<FireNarrative> {
    this.assertKnownFire(fireId);
    const [metadata, entry] = await Promise.all([
      this.loadMetadata(fireId),
      Promise.resolve(mockYanginMetinleri.yanginlar[fireId]),
    ]);
    if (!entry) throw new Error(`Fire narrative not found: ${fireId}`);
    return {
      fire_id: fireId,
      model_run_id: 0,
      model_version: metadata.model_version,
      generated_at: metadata.generated_at,
      narrative_version: mockYanginMetinleri.surum,
      paragraf: entry.paragraf,
      profil: entry.profil,
      onaylandi: entry.onaylandi,
      uretim: mockYanginMetinleri.uretim,
      sayi_blogu: mockYanginOzetleri.yanginlar[fireId] ?? {},
    };
  }

  async getHukumSozlugu(surum?: string): Promise<HukumSozlugu> {
    if (surum && surum !== mockHukumSozlugu.surum)
      throw new Error(`Decision dictionary not found: ${surum}`);
    return mockHukumSozlugu;
  }

  private assertKnownFire(fireId: string): void {
    if (!mockFireSummaries.some((fire) => fire.fire_id === fireId)) throw new Error(`Fire not found: ${fireId}`);
  }

  private loadVerdicts(fireId: string): Promise<RawCellVerdict[]> {
    return cached(this.verdictsCache, fireId, async () => parseVerdicts(await requireLoader(hukumLoaders, fireId)()));
  }

  private loadMetadata(fireId: string): Promise<Metadata> {
    return cached(this.metadataCache, fireId, async () => JSON.parse(await requireLoader(metadataLoaders, fireId)()) as Metadata);
  }

  private loadPerimeter(fireId: string): Promise<Feature<Polygon | MultiPolygon, FirePerimeterProperties>> {
    return cached(this.perimeterCache, fireId, async () => JSON.parse(await requireLoader(perimeterLoaders, fireId)()) as Feature<Polygon | MultiPolygon, FirePerimeterProperties>);
  }

  private loadCells(fireId: string): Promise<Cell[]> {
    return cached(this.cellsCache, fireId, async () => parseCells(await requireLoader(cellLoaders, fireId)()));
  }
}

function validatePriorityWeights(weights: { recovery: number; erosion: number; access: number }): void {
  const values = [weights.recovery, weights.erosion, weights.access];
  const total = values.reduce((sum, value) => sum + value, 0);
  if (values.some((value) => !Number.isFinite(value) || value < 0) || !Number.isFinite(total) || total <= 0) {
    throw new ApiError({
      type: "https://regreen/errors/invalid-priority-weights",
      title: "Invalid priority weights",
      status: 400,
      code: "INVALID_PRIORITY_WEIGHTS",
      detail: "recovery, erosion and access must be finite, non-negative, and sum to a positive number.",
    });
  }
}

function requireLoader(loaders: Record<string, RawLoader>, fireId: string): RawLoader {
  const loader = loaders[fireId];
  if (!loader) throw new Error(`Mock data file not found for fire: ${fireId}`);
  return loader;
}

function cached<T>(cache: Map<string, Promise<T>>, key: string, load: () => Promise<T>): Promise<T> {
  const existing = cache.get(key);
  if (existing) return existing;
  const pending = load();
  cache.set(key, pending);
  return pending;
}
