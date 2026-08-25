import type { Feature, FeatureCollection, Polygon } from "geojson";
import type { Cell } from "../types";

export function cellsToGeoJson(cells: Cell[], cellSizeM: number): FeatureCollection<Polygon> {
  return {
    type: "FeatureCollection",
    features: cells.map((cell): Feature<Polygon> => {
      const half = cellSizeM / 2;
      const dLat = half / 110540;
      const dLon = half / (111320 * Math.cos(cell.lat * Math.PI / 180));
      return {
        type: "Feature",
        properties: {
          cell_id: cell.cell_id,
          priority_class: cell.priority_class,
          prediction_status: cell.prediction_status,
          severity_class: cell.severity_class,
        },
        geometry: {
          type: "Polygon",
          coordinates: [[
            [cell.lon - dLon, cell.lat - dLat], [cell.lon + dLon, cell.lat - dLat],
            [cell.lon + dLon, cell.lat + dLat], [cell.lon - dLon, cell.lat + dLat],
            [cell.lon - dLon, cell.lat - dLat],
          ]],
        },
      };
    }),
  };
}
