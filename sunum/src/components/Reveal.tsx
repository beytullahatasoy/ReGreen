import { useEffect, useRef, useState, type ReactNode } from 'react'

/** Kaydirma ile gorunur olunca iceriği yumusakca yukari suzer. */
export default function Reveal({
  children,
  delay = 0,
  className = '',
}: {
  children: ReactNode
  delay?: number
  className?: string
}) {
  const ref = useRef<HTMLDivElement>(null)
  const [gorunur, setGorunur] = useState(false)

  useEffect(() => {
    const el = ref.current
    if (!el) return
    const gozlemci = new IntersectionObserver(
      ([giris]) => {
        if (giris.isIntersecting) {
          setGorunur(true)
          gozlemci.disconnect()
        }
      },
      { threshold: 0.12, rootMargin: '0px 0px -60px 0px' },
    )
    gozlemci.observe(el)
    return () => gozlemci.disconnect()
  }, [])

  return (
    <div
      ref={ref}
      className={`reveal-on-scroll ${gorunur ? 'is-visible' : ''} ${className}`}
      style={{ transitionDelay: `${delay}ms` }}
    >
      {children}
    </div>
  )
}
