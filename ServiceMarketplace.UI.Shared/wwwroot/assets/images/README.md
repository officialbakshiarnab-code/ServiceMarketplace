# Image Asset Folders

Use section-based folders under `assets/images`, for example:

- `landing/` for public landing-page visuals.
- `dashboard/` for dashboard-only visuals.
- `profiles/` for profile-directory visuals.
- `marketplace/` for service/product marketplace visuals.

Generated or machine-local image files should use `.local` in the filename and remain ignored by git, for example:

`landing/hero-service-marketplace.local.png`

Only commit intentional, reusable, license-safe assets after renaming them to a non-local filename.
