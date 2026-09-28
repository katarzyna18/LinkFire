# Music Albums Library

## Overview

A small ASP.NET Core microservice that lets a user keep a personal library of music albums.

- A user is just a name and owns exactly one library.
- Albums are found through an external music catalogue (Deezer) by album and/or artist name.
- One or more search results can be added to the library; albums can be removed again.
- Search results and saved albums expose the artist name, album name, cover (when available) and public album URL.

Stack: .NET 10, ASP.NET Core Web API, EF Core with SQLite, `HttpClientFactory`, NUnit, Docker. No authentication (not required by the assignment).

## Architecture

```
src/
  Linkfire.MusicLibrary.Api             HTTP layer: controllers, request/response DTOs, ProblemDetails mapping, Swagger
  Linkfire.MusicLibrary.Application     Use cases (UserService, LibraryService), IMusicCatalogProvider, persistence contract
  Linkfire.MusicLibrary.Domain          User, Library, SavedAlbum, CatalogAlbum and the duplicate-album rule
  Linkfire.MusicLibrary.Infrastructure  DeezerMusicCatalogProvider (+ Deezer wire contracts), EF Core DbContext, migration
tests/
  Linkfire.MusicLibrary.UnitTests       Domain rules, application services, Deezer adapter
  Linkfire.MusicLibrary.ApiTests        End-to-end HTTP tests via WebApplicationFactory
```

Two flows go through the service:

```
Search:   HTTP GET /api/albums/search
          -> AlbumsController -> IMusicCatalogProvider -> DeezerMusicCatalogProvider -> api.deezer.com
          <- IReadOnlyList<CatalogAlbum> (provider-neutral) <- Deezer JSON mapped inside the adapter

Library:  HTTP POST/GET/DELETE /api/users/{userId}/library/...
          -> LibraryController -> LibraryService -> Library (domain rules) -> IMusicLibraryDbContext -> EF Core -> SQLite
```

Dependencies point inwards: `Api -> Infrastructure -> Application -> Domain`. `Infrastructure` implements the interfaces `Application` declares; nothing outside `Infrastructure/Deezer` knows Deezer's field names.

## Why this architecture?

The assignment is explicit that the interesting question is how the design absorbs a second and third catalogue provider. The one abstraction that earns its place is therefore the provider boundary:

```csharp
public interface IMusicCatalogProvider
{
    string ProviderName { get; }
    Task<IReadOnlyList<CatalogAlbum>> SearchAlbumsAsync(AlbumSearchQuery query, CancellationToken cancellationToken);
}
```

Everything else is deliberately plain:

- Application services use EF Core directly through a one-interface `IMusicLibraryDbContext` (three `DbSet`s and `SaveChangesAsync`). There are no per-entity repositories, no CQRS, no mediator. The interface exists only so the application layer does not reference the SQLite provider or migrations.
- The duplicate rule lives on the `Library` aggregate (`AddAlbum` returns `null` for a duplicate) and is backed by a unique index in the database, so the rule is unit-testable without a database and still enforced under concurrency.
- Controllers translate HTTP to service calls and back. Known failures are exceptions (`NotFoundException`, `MusicCatalogUnavailableException`) mapped once in `ApiExceptionHandler` to ProblemDetails.

A four-project split for a service this size is on the generous side; it was kept because it makes the provider boundary and the "what stays the same" argument visible in the folder structure rather than only in prose.

## Running locally

Prerequisites: .NET 10 SDK.

```bash
dotnet restore
dotnet build
dotnet run --project src/Linkfire.MusicLibrary.Api
```

The API listens on `http://localhost:5080`. Swagger UI: `http://localhost:5080/swagger`. OpenAPI document: `http://localhost:5080/openapi/v1.json`.

The SQLite database `musiclibrary.db` is created next to the project (`src/Linkfire.MusicLibrary.Api/`) on first start; EF Core migrations are applied automatically at startup. Delete the file to start from scratch. Deezer's public search endpoint needs no credentials.

Configuration (`appsettings.json`, overridable with environment variables such as `Deezer__TimeoutSeconds`):

| Key | Default | Purpose |
| --- | --- | --- |
| `ConnectionStrings:MusicLibrary` | `Data Source=musiclibrary.db` | SQLite file |
| `Deezer:BaseUrl` | `https://api.deezer.com/` | Provider base URL |
| `Deezer:TimeoutSeconds` | `5` | Per-request timeout for Deezer |
| `Deezer:MaxResults` | `25` | Page size requested from Deezer |

## Running with Docker

Prerequisites: Docker with Compose v2.

```bash
docker compose up --build
```

The API is exposed on `http://localhost:8080` (Swagger: `http://localhost:8080/swagger`). The SQLite file is stored in the named volume `musiclibrary-data` mounted at `/app/data`, so data survives `docker compose down` / `up`. Use `docker compose down -v` to delete it. Stop with `Ctrl+C` or `docker compose down`.

The image is a standard multi-stage build (`sdk:10.0` to publish, `aspnet:10.0` to run) and runs as the non-root `app` user.

Note: Docker was not available on the machine used to write this solution, so the Dockerfile and compose file were reviewed and the `dotnet publish -c Release` step they rely on was verified locally, but the image build and container start were not executed. Please report any issue.

## Running tests

```bash
dotnet test
```

This runs both projects (46 tests). Tests are deterministic and need no network: the Deezer adapter is tested against a stubbed `HttpMessageHandler`, and the API tests replace the provider with an in-process fake. Both use in-memory SQLite, so nothing is written to disk.

To run a single project:

```bash
dotnet test tests/Linkfire.MusicLibrary.UnitTests
dotnet test tests/Linkfire.MusicLibrary.ApiTests
```

## API examples

Base URL below is the local one (`http://localhost:5080`); with Docker use `http://localhost:8080`.

Create a user (201; response includes the user id you need next):

```bash
curl -s -X POST http://localhost:5080/api/users \
  -H "Content-Type: application/json" \
  -d '{"name":"Kasia"}'
# {"id":"6f1c...","name":"Kasia","libraryId":"9a2d..."}
```

Search the catalogue (200; either parameter may be omitted, but not both):

```bash
curl -s "http://localhost:5080/api/albums/search?album=Discovery&artist=Daft%20Punk"
# [{"provider":"deezer","providerAlbumId":"302127","artistName":"Daft Punk","albumName":"Discovery",
#   "coverUrl":"https://cdn-images.dzcdn.net/.../250x250-000000-80-0-0.jpg","albumUrl":"https://www.deezer.com/album/302127"}, ...]
```

Add one or more albums to the library (200). The body is a list of search results, copied as-is:

```bash
curl -s -X POST http://localhost:5080/api/users/{userId}/library/albums \
  -H "Content-Type: application/json" \
  -d '{
    "albums": [
      {
        "provider": "deezer",
        "providerAlbumId": "302127",
        "artistName": "Daft Punk",
        "albumName": "Discovery",
        "coverUrl": "https://cdn-images.dzcdn.net/images/cover/5718f7c81c27e0b2417e2a4c45224f8a/250x250-000000-80-0-0.jpg",
        "albumUrl": "https://www.deezer.com/album/302127"
      }
    ]
  }'
# {"added":[{"id":"cc45...","provider":"deezer","providerAlbumId":"302127",...,"addedAt":"2026-..."}],"skipped":[]}
```

Retrieve the library (200):

```bash
curl -s http://localhost:5080/api/users/{userId}/library
# {"id":"9a2d...","userId":"6f1c...","albums":[{"id":"cc45...","provider":"deezer",...}]}
```

Remove an album by its saved-album `id` (204):

```bash
curl -s -X DELETE http://localhost:5080/api/users/{userId}/library/albums/{albumId} -i
```

Error responses use RFC 9457 ProblemDetails:

| Situation | Status |
| --- | --- |
| Blank user name, empty `albums` list, missing `provider`/`providerAlbumId`, malformed JSON, search without `album` and `artist`, search term longer than 200 characters | 400 |
| Unknown user, unknown saved album, non-GUID id, unknown route | 404 |
| Two requests changed the same library at the same time (unique index hit); safe to retry | 409 |
| Deezer unreachable, timed out, non-2xx, malformed body or Deezer error payload | 503 |

Adding an album that is already in the library is idempotent: it returns 200 and lists the album under `skipped` instead of failing with 409. Batches typically come from a search screen where the user may re-select something already saved, and "make sure these are in my library" is the more useful contract. A 409 would force clients to diff first. The 409 is reserved for a genuine race: the in-memory duplicate check cannot see a concurrent request, so the database unique index is the last line of defence and its violation is reported as a retryable conflict rather than a 500.

## Provider design

`IMusicCatalogProvider` is the only place the application meets an external catalogue. `DeezerMusicCatalogProvider` (in `Infrastructure/Deezer`) owns:

- the HTTP call (`HttpClientFactory` typed client, base URL and timeout from `DeezerOptions`);
- Deezer's wire contracts (`DeezerSearchResponse`, `DeezerAlbum`, ... all `internal`);
- mapping to `CatalogAlbum`;
- failure translation: network errors, timeouts, non-2xx, unparseable JSON and Deezer's in-band `{"error": {...}}` payloads all become `MusicCatalogUnavailableException`, which the API turns into a 503 without leaking provider details. A caller's own cancellation is passed through untouched.

`CatalogAlbum` is the provider-neutral shape used by search results, by the add-to-library request and by the domain:

```csharp
public sealed record CatalogAlbum(
    string Provider, string ProviderAlbumId, string ArtistName, string AlbumName, string? CoverUrl, string AlbumUrl);
```

Normalization decisions per field:

- **Provider album ID**: stored as a string. Deezer uses numeric ids, Spotify uses base-62 strings; a string column with the provider name next to it handles both. Identity of a saved album is `(provider, providerAlbumId)`; the provider name is compared case-insensitively and stored lower-case, the id exactly as the provider defines it.
- **Artist name**: Deezer returns a single primary artist; Spotify returns an array. The neutral model keeps one display string, so a Spotify adapter would join names (`"A, B"`) or take the first. If per-artist data were ever needed the model would gain an `Artists` collection at that point, not speculatively.
- **Album name**: trimmed, otherwise as delivered. Providers differ in how they label editions ("Deluxe", "Remastered"); no attempt is made to canonicalise across providers because the same provider id is what dedupes, not the title.
- **Artwork URL**: providers return several sizes. Each adapter picks one reasonable size (Deezer: `cover_medium`, 250px, falling back to `cover`); missing artwork is `null`, never an empty string or placeholder.
- **Public album URL**: required. Results without a usable id or link are skipped by the adapter rather than failing the whole search.

### Adding Spotify

1. Create `Infrastructure/Spotify/SpotifyMusicCatalogProvider : IMusicCatalogProvider` with `ProviderName => "spotify"`, its own `SpotifyOptions` (client id/secret, base URL) and `internal` wire contracts.
2. Add a small token handler (`DelegatingHandler`) for the client-credentials flow and register it on Spotify's typed `HttpClient`, so auth stays inside the adapter.
3. Map `albums[].id`, `albums[].name`, `albums[].artists[].name`, `albums[].images[]` (pick e.g. the 300px entry) and `albums[].external_urls.spotify` to `CatalogAlbum`.
4. Register it in `AddInfrastructure`.

No change is needed in `Domain`, `Application`, controllers, DTOs, persistence or the existing tests. Saved albums already carry `provider`, so Deezer and Spotify entries coexist in one library.

## Multiple providers

What stays the same when Spotify, and later a third provider, arrives:

- `CatalogAlbum`, `AlbumSearchQuery`, `MusicCatalogUnavailableException`
- `Library`, `SavedAlbum`, the duplicate rule and the database schema
- `LibraryService`, `UserService`, all controllers and DTOs
- the API tests (they use a fake provider) and the domain/service unit tests

What is added: one adapter per provider plus its unit tests against a stubbed `HttpMessageHandler`.

What changes once, when the second provider is introduced: the search endpoint currently injects the single `IMusicCatalogProvider`. With two providers the controller would instead call a small `AlbumSearchService` sitting in `Application` that receives `IEnumerable<IMusicCatalogProvider>` and either:

- **selects** one provider from a `?provider=deezer` query parameter (default from configuration), or
- **aggregates**: fans the query out with `Task.WhenAll`, tags results with their provider (already part of `CatalogAlbum`), and degrades gracefully when one provider fails (return the others' results plus a `warnings` field) instead of failing the whole search.

Cross-provider deduplication (the same album on Deezer and Spotify) is intentionally not attempted: there is no shared key, and UPC/ISRC matching is a separate problem. Both entries can be saved; they have different `(provider, providerAlbumId)`.

This orchestration is not implemented now because it would be code without a second caller.

## Rate limits and resilience

Implemented:

- Per-request timeout on the Deezer `HttpClient` (5 s, configurable) so a slow provider cannot pin request threads.
- `CancellationToken` flows from the HTTP request to the outbound call; client disconnects cancel the Deezer call.
- Deezer's quota error (HTTP 200 with `error.code = 4`) is recognised and surfaced as 503, so callers can back off.
- Failures are logged with status code / error code, without logging the full payload.
- `HttpClientFactory` handler pooling (no socket exhaustion).

Not implemented, and what production would add:

- **Provider-specific limits**: Deezer allows roughly 50 requests per 5 seconds per IP; Spotify uses a rolling 30-second window and returns 429 with `Retry-After`. Each adapter would own its own policy; there is no shared rule.
- **Retries with backoff and jitter**: only for idempotent GETs and only on transient failures (5xx, 429, timeouts, connection resets), typically 2 attempts with exponential backoff and jitter, honouring `Retry-After` when present. `Microsoft.Extensions.Http.Resilience` provides this as a handler on the typed client without touching adapter code.
- **Circuit breaker**: per provider, so a Deezer outage fails fast and (with multiple providers) the search degrades to the healthy ones.
- **Caching**: search results for identical `(provider, artist, album)` queries are safe to cache for minutes; a memory cache in-process, or a distributed cache if the service scales out. Cover URLs are stable and are stored with the saved album already, so retrieving a library never calls the provider.
- **Metrics and alerting**: per-provider request count, latency histogram, error rate and 429 count; alert on error-rate and circuit-open events. OpenTelemetry traces through the outbound `HttpClient` give per-request visibility.

## Testing strategy

There is no separate QA step for this service, so the tests are the safety net and were chosen for behaviour rather than coverage.

Unit tests (`Linkfire.MusicLibrary.UnitTests`, 30 tests):

- **Domain** (`LibraryTests`): user creation always creates one library; blank names are rejected; adding stores provider-neutral data; the same `(provider, providerAlbumId)` is not added twice (case-insensitive provider); the same id from another provider is a different album; remove reports unknown ids. These run with no I/O.
- **Application** (`LibraryServiceTests`): the services against a real EF Core model on in-memory SQLite. Covers persistence of user + library, adding several albums, skipping duplicates across and within requests, removal, `NotFoundException` for unknown users/albums, and the concurrent-insert race surfacing as `ConflictException` (simulated with the `SavingChanges` hook). Using real SQLite instead of the EF in-memory provider means the mapping, unique indexes and change tracking are actually exercised.
- **Infrastructure** (`DeezerMusicCatalogProviderTests`): the adapter with a stubbed `HttpMessageHandler`. Covers mapping (including cover fallback and id-to-string), the outgoing URL and limit, empty results, skipping unusable entries, non-2xx, network errors, timeout, malformed JSON, Deezer's error payload, and that caller cancellation is not reported as a provider failure.
- `AlbumSearchQueryTests`: the "at least one criterion" rule and trimming.

API tests (`Linkfire.MusicLibrary.ApiTests`, 16 tests) boot the real application with `WebApplicationFactory<Program>`, swapping in an in-memory SQLite connection and a `FakeMusicCatalogProvider`:

- `LibraryFlowTests`: create user (201 + Location) -> search -> add two albums -> read library -> re-add (skipped) -> delete (204) -> read again -> delete again (404); libraries are isolated between users.
- `ErrorHandlingTests`: 404 ProblemDetails for unknown user/album, non-GUID ids and unknown routes; 400 for blank name, empty album list, missing provider identifiers, malformed JSON, empty search and over-long search terms; 503 when the provider is unavailable, asserting that the provider's internal message does not leak.

Live Deezer is not needed for any automated test: the provider is a boundary, so its behaviour is simulated on both sides of it (fake provider above, stubbed HTTP below). The real integration was exercised manually (search for "Daft Punk" / "Discovery" returns the expected album with cover and URL).

## Trade-offs

- **SQLite instead of SQL Server/PostgreSQL**: zero setup for the reviewer and one file to delete to reset. EF Core hides the difference; switching is a provider package and a connection string. Not a statement about production databases.
- **Deezer instead of Spotify**: no app registration, no secrets, so the reviewer can run everything immediately. The provider boundary is the same either way.
- **Docker Compose instead of Kubernetes**: one service with one file-based dependency does not justify manifests, and Compose is what a reviewer is most likely to have installed.
- **No authentication**: explicitly optional in the brief. Production would need it (see below).
- **No distributed cache, no retries/circuit breaker**: discussed above; adding them without a load profile would be guesswork.
- **No repository layer, no CQRS/mediator**: `LibraryService` and `UserService` are the use cases; EF Core already is the repository and unit of work.
- **Album data is copied from the search result into the add request** instead of being re-fetched from the provider by id. This makes adding independent of provider availability and rate limits and keeps the request cheap. The cost is trusting the client's copy of the metadata; with authentication in place, re-validating against the provider (or accepting only `(provider, id)` and fetching server-side) is a reasonable tightening.
- **Swagger UI is always on**: there is nothing to protect yet. It would be limited to non-production environments in a real deployment.
- **Deezer's fielded search syntax is not used**: `search/album?q=artist:"..." album:"..."` returns no results for combined filters (verified during development), so both terms are sent as free text, which ranks the intended album first.

These keep the solution proportional to a four-hour assignment; each has a clear production replacement.

## Production improvements

In the order I would actually do them:

1. **Authentication and authorization**: users may only read or change their own library. Without this the service is a demo.
2. **Resilience on the provider client**: timeout is there; add retry-with-jitter for transient failures and a circuit breaker per provider, plus per-provider metrics and alerting.
3. **Second provider and the search orchestration** described above, driven by a real product need.
4. **Database**: PostgreSQL (or SQL Server) with migrations applied by a deployment step rather than at application start, so multiple instances do not race.
5. **Observability**: OpenTelemetry traces and metrics, request/provider latency and error dashboards, health endpoints (`/health/live`, `/health/ready` including a provider probe).
6. **Pagination** for library retrieval and search once libraries grow; both endpoints are unbounded today.
7. **Search caching** for repeated queries to cut provider calls and rate-limit exposure.

## AI usage

AI-assisted development tools were used as a productivity aid for drafting boilerplate, tests and this document. Architecture and design decisions, code review, running the tests, manual verification against the live Deezer API and the final content of the repository were done and validated by me.
