using Xunit;

// Bir test koşusundaki entegrasyon testleri aynı benzersiz LocalDB veritabanını paylaşıyor —
// paralel çalışırsa testler birbirinin verisini görür/bozar. Bu ilk test paketi için
// basitlik/güvenilirlik adına paralellik tamamen kapatıldı.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
