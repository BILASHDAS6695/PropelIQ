# ─── Stage 1: deps (npm install) ──────────────────────────────────────────────
FROM node:20-alpine AS deps

WORKDIR /app

# Copy manifests first — layer is cached until package-lock.json changes
COPY package.json package-lock.json ./
RUN npm ci --prefer-offline

# ─── Stage 2: development (ng serve with hot-reload) ──────────────────────────
FROM deps AS development

ENV NODE_ENV=development

# Copy full source — overlaid by bind-mount volume in Compose so edits on the
# host are immediately visible inside the container.
COPY . .

EXPOSE 4200

# --host 0.0.0.0 binds to all interfaces so the port is reachable from the host.
# --disable-host-check is required because the container hostname != localhost.
CMD ["npx", "ng", "serve", "--host", "0.0.0.0", "--port", "4200", \
     "--proxy-config", "proxy.conf.json", "--disable-host-check"]

# ─── Stage 3: build (CI / production artefact) ────────────────────────────────
FROM deps AS build
COPY . .
RUN npx ng build --configuration production

# ─── Stage 4: release (serve static files via nginx) ─────────────────────────
FROM nginx:1.27-alpine AS release
COPY --from=build /app/dist/health-platform-ui/browser /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
