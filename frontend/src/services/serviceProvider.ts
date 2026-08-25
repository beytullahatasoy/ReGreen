import type { FireService } from "./FireService";
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
