# Getting Started with OctoPublisher

OctoPublisher can be used in three ways: as a global .NET CLI tool, as a standalone native single-file executable, or directly integrated into your CI/CD pipeline.

---

## 1. Global .NET CLI Tool (Recommended)

Requires [.NET 10 SDK](https://dotnet.microsoft.com/download):

```bash
# Install globally from NuGet or local package
dotnet tool install -g OctoPublisher

# Verify installation
octopublish --version
```

To update:
```bash
dotnet tool update -g OctoPublisher
```

---

## 2. Standalone Native Executables (Zero Prerequisites)

If you don't have .NET installed on your machine or CI runner, download pre-compiled standalone binaries from [GitHub Releases](https://github.com/OctoCore-Dev/OctoPublisher/releases):

- **Windows (x64)**: `octopublish-win-x64.exe`
- **Linux (x64)**: `octopublish-linux-x64`
- **macOS (Apple Silicon arm64)**: `octopublish-osx-arm64`
- **macOS (Intel x64)**: `octopublish-osx-x64`

Make the binary executable on Unix:
```bash
chmod +x octopublish-linux-x64
./octopublish-linux-x64 --version
```

---

## 3. Quick Local Configuration

To avoid passing API secrets in command line arguments on every run, configure OctoPublisher once:

```bash
octopublish mstore configure
```

This will interactively prompt for:
- Azure AD Tenant ID
- Azure AD Client ID
- Azure AD Client Secret
- Microsoft Store App ID (Product ID)
- Partner Center Seller ID (optional)

Credentials are saved securely in `~/.octo/mstore.json` (or `.octopublish.json` if using the `--local` flag).

---

## 4. First Publish Command

Once configured, simply point OctoPublisher to your built package:

```bash
octopublish mstore publish --package "./App_1.0.0.0_x64.msix"
```
