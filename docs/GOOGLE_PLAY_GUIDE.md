# Руководство: Публикация в Google Play Store

Руководство по настройке сервисного аккаунта Google Cloud и автоматизации публикации в Google Play Developer Console.

---

## 1. Создание сервисного аккаунта Google Cloud

1. Перейдите в [Google Cloud Console](https://console.cloud.google.com/).
2. Выберите проект, связанный с вашей учетной записью Google Play Console.
3. Перейдите в **IAM & Admin -> Service Accounts** и нажмите **Create Service Account**.
4. Задайте имя (например, `octopublisher-deployer`).
5. В разделе **Keys** добавьте новый ключ формата **JSON** и сохраните его.
6. В [Google Play Console](https://play.google.com/console/) перейдите в **API access**, найдите созданный сервисный аккаунт и предоставьте ему права **Releases -> Manage production releases, Manage testing track releases**.

---

## 2. Команды публикации

### Публикация AAB бандла:
```bash
octopublish playstore publish \
  --bundle "./bin/Release/App.aab" \
  --package-name "com.example.app" \
  --service-account "./google-service-account.json" \
  --track "internal" \
  --notes-dir "./packaging/whatsnew" \
  --desc-dir "./packaging/store/android"
```

### Доступные треки:
- `internal` (внутреннее тестирование)
- `alpha` (закрытое альфа-тестирование)
- `beta` (открытое бета-тестирование)
- `production` (боевой релиз для всех пользователей)
