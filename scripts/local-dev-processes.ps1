$LocalDevRepoRoot = Split-Path -Parent $PSScriptRoot
$LocalDevStateDir = Join-Path $LocalDevRepoRoot ".local-dev"
$LocalDevStateFile = Join-Path $LocalDevStateDir "local-dev-session.json"

function Get-LocalDevServiceDefinitions {
    $apiProject = Join-Path $LocalDevRepoRoot "ServiceMarketplace.API"
    $webProject = Join-Path $LocalDevRepoRoot "ServiceMarketplace.UI.Web"
    $apiPorts = Get-LaunchProfilePorts -ProjectPath $apiProject -LaunchProfile "ServiceMarketplace.API" -DefaultHttpsPort 7147 -DefaultHttpPort 5147
    $webPorts = Get-LaunchProfilePorts -ProjectPath $webProject -LaunchProfile "ServiceMarketplace.UI.Web" -DefaultHttpsPort 7241 -DefaultHttpPort 5241

    return @(
        [PSCustomObject]@{
            Name          = "API"
            ProjectPath   = $apiProject
            LaunchProfile = "ServiceMarketplace.API"
            HttpsPort     = $apiPorts.HttpsPort
            HttpPort      = $apiPorts.HttpPort
            KnownPorts     = @($apiPorts.HttpsPort, $apiPorts.HttpPort, 7147, 5147) | Select-Object -Unique
            ReadyUrl      = "https://localhost:$($apiPorts.HttpsPort)/health/ready"
            DisplayUrl    = "https://localhost:$($apiPorts.HttpsPort)"
            OutLog        = Join-Path $env:TEMP "servicemarketplace-api-local.log"
            ErrLog        = Join-Path $env:TEMP "servicemarketplace-api-local.err.log"
        },
        [PSCustomObject]@{
            Name          = "Web"
            ProjectPath   = $webProject
            LaunchProfile = "ServiceMarketplace.UI.Web"
            HttpsPort     = $webPorts.HttpsPort
            HttpPort      = $webPorts.HttpPort
            KnownPorts     = @($webPorts.HttpsPort, $webPorts.HttpPort, 7241, 5241) | Select-Object -Unique
            ReadyUrl      = "https://localhost:$($webPorts.HttpsPort)"
            DisplayUrl    = "https://localhost:$($webPorts.HttpsPort)"
            OutLog        = Join-Path $env:TEMP "servicemarketplace-web-local.log"
            ErrLog        = Join-Path $env:TEMP "servicemarketplace-web-local.err.log"
        }
    )
}

function Get-LaunchProfilePorts {
    param(
        [Parameter(Mandatory = $true)][string]$ProjectPath,
        [Parameter(Mandatory = $true)][string]$LaunchProfile,
        [Parameter(Mandatory = $true)][int]$DefaultHttpsPort,
        [Parameter(Mandatory = $true)][int]$DefaultHttpPort
    )

    $httpsPort = $DefaultHttpsPort
    $httpPort = $DefaultHttpPort
    $launchSettingsPath = Join-Path $ProjectPath "Properties\launchSettings.json"

    if (Test-Path -LiteralPath $launchSettingsPath) {
        try {
            $settings = Get-Content -LiteralPath $launchSettingsPath -Raw | ConvertFrom-Json
            $profileProperty = $settings.profiles.PSObject.Properties[$LaunchProfile]
            if ($profileProperty) {
                $applicationUrls = [string]$profileProperty.Value.applicationUrl
                foreach ($applicationUrl in $applicationUrls.Split(";", [System.StringSplitOptions]::RemoveEmptyEntries)) {
                    $uri = $null
                    if ([System.Uri]::TryCreate($applicationUrl.Trim(), [System.UriKind]::Absolute, [ref]$uri)) {
                        if ($uri.Scheme -eq "https") {
                            $httpsPort = $uri.Port
                        }
                        elseif ($uri.Scheme -eq "http") {
                            $httpPort = $uri.Port
                        }
                    }
                }
            }
        }
        catch {
            Write-Host "Could not parse launch settings at $launchSettingsPath. Falling back to defaults."
        }
    }

    return [PSCustomObject]@{
        HttpsPort = $httpsPort
        HttpPort = $httpPort
    }
}

function ConvertTo-ComparablePath {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ""
    }

    return [System.IO.Path]::GetFullPath($Path).TrimEnd("\")
}

function Get-LocalDevProcessInfo {
    param([int]$ProcessId)

    return Get-CimInstance Win32_Process -Filter "ProcessId = $ProcessId" -ErrorAction SilentlyContinue
}

function Get-LocalDevProcessLineage {
    param([int]$ProcessId)

    $lineage = @()
    $seen = @{}
    $currentId = $ProcessId

    while ($currentId -and -not $seen.ContainsKey($currentId) -and $lineage.Count -lt 12) {
        $seen[$currentId] = $true
        $process = Get-LocalDevProcessInfo -ProcessId $currentId
        if (-not $process) {
            break
        }

        $lineage += $process
        $currentId = $process.ParentProcessId
    }

    return $lineage
}

function Test-LaunchCommandForService {
    param(
        [Parameter(Mandatory = $true)]$ProcessInfo,
        [Parameter(Mandatory = $true)]$Service
    )

    $commandLine = [string]$ProcessInfo.CommandLine
    $projectPath = ConvertTo-ComparablePath -Path $Service.ProjectPath

    return $commandLine -like "*dotnet*" -and
        $commandLine -like "* run *" -and
        $commandLine -like "*--project*" -and
        $commandLine -like "*$projectPath*" -and
        $commandLine -like "*--launch-profile*" -and
        $commandLine -like "*$($Service.LaunchProfile)*"
}

function Test-ProcessMatchesService {
    param(
        [Parameter(Mandatory = $true)]$ProcessInfo,
        [Parameter(Mandatory = $true)]$Service
    )

    $commandLine = [string]$ProcessInfo.CommandLine
    $executablePath = ConvertTo-ComparablePath -Path ([string]$ProcessInfo.ExecutablePath)
    $projectPath = ConvertTo-ComparablePath -Path $Service.ProjectPath

    if (Test-LaunchCommandForService -ProcessInfo $ProcessInfo -Service $Service) {
        return $true
    }

    if ($Service.Name -eq "API") {
        return ($executablePath -like "$projectPath*") -or
            ($commandLine -like "*$projectPath*" -and $commandLine -like "*ServiceMarketplace.API*")
    }

    return ($commandLine -like "*blazor-devserver.dll*" -and
            $commandLine -like "*$projectPath*" -and
            $commandLine -like "*ServiceMarketplace.UI.Web.dll*") -or
        ($commandLine -like "*$projectPath*" -and $commandLine -like "*ServiceMarketplace.UI.Web*")
}

function Get-LocalDevLaunchAncestor {
    param(
        [int]$ProcessId,
        [Parameter(Mandatory = $true)]$Service
    )

    foreach ($process in Get-LocalDevProcessLineage -ProcessId $ProcessId) {
        if (Test-LaunchCommandForService -ProcessInfo $process -Service $Service) {
            return $process
        }
    }

    return $null
}

function Get-PortOwnersForService {
    param([Parameter(Mandatory = $true)]$Service)

    $owners = @()
    foreach ($port in @($Service.KnownPorts)) {
        $connections = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue |
            Sort-Object OwningProcess -Unique

        foreach ($connection in $connections) {
            $processInfo = Get-LocalDevProcessInfo -ProcessId $connection.OwningProcess
            if (-not $processInfo) {
                continue
            }

            $launchAncestor = Get-LocalDevLaunchAncestor -ProcessId $connection.OwningProcess -Service $Service
            $owners += [PSCustomObject]@{
                Port          = $port
                ProcessId     = $connection.OwningProcess
                ProcessName   = $processInfo.Name
                Executable    = $processInfo.ExecutablePath
                CommandLine   = $processInfo.CommandLine
                Matches       = Test-ProcessMatchesService -ProcessInfo $processInfo -Service $Service
                ScriptOwned   = $null -ne $launchAncestor
                LauncherPid   = if ($launchAncestor) { $launchAncestor.ProcessId } else { $null }
            }
        }
    }

    return $owners
}

function Read-LocalDevState {
    if (-not (Test-Path -LiteralPath $LocalDevStateFile)) {
        return [PSCustomObject]@{
            RepoRoot  = $LocalDevRepoRoot
            UpdatedAt = $null
            Services  = @()
        }
    }

    try {
        $state = Get-Content -LiteralPath $LocalDevStateFile -Raw | ConvertFrom-Json
        if (-not $state.PSObject.Properties.Name.Contains("Services")) {
            $state | Add-Member -MemberType NoteProperty -Name Services -Value @()
        }

        return $state
    }
    catch {
        Remove-Item -LiteralPath $LocalDevStateFile -Force -ErrorAction SilentlyContinue
        return [PSCustomObject]@{
            RepoRoot  = $LocalDevRepoRoot
            UpdatedAt = $null
            Services  = @()
        }
    }
}

function Write-LocalDevState {
    param([Parameter(Mandatory = $true)]$State)

    New-Item -ItemType Directory -Path $LocalDevStateDir -Force | Out-Null
    $State.UpdatedAt = (Get-Date).ToString("o")
    $State | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $LocalDevStateFile -Encoding UTF8
}

function Set-LocalDevStateService {
    param(
        [Parameter(Mandatory = $true)]$State,
        [Parameter(Mandatory = $true)]$Entry
    )

    $services = @($State.Services | Where-Object { $_.Name -ne $Entry.Name })
    $State.Services = @($services + $Entry)
    Write-LocalDevState -State $State
}

function Remove-LocalDevStateService {
    param(
        [Parameter(Mandatory = $true)]$State,
        [Parameter(Mandatory = $true)][string]$Name
    )

    $State.Services = @($State.Services | Where-Object { $_.Name -ne $Name })
    if ($State.Services.Count -eq 0 -and (Test-Path -LiteralPath $LocalDevStateFile)) {
        Remove-Item -LiteralPath $LocalDevStateFile -Force
        return
    }

    Write-LocalDevState -State $State
}

function Get-LocalDevStateService {
    param(
        [Parameter(Mandatory = $true)]$State,
        [Parameter(Mandatory = $true)][string]$Name
    )

    return @($State.Services | Where-Object { $_.Name -eq $Name } | Select-Object -First 1)
}

function Test-StateServiceIsValid {
    param(
        $Entry,
        [Parameter(Mandatory = $true)]$Service
    )

    if (-not $Entry) {
        return $false
    }

    foreach ($processId in @($Entry.LauncherPid, $Entry.ListenerPid)) {
        if (-not $processId) {
            continue
        }

        $processInfo = Get-LocalDevProcessInfo -ProcessId ([int]$processId)
        if ($processInfo -and (Test-ProcessMatchesService -ProcessInfo $processInfo -Service $Service)) {
            return $true
        }
    }

    return $false
}

function New-LocalDevStateEntry {
    param(
        [Parameter(Mandatory = $true)]$Service,
        [int]$LauncherPid,
        [int]$ListenerPid,
        [string]$Status
    )

    return [PSCustomObject]@{
        Name          = $Service.Name
        ProjectPath   = $Service.ProjectPath
        LaunchProfile = $Service.LaunchProfile
        LauncherPid   = $LauncherPid
        ListenerPid   = $ListenerPid
        HttpsPort     = $Service.HttpsPort
        HttpPort      = $Service.HttpPort
        Status        = $Status
        UpdatedAt     = (Get-Date).ToString("o")
    }
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

function Get-LocalDevListenerPid {
    param([Parameter(Mandatory = $true)]$Service)

    $owner = Get-PortOwnersForService -Service $Service |
        Where-Object { $_.Port -eq $Service.HttpsPort -and $_.Matches } |
        Select-Object -First 1

    if ($owner) {
        return [int]$owner.ProcessId
    }

    return 0
}
