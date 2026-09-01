import { describe, expect, it } from "vitest";
import { MockFireService } from "./MockFireService";

describe("MockFireService", () => {
  it("exposes all fires declared by the current backend data package", async () => {
    const service = new MockFireService();

    const fires = await service.getFires();

    expect(fires).toHaveLength(53);
  });

  it.each([
    { recovery: 0, erosion: 0, access: 0 },
    { recovery: -0.1, erosion: 0.6, access: 0.5 },
    { recovery: Number.NaN, erosion: 0.5, access: 0.5 },
    { recovery: Number.POSITIVE_INFINITY, erosion: 0.5, access: 0.5 },
    { recovery: 1e308, erosion: 1e308, access: 0 },
  ])("rejects invalid custom weights like the API: $recovery/$erosion/$access", async (weights) => {
    const service = new MockFireService();

    await expect(service.getCells("AKD_2021_01", { weights })).rejects.toMatchObject({
      problem: { status: 400, code: "INVALID_PRIORITY_WEIGHTS" },
    });
  });

  it("returns ridge_v2 metadata and recalculates predicted cells with custom weights", async () => {
    const service = new MockFireService();
    const weights = { recovery: 0.4, erosion: 0.35, access: 0.25 };

    const response = await service.getCells("AKD_2021_05", { weights });
    const cell = response.items.find((item) => item.prediction_status === "predicted");

    expect(response.model_version).toBe("ridge_v2");
    expect(response.normalization_reference.recovery_gap_pred.min).toBeLessThan(
      response.normalization_reference.recovery_gap_pred.max,
    );
    expect(response.priority_thresholds.YUKSEK).toBe(0.5);
    expect(cell?.priority_score).not.toBeNull();
    expect(cell?.priority_class).not.toBeNull();
  });

  it("returns a cell verdict parsed from the hukumler CSV, including quoted comma-bearing fields", async () => {
    const service = new MockFireService();

    const response = await service.getCells("AKD_2021_01");
    const cells = response.items;
    const verdict = await service.getCellVerdict("AKD_2021_01", cells[0]!.cell_id);

    expect(verdict.cell_id).toBe(cells[0]!.cell_id);
    expect(verdict.hukum).toBeTruthy();
    expect(Array.isArray(verdict.ek_kosullar)).toBe(true);
  });

  it("rejects an unknown cell id when fetching a verdict", async () => {
    const service = new MockFireService();

    await expect(service.getCellVerdict("AKD_2021_01", "does-not-exist")).rejects.toThrow();
  });

  it("returns the fire narrative paragraph for a known fire", async () => {
    const service = new MockFireService();

    const narrative = await service.getFireNarrative("AKD_2021_01");

    expect(narrative.fire_id).toBe("AKD_2021_01");
    expect(narrative.paragraf.length).toBeGreaterThan(0);
    expect(narrative.uretim).toBeTruthy();
    expect(narrative.sayi_blogu.buyukluk?.hucre).toBeGreaterThan(0);
    expect(narrative.sayi_blogu.hukum_dagilimi?.ONCELIGE_GORE).toBeTypeOf("number");
  });

  it("returns the global hukum dictionary with all 7 verdict definitions", async () => {
    const service = new MockFireService();

    const sozluk = await service.getHukumSozlugu();

    expect(sozluk.hukumler).toHaveLength(7);
    expect(sozluk.tur_tablosu.onaylandi).toBe(false);
  });
});
