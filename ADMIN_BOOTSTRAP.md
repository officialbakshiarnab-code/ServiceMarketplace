# Admin Bootstrap

ServiceMarketplace admin credentials use the same identity tables as normal users:

- `Users` stores the account and password hash.
- `Roles` stores the `Admin` role.
- `UserRoles` grants the admin role to a user.
- JWTs emit `administrative_permission = platform.admin` for admin API access.

There is no `AdminLogins` table and public registration cannot create admin users.

## Local/operator bootstrap

Use the PowerShell wrapper from a trusted local terminal:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\bootstrap-admin.ps1 `
  -Email "admin@example.local" `
  -FirstName "Admin" `
  -LastName "Operator" `
  -Phone "9999999999" `
  -Reason "Local pilot bootstrap"
```

The script reads the connection string from one of these sources:

- `-ConnectionString`
- `SERVICE_MARKETPLACE_CONNECTION_STRING`
- `ConnectionStrings__DefaultConnection`
- API user-secrets `ConnectionStrings:DefaultConnection`

The admin password is read from `SERVICE_MARKETPLACE_ADMIN_PASSWORD` or from a secure prompt.
Do not put passwords or real connection strings in git.

To intentionally reset an existing admin password:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\bootstrap-admin.ps1 `
  -Email "admin@example.local" `
  -FirstName "Admin" `
  -LastName "Operator" `
  -Phone "9999999999" `
  -Reason "Operator-approved password reset" `
  -ResetPassword
```

After bootstrap, sign in at `/admin`.
