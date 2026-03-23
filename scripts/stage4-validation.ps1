param(
    [string]$BaseUrl = "http://localhost:5007",
    [int]$Iterations = 15,
    [int]$PageSize = 100
)

$ErrorActionPreference = "Stop"

function Get-Json {
    param([string]$Url)
    return Invoke-RestMethod -Uri $Url -Method Get
}

function Measure-Endpoint {
    param(
        [string]$Name,
        [string]$Url,
        [int]$Count
    )

    $samples = New-Object System.Collections.Generic.List[double]
    for ($i = 0; $i -lt $Count; $i++) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $null = Invoke-WebRequest -Uri $Url -Method Get -UseBasicParsing
        $sw.Stop()
        $samples.Add($sw.Elapsed.TotalMilliseconds)
    }

    $avg = [Math]::Round(($samples | Measure-Object -Average).Average, 2)
    $min = [Math]::Round(($samples | Measure-Object -Minimum).Minimum, 2)
    $max = [Math]::Round(($samples | Measure-Object -Maximum).Maximum, 2)

    [PSCustomObject]@{
        Endpoint = $Name
        AverageMs = $avg
        MinMs = $min
        MaxMs = $max
        Iterations = $Count
    }
}

Write-Output "Stage 4 validation started against $BaseUrl"

$camAllUrl = "$BaseUrl/api/pcap/map-entities/paged?pageNumber=1&pageSize=$PageSize&messageTypes=CAM"
$camStation6Url = "$BaseUrl/api/pcap/map-entities/paged?pageNumber=1&pageSize=$PageSize&messageTypes=CAM&stationTypes=6"
$corrAllUrl = "$BaseUrl/api/pcap/correlations/paged?pageNumber=1&pageSize=$PageSize"
$corrStrictUrl = "$BaseUrl/api/pcap/correlations/paged?pageNumber=1&pageSize=$PageSize&correlationType=strict"

$camAll = Get-Json -Url $camAllUrl
$camStation6 = Get-Json -Url $camStation6Url
$corrAll = Get-Json -Url $corrAllUrl
$corrStrict = Get-Json -Url $corrStrictUrl

if ($null -eq $camAll.totalCount -or $camAll.totalCount -le 0) {
    throw "Validation failed: CAM paged endpoint returned no data."
}

if ($null -eq $corrAll.totalCount -or $corrAll.totalCount -le 0) {
    throw "Validation failed: Correlations paged endpoint returned no data."
}

if ($camStation6.totalCount -gt $camAll.totalCount) {
    throw "Validation failed: station type filtered CAM count exceeds total CAM count."
}

if ($corrStrict.totalCount -gt $corrAll.totalCount) {
    throw "Validation failed: strict correlation count exceeds total correlations count."
}

$summary = [PSCustomObject]@{
    CamTotal = $camAll.totalCount
    CamStationType6 = $camStation6.totalCount
    CorrelationsTotal = $corrAll.totalCount
    CorrelationsStrict = $corrStrict.totalCount
}

Write-Output "Validation checks passed:"
$summary | Format-List | Out-String | Write-Output

Write-Output "Running endpoint latency benchmark..."
$benchmarks = @(
    (Measure-Endpoint -Name "map-entities/paged CAM" -Url $camAllUrl -Count $Iterations),
    (Measure-Endpoint -Name "map-entities/paged CAM stationTypes=6" -Url $camStation6Url -Count $Iterations),
    (Measure-Endpoint -Name "correlations/paged all" -Url $corrAllUrl -Count $Iterations),
    (Measure-Endpoint -Name "correlations/paged strict" -Url $corrStrictUrl -Count $Iterations)
)

$benchmarks | Format-Table -AutoSize | Out-String | Write-Output

Write-Output "Stage 4 validation complete."
