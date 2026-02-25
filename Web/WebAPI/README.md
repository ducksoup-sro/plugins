# WebApiPlugin

Plugin for DuckSoup that provides REST routes for a web dashboard and API.

## Features

- **Services:** List, add, edit (PATCH), remove, start, stop (with validation)
- **Sessions:** List (filter by ServerType), session details, disconnect, read packets per session
- **Packets:** Send (typed or rawHex), array types in data format
- **Machines:** CRUD (create, edit, delete)
- **Audit log:** Retrieve recent actions
- **Roles:** Service/machine mutations require **Admin** role
- **Rate limiting:** 120 requests/minute per user or IP
- **System metrics:** CPU (total + per-core) and memory (proxy process and host)
- **CORS:** List allowed origins, add (Admin), remove (Admin). **CORS is stored in the core database** (API.Database, table `CorsOrigin` in ProxyDb); the plugin loads them on startup and reads/writes via the core context.
- **Audit log:** Stored in the **plugin** database (table `AuditEntry` in ProxyDb). Migrated on plugin load.
- **Events:** List loaded/available events, load, unload, get/set event state (Starting, Running, Ending)

All routes under `/api/v1/web/` require authentication (Bearer token after login via `/api/v1/auth/login`).

## Database

- **CORS** is a **core** feature: table `CorsOrigin` lives in the main DuckSoup context (API.Database.Context.DuckSoup). The core app seeds default origins on first migrate; the plugin loads them into the webserver on enable and uses the core context for add/remove via the API.
- **Audit log** is a **plugin** feature: table `AuditEntry` is created by the plugin’s own EF context (WebApiPlugin.Database.WebApiContext). Connection string is taken from `ISettingsManager.Settings` (ProxyDb). To add or change plugin migrations, use the project’s EF tutorial: run `dotnet ef migrations add ... --project WebApiPlugin --context WebApiContext --output-dir Database/Migrations`, then load the plugin so migrations run on startup.

## Hosting the frontend

The React dashboard is built separately (see `frontend/` in the repository). You can:

1. Run it in development: `npm run dev` in `frontend/` and use the configured API URL (e.g. `http://localhost:9000`).
2. Build and host the `frontend/dist/` output on any static host (e.g. Apache, nginx). Configure the API URL in the dashboard (stored in `localStorage`).

The plugin adds CORS origins for `http://localhost`, `http://127.0.0.1`, and common dev ports (3000, 5173) so the dashboard can call the API from another origin.

## Plugin folder structure

Each plugin lives in `plugins/<PluginName>/` with:

- **plugin.json**: `MainLibrary` (DLL name), `AutoStart` (bool).
- **&lt;MainLibrary&gt;.dll** and any dependencies.

The frontend is built and hosted separately (see Hosting the frontend).

## Installation

1. Build: `dotnet build plugins/WebApiPlugin/WebApiPlugin.csproj`
2. Copy the entire build output of WebApiPlugin (e.g. `plugins/WebApiPlugin/bin/Release/net10.0/`) into a plugin folder under the host (e.g. `plugins/ExampleWebPlugin/`). Required:
   - `plugin.json`
   - `WebApiPlugin.dll`
   - All DLLs from the build output, and **the entire `runtimes` folder**. On Windows, the Performance Counter implementation is in `runtimes/win/lib/net8.0/System.Diagnostics.PerformanceCounter.dll` — copying only the root-level DLLs is not enough; you must copy the full output including `runtimes/`.

The project has `CopyLocalLockFileAssemblies` set to `true`, so a normal build copies all referenced assemblies into the output directory. **Deploy the entire build output (including the `runtimes` folder) as the plugin folder.**

## API overview

| Method | Path | Description |
|--------|------|--------------|
| GET | `/api/v1/web/services` | List all services (with run status) |
| POST | `/api/v1/web/services` | Add service (Admin, body: AddServiceRequest) |
| PATCH | `/api/v1/web/services/{name}` | Update service (Admin, optional restart) |
| DELETE | `/api/v1/web/services/{name}` | Stop and remove service (Admin) |
| POST | `/api/v1/web/services/{name}/start` | Start service (Admin) |
| POST | `/api/v1/web/services/{name}/stop` | Stop service (Admin) |
| GET | `/api/v1/web/sessions?serverType=` | List sessions (filter: DownloadServer, GatewayServer, AgentServer) |
| GET | `/api/v1/web/sessions/{guid}` | Session details (CharInfo, gameReady, Region) |
| GET | `/api/v1/web/sessions/{guid}/data` | Session data (CharInfo, DataKeys, etc.) |
| POST | `/api/v1/web/sessions/{guid}/disconnect` | Disconnect session |
| GET | `/api/v1/web/sessions/{guid}/packets?limit=N&since=ISO8601` | Last N packets (optional since for polling) |
| POST | `/api/v1/web/sessions/packet` | Send packet (body: SendPacketRequest) |
| GET | `/api/v1/web/machines` | List all machines |
| POST | `/api/v1/web/machines` | Add machine (Admin) |
| PATCH | `/api/v1/web/machines/{id}` | Update machine (Admin) |
| DELETE | `/api/v1/web/machines/{id}` | Delete machine (Admin) |
| GET | `/api/v1/web/audit?limit=N` | Audit log (recent actions) |
| GET | `/api/v1/web/events` | List events (loaded + available from `events/` folder) |
| POST | `/api/v1/web/events/load` | Load and start event (Admin, body: `{ "name": "EventName" }`) |
| POST | `/api/v1/web/events/{name}/unload` | Unload event (Admin) |
| GET | `/api/v1/web/events/{name}/state` | Get current event state |
| PATCH | `/api/v1/web/events/{name}/state` | Set event state (Admin, body: `{ "state": 0\|1\|2 }` or `"Starting"`\|`"Running"`\|`"Ending"`) |
| GET | `/api/v1/web/summary` | Summary (sessions, machines, plugins) |
| GET | `/api/v1/web/system/metrics` | System metrics (CPU total/per-core, memory, process name) |
| GET | `/api/v1/web/cors/origins` | CORS origins (default + plugin) |
| POST | `/api/v1/web/cors/origins` | Add CORS origin (Admin, body: `{ "origin": "http://..." }`) |
| DELETE | `/api/v1/web/cors/origins?origin=...` | Remove CORS origin (Admin, plugin origins only) |
| GET | `/api/v1/web/settings/global` | Global settings (sensitive values masked) |
| PATCH | `/api/v1/web/settings/global` | Update global setting (Admin, body: `{ "key", "value" }`) |
| GET | `/api/v1/web/settings/proxy` | Proxy-related settings (read-only) |

### SendPacketRequest (example)

- `clientId`: Session GUID
- `direction`: `"Client"` = to client, `"Module"` = to server/module
- `msgId`: Opcode (e.g. 0x3010)
- `encrypted`, `massive`: boolean
- `data`: Format `type:;:value;:;type:;:value` or `type;:;value;:;type;:;value` (e.g. `uint8`, `int16`, `uint32`, `ascii`, `unicode`, `bool`, `float`, `double`, and array types: `uint8array`, etc.). Field separator: `;:;`, type-value: `:;:` or `;:;` (both work)
- `rawHex` (optional): Raw hex bytes (e.g. `"A1B2C3"`), overrides `data`

### AddServiceRequest (excerpt)

- `name`, `serverType` (enum), `securityType` (enum)
- `remotePort`, `bindPort`, `byteLimitation`, `autoStart`
- `localMachineId`, `remoteMachineId`, `spoofMachineId` (optional)

## Platform support

The plugin runs on **Windows** and **Linux**. System metrics: CPU (total + per-core) and memory use Windows APIs or Linux `/proc` (e.g. `/proc/stat`, `/proc/meminfo`). Event and plugin paths use `Path.Combine` and work on both platforms; event loading accepts both relative and absolute paths.

## Security

- All `/api/v1/web/` routes require an authenticated user (Bearer token).
- Mutations (services, machines, CORS, global settings, events load/unload/state) require **Admin** role; 403 is returned otherwise.
- Rate limiting: 120 requests per minute per user (or per IP if unauthenticated); 429 when exceeded.
- Sensitive setting keys (e.g. secrets, passwords) are masked in GET responses.
