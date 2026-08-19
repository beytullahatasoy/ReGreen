import type { ReactNode } from 'react'

export function Bolum({ id, children }: { id?: string; children: ReactNode }) {
  return (
    <section id={id} className="px-5 sm:px-8 py-24 sm:py-28 border-t border-white/[0.09]">
      <div className="max-w-6xl mx-auto">{children}</div>
    </section>
  )
}

export function Baslik({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <h2 className={`text-white text-[2rem] sm:text-[2.6rem] lg:text-[3rem] font-normal
      leading-[1.09] tracking-[-0.035em] text-balance pb-1 ${className}`}>
      {children}
    </h2>
  )
}

export function It({ children }: { children: ReactNode }) {
  return <span className="font-playfair italic leading-[1.15]">{children}</span>
}

export function Metin({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <p className={`text-white/60 text-[16.5px] leading-[1.72] max-w-[62ch] ${className}`}>
      {children}
    </p>
  )
}

export function V({ children }: { children: ReactNode }) {
  return <strong className="text-white/95 font-medium">{children}</strong>
}
