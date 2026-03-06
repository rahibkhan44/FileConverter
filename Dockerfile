# ═══ FileConverter — Multi-stage Dockerfile ═══
# .NET 9 + FFmpeg for video/audio conversion support

# ── Build stage ──
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files for restore
COPY src/FileConverter.Domain/FileConverter.Domain.csproj src/FileConverter.Domain/
COPY src/FileConverter.Application/FileConverter.Application.csproj src/FileConverter.Application/
COPY src/FileConverter.Infrastructure/FileConverter.Infrastructure.csproj src/FileConverter.Infrastructure/
COPY src/FileConverter.Shared/FileConverter.Shared.csproj src/FileConverter.Shared/
COPY src/FileConverter.API/FileConverter.API.csproj src/FileConverter.API/
COPY src/FileConverter.Web/FileConverter.Web.csproj src/FileConverter.Web/

RUN dotnet restore src/FileConverter.API/FileConverter.API.csproj
RUN dotnet restore src/FileConverter.Web/FileConverter.Web.csproj

# Copy everything and build
COPY src/ src/
RUN dotnet publish src/FileConverter.API/FileConverter.API.csproj -c Release -o /app/api --no-restore
RUN dotnet publish src/FileConverter.Web/FileConverter.Web.csproj -c Release -o /app/web --no-restore

# ── API Runtime ──
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS api
WORKDIR /app

# Install FFmpeg for video/audio conversion
RUN apt-get update && \
    apt-get install -y --no-install-recommends ffmpeg && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

COPY --from=build /app/api .

ENV ASPNETCORE_URLS=http://+:5001
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5001

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:5001/health/live || exit 1

ENTRYPOINT ["dotnet", "FileConverter.API.dll"]

# ── Web Runtime ──
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS web
WORKDIR /app

COPY --from=build /app/web .

ENV ASPNETCORE_URLS=http://+:5002
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5002

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:5002/ || exit 1

ENTRYPOINT ["dotnet", "FileConverter.Web.dll"]
