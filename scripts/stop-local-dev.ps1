param(
    [switch]$ApiOnly,
    [switch]$WebOnly
)

$ErrorActionPreference = "Stop"

. "$PSScriptRoot\local-dev-processes.ps1"

if ($ApiOnly -and $WebOnly) {
    throw "Use either -ApiOnly or -WebOnly, not both."
}

function Add-ProcessId {
    param(
        [System.Collections.Generic.List[int]]$List,
        [int]$ProcessId
    )

    if ($ProcessId -gt 0 -and -not $List.Contains($ProcessId)) {
        $List.Add($ProcessId)
    }
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
    $processIds = [System.Collections.Generic.List[int]]::new()
    $entry = Get-LocalDevStateService -State $state -Name $service.Name

    if ($entry -and (Test-StateServiceIsValid -Entry $entry -Service $service)) {
        Add-ProcessId -List $processIds -ProcessId ([int]$entry.ListenerPid)
        Add-ProcessId -List $processIds -ProcessId ([int]$entry.LauncherPid)
    }

    $owners = @(Get-PortOwnersForService -Service $service)
    foreach ($owner in $owners) {
        if ($owner.ScriptOwned) {
            Add-ProcessId -List $processIds -ProcessId ([int]$owner.ProcessId)
            Add-ProcessId -List $processIds -ProcessId ([int]$owner.LauncherPid)
        }
    }

    if ($processIds.Count -eq 0) {
        $externalOwners = @($owners | Where-Object { $_.Matches })
        if ($externalOwners) {
            Write-Host "$($service.Name) is running, but it is not owned by this local-dev script. Leaving it alone."
            foreach ($owner in $externalOwners) {
                Write-Host " - Port $($owner.Port): PID $($owner.ProcessId) $($owner.ProcessName)"
            }
        }
        else {
            Write-Host "No script-owned $($service.Name) local-dev process found for ports $($service.KnownPorts -join '/')."
        }

        Remove-LocalDevStateService -State $state -Name $service.Name
        $state = Read-LocalDevState
        continue
    }

    foreach ($processId in @($processIds)) {
        $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
        if ($process) {
            Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
            Write-Host "Stopped $($service.Name) $($process.ProcessName) PID $processId"
        }
    }

    Remove-LocalDevStateService -State $state -Name $service.Name
    $state = Read-LocalDevState
}
