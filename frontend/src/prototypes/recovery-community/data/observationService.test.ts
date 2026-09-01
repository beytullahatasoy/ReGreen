import { beforeEach, describe, expect, it, vi } from "vitest";
import { observationService } from "./observationService";

const girdi = {
  fireId: "AKD_2021_01",
  activityId: "activity-3",
  location: "Manavgat access point",
  photoName: null,
  answers: ["Vegetation: sparse"],
};

describe("observationService", () => {
  beforeEach(() => observationService.clearLocal());

  it("demo kayitlar her zaman listede kalir", () => {
    const hepsi = observationService.list();
    expect(hepsi.length).toBeGreaterThan(0);
    expect(hepsi.every((o) => o.origin === "demo")).toBe(true);
  });

  it("gonderim Pending olarak en uste dusar", () => {
    const kayit = observationService.submit(girdi);
    const hepsi = observationService.list();
    expect(hepsi[0]?.id).toBe(kayit.id);
    expect(kayit.status).toBe("Pending");
    expect(kayit.fireId).toBe("AKD_2021_01");
    // Hangi alana ait oldugu tasinmazsa kurum kuyrukta ne oldugunu bilemez.
    expect(kayit.activityId).toBe("activity-3");
  });

  it("inceleme karari durumu degistirir", () => {
    const kayit = observationService.submit(girdi);
    expect(observationService.review(kayit.id, "Accepted as supporting evidence")).toBe(true);
    expect(observationService.mine()[0]?.status).toBe("Accepted as supporting evidence");
  });

  it("demo kayitlar degistirilemez", () => {
    const demo = observationService.list().find((o) => o.origin === "demo")!;
    expect(observationService.review(demo.id, "Needs clarification")).toBe(false);
  });

  it("abone her degisiklikte haberdar olur", () => {
    const dinleyici = vi.fn();
    const birak = observationService.subscribe(dinleyici);
    observationService.submit(girdi);
    expect(dinleyici).toHaveBeenCalledTimes(1);
    birak();
    observationService.submit(girdi);
    expect(dinleyici).toHaveBeenCalledTimes(1);
  });

  it("degisiklik olmadan ayni referansi dondurur", () => {
    // useSyncExternalStore bunu sart kosuyor; yoksa sonsuz render dongusu.
    expect(observationService.list()).toBe(observationService.list());
    observationService.submit(girdi);
    expect(observationService.list()).not.toBe([]);
    expect(observationService.list()).toBe(observationService.list());
  });

  it("clearLocal yalnizca kendi gonderimlerini siler", () => {
    observationService.submit(girdi);
    const demoSayisi = observationService.list().filter((o) => o.origin === "demo").length;
    observationService.clearLocal();
    expect(observationService.mine()).toHaveLength(0);
    expect(observationService.list()).toHaveLength(demoSayisi);
  });
});
