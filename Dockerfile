FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the package layer is cached while source files change.
COPY src/Linkfire.MusicLibrary.Domain/Linkfire.MusicLibrary.Domain.csproj src/Linkfire.MusicLibrary.Domain/
COPY src/Linkfire.MusicLibrary.Application/Linkfire.MusicLibrary.Application.csproj src/Linkfire.MusicLibrary.Application/
COPY src/Linkfire.MusicLibrary.Infrastructure/Linkfire.MusicLibrary.Infrastructure.csproj src/Linkfire.MusicLibrary.Infrastructure/
COPY src/Linkfire.MusicLibrary.Api/Linkfire.MusicLibrary.Api.csproj src/Linkfire.MusicLibrary.Api/
RUN dotnet restore src/Linkfire.MusicLibrary.Api/Linkfire.MusicLibrary.Api.csproj

COPY src/ src/
RUN dotnet publish src/Linkfire.MusicLibrary.Api/Linkfire.MusicLibrary.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# SQLite file lives here; docker-compose mounts a volume on it so data survives container restarts.
RUN mkdir -p /app/data && chown $APP_UID:$APP_UID /app/data
USER $APP_UID

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
ENV ConnectionStrings__MusicLibrary="Data Source=/app/data/musiclibrary.db"
EXPOSE 8080

ENTRYPOINT ["dotnet", "Linkfire.MusicLibrary.Api.dll"]
