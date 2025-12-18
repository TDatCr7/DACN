# CinemaS

CinemaS is an ASP.NET Core 9.0 MVC web application for cinema ticketing, snack ordering, and membership management. It uses ASP.NET Identity for authentication, integrates VNPAY for payments, and ships with an admin portal for managing movies, showtimes, and promotions.

## Features

- **Movie discovery & showtimes**: Manage movies, genres, participants, showtimes, cinemas, and seat maps.
- **Ticketing flow**: Reserve seats, choose ticket types, add snacks, and generate invoices with transaction tracking.
- **Payments**: Built-in VNPAY integration with configurable gateway settings.
- **Accounts & roles**: ASP.NET Identity with role-based access (Admin/User) and email confirmation support.
- **Loyalty**: Membership ranks, points, and promotions with customizable seat and ticket types.
- **Admin portal**: CRUD management for cinema data, pricing, permissions, and statuses.

## Project structure

- `Controllers/` – MVC controllers for booking, payments, admin management, and Identity endpoints.
- `Models/` – Entity models, payment/email settings, and identity models.
- `Services/` – Application services including the Gmail-based email sender.
- `VNPAY/` – VNPAY helper utilities and configuration bindings.
- `Views/` & `wwwroot/` – Razor views, static assets, and UI resources.
- `Migrations/` – Entity Framework Core migrations for the SQL Server database.

## Prerequisites

- .NET SDK 9.0 or later
- SQL Server instance (local or remote)
- A configured SMTP account (Gmail by default) for confirmation emails

## Configuration

1. Copy `CinemaS/appsettings.json` to `CinemaS/appsettings.Development.json` (or set environment variables) to avoid editing the committed file directly.
2. Update the configuration values:
   - `ConnectionStrings:CinemaS` – point to your SQL Server database.
   - `EmailSettings` – supply SMTP server, port, sender email, and app password.
   - `VnPay` – set your merchant code (`TmnCode`), `HashSecret`, and gateway URLs.
3. Optional: use [Secret Manager](https://learn.microsoft.com/aspnet/core/security/app-secrets) to store sensitive values during development.

## Database setup

1. Ensure the EF Core CLI is available:
   ```bash
   dotnet tool install --global dotnet-ef  # if not already installed
   ```
2. Apply migrations from the `CinemaS` project directory:
   ```bash
   dotnet ef database update
   ```
3. On first run, the app seeds default data:
   - Roles `Admin` and `User`.
   - Admin account `admin@cinemas.local` with password `Admin@123`.
   - Basic membership rank and seat types (Normal, VIP, Couple).

## Run the application

From the `CinemaS` directory:

```bash
dotnet restore
dotnet run
```

The app listens on the configured ASP.NET URLs (HTTPS by default). Access the site and log in with the seeded admin account or register a new user (email confirmation required).

## Development tips

- Identity UI endpoints use default ASP.NET Identity pages for account management and email confirmation.
- Adjust authentication paths in `Program.cs` if hosting behind a reverse proxy.
- Static assets are mapped via `app.MapStaticAssets()`; ensure file paths under `wwwroot` remain consistent when adding images.

