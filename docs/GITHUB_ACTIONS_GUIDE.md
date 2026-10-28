# GitHub Actions CI/CD Integration Guide

OctoPublisher is designed from the ground up for unattended, headless CI/CD environments. It never prompts for interactive browser authentication and natively honors GitHub Secrets.

---

## 1. Configure GitHub Secrets

In your GitHub repository, navigate to **Settings** -> **Secrets and variables** -> **Actions** -> **New repository secret**.

Add the following secrets:

| Secret Name | Description | Example |
|-------------|-------------|---------|
| `STORE_TENANT_ID` | Azure AD Directory (tenant) ID | `00000000-0000-0000-0000-000000000000` |
| `STORE_CLIENT_ID` | Azure AD Application (client) ID | `11111111-1111-1111-1111-111111111111` |
| `STORE_CLIENT_SECRET` | Azure AD Application Client Secret | `~XyZ123...` |
| `STORE_APP_ID` | Microsoft Partner Center Store ID | `9NZXP5WR803J` |

---

## 2. GitHub Actions Workflow Examples

### Option A: Install via .NET Tool

This is the standard approach for repositories with .NET SDK available in the runner.

```yaml
name: Deploy Windows MSIX to Microsoft Store

on:
  push:
    tags:
      - 'v*'

jobs:
  publish:
    runs-on: windows-latest
    steps:
      - name: Checkout repository
        uses: actions/checkout@v4

      - name: Setup .NET SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Install OctoPublisher Tool
        run: dotnet tool install -g OctoPublisher

      - name: Build MSIX Package
        run: |
          dotnet publish MyApp.sln -c Release /p:GenerateAppInstallerFile=false /p:AppxPackageSigningEnabled=true

      - name: Publish to Microsoft Store
        env:
          STORE_TENANT_ID: ${{ secrets.STORE_TENANT_ID }}
          STORE_CLIENT_ID: ${{ secrets.STORE_CLIENT_ID }}
          STORE_CLIENT_SECRET: ${{ secrets.STORE_CLIENT_SECRET }}
          STORE_APP_ID: ${{ secrets.STORE_APP_ID }}
        run: |
          octopublish mstore publish `
            --package "bin\Release\MyApp.msix" `
            --notes-dir "distribution\whatsnew"
```

---

### Option B: Download Standalone Binary (Zero Prerequisites)

If your runner does not have the .NET SDK or you want minimal pipeline setup:

```yaml
name: Deploy via Standalone Binary

on:
  workflow_dispatch:

jobs:
  deploy:
    runs-on: windows-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Download OctoPublisher CLI
        run: |
          Invoke-WebRequest -Uri "https://github.com/OctoCore-Dev/OctoPublisher/releases/latest/download/octopublish-win-x64.exe" -OutFile "octopublish.exe"

      - name: Publish MSIX Package
        env:
          STORE_TENANT_ID: ${{ secrets.STORE_TENANT_ID }}
          STORE_CLIENT_ID: ${{ secrets.STORE_CLIENT_ID }}
          STORE_CLIENT_SECRET: ${{ secrets.STORE_CLIENT_SECRET }}
          STORE_APP_ID: ${{ secrets.STORE_APP_ID }}
        run: |
          .\octopublish.exe mstore publish `
            --package "artifacts\MyApp.msix" `
            --release-notes "Stability updates and performance improvements."
```

---

## 3. Localized Release Notes Directory

When passing `--notes-dir <directory>`, OctoPublisher automatically scans the folder for files named after BCP-47 language codes:

```
distribution/whatsnew/
|-- whatsnew-en-US.txt
|-- whatsnew-ru-RU.txt
|-- whatsnew-de-DE.txt
`-- whatsnew-zh-CN.txt
```

Each listing configured in Microsoft Partner Center matching the locale will have its `releaseNotes` field populated.

---

## 4. Exit Codes

OctoPublisher returns standard Unix/Windows exit codes for CI/CD status reporting:

| Code | Meaning | CI Behavior |
|------|---------|-------------|
| `0`  | Success (committed or active submission detected) | Step succeeds |
| `1`  | Missing parameters / invalid arguments | Step fails |
| `2`  | Authentication error (Azure AD token failure) | Step fails |
| `3`  | API network error or validation failure | Step fails |
