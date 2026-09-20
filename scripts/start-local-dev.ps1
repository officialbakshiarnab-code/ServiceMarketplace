param(
    [switch]$ApplyMigrations,
    [switch]$ApiOnly,
    [switch]$WebOnly
)

$ErrorActionPreference = "Stop"

. "$PSScriptRoot\local-dev-processes.ps1"

$repoRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $repoRoot "ServiceMarketplace.API"
$infrastructureProject = Join-Path $repoRoot "ServiceMarketplace.Infrastructure"

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

if ($ApplyMigrations) {
    dotnet ef database update --project $infrastructureProject --startup-project $apiProject
}

if ($ApiOnly -and $WebOnly) {
    throw "Use either -ApiOnly or -WebOnly, not both."
}

$state = Read-LocalDevState
$services = Get-LocalDevServiceDefinitions
if ($ApiOnly) {
    $services = @($services | Where-Object { $_.Name -eq "API" })
}
elseif ($WebOnly) {
    $services = @($services | Where-Object { $_.Name -eq "Web" })
}

foreach ($service in $services) {
    $owners = @(Get-PortOwnersForService -Service $service)
    $expectedPorts = @($service.HttpsPort, $service.HttpPort)
    $staleScriptOwners = @($owners | Where-Object { $_.ScriptOwned -and $_.Port -notin $expectedPorts })
    foreach ($owner in $staleScriptOwners) {
        foreach ($processId in @($owner.ProcessId, $owner.LauncherPid) | Select-Object -Unique) {
            if ($processId) {
                $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
                if ($process) {
                    Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
                    Write-Host "$($service.Name) stale script-owned process stopped from legacy port $($owner.Port): PID $processId"
                }
            }
        }
    }

    if ($staleScriptOwners) {
        Start-Sleep -Seconds 1
        $owners = @(Get-PortOwnersForService -Service $service)
    }

    $expectedOwners = @($owners | Where-Object { $_.Port -in $expectedPorts })
    $blockingOwners = @($expectedOwners | Where-Object { -not $_.Matches })
    if ($blockingOwners) {
        Write-Host "$($service.Name) blocked: expected ports $($service.HttpsPort)/$($service.HttpPort) are owned by another process."
        foreach ($owner in $blockingOwners) {
            Write-Host " - Port $($owner.Port): PID $($owner.ProcessId) $($owner.ProcessName)"
            Write-Host "   Path: $($owner.Executable)"
            Write-Host "   Command: $($owner.CommandLine)"
        }

        throw "$($service.Name) startup blocked by non-ServiceMarketplace process."
    }

    $matchingOwners = @($expectedOwners | Where-Object { $_.Matches })
    if ($matchingOwners) {
        $httpsOwner = @($matchingOwners | Where-Object { $_.Port -eq $service.HttpsPort } | Select-Object -First 1)
        $listenerPid = if ($httpsOwner) { [int]$httpsOwner.ProcessId } else { [int]$matchingOwners[0].ProcessId }
        $launcherPid = if ($httpsOwner -and $httpsOwner.LauncherPid) { [int]$httpsOwner.LauncherPid } else { 0 }
        $ownership = if ($matchingOwners | Where-Object { $_.ScriptOwned }) { "script-owned" } else { "external" }

        Write-Host "$($service.Name) already running ($ownership)."
        Write-Host " - URL: $($service.DisplayUrl)"
        Write-Host " - HTTPS port: $($service.HttpsPort)"
        Write-Host " - HTTP port: $($service.HttpPort)"
        Write-Host " - Listener PID: $listenerPid"
        if ($launcherPid) {
            Write-Host " - Launcher PID: $launcherPid"
            Set-LocalDevStateService -State $state -Entry (New-LocalDevStateEntry -Service $service -LauncherPid $launcherPid -ListenerPid $listenerPid -Status "AlreadyRunning")
        }

        if (Wait-ForUrl -Url $service.ReadyUrl) {
            Write-Host " - Ready: $($service.ReadyUrl)"
        }
        else {
            Write-Host " - Not ready within timeout. Logs:"
            Write-Host "   $($service.OutLog)"
            Write-Host "   $($service.ErrLog)"
        }

        continue
    }

    $entry = Get-LocalDevStateService -State $state -Name $service.Name
    if ($entry -and -not (Test-StateServiceIsValid -Entry $entry -Service $service)) {
        Remove-LocalDevStateService -State $state -Name $service.Name
        $state = Read-LocalDevState
        Write-Host "$($service.Name) stale local-dev state removed."
    }

    $process = Start-DotNetProject `
        -ProjectPath $service.ProjectPath `
        -LaunchProfile $service.LaunchProfile `
        -OutLog $service.OutLog `
        -ErrLog $service.ErrLog

    Write-Host "$($service.Name) started."
    Write-Host " - Launcher PID: $($process.Id)"
    Write-Host " - HTTPS port: $($service.HttpsPort)"
    Write-Host " - HTTP port: $($service.HttpPort)"
    Write-Host " - Logs:"
    Write-Host "   $($service.OutLog)"
    Write-Host "   $($service.ErrLog)"

    if (Wait-ForUrl -Url $service.ReadyUrl) {
        $listenerPid = Get-LocalDevListenerPid -Service $service
        Write-Host " - Listener PID: $listenerPid"
        Write-Host " - Ready: $($service.ReadyUrl)"
        Set-LocalDevStateService -State $state -Entry (New-LocalDevStateEntry -Service $service -LauncherPid $process.Id -ListenerPid $listenerPid -Status "Started")
    }
    else {
        Set-LocalDevStateService -State $state -Entry (New-LocalDevStateEntry -Service $service -LauncherPid $process.Id -ListenerPid 0 -Status "StartedNotReady")
        Write-Host " - Did not become ready within the timeout. Check logs:"
        Write-Host "   $($service.OutLog)"
        Write-Host "   $($service.ErrLog)"
    }
}

Write-Host ""
Write-Host "Local URLs"
$allServices = Get-LocalDevServiceDefinitions
$apiService = @($allServices | Where-Object { $_.Name -eq "API" } | Select-Object -First 1)
$webService = @($allServices | Where-Object { $_.Name -eq "Web" } | Select-Object -First 1)
Write-Host " - API Swagger: https://localhost:$($apiService.HttpsPort)/swagger"
Write-Host " - API health:  $($apiService.ReadyUrl)"
Write-Host " - Web UI:      $($webService.DisplayUrl)"
