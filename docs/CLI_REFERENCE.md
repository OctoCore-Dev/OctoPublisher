# OctoPublisher CLI Reference

Complete reference for all CLI commands, arguments, and environment variables.

---

## Global Options

| Option | Short | Description |
| :--- | :--- | :--- |
| `--help` | `-h` | Display help and usage instructions |
| `--version` | `-v` | Display version and runtime info |

---

## Microsoft Store Module (`mstore`)

### 1. `octopublish mstore publish`

Uploads an application package to Microsoft Store, updates metadata, and commits the submission for certification.

#### Flags:
- `--package <path>` **(Required)**: Absolute or relative path to `.msix`, `.msixupload`, `.msixbundle`, or `.zip` file.
- `--app-id <id>`: Store Product ID (e.g. `9NZXP5WR803J`). Fallback: `STORE_APP_ID`.
- `--tenant-id <id>`: Azure AD Directory (Tenant) ID. Fallback: `STORE_TENANT_ID`.
- `--client-id <id>`: Azure AD Application (Client) ID. Fallback: `STORE_CLIENT_ID`.
- `--client-secret <sec>`: Azure AD Client Secret. Fallback: `STORE_CLIENT_SECRET`.
- `--seller-id <id>`: Partner Center Seller ID (e.g. `94042650`). Fallback: `SELLER_ID`.
- `--notes <path>`: Path to a plain text file containing release notes to apply to all listings.
- `--notes-dir <path>`: Directory containing localized release notes files (e.g. `whatsnew-ru-RU`, `whatsnew-en-US`).
- `--desc <path>`: Path to a plain text file containing full store description.
- `--desc-dir <path>`: Directory containing localized full descriptions.

#### Example:
```bash
octopublish mstore publish \
  --package "./bin/Release/App_1.0.0.0_x64.msix" \
  --notes-dir "./packaging/whatsnew" \
  --desc-dir "./packaging/store"
```

---

### 2. `octopublish mstore status`

Queries the current live application status in Partner Center, displaying:
- Application primary name
- App ID & Package Family Name
- Last published submission ID
- Active pending submission ID & its current status (`Release`, `Certification`, `PendingCommit`, etc.)

#### Flags:
- `--app-id <id>`: Target Store Product ID.

#### Example:
```bash
octopublish mstore status --app-id "9NZXP5WR803J"
```

---

### 3. `octopublish mstore delete-draft`

Safely deletes an active uncommitted draft submission if it was created in error or has an expired Azure SAS upload URL.

> Note: If the submission is already in `Certification` or `Release`, Microsoft prevents deletion. OctoPublisher will report this clearly without crashing.

#### Flags:
- `--app-id <id>`: Target Store Product ID.

---

### 4. `octopublish mstore configure`

Interactively sets up credentials and saves them to a configuration file.

#### Flags:
- `--local`: Saves credentials to `./.octopublish.json` in the current working directory instead of user home (`~/.octo/mstore.json`).
- You can also pass flags directly (`--tenant-id`, `--client-id`, etc.) to run non-interactively.
