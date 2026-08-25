/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string;
  readonly VITE_SERVICE_MODE?: "mock" | "http";
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}

declare module "*?raw" {
  const content: string;
  export default content;
}

declare module "virtual:mock-fire-summaries" {
  import type { FireSummary } from "./types";
  const summaries: FireSummary[];
  export default summaries;
}

declare module "virtual:mock-data-loaders" {
  type RawLoader = () => Promise<string>;
  export const metadataLoaders: Record<string, RawLoader>;
  export const perimeterLoaders: Record<string, RawLoader>;
  export const cellLoaders: Record<string, RawLoader>;
}
