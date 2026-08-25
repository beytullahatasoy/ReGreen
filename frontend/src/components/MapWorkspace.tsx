import { useEffect, useRef, useState } from "react";
import maplibregl, { type ExpressionSpecification, type GeoJSONSource, type Map as MapLibreMap, type Marker } from "maplibre-gl";
import type { Feature, FeatureCollection, GeoJsonProperties, Geometry, Position } from "geojson";
import type { Cell, FirePerimeter } from "../types";
import { cellsToGeoJson } from "../utils/cellGeometry";
import { Legend, type MapLayer } from "./Legend";

const style = {
  version: 8 as const,
  sources: { osm: { type: "raster" as const, tiles: ["https://tile.openstreetmap.org/{z}/{x}/{y}.png"], tileSize: 256, attribution: "© OpenStreetMap contributors" } },
  layers: [{ id: "osm", type: "raster" as const, source: "osm", paint: { "raster-opacity": 0.82, "raster-saturation": -0.72, "raster-contrast": -0.08, "raster-brightness-min": 0.14, "raster-brightness-max": 0.94 } }],
};

interface Props { perimeter: FirePerimeter | null; cells: Cell[]; cellSizeM: number; layer: MapLayer; loading: boolean; loadingMessage: string; error: string | null; selectedCell: Cell | null; scenarioHighlights: { increased: Set<string>; decreased: Set<string> }; scenarioFeedback: string | null; onSelectCell: (cell: Cell) => void }

export function MapWorkspace({ perimeter, cells, cellSizeM, layer, loading, loadingMessage, error, selectedCell, scenarioHighlights, scenarioFeedback, onSelectCell }: Props) {
  const containerRef = useRef<HTMLDivElement>(null);
  const mapRef = useRef<MapLibreMap | null>(null);
  const markerRef = useRef<Marker | null>(null);
  const cellsRef = useRef(cells);
  const [ready, setReady] = useState(false);
  cellsRef.current = cells;

  useEffect(() => {
    if (!containerRef.current || mapRef.current) return;
    const map = new maplibregl.Map({ container: containerRef.current, style, center: [35, 38.5], zoom: 5.2, attributionControl: false });
    map.addControl(new maplibregl.NavigationControl({ showCompass: false }), "top-right");
    map.addControl(new maplibregl.AttributionControl({ compact: true }), "bottom-right");
    map.on("load", () => setReady(true));
    map.on("click", "cells-fill", (event) => {
      const id = event.features?.[0]?.properties?.cell_id as string | undefined;
      const cell = cellsRef.current.find((item) => item.cell_id === id);
      if (cell) onSelectCell(cell);
    });
    map.on("mouseenter", "cells-fill", () => { map.getCanvas().style.cursor = "pointer"; });
    map.on("mouseleave", "cells-fill", () => { map.getCanvas().style.cursor = ""; });
    mapRef.current = map;
    return () => { markerRef.current?.remove(); map.remove(); mapRef.current = null; };
  }, [onSelectCell]);

  useEffect(() => {
    const map = mapRef.current;
    if (!map || !ready || !perimeter) return;
    const source = map.getSource("perimeter") as GeoJSONSource | undefined;
    if (source) {
      map.setPaintProperty("perimeter-fill", "fill-opacity", 0);
      map.setPaintProperty("perimeter-line", "line-opacity", 0);
      source.setData(perimeter.perimeter);
      window.requestAnimationFrame(() => {
        map.setPaintProperty("perimeter-fill", "fill-opacity", 0.08);
        map.setPaintProperty("perimeter-line", "line-opacity", 0.9);
      });
    } else addPerimeter(map, perimeter.perimeter);
    markerRef.current?.remove();
    markerRef.current = new maplibregl.Marker({ color: "#0d2b22", scale: 0.72 }).setLngLat([perimeter.marker.lon, perimeter.marker.lat]).addTo(map);
    const bounds = boundsFor(perimeter.perimeter);
    if (!bounds.isEmpty()) map.fitBounds(bounds, { padding: 72, duration: reducedMotion() ? 0 : 720, maxZoom: 13 });
  }, [perimeter, ready]);

  useEffect(() => {
    const map = mapRef.current;
    if (!map || !ready) return;
    const data = cellsToGeoJson(cells, cellSizeM);
    const source = map.getSource("cells") as GeoJSONSource | undefined;
    if (!source) {
      addCells(map, data);
      return;
    }
    if (reducedMotion()) {
      source.setData(data);
      return;
    }
    map.setPaintProperty("cells-fill", "fill-opacity", 0.2);
    const timer = window.setTimeout(() => {
      source.setData(data);
      map.setPaintProperty("cells-fill", "fill-opacity", 0.77);
    }, 130);
    return () => window.clearTimeout(timer);
  }, [cells, cellSizeM, ready]);

  useEffect(() => {
    const map = mapRef.current;
    if (!map || !ready) return;
    const source = map.getSource("scenario-changes") as GeoJSONSource | undefined;
    if (!source) return;
    const changed = cells.filter((cell) => scenarioHighlights.increased.has(cell.cell_id) || scenarioHighlights.decreased.has(cell.cell_id));
    const data = cellsToGeoJson(changed, cellSizeM);
    data.features.forEach((feature) => {
      const id = String(feature.properties?.cell_id ?? "");
      if (feature.properties) feature.properties.scenario_direction = scenarioHighlights.increased.has(id) ? "increased" : "decreased";
    });
    if (reducedMotion()) {
      source.setData(data);
      return;
    }
    const hasChanges = changed.length > 0;
    map.setPaintProperty("scenario-increased", "line-opacity", 0);
    map.setPaintProperty("scenario-decreased", "line-opacity", 0);
    const timer = window.setTimeout(() => {
      source.setData(data);
      map.setPaintProperty("scenario-increased", "line-opacity", hasChanges ? 0.9 : 0);
      map.setPaintProperty("scenario-decreased", "line-opacity", hasChanges ? 0.8 : 0);
    }, hasChanges ? 20 : 160);
    return () => window.clearTimeout(timer);
  }, [cells, cellSizeM, scenarioHighlights, ready]);

  useEffect(() => {
    const map = mapRef.current;
    if (!map || !ready || !map.getLayer("cells-fill")) return;
    map.setPaintProperty("cells-fill", "fill-color", colorExpression(layer));
  }, [layer, ready]);

  useEffect(() => {
    const map = mapRef.current;
    if (!map || !ready || !map.getLayer("cells-selected")) return;
    map.setFilter("cells-selected", selectedCell ? ["==", ["get", "cell_id"], selectedCell.cell_id] : ["==", ["get", "cell_id"], ""]);
    if (!selectedCell || reducedMotion()) return;
    map.setPaintProperty("cells-selected", "line-width", 5);
    map.setPaintProperty("cells-selected", "line-opacity", 0.95);
    const timer = window.setTimeout(() => map.setPaintProperty("cells-selected", "line-width", 2.5), 260);
    return () => window.clearTimeout(timer);
  }, [selectedCell, ready]);

  return <section className="map-stage" aria-label="Fire recovery map">
    <div className="map-canvas" ref={containerRef} />
    {loading && <><div className="map-loading-line" /><div className="map-loading-overlay"><span>{loadingMessage}</span></div></>}
    {!loading && error && <div className="map-status"><div className="map-status__box map-status__box--error"><strong>Data unavailable</strong><span>{error}</span></div></div>}
    {!loading && !error && !perimeter && <div className="map-status"><div className="map-status__box"><strong>Select a fire area</strong><span>The perimeter and recovery cells will appear here.</span></div></div>}
    <div className="cell-count">{cells.length.toLocaleString()} LOADED CELLS</div>
    {scenarioFeedback && <div className="scenario-feedback" role="status">{scenarioFeedback}</div>}
    <Legend layer={layer} />
  </section>;
}

function addPerimeter(map: MapLibreMap, data: Feature<Geometry, GeoJsonProperties>) {
  map.addSource("perimeter", { type: "geojson", data });
  map.addLayer({ id: "perimeter-fill", type: "fill", source: "perimeter", paint: { "fill-color": "#315f4c", "fill-opacity": 0.08, "fill-opacity-transition": { duration: 320 } } });
  map.addLayer({ id: "perimeter-line", type: "line", source: "perimeter", paint: { "line-color": "#173f30", "line-width": 2, "line-opacity": 0.9, "line-opacity-transition": { duration: 320 } } });
}

function addCells(map: MapLibreMap, data: FeatureCollection) {
  map.addSource("cells", { type: "geojson", data });
  map.addLayer({ id: "cells-fill", type: "fill", source: "cells", paint: { "fill-color": colorExpression("priority"), "fill-opacity": 0.77, "fill-outline-color": "rgba(255,255,255,.28)", "fill-color-transition": { duration: 320 }, "fill-opacity-transition": { duration: 280 } } });
  map.addSource("scenario-changes", { type: "geojson", data: { type: "FeatureCollection", features: [] } });
  map.addLayer({ id: "scenario-increased", type: "line", source: "scenario-changes", filter: ["==", ["get", "scenario_direction"], "increased"], paint: { "line-color": "#8f241b", "line-width": 2.7, "line-opacity": 0, "line-opacity-transition": { duration: reducedMotion() ? 0 : 180 } } });
  map.addLayer({ id: "scenario-decreased", type: "line", source: "scenario-changes", filter: ["==", ["get", "scenario_direction"], "decreased"], paint: { "line-color": "#39756c", "line-width": 2.4, "line-opacity": 0, "line-opacity-transition": { duration: reducedMotion() ? 0 : 180 } } });
  map.addLayer({ id: "cells-selected", type: "line", source: "cells", filter: ["==", ["get", "cell_id"], ""], paint: { "line-color": "#102c23", "line-width": 2.5, "line-opacity": 0.95, "line-width-transition": { duration: 280 }, "line-opacity-transition": { duration: 180 } } });
}

function reducedMotion(): boolean {
  return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

function colorExpression(layer: MapLayer): ExpressionSpecification {
  if (layer === "status") return ["match", ["get", "prediction_status"], "predicted", "#347557", "low_severity", "#c49a3b", "no_data", "#7b827e", "#7b827e"];
  if (layer === "severity") return ["match", ["get", "severity_class"], "yuksek", "#a63c2f", "orta-yuksek", "#d66d3f", "orta-dusuk", "#d7a949", "dusuk", "#7f9d75", "#7b827e"];
  return ["case", ["==", ["get", "prediction_status"], "no_data"], "#7b827e", ["match", ["get", "priority_class"], "COK_YUKSEK", "#b42318", "YUKSEK", "#d75b20", "ORTA", "#c58a12", "DUSUK", "#438360", "#7b827e"]];
}

function boundsFor(feature: Feature): maplibregl.LngLatBounds {
  const bounds = new maplibregl.LngLatBounds();
  const visit = (coordinates: unknown): void => {
    if (Array.isArray(coordinates) && coordinates.length >= 2 && typeof coordinates[0] === "number" && typeof coordinates[1] === "number") bounds.extend([coordinates[0], coordinates[1]]);
    else if (Array.isArray(coordinates)) coordinates.forEach(visit);
  };
  if ("coordinates" in feature.geometry) visit(feature.geometry.coordinates);
  return bounds;
}
