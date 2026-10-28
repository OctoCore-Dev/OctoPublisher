# OctoPublisher

Universal multi-store deployment automation engine for OctoCore applications.

## Architecture

- **`Octo.Common`**: Shared models, result contracts, ANSI terminal formatting (Fluent terminal aesthetics, no emojis).
- **`Octo.MStore`**: Microsoft Store Dev Center Ingestion API client, Azure Blob chunked uploader, metadata and release notes manager.
- **`Octo.PlayStore`**: Google Play Developer API publisher (AAB/APK deployments and track assignment).
- **`OctoPublisher.Cli` (`octopublish`)**: Single executable CLI tool for CI/CD and local automated publishing.

## CLI Usage

### Microsoft Store (`mstore`)

#### Publish Package
```bash
octopublish mstore publish \
  --package "./bin/Release/App_1.0.0.0_x64.msix" \
  --app-id "9NZXP5WR803J" \
  --tenant-id "$STORE_TENANT_ID" \
  --client-id "$STORE_CLIENT_ID" \
  --client-secret "$STORE_CLIENT_SECRET" \
  --notes "./packaging/whatsnew/whatsnew-ru-RU"
```

Or pass localized directories:
```bash
octopublish mstore publish \
  --package "./App.msix" \
  --notes-dir "./packaging/whatsnew" \
  --desc-dir "./packaging/store"
```

#### Inspect Application Status
```bash
octopublish mstore status --app-id "9NZXP5WR803J"
```

#### Delete Stale Draft
```bash
octopublish mstore delete-draft --app-id "9NZXP5WR803J"
```

## GitHub Actions Integration

```yaml
- name: Publish to Microsoft Store via OctoPublisher
  env:
    STORE_TENANT_ID: ${{ secrets.STORE_TENANT_ID }}
    STORE_CLIENT_ID: ${{ secrets.STORE_CLIENT_ID }}
    STORE_CLIENT_SECRET: ${{ secrets.STORE_CLIENT_SECRET }}
    STORE_APP_ID: ${{ secrets.STORE_APP_ID }}
  run: |
    dotnet run --project tools/OctoPublisher/src/OctoPublisher.Cli/OctoPublisher.Cli.csproj -- \
      mstore publish \
      --package "${{ steps.find-pkg.outputs.pkg_path }}" \
      --notes-dir "./packaging/whatsnew" \
      --desc-dir "./packaging/store"
```
