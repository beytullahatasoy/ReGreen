export type MapLayer = "priority" | "severity" | "status";

const legends = {
  priority: [["Very high", "#b42318"], ["High", "#d75b20"], ["Medium", "#c58a12"], ["Low", "#438360"], ["Not prioritized", "#c49a3b"], ["No data", "#7b827e"]],
  severity: [["High", "#a63c2f"], ["Medium-high", "#d66d3f"], ["Medium-low", "#d7a949"], ["Low", "#7f9d75"]],
  status: [["Predicted", "#347557"], ["Low severity", "#c49a3b"], ["No data", "#7b827e"]],
};

export function Legend({ layer }: { layer: MapLayer }) {
  return <aside className="legend"><h3>{layer === "priority" ? "Priority class" : layer === "severity" ? "Burn severity" : "Prediction status"}</h3>
    {legends[layer].map(([label, color]) => <div className="legend__item" key={label}><span className="legend__swatch" style={{ background: color }} />{label}</div>)}
  </aside>;
}
