<#
.SYNOPSIS
    /api/fires/{id}/cells icin tekrarlanabilir, kucuk bir gecikme/boyut olcumu.

.DESCRIPTION
    docs/api-contract.md #cells endpoint'inin, en buyuk yangin (varsayilan AKD_2021_01,
    9048 hucre) uzerinde ne kadar surdugunu ve sikistirmali/sikistirmasiz yanit boyutunu
    olcer. Sonuc "kişi X'te 100ms gördü" gibi tekrarlanamaz bir iddia olarak kalmasin diye
    -- API'nin kendisi kosulmali (bu script baglanmaz, sadece olcer), sonucu bu script
    uretir, elle kopyalanan sayi degil.

    Olcum icin curl.exe (Windows 10 1803+ / Windows 11'de System32'de hazir gelir, ek
    kurulum gerekmez) kullanilir -- Invoke-WebRequest/System.Net.Http.HttpClient KULLANILMAZ:
    Windows PowerShell 5.1'in (.NET Framework tabanli) HttpClient'i birkaç MB'lik govdelerde
    olcerek dogrulanmis sekilde onlarca kat yavas/yaniltici sonuc veriyor (ayni istek
    curl'de ~80ms, ayni process icinde System.Net.Http.HttpClient ile ~1800-2000ms olculdu
    -- API'nin kendisiyle degil, eski BCL'nin buyuk govde okuma yoluyla ilgili).

    API çalışmıyorsa (curl exit code != 0) veya bir istek 2xx dışı bir durum kodu dönerse
    script HATA VERİR ve durur -- "0 bayt / NaN küçültme" gibi başarılı görünen ama anlamsız
    bir sonuç YAZMAZ (bu, gerçek bir bug olarak bulunup düzeltildi: API kapalıyken curl
    bağlanamadığında script bunu sessizce "0 baytlık bir ölçüm" sayıyordu).

.PARAMETER BaseUrl
    API'nin calistigi taban adres.

.PARAMETER FireId
    Olculecek yangin. Varsayilan: en buyuk yangin (AKD_2021_01, 9048 hucre).

.PARAMETER Query
    /cells'e eklenecek query string (bas '?' olmadan), ör. "recovery=0.8&erosion=0.1&access=0.1".
    Bos birakilirsa varsayilan agirliklarla (DB'deki degerler, yeniden hesap yok) olculur.

.PARAMETER WarmupRequests
    Olcume dahil edilmeyen, once atilan istek sayisi (JIT/connection pool isinmasi icin).

.PARAMETER MeasuredRequests
    Ortalama/min/max hesaplanacak, gercekten olculen istek sayisi.

.EXAMPLE
    # Birinci terminal:
    dotnet run --project backend/ReGreen.Api

    # İkinci terminal:
    ./backend/scripts/perf-check.ps1 -BaseUrl http://localhost:5066

.EXAMPLE
    ./backend/scripts/perf-check.ps1 -FireId AKD_2021_01 -Query "min_lon=31.4&min_lat=36.9&max_lon=31.5&max_lat=37.0"
#>
param(
    [ValidateNotNullOrEmpty()]
    [string]$BaseUrl = "http://localhost:5066",
    [ValidateNotNullOrEmpty()]
    [string]$FireId = "AKD_2021_01",
    [string]$Query = "",
    [ValidateRange(0, 2147483647)]
    [int]$WarmupRequests = 3,
    [ValidateRange(1, 2147483647)]
    [int]$MeasuredRequests = 10
)

$ErrorActionPreference = "Stop"

$curlExe = (Get-Command curl.exe -ErrorAction Stop).Source
$delimiter = "---CURL-EXIT-STATUS---"

# curl.exe'yi verilen argumanlarla calistirir; -w formatini TEK bir yerden (burada)
# kontrol eder ki cagiranin kendi -w'si ile bizim http_code/delimiter ekimiz curl'un
# "sadece son -w gecerli" davranisi yuzunden birbirini EZMESIN. Baglanti hatasinda
# (exit code != 0) veya HTTP 2xx disinda AÇIKÇA hata firlatir.
function Invoke-CurlChecked {
    param([string[]]$CurlArgs, [string]$WriteOutFormat = "")

    $combinedWriteOut = "$WriteOutFormat`n$delimiter`n%{http_code}"
    $fullArgs = $CurlArgs + @("-w", $combinedWriteOut)
    $rawOutput = (& $curlExe @fullArgs 2>&1) -join "`n"
    $exitCode = $LASTEXITCODE

    if ($exitCode -ne 0) {
        throw "curl.exe basarisiz oldu (exit code $exitCode) -- API '$BaseUrl' adresinde calisiyor mu? Cikti: $rawOutput"
    }

    $idx = $rawOutput.LastIndexOf($delimiter)
    if ($idx -lt 0) {
        throw "curl.exe ciktisi beklenmeyen formatta (delimiter bulunamadi): $rawOutput"
    }
    $beforeDelimiter = $rawOutput.Substring(0, $idx).TrimEnd("`n")
    $httpCode = $rawOutput.Substring($idx + $delimiter.Length).Trim()

    if ($httpCode -notmatch "^2\d\d$") {
        throw "Beklenmeyen HTTP durum kodu: $httpCode (istek: $($CurlArgs -join ' '))."
    }

    return $beforeDelimiter
}

$path = "/api/fires/$FireId/cells"
if ($Query) { $path += "?$Query" }
$uri = "$BaseUrl$path"

Write-Host "Endpoint:            $uri"
Write-Host "Isinma istegi:       $WarmupRequests"
Write-Host "Olculen istek:       $MeasuredRequests"
Write-Host ""

# --- Isinma (JIT, connection pool, ilk-sorgu EF Core derlemesi) ---
for ($i = 0; $i -lt $WarmupRequests; $i++) {
    Invoke-CurlChecked -CurlArgs @("-s", "--compressed", "-o", "NUL", $uri) | Out-Null
}

# --- Gecikme olcumu (negotiated encoding: br/gzip, normal istemci davranisi) ---
$timings = @()
for ($i = 0; $i -lt $MeasuredRequests; $i++) {
    $result = Invoke-CurlChecked -CurlArgs @("-s", "--compressed", "-o", "NUL", $uri) -WriteOutFormat "%{time_total}"
    $ms = [double]::Parse($result.Trim(), [System.Globalization.CultureInfo]::InvariantCulture) * 1000
    if ($ms -le 0) { throw "Gecikme olcumu gecersiz (<=0 ms): $ms -- gercek bir istek atilmamis olabilir." }
    $timings += $ms
}

$avg = ($timings | Measure-Object -Average).Average
$min = ($timings | Measure-Object -Minimum).Minimum
$max = ($timings | Measure-Object -Maximum).Maximum

Write-Host ("Gecikme (ms)  -> ort: {0:N1}  min: {1:N1}  max: {2:N1}  (n={3})" -f $avg, $min, $max, $MeasuredRequests)

# --- Yanit boyutu: sikistirmasiz (Accept-Encoding: identity) vs sikistirmali (br/gzip negotiated) ---
$uncompressedResult = Invoke-CurlChecked -CurlArgs @("-s", "-H", "Accept-Encoding: identity", "-o", "NUL", $uri) -WriteOutFormat "%{size_download}"
$uncompressedBytes = [double]$uncompressedResult.Trim()
if ($uncompressedBytes -le 0) { throw "Sikistirmasiz yanit boyutu gecersiz (<=0 bayt): $uncompressedBytes" }

$compressedResult = Invoke-CurlChecked -CurlArgs @("-s", "--compressed", "-o", "NUL", $uri) -WriteOutFormat "%{size_download}"
$compressedBytes = [double]$compressedResult.Trim()
if ($compressedBytes -le 0) { throw "Sikistirmali yanit boyutu gecersiz (<=0 bayt): $compressedBytes" }

$headersResult = Invoke-CurlChecked -CurlArgs @("-s", "--compressed", "-D", "-", "-o", "NUL", $uri)
$encodingHeader = $headersResult -split "`n" | Select-String -Pattern "^content-encoding:" -CaseSensitive:$false
$encoding = if ($encodingHeader) { ($encodingHeader.Line -split ":", 2)[1].Trim() } else { "(yok - sunucu sikistirmadi)" }

Write-Host ("Yanit boyutu  -> sikistirmasiz: {0:N0} bayt, sikistirmali ({1}): {2:N0} bayt ({3:P0} kucultme)" -f `
    $uncompressedBytes, $encoding, $compressedBytes, (1 - ($compressedBytes / $uncompressedBytes)))
