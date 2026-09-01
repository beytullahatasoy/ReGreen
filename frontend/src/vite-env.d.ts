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
  export const hukumLoaders: Record<string, RawLoader>;
}

declare module "virtual:mock-hukum-sozlugu" {
  import type { HukumSozlugu } from "./types";
  const sozluk: HukumSozlugu;
  export default sozluk;
}

declare module "virtual:mock-yangin-metinleri" {
  import type { FireNarrativeProfile } from "./types";
  interface YanginMetniRaw {
    paragraf: string;
    profil: FireNarrativeProfile;
    kaynak: string;
    onaylandi: boolean;
  }
  interface YanginMetinleriRaw {
    surum: string;
    dil: string;
    uretim: string;
    yanginlar: Record<string, YanginMetniRaw>;
  }
  const data: YanginMetinleriRaw;
  export default data;
}

declare module "virtual:mock-yangin-ozetleri" {
  import type { FireNarrativeNumbers } from "./types";
  interface YanginOzetleriRaw {
    surum: string;
    yanginlar: Record<string, FireNarrativeNumbers>;
  }
  const data: YanginOzetleriRaw;
  export default data;
}
