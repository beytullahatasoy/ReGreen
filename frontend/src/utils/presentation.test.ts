import { describe, expect, it } from "vitest";
import { formatDecimal, formatMeters, predictionStatusLabels } from "./presentation";

describe("presentation helpers", () => {
  it("distinguishes missing source values from unavailable application data", () => {
    expect(formatDecimal(null)).toBe("Missing in source data");
    expect(formatMeters(null)).toBe("Missing in source data");
  });

  it("uses explicit labels for cells that are intentionally not scored", () => {
    expect(predictionStatusLabels.low_severity).toBe("Not Prioritized");
    expect(predictionStatusLabels.no_data).toBe("Insufficient Data");
  });
});
