# MemoryMonitor# README.md

## Memory & CPU Monitor API Server (.NET 8)

Cross‑OS monitor for **Windows / Linux / macOS** providing:

* **REST APIs**: system memory, top processes by working set, CPU usage (overall & per‑core on Linux)
* **Prometheus `/metrics`**: memory + CPU exportable to Prometheus/Grafana
* Lightweight **console output** for live sampling

> This repo contains a minimal, production‑ready baseline you can extend (auth, CORS, logging, tracing, dashboards).

---

### ✨ Features

* Cross‑platform: Windows (P/Invoke), Linux (`/proc`), macOS (`sysctl`)
* Minimal API (no controllers) + Kestrel HTTP server
* Prometheus text format compatible with `scrape_configs`
* Top‑N process list (by working set)

### 🧩 Requirements

* .NET 8 SDK or later
* (Linux) access to `/proc`
* (macOS) `sysctl` available

### 🚀 Quick start

```bash
# Build
 dotnet build

# Run: sample every 1s and expose metrics on 127.0.0.1:9095
 dotnet run -- 1 --metrics 127.0.0.1:9095

# Check metrics
 curl http://127.0.0.1:9095/metrics
```

### 🛣️ Endpoints

| Route                    | Method | Description                                                     |
| ------------------------ | ------ | --------------------------------------------------------------- |
| `/`                      | GET    | About & endpoint list                                           |
| `/healthz`               | GET    | Health probe                                                    |
| `/api/v1/memory/system`  | GET    | System memory (total/available/used/ratio)                      |
| `/api/v1/memory/process` | GET    | **Top 10** processes by working set (JSON)                      |
| `/api/v1/cpu`            | GET    | CPU overall usage ratio, logical cores, per‑core ratios (Linux) |
| `/metrics`               | GET    | Prometheus text format (memory + CPU)                           |

#### Example responses

```bash
curl -s http://127.0.0.1:9095/api/v1/memory/system | jq
curl -s http://127.0.0.1:9095/api/v1/cpu | jq
curl -s http://127.0.0.1:9095/api/v1/memory/process | jq
```

### 🔧 CLI arguments

```
[intervalSeconds]       Sampling interval in seconds (default: 1)
--metrics host:port     Enable Prometheus endpoint and listen on host:port
```

### 📦 Docker (optional)

Create a simple container for Linux:

```dockerfile
# Dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app
COPY ./bin/Release/net8.0/publish/ .
EXPOSE 9095
ENTRYPOINT ["./MemoryMonitor", "1", "--metrics", "0.0.0.0:9095"]
```

Build & run:

```bash
 dotnet publish -c Release -r linux-x64 --self-contained false
 docker build -t memmon:latest .
 docker run --rm -p 9095:9095 memmon:latest
```

### 🔒 Notes / hardening

* Add reverse proxy (nginx/Apache) if exposing to the Internet
* Consider Basic Auth / mTLS for restricted environments
* Add rate limiting, request logging, OpenTelemetry tracing if needed

### 🧪 Testing ideas

* Unit tests for parsers (`/proc/meminfo`, `/proc/stat`, sysctl)
* Golden file tests for Prometheus output

### 📄 License

MIT — see [LICENSE](LICENSE).

---

# .gitignore

```
# Build artifacts
bin/
obj/

# VS/JetBrains
.vs/
.idea/
*.suo
*.user
*.userosscache
*.sln.docstates

# VS Code settings (keep if you want to share workspace settings)
.vscode/

# Test results & logs
TestResults/
*.trx
*.log
logs/

# NuGet
*.nupkg
packages/
.nuget/

# Publish outputs
publish/
*.deps.json
*.runtimeconfig.json

# OS junk
.DS_Store
Thumbs.db

# Rider / Roslyn caches
*.DotSettings.user
.ReSharper*/
_Resharper*/
```
