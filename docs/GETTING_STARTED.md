# Быстрый старт с OctoPublisher

Инструмент **OctoPublisher** (`octopublish`) можно использовать тремя способами: как глобальный инструмент .NET CLI, как автономный исполняемый файл или напрямую в пайплайнах CI/CD.

---

## 1. Установка как глобальный инструмент .NET CLI

Требуется [.NET 10 SDK](https://dotnet.microsoft.com/download):

```bash
# Установка глобально из NuGet или локального пакета
dotnet tool install -g OctoPublisher

# Проверка версии и готовности
octopublish --version
```

Для обновления до актуальной версии:
```bash
dotnet tool update -g OctoPublisher
```

---

## 2. Инициализация структуры проекта ("Store as Code")

Чтобы начать управлять метаданными сторов в вашем репозитории:

```bash
# Вариант А: Создать чистую структуру с русскими шаблонами и PNG
octopublish init all --dir packaging

# Вариант Б: Выгрузить реальные ассеты из уже опубликованного приложения
octopublish pull windows 9NZXP5WR803J --dir packaging
octopublish pull android com.example.app --service-account ./play-key.json --dir packaging

# Проверить список созданных или скачанных ассетов
octopublish list --dir packaging

# Добавить новый язык (например, английский или французский)
octopublish add all en --dir packaging
octopublish add android fr --dir packaging
```

---

## 3. Предполётная проверка перед релизом

Перед коммитом в Git или запуском публикации выполните проверку 5-уровневым линтером:

```bash
octopublish test --dir packaging
```
Линтер гарантирует, что вы не забыли шаблонные заглушки, не превысили лимиты длины названий и описаний и подготовили изображения правильного разрешения.

---

## 4. Локальная настройка авторизации Microsoft Store

Чтобы не передавать ключи авторизации через флаги при каждом запуске, настройте профиль один раз:

```bash
octopublish mstore configure
```

Мастер интерактивно запросит:
- Azure AD Tenant ID
- Azure AD Client ID
- Azure AD Client Secret
- Microsoft Store App ID (Product ID)
- Partner Center Seller ID (необязательно)

Данные сохраняются в защищенном пользовательском файле `~/.octo/mstore.json` (или в локальном `.octopublish.json` при флаге `--local`).

---

## 5. Первый деплой в магазины

После настройки публикация выполняется одной командой:

```bash
# Публикация в Microsoft Store (Windows MSIX):
octopublish mstore publish --package "./App_1.0.0.0_x64.msix"

# Публикация в Google Play Store (Android AAB):
octopublish playstore publish \
  --bundle "./App.aab" \
  --package-name "com.example.app" \
  --service-account "service_account.json"
```
