# Authentication & Authorization Test Script
# Tests login, JWT generation, logout, and authorization flows

$ErrorActionPreference = "Continue"
$apiBaseUrl = "https://localhost:7147"
$webBaseUrl = "https://localhost:7241"

Write-Host "==================================" -ForegroundColor Cyan
Write-Host "Authentication & Authorization Test" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

# Ignore SSL certificate errors for local testing
add-type @"
using System.Net;
using System.Security.Cryptography.X509Certificates;
public class TrustAllCertsPolicy : ICertificatePolicy {
    public bool CheckValidationResult(
        ServicePoint svcPt, X509Certificate cert,
        WebRequest req, int problem) {
        return true;
    }
}
"@
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12

# Test Results
$testResults = @()

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Url,
        [string]$Method = "GET",
        [hashtable]$Headers = @{},
        [object]$Body = $null,
        [int[]]$ExpectedStatusCodes = @(200)
    )
    
    Write-Host "Testing: $Name" -ForegroundColor Yellow
    Write-Host "  URL: $Method $Url"
    
    try {
        $params = @{
            Uri = $Url
            Method = $Method
            Headers = $Headers
            UseBasicParsing = $true
        }
        
        if ($Body) {
            $params.Body = ($Body | ConvertTo-Json -Depth 10)
            $params.ContentType = "application/json"
        }
        
        $response = Invoke-WebRequest @params -ErrorAction Stop
        
        if ($ExpectedStatusCodes -contains $response.StatusCode) {
            Write-Host "  ? PASSED - Status: $($response.StatusCode)" -ForegroundColor Green
            $script:testResults += [PSCustomObject]@{
                Test = $Name
                Status = "PASSED"
                StatusCode = $response.StatusCode
                Details = "Success"
            }
            Write-Host ""
            return $response
        } else {
            Write-Host "  ? FAILED - Expected: $ExpectedStatusCodes, Got: $($response.StatusCode)" -ForegroundColor Red
            $script:testResults += [PSCustomObject]@{
                Test = $Name
                Status = "FAILED"
                StatusCode = $response.StatusCode
                Details = "Unexpected status code"
            }
            Write-Host ""
            return $null
        }
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($ExpectedStatusCodes -contains $statusCode) {
            Write-Host "  ? PASSED - Status: $statusCode (Expected error)" -ForegroundColor Green
            $script:testResults += [PSCustomObject]@{
                Test = $Name
                Status = "PASSED"
                StatusCode = $statusCode
                Details = "Expected error"
            }
            Write-Host ""
            return $_.Exception.Response
        } else {
            Write-Host "  ? FAILED - Error: $($_.Exception.Message)" -ForegroundColor Red
            $script:testResults += [PSCustomObject]@{
                Test = $Name
                Status = "FAILED"
                StatusCode = $statusCode
                Details = $_.Exception.Message
            }
            Write-Host ""
            return $null
        }
    }
}

# Test 1: Check if API is running
Write-Host "`n[Test 1] API Health Check" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
$apiHealth = Test-Endpoint -Name "API Running" -Url "$apiBaseUrl/swagger/index.html" -ExpectedStatusCodes @(200, 301, 302)

# Test 2: Check if Web UI is running
Write-Host "`n[Test 2] Web UI Health Check" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
$webHealth = Test-Endpoint -Name "Web UI Running" -Url "$webBaseUrl/" -ExpectedStatusCodes @(200)

# Test 3: Try to access protected endpoint without auth (should get 401)
Write-Host "`n[Test 3] Authorization Test - No Token" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Test-Endpoint -Name "Protected endpoint without auth" -Url "$apiBaseUrl/api/requests/mine" -ExpectedStatusCodes @(401)

# Test 4: Try to register a test user
Write-Host "`n[Test 4] User Registration" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
$testEmail = "testuser_$(Get-Random)@example.com"
$testPassword = "Test@123456"
$registerBody = @{
    Email = $testEmail
    Password = $testPassword
    Role = "User"
}
$registerResponse = Test-Endpoint -Name "Register new user" -Url "$apiBaseUrl/api/auth/register" -Method POST -Body $registerBody -ExpectedStatusCodes @(200, 400)

# Test 5: Login with the test user
Write-Host "`n[Test 5] User Login & JWT Generation" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
$loginBody = @{
    Email = $testEmail
    Password = $testPassword
}
$loginResponse = Test-Endpoint -Name "Login with credentials" -Url "$apiBaseUrl/api/auth/login" -Method POST -Body $loginBody -ExpectedStatusCodes @(200, 401)

$token = $null
$expiresAt = $null
if ($loginResponse) {
    try {
        $loginData = $loginResponse.Content | ConvertFrom-Json
        $token = $loginData.Token
        $expiresAt = $loginData.ExpiresAt
        
        Write-Host "  Token received: $($token.Substring(0, 50))..." -ForegroundColor Green
        Write-Host "  Expires at: $expiresAt" -ForegroundColor Green
        
        # Verify token expiration is 10 minutes
        $expirationTime = [DateTime]::Parse($expiresAt)
        $now = [DateTime]::UtcNow
        $minutesUntilExpiry = ($expirationTime - $now).TotalMinutes
        
        Write-Host "  Token lifetime: $([math]::Round($minutesUntilExpiry, 2)) minutes" -ForegroundColor $(if ($minutesUntilExpiry -le 10.5 -and $minutesUntilExpiry -ge 9.5) { "Green" } else { "Red" })
        
        if ($minutesUntilExpiry -le 10.5 -and $minutesUntilExpiry -ge 9.5) {
            Write-Host "  ? Token expiration is correct (10 minutes)" -ForegroundColor Green
        } else {
            Write-Host "  ? Token expiration is incorrect (expected 10 minutes)" -ForegroundColor Red
        }
    }
    catch {
        Write-Host "  ? Failed to parse login response: $($_.Exception.Message)" -ForegroundColor Red
    }
}

# Test 6: Access protected endpoint WITH auth token (should get 200 or appropriate response)
if ($token) {
    Write-Host "`n[Test 6] Authorization Test - With Token" -ForegroundColor Cyan
    Write-Host "======================================" -ForegroundColor Cyan
    $authHeaders = @{
        Authorization = "Bearer $token"
    }
    Test-Endpoint -Name "Protected endpoint with auth" -Url "$apiBaseUrl/api/requests/mine" -Headers $authHeaders -ExpectedStatusCodes @(200)
}

# Test 7: Try to access ServiceProvider-only endpoint with User role (should get 403)
if ($token) {
    Write-Host "`n[Test 7] Role-Based Authorization Test" -ForegroundColor Cyan
    Write-Host "======================================" -ForegroundColor Cyan
    $authHeaders = @{
        Authorization = "Bearer $token"
    }
    Test-Endpoint -Name "ServiceProvider endpoint with User role" -Url "$apiBaseUrl/api/requests/open" -Headers $authHeaders -ExpectedStatusCodes @(403)
}

# Test 8: Logout
if ($token) {
    Write-Host "`n[Test 8] User Logout" -ForegroundColor Cyan
    Write-Host "======================================" -ForegroundColor Cyan
    $authHeaders = @{
        Authorization = "Bearer $token"
    }
    $logoutResponse = Test-Endpoint -Name "Logout" -Url "$apiBaseUrl/api/auth/logout" -Method POST -Headers $authHeaders -ExpectedStatusCodes @(200)
    
    if ($logoutResponse) {
        Write-Host "  ? Logout successful - audit log should have LogoutTime" -ForegroundColor Green
    }
}

# Test 9: Try to use token after logout (should get 401 or still work until expiry)
if ($token) {
    Write-Host "`n[Test 9] Token Usage After Logout" -ForegroundColor Cyan
    Write-Host "======================================" -ForegroundColor Cyan
    Write-Host "  Note: JWT tokens are stateless, so token still works until expiry" -ForegroundColor Yellow
    $authHeaders = @{
        Authorization = "Bearer $token"
    }
    Test-Endpoint -Name "Protected endpoint after logout" -Url "$apiBaseUrl/api/requests/mine" -Headers $authHeaders -ExpectedStatusCodes @(200)
}

# Test 10: Register a ServiceProvider and test their endpoints
Write-Host "`n[Test 10] ServiceProvider Registration & Authorization" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
$providerEmail = "provider_$(Get-Random)@example.com"
$providerPassword = "Provider@123456"
$providerRegisterBody = @{
    Email = $providerEmail
    Password = $providerPassword
    Role = "ServiceProvider"
}
$providerRegisterResponse = Test-Endpoint -Name "Register new provider" -Url "$apiBaseUrl/api/auth/register" -Method POST -Body $providerRegisterBody -ExpectedStatusCodes @(200, 400)

# Login as provider
$providerLoginBody = @{
    Email = $providerEmail
    Password = $providerPassword
}
$providerLoginResponse = Test-Endpoint -Name "Provider login" -Url "$apiBaseUrl/api/auth/login" -Method POST -Body $providerLoginBody -ExpectedStatusCodes @(200, 401)

$providerToken = $null
if ($providerLoginResponse) {
    try {
        $providerLoginData = $providerLoginResponse.Content | ConvertFrom-Json
        $providerToken = $providerLoginData.Token
        Write-Host "  Provider token received" -ForegroundColor Green
    }
    catch {
        Write-Host "  ? Failed to parse provider login response" -ForegroundColor Red
    }
}

# Test provider can access ServiceProvider endpoints
if ($providerToken) {
    Write-Host "`n[Test 11] ServiceProvider Endpoint Access" -ForegroundColor Cyan
    Write-Host "======================================" -ForegroundColor Cyan
    $providerHeaders = @{
        Authorization = "Bearer $providerToken"
    }
    Test-Endpoint -Name "Provider accessing open requests" -Url "$apiBaseUrl/api/requests/open" -Headers $providerHeaders -ExpectedStatusCodes @(200)
}

# Test provider CANNOT create service requests (User-only feature)
if ($providerToken) {
    Write-Host "`n[Test 12] ServiceProvider Forbidden Action" -ForegroundColor Cyan
    Write-Host "======================================" -ForegroundColor Cyan
    $providerHeaders = @{
        Authorization = "Bearer $providerToken"
    }
    $createRequestBody = @{
        Title = "Test Request"
        Description = "This should fail"
        Category = "Plumbing"
        Location = "Test Location"
        Latitude = 0.0
        Longitude = 0.0
    }
    Test-Endpoint -Name "Provider creating service request (should fail)" -Url "$apiBaseUrl/api/requests" -Method POST -Headers $providerHeaders -Body $createRequestBody -ExpectedStatusCodes @(403)
}

# Summary
Write-Host "`n==================================" -ForegroundColor Cyan
Write-Host "Test Summary" -ForegroundColor Cyan
Write-Host "==================================" -ForegroundColor Cyan
Write-Host ""

$passed = ($testResults | Where-Object { $_.Status -eq "PASSED" }).Count
$failed = ($testResults | Where-Object { $_.Status -eq "FAILED" }).Count
$total = $testResults.Count

Write-Host "Total Tests: $total" -ForegroundColor White
Write-Host "Passed: $passed" -ForegroundColor Green
Write-Host "Failed: $failed" -ForegroundColor $(if ($failed -gt 0) { "Red" } else { "Green" })
Write-Host ""

if ($failed -eq 0) {
    Write-Host "? ALL TESTS PASSED!" -ForegroundColor Green
} else {
    Write-Host "? Some tests failed. Review the output above." -ForegroundColor Red
}

Write-Host ""
Write-Host "Detailed Results:" -ForegroundColor Cyan
$testResults | Format-Table -AutoSize

Write-Host ""
Write-Host "Manual Testing Required:" -ForegroundColor Yellow
Write-Host "  1. Open browser to: $webBaseUrl" -ForegroundColor White
Write-Host "  2. Navigate to /login" -ForegroundColor White
Write-Host "  3. Try logging in with:" -ForegroundColor White
Write-Host "     - Email: $testEmail" -ForegroundColor White
Write-Host "     - Password: $testPassword" -ForegroundColor White
Write-Host "  4. Verify:" -ForegroundColor White
Write-Host "     - Login succeeds" -ForegroundColor White
Write-Host "     - Redirects to /user/dashboard" -ForegroundColor White
Write-Host "     - Dashboard shows user-specific features" -ForegroundColor White
Write-Host "     - No console errors (F12)" -ForegroundColor White
Write-Host "     - Network tab shows no 4xx/5xx errors" -ForegroundColor White
Write-Host "  5. Click 'Sign Out' button" -ForegroundColor White
Write-Host "  6. Verify:" -ForegroundColor White
Write-Host "     - Logout succeeds" -ForegroundColor White
Write-Host "     - Redirects to /login" -ForegroundColor White
Write-Host "     - No console errors" -ForegroundColor White
Write-Host "     - Network tab shows no 4xx/5xx errors" -ForegroundColor White
Write-Host ""
Write-Host "Database Verification:" -ForegroundColor Yellow
Write-Host "  Run this SQL query to verify audit logs:" -ForegroundColor White
Write-Host "  SELECT TOP 10 * FROM AuditLogs ORDER BY TimestampUtc DESC;" -ForegroundColor Gray
Write-Host ""
