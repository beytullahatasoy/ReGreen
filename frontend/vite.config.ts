import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const virtualMockSummaries = "virtual:mock-fire-summaries";
const resolvedVirtualMockSummaries = `\0${virtualMockSummaries}`;
const virtualMockLoaders = "virtual:mock-data-loaders";
const resolvedVirtualMockLoaders = `\0${virtualMockLoaders}`;
const virtualMockDataPrefix = "virtual:mock-data/";
const resolvedVirtualMockDataPrefix = `\0${virtualMockDataPrefix}`;
const virtualMockHukumSozlugu = "virtual:mock-hukum-sozlugu";
const resolvedVirtualMockHukumSozlugu = `\0${virtualMockHukumSozlugu}`;
const virtualMockYanginMetinleri = "virtual:mock-yangin-metinleri";
const resolvedVirtualMockYanginMetinleri = `\0${virtualMockYanginMetinleri}`;
const virtualMockYanginOzetleri = "virtual:mock-yangin-ozetleri";
const resolvedVirtualMockYanginOzetleri = `\0${virtualMockYanginOzetleri}`;

function mockFireSummariesPlugin() {
  const repositoryRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
  const dataDirectory = resolve(repositoryRoot, "sample-data/backend-data");
  return {
    name: "regreen-mock-fire-summaries",
    resolveId(id: string) {
      if (id === virtualMockSummaries) return resolvedVirtualMockSummaries;
      if (id === virtualMockLoaders) return resolvedVirtualMockLoaders;
      if (id === virtualMockHukumSozlugu) return resolvedVirtualMockHukumSozlugu;
      if (id === virtualMockYanginMetinleri) return resolvedVirtualMockYanginMetinleri;
      if (id === virtualMockYanginOzetleri) return resolvedVirtualMockYanginOzetleri;
      if (id.startsWith(virtualMockDataPrefix)) return `\0${id}`;
      return null;
    },
    load(id: string) {
      const manifest = JSON.parse(readFileSync(resolve(dataDirectory, "manifest.json"), "utf8")) as { fires: Array<{ fire_id: string }> };
      if (id === resolvedVirtualMockLoaders) {
        const loaderMap = (kind: "metadata" | "perimeter" | "cells" | "hukum") => `{${manifest.fires.map(({ fire_id }) =>
          `${JSON.stringify(fire_id)}: () => import(${JSON.stringify(`${virtualMockDataPrefix}${kind}/${fire_id}`)}).then(module => module.default)`,
        ).join(",")}}`;
        return [
          `export const metadataLoaders = ${loaderMap("metadata")};`,
          `export const perimeterLoaders = ${loaderMap("perimeter")};`,
          `export const cellLoaders = ${loaderMap("cells")};`,
          `export const hukumLoaders = ${loaderMap("hukum")};`,
        ].join("\n");
      }
      if (id === resolvedVirtualMockHukumSozlugu) {
        return `export default ${readFileSync(resolve(dataDirectory, "hukum_sozlugu.json"), "utf8")};`;
      }
      if (id === resolvedVirtualMockYanginMetinleri) {
        return `export default ${readFileSync(resolve(dataDirectory, "yangin_metinleri.json"), "utf8")};`;
      }
      if (id === resolvedVirtualMockYanginOzetleri) {
        return `export default ${readFileSync(resolve(dataDirectory, "yangin_ozetleri.json"), "utf8")};`;
      }
      if (id.startsWith(resolvedVirtualMockDataPrefix)) {
        const [kind, fireId] = id.slice(resolvedVirtualMockDataPrefix.length).split("/");
        const suffix = kind === "metadata" ? "_metadata.json" : kind === "perimeter" ? "_sinir.geojson" : kind === "cells" ? "_hucreler.csv" : kind === "hukum" ? "_hukumler.csv" : null;
        if (!suffix || !fireId || !manifest.fires.some((fire) => fire.fire_id === fireId)) return null;
        return `export default ${JSON.stringify(readFileSync(resolve(dataDirectory, `${fireId}${suffix}`), "utf8"))};`;
      }
      if (id !== resolvedVirtualMockSummaries) return null;
      const summaries = manifest.fires.map(({ fire_id }) => {
        const metadata = JSON.parse(readFileSync(resolve(dataDirectory, `${fire_id}_metadata.json`), "utf8"));
        const perimeter = JSON.parse(readFileSync(resolve(dataDirectory, `${fire_id}_sinir.geojson`), "utf8"));
        const marker = markerFor(perimeter);
        return {
          fire_id: metadata.fire_id,
          fire_date: metadata.fire_date,
          province: metadata.province,
          region: metadata.region,
          modis_area_ha: metadata.modis_area_ha,
          burned_area_ha: metadata.burned_area_ha,
          cell_count: metadata.cell_count,
          has_perimeter: metadata.has_perimeter,
          marker_lat: marker.lat,
          marker_lon: marker.lon,
          quality_flag: metadata.quality_flag,
          quality_note: metadata.quality_note,
        };
      });
      return `export default ${JSON.stringify(summaries)};`;
    },
  };
}

function markerFor(feature: { geometry: { type: "Polygon" | "MultiPolygon"; coordinates: any[] } }) {
  const rings: number[][][] = feature.geometry.type === "Polygon"
    ? [feature.geometry.coordinates[0] ?? []]
    : feature.geometry.coordinates.map((polygon: number[][][]) => polygon[0] ?? []);
  const ring = rings.sort((a, b) => ringArea(b) - ringArea(a))[0] ?? [];
  const points = ring.slice(0, -1);
  const divisor = Math.max(points.length, 1);
  return {
    lon: points.reduce((sum, point) => sum + Number(point[0]), 0) / divisor,
    lat: points.reduce((sum, point) => sum + Number(point[1]), 0) / divisor,
  };
}

function ringArea(ring: number[][]): number {
  return Math.abs(ring.reduce((sum, point, index) => {
    const next = ring[(index + 1) % ring.length] ?? point;
    return sum + Number(point[0]) * Number(next[1]) - Number(next[0]) * Number(point[1]);
  }, 0) / 2);
}

export default defineConfig({
  plugins: [react(), mockFireSummariesPlugin()],
});
