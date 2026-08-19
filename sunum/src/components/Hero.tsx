import { useEffect, useRef, useState } from 'react'

/** Taban katman: kavrulmus, kor halindeki arazi */
const BG_IMAGE_1 =
  'https://images.higgs.ai/?default=1&output=webp&url=https%3A%2F%2Fd8j0ntlcm91z4.cloudfront.net%2Fuser_38xzZboKViGWJOttwIXH07lWA1P%2Fhf_20260609_195923_b0ba8ace-1d1d-4f2c-9a28-1ab84b330680.png&w=1280&q=85'
/** Isikla acilan katman: ayni arazi, yesermis hali */
const BG_IMAGE_2 =
  'https://images.higgs.ai/?default=1&output=webp&url=https%3A%2F%2Fd8j0ntlcm91z4.cloudfront.net%2Fuser_38xzZboKViGWJOttwIXH07lWA1P%2Fhf_20260609_201152_bba90a12-bf12-459f-91f0-51f237dbaf3b.png&w=1280&q=85'

const SPOTLIGHT_R = 260

/**
 * Imlecin altinda yumusak dairesel bir maske acar ve ALTTAKI goruntuyu
 * degil, USTTEKI ikinci goruntuyu gorunur kilar. Maske her karede canvas'a
 * radial-gradient olarak cizilip data URL'e cevrilir.
 */
function RevealLayer({
  image,
  cursorX,
  cursorY,
}: {
  image: string
  cursorX: number
  cursorY: number
}) {
  const canvasRef = useRef<HTMLCanvasElement>(null)
  // Maske React state'i olarak degil, dogrudan DOM'a yaziliyor.
  // State kullanilirsa her karede fazladan bir render turu doguyor ve
  // React "Maximum update depth" uyarisi veriyor.
  const katmanRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    const boyutla = () => {
      const c = canvasRef.current
      if (!c) return
      c.width = window.innerWidth
      c.height = window.innerHeight
    }
    boyutla()
    window.addEventListener('resize', boyutla)
    return () => window.removeEventListener('resize', boyutla)
  }, [])

  useEffect(() => {
    const c = canvasRef.current
    if (!c) return
    const ctx = c.getContext('2d')
    if (!ctx) return

    ctx.clearRect(0, 0, c.width, c.height)

    const g = ctx.createRadialGradient(cursorX, cursorY, 0, cursorX, cursorY, SPOTLIGHT_R)
    g.addColorStop(0, 'rgba(255,255,255,1)')
    g.addColorStop(0.4, 'rgba(255,255,255,1)')
    g.addColorStop(0.6, 'rgba(255,255,255,0.75)')
    g.addColorStop(0.75, 'rgba(255,255,255,0.4)')
    g.addColorStop(0.88, 'rgba(255,255,255,0.12)')
    g.addColorStop(1, 'rgba(255,255,255,0)')

    ctx.fillStyle = g
    ctx.beginPath()
    ctx.arc(cursorX, cursorY, SPOTLIGHT_R, 0, Math.PI * 2)
    ctx.fill()

    const url = c.toDataURL()
    const k = katmanRef.current
    if (k) {
      k.style.setProperty('mask-image', `url(${url})`)
      k.style.setProperty('-webkit-mask-image', `url(${url})`)
    }
  }, [cursorX, cursorY])

  return (
    <>
      <canvas
        ref={canvasRef}
        className="absolute inset-0 pointer-events-none"
        style={{ display: 'none' }}
      />
      <div
        ref={katmanRef}
        className="absolute inset-0 bg-center bg-cover bg-no-repeat z-30 pointer-events-none"
        style={{
          backgroundImage: `url('${image}')`,
          maskSize: '100% 100%',
          WebkitMaskSize: '100% 100%',
          maskRepeat: 'no-repeat',
          WebkitMaskRepeat: 'no-repeat',
        }}
      />
    </>
  )
}

export default function Hero() {
  const mouse = useRef({ x: -999, y: -999 })
  const smooth = useRef({ x: -999, y: -999 })
  const rafRef = useRef<number>()
  const [cursorPos, setCursorPos] = useState({ x: -999, y: -999 })

  useEffect(() => {
    const izle = (e: MouseEvent) => {
      mouse.current.x = e.clientX
      mouse.current.y = e.clientY
    }
    // Dokunmatik cihazda mousemove yok; parmak da isigi tasisin,
    // yoksa ikinci goruntu telefonda hic gorunmez.
    const dokunus = (e: TouchEvent) => {
      const t = e.touches[0]
      if (!t) return
      mouse.current.x = t.clientX
      mouse.current.y = t.clientY
    }
    window.addEventListener('mousemove', izle)
    window.addEventListener('touchstart', dokunus, { passive: true })
    window.addEventListener('touchmove', dokunus, { passive: true })

    const dongu = () => {
      const dx = mouse.current.x - smooth.current.x
      const dy = mouse.current.y - smooth.current.y
      // Lerp hedefe asla tam ulasmaz; esik olmazsa imlec dururken bile
      // her karede state yazilir ve maske bosuna yeniden uretilir.
      if (Math.abs(dx) > 0.4 || Math.abs(dy) > 0.4) {
        smooth.current.x += dx * 0.1
        smooth.current.y += dy * 0.1
        setCursorPos({ x: smooth.current.x, y: smooth.current.y })
      }
      rafRef.current = requestAnimationFrame(dongu)
    }
    rafRef.current = requestAnimationFrame(dongu)

    return () => {
      window.removeEventListener('mousemove', izle)
      window.removeEventListener('touchstart', dokunus)
      window.removeEventListener('touchmove', dokunus)
      if (rafRef.current) cancelAnimationFrame(rafRef.current)
    }
  }, [])

  return (
    <section
      className="relative w-full overflow-hidden h-screen bg-black"
      style={{ height: '100dvh' }}
    >
      {/* 1 - taban katman: yangindan sonra */}
      <div
        className="absolute inset-0 bg-center bg-cover bg-no-repeat z-10 hero-zoom"
        style={{ backgroundImage: `url('${BG_IMAGE_1}')` }}
      />

      {/* 2 - spotlight ile acilan katman: yangindan once */}
      <RevealLayer image={BG_IMAGE_2} cursorX={cursorPos.x} cursorY={cursorPos.y} />

      {/* okunabilirlik icin ust ve alt karartma */}
      <div
        className="absolute inset-0 z-40 pointer-events-none"
        style={{
          background:
            'linear-gradient(to bottom, rgba(0,0,0,.62) 0%, rgba(0,0,0,.18) 38%, rgba(0,0,0,.30) 65%, rgba(0,0,0,.78) 100%)',
        }}
      />

      {/* 3 - baslik */}
      <div className="absolute top-[14%] left-0 right-0 z-50 flex flex-col items-center text-center px-5 pointer-events-none">
        <h1 className="text-white leading-[0.95]">
          <span
            className="block font-playfair italic font-normal text-5xl sm:text-7xl md:text-8xl hero-anim hero-reveal"
            style={{ letterSpacing: '-0.05em', animationDelay: '0.25s' }}
          >
            Beneath the ash
          </span>
          <span
            className="block font-normal text-5xl sm:text-7xl md:text-8xl -mt-1 hero-anim hero-reveal"
            style={{ letterSpacing: '-0.08em', animationDelay: '0.42s' }}
          >
            what did we lose?
          </span>
        </h1>
      </div>

      {/* 4 - sol alt paragraf */}
      <div
        className="hidden sm:block absolute bottom-14 left-10 md:left-14 max-w-[260px] z-50 hero-anim hero-fade"
        style={{ animationDelay: '0.7s' }}
      >
        <p className="text-sm text-white/80 leading-relaxed">
          Move your cursor. Where the light falls, the land turns green — that is
          exactly what the system does: predict which parts of a burned area
          can still come back.
        </p>
      </div>

      {/* 5 - sag alt blok */}
      <div
        className="absolute bottom-10 sm:bottom-24 left-5 right-5 sm:left-auto sm:right-10 md:right-14 max-w-full sm:max-w-[260px] z-50 flex flex-col items-start gap-4 sm:gap-5 hero-anim hero-fade"
        style={{ animationDelay: '0.85s' }}
      >
        <p className="text-xs sm:text-sm text-white/80 leading-relaxed">
          After a fire, resources are limited and burned land is vast.
          ReGreen uses satellite data to recommend which area
          should be addressed first.
        </p>
        <a
          href="#sorun"
          className="bg-[#e8702a] hover:bg-[#d2611f] text-white text-sm font-medium px-7 py-3 rounded-full transition-all hover:scale-[1.03] active:scale-95 hover:shadow-lg hover:shadow-[#e8702a]/30"
        >
          See how it works
        </a>
      </div>

    </section>
  )
}
