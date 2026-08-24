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
    dotnet run --project backend/ReGreen.Api &
    ./backend/scripts/perf-check.ps1 -BaseUrl http://localhost:5066

.EXAMPLE
    ./backend/scripts/perf-check.ps1 -FireId AKD_2021_01 -Query "min_lon=31.4&min_lat=36.9&max_lon=31.5&max_lat=37.0"
#>
param(
    [string]$BaseUrl = "http://localhost:5066",
    [string]$FireId = "AKD_2021_01",
    [string]$Query = "",
    [int]$WarmupRequests = 3,
    [int]$MeasuredRequests = 10
)

$ErrorActionPreference = "Stop"

$curlExe = (Get-Command curl.exe -ErrorAction Stop).Source

$path = "/api/fires/$FireId/cells"
if ($Query) { $path += "?$Query" }
$uri = "$BaseUrl$path"

Write-Host "Endpoint:            $uri"
Write-Host "Isinma istegi:       $WarmupRequests"
Write-Host "Olculen istek:       $MeasuredRequests"
Write-Host ""

# --- Isinma (JIT, connection pool, ilk-sorgu EF Core derlemesi) ---
for ($i = 0; $i -lt $WarmupRequests; $i++) {
    & $curlExe -s --compressed -o NUL $uri
}

# --- Gecikme olcumu (negotiated encoding: br/gzip, normal istemci davranisi) ---
$timings = @()
for ($i = 0; $i -lt $MeasuredRequests; $i++) {
    $result = & $curlExe -s --compressed -o NUL -w "%{time_total}" $uri
    $timings += [double]::Parse($result, [System.Globalization.CultureInfo]::InvariantCulture) * 1000
}

$avg = ($timings | Measure-Object -Average).Average
$min = ($timings | Measure-Object -Minimum).Minimum
$max = ($timings | Measure-Object -Maximum).Maximum

Write-Host ("Gecikme (ms)  -> ort: {0:N1}  min: {1:N1}  max: {2:N1}  (n={3})" -f $avg, $min, $max, $MeasuredRequests)

# --- Yanit boyutu: sikistirmasiz (Accept-Encoding: identity) vs sikistirmali (br/gzip negotiated) ---
$uncompressedBytes = [double](& $curlExe -s -H "Accept-Encoding: identity" -o NUL -w "%{size_download}" $uri)
$compressedBytes = [double](& $curlExe -s --compressed -o NUL -w "%{size_download}" $uri)
$encodingHeader = (& $curlExe -s --compressed -D - -o NUL $uri | Select-String -Pattern "^content-encoding:" -CaseSensitive:$false)
$encoding = if ($encodingHeader) { ($encodingHeader -split ":", 2)[1].Trim() } else { "(yok - sunucu sikistirmadi)" }

Write-Host ("Yanit boyutu  -> sikistirmasiz: {0:N0} bayt, sikistirmali ({1}): {2:N0} bayt ({3:P0} kucultme)" -f `
    $uncompressedBytes, $encoding, $compressedBytes, (1 - ($compressedBytes / $uncompressedBytes)))
