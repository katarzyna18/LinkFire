# Music Albums Library

## Overview

A small ASP.NET Core microservice that lets a user keep a personal library of music albums.

- A user is just a name and owns exactly one library.
- Albums are found through an external music catalogue (Deezer) by album and/or artist name.
- One or more search results can be added to the library; albums can be removed again.
- Search results and saved albums expose the artist name, album name, cover (when available) and public album URL.

Stack: .NET 10, ASP.NET Core Web API, EF Core with SQLite, `HttpClientFactory` with the standard HTTP resilience pipeline, NUnit, Docker. No authentication (not required by the assignment).

## Architecture

```
src/
  Linkfire.MusicLibrary.Api             HTTP layer: controllers, request/response DTOs, Result -> ProblemDetails, Swagger
  Linkfire.MusicLibrary.Application     Use cases, Result<T>, IMusicCatalogProvider, CompositeMusicCatalogProvider
  Linkfire.MusicLibrary.Domain          User, Library, SavedAlbum, CatalogAlbum and the duplicate-album rule
  Linkfire.MusicLibrary.Infrastructure  Deezer adapter (+ Deezer wire contracts), EF Core DbContext, migration
tests/
  Linkfire.MusicLibrary.UnitTests       Domain rules, services, composite provider, Deezer adapter, resilience pipeline
  Linkfire.MusicLibrary.ApiTests        End-to-end HTTP tests via WebApplicationFactory
```

Two flows go through the service:

```
Search:   HTTP GET /api/albums/search
          -> AlbumsController -> IMusicCatalogProvider (composite)
          -> each leaf provider (Deezer today) -> api.deezer.com
          <- CatalogSearchResult (albums + which providers failed) <- Deezer JSON mapped inside the adapter

Library:  HTTP POST/GET/DELETE /api/users/{userId}/library/...
          -> LibraryController -> LibraryService -> Library (domain rules) -> IMusicLibraryDbContext -> EF Core -> SQLite
```

Dependencies point inwards: `Api -> Infrastructure -> Application -> Domain`. `Infrastructure` implements the interfaces `Application` declares; nothing outside `Infrastructure/Deezer` knows Deezer's field names.

## Why this architecture?

The assignment is explicit that the interesting question is how the design absorbs a second and third catalogue provider. The provider boundary is therefore the one abstraction that earns its place, and it is already a composite:

```csharp
public interface IMusicCatalogProvider
{
    string ProviderName { get; }
    Task<CatalogSearchResult> SearchAlbumsAsync(AlbumSearchQuery query, CancellationToken cancellationToken);
}
```

Leaf providers (Deezer, and later Spotify) are registered with a keyed service. Anything that searches asks for one `IMusicCatalogProvider` and receives `CompositeMusicCatalogProvider`, which fans the query out and merges the answers. A provider that fails is reported in `FailedProviders`; the search is a 503 only when every provider failed.

Everything else is deliberately plain:

- Application services use EF Core directly through a one-interface `IMusicLibraryDbContext` (three `DbSet`s and `SaveChangesAsync`). There are no per-entity repositories, no CQRS, no mediator. The interface exists only so the application layer does not reference the SQLite provider or migrations.
- Expected failures are values, not exceptions. `Result<T>` carries `ErrorKind.NotFound` or `ErrorKind.Conflict`. Controllers map those to ProblemDetails in one place (`ErrorResults`). Exceptions stay reserved for bugs and for infrastructure faults the caller cannot distinguish.
- The duplicate rule lives on the `Library` aggregate (`AddAlbum` returns `null` for a duplicate) and is backed by a unique index in the database, so the rule is unit-testable without a database and still enforced under concurrency.
- Library retrieval is paged (`page`, `pageSize`, `totalCount`). The query counts and then `Skip`/`Take`s the album rows; it does not `Include` the whole collection.

A four-project split for a service this size is on the generous side; it was kept because it makes the provider boundary and the "what stays the same" argument visible in the folder structure rather than only in prose.

## Running locally

Prerequisites: .NET 10 SDK.

```bash
dotnet restore
dotnet build
dotnet run --project src/Linkfire.MusicLibrary.Api
```

The API listens on `http://localhost:5080`. Swagger UI: `http://localhost:5080/swagger`. OpenAPI document: `http://localhost:5080/openapi/v1.json`.

`dotnet run` uses the Development environment, where `Database:MigrateOnStartup` is `true`, so the SQLite file `musiclibrary.db` is created next to the project (`src/Linkfire.MusicLibrary.Api/`) and migrated on first start. Delete the file to start from scratch. The default (and Production) setting is `false`: deployed environments apply the EF migration bundle before the process starts. Deezer's public search endpoint needs no credentials.

Configuration (`appsettings.json`, overridable with environment variables such as `Deezer__MaxRetries`):

| Key | Default | Purpose |
| --- | --- | --- |
| `ConnectionStrings:MusicLibrary` | `Data Source=musiclibrary.db` | SQLite file |
| `Database:MigrateOnStartup` | `false` (`true` in Development) | Apply migrations inside the API process |
| `Deezer:BaseUrl` | `https://api.deezer.com/` | Provider base URL |
| `Deezer:AttemptTimeoutSeconds` | `5` | Timeout of a single Deezer attempt |
| `Deezer:TotalTimeoutSeconds` | `15` | Budget for the call including retries |
| `Deezer:MaxRetries` | `2` | Retries of transient failures (5xx, 408, 429, network) |
| `Deezer:MaxResults` | `25` | Page size requested from Deezer |

## Running with Docker

Prerequisites: Docker with Compose v2.

```bash
docker compose up --build
```

Compose builds two images from the same Dockerfile. The `migrate` service runs the EF Core migration bundle against the shared volume and must finish successfully before `api` starts (`depends_on` with `service_completed_successfully`). The API itself has `Database:MigrateOnStartup=false`, so extra replicas would not each try to migrate.

The API is exposed on `http://localhost:8080` (Swagger: `http://localhost:8080/swagger`). The SQLite file is stored in the named volume `musiclibrary-data`, so data survives `docker compose down` / `up`. Use `docker compose down -v` to delete it. Stop with `Ctrl+C` or `docker compose down`.

To apply a new migration without Compose:

```bash
dotnet tool restore
dotnet ef migrations bundle --project src/Linkfire.MusicLibrary.Infrastructure --startup-project src/Linkfire.MusicLibrary.Api --output efbundle.exe
# appsettings.json must sit next to the bundle: it builds the application host before migrating,
# and that host refuses to start without ConnectionStrings:MusicLibrary. --connection picks the file.
cp src/Linkfire.MusicLibrary.Api/appsettings.json .
./efbundle.exe --connection "Data Source=musiclibrary.db"
```

The image is a multi-stage build (`sdk:10.0` publishes the API and the bundle, `aspnet:10.0` runs each) and both containers run as the non-root `app` user.

Note: Docker was not available on the machine used to write this solution, so the image build and container start were not executed. The `dotnet publish -c Release` step and a local run of the migration bundle against a temporary SQLite file were verified. Please report any issue.

## Running tests

```bash
dotnet test
```

This runs both projects (61 tests). Tests are deterministic and need no network: the Deezer adapter is tested against a stubbed `HttpMessageHandler`, the resilience pipeline is tested with a scripted handler, and the API tests replace the provider with an in-process fake. Both use in-memory SQLite, so nothing is written to disk.

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

Search the catalogue (200; either parameter may be omitted, but not both). `unavailableProviders` lists catalogues that could not answer; with only Deezer configured it is empty unless you got a 503 instead:

```bash
curl -s "http://localhost:5080/api/albums/search?album=Discovery&artist=Daft%20Punk"
# {"albums":[{"provider":"deezer","providerAlbumId":"302127","artistName":"Daft Punk","albumName":"Discovery",
#   "coverUrl":"https://cdn-images.dzcdn.net/.../250x250-000000-80-0-0.jpg","albumUrl":"https://www.deezer.com/album/302127"}],
#  "unavailableProviders":[]}
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

Retrieve one page of the library (200), oldest additions first. `page` defaults to 1, `pageSize` to 50 (maximum 200):

```bash
curl -s "http://localhost:5080/api/users/{userId}/library?page=1&pageSize=50"
# {"id":"9a2d...","userId":"6f1c...","albums":[{"id":"cc45...","provider":"deezer",...}],
#  "page":1,"pageSize":50,"totalCount":1,"totalPages":1}
```

Remove an album by its saved-album `id` (204):

```bash
curl -s -X DELETE http://localhost:5080/api/users/{userId}/library/albums/{albumId} -i
```

Error responses use RFC 9457 ProblemDetails:

| Situation | Status |
| --- | --- |
| Blank user name, empty `albums` list, missing `provider`/`providerAlbumId`, malformed JSON, search without `album` and `artist`, search term longer than 200 characters, `page` < 1, `pageSize` outside 1–200 | 400 |
| Unknown user, unknown saved album, non-GUID id, unknown route | 404 |
| Two requests changed the same library at the same time (unique index hit); safe to retry | 409 |
| Every configured catalogue failed (unreachable, timed out, non-2xx, malformed body, provider error payload, or retries exhausted) | 503 |

Adding an album that is already in the library is idempotent: it returns 200 and lists the album under `skipped` instead of failing with 409. Batches typically come from a search screen where the user may re-select something already saved, and "make sure these are in my library" is the more useful contract. A 409 would force clients to diff first. The 409 is reserved for a genuine race: the in-memory duplicate check cannot see a concurrent request, so the database unique index is the last line of defence and its violation is reported as a retryable conflict rather than a 500.

When several catalogues are configured, a failure of one of them does not fail the search. The response is 200, `albums` contains whatever the healthy providers returned, and `unavailableProviders` names the rest. 503 is only returned when none of them could answer.

## Provider design

`IMusicCatalogProvider` is the only place the application meets an external catalogue. Leaf adapters are registered with the key `MusicCatalogProviders.LeafServiceKey`. `CompositeMusicCatalogProvider` (registered as the unkeyed `IMusicCatalogProvider`) calls them in parallel, merges albums, and isolates a throwing adapter so one bug cannot fail the search.

`DeezerMusicCatalogProvider` (in `Infrastructure/Deezer`) owns:

- the HTTP call (typed `HttpClient`, base URL and timeouts from `DeezerOptions`);
- the standard resilience pipeline: per-attempt timeout, total timeout, retries with jitter, circuit breaker;
- Deezer's wire contracts (`DeezerSearchResponse`, `DeezerAlbum`, ... all `internal`);
- mapping to `CatalogAlbum`;
- failure translation: network errors, timeouts, non-2xx, unparseable JSON, Deezer's in-band `{"error": {...}}` payloads and a rejected resilience pipeline all become `CatalogSearchResult.Failed("deezer")`. The API turns "every provider failed" into a 503 without leaking provider details. A caller's own cancellation is passed through untouched.

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
2. Add a small token handler (`DelegatingHandler`) for the client-credentials flow and register it on Spotify's typed `HttpClient`, so auth stays inside the adapter. Attach the same standard resilience handler, with Spotify's own timeouts and `Retry-After` behaviour.
3. Map `albums[].id`, `albums[].name`, `albums[].artists[].name`, `albums[].images[]` (pick e.g. the 300px entry) and `albums[].external_urls.spotify` to `CatalogAlbum`. Return `CatalogSearchResult.Success` or `Failed`; do not throw for an outage.
4. Register it with `AddKeyedTransient<IMusicCatalogProvider>(MusicCatalogProviders.LeafServiceKey, ...)`.

No change is needed in `Domain`, `Application` consumers, the composite, controllers, DTOs, persistence or the existing tests. Saved albums already carry `provider`, so Deezer and Spotify entries coexist in one library. Search results from both appear in one response; if Spotify is down, `unavailableProviders` contains `"spotify"` and Deezer's albums are still returned.

## Multiple providers

What stays the same when Spotify, and later a third provider, arrives:

- `CatalogAlbum`, `AlbumSearchQuery`, `CatalogSearchResult`, `CompositeMusicCatalogProvider`
- `Library`, `SavedAlbum`, the duplicate rule and the database schema
- `LibraryService`, `UserService`, all controllers and DTOs
- the API tests (they replace the composite with a fake provider) and the domain/service unit tests

What is added: one adapter per provider, its keyed registration, and its unit tests against a stubbed `HttpMessageHandler`.

The composite already fans the query out with `Task.WhenAll` and degrades when one provider fails. There is no `?provider=` selector; every registered catalogue is searched. A selector would be a query parameter on the controller that filters the leaf list, and it is not implemented because nothing asks for it yet.

Cross-provider deduplication (the same album on Deezer and Spotify) is intentionally not attempted: there is no shared key, and UPC/ISRC matching is a separate problem. Both entries can be saved; they have different `(provider, providerAlbumId)`.

## Rate limits and resilience

Implemented on the Deezer typed client via `AddStandardResilienceHandler` (`Microsoft.Extensions.Http.Resilience`):

- **Attempt timeout** (5 s) and a **total timeout** (15 s) covering the retries, so a slow provider cannot pin the call indefinitely. The `HttpClient.Timeout` sits a few seconds above the total as a last-resort bound.
- **Retries with jitter** (2 by default) for transient failures only: 5xx, 408, 429, connection failures and attempt timeouts. Search is an idempotent GET, so retrying is safe. Client errors (4xx other than 408/429) are not retried.
- **Circuit breaker** per client, with a sampling window at least twice the attempt timeout (the pipeline rejects a shorter window).
- `CancellationToken` flows from the HTTP request to the outbound call; client disconnects cancel the Deezer call and are not reported as a provider failure.
- Deezer's quota error (HTTP 200 with `error.code = 4`) is recognised and reported as a failed provider, so callers can back off.
- Failures are logged with status code / error code, without logging the full payload.
- `HttpClientFactory` handler pooling (no socket exhaustion).

Not implemented, and what production would add:

- **Provider-specific limits**: Deezer allows roughly 50 requests per 5 seconds per IP; Spotify uses a rolling 30-second window and returns 429 with `Retry-After`. The standard pipeline retries 429, but it does not parse `Retry-After`; a Spotify adapter would add that.
- **Caching**: search results for identical `(provider, artist, album)` queries are safe to cache for minutes; a memory cache in-process, or a distributed cache if the service scales out. Cover URLs are stable and are stored with the saved album already, so retrieving a library never calls the provider.
- **Metrics and alerting**: per-provider request count, latency histogram, error rate and 429 count; alert on error-rate and circuit-open events. OpenTelemetry traces through the outbound `HttpClient` give per-request visibility.

## Testing strategy

There is no separate QA step for this service, so the tests are the safety net and were chosen for behaviour rather than coverage.

Unit tests (`Linkfire.MusicLibrary.UnitTests`, 40 tests):

- **Domain** (`LibraryTests`): user creation always creates one library; blank names are rejected; adding stores provider-neutral data; the same `(provider, providerAlbumId)` is not added twice (case-insensitive provider); the same id from another provider is a different album; remove reports unknown ids. These run with no I/O.
- **Application** (`LibraryServiceTests`): the services against a real EF Core model on in-memory SQLite. Covers persistence of user + library, adding several albums, skipping duplicates across and within requests, removal, `ErrorKind.NotFound` for unknown users/albums, paged retrieval (order by `AddedAt`, total count), and the concurrent-insert race surfacing as `ErrorKind.Conflict` (simulated with the `SavingChanges` hook). Using real SQLite instead of the EF in-memory provider means the mapping, unique indexes and change tracking are actually exercised.
- **Composite** (`CompositeMusicCatalogProviderTests`): merged results, a single failing provider degrading the search, every provider failing (`IsUnavailable`), a throwing provider isolated from the others, and caller cancellation not swallowed.
- **Infrastructure** (`DeezerMusicCatalogProviderTests`): the adapter with a stubbed `HttpMessageHandler`. Covers mapping (including cover fallback and id-to-string), the outgoing URL and limit, empty results, skipping unusable entries, non-2xx, network errors, timeout, malformed JSON, Deezer's error payload, and that caller cancellation is not reported as a provider failure.
- **Resilience** (`DeezerResiliencePipelineTests`): the client as the application registers it. A 503 is retried until a 200; retries stop at `MaxRetries` and the provider is then reported failed; a 400 is not retried.
- `AlbumSearchQueryTests`: the "at least one criterion" rule and trimming.

API tests (`Linkfire.MusicLibrary.ApiTests`, 21 tests) boot the real application with `WebApplicationFactory<Program>`, swapping in an in-memory SQLite connection and a `FakeMusicCatalogProvider`:

- `LibraryFlowTests`: create user (201 + Location) -> search (albums plus `unavailableProviders`) -> add two albums -> read library (page metadata) -> re-add (skipped) -> delete (204) -> read again -> delete again (404); libraries are isolated between users; paging returns the right slice and `totalCount`.
- `ErrorHandlingTests`: 404 ProblemDetails for unknown user/album, non-GUID ids and unknown routes; 400 for blank name, empty album list, missing provider identifiers, malformed JSON, empty search, over-long search terms and invalid `page`/`pageSize`; 503 ProblemDetails when the provider is unavailable, asserting that no exception text leaks.

Live Deezer is not needed for any automated test: the provider is a boundary, so its behaviour is simulated on both sides of it (fake provider above, stubbed HTTP below). The real integration is exercised manually.

## Trade-offs

- **SQLite instead of SQL Server/PostgreSQL**: zero setup for the reviewer and one file to delete to reset. EF Core hides the difference; switching is a provider package and a connection string. Not a statement about production databases. SQLite also cannot `ORDER BY` a `DateTimeOffset`, so `AddedAt` is a UTC `DateTime`.
- **Deezer instead of Spotify**: no app registration, no secrets, so the reviewer can run everything immediately. The provider boundary is the same either way.
- **Docker Compose instead of Kubernetes**: one service with one file-based dependency does not justify manifests, and Compose is what a reviewer is most likely to have installed. The migrate-then-start split is the same step a Kubernetes Job would run before the Deployment.
- **No authentication**: explicitly optional in the brief. Production would need it (see below).
- **No search caching**: the resilience pipeline is in place; a cache without a measured hit rate would be guesswork.
- **No repository layer, no CQRS/mediator**: `LibraryService` and `UserService` are the use cases; EF Core already is the repository and unit of work.
- **`Result<T>` instead of exceptions for expected failures**: not-found and conflict are normal outcomes, and a result makes every caller handle them. The cost is a small amount of mapping in each action. Unexpected exceptions still become a generic 500 through `UseExceptionHandler`.
- **Composite over every provider, with no selector**: one search hits every catalogue. That is the right default for "find this album". A `?provider=` filter is a small addition when a client needs it.
- **Album data is copied from the search result into the add request** instead of being re-fetched from the provider by id. This makes adding independent of provider availability and rate limits and keeps the request cheap. The cost is trusting the client's copy of the metadata; with authentication in place, re-validating against the provider (or accepting only `(provider, id)` and fetching server-side) is a reasonable tightening.
- **Swagger UI is always on**: there is nothing to protect yet. It would be limited to non-production environments in a real deployment.
- **Deezer's fielded search syntax is not used**: `search/album?q=artist:"..." album:"..."` returns no results for combined filters (verified during development), so both terms are sent as free text, which ranks the intended album first.
- **Startup migrations only in Development**: several replicas migrating on boot lock the database. Production runs the migration bundle as its own step. The cost is one extra container in Compose.

## Production improvements

In the order I would actually do them:

1. **Authentication and authorization**: users may only read or change their own library. Without this the service is a demo.
2. **A real database**: PostgreSQL (or SQL Server). The migration bundle already runs as a separate step; point `--connection` at that database.
3. **Observability**: OpenTelemetry traces and metrics, request/provider latency and error dashboards, health endpoints (`/health/live`, `/health/ready` including a provider probe).
4. **Search caching** for repeated queries, to cut provider calls and rate-limit exposure.
5. **Spotify**, registered as another leaf. The composite, controllers and library already accept it.
6. **`Retry-After`** on the Spotify client, and paging of search results (`index`/`limit`) once result sets grow. Library retrieval is already paged.

## AI usage

AI-assisted development tools were used as a productivity aid for drafting boilerplate, tests and this document. Architecture and design decisions, code review, running the tests, manual verification against the live Deezer API and the final content of the repository were done and validated by me.
