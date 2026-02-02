-- ============================================
-- LOGOUT AUDIT VERIFICATION SQL SCRIPT
-- ============================================
-- Purpose: Verify that logout audit records are being created correctly
-- Usage: Run this script after clicking "Sign Out" in the UI
-- Expected: Latest row should have EventType = 'Logout'
-- ============================================

-- ============================================
-- SECTION 1: BASIC VERIFICATION
-- ============================================

PRINT '========================================';
PRINT 'SECTION 1: BASIC VERIFICATION';
PRINT '========================================';
PRINT '';

-- Query 1: Latest audit events
PRINT '1. Latest Audit Events (Expected: Logout at top):';
PRINT '---------------------------------------------------';
SELECT TOP 10
    Id,
    EventType,
    UserId,
    Role,
    SessionId,
    TimestampUtc,
    IpAddress
FROM AuditLogs
ORDER BY TimestampUtc DESC;
PRINT '';

-- Query 2: Count by event type
PRINT '2. Event Counts by Type:';
PRINT '---------------------------------------------------';
SELECT 
    EventType,
    COUNT(*) AS Total,
    MIN(TimestampUtc) AS FirstEvent,
    MAX(TimestampUtc) AS LastEvent
FROM AuditLogs
GROUP BY EventType
ORDER BY EventType;
PRINT '';

-- ============================================
-- SECTION 2: LOGOUT-SPECIFIC VERIFICATION
-- ============================================

PRINT '========================================';
PRINT 'SECTION 2: LOGOUT-SPECIFIC VERIFICATION';
PRINT '========================================';
PRINT '';

-- Query 3: All logout events
PRINT '3. All Logout Events:';
PRINT '---------------------------------------------------';
SELECT 
    Id,
    TimestampUtc,
    UserId,
    Role,
    SessionId,
    IpAddress,
    UserAgent
FROM AuditLogs
WHERE EventType = 'Logout'
ORDER BY TimestampUtc DESC;
PRINT '';

-- Query 4: Recent logout activity (last hour)
PRINT '4. Recent Logout Activity (Last Hour):';
PRINT '---------------------------------------------------';
SELECT 
    COUNT(*) AS LogoutCount,
    COUNT(DISTINCT UserId) AS UniqueUsers,
    COUNT(DISTINCT SessionId) AS UniqueSessions,
    MIN(TimestampUtc) AS FirstLogout,
    MAX(TimestampUtc) AS LastLogout
FROM AuditLogs
WHERE EventType = 'Logout'
  AND TimestampUtc >= DATEADD(HOUR, -1, GETUTCDATE());
PRINT '';

-- ============================================
-- SECTION 3: SESSION LIFECYCLE ANALYSIS
-- ============================================

PRINT '========================================';
PRINT 'SECTION 3: SESSION LIFECYCLE ANALYSIS';
PRINT '========================================';
PRINT '';

-- Query 5: Complete session lifecycle
PRINT '5. Complete Session Lifecycle:';
PRINT '---------------------------------------------------';
WITH SessionEvents AS (
    SELECT 
        SessionId,
        UserId,
        Role,
        EventType,
        TimestampUtc,
        IpAddress
    FROM AuditLogs
    WHERE SessionId IS NOT NULL
)
SELECT 
    SessionId,
    UserId,
    Role,
    MAX(CASE WHEN EventType = 'Login' THEN TimestampUtc END) AS LoginTime,
    MAX(CASE WHEN EventType = 'Logout' THEN TimestampUtc END) AS LogoutTime,
    MAX(CASE WHEN EventType = 'SessionExpired' THEN TimestampUtc END) AS ExpiredTime,
    DATEDIFF(SECOND, 
        MAX(CASE WHEN EventType = 'Login' THEN TimestampUtc END),
        COALESCE(
            MAX(CASE WHEN EventType = 'Logout' THEN TimestampUtc END),
            MAX(CASE WHEN EventType = 'SessionExpired' THEN TimestampUtc END)
        )
    ) AS SessionDurationSeconds,
    CASE 
        WHEN MAX(CASE WHEN EventType = 'Logout' THEN 1 ELSE 0 END) = 1 THEN 'Manual Logout'
        WHEN MAX(CASE WHEN EventType = 'SessionExpired' THEN 1 ELSE 0 END) = 1 THEN 'Auto Expired'
        ELSE 'Active'
    END AS SessionStatus
FROM SessionEvents
GROUP BY SessionId, UserId, Role
ORDER BY LoginTime DESC;
PRINT '';

-- Query 6: Sessions with manual logout
PRINT '6. Sessions Ending with Manual Logout:';
PRINT '---------------------------------------------------';
SELECT 
    SessionId,
    UserId,
    COUNT(*) AS EventCount,
    STRING_AGG(EventType, ' ? ') WITHIN GROUP (ORDER BY TimestampUtc) AS EventSequence,
    MIN(TimestampUtc) AS SessionStart,
    MAX(TimestampUtc) AS SessionEnd,
    DATEDIFF(MINUTE, MIN(TimestampUtc), MAX(TimestampUtc)) AS DurationMinutes
FROM AuditLogs
WHERE SessionId IN (
    SELECT SessionId 
    FROM AuditLogs 
    WHERE EventType = 'Logout'
)
GROUP BY SessionId, UserId
ORDER BY SessionEnd DESC;
PRINT '';

-- ============================================
-- SECTION 4: DATA INTEGRITY VERIFICATION
-- ============================================

PRINT '========================================';
PRINT 'SECTION 4: DATA INTEGRITY VERIFICATION';
PRINT '========================================';
PRINT '';

-- Query 7: Check for duplicate events (should be empty)
PRINT '7. Check for Duplicate Events (SHOULD BE EMPTY):';
PRINT '---------------------------------------------------';
SELECT 
    SessionId,
    EventType,
    COUNT(*) AS DuplicateCount,
    STRING_AGG(CAST(Id AS NVARCHAR(36)), ', ') AS DuplicateIds
FROM AuditLogs
WHERE SessionId IS NOT NULL
GROUP BY SessionId, EventType
HAVING COUNT(*) > 1;

IF @@ROWCOUNT = 0
BEGIN
    PRINT '? PASS: No duplicate events found (append-only design verified)';
END
ELSE
BEGIN
    PRINT '? FAIL: Duplicate events detected!';
END
PRINT '';

-- Query 8: Check for NULL required fields
PRINT '8. Check for NULL Required Fields:';
PRINT '---------------------------------------------------';
SELECT 
    EventType,
    COUNT(*) AS RecordsWithNullUserId
FROM AuditLogs
WHERE UserId IS NULL
GROUP BY EventType;

IF @@ROWCOUNT = 0
BEGIN
    PRINT '? PASS: All records have UserId populated';
END
ELSE
BEGIN
    PRINT '? FAIL: Some records have NULL UserId!';
END
PRINT '';

-- Query 9: Check for future timestamps (should be empty)
PRINT '9. Check for Future Timestamps (SHOULD BE EMPTY):';
PRINT '---------------------------------------------------';
SELECT 
    Id,
    EventType,
    TimestampUtc,
    GETUTCDATE() AS CurrentUtc,
    DATEDIFF(SECOND, GETUTCDATE(), TimestampUtc) AS SecondsFuture
FROM AuditLogs
WHERE TimestampUtc > GETUTCDATE();

IF @@ROWCOUNT = 0
BEGIN
    PRINT '? PASS: No future timestamps detected';
END
ELSE
BEGIN
    PRINT '? FAIL: Some records have future timestamps!';
END
PRINT '';

-- ============================================
-- SECTION 5: PATTERN ANALYSIS
-- ============================================

PRINT '========================================';
PRINT 'SECTION 5: PATTERN ANALYSIS';
PRINT '========================================';
PRINT '';

-- Query 10: Logout patterns by hour
PRINT '10. Logout Activity by Hour (Last 24h):';
PRINT '---------------------------------------------------';
SELECT 
    DATEPART(HOUR, TimestampUtc) AS HourOfDay,
    COUNT(*) AS LogoutCount
FROM AuditLogs
WHERE EventType = 'Logout'
  AND TimestampUtc >= DATEADD(HOUR, -24, GETUTCDATE())
GROUP BY DATEPART(HOUR, TimestampUtc)
ORDER BY HourOfDay;
PRINT '';

-- Query 11: Average session duration
PRINT '11. Average Session Duration:';
PRINT '---------------------------------------------------';
WITH SessionDurations AS (
    SELECT 
        SessionId,
        DATEDIFF(SECOND, 
            MIN(CASE WHEN EventType = 'Login' THEN TimestampUtc END),
            MAX(CASE WHEN EventType IN ('Logout', 'SessionExpired') THEN TimestampUtc END)
        ) AS DurationSeconds
    FROM AuditLogs
    WHERE SessionId IS NOT NULL
    GROUP BY SessionId
    HAVING MIN(CASE WHEN EventType = 'Login' THEN TimestampUtc END) IS NOT NULL
       AND MAX(CASE WHEN EventType IN ('Logout', 'SessionExpired') THEN TimestampUtc END) IS NOT NULL
)
SELECT 
    COUNT(*) AS CompletedSessions,
    AVG(DurationSeconds) AS AvgDurationSeconds,
    AVG(DurationSeconds) / 60.0 AS AvgDurationMinutes,
    MIN(DurationSeconds) AS MinDurationSeconds,
    MAX(DurationSeconds) AS MaxDurationSeconds
FROM SessionDurations;
PRINT '';

-- Query 12: User logout frequency
PRINT '12. User Logout Frequency:';
PRINT '---------------------------------------------------';
SELECT TOP 10
    UserId,
    Role,
    COUNT(*) AS LogoutCount,
    MIN(TimestampUtc) AS FirstLogout,
    MAX(TimestampUtc) AS LastLogout,
    DATEDIFF(DAY, MIN(TimestampUtc), MAX(TimestampUtc)) + 1 AS DaysActive
FROM AuditLogs
WHERE EventType = 'Logout'
GROUP BY UserId, Role
ORDER BY LogoutCount DESC;
PRINT '';

-- ============================================
-- SECTION 6: SPECIFIC TEST VERIFICATION
-- ============================================

PRINT '========================================';
PRINT 'SECTION 6: SPECIFIC TEST VERIFICATION';
PRINT '========================================';
PRINT '';

-- Query 13: Most recent logout (THE CRITICAL TEST)
PRINT '13. Most Recent Logout (Expected after clicking "Sign Out"):';
PRINT '---------------------------------------------------';
SELECT TOP 1
    Id,
    EventType,
    UserId,
    Role,
    SessionId,
    TimestampUtc,
    DATEDIFF(SECOND, TimestampUtc, GETUTCDATE()) AS SecondsAgo,
    IpAddress,
    LEFT(UserAgent, 50) AS UserAgent_Truncated
FROM AuditLogs
WHERE EventType = 'Logout'
ORDER BY TimestampUtc DESC;

DECLARE @LastLogoutSeconds INT;
SELECT TOP 1 @LastLogoutSeconds = DATEDIFF(SECOND, TimestampUtc, GETUTCDATE())
FROM AuditLogs
WHERE EventType = 'Logout'
ORDER BY TimestampUtc DESC;

IF @LastLogoutSeconds IS NOT NULL
BEGIN
    IF @LastLogoutSeconds < 60
        PRINT '? PASS: Recent logout detected within last minute';
    ELSE IF @LastLogoutSeconds < 300
        PRINT '? WARNING: Last logout was ' + CAST(@LastLogoutSeconds AS VARCHAR) + ' seconds ago';
    ELSE
        PRINT '? INFO: Last logout was more than 5 minutes ago';
END
ELSE
BEGIN
    PRINT '? FAIL: No logout records found!';
END
PRINT '';

-- ============================================
-- SECTION 7: SUMMARY REPORT
-- ============================================

PRINT '========================================';
PRINT 'SECTION 7: SUMMARY REPORT';
PRINT '========================================';
PRINT '';

PRINT 'Overall Statistics:';
PRINT '---------------------------------------------------';
SELECT 
    (SELECT COUNT(*) FROM AuditLogs) AS TotalAuditRecords,
    (SELECT COUNT(*) FROM AuditLogs WHERE EventType = 'Login') AS TotalLogins,
    (SELECT COUNT(*) FROM AuditLogs WHERE EventType = 'Logout') AS TotalLogouts,
    (SELECT COUNT(*) FROM AuditLogs WHERE EventType = 'SessionExpired') AS TotalSessionExpired,
    (SELECT COUNT(DISTINCT UserId) FROM AuditLogs) AS UniqueUsers,
    (SELECT COUNT(DISTINCT SessionId) FROM AuditLogs WHERE SessionId IS NOT NULL) AS UniqueSessions,
    (SELECT MIN(TimestampUtc) FROM AuditLogs) AS FirstAuditRecord,
    (SELECT MAX(TimestampUtc) FROM AuditLogs) AS LatestAuditRecord;
PRINT '';

-- ============================================
-- SECTION 8: EXPECTED VS ACTUAL
-- ============================================

PRINT '========================================';
PRINT 'SECTION 8: EXPECTED VS ACTUAL';
PRINT '========================================';
PRINT '';

PRINT 'Expected Result After Logout:';
PRINT '  - Latest row has EventType = ''Logout''';
PRINT '  - TimestampUtc is recent (within last minute)';
PRINT '  - UserId, SessionId, Role are populated';
PRINT '  - IpAddress and UserAgent are captured';
PRINT '  - New row created (not updated)';
PRINT '';

DECLARE @LatestEventType NVARCHAR(50);
DECLARE @LatestTimestamp DATETIME2;

SELECT TOP 1 
    @LatestEventType = EventType,
    @LatestTimestamp = TimestampUtc
FROM AuditLogs
ORDER BY TimestampUtc DESC;

PRINT 'Actual Result:';
PRINT '  Latest EventType: ' + ISNULL(@LatestEventType, 'NULL');
PRINT '  Latest Timestamp: ' + ISNULL(CONVERT(VARCHAR, @LatestTimestamp, 120), 'NULL') + ' UTC';
PRINT '  Seconds Ago: ' + CAST(DATEDIFF(SECOND, @LatestTimestamp, GETUTCDATE()) AS VARCHAR);
PRINT '';

-- Final verification
IF @LatestEventType = 'Logout' AND DATEDIFF(SECOND, @LatestTimestamp, GETUTCDATE()) < 60
BEGIN
    PRINT '??? SUCCESS: Logout audit record created successfully! ???';
END
ELSE IF @LatestEventType = 'Logout' AND DATEDIFF(SECOND, @LatestTimestamp, GETUTCDATE()) < 300
BEGIN
    PRINT '? PARTIAL: Logout record exists but is not recent';
END
ELSE IF @LatestEventType != 'Logout'
BEGIN
    PRINT '? FAIL: Latest record is not a Logout event';
END
ELSE
BEGIN
    PRINT '? FAIL: No recent logout record found';
END

PRINT '';
PRINT '========================================';
PRINT 'VERIFICATION COMPLETE';
PRINT '========================================';
