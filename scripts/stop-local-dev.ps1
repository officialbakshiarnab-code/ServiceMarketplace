param(
    [switch]$ApiOnly,
    [switch]$WebOnly
)

$ErrorActionPreference = "Stop"

$apiPorts = @(7147, 5147)
$webPorts = @(7241, 5241)

if ($ApiOnly -and $WebOnly) {
    throw "Use either -ApiOnly or -WebOnly, not both."
}

$ports = if ($ApiOnly) {
    $apiPorts
}
elseif ($WebOnly) {
    $webPorts
}
else {
    $apiPorts + $webPorts
}

$processIds = @()
foreach ($port in $ports) {
    $connections = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    foreach ($connection in $connections) {
        if ($connection.OwningProcess -and $processIds -notcontains $connection.OwningProcess) {
            $processIds += $connection.OwningProcess
        }
    }
}

if (-not $processIds) {
    Write-Host "No ServiceMarketplace local dev processes found for ports: $($ports -join ', ')"
    return
}

foreach ($processId in $processIds) {
    $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
    if ($process) {
        Stop-Process -Id $processId -Force
        Write-Host "Stopped $($process.ProcessName) PID $processId"
    }
}
