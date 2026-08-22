param(
    [string]$ConnectionString,
    [Parameter(Mandatory = $true)]
    [string]$Email,
    [Parameter(Mandatory = $true)]
    [string]$FirstName,
    [Parameter(Mandatory = $true)]
    [string]$LastName,
    [Parameter(Mandatory = $true)]
    [string]$Phone,
    [Parameter(Mandatory = $true)]
    [string]$Reason,
    [string]$OperatorUserId,
    [switch]$ResetPassword
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$toolProject = Join-Path $repoRoot "ServiceMarketplace.AdminTool\ServiceMarketplace.AdminTool.csproj"
$apiProject = Join-Path $repoRoot "ServiceMarketplace.API\ServiceMarketplace.API.csproj"

if (-not (Test-Path $toolProject)) {
    throw "Could not find admin tool project at $toolProject"
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $ConnectionString = $env:SERVICE_MARKETPLACE_CONNECTION_STRING
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $ConnectionString = $env:ConnectionStrings__DefaultConnection
}

if ([string]::IsNullOrWhiteSpace($ConnectionString) -and (Test-Path $apiProject)) {
    $secretLines = dotnet user-secrets list --project $apiProject 2>$null
    $secretLine = $secretLines | Where-Object { $_ -like "ConnectionStrings:DefaultConnection = *" } | Select-Object -First 1
    if ($secretLine) {
        $ConnectionString = $secretLine.Substring("ConnectionStrings:DefaultConnection = ".Length)
    }
}

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    throw "Connection string not found. Pass -ConnectionString or set SERVICE_MARKETPLACE_CONNECTION_STRING / ConnectionStrings__DefaultConnection."
}

$passwordWasSet = -not [string]::IsNullOrWhiteSpace($env:SERVICE_MARKETPLACE_ADMIN_PASSWORD)
if (-not $passwordWasSet) {
    $securePassword = Read-Host "Admin password" -AsSecureString
    $bstr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
    try {
        $env:SERVICE_MARKETPLACE_ADMIN_PASSWORD = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($bstr)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
    }
}

try {
    $argsList = @(
        "run", "--project", $toolProject, "--",
        "--connection", $ConnectionString,
        "--email", $Email,
        "--first-name", $FirstName,
        "--last-name", $LastName,
        "--phone", $Phone,
        "--reason", $Reason
    )

    if (-not [string]::IsNullOrWhiteSpace($OperatorUserId)) {
        $argsList += @("--operator-user-id", $OperatorUserId)
    }

    if ($ResetPassword) {
        $argsList += "--reset-password"
    }

    dotnet @argsList
}
finally {
    if (-not $passwordWasSet) {
        Remove-Item Env:\SERVICE_MARKETPLACE_ADMIN_PASSWORD -ErrorAction SilentlyContinue
    }
}
