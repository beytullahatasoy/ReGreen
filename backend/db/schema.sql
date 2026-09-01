-- ReGreen — Veritabanı Şeması (MS SQL Server)
-- Kaynak: docs/db-schema.md v1.4 (tasarım gerekçeleri orada)
-- Dayanak: docs/data-contract.md v1.5, docs/hukum_sozlesmesi.md + Teknik Mimari Planı v4.1 §7 DDL
-- Kapsam: Faz 1/2 çekirdek (Fires, Cells, ModelRuns, Predictions) + hüküm katmanı
-- (CellVerdicts, FireNarratives, HukumSozlugu — docs/db-schema.md §9). Faz 3 (Zone/Campaign/
-- Community/Volunteer/FieldObservation) bu dosyanın DIŞINDA.
--
-- ⚡ işaretli satırlar PDF v4.1'in DDL'ine göre değişiklik/eklemedir — gerekçesi
-- docs/db-schema.md'de ilgili tabloya bakın.
--
-- v1.1 notu: bu paket için HasPerimeter HER ZAMAN 1'dir (53/53 doğrulandı) — perimeter'ı
-- opsiyonel yapıp marker'ı da nullable etmek YARIM bir özellik olurdu (marker perimeter'sız
-- nasıl üretilecek sorusu cevapsız kalırdı). Bu yüzden v1.0'daki nullable PerimeterGeoJson
-- geri alındı; HasPerimeter kolonu KALDI (contract'ta gerçek bir alan) ama şu an için
-- sabit 1 olmaya zorlanıyor. `false` desteği gerçekten gerekirse o zaman marker fallback'i
-- (ör. hücrelerin bounding box'ından) ayrıca tasarlanmalı.

CREATE TABLE Fires (
    FireId              NVARCHAR(50)   NOT NULL PRIMARY KEY,
    FireDate            DATE           NOT NULL,
    Province            NVARCHAR(100)  NOT NULL,
    Region              NVARCHAR(100)  NOT NULL,
    ModisAreaHa         FLOAT          NOT NULL,
    BurnedAreaHa        FLOAT          NOT NULL,
    CellSizeM           INT            NOT NULL,                        -- ⚡ default kaldırıldı (v1.1) — metadata'dan explicit gelir

    HasPerimeter        BIT            NOT NULL,                        -- ⚡ default kaldırıldı (v1.1) — bkz. üstteki not
    PerimeterGeoJson    NVARCHAR(MAX)  NOT NULL,                        -- ⚡ v1.1: NOT NULL'a geri alındı (bu paket için)

    -- Marker: PerimeterGeoJson'dan representative_point()/InteriorPoint ile import
    -- sırasında hesaplanır, saklanır (centroid KULLANILMAZ — bkz. data-contract §7.2)
    MarkerLat           FLOAT          NOT NULL,
    MarkerLon           FLOAT          NOT NULL,

    QualityFlag         NVARCHAR(10)   NOT NULL,                        -- ⚡ default kaldırıldı (v1.1) — metadata'dan explicit gelir
    QualityNote         NVARCHAR(300)  NULL,

    CONSTRAINT CK_Fires_QualityFlag CHECK (QualityFlag IN ('ok', 'check')),
    CONSTRAINT CK_Fires_HasPerimeter CHECK (HasPerimeter = 1)           -- ⚡ v1.1: bu paket için sabit 1, bkz. üstteki not
);

CREATE TABLE Cells (
    CellId              NVARCHAR(50)   NOT NULL PRIMARY KEY,             -- global benzersiz, doğrulandı
    FireId              NVARCHAR(50)   NOT NULL
                         FOREIGN KEY REFERENCES Fires(FireId),

    -- Kare geometri saklanmaz; API cevabında CenterLat/Lon + Fires.CellSizeM'den
    -- anlık üretilir (data-contract §7.1)
    CenterLat           FLOAT          NOT NULL,
    CenterLon           FLOAT          NOT NULL,

    -- Hücrenin SABİT özellikleri (model girdisi) — model tekrar çalışsa bile
    -- değişmez, bu yüzden Predictions'ta değil burada:
    TreeCover           FLOAT          NOT NULL,
    TreeCoverAnnual      FLOAT          NULL,                             -- 1.681/37.163 satırda NULL, doğrulandı
    BurnSeverityDnbr     FLOAT          NOT NULL,
    SlopeDeg             FLOAT          NOT NULL,
    ElevationM           FLOAT          NULL,                             -- 22/37.163 satırda NULL, doğrulandı
    RoadDistanceKm       FLOAT          NOT NULL,

    -- Sadece gösterim (ndvi_drop ayrıca prediction_status'u belirlemekte kullanılıyor,
    -- data-contract §6.1) — 37.163 satırın TAMAMI tarandı, hiçbiri NULL değil:
    NdviBefore          FLOAT          NOT NULL,
    NdviAfter           FLOAT          NOT NULL,
    NdviDrop            FLOAT          NOT NULL,
    SeverityClass       NVARCHAR(30)   NOT NULL,
    LandCover           NVARCHAR(50)   NULL,                             -- 1.681/37.163 satırda NULL (TreeCoverAnnual ile aynı satırlar)

    CONSTRAINT CK_Cells_SeverityClass CHECK (
        SeverityClass IN ('dusuk', 'orta-dusuk', 'orta-yuksek', 'yuksek')
    ),
    CONSTRAINT CK_Cells_LandCover CHECK (
        LandCover IS NULL OR LandCover IN
        ('Agaclik', 'Ciplak', 'Otlak/calilik', 'Su', 'Sulak alan', 'Tarim', 'Yerlesim')
    ),

    -- Predictions'tan (FireId, CellId) composite FK'siyle referans alınabilmesi için
    -- (bkz. Predictions tablosu) — CellId zaten tek başına benzersiz, bu sadece o
    -- benzersizliği FireId ile birlikte de görünür kılıyor:
    CONSTRAINT UQ_Cells_FireId_CellId UNIQUE (FireId, CellId)            -- ⚡ yeni (v1.1)
);

-- Her AI teslimatındaki HER YANGIN için bir satır (normalization_reference fire'a
-- özel, bkz. data-contract §11). Aynı teslimatın 53 satırı ModelVersion+GeneratedAt
-- ile eşleşir.
CREATE TABLE ModelRuns (
    Id                    INT           IDENTITY PRIMARY KEY,
    FireId                NVARCHAR(50)  NOT NULL
                          FOREIGN KEY REFERENCES Fires(FireId),
    ModelVersion          NVARCHAR(50)  NOT NULL,
    GeneratedAt           DATETIMEOFFSET(0) NOT NULL,                  -- ⚡ v1.2: kaynak ISO 8601 offset'ini kayıpsız saklar
    SchemaVersion         NVARCHAR(20)  NOT NULL,

    InTrainingSet         BIT           NOT NULL,
    OutOfFoldCells        INT           NOT NULL,                       -- ⚡ default kaldırıldı (v1.1) — metadata'dan explicit gelir

    NormRecoveryGapMin    FLOAT         NOT NULL,
    NormRecoveryGapMax    FLOAT         NOT NULL,
    NormSlopeMin          FLOAT         NOT NULL,
    NormSlopeMax          FLOAT         NOT NULL,
    NormRoadDistMin       FLOAT         NOT NULL,
    NormRoadDistMax       FLOAT         NOT NULL,

    DefaultWeightRecovery FLOAT         NOT NULL,                       -- ⚡ default kaldırıldı (v1.1) — metadata'dan explicit gelir
    DefaultWeightErosion  FLOAT         NOT NULL,                       -- ⚡ aynı
    DefaultWeightAccess   FLOAT         NOT NULL,                       -- ⚡ aynı

    -- Eşikler metadata'dan: bir ModelRun'ın hangi sınırlarla YUKSEK/ORTA ürettiğini
    -- kalıcı kaydeder. rf_v2 farklı eşikle gelirse geçmiş ModelRun doğru yorumlanır.
    ThresholdVeryHigh     FLOAT         NOT NULL,                       -- ⚡ default kaldırıldı (v1.1) — metadata'dan explicit gelir
    ThresholdHigh         FLOAT         NOT NULL,                       -- ⚡ aynı
    ThresholdMedium       FLOAT         NOT NULL,                       -- ⚡ aynı

    ImportedAt            DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),  -- teslim paketinden gelmez, DB üretir — tek DEFAULT bu

    CONSTRAINT CK_ModelRuns_OutOfFoldCells CHECK (OutOfFoldCells >= 0),
    CONSTRAINT CK_ModelRuns_OutOfFold_InTraining CHECK (                -- ⚡ yeni (v1.1)
        InTrainingSet = 1 OR OutOfFoldCells = 0
    ),
    CONSTRAINT CK_ModelRuns_Weights CHECK (                             -- ⚡ yeni (v1.1)
        DefaultWeightRecovery >= 0 AND DefaultWeightErosion >= 0 AND DefaultWeightAccess >= 0
        AND (DefaultWeightRecovery + DefaultWeightErosion + DefaultWeightAccess) > 0
    ),
    CONSTRAINT CK_ModelRuns_NormRanges CHECK (                          -- ⚡ yeni (v1.1)
        NormRecoveryGapMin <= NormRecoveryGapMax
        AND NormSlopeMin <= NormSlopeMax
        AND NormRoadDistMin <= NormRoadDistMax
    ),
    CONSTRAINT CK_ModelRuns_Thresholds CHECK (                          -- ⚡ yeni (v1.1)
        ThresholdMedium >= 0
        AND ThresholdMedium < ThresholdHigh
        AND ThresholdHigh < ThresholdVeryHigh
        AND ThresholdVeryHigh <= 1
    ),

    -- Aynı manifest yanlışlıkla iki kez import edilirse ikinci deneme burada patlar,
    -- veri çoğalmaz (idempotency, PDF §8.1):
    CONSTRAINT UQ_ModelRuns UNIQUE (FireId, ModelVersion, GeneratedAt),

    -- Predictions'tan (FireId, ModelRunId) composite FK'siyle referans alınabilmesi
    -- için (bkz. Predictions tablosu) — Id zaten tek başına benzersiz (IDENTITY):
    CONSTRAINT UQ_ModelRuns_FireId_Id UNIQUE (FireId, Id)                -- ⚡ yeni (v1.1)
);

CREATE INDEX IX_ModelRuns_FireId ON ModelRuns (FireId, GeneratedAt DESC); -- "en güncel run" sorgusu için

-- KATMAN 1: AI'nin ürettiği tahmin. Insert-only, model tekrar çalışınca yeni satır.
-- SlopeDeg/RoadDistanceKm BURADA YOK — onlar hücrenin özelliği, Cells'te.
--
-- FireId burada DENORMALİZE saklanıyor (v1.1) — sebep: CellId ve ModelRunId ayrı ayrı
-- FK olsaydı, AKD yangınına ait bir hücre yanlışlıkla EGE yangınının ModelRun'ına
-- bağlanabilirdi ve DB bunu sessizce kabul ederdi. FireId'yi composite FK'lerin bir
-- parçası yaparak (Cells ve ModelRuns'daki (FireId, ...) UNIQUE kısıtlarına referansla)
-- "hücre ve model run aynı yangına ait olmalı" kuralı DB seviyesinde garanti ediliyor.
CREATE TABLE Predictions (
    Id                    INT           IDENTITY PRIMARY KEY,
    FireId                NVARCHAR(50)  NOT NULL,                       -- ⚡ yeni (v1.1), denormalize — bkz. üstteki not
    CellId                NVARCHAR(50)  NOT NULL,
    ModelRunId            INT           NOT NULL,

    PredictionStatus      NVARCHAR(20)  NOT NULL,                        -- predicted / low_severity / no_data
    RecoveryGapPred       FLOAT         NULL,                            -- predicted dışında NULL
    DefaultPriorityScore  FLOAT         NULL,                            -- AI'nin varsayılan ağırlıkla hesapladığı
    DefaultPriorityClass  NVARCHAR(20)  NULL,

    -- ⚡ v1.1: 3 ayrı CHECK yerine TEK birleşik CHECK — data-contract §6.2 "KESİN TABLO"yu
    -- tam olarak birebir uygular (skorun low_severity'de tam 0.0, sınıfın tam 'DUSUK'
    -- olması dahil). predicted durumunda skor/sınıf DEĞERİ (hangi eşiğe göre hangi sınıf)
    -- ModelRun'a göre değişken olduğu için burada değil, import katmanında doğrulanır —
    -- bu CHECK sadece "predicted ise üçü de dolu olmalı" der.
    CONSTRAINT CK_Predictions_StatusConsistency CHECK (
        (PredictionStatus = 'predicted'
            AND RecoveryGapPred IS NOT NULL
            AND DefaultPriorityScore IS NOT NULL
            AND DefaultPriorityClass IS NOT NULL)
        OR (PredictionStatus = 'low_severity'
            AND RecoveryGapPred IS NULL
            AND DefaultPriorityScore IS NOT NULL
            AND DefaultPriorityScore = 0.0
            AND DefaultPriorityClass IS NOT NULL
            AND DefaultPriorityClass = 'DUSUK')
        OR (PredictionStatus = 'no_data'
            AND RecoveryGapPred IS NULL
            AND DefaultPriorityScore IS NULL
            AND DefaultPriorityClass IS NULL)
    ),
    CONSTRAINT CK_Predictions_Status CHECK (
        PredictionStatus IN ('predicted', 'low_severity', 'no_data')
    ),
    CONSTRAINT CK_Predictions_RecoveryGapPred_Range CHECK (              -- data-contract §6.4 doğrulama aralığı
        RecoveryGapPred IS NULL OR RecoveryGapPred BETWEEN -0.5 AND 1.5
    ),
    CONSTRAINT CK_Predictions_PriorityScore_Range CHECK (                -- Math.Clamp(x,0,1) DB'de de garanti
        DefaultPriorityScore IS NULL OR DefaultPriorityScore BETWEEN 0 AND 1
    ),
    CONSTRAINT CK_Predictions_PriorityClass_Enum CHECK (                 -- predicted durumunda gerçek enum'a sınırlar
        DefaultPriorityClass IS NULL OR DefaultPriorityClass IN
        ('COK_YUKSEK', 'YUKSEK', 'ORTA', 'DUSUK')
    ),

    -- Aynı ModelRun içinde bir hücre için tek satır olmalı; ikinci import denemesi
    -- burada patlar, kopya veri oluşmaz (idempotency, PDF §8.1):
    CONSTRAINT UQ_Predictions UNIQUE (CellId, ModelRunId),

    -- ⚡ v1.1: composite FK'ler — "hücre ve model run aynı yangına ait olmalı" kuralını
    -- DB seviyesinde garanti eder (bkz. üstteki tablo notu):
    CONSTRAINT FK_Predictions_Cell FOREIGN KEY (FireId, CellId)
        REFERENCES Cells (FireId, CellId),
    CONSTRAINT FK_Predictions_ModelRun FOREIGN KEY (FireId, ModelRunId)
        REFERENCES ModelRuns (FireId, Id)
);

-- ⚡ v1.1: eski IX_Predictions_CellId_ModelRunId KALDIRILDI — UNIQUE (CellId, ModelRunId)
-- zaten aynı sırayla bir unique indeks oluşturuyordu, birebir tekrardı.
-- API akışı önce ilgili yangının en güncel ModelRunId'sini bulup o run'ın TÜM
-- tahminlerini çekecek (PDF §9), bu yüzden ModelRunId önde olan indeks daha faydalı:
CREATE INDEX IX_Predictions_ModelRunId_CellId
    ON Predictions (ModelRunId, CellId);

-- ⚡ v1.3: composite FK'ler ((FireId,CellId) ve (FireId,ModelRunId)) eklendiğinde EF Core
-- migration'ı bu iki kolon-setini otomatik indeksledi (FK enforcement/JOIN performansı
-- için standart konvansiyon). Zararsız ve faydalı oldukları için schema.sql'e de eklendi
-- — böylece "EF migration ile schema.sql birebir aynı" iddiası gerçekten doğru olur:
CREATE INDEX IX_Predictions_FireId_CellId ON Predictions (FireId, CellId);
CREATE INDEX IX_Predictions_FireId_ModelRunId ON Predictions (FireId, ModelRunId);

-- TASARIM KARARI (PDF v4.1'den korunuyor):
-- priority_score/priority_class KALICI olarak saklanmıyor (sadece AI'nin varsayılan
-- hesabı DefaultPriorityScore/Class'ta duruyor, referans/ilk-yükleme için).
-- Kullanıcı ağırlık değiştirdiğinde API bunu Cells (SlopeDeg/RoadDistanceKm) +
-- Predictions (RecoveryGapPred) + ModelRuns'daki normalizasyon referansından ANLIK
-- hesaplar, DB'ye yazmaz.
--
-- İMPORT NOTU (v1.1): Cells.FireId == ModelRuns.FireId kuralı artık composite FK ile
-- DB seviyesinde garanti; ama import script yine de Predictions satırı yazmadan önce
-- CSV'nin fire_id'siyle ilgili ModelRun'ın FireId'sinin aynı olduğunu kontrol etmeli —
-- composite FK sadece YANLIŞ eşleşmeyi INSERT anında reddeder, önceden anlamlı bir
-- hata mesajıyla durdurmaz (ham FK constraint hatası olarak sızar).

-- ============================================================================
-- HÜKÜM KATMANI (docs/db-schema.md §9, docs/hukum_sozlesmesi.md) — AI ekibinin
-- ayrı, opsiyonel "hüküm katmanı" teslimatı. Mevcut Faz 1/2 tablolarına (yukarıda)
-- hiçbir değişiklik yapmaz, tamamen katmanlı ekleme (AddHukumKatmani migration'ı).
-- ============================================================================

-- Yangına özgü değildir; sözlük sürümü başına bir satır tutulur. Aynı sürüm farklı
-- içerikle tekrar teslim edilirse importer reddeder, yeni sürüm geçmişi bozmaz.
CREATE TABLE HukumSozlugu (
    Surum               NVARCHAR(20)   NOT NULL PRIMARY KEY,
    JsonIcerik          NVARCHAR(MAX)  NOT NULL,
    ImportedAt          DATETIMEOFFSET NOT NULL DEFAULT SYSUTCDATETIME()
);

-- Hücre + ModelRun başına opsiyonel hüküm. Ağırlık kaydırıcısından bağımsızdır;
-- recovery_gap_pred kullandığı için model teslimatından bağımsız DEĞİLDİR.
CREATE TABLE CellVerdicts (
    CellId              NVARCHAR(50)   NOT NULL,
    FireId              NVARCHAR(50)   NOT NULL,
    ModelRunId          INT            NOT NULL,
    HukumSozluguSurum   NVARCHAR(20)   NOT NULL,

    Hukum               NVARCHAR(20)   NOT NULL,
    EkKosullar          NVARCHAR(120)  NULL,                             -- '|' ile ayrılmış 0..6 kod, boşsa NULL
    ToparlanmaOrani     FLOAT          NULL,
    TurOnerisi          NVARCHAR(500)  NULL,                             -- onaylanmamış örnek veri, bkz. HukumSozlugu.JsonIcerik -> tur_tablosu.onaylandi
    Tetikleyen          NVARCHAR(300)  NOT NULL,
    Ozet                NVARCHAR(MAX)  NOT NULL,
    Ayrinti             NVARCHAR(MAX)  NOT NULL,
    ZamanlamaNotuVar    BIT            NOT NULL,

    CONSTRAINT CK_CellVerdicts_Hukum CHECK (
        Hukum IN ('KAPSAM_DISI', 'SAHA_KONTROL', 'IZLE', 'EROZYON_ONCE',
                  'DIKIM_ADAYI', 'ONCELIGE_GORE', 'GENCLESME_IZLE')
    ),
    CONSTRAINT CK_CellVerdicts_ToparlanmaOrani_Range CHECK (
        ToparlanmaOrani IS NULL OR ToparlanmaOrani BETWEEN 0 AND 1
    ),
    CONSTRAINT PK_CellVerdicts PRIMARY KEY (CellId, ModelRunId, HukumSozluguSurum),
    CONSTRAINT FK_CellVerdicts_Cell FOREIGN KEY (FireId, CellId)
        REFERENCES Cells (FireId, CellId),
    CONSTRAINT FK_CellVerdicts_ModelRun FOREIGN KEY (FireId, ModelRunId)
        REFERENCES ModelRuns (FireId, Id),
    CONSTRAINT FK_CellVerdicts_HukumSozlugu FOREIGN KEY (HukumSozluguSurum)
        REFERENCES HukumSozlugu (Surum)
);

CREATE INDEX IX_CellVerdicts_FireId_CellId ON CellVerdicts (FireId, CellId);
CREATE INDEX IX_CellVerdicts_FireId_ModelRunId ON CellVerdicts (FireId, ModelRunId);
CREATE INDEX IX_CellVerdicts_ModelRunId_CellId ON CellVerdicts (ModelRunId, CellId);
CREATE INDEX IX_CellVerdicts_HukumSozluguSurum ON CellVerdicts (HukumSozluguSurum);

-- ModelRun başına opsiyonel yangın anlatısı.
CREATE TABLE FireNarratives (
    ModelRunId          INT            NOT NULL,
    FireId              NVARCHAR(50)   NOT NULL FOREIGN KEY REFERENCES Fires(FireId),
    NarrativeVersion    NVARCHAR(20)   NOT NULL,

    Paragraf            NVARCHAR(MAX)  NOT NULL,
    Profil              NVARCHAR(30)   NOT NULL,
    Onaylandi           BIT            NOT NULL,
    Uretim              NVARCHAR(100)  NOT NULL,                         -- paket genelinde sabit (yangin_metinleri.json üst seviye 'uretim')
    SayiBlogu           NVARCHAR(MAX)  NOT NULL,                         -- yangin_ozetleri.json'daki ham sayı bloğu, opak JSON

    CONSTRAINT PK_FireNarratives PRIMARY KEY (ModelRunId, NarrativeVersion),
    CONSTRAINT CK_FireNarratives_Profil CHECK (
        Profil IN ('yogun_mudahale', 'karisik', 'kendi_toparlaniyor',
                   'dik_arazi', 'belirsiz', 'kapsam_dar')
    ),
    CONSTRAINT FK_FireNarratives_ModelRun FOREIGN KEY (FireId, ModelRunId)
        REFERENCES ModelRuns (FireId, Id)
);
CREATE INDEX IX_FireNarratives_FireId_ModelRunId
    ON FireNarratives (FireId, ModelRunId);
