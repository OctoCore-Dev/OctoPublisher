# Руководство: Автоматизация в GitHub Actions

Пример настройки непрерывного развертывания приложений (CI/CD) в Google Play Store и Microsoft Store с предварительной валидацией через `octopublish test`.

---

## 1. Секреты репозитория (Repository Secrets)

В настройках репозитория GitHub (**Settings -> Secrets and variables -> Actions**) добавьте следующие переменные:

| Имя секрета | Описание |
| :--- | :--- |
| `PLAY_CONFIG_JSON` | Содержимое JSON-ключа сервисного аккаунта Google Play |
| `STORE_TENANT_ID` | Идентификатор Azure AD Tenant ID |
| `STORE_CLIENT_ID` | Идентификатор Azure AD Client ID |
| `STORE_CLIENT_SECRET` | Секрет Azure AD Client Secret |
| `STORE_APP_ID` | Идентификатор продукта в Microsoft Partner Center |

---

## 2. Пример рабочего процесса (Workflow)

```yaml
name: Релиз в сторы через OctoPublisher

on:
  push:
    tags:
      - 'v*'

jobs:
  validate:
    name: Предполётная валидация
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Установка .NET 10
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Установка OctoPublisher
        run: dotnet tool install -g OctoPublisher
      - name: Валидация Store as Code
        run: octopublish test --dir packaging

  publish-android:
    name: Публикация Google Play
    needs: validate
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Сборка AAB
        run: dotnet build -c Release -f net10.0-android
      - name: Деплой в Google Play
        env:
          PLAY_CONFIG_JSON: ${{ secrets.PLAY_CONFIG_JSON }}
        run: |
          octopublish playstore publish \
            --bundle "./bin/Release/net10.0-android/App.aab" \
            --package-name "com.example.app" \
            --track "production" \
            --notes-dir "./packaging/whatsnew" \
            --desc-dir "./packaging/store/android"

  publish-windows:
    name: Публикация Microsoft Store
    needs: validate
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - name: Сборка MSIX
        run: dotnet publish -c Release -f net10.0-windows10.0.19041.0
      - name: Деплой в Microsoft Store
        env:
          STORE_TENANT_ID: ${{ secrets.STORE_TENANT_ID }}
          STORE_CLIENT_ID: ${{ secrets.STORE_CLIENT_ID }}
          STORE_CLIENT_SECRET: ${{ secrets.STORE_CLIENT_SECRET }}
          STORE_APP_ID: ${{ secrets.STORE_APP_ID }}
        run: |
          octopublish mstore publish \
            --package "./bin/Release/App.msix" \
            --notes-dir "./packaging/whatsnew" \
            --desc-dir "./packaging/store/windows"
```
