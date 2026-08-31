import { useEffect, useRef } from "react";

interface Props { title: string; message: string; onClose: () => void; }

export function PrototypeModal({ title, message, onClose }: Props) {
  const closeRef = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null;
    closeRef.current?.focus();
    const onKey = (event: KeyboardEvent) => event.key === "Escape" && onClose();
    document.addEventListener("keydown", onKey);
    return () => { document.removeEventListener("keydown", onKey); previous?.focus(); };
  }, [onClose]);
  return <div className="rc-modal-backdrop" role="presentation" onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
    <section className="rc-modal" role="dialog" aria-modal="true" aria-labelledby="prototype-modal-title">
      <button ref={closeRef} className="rc-modal__close" onClick={onClose} aria-label="Close dialog">×</button>
      <span className="rc-kicker">Demo workflow</span>
      <h2 id="prototype-modal-title">{title}</h2><p>{message}</p>
      <button className="rc-button rc-button--primary" onClick={onClose}>Understood</button>
    </section>
  </div>;
}
