FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached while source files change.
COPY .config/dotnet-tools.json .config/dotnet-tools.json
COPY src/Linkfire.MusicLibrary.Domain/Linkfire.MusicLibrary.Domain.csproj src/Linkfire.MusicLibrary.Domain/
COPY src/Linkfire.MusicLibrary.Application/Linkfire.MusicLibrary.Application.csproj src/Linkfire.MusicLibrary.Application/
COPY src/Linkfire.MusicLibrary.Infrastructure/Linkfire.MusicLibrary.Infrastructure.csproj src/Linkfire.MusicLibrary.Infrastructure/
COPY src/Linkfire.MusicLibrary.Api/Linkfire.MusicLibrary.Api.csproj src/Linkfire.MusicLibrary.Api/
RUN dotnet restore src/Linkfire.MusicLibrary.Api/Linkfire.MusicLibrary.Api.csproj
RUN dotnet tool restore

COPY src/ src/
RUN dotnet publish src/Linkfire.MusicLibrary.Api/Linkfire.MusicLibrary.Api.csproj -c Release -o /app/publish --no-restore

# Framework-dependent bundle: the migrate stage already has the ASP.NET runtime.
# Schema changes are applied by this executable, not by the API process.
RUN dotnet ef migrations bundle \
    --project src/Linkfire.MusicLibrary.Infrastructure \
    --startup-project src/Linkfire.MusicLibrary.Api \
    --configuration Release \
    --output /app/efbundle \
    --force

# One-shot schema step. Compose runs this to completion before any API replica starts,
# so several pods never migrate the same database at the same time.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migrate
WORKDIR /migrate
RUN mkdir -p /data && chown $APP_UID:$APP_UID /data /migrate
COPY --from=build --chown=$APP_UID:$APP_UID /app/efbundle ./efbundle
# The bundle builds the application host before it migrates, so the same configuration
# the API needs (connection string, Deezer options) must be available beside it.
# --connection still selects the database file; this file only lets the host start.
COPY --from=build --chown=$APP_UID:$APP_UID /src/src/Linkfire.MusicLibrary.Api/appsettings.json ./appsettings.json
USER $APP_UID
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["./efbundle"]
CMD ["--connection", "Data Source=/data/musiclibrary.db"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# SQLite file lives here; docker-compose mounts a volume on it so data survives container restarts.
RUN mkdir -p /app/data && chown $APP_UID:$APP_UID /app/data
USER $APP_UID

COPY --from=build --chown=$APP_UID:$APP_UID /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
ENV ConnectionStrings__MusicLibrary="Data Source=/app/data/musiclibrary.db"
ENV Database__MigrateOnStartup=false
EXPOSE 8080

ENTRYPOINT ["dotnet", "Linkfire.MusicLibrary.Api.dll"]
