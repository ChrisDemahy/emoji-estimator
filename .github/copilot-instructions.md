# Copilot instructions for EmojiEstimator

## Build and test commands

Run these from the repository root unless noted otherwise.

- Build the solution: `dotnet build EmojiEstimator.slnx`
- Run the full test suite: `dotnet test EmojiEstimator.slnx`
- Run a single xUnit test: `dotnet test tests\EmojiEstimator.UnitTests\EmojiEstimator.UnitTests.csproj --no-build --filter "FullyQualifiedName~EmojiEstimator.UnitTests.RepositoryScannerTests.ScanAsync_AggregatesPullRequestsAndPersistsCompletedResult"`
- Run a single Playwright journey: `dotnet test tests\EmojiEstimator.PlaywrightTests\EmojiEstimator.PlaywrightTests.csproj --no-build --filter "FullyQualifiedName~EmojiEstimator.PlaywrightTests.RepositoryJourneysTests.HomePage_RendersExpectedProductMessaging"`
- Build Tailwind CSS from `src\EmojiEstimator.Web`: `npm run build:css`
- Watch Tailwind CSS from `src\EmojiEstimator.Web`: `npm run watch:css`
- Run the web app: `dotnet run --project src\EmojiEstimator.Web`

`Program.cs` validates `GitHub:Token` on startup, so real app runs need a token in config or user secrets. The Playwright test host replaces GitHub access with fakes.

## High-level architecture

- `src\EmojiEstimator.Web` is a single ASP.NET Core MVC app centered on the route `/{username}/{repository}`.
- `Program.cs` registers EF Core SQLite persistence, GitHub access services, scan coordination/background processing, Server-Sent Events, and applies migrations on startup.
- `RepositoryController` does not perform scans directly. It asks `IRepositoryScanCoordinator` for the current state and renders the repository page with both a server-rendered `RepositoryScanProgressUpdate` and serialized JSON used to bootstrap the client.
- Scan execution is asynchronous. `RepositoryScanCoordinator` deduplicates active requests, saves/publishes a pending state, and queues a `RepositoryScanWorkItem`. `RepositoryScanBackgroundService` dequeues work and calls `IRepositoryScanner`.
- `RepositoryScanner` is the main pipeline: reuse a fresh cached scan when possible; otherwise mark the scan as running, read pull requests and issues through `IGitHubContentReader` (backed by `OctokitGitHubPullRequestPageSource` and `OctokitGitHubIssuePageSource`), aggregate emoji and em-dash counts with `RepositoryScanAggregator`, `UnicodeEmojiCounter`, and `CanonicalEmDashCounter`, then persist completed or failed results through `IRepositoryScanStore`.
- Live progress is a first-class part of the design. `ServerSentEventRepositoryScanProgressNotifier` stores and streams the latest `RepositoryScanProgressUpdate`, `RepositoryScanUpdatesController` exposes the `/{username}/{repository}/live-updates` SSE endpoint, and `wwwroot\js\repository-page.js` uses the browser `EventSource` API to consume those events.
- Persistence is one `RepositoryScan` row per normalized repository key in SQLite. Completed results stay fresh for 24 hours, and stale rows are deleted before the next scan is queued.

## Key conventions

- Always derive repository identity with `RepositoryScan.CreateNormalizedKey(owner, repository)` when deduplicating scans, persisting repository state, or keying SSE subscriptions. The app preserves trimmed display casing separately from the uppercase normalized key.
- Keep the live-update contract synchronized across server and client. If you change scan statuses or payload fields, update `RepositoryScanProgressUpdate`, the Razor helper logic in `Views\Repository\Index.cshtml`, and the mirrored state-building logic in `wwwroot\js\repository-page.js`.
- Keep controllers thin. New scan behavior should flow through the coordinator/queue/scanner pipeline rather than making synchronous GitHub calls from MVC actions.
- `dotnet build` can trigger `npm run build:css` through the `BuildTailwindCss` MSBuild target when `src\EmojiEstimator.Web\node_modules` exists. Tailwind input is `Styles\app.css`, output is `wwwroot\css\app.css`, and content scanning covers Razor views plus `wwwroot\js`.
- The emoji counting logic works on text elements/grapheme clusters, not raw code points. Reuse `UnicodeEmojiCounter` behavior for composite emoji, flags, keycaps, and variation-selector cases instead of introducing ad hoc regex counting. Em-dash counting is handled by `CanonicalEmDashCounter`; always use `IEmDashCounter` rather than ad hoc character matching.
- Tests are intentionally isolated from real external services. Unit tests use xUnit. Playwright tests use NUnit plus `PlaywrightWebApplicationFactory`, which switches the app to the `Testing` environment and replaces `IGitHubContentReader` and `TimeProvider` with test doubles.
- C# style is driven by `.editorconfig`: file-scoped namespaces, primary-constructor dependency injection, explicit local types over `var`, and predominantly sealed types.
