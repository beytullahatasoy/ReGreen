import Nav from './components/Nav'
import Hero from './components/Hero'
import { Sorun, Yontem, Test, Bulgular, Sirada, Footer } from './components/Sections'
import { RecoveryZone, GonulluYolculugu } from './components/Sosyal'

export default function App() {
  return (
    <div
      id="top"
      className="min-h-screen bg-[#0b0b0c] tracking-[-0.02em]"
      style={{ fontFamily: "'Inter', sans-serif" }}
    >
      <Nav />
      <Hero />
      <Sorun />
      <Yontem />
      <Test />
      <Bulgular />
      <RecoveryZone />
      <GonulluYolculugu />
      <Sirada />
      <Footer />
    </div>
  )
}
