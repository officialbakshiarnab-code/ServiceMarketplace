# Database Migration: Add Missing Columns

## Summary
Created EF Core migration to resolve "Invalid column name" errors for `Category`, `Location`, and `Message` columns.

## Issues Fixed

### Before Migration
```
Runtime Errors:
- "Invalid column name 'Category'"
- "Invalid column name 'Location'"
- "Invalid column name 'Message'"

Affected Endpoints:
- GET /api/requests/mine ? 500 error
- GET /api/requests/available ? 500 error
- GET /api/bids/mine ? 500 error
```

### Root Cause
The database schema was missing three columns that were present in the EF Core entities:
1. **ServiceRequest.Category** - Service category/type
2. **ServiceRequest.Location** - Service location
3. **Bid.Message** - Optional message from provider (nullable)

## Migration Details

### Migration File
**Name:** `20250130_AddMissingColumnsToRequestsAndBids`

**Location:** `ServiceMarketplace.Infrastructure/Migrations/`

### Changes Applied

#### 1. ServiceRequests Table
```sql
-- Added columns
ALTER TABLE ServiceRequests
ADD Category NVARCHAR(MAX) NOT NULL DEFAULT ''
ADD Location NVARCHAR(MAX) NOT NULL DEFAULT ''
```

**Properties:**
- `Category` (nvarchar(max), NOT NULL)
  - Default value: empty string
  - Required in entity: `public string Category { get; set; } = string.Empty;`
- `Location` (nvarchar(max), NOT NULL)
  - Default value: empty string
  - Required in entity: `public string Location { get; set; } = string.Empty;`

#### 2. Bids Table
```sql
-- Added column
ALTER TABLE Bids
ADD Message NVARCHAR(MAX) NULL
```

**Properties:**
- `Message` (nvarchar(max), NULL)
  - Nullable: yes
  - Optional in entity: `public string? Message { get; set; }`

## Entity Mappings

### ServiceRequest Entity
```csharp
public class ServiceRequest : BaseAuditableEntity
{
    // ... existing properties ...
    
    public string Category { get; set; } = string.Empty;      // ? Now mapped
    public string Location { get; set; } = string.Empty;      // ? Now mapped
}
```

### Bid Entity
```csharp
public class Bid : BaseAuditableEntity
{
    // ... existing properties ...
    
    public string? Message { get; set; }                       // ? Now mapped
}
```

## Migration Metadata

| Property | Value |
|----------|-------|
| Migration Name | AddMissingColumnsToRequestsAndBids |
| Timestamp | 20250130 |
| Direction | Up |
| Status | ? Built Successfully |

## Files Modified/Created

### Created
1. `ServiceMarketplace.Infrastructure/Migrations/20250130_AddMissingColumnsToRequestsAndBids.cs`
   - Migration class with Up() and Down() methods
   
2. `ServiceMarketplace.Infrastructure/Migrations/20250130_AddMissingColumnsToRequestsAndBids.Designer.cs`
   - Designer metadata for migration

### Modified
1. `ServiceMarketplace.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`
   - Updated Bid entity model to include Message property
   - Updated ServiceRequest entity model to include Category and Location properties

## How to Apply Migration

### Option 1: Using EF Core CLI
```bash
# From project directory containing AppDbContext
cd ServiceMarketplace.Infrastructure

# Apply migration to database
dotnet ef database update

# Or if using specific startup project
dotnet ef database update --startup-project ../ServiceMarketplace.API
```

### Option 2: Using Visual Studio Package Manager Console
```powershell
# Navigate to Package Manager Console
# Set Default project to ServiceMarketplace.Infrastructure

Update-Database
```

## Verification

### After Migration Applied
Run these commands to verify:

```bash
# Check migration status
dotnet ef migrations list

# Expected output should show:
# 20250130_AddMissingColumnsToRequestsAndBids (Pending or Applied)
```

### Test API Endpoints
```bash
# These endpoints should now return 200 OK instead of 500

# Get user's requests
GET /api/requests/mine
Authorization: Bearer {token}

# Get provider's available requests
GET /api/requests/available
Authorization: Bearer {token}

# Get provider's bids
GET /api/bids/mine
Authorization: Bearer {token}
```

## Rollback (If Needed)

### Option 1: Using CLI
```bash
# Rollback to previous migration
dotnet ef database update 20260128223201_RenameIdentityTablesToCleanNames

# Or remove the migration
dotnet ef migrations remove
```

### Option 2: Using Package Manager Console
```powershell
# Rollback
Update-Database -TargetMigration "RenameIdentityTablesToCleanNames"
```

## Impact Analysis

### Database Schema Changes
| Table | Column | Type | Nullable | Default |
|-------|--------|------|----------|---------|
| ServiceRequests | Category | nvarchar(max) | NO | '' |
| ServiceRequests | Location | nvarchar(max) | NO | '' |
| Bids | Message | nvarchar(max) | YES | NULL |

### No Breaking Changes
- ? Existing rows will use default values (empty string for Category/Location)
- ? Message is nullable, so existing bids won't be affected
- ? No stored procedures or views are affected
- ? No foreign key constraints added/removed

### Performance Impact
- ? Minimal: Adding columns doesn't impact existing query performance
- ? New columns are added at end of table
- ? No indexes added on new columns (not needed for current queries)

## Testing Checklist

After applying the migration:

- [ ] Database update command runs without errors
- [ ] `GET /api/requests/mine` returns 200 with request list including Category & Location
- [ ] `GET /api/requests/available` returns 200 with provider's available requests
- [ ] `GET /api/bids/mine` returns 200 with provider's bids including optional Message
- [ ] Create new service request with Category & Location - saves successfully
- [ ] Place bid with optional Message - saves successfully
- [ ] Existing requests/bids display correctly (Category/Location are empty strings)

## Troubleshooting

### Issue: Migration says "Pending"
**Solution:** Ensure startup project is set to ServiceMarketplace.API when running migrations

### Issue: "Invalid column name" still appears
**Solution:** 
1. Verify migration was applied: `dotnet ef migrations list`
2. Check database schema: `SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'ServiceRequests'`
3. Restart application to ensure connection pooling cache is cleared

### Issue: Rollback needed
**Solution:** Use `dotnet ef database update {previous-migration-name}` to revert

## Build Status

? **Build Successful**
- All projects compile without errors
- Migration schema is valid
- EF Core model snapshot updated

## Summary of Changes

| Item | Status |
|------|--------|
| Migration Created | ? |
| Migration Compiles | ? |
| Model Snapshot Updated | ? |
| Up() method defined | ? |
| Down() method defined | ? |
| All projects build | ? |
| Ready to apply | ? |

---

**Next Step:** Run `dotnet ef database update` to apply changes to your database.
