import { useState } from 'react'
import { Menu, X } from 'lucide-react'

const BOLUMLER = [
  { id: 'sorun', ad: 'Problem' },
  { id: 'yontem', ad: 'Method' },
  { id: 'test', ad: 'Feasibility' },
  { id: 'bulgular', ad: 'Findings' },
]

export default function Nav() {
  const [acik, setAcik] = useState(false)

  return (
    <nav className="fixed top-0 left-0 right-0 z-[100] flex items-center justify-between p-4 sm:p-5">
      <a href="#top" className="flex items-center gap-2.5 group">
        <svg width="26" height="26" viewBox="0 0 256 256" fill="#ffffff" aria-hidden="true">
          <path d="M 256 256 L 128 256 L 0 128 L 128 128 Z M 256 128 L 128 128 L 0 0 L 128 0 Z" />
        </svg>
        <span className="text-white text-2xl font-playfair italic">ReGreen</span>
      </a>

      <div className="hidden md:flex absolute left-1/2 -translate-x-1/2 bg-white/20 backdrop-blur-md border border-white/30 rounded-full px-2 py-2 items-center gap-1">
        {BOLUMLER.map((b, i) => (
          <a
            key={b.id}
            href={`#${b.id}`}
            className={
              i === 0
                ? 'text-white px-4 py-1.5 rounded-full text-sm font-medium bg-white/20'
                : 'text-white/80 px-4 py-1.5 rounded-full text-sm font-medium hover:bg-white/20 hover:text-white transition-colors'
            }
          >
            {b.ad}
          </a>
        ))}
      </div>

      <a
        href="#sirada"
        className="hidden md:block bg-white text-gray-900 text-sm font-semibold px-6 py-2.5 rounded-full hover:bg-gray-100 transition-colors"
      >
        What's next
      </a>

      <button
        type="button"
        onClick={() => setAcik((v) => !v)}
        aria-label={acik ? 'Close menu' : 'Open menu'}
        aria-expanded={acik}
        className="md:hidden text-white p-2 -mr-2 rounded-full hover:bg-white/15 transition-colors"
      >
        {acik ? <X size={22} /> : <Menu size={22} />}
      </button>

      {acik && (
        <div className="md:hidden absolute top-full left-4 right-4 mt-1 bg-black/85 backdrop-blur-md border border-white/15 rounded-2xl p-2 flex flex-col">
          {[
            ...BOLUMLER,
            { id: 'recovery', ad: 'Recovery Zone' },
            { id: 'gonullu', ad: 'Volunteer Journey' },
            { id: 'sirada', ad: "What's next" },
          ].map((b) => (
            <a
              key={b.id}
              href={`#${b.id}`}
              onClick={() => setAcik(false)}
              className="text-white/85 hover:text-white hover:bg-white/10 px-4 py-2.5 rounded-xl text-sm font-medium transition-colors"
            >
              {b.ad}
            </a>
          ))}
        </div>
      )}
    </nav>
  )
}
