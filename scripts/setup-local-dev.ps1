param(
    [string]$PostgresHost = "localhost",
    [int]$PostgresPort = 5432,
    [string]$Database = "service_marketplace",
    [string]$Username = "servicemarketplace",
    [string]$Password = "servicemarketplace_local_dev",
    [string]$JwtIssuer = "ServiceMarketplace",
    [string]$JwtAudience = "ServiceMarketplaceUsers",
    [switch]$StartPostgresContainer,
    [switch]$ApplyMigrations,
    [string]$ContainerName = "service-marketplace-postgres"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $repoRoot "ServiceMarketplace.API\ServiceMarketplace.API.csproj"
$infrastructureProject = Join-Path $repoRoot "ServiceMarketplace.Infrastructure\ServiceMarketplace.Infrastructure.csproj"

if (-not (Test-Path $apiProject)) {
    throw "Could not find API project at $apiProject"
}

if ($StartPostgresContainer) {
    $docker = Get-Command docker -ErrorAction SilentlyContinue
    if (-not $docker) {
        throw "Docker was not found. Install Docker Desktop, or start PostgreSQL yourself and rerun without -StartPostgresContainer."
    }

    $existingContainer = docker ps -a --filter "name=^/$ContainerName$" --format "{{.Names}}"
    if ($existingContainer -eq $ContainerName) {
        docker start $ContainerName | Out-Null
    }
    else {
        docker run --name $ContainerName `
            -e POSTGRES_USER=$Username `
            -e POSTGRES_PASSWORD=$Password `
            -e POSTGRES_DB=$Database `
            -p "${PostgresPort}:5432" `
            -d postgres:16 | Out-Null
    }
}

$bytes = New-Object byte[] 48
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try {
    $rng.GetBytes($bytes)
}
finally {
    $rng.Dispose()
}

$jwtKey = [Convert]::ToBase64String($bytes)
$connectionString = "Host=$PostgresHost;Port=$PostgresPort;Database=$Database;Username=$Username;Password=$Password"

dotnet user-secrets set "Jwt:Key" $jwtKey --project $apiProject | Out-Null
dotnet user-secrets set "Jwt:Issuer" $JwtIssuer --project $apiProject | Out-Null
dotnet user-secrets set "Jwt:Audience" $JwtAudience --project $apiProject | Out-Null
dotnet user-secrets set "ConnectionStrings:DefaultConnection" $connectionString --project $apiProject | Out-Null

# Also set user environment variables for IDE sessions that do not load user-secrets as expected.
[Environment]::SetEnvironmentVariable("Jwt__Key", $jwtKey, "User")
[Environment]::SetEnvironmentVariable("Jwt__Issuer", $JwtIssuer, "User")
[Environment]::SetEnvironmentVariable("Jwt__Audience", $JwtAudience, "User")
[Environment]::SetEnvironmentVariable("ConnectionStrings__DefaultConnection", $connectionString, "User")

$env:Jwt__Key = $jwtKey
$env:Jwt__Issuer = $JwtIssuer
$env:Jwt__Audience = $JwtAudience
$env:ConnectionStrings__DefaultConnection = $connectionString

if ($ApplyMigrations) {
    dotnet ef database update --project $infrastructureProject --startup-project $apiProject
}

Write-Host "ServiceMarketplace local development settings are configured outside source control."
Write-Host "JWT: configured"
Write-Host "DefaultConnection: configured for ${PostgresHost}:$PostgresPort/$Database as $Username"
Write-Host "Restart Visual Studio or your terminal before debugging if it was already open."
