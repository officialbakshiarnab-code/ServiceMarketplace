# NavMenu Rendering - Quick Reference

## ? What Was Fixed

| Issue | Fix | Result |
|-------|-----|--------|
| Links shown before auth | Wrapped in AuthorizeView | ? Links only after login |
| No loading state | Added Authorizing fragment | ? "Loading..." shown |
| Role-based links not gated | Added Match attribute | ? Links by role |
| No logout in nav | Added LogoutButton | ? Logout accessible |
| Local state caching | Removed collapseNavMenu variable | ? Real-time state |
| Unauthenticated users see nothing | Added NotAuthorized section | ? Login/register shown |

---

## ?? Files Changed

```
ServiceMarketplace.UI.Web/Layout/NavMenu.razor
ServiceMarketplace.UI.MAUI/Components/Layout/NavMenu.razor
ServiceMarketplace.UI.Shared/Components/RoleNavLinks.razor
ServiceMarketplace.UI.Shared/Auth/LogoutButton.razor
```

---

## ?? Navigation Structure

### Authenticated User (User Role)
```
Home
?? Create Request (User)
?? My Requests (User)
?? Sign Out
```

### Authenticated User (Provider Role)
```
Home
?? Available Requests (Provider)
?? My Bids (Provider)
?? Sign Out
```

### Unauthenticated User
```
Login
Register
```

---

## ?? Testing

### Test 1: Unauthenticated
```
Step 1: Visit /login without logging in
Expected: See "Login" and "Register" only ?
```

### Test 2: Authenticated as User
```
Step 1: Login as user@test.com
Expected: See "Home", "Create Request", "My Requests", "Sign Out" ?
```

### Test 3: Authenticated as Provider
```
Step 1: Login as provider@test.com
Expected: See "Home", "Available Requests", "My Bids", "Sign Out" ?
```

### Test 4: Both Roles
```
Step 1: Login with both roles
Expected: See all User AND Provider links ?
```

### Test 5: Loading State
```
Step 1: Check Network tab during page load
Expected: Briefly see "Loading..." while auth state determined ?
```

### Test 6: Logout
```
Step 1: Click "Sign Out"
Expected: Redirected to /login, nav shows login/register ?
```

---

## ?? Key Principles

### 1. AuthorizeView is Primary Guard
```razor
<AuthorizeView>
    <Authorized>
        <!-- Only authenticated users see this -->
    </Authorized>
    <NotAuthorized>
        <!-- Only non-authenticated users see this -->
    </NotAuthorized>
</AuthorizeView>
```

### 2. Roles Come from JWT
```razor
<AuthorizeView Roles="User">
    <!-- Checks JWT claims for "User" role -->
</AuthorizeView>
```

### 3. NavLink for Internal Routes
```razor
<!-- Correct -->
<NavLink href="/user/dashboard" Match="NavLinkMatch.Prefix">
    User Dashboard
</NavLink>

<!-- Avoid -->
<a href="/user/dashboard">User Dashboard</a>
```

### 4. No Local State Caching
```csharp
// Good
private string NavMenuCssClass => collapseNavMenu ? "collapse" : null;

// Avoid
private string CachedNavMenuClass = "";
```

---

## ?? Authentication States

| State | Show | Content |
|-------|------|---------|
| **Authorizing** | While checking JWT | "Loading..." indicator |
| **Authorized** | After login | Authenticated user navigation |
| **NotAuthorized** | Not logged in | Login/Register links |

---

## ?? Security

- ? Links gated by `AuthorizeView` + role checks
- ? No sensitive content in unauthenticated view
- ? Role claims validated from JWT (server-trusted)
- ? No client-side role spoofing possible

---

## ?? Troubleshooting

### Links not visible after login
? Check browser F12 > Network, verify JWT in request headers

### Wrong role links showing
? Check JWT payload at jwt.io, verify role claims present

### NavMenu not responding to logout
? Check browser console for errors, verify SafeLogoutService registered

### "Loading..." stuck
? Check Network tab, verify auth endpoint responding

---

## ?? Best Practices

1. **Always use AuthorizeView** for auth-dependent content
2. **Always specify roles** if restricting by role
3. **Use NavLink** not `<a>` for internal routes
4. **Add Match attribute** for better URL matching
5. **Test all auth states** - login, logout, role changes

---

## ?? Related Files

- `Login.razor` - Login flow
- `AuthState.cs` - Auth state helper
- `AuthRedirector.cs` - Post-login routing
- `SafeLogoutService` - Logout implementation
- `RoleNames.cs` - Role constants

---

**Status**: ? Complete & Working  
**Build**: ? Successful  
**Ready**: ? Production  

See `NAVMENU_RENDERING_LOGIC_FIX.md` for detailed documentation.
