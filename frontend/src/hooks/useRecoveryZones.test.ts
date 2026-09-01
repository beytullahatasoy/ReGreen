import { describe, expect, it } from "vitest";
import { aciliyeteGore, toplamlar, type RecoveryZone } from "./useRecoveryZones";

function zone(over: Partial<RecoveryZone>): RecoveryZone {
  return {
    fireId: "X", ad: "X", il: "X", bolge: "Ege", tarih: "2021-07-29",
    alanHa: 0, hucre: 0, tahminliHucre: 0,
    mudahaleHucre: 0, mudahaleHa: 0, erozyonHucre: 0,
    siradaHucre: 0, izlemeHucre: 0, kapsamDisiHucre: 0,
    guven: "yuksek", bolgedekiReferans: 13,
    paragraf: "", profil: null, aciliyet: 0, ozetEksik: false,
    ...over,
  };
}

describe("aciliyeteGore", () => {
  it("mudahale yuku agir olani one alir", () => {
    const s = aciliyeteGore([
      zone({ fireId: "hafif", aciliyet: 10 }),
      zone({ fireId: "agir", aciliyet: 900 }),
      zone({ fireId: "orta", aciliyet: 120 }),
    ]);
    expect(s.map((z) => z.fireId)).toEqual(["agir", "orta", "hafif"]);
  });

  it("girdiyi degistirmez", () => {
    const girdi = [zone({ fireId: "a", aciliyet: 1 }), zone({ fireId: "b", aciliyet: 2 })];
    aciliyeteGore(girdi);
    expect(girdi.map((z) => z.fireId)).toEqual(["a", "b"]);
  });
});

describe("toplamlar", () => {
  it("alanlari toplar ve dusuk guvenli bolgeleri sayar", () => {
    const t = toplamlar([
      zone({ mudahaleHa: 100, mudahaleHucre: 16, erozyonHucre: 4, siradaHucre: 50, guven: "yuksek" }),
      zone({ mudahaleHa: 50, mudahaleHucre: 8, erozyonHucre: 2, siradaHucre: 20, guven: "dusuk" }),
      zone({ mudahaleHa: 25, mudahaleHucre: 4, erozyonHucre: 0, siradaHucre: 5, guven: "dusuk" }),
    ]);
    expect(t.mudahaleHa).toBe(175);
    expect(t.mudahaleHucre).toBe(28);
    expect(t.erozyonHucre).toBe(6);
    expect(t.siradaHucre).toBe(75);
    expect(t.dusukGuven).toBe(2);
  });

  it("bos listede sifir doner", () => {
    expect(toplamlar([])).toEqual({
      mudahaleHa: 0, mudahaleHucre: 0, erozyonHucre: 0, siradaHucre: 0, dusukGuven: 0,
    });
  });
});
