# Local Testing

## Demo Data

Run the fake Kolkata demo seed from the repo root:

```powershell
.\scripts\seed-demo-data.ps1
```

Or pass a local connection string explicitly:

```powershell
.\scripts\seed-demo-data.ps1 -ConnectionString "Host=localhost;Port=5432;Database=service_marketplace;Username=servicemarketplace;Password=<local-password>"
```

The script resets only these known fake accounts and records connected to them:

- `user1@test.com`
- `user2@test.com`
- `provider1@test.com`
- `provider2@test.com`
- `both1@test.com`
- `seller1@test.com`
- `admin@test.com`

All demo accounts use password `Demo@12345`.

The seed covers users, roles, provider/seller profiles, service catalog, service requests, bids, service packages, orders, messages, payments, payouts, disputes, reviews, products, product delivery orders, notifications, contact requests, and login audit examples.
