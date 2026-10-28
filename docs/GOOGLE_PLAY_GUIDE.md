# Google Play Store Publishing Guide

This document outlines the architecture and deployment workflow for publishing Android application bundles (`.aab` and `.apk`) using `Octo.PlayStore`.

---

## 1. Prerequisites

To automate Google Play Store deployments:
- A Google Play Developer Console account.
- A Google Cloud Platform (GCP) project linked to the Google Play Developer account.
- A Service Account with **Release manager** permissions in Google Play Console.
- Downloaded Service Account JSON key (`service_account.json`).

---

## 2. Setting Up Service Account Authentication

1. Go to [Google Cloud Console](https://console.cloud.google.com/).
2. Select your Google Play linked project.
3. Navigate to **IAM & Admin** -> **Service Accounts** -> **Create Service Account**.
4. Name: `octopublisher-ci`.
5. Grant role: **Service Account User**.
6. Generate a JSON Key:
   - Click the created service account -> **Keys** -> **Add Key** -> **Create new key** -> **JSON**.
   - Save the file securely.
7. Grant access in Google Play Console:
   - Go to [Google Play Console](https://play.google.com/console).
   - Navigate to **Users and permissions** -> **Invite new user**.
   - Enter the service account email.
   - Under **App permissions**, select the target application.
   - Under **Releases**, check **Create, edit, and roll out releases**.

---

## 3. CLI Command Reference (Coming in v1.1)

```bash
octopublish play publish \
  --package "app-release.aab" \
  --package-name "com.octocore.app" \
  --service-account "service_account.json" \
  --track "production" \
  --rollout "0.2" \
  --notes "General stability improvements."
```

### Options:
- `--package <path>`: Path to the `.aab` (Android App Bundle) or `.apk` file.
- `--package-name <id>`: Android package identifier (e.g. `com.company.app`).
- `--service-account <path>`: Path to the GCP service account JSON key file (or base64 ENV `PLAY_SERVICE_ACCOUNT_JSON`).
- `--track <track>`: Deployment track: `internal`, `alpha`, `beta`, `production`.
- `--rollout <fraction>`: Rollout fraction between `0.05` (5%) and `1.0` (100%).
- `--notes <text>` or `--notes-dir <dir>`: Localized changelog directory (`whatsnew/whatsnew-en-US.txt`).
