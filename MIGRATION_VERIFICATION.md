# Migration Verification Checklist

## Pre-Application Verification ?

### Step 1: Entity-to-Database Mapping
```
ServiceRequest Entity Properties:
??? Category (string) = string.Empty      ? ServiceRequests.Category (nvarchar(max), NOT NULL)
??? Location (string) = string.Empty      ? ServiceRequests.Location (nvarchar(max), NOT NULL)
??? UpdatedAt (DateTime?) inherited       ? ServiceRequests.UpdatedAt (datetime2, nullable)

Bid Entity Properties:
??? Message (string?) = nullable          ? Bids.Message (nvarchar(max), nullable)
??? UpdatedAt (DateTime?) inherited       ? Bids.UpdatedAt (datetime2, nullable)
```

### Step 2: Migration File Contents
```
File: 20250130_AddMissingColumnsToRequestsAndBids.cs
??? Up Method:
?   ??? migrationBuilder.AddColumn<string>("Category", "ServiceRequests", ...) ?
?   ??? migrationBuilder.AddColumn<string>("Location", "ServiceRequests", ...) ?
?   ??? migrationBuilder.AddColumn<string>("Message", "Bids", ...) ?
??? Down Method:
    ??? migrationBuilder.DropColumn("Message", "Bids") ?
    ??? migrationBuilder.DropColumn("Location", "ServiceRequests") ?
    ??? migrationBuilder.DropColumn("Category", "ServiceRequests") ?
```

### Step 3: Designer File
```
File: 20250130_AddMissingColumnsToRequestsAndBids.Designer.cs
??? BuildTargetModel includes:
?   ??? Bid entity with Message property ?
?   ??? ServiceRequest entity with Category & Location properties ?
```

### Step 4: Model Snapshot
```
File: AppDbContextModelSnapshot.cs
??? Bid entity:
?   ??? b.Property<string>("Message") ?
??? ServiceRequest entity:
    ??? b.Property<string>("Category") ?
    ??? b.Property<string>("Location") ?
```

### Step 5: Build Status
```
Compilation Result:
??? ServiceMarketplace.API ?
??? ServiceMarketplace.Infrastructure ?
??? ServiceMarketplace.Domain ?
??? ServiceMarketplace.Application ?
??? ServiceMarketplace.UI.Web ?
??? ServiceMarketplace.UI.Shared ?
??? ServiceMarketplace.UI.MAUI ?
??? All projects build successfully ?
```

## Column Specifications

### ServiceRequests.Category
```
Column Type: nvarchar(max)
Nullable: NO
Default: '' (empty string)
Required: YES

Entity Definition:
public string Category { get; set; } = string.Empty;

Migration Code:
migrationBuilder.AddColumn<string>(
    name: "Category",
    table: "ServiceRequests",
    type: "nvarchar(max)",
    nullable: false,
    defaultValue: "");
```

### ServiceRequests.Location
```
Column Type: nvarchar(max)
Nullable: NO
Default: '' (empty string)
Required: YES

Entity Definition:
public string Location { get; set; } = string.Empty;

Migration Code:
migrationBuilder.AddColumn<string>(
    name: "Location",
    table: "ServiceRequests",
    type: "nvarchar(max)",
    nullable: false,
    defaultValue: "");
```

### Bids.Message
```
Column Type: nvarchar(max)
Nullable: YES
Default: NULL
Required: NO (Optional)

Entity Definition:
public string? Message { get; set; }

Migration Code:
migrationBuilder.AddColumn<string>(
    name: "Message",
    table: "Bids",
    type: "nvarchar(max)",
    nullable: true);
```

## DTOs Compatibility Check

### CreateServiceRequestDto
```
? Title ? ServiceRequest.Title
? Description ? ServiceRequest.Description
? Category ? ServiceRequest.Category (NOW ADDED)
? Location ? ServiceRequest.Location (NOW ADDED)
? Latitude ? ServiceRequest.Latitude
? Longitude ? ServiceRequest.Longitude
```

### CreateBidDto
```
? ServiceRequestId ? Bid.ServiceRequestId
? Amount ? Bid.Amount
? ProposedDateTime ? Bid.ProposedDateTime
? Message ? Bid.Message (NOW ADDED)
```

### ServiceRequestDto
```
? Id ? ServiceRequest.Id
? CustomerId ? ServiceRequest.CustomerId
? Title ? ServiceRequest.Title
? Description ? ServiceRequest.Description
? Category ? ServiceRequest.Category (NOW ADDED)
? Location ? ServiceRequest.Location (NOW ADDED)
? Latitude ? ServiceRequest.Latitude
? Longitude ? ServiceRequest.Longitude
? Status ? ServiceRequest.Status
? BidCount ? r.Bids.Count
? CreatedAt ? ServiceRequest.CreatedAt
? UpdatedAt ? ServiceRequest.UpdatedAt
```

### BidDto
```
? Id ? Bid.Id
? ServiceRequestId ? Bid.ServiceRequestId
? ServiceProviderId ? Bid.ServiceProviderId
? Amount ? Bid.Amount
? ProposedDateTime ? Bid.ProposedDateTime
? Message ? Bid.Message (NOW ADDED)
? Status ? Bid.Status
? CreatedAt ? Bid.CreatedAt
? UpdatedAt ? Bid.UpdatedAt
```

## Endpoints Impact

### GET /api/requests/mine
**Before Migration:** ? "Invalid column name 'Category'"
**After Migration:** ? Returns ServiceRequestDto with Category & Location

**Query Execution:**
```sql
SELECT 
    r.Id, r.CustomerId, r.Title, r.Description, 
    r.Category,     -- NOW AVAILABLE ?
    r.Location,     -- NOW AVAILABLE ?
    r.Latitude, r.Longitude, 
    r.Status, 
    (SELECT COUNT(*) FROM Bids WHERE ServiceRequestId = r.Id) as BidCount,
    r.CreatedAt, r.UpdatedAt
FROM ServiceRequests r
WHERE r.CustomerId = @userId
ORDER BY r.CreatedAt DESC
```

### GET /api/requests/available
**Before Migration:** ? "Invalid column name 'Category'"
**After Migration:** ? Returns available requests with Category & Location

**Query Execution:**
```sql
SELECT 
    r.Id, r.CustomerId, r.Title, r.Description,
    r.Category,     -- NOW AVAILABLE ?
    r.Location,     -- NOW AVAILABLE ?
    r.Latitude, r.Longitude,
    r.Status,
    (SELECT COUNT(*) FROM Bids WHERE ServiceRequestId = r.Id) as BidCount,
    r.CreatedAt, r.UpdatedAt
FROM ServiceRequests r
WHERE r.Status = 0 AND r.CustomerId != @providerId
ORDER BY r.CreatedAt DESC
```

### GET /api/bids/mine
**Before Migration:** ? "Invalid column name 'Message'"
**After Migration:** ? Returns ProviderBidDto with Message

**Query Execution:**
```sql
SELECT 
    b.Id, b.ServiceRequestId, 
    r.Title as RequestTitle,
    b.Amount, b.ProposedDateTime,
    b.Message,     -- NOW AVAILABLE ?
    b.Status,
    b.CreatedAt
FROM Bids b
LEFT JOIN ServiceRequests r ON b.ServiceRequestId = r.Id
WHERE b.ServiceProviderId = @providerId
ORDER BY b.CreatedAt DESC
```

## Database State After Migration

### ServiceRequests Table Structure
```
Columns:
??? Id (uniqueidentifier) PRIMARY KEY
??? CustomerId (nvarchar(max)) NOT NULL
??? Title (nvarchar(max)) NOT NULL
??? Description (nvarchar(max)) NOT NULL
??? Category (nvarchar(max)) NOT NULL ? NEW
??? Location (nvarchar(max)) NOT NULL ? NEW
??? Latitude (float) NOT NULL
??? Longitude (float) NOT NULL
??? Status (int) NOT NULL
??? CreatedAt (datetime2) NOT NULL
??? UpdatedAt (datetime2) nullable

Indexes:
??? PK_ServiceRequests (Id)
```

### Bids Table Structure
```
Columns:
??? Id (uniqueidentifier) PRIMARY KEY
??? ServiceRequestId (uniqueidentifier) NOT NULL
??? ServiceProviderId (nvarchar(max)) NOT NULL
??? Amount (decimal(18,2)) NOT NULL
??? ProposedDateTime (datetime2) NOT NULL
??? Message (nvarchar(max)) nullable ? NEW
??? Status (int) NOT NULL
??? CreatedAt (datetime2) NOT NULL
??? UpdatedAt (datetime2) nullable

Indexes:
??? PK_Bids (Id)
??? FK_Bids_ServiceRequests (ServiceRequestId)
??? IX_Bids_ServiceRequestId
```

## Backward Compatibility

### Existing Records
- ? ServiceRequests.Category = '' (empty string) for existing records
- ? ServiceRequests.Location = '' (empty string) for existing records
- ? Bids.Message = NULL for existing records
- ? No data loss on existing records
- ? Queries continue to work with new columns

### Rollback Safety
- ? Down() method removes columns in reverse order
- ? No data is dropped until Down() is executed
- ? Migration can be safely reversed if needed

## Performance Considerations

### Query Impact
- ? Adding columns doesn't affect existing query performance
- ? New string columns (nvarchar(max)) - no size limit, no performance penalty
- ? No full-text indexes created
- ? No additional indexes needed for current queries

### Disk Space
- ? Existing rows: ~0-8 KB per row added (empty strings)
- ? New rows: additional space depends on content
- ? Overall impact: minimal for typical data sizes

## Application Compatibility

### Controllers
- ? ServiceRequestsController reads Category & Location
- ? BidsController reads Message
- ? No controller changes needed
- ? DTOs already support these fields

### Services
- ? ServiceRequestService queries include new columns
- ? BidService queries include Message
- ? No service changes needed

### Validators
- ? CreateServiceRequestDtoValidator validates Category & Location
- ? CreateBidDtoValidator validates Message (optional)
- ? No validator changes needed

## Sign-Off Checklist

- [x] Migration file created
- [x] Designer file created
- [x] Model snapshot updated
- [x] All projects build successfully
- [x] No compilation errors
- [x] No breaking changes
- [x] Backward compatible
- [x] Ready for database update

## Next Steps

1. **Apply Migration to Development Database:**
   ```bash
   dotnet ef database update --project ServiceMarketplace.Infrastructure
   ```

2. **Test API Endpoints:**
   ```bash
   GET /api/requests/mine
   GET /api/requests/available
   GET /api/bids/mine
   ```

3. **Verify Data:**
   ```sql
   SELECT TOP 5 * FROM ServiceRequests;
   SELECT TOP 5 * FROM Bids;
   ```

4. **Apply to Production:**
   - Back up production database
   - Apply migration same way as development
   - Monitor for errors

---

**Migration Status:** ? Ready to Apply
**Build Status:** ? Successful
**Compatibility:** ? Backward Compatible
