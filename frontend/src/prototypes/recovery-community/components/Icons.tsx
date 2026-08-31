type IconProps = { name: "zone" | "activity" | "people" | "report" | "follow" | "observe" | "recovery" | "arrow" };

export function Icon({ name }: IconProps) {
  const paths = {
    zone: <><path d="M12 21s6-5.1 6-11a6 6 0 1 0-12 0c0 5.9 6 11 6 11Z"/><circle cx="12" cy="10" r="2"/></>,
    activity: <><path d="M12 22V10M7 15c-3 0-5-2-5-5 3 0 5 2 5 5Zm5-4c0-4 2.5-7 7-7 0 4-2.5 7-7 7Z"/><path d="M8 22h8"/></>,
    people: <><circle cx="9" cy="8" r="3"/><path d="M3 20c0-4 2-6 6-6s6 2 6 6M16 4a3 3 0 0 1 0 6M17 14c2.7.4 4 2.4 4 5"/></>,
    report: <><path d="M6 3h9l3 3v15H6z"/><path d="M14 3v4h4M9 12h6M9 16h6"/></>,
    follow: <><path d="M12 21s-7-4.6-7-11a4 4 0 0 1 7-2.6A4 4 0 0 1 19 10c0 6.4-7 11-7 11Z"/></>,
    observe: <><path d="M2 12s4-6 10-6 10 6 10 6-4 6-10 6S2 12 2 12Z"/><circle cx="12" cy="12" r="2.5"/></>,
    recovery: <><path d="M4 14a8 8 0 1 0 2-7M4 4v5h5"/><path d="m9 14 2 2 4-5"/></>,
    arrow: <><path d="M5 12h14M14 7l5 5-5 5"/></>,
  };
  return <svg className="rc-icon" viewBox="0 0 24 24" aria-hidden="true" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round">{paths[name]}</svg>;
}
