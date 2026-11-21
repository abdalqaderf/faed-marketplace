# Faed (فائض)

Faed is a marketplace for open-box and ex-display home appliances and power tools in Amman, Jordan. Buyers can see each item's condition and reason for discount, reserve it online, and pay the merchant in cash at pickup.

## What you can do

| Role | Workflow |
| --- | --- |
| Buyer | Browse listings, inspect condition details and photos, choose a pickup location, and track reservations. |
| Merchant | Submit verification documents, choose a subscription plan, manage listings, and handle pickup orders. |
| Admin | Review merchant verification requests, moderate listings, and oversee subscriptions. |

Before submitting a listing for moderation, a merchant must be verified, have an active subscription, and have room within the plan's listing quota. Reservations set aside stock and release it when they expire.

## Technology

- **Application** — ASP.NET Core MVC and Razor on .NET 10
- **Data and authentication** — Entity Framework Core, SQL Server, and ASP.NET Core Identity
- **Frontend** — Bootstrap 5 and vanilla JavaScript
- **Integrations** — Cloudflare R2 for file storage and Brevo for transactional email

The [web application](src/Faed.Web) contains the public marketplace and separate Buyer, Merchant, and Admin areas. The [test project](tests/Faed.Web.Tests) covers workflows such as listing eligibility, reservation expiry, and merchant response tracking.

## Run locally

The default development setup uses Windows, Git, the .NET 10 SDK, and SQL Server Express LocalDB. Run these commands in PowerShell.

```powershell
git clone --branch faed-core https://github.com/abdalqaderf/faed-marketplace.git
Set-Location faed-marketplace/src/Faed.Web
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet tool restore
dotnet ef database update
dotnet dev-certs https --trust
```

### Optional demo data

From `src/Faed.Web`, enable the demo seed and replace `<your-demo-password>` with a local password that meets the application's password requirements.

```powershell
dotnet user-secrets set "Faed:DemoSeed:Enabled" "true"
dotnet user-secrets set "Faed:DemoSeed:Password" "<your-demo-password>"
```

The development seed creates sample listings, orders, and accounts. Sign in with the password you configured.

| Role | Demo email |
| --- | --- |
| Buyer | `buyer-a@faed.local` |
| Verified merchant | `merchant-a@faed.local` |
| Admin | `demo-admin@faed.local` |

### Start the application

```powershell
dotnet run --launch-profile https
```

Open [https://localhost:7241](https://localhost:7241).

## Build and test

From the repository root, run these commands.

```powershell
dotnet build Faed.slnx --configuration Release
dotnet test Faed.slnx --configuration Release
```

## Demo image credits

See the [demo image credits](https://github.com/abdalqaderf/faed-marketplace/blob/aecd4c147854000d2b5366ae980ba258a3864972/docs/CREDITS.md) for photo sources, creators, and licenses.