# Microsoft Store Setup Guide

This guide walks you through setting up automated submissions to the Microsoft Store via OctoPublisher.

---

## 1. Prerequisites

To publish to the Microsoft Store programmatically, you need:
- An active Microsoft Partner Center developer account.
- An Azure Active Directory (Azure AD / Entra ID) tenant linked to Partner Center.
- An application registered in Azure AD with permissions in Partner Center.

---

## 2. Azure Active Directory Application Setup

### Step 2.1: Register Azure AD App
1. Go to [Azure Portal](https://portal.azure.com/) -> **Azure Active Directory** (Microsoft Entra ID).
2. Navigate to **App registrations** -> **New registration**.
3. Name: `OctoPublisher-CI` (or your company preference).
4. Supported account types: **Accounts in this organizational directory only (Single tenant)**.
5. Redirect URI: Leave blank.
6. Click **Register**.

### Step 2.2: Note Identifiers
From the app overview page, copy:
- **Application (client) ID** -> This is your `STORE_CLIENT_ID`.
- **Directory (tenant) ID** -> This is your `STORE_TENANT_ID`.

### Step 2.3: Generate Client Secret
1. In the app registration, select **Certificates & secrets** -> **Client secrets** -> **New client secret**.
2. Description: `OctoPublisher CI Key`.
3. Expires: Choose the duration required by your security policy (e.g. 12 or 24 months).
4. Click **Add** and immediately copy the **Value** -> This is your `STORE_CLIENT_SECRET`.

---

## 3. Link Azure AD App to Partner Center

1. Sign in to [Microsoft Partner Center](https://partner.microsoft.com/dashboard).
2. Click the gear icon in the top right -> **Account settings**.
3. Under **Organization profile**, click **User management** (or **Tenant management**).
4. Select the **Azure AD applications** tab.
5. Click **Add Azure AD Application** -> select the application registered in Step 2 (`OctoPublisher-CI`).
6. Assign the role: **Manager** (grants permission to create, update, and commit submissions).
7. Save changes.

---

## 4. Obtain Your Application Store ID

1. In Microsoft Partner Center, navigate to **Apps and games** -> select your app.
2. In the left navigation menu, expand **Product management** -> **Product Identity**.
3. Copy the **Store ID** (e.g. `9NZXP5WR803J`) -> This is your `STORE_APP_ID`.

---

## 5. Local Usage

Run interactive configuration or set environment variables:

```bash
octopublish mstore configure
```

Or configure via environment variables:

```powershell
$env:STORE_TENANT_ID="00000000-0000-0000-0000-000000000000"
$env:STORE_CLIENT_ID="11111111-1111-1111-1111-111111111111"
$env:STORE_CLIENT_SECRET="secret_value"
$env:STORE_APP_ID="9NZXP5WR803J"
```

Verify connection:

```bash
octopublish mstore status
```

---

## 6. How OctoPublisher Protects Submissions

Official CLI tools frequently fail when a submission is already undergoing certification or publication. OctoPublisher inspects the current state:

- If a submission is in `PendingCommit` (draft): OctoPublisher automatically deletes the stale draft and provisions a fresh submission.
- If a submission is in `Certification` or `Release`: OctoPublisher logs the active status and exits gracefully (code 0) to prevent broken CI pipelines.
- When uploading packages: OctoPublisher streams 8 MB blocks directly to Azure Storage SAS without writing temp archives to disk.
