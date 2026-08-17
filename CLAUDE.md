# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Ferrealiados Cotizaciones: internal web app for FERREALIADOS JV (hardware-store business) that catalogs products, records what each provider quoted for them over time, compares providers by price, and flags quotes that have gone stale (past a configurable number of months) so they get re-quoted. Building/sending the final client-facing quotation (with margin) is explicitly out of scope for now.

This codebase started as a clone of a sibling system for a related company, **Jimaco Cotizaciones** (same group, different hardware business) — same architecture, same conventions, fully independent database and deployment. Do not assume any data or infrastructure is shared between the two beyond the Lightsail host they happen to run on.

## Repo layout (two repos, one system)

This is the **backend** repo (`FerrealiadosCotizacionesBackend`). The Angular frontend lives in a separate sibling repo, **`FerrealiadosCotizacionesFrontend`**, checked out as a sibling folder (`../Ferrealiados.Cotizaciones.Web` relative to this repo) — `docker-compose.yml` here builds it from that relative path, so the two repos must be cloned next to each other for `docker compose up` to work.

## Architecture

**Solution layout** (`Ferrealiados.Cotizaciones.slnx`) — simple N-tier, not Clean Architecture:

- `Ferrealiados.Cotizaciones.Modelo` — EF Core entities, `AppDbContext`, migrations (`Migraciones/`).
- `Ferrealiados.Cotizaciones.Negocio` — business logic. `Servicios/` has the real implementations, `Interfaces/` the contracts, `DTOs/` the API-facing shapes. `ServiceCollectionExtensions.AddNegocio()` wires DI.
- `Ferrealiados.Cotizaciones.Api` — ASP.NET Core Web API. Controllers only call into Negocio services; no business logic here.
- `Ferrealiados.Cotizaciones.MigracionExcel` — console tool that imports supplier/pricing Excel files into the database. Not part of the running app. Supports **three file formats, auto-detected** (checked in `Program.cs` before dispatching, in this order): `historial de precios` sheet (`MigradorHistorialPrecios.cs` — one row per real purchase: Codigo/Producto/FechaDeCompra/NitProvee/Proveedor/Costo; idempotent on Producto+Proveedor+Fecha+Costo since the same day can have multiple distinct purchases at different prices); `Proveedores`+`Cotizaciones` sheets (`MigradorProveedorCotizacion.cs` — loads every valid Cotizaciones row as its own price entry); and a single-sheet historical fallback (`MigradorHistorico.cs`, collapses to one latest price per Producto+Proveedor pair). All three are idempotent (safe to re-run) and upsert Productos/Proveedores by key rather than blind-inserting.
- `Ferrealiados.Cotizaciones.TestUnitarios` — xUnit + Moq + EF Core InMemory.

The Angular frontend (`FerrealiadosCotizacionesFrontend` repo) is standalone components, no NgModules — see that repo's own `CLAUDE.md` for frontend-specific detail, including the brand color palette (`#f38138` naranja, `#0a578d` celeste vivido, `#043760` celeste oscuro).

**Core domain model:** `Producto` and `Proveedor` are joined by `ProductoProveedorPrecio`, which is **append-only** — every time a provider re-quotes a product, a new row is inserted with a new `FechaCotizacion`; existing rows are never overwritten. The "current" price for a (Producto, Proveedor) pair is always the row with the latest `FechaCotizacion`.

**CostoBase vs Costo — do not confuse these.** `CostoBase` is the real/normal cost and is what **all business logic** uses: "mejor precio" comparisons (`PrecioService.ObtenerPreciosPorProductoAsync`, ordered by `CostoBase`) and stale-price alerts (`ObtenerAlertasVencidasAsync`, also `CostoBase`). `Costo` is `CostoBase` adjusted by an optional `PorcentajeAjuste` (-100 to 100, whole numbers, applied via `AjustePrecio.CalcularCostoFinal`) entered at the point of registering a price — it is **purely informational/display**, never used for comparisons. Any code that inserts a `ProductoProveedorPrecio` directly (migration tools, scripts) must explicitly set `CostoBase` — it defaults to 0 and silently breaks both the "mejor precio" badge and the UI's "Costo" column if left unset.

**Vigencia (staleness) rule** lives in `Ferrealiados.Cotizaciones.Negocio/Servicios/VigenciaPrecio.cs` as pure static functions (`EsVencido`, `DiasDesde`) taking `hoy` as an explicit parameter. `PrecioService` injects `TimeProvider` (not `DateTime.Now`) so tests can supply a fixed clock. The configurable threshold (default 3 months) is stored in the `ConfiguracionSistema` key-value table, read via `IConfiguracionService`.

**Pagination:** all list endpoints (`/api/productos`, `/api/proveedores`, `/api/alertas/precios-vencidos`) return `PaginaResultado<T>` — `{ items, total, pagina, tamanoPagina }`, default page size 10, capped at 100 server-side. `ProveedorService.ListarActivosAsync()` and `UsuarioService.ListarAsync()` are intentionally unpaged.

**Auth & roles:** JWT via standard `AddAuthentication().AddJwtBearer()` + `[Authorize]` / `[Authorize(Roles = "Admin")]`. Two roles: `Admin`, `Cotizador` (`RolUsuario` enum — persisted as `int`, **serialized as a JSON string** via a global `JsonStringEnumConverter` in `Program.cs`; without it the frontend receives raw numbers and role comparisons silently break). Token lifetime is `Jwt:ExpiracionMinutos` (default 480 = 8h). On first run with an empty `Usuarios` table, `Program.cs` seeds `admin@ferrealiados.local` / `Admin123!` as Admin — **change this before any real deployment.**

**Proveedor fields:** `Nombre` (unique, case-insensitive collation) is the real identity key for matching/dedup; `Nit` is the business-facing identifier. Most proveedores seeded from the initial import do **not** have a NIT (the source data didn't include it for most rows) — that's expected and fine, fill in via the UI as it becomes known.

**Docker Compose topology** (`docker-compose.yml`): `db` (SQL Server 2022) → `api` (applies EF migrations + seeds admin on startup) → `web` (built from `../Ferrealiados.Cotizaciones.Web`, nginx reverse-proxies `/api/*` to `api` so the SPA and API are same-origin). `migracion` is a `profiles: ["tools"]` service — run it explicitly (see below). Local dev ports are deliberately different from Jimaco's (which may run at the same time on this machine): web `4300`, api `8090`, db `127.0.0.1:1434`.

## Commands

### Backend (.NET 10)
```bash
dotnet build
dotnet test Ferrealiados.Cotizaciones.TestUnitarios
dotnet ef migrations add <Nombre> --project Ferrealiados.Cotizaciones.Modelo --startup-project Ferrealiados.Cotizaciones.Api --output-dir Migraciones
```

### Docker (full stack, local — requires the frontend repo checked out as `../Ferrealiados.Cotizaciones.Web`)
```bash
cp .env.example .env                          # first time only, then fill in real values
docker compose up -d                          # db + api + web
docker compose build api web                  # rebuild after backend/frontend changes
docker compose logs api --tail 50             # api applies migrations + seeds admin on boot; check here first

# Excel migration — file lives under .env's EXCEL_SOURCE_DIR, mounted at /data. Format is auto-detected.
docker compose --profile tools build migracion
docker compose --profile tools run --rm migracion "/data/COTIZACIONES REPORTE MARZO.xlsx"
```
Running from Git Bash on Windows: prefix with `MSYS_NO_PATHCONV=1` or the leading `/data/...` arg gets mangled into a bogus Windows path before Docker ever sees it.

## Production deployment

Runs on the **same AWS Lightsail instance as Jimaco** (`54.232.227.230`, São Paulo, 2 vCPU/3.7GB — see Jimaco's own repo/memory for SSH access), as an independent stack under `/opt/ferrealiados-cotizaciones/`, with its own database (`ferrealiados-db`) and no shared data with Jimaco.

**Public URL**: `https://ferrealiados.54-232-227-230.sslip.io`.

**No Caddy of its own.** Jimaco already runs a Caddy container (`jimaco-caddy`) bound to host ports 80/443 — a second Caddy on the same host would collide on those ports. Instead, this stack's `docker-compose.prod.yml` joins the **external** Docker network `jimacocotizaciones_default` (the one Jimaco's compose creates) so `jimaco-caddy` can reach `ferrealiados-web` by container name, and a **separate site block** was added to Jimaco's `Caddyfile` (`/opt/jimaco/Jimaco.Cotizaciones/Caddyfile`) routing the new subdomain there — Jimaco's own block was not touched. No service in this stack exposes ports to the host in production.

**Never build with `--build` directly on the server.** This is a burstable 2 vCPU instance shared with Jimaco (and other unrelated containers) already in production. A sibling project on this same server (`ProspeccionConstructoras`) hit this twice: building on the server exhausted CPU credits and made the instance unresponsive over SSH, requiring a manual reboot. Always build locally and load the image instead:
```bash
# Local: build, then export
docker compose -f docker-compose.yml -f docker-compose.prod.yml build api web
docker save ferrealiadoscotizaciones-api:latest ferrealiadoscotizaciones-web:latest | gzip > /tmp/ferrealiados-images.tar.gz

# Upload + load on server + recreate (no --build)
scp -i <key> /tmp/ferrealiados-images.tar.gz ubuntu@54.232.227.230:/tmp/
ssh -i <key> ubuntu@54.232.227.230 "gunzip -c /tmp/ferrealiados-images.tar.gz | docker load && cd /opt/ferrealiados-cotizaciones && docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d && rm -f /tmp/ferrealiados-images.tar.gz"
```

Production secrets live in `/opt/ferrealiados-cotizaciones/.env` on the server (never committed), generated with `openssl rand`, independent from Jimaco's. **The seeded `admin@ferrealiados.local` / `Admin123!` account must have its password changed via the Usuarios screen before real use.**

**No automated DB backups yet.** Take a manual `BACKUP DATABASE` (same procedure as Jimaco) before any risky migration or bulk import against production.

## Non-obvious gotchas (inherited from the Jimaco codebase this was cloned from)

- **Console apps don't get environment-variable config for free.** `WebApplication.CreateBuilder()` (used in `Api`) wires `AddEnvironmentVariables()` automatically; a bare `ConfigurationBuilder` (used in `MigracionExcel`) does not — it must be added explicitly or Docker env vars get silently ignored in favor of the checked-in `appsettings.json`.
- **Enums need `JsonStringEnumConverter` explicitly.** Without it, `RolUsuario` serializes as a raw int and deserializes strictly as one too. If you add another enum to a DTO, this converter already covers it.
- **SQL Server's default collation is case-insensitive but doesn't collapse other formatting** — always uppercase/trim product codes before comparing/grouping, don't assume the DB will normalize for you.
- **Swashbuckle is pinned to 7.2.0**, not latest — 10.x reworks the OpenAPI security-scheme API. Don't bump without rewriting the Swagger setup in `Api/Program.cs`.
- **Free-text Excel columns will eventually exceed whatever `MaxLength` you picked.** Importers truncate defensively (`Truncar` helper) — do the same for any new free-text field sourced from Excel.
- **Source Excel data may be double-UTF-8-encoded** (e.g. "uña" stored as "uÃ±a"). `MigracionExcel/NormalizadorTexto.cs` repairs this reversibly — check there before assuming mojibake is a display bug.
