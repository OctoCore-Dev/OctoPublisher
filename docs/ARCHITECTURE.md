# OctoPublisher Architecture

OctoPublisher is designed as a modular, high-performance publishing engine for digital app stores.

---

## 1. Solution Structure

```
OctoPublisher.slnx
|-- src/
|   |-- Octo.Common/               (Shared models, ANSI terminal renderer, contracts)
|   |-- Octo.MStore/               (Microsoft Store DevCenter API client, Azure Blob engine)
|   |-- Octo.PlayStore/            (Google Play Developer API client)
|   `-- OctoPublisher.Cli/         (Command-line interface and entry point)
|-- tests/
|   `-- OctoPublisher.Tests/       (xUnit unit and integration tests)
`-- docs/                          (Developer and user documentation)
```

---

## 2. Key Architectural Decisions

### 2.1 Zero External SDK Dependencies
Rather than pulling in heavy Microsoft Azure or Store SDKs that bring transitive dependency conflicts, OctoPublisher uses direct, high-performance HTTP REST implementations over `System.Net.Http.HttpClient` and `System.Text.Json`.

### 2.2 Direct Azure Blob Streaming
MSIX and APK packages can exceed hundreds of megabytes. OctoPublisher avoids loading files into contiguous memory:
- Files under 32 MB are uploaded in a single HTTP `PUT` request with block blob headers.
- Files 32 MB and larger are sliced into sequential 8 MB blocks (`Comp=block&blockid=...`) and finalized with a blocklist commit (`Comp=blocklist`).
- Progress is calculated on the stream and displayed via ASCII progress bars with transfer rate calculations.

### 2.3 Resilient Submission State Machine

Microsoft Partner Center enforces a strict submission lifecycle:

```
[No active submission]
          |
          v
   Create Submission (Draft)
          |
          v
   Upload Package & Update Metadata
          |
          v
   Commit Submission
          |
          v
   [In Certification]
          |
          v
   [In Publishing / Release]
          |
          v
   [Published]
```

#### Problem in Official Tools
Official tools like `msstore-cli` attempt to delete active submissions unconditionally. If an app is currently in `Certification` or `Release`, Dev Center rejects deletion with `409 Conflict`. Official tools crash with unhandled exceptions.

#### OctoPublisher Strategy
1. Queries the target application's pending submission endpoint.
2. If `status` is `PendingCommit`: The previous uncommitted draft is deleted and a fresh submission is generated.
3. If `status` is `CommitStarted`, `Certification`, or `Release`: OctoPublisher logs the active submission status and exits cleanly (code 0), preventing broken continuous deployment pipelines while safeguarding the submission in flight.

### 2.4 Decoupled Configuration Hierarchy
OctoPublisher resolves settings in order:
1. Explicit CLI arguments (`--app-id`, `--client-id`, etc.)
2. Environment variables (`STORE_APP_ID`, `STORE_CLIENT_ID`, etc.)
3. Local configuration file (`.octopublish.json`)
4. Global user configuration (`~/.octo/mstore.json`)

---

## 3. Terminal Renderer

The logging engine (`AnsiTerminal`) adheres to strict design constraints:
- **No Emojis**: Replaced entirely by clean, professional ASCII and Unicode engineering tags: `[+]`, `[*]`, `[!]`, `[x]`, `-->`, `|--`.
- **Microsoft Fluent Accent**: Uses Windows Accent Cyan (`\u001b[38;2;96;205;255m`) and muted slate grays.
- **Dynamic Width Compensation**: Strips ANSI escape codes when calculating terminal string lengths to prevent alignment distortion in ASCII boxes and tables.
