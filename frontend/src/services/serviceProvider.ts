import type { CommunityService } from "./CommunityService";
import type { FireService } from "./FireService";
import { HttpCommunityService } from "./HttpCommunityService";
import { HttpFireService } from "./HttpFireService";
import { MockFireService } from "./MockFireService";

export type ServiceMode = "mock" | "http";

export interface ServiceConfig {
  mode: ServiceMode;
  apiBaseUrl: string;
}

export function createFireService(config: ServiceConfig): FireService {
  return config.mode === "http" ? new HttpFireService(config.apiBaseUrl) : new MockFireService();
}

export const fireService = createFireService({
  mode: import.meta.env.VITE_SERVICE_MODE ?? "mock",
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? "",
});

// Topluluk katmanının (etkinlik/gönüllü/gözlem) mock karşılığı yok — bu veri
// bir tarayıcı prototipi değil, gerçek kurum ve gönüllü kayıtları. Her zaman
// gerçek API'ye gider.
export const communityService: CommunityService = new HttpCommunityService(
  import.meta.env.VITE_API_BASE_URL ?? "",
);
