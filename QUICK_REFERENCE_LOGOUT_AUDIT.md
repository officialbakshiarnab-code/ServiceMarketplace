# Quick Reference - Logout Audit Testing

## ?? Quick Start (Copy & Paste)

### 1. Start API
```bash
cd C:\MyProject\ServiceMarketplace\ServiceMarketplace.API
dotnet run
```

### 2. Start UI
```bash
cd C:\MyProject\ServiceMarketplace\ServiceMarketplace.UI.Web
dotnet run
```

### 3. Test Logout
1. Navigate to `https://localhost:7241`
2. Login
3. Click "Sign Out"

### 4. Verify in SQL
```sql
SELECT TOP 5 EventType, TimestampUtc, UserId, SessionId
FROM AuditLogs
ORDER BY TimestampUtc DESC;
```

**Expected Result:**
```
EventType | TimestampUtc           | UserId    | SessionId
----------|------------------------|-----------|----------
Logout    | 2025-02-01 14:35:22   | abc123... | def456...
Login     | 2025-02-01 14:30:15   | abc123... | def456...
```

---

## ? Success Indicators

### Browser Console (F12 ? Console)
```
[LogoutButton] ========== LOGOUT FLOW STARTED ==========
[AuthApiClient] Calling logout API with Bearer token...
[AuthApiClient] Logout API call succeeded - audit record created
[LogoutButton] ========== LOGOUT FLOW COMPLETED ==========
```

### API Console
```
AUDIT INSERTED: Logout | UserId: abc123... | SessionId: def456... | Timestamp: 2025-02-01 14:35:22 UTC
```

### Database
```sql
-- Latest row should be Logout
SELECT TOP 1 * FROM AuditLogs ORDER BY TimestampUtc DESC;
```

---

## ? Common Issues

### Issue: No "AUDIT INSERTED" in API console
**Fix:** Check if logout endpoint is being called
- Open browser DevTools ? Network tab
- Click "Sign Out"
- Look for POST request to `/api/auth/logout`
- Check response status (should be 200 OK)

### Issue: 401 Unauthorized
**Fix:** Token not attached to request
- Verify `AuthApiClient.LogoutAsync()` attaches Bearer token
- Check that token is not expired

### Issue: No database record
**Fix:** Check database connection
- Verify connection string in `appsettings.json`
- Ensure `SaveChangesAsync()` is called

---

## ?? Full Verification Script

Run this SQL script for complete verification:
```sql
-- File: SQL_LOGOUT_AUDIT_VERIFICATION.sql
-- Location: C:\MyProject\ServiceMarketplace\SQL_LOGOUT_AUDIT_VERIFICATION.sql
```

Or quick check:
```sql
SELECT 
    EventType,
    COUNT(*) AS Total
FROM AuditLogs
GROUP BY EventType;
```

**Expected:**
```
EventType      | Total
---------------|------
Login          | N
Logout         | M  (where M ? N)
SessionExpired | X
```

---

## ?? Repeat Testing

To verify multiple logouts:
1. Login ? Logout ? Check (should see 1 logout record)
2. Login ? Logout ? Check (should see 2 logout records)
3. Login ? Logout ? Check (should see 3 logout records)

**Each logout creates a NEW row (append-only)**

---

## ?? Documentation Files

| File | Purpose |
|------|---------|
| `LOGOUT_AUDIT_VERIFICATION_GUIDE.md` | Complete testing guide |
| `SQL_LOGOUT_AUDIT_VERIFICATION.sql` | Comprehensive SQL verification |
| `LOGOUT_AUDIT_IMPLEMENTATION.md` | Technical implementation details |
| `SESSION_EXPIRY_AUDIT_IMPLEMENTATION.md` | Session expiry specifics |
| `AUDIT_IMPLEMENTATION_VERIFICATION.md` | Full system verification |

---

## ?? Need Help?

1. Check console logs (both browser and API)
2. Run SQL verification script
3. Review documentation files
4. Verify all files compiled successfully

---

## ? Final Checklist

- [ ] API console shows "AUDIT INSERTED: Logout"
- [ ] Browser console shows complete logout flow
- [ ] Database has new Logout record
- [ ] TimestampUtc is current UTC time
- [ ] User is redirected to /login
- [ ] Token is cleared from storage
- [ ] User cannot access protected pages

**If all checked: ? SUCCESS!**

---

**Quick Reference Version**: 1.0  
**Last Updated**: 2025-02-01
