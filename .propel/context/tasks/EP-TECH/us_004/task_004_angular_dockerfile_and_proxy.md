# Task 004: Angular Dev Server Dockerfile & Proxy Configuration

## Context

| Field | Value |
|-------|-------|
| **User Story** | US-004 |
| **Epic** | EP-TECH |
| **Layer** | DevOps / Frontend Container |
| **Priority** | High |
| **Estimated Effort** | 1 hour |
| **Dependencies** | Task 001 |

## Objective

Create a lightweight Dockerfile for the Angular development server that runs `ng serve` with hot-reload inside Docker, and update `proxy.conf.json` to target the `.NET API` container by its Compose service name so browser requests are correctly forwarded.

## Implementation Steps

### 1. Create the Angular Dockerfile

**File:** `src/health-platform-ui/Dockerfile`

```dockerfile
# ─── Stage 1: deps (npm install) ──────────────────────────────────────────────
FROM node:20-alpine AS deps

WORKDIR /app

# Copy manifests first — layer is cached until package-lock.json changes
COPY package.json package-lock.json ./
RUN npm ci --prefer-offline

# ─── Stage 2: development (ng serve with hot-reload) ──────────────────────────
FROM deps AS development

ENV NODE_ENV=development

# Copy full source — will be overlaid by bind-mount volume in Compose.
# The image copy here is only a fallback if no volume is mounted.
COPY . .

EXPOSE 4200

# --host 0.0.0.0 binds to all interfaces so the port is reachable from the host
# --disable-host-check is needed because the container hostname != localhost
CMD ["npx", "ng", "serve", "--host", "0.0.0.0", "--port", "4200", \
     "--proxy-config", "proxy.conf.json", "--disable-host-check"]

# ─── Stage 3: build (CI / production artefact) ────────────────────────────────
FROM deps AS build
COPY . .
RUN npx ng build --configuration production

# ─── Stage 4: release (serve static via nginx) ────────────────────────────────
FROM nginx:1.27-alpine AS release
COPY --from=build /app/dist/health-platform-ui/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
```

### 2. Create `.dockerignore` for the Angular build context

**File:** `src/health-platform-ui/.dockerignore`

```
node_modules/
.angular/
dist/
coverage/
.vscode/
*.md
```

### 3. Update `proxy.conf.json` for Docker networking

Inside the Compose network, the Angular container must resolve the API by its **service name** `api`, not `localhost`.

**File:** `src/health-platform-ui/proxy.conf.json`

```json
{
  "/api": {
    "target": "http://api:5013",
    "secure": false,
    "changeOrigin": true,
    "logLevel": "debug"
  }
}
```

### 4. Create `proxy.conf.local.json` for native (non-Docker) development

Preserve the original localhost target for developers who run `ng serve` directly:

**File:** `src/health-platform-ui/proxy.conf.local.json`

```json
{
  "/api": {
    "target": "https://localhost:5013",
    "secure": false,
    "changeOrigin": true,
    "logLevel": "debug"
  }
}
```

Update `package.json` scripts to support both modes:

```json
{
  "scripts": {
    "start":        "ng serve --proxy-config proxy.conf.local.json",
    "start:docker": "ng serve --host 0.0.0.0 --proxy-config proxy.conf.json --disable-host-check"
  }
}
```

### 5. Create `nginx.conf` for the release stage

**File:** `src/health-platform-ui/nginx.conf`

```nginx
server {
    listen       80;
    server_name  _;
    root         /usr/share/nginx/html;
    index        index.html;

    # SPA fallback — return index.html for all non-asset routes
    location / {
        try_files $uri $uri/ /index.html;
    }

    # Cache static assets aggressively
    location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff2?)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
    }
}
```

## Acceptance Criteria

- [ ] `src/health-platform-ui/Dockerfile` exists with stages: `deps`, `development`, `build`, `release`
- [ ] `development` stage runs `ng serve --host 0.0.0.0` with `proxy.conf.json`
- [ ] `proxy.conf.json` targets `http://api:5013` (Docker service name)
- [ ] `proxy.conf.local.json` targets `https://localhost:5013` (native dev)
- [ ] `npm start` uses `proxy.conf.local.json`; `npm run start:docker` uses `proxy.conf.json`
- [ ] `src/health-platform-ui/.dockerignore` excludes `node_modules/`, `.angular/`, `dist/`
- [ ] `docker build --target development -t hp-web-dev .` succeeds from `src/health-platform-ui/`

## Verification

```bash
# Build dev stage
docker build --target development -t hp-web-dev src/health-platform-ui/

# Confirm image size is reasonable (should be ~300–500 MB for node+angular)
docker images hp-web-dev --format "{{.Size}}"
```

## Traceability

| Requirement | Acceptance Criteria |
|-------------|---------------------|
| US-004 AC-6 | Angular dev server proxies API calls to .NET container |
| TR-033 | Docker dev environment |
