# ─── Stage 1: restore ──────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS restore
WORKDIR /app

# Copy solution manifest and project files first to exploit Docker layer caching.
# Directory.Build.props must be present before restore so SDK settings apply.
COPY HealthPlatform.sln                                                     ./
COPY Directory.Build.props                                                  ./
COPY HealthPlatform.Api/HealthPlatform.Api.csproj                          ./HealthPlatform.Api/
COPY HealthPlatform.Application/HealthPlatform.Application.csproj          ./HealthPlatform.Application/
COPY HealthPlatform.Domain/HealthPlatform.Domain.csproj                    ./HealthPlatform.Domain/
COPY HealthPlatform.Infrastructure/HealthPlatform.Infrastructure.csproj    ./HealthPlatform.Infrastructure/
COPY HealthPlatform.Tests/HealthPlatform.Tests.csproj                      ./HealthPlatform.Tests/

RUN dotnet restore HealthPlatform.sln

# ─── Stage 2: development (hot-reload via dotnet watch) ───────────────────────
FROM restore AS development
WORKDIR /app

# Copy full source — overlaid by a bind-mount volume in Compose so changes are
# picked up immediately without rebuilding the image.
COPY . .

EXPOSE 5013

CMD ["dotnet", "watch", "--project", "HealthPlatform.Api/HealthPlatform.Api.csproj", \
     "run", "--urls", "http://+:5013", "--no-launch-profile"]

# ─── Stage 3: build (CI / release candidate) ──────────────────────────────────
FROM restore AS build
WORKDIR /app
COPY . .
RUN dotnet publish HealthPlatform.Api/HealthPlatform.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore

# ─── Stage 4: release (minimal runtime image) ─────────────────────────────────
# Uses Debian-based aspnet image (not Alpine) because Tesseract.NET and PDFtoImage
# bundle native glibc-linked binaries that are incompatible with Alpine musl libc.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS release
WORKDIR /app

# Install Tesseract OCR and English language data for local OCR processing
RUN apt-get update && apt-get install -y --no-install-recommends \
        tesseract-ocr \
        tesseract-ocr-eng \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Run as non-root user (security hardening — OWASP A05)
RUN groupadd -r appgroup && useradd -r -g appgroup appuser
USER appuser

COPY --from=build /app/publish .

EXPOSE 5013
ENTRYPOINT ["dotnet", "HealthPlatform.Api.dll"]
