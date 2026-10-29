# Справочник команд OctoPublisher CLI

Полный справочник команд, флагов и параметров командной строки `octopublish`.

---

## Глобальные параметры

| Флаг | Описание |
| :--- | :--- |
| `-h`, `--help` | Вывести подробную справочную информацию |
| `-v`, `--version` | Вывести установленную версию программы |

---

## 1. Команда `init` (Сборка структуры "Store as Code")

```bash
octopublish init [all|android|windows] [--dir <каталог>] [--lang <локаль>]
```

- `all` *(по умолчанию)*: Создает структуру каталогов для Android и Windows.
- `android`: Создает структуру только для Google Play Store.
- `windows`: Создает структуру только для Microsoft Store.
- `--dir <каталог>`: Целевая папка (по умолчанию: `packaging`).
- `--lang <локаль>`: Основной язык приложения (по умолчанию: `ru-RU`).

---

## 2. Команда `pull` (Экспорт ассетов из сторов)

Выгружает опубликованные описания, иконки, баннеры и скриншоты напрямую из Google Play и Microsoft Store.

```bash
octopublish pull [all|android|windows] [ID] [параметры]
```

- `windows <Product ID>`: Выгрузка из Microsoft Store (по Product ID без авторизации или через Partner Center API).
- `android <Package Name>`: Выгрузка из Google Play Developer API (требует сервисный аккаунт).
- `--dir <каталог>`: Папка сохранения (по умолчанию: `packaging`).
- `--app-id <id>`: Product ID приложения Microsoft Store.
- `--package-name <id>`: Идентификатор пакета Google Play.
- `--service-account <путь>`: Ключ сервисного аккаунта Google Play (JSON).
- `--lang <локаль>`: Конкретный язык для выгрузки (по умолчанию: все доступные).
- `--market <рынок>`: Региональный рынок каталога Microsoft Store (по умолчанию: RU, US).

---

## 3. Команда `add` (Добавление языкового пакета)

```bash
octopublish add [all|android|windows] <локаль> [--dir <каталог>]
```

Поддерживает автоматическое сопоставление коротких кодов:
- `ru` -> `ru-RU`
- `en`, `us` -> `en-US`
- `fr` -> `fr-FR`
- `de` -> `de-DE`
- `es` -> `es-ES`
- `zh`, `cn` -> `zh-CN`
- `ja`, `jp` -> `ja-JP`
- `it` -> `it-IT`
- `pt`, `br` -> `pt-BR`
- `uk`, `ua` -> `uk-UA`
- `kk`, `kz` -> `kk-KZ`
- `tr` -> `tr-TR`
- `ar`, `sa` -> `ar-SA`

---

## 4. Команда `test` (Предполётный линтер)

```bash
octopublish test [--dir <каталог>] [--strict]
```

- `--dir <каталог>`: Папка с метаданными (по умолчанию: `packaging`).
- `--strict`: Строгий режим: завершает процесс с кодом `1` при наличии любых предупреждений.

---

## 5. Команда `list` (Сводная инспекция)

```bash
octopublish list [--dir <каталог>]
```

Выводит ANSI-таблицу статуса готовности обязательных текстовых файлов, разрешений иконок и скриншотов.

---

## 6. Команда `sync` (Синхронизация метаданных)

```bash
octopublish sync [all|android|windows] [--dir <каталог>] [--dry-run]
```

- `--dry-run`: Выполнить валидацию и сформировать diff без фактической отправки в магазины.

---

## 7. Модуль `playstore` (Google Play Store)

### `playstore publish`
```bash
octopublish playstore publish \
  --bundle <путь к .aab/.apk> \
  --package-name <id приложения> \
  --service-account <путь к JSON или содержимое> \
  [--track <internal|alpha|beta|production>] \
  [--notes-dir <папка whatsnew>] \
  [--desc-dir <папка store/android>] \
  [--icon <путь к иконке>] \
  [--release-name <название версии>]
```

---

## 8. Модуль `mstore` (Microsoft Store)

### `mstore publish`
```bash
octopublish mstore publish \
  --package <путь к .msix/.zip> \
  [--app-id <Product ID>] \
  [--tenant-id <Azure Tenant ID>] \
  [--client-id <Azure Client ID>] \
  [--client-secret <Azure Secret>] \
  [--notes-dir <папка whatsnew>] \
  [--desc-dir <папка store/windows>]
```

### `mstore status`
```bash
octopublish mstore status [--app-id <Product ID>]
```

### `mstore delete-draft`
```bash
octopublish mstore delete-draft [--app-id <Product ID>]
```

### `mstore configure`
```bash
octopublish mstore configure [--local]
```
