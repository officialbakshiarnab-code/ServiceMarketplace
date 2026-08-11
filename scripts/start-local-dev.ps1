param(
    [switch]$ApplyMigrations,
    [switch]$ApiOnly,
    [switch]$WebOnly
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $repoRoot "ServiceMarketplace.API"
$webProject = Join-Path $repoRoot "ServiceMarketplace.UI.Web"
$infrastructureProject = Join-Path $repoRoot "ServiceMarketplace.Infrastructure"

$apiHttpsPort = 7147
$webHttpsPort = 7241
$apiHealthUrl = "https://localhost:$apiHttpsPort/health/ready"
$apiLog = Join-Path $env:TEMP "servicemarketplace-api-local.log"
$apiErrLog = Join-Path $env:TEMP "servicemarketplace-api-local.err.log"
$webLog = Join-Path $env:TEMP "servicemarketplace-web-local.log"
$webErrLog = Join-Path $env:TEMP "servicemarketplace-web-local.err.log"

function Test-PortListening {
    param([int]$Port)

    return [bool](Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)
}

function Start-DotNetProject {
    param(
        [string]$ProjectPath,
        [string]$LaunchProfile,
        [string]$OutLog,
        [string]$ErrLog
    )

    return Start-Process `
        -FilePath dotnet `
        -ArgumentList @("run", "--project", $ProjectPath, "--launch-profile", $LaunchProfile) `
        -WorkingDirectory $repoRoot `
        -RedirectStandardOutput $OutLog `
        -RedirectStandardError $ErrLog `
        -WindowStyle Hidden `
        -PassThru
}

function Wait-ForUrl {
    param(
        [string]$Url,
        [int]$TimeoutSeconds = 45
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $curl = Get-Command curl.exe -ErrorAction SilentlyContinue
        if ($curl) {
            $statusCode = & curl.exe -k -s -o NUL -w "%{http_code}" $Url
            $numericStatusCode = 0
            if ([int]::TryParse($statusCode, [ref]$numericStatusCode) -and
                $numericStatusCode -ge 200 -and
                $numericStatusCode -lt 500) {
                return $true
            }
        }
        else {
            $previousCertificateCallback = [System.Net.ServicePointManager]::ServerCertificateValidationCallback
            try {
                [System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }
                $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5
                if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 500) {
                    return $true
                }
            }
            catch {
            }
            finally {
                [System.Net.ServicePointManager]::ServerCertificateValidationCallback = $previousCertificateCallback
            }
        }

        Start-Sleep -Seconds 2
    }

    return $false
}

if ($ApplyMigrations) {
    dotnet ef database update --project $infrastructureProject --startup-project $apiProject
}

if (-not $WebOnly) {
    if (Test-PortListening -Port $apiHttpsPort) {
        Write-Host "API already listening on https://localhost:$apiHttpsPort"
    }
    else {
        $apiProcess = Start-DotNetProject `
            -ProjectPath $apiProject `
            -LaunchProfile "ServiceMarketplace.API" `
            -OutLog $apiLog `
            -ErrLog $apiErrLog

        Write-Host "Started API process $($apiProcess.Id). Logs:"
        Write-Host " - $apiLog"
        Write-Host " - $apiErrLog"
    }

    if (Wait-ForUrl -Url $apiHealthUrl) {
        Write-Host "API ready: $apiHealthUrl"
    }
    else {
        Write-Host "API did not become ready within the timeout. Check logs:"
        Write-Host " - $apiLog"
        Write-Host " - $apiErrLog"
    }
}

if (-not $ApiOnly) {
    if (Test-PortListening -Port $webHttpsPort) {
        Write-Host "Web UI already listening on https://localhost:$webHttpsPort"
    }
    else {
        $webProcess = Start-DotNetProject `
            -ProjectPath $webProject `
            -LaunchProfile "ServiceMarketplace.UI.Web" `
            -OutLog $webLog `
            -ErrLog $webErrLog

        Write-Host "Started Web UI process $($webProcess.Id). Logs:"
        Write-Host " - $webLog"
        Write-Host " - $webErrLog"
    }
}

Write-Host ""
Write-Host "Local URLs"
Write-Host " - API Swagger: https://localhost:$apiHttpsPort/swagger"
Write-Host " - API health:  $apiHealthUrl"
Write-Host " - Web UI:      https://localhost:$webHttpsPort"
