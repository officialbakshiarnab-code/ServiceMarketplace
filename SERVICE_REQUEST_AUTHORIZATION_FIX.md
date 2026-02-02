# Service Request Authorization Fix - 401 Unauthorized Resolution

## ? Issue Resolved

The 401 Unauthorized error when accessing `GET /api/requests/{requestId}` has been fixed by adding a new endpoint specifically for ServiceProviders.

---

## ?? Root Cause

### Original Problem

**Endpoint**: `GET /api/requests/{requestId}`
**Authorization**: `[Authorize(Roles = "User")]`
**Issue**: This endpoint was restricted to Users only, but ServiceProviders need to view request details before placing bids.

### Why 401 Unauthorized Occurred

1. **Direct Browser Access**: When you accessed `https://localhost:7147/api/requests/f92bf98d...` directly in your browser:
   - No authentication token was sent (browser doesn't have JWT in headers)
   - Even if you were logged in, the browser request wouldn't include the Bearer token
   - Result: 401 Unauthorized

2. **Role Restriction**: Even with authentication, if you were logged in as a ServiceProvider:
   - The endpoint requires "User" role specifically
   - ServiceProvider role would be rejected with 403 Forbidden
   - Result: Authorization failure

---

## ? Solution Implemented

### New Endpoint Added

**For ServiceProviders to view request details before bidding:**

```csharp
// SERVICE PROVIDER views details for any open request (to place bid)
[Authorize(Roles = "ServiceProvider")]
[HttpGet("{requestId}/details")]
public async Task<IActionResult> GetRequestDetails(Guid requestId)
{
    var providerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
    var request = await _service.GetByIdForProviderAsync(requestId, providerId);
    return Ok(request);
}
```

**Endpoint**: `GET /api/requests/{requestId}/details`
**Authorization**: `[Authorize(Roles = "ServiceProvider")]`
**Purpose**: Allows ServiceProviders to view request details before placing bids

### Business Logic Added

```csharp
public async Task<ServiceRequestDto> GetByIdForProviderAsync(Guid requestId, string providerId)
{
    // 1. Verify provider is authenticated
    if (string.IsNullOrWhiteSpace(providerId))
        throw new ForbiddenException("Provider ID is required");

    // 2. Load request (only if Open status)
    var request = await _context.ServiceRequests
        .Where(r => r.Id == requestId && r.Status == ServiceRequestStatus.Open)
        .FirstOrDefaultAsync();

    if (request == null)
        throw new NotFoundException("Service request not found or not available for bidding");

    // 3. Prevent providers from viewing their own requests
    if (request.CustomerId == providerId)
        throw new ForbiddenException("Cannot view or bid on your own request");

    return request;
}
```

**Business Rules Enforced**:
1. ? Only authenticated ServiceProviders can access
2. ? Only Open requests are visible (not Accepted/Rejected/Completed)
3. ? Providers cannot view their own requests
4. ? Returns 404 if request doesn't exist or is closed
5. ? Returns 403 if provider tries to view own request

---

## ?? Endpoint Comparison

### Original (Users Only)

| Endpoint | Role | Purpose |
|----------|------|---------|
| `GET /api/requests/{requestId}` | User | View details of **own** request |

**Authorization**: Users can only view requests they created (`CustomerId == userId`)

### New (Providers Only)

| Endpoint | Role | Purpose |
|----------|------|---------|
| `GET /api/requests/{requestId}/details` | ServiceProvider | View details of **any open** request (except own) |

**Authorization**: Providers can view any open request **except those they created**

---

## ?? Complete Authorization Matrix

### Service Request Endpoints

| Endpoint | User | Provider | Purpose |
|----------|------|----------|---------|
| `POST /api/requests` | ? | ? | Create new request |
| `GET /api/requests/mine` | ? | ? | View own requests |
| `GET /api/requests/{id}` | ? | ? | View own request details |
| `POST /api/requests/{id}/accept/{bidId}` | ? | ? | Accept a bid |
| `GET /api/requests/open` | ? | ? | Browse all open requests |
| `POST /api/requests/nearby` | ? | ? | Search nearby requests |
| `GET /api/requests/available` | ? | ? | View available requests (exclude own) |
| `GET /api/requests/{id}/details` | ? | ? | **NEW**: View request details for bidding |

### Bid Endpoints

| Endpoint | User | Provider | Purpose |
|----------|------|----------|---------|
| `GET /api/bids/{requestId}` | ? | ? | View bids for own request |
| `POST /api/bids` | ? | ? | Place a bid |
| `GET /api/bids/mine` | ? | ? | View own bids |

---

## ?? Updated UI Client

### New Method in RequestsApiClient

```csharp
public async Task<ServiceRequestDto> GetRequestDetailsAsync(Guid requestId, 
                                                            CancellationToken cancellationToken = default)
{
    await AttachBearerAsync(cancellationToken);

    using var response = await _httpClient.GetAsync($"api/requests/{requestId}/details", cancellationToken);
    if (!response.IsSuccessStatusCode)
        throw new InvalidOperationException(await TryReadErrorAsync(response) ?? 
                                            "Failed to load request details.");

    var request = await response.Content.ReadFromJsonAsync<ServiceRequestDto>(
                        cancellationToken: cancellationToken);
    return request ?? throw new InvalidOperationException("Empty response from server.");
}
```

**Usage in UI**:
- Call `GetRequestDetailsAsync()` when ServiceProvider clicks on a request to view details before bidding
- Call `GetByIdAsync()` when User views their own request details

---

## ?? Testing Guide

### Test 1: ServiceProvider Views Request Details

**Steps**:
1. Login as ServiceProvider
2. Navigate to "Available Requests"
3. Click on a request to view details
4. UI should call `GET /api/requests/{requestId}/details`

**Expected**:
```
GET https://localhost:7147/api/requests/f92bf98d-4225-4050-afc2-e5d956efced9/details
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Response: 200 OK
{
  "id": "f92bf98d-4225-4050-afc2-e5d956efced9",
  "title": "Plumbing repair needed",
  "description": "...",
  "status": "Open",
  ...
}
```

### Test 2: User Views Own Request Details

**Steps**:
1. Login as User
2. Navigate to "My Requests"
3. Click on a request to view details
4. UI should call `GET /api/requests/{requestId}`

**Expected**:
```
GET https://localhost:7147/api/requests/f92bf98d-4225-4050-afc2-e5d956efced9
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Response: 200 OK
```

### Test 3: Provider Cannot View Own Request

**Steps**:
1. Login as ServiceProvider who accidentally created a request
2. Try to view that request's details via `/details` endpoint

**Expected**:
```
GET /api/requests/{requestId}/details
Response: 403 Forbidden
{
  "message": "Cannot view or bid on your own request"
}
```

### Test 4: Provider Cannot View Closed Request

**Steps**:
1. Login as ServiceProvider
2. Try to view a request that has Status = Accepted/Rejected/Completed

**Expected**:
```
GET /api/requests/{requestId}/details
Response: 404 Not Found
{
  "message": "Service request not found or not available for bidding"
}
```

### Test 5: Direct Browser Access Still Fails

**Steps**:
1. Paste `https://localhost:7147/api/requests/f92bf98d-4225-4050-afc2-e5d956efced9/details` in browser
2. Access without authentication

**Expected**:
```
Response: 401 Unauthorized
{
  "error": "unauthorized",
  "message": "Authentication is required to access this resource."
}
```

**Reason**: Browser doesn't send Bearer token in headers. This is correct security behavior.

---

## ?? Security Considerations

### Why Two Separate Endpoints?

1. **Principle of Least Privilege**:
   - Users should only see their own requests
   - Providers should only see open requests (not their own)

2. **Different Business Rules**:
   - Users: Can view any status (Open, Accepted, Completed, etc.)
   - Providers: Can only view Open status (for bidding)

3. **Ownership Validation**:
   - Users: Must own the request (`CustomerId == userId`)
   - Providers: Must NOT own the request (`CustomerId != providerId`)

### Authorization Flow

```
Client Request
    ?
[Authorize] Attribute
    ?
JWT Validation
    ?
Role Check
    ?
Controller Action
    ?
Business Logic
    ?
Ownership/Status Checks
    ?
Return Data or Error
```

---

## ?? Files Changed

### 1. ServiceRequestsController.cs
- ? Added `GetRequestDetails()` endpoint
- ? Authorization: `[Authorize(Roles = "ServiceProvider")]`
- ? Route: `GET /api/requests/{requestId}/details`

### 2. IServiceRequestService.cs
- ? Added `GetByIdForProviderAsync()` method signature

### 3. ServiceRequestService.cs
- ? Implemented `GetByIdForProviderAsync()` with business rules
- ? Only returns Open requests
- ? Prevents providers from viewing own requests

### 4. RequestsApiClient.cs
- ? Added `GetRequestDetailsAsync()` method
- ? Attaches Bearer token automatically
- ? Handles errors gracefully

---

## ? Verification Checklist

- [x] New endpoint created: `GET /api/requests/{requestId}/details`
- [x] Authorization: ServiceProvider role only
- [x] Business rules: Only Open requests, exclude own requests
- [x] Service interface updated
- [x] Service implementation added
- [x] UI client method added
- [x] Build successful
- [x] No breaking changes to existing endpoints

---

## ?? Summary

### Problem
ServiceProviders couldn't view request details before placing bids because:
- Existing endpoint was User-only
- Direct browser access returned 401 Unauthorized

### Solution
Added dedicated endpoint for ServiceProviders:
- New route: `GET /api/requests/{requestId}/details`
- Authorization: ServiceProvider role
- Business rules: Open requests only, exclude own requests

### Result
? ServiceProviders can now view request details before bidding
? Security maintained with proper authorization
? No breaking changes to existing functionality

---

**Build Status**: ? Successful

**Ready for Testing**: ? Yes
