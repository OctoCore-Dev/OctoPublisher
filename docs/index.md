# OctoPublisher Wiki

Welcome to the official documentation and wiki for **OctoPublisher**, the universal high-performance deployment engine designed by **OctoCore** for modern application stores.

---

## Table of Contents

1. [Getting Started](GETTING_STARTED.md)
   - Installation via `dotnet tool`
   - Standalone native binaries (Windows, Linux, macOS)
   - Building from source
2. [CLI Reference](CLI_REFERENCE.md)
   - Global flags
   - Microsoft Store (`mstore`) commands: `publish`, `status`, `delete-draft`, `configure`
   - Google Play Store (`playstore`) commands
3. [Microsoft Store Setup Guide](MICROSOFT_STORE_GUIDE.md)
   - Creating Azure AD Application Registration
   - Granting Partner Center permissions
   - Finding your Store Product ID, Tenant ID, and Client Secret
4. [GitHub Actions & CI/CD Guide](GITHUB_ACTIONS_GUIDE.md)
   - Secrets configuration
   - End-to-end pipeline examples
   - Safe concurrent build protection
5. [Architecture & Engine Deep Dive](ARCHITECTURE.md)
   - Dev Center Ingestion REST API state machine
   - Azure Blob SAS chunked uploader
   - Zero-dependency .NET 10 core design
6. [Google Play Store Architecture (Roadmap)](GOOGLE_PLAY_GUIDE.md)
   - Service account setup
   - AAB / APK artifact delivery
