<div align="center">
  <img src="https://raw.githubusercontent.com/OctoCore-Dev/OctoPublisher/main/icon.svg" width="128" height="128" alt="Логотип OctoPublisher" />
  <h1>OctoPublisher</h1>
  <p>Универсальный высокопроизводительный движок автоматизации публикации приложений в Google Play Store и Microsoft Store с парадигмой <b>Store as Code</b>.</p>
</div>

---

## Архитектура движка

Движок разработан на платформе **.NET 10** с применением передовых стандартов Microsoft Fluent 2, строгой типизации и модульной структуры:

- **`Octo.Common`**: Базовые контракты результатов (`PublisherResult`), кроссплатформенный генератор растровых PNG без внешних зависимостей (`ImageTemplateGenerator`), реестр локалей и лимитов сторов (`LocaleRegistry`), терминальный вывод ANSI (`AnsiTerminal`).
- **`Octo.PlayStore`**: Клиент Google Play Developer API v3 (публикация AAB / APK, назначение треков релизов, управление локализованными описаниями, загрузка иконок и скриншотов с защитой Safe-Merge).
- **`Octo.MStore`**: Клиент Microsoft Partner Center Ingestion API (генерация Azure Blob SAS токенов, блочная загрузка MSIX / AppX пакетов, управление описаниями, фичами и скриншотами).
- **`OctoPublisher.Cli` (`octopublish`)**: Консольная утилита с русскоязычным интерфейсом, 5-уровневым предполётным линтером, генерацией шаблонов и поддержкой CI/CD.

---

## Концепция "Store as Code"

Все тексты, локализации, иконки, баннеры и скриншоты хранятся в репозитории Git в каталоге `packaging/` и синхронизируются с магазинами без необходимости ручной работы через веб-консоли.

### Структура директорий
```
packaging/
├── whatsnew/                                # Заметки о выпуске («Что нового» при обновлении)
│   ├── whatsnew-ru-RU.txt                   # Русский текст (до 500 символов)
│   └── whatsnew-en-US.txt                   # Английский текст (до 500 символов)
│
└── store/
    ├── android/                             # Метаданные и графика Google Play Store
    │   ├── ru-RU/                           # Русская локализация (Основной язык)
    │   │   ├── title.txt                    # Название приложения (строго <= 30 символов)
    │   │   ├── short_description.txt        # Краткое описание (строго <= 80 символов)
    │   │   ├── full_description.txt         # Полное описание (до 4000 символов)
    │   │   ├── privacy.txt                  # Ссылка на политику конфиденциальности
    │   │   ├── icon.png                     # Иконка приложения (512x512 PNG, 32-bit)
    │   │   ├── feature_graphic.png          # Промо-баннер (1024x500 PNG/JPEG)
    │   │   └── phoneScreenshots/            # Скриншоты телефона (от 2 до 8 штук, 16:9 / 9:16)
    │   │       ├── 01_home.png
    │   │       └── 02_settings.png
    │   │
    │   └── en-US/                           # Английская локализация
    │       ├── title.txt
    │       ├── short_description.txt
    │       ├── full_description.txt
    │       ├── privacy.txt
    │       ├── icon.png
    │       ├── feature_graphic.png
    │       └── phoneScreenshots/
    │
    └── windows/                             # Метаданные Microsoft Store
        ├── ru-RU/                           # Русская локализация
        │   ├── title.txt                    # Название продукта в магазине (до 256 знаков)
        │   ├── full_description.txt         # Описание продукта (до 10 000 знаков)
        │   ├── features.txt                 # Список возможностей (1 строка = 1 пункт, до 20)
        │   ├── release_notes.txt            # Заметки для пользователей Windows (до 1500 знаков)
        │   ├── privacy.txt                  # Ссылка на политику конфиденциальности
        │   ├── icon.png                     # Логотип приложения (квадрат 300x300 PNG)
        │   ├── banner.png                   # Баннер приложения (1920x1080 SuperHeroArt)
        │   └── screenshots/                 # Скриншоты Windows (1920x1080 PNG)
        │
        └── en-US/                           # Английская локализация
            ├── title.txt
            ├── full_description.txt
            ├── features.txt
            ├── release_notes.txt
            ├── privacy.txt
            ├── icon.png
            ├── banner.png
            └── screenshots/
```

---

## Команды CLI (`octopublish`)

### 1. Инициализация каталога (`init`)
Создает дерево каталогов `packaging/` с русскими шаблонами текстов и сгенерированными PNG-файлами строгого разрешения (512x512, 1024x500, 300x300, 1080x1920, 1920x1080):
```bash
octopublish init [all|android|windows] [--dir <каталог>] [--lang <локаль>]
```

### 2. Экспорт ассетов из сторов (`pull`)
Выгружает опубликованные названия, описания, иконки, баннеры и скриншоты напрямую из магазинов в структуру `Store as Code`:
```bash
# Импорт из Microsoft Store по Product ID (без авторизации, через Display Catalog API):
octopublish pull windows 9NZXP5WR803J

# Импорт из Google Play по ключу сервисного аккаунта и имени пакета:
octopublish pull android com.example.app --service-account ./play-key.json

# Выгрузка всех платформ в указанный каталог:
octopublish pull all --dir packaging
```

### 3. Добавление локали (`add`)
Добавляет языковой пакет для выбранной платформы с автоматическим распознаванием коротких кодов (`ru` -> `ru-RU`, `en` -> `en-US`, `fr` -> `fr-FR`, `de` -> `de-DE`, `es` -> `es-ES`, `zh` -> `zh-CN` и др.):
```bash
octopublish add all en --dir packaging
octopublish add android fr --dir packaging
octopublish add windows de --dir packaging
```

### 4. Предполётный линтер (`test`)
5-уровневый валидатор перед публикацией. Блокирует пайплайн CI/CD (возвращает код `1`) при обнаружении:
- Незаполненных шаблонов (`[ШАБЛОН]`, `example.com`, `App Name`).
- Отсутствия или пустоты обязательных файлов (`title.txt`, `full_description.txt`, `icon.png`).
- Превышения жестких лимитов длины символов сторов.
- Несоответствия геометрии или повреждения PNG-файлов.
- Невалидных URL политики конфиденциальности.
```bash
octopublish test --dir packaging
```

### 5. Сводная инспекция ассетов (`list`)
Выводит наглядную ANSI-таблицу статуса готовности локалей, размеров графики и скриншотов:
```bash
octopublish list --dir packaging
```

### 6. Быстрая синхронизация метаданных (`sync`)
Синхронизирует описания и графику напрямую со сторами без необходимости сборки и передачи AAB / MSIX:
```bash
octopublish sync all --dir packaging
octopublish sync android --dir packaging --dry-run
```

### 7. Публикация в Google Play Store (`playstore publish`)
```bash
octopublish playstore publish \
  --bundle "./bin/Release/App.aab" \
  --package-name "dev.octocore.obxodka" \
  --service-account "$PLAY_CONFIG_JSON" \
  --track "production" \
  --notes-dir "./packaging/whatsnew" \
  --desc-dir "./packaging/store/android"
```

### 8. Публикация в Microsoft Store (`mstore publish`)
```bash
octopublish mstore publish \
  --package "./bin/Release/App_1.0.0.0_x64.msix" \
  --app-id "9NZXP5WR803J" \
  --tenant-id "$STORE_TENANT_ID" \
  --client-id "$STORE_CLIENT_ID" \
  --client-secret "$STORE_CLIENT_SECRET" \
  --notes-dir "./packaging/whatsnew" \
  --desc-dir "./packaging/store/windows"
```

---

## Защита от потери данных: Технология Safe-Merge

В Google Play Developer API при обновлении листинга (`PUT /edits/{id}/listings/{language}`) незаполненные поля очищаются. Если в запросе отсутствует заголовок, Google Play удаляет его, вызывая ошибку `403 Forbidden: This app does not have a title set for the default language`.

**OctoPublisher** автоматически использует алгоритм **Safe-Merge**:
1. Перед отправкой изменений выполняет `GET /listings/{language}`.
2. Сохраняет текущее боевое название приложения, если локально оно не было изменено.
3. Безопасно обновляет только измененные поля описаний и графических материалов.

---

## Интеграция с GitHub Actions

```yaml
name: Релиз в магазины приложений

on:
  push:
    tags:
      - 'v*'

jobs:
  validate:
    name: Предполётная проверка метаданных
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Установка .NET 10
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Установка OctoPublisher
        run: dotnet tool install -g OctoPublisher
      - name: Запуск линтера метаданных
        run: octopublish test --dir packaging

  publish-android:
    name: Публикация Google Play Store
    needs: validate
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - name: Публикация AAB и метаданных
        env:
          PLAY_CONFIG_JSON: ${{ secrets.PLAY_CONFIG_JSON }}
          PLAY_PACKAGE_NAME: dev.octocore.obxodka
        run: |
          octopublish playstore publish \
            --bundle "./build/App.aab" \
            --track "production" \
            --notes-dir "./packaging/whatsnew" \
            --desc-dir "./packaging/store/android"

  publish-windows:
    name: Публикация Microsoft Store
    needs: validate
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - name: Публикация MSIX и метаданных
        env:
          STORE_TENANT_ID: ${{ secrets.STORE_TENANT_ID }}
          STORE_CLIENT_ID: ${{ secrets.STORE_CLIENT_ID }}
          STORE_CLIENT_SECRET: ${{ secrets.STORE_CLIENT_SECRET }}
          STORE_APP_ID: ${{ secrets.STORE_APP_ID }}
        run: |
          octopublish mstore publish \
            --package "./build/App.msix" \
            --notes-dir "./packaging/whatsnew" \
            --desc-dir "./packaging/store/windows"
```

---

## Документация

- [Руководство Store as Code](docs/STORE_AS_CODE_GUIDE.md)
- [Справочник команд CLI](docs/CLI_REFERENCE.md)
- [Архитектура движка](docs/ARCHITECTURE.md)
- [Руководство Google Play Store](docs/GOOGLE_PLAY_GUIDE.md)
- [Руководство Microsoft Store](docs/MICROSOFT_STORE_GUIDE.md)
- [Руководство по GitHub Actions](docs/GITHUB_ACTIONS_GUIDE.md)
