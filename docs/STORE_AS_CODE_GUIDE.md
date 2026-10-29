# Руководство по управлению метаданными и ассетами: "Store as Code"

Инструмент **OctoPublisher** (`octopublish`) реализует парадигму **Store as Code**: все текстовые описания, иконки, баннеры, скриншоты и заметки о релизах хранятся в репозитории Git и автоматически разворачиваются на страницах магазинов **Google Play Store** и **Microsoft Store**.

Любые изменения файлов в этих папках автоматически синхронизируются со страницами магазинов при запуске команды `octopublish sync` или в релизном пайплайне GitHub Actions.

---

## 1. Структура директорий

```
packaging/
├── whatsnew/                                # Заметки о выпуске («Что нового» при обновлении)
│   ├── whatsnew-ru-RU.txt                   # Русский текст (до 500 символов)
│   └── whatsnew-en-US.txt                   # Английский текст (до 500 символов)
│
└── store/
    ├── android/                             # Метаданные и графика для Google Play Store
    │   ├── ru-RU/                           # Русская локализация
    │   │   ├── title.txt                    # Название приложения (строго <= 30 символов)
    │   │   ├── short_description.txt        # Краткое описание (строго <= 80 символов)
    │   │   ├── full_description.txt         # Полное описание (до 4000 символов)
    │   │   ├── privacy.txt                  # Ссылка на политику конфиденциальности
    │   │   ├── icon.png                     # Иконка для Play Market (512x512, 32-bit PNG)
    │   │   ├── feature_graphic.png          # Баннер (1024x500, PNG или JPEG)
    │   │   └── phoneScreenshots/            # Скриншоты телефона (от 2 до 8 штук, 16:9 / 9:16)
    │   │       ├── 01_главная.png
    │   │       └── 02_настройки.png
    │   │
    │   └── en-US/                           # Английская локализация
    │       ├── title.txt                    # App title (max 30 chars)
    │       ├── short_description.txt        # Short description (max 80 chars)
    │       ├── full_description.txt         # Full description (max 4000 chars)
    │       ├── privacy.txt                  # Privacy policy URL
    │       ├── icon.png                     # Store icon (512x512 PNG)
    │       ├── feature_graphic.png          # Promo banner (1024x500 PNG/JPEG)
    │       └── phoneScreenshots/            # Phone screenshots
    │           ├── 01_home.png
    │           └── 02_settings.png
    │
    └── windows/                             # Метаданные для Microsoft Store
        ├── ru-RU/                           # Русская локализация
        │   ├── title.txt                    # Название в магазине (override)
        │   ├── full_description.txt         # Описание приложения (до 10 000 символов)
        │   ├── features.txt                 # Список возможностей (1 строка = 1 пункт, до 20)
        │   ├── release_notes.txt            # Заметки к обновлению для Windows (до 1500 символов)
        │   ├── privacy.txt                  # Ссылка на политику конфиденциальности
        │   ├── icon.png                     # Логотип приложения (квадрат 300x300 PNG)
        │   ├── banner.png                   # Баннер приложения (1920x1080 SuperHeroArt)
        │   └── screenshots/                 # Скриншоты Windows (1920x1080 PNG)
        │       └── 01_рабочий_стол.png
        │
        └── en-US/                           # Английская локализация
            ├── title.txt                    # Title override
            ├── full_description.txt         # Application description
            ├── features.txt                 # Key features (1 line per item)
            ├── release_notes.txt            # Windows release notes
            ├── privacy.txt                  # Privacy policy URL
            ├── icon.png                     # App logo (300x300 PNG)
            ├── banner.png                   # Hero banner (1920x1080 PNG)
            └── screenshots/                 # Windows screenshots
                └── 01_desktop.png
```

---

## 2. Спецификация для Google Play Store (`store/android/`)

Каждая подпапка внутри `store/android/` соответствует коду языка (например, `ru-RU`, `en-US`, `de-DE`, `fr-FR`).

| Файл / Папка | Назначение | Требования и лимиты Google Play |
| :--- | :--- | :--- |
| `title.txt` | Название приложения | **Максимум 30 символов**. Нельзя использовать эмодзи и спецсимволы рекламного характера («БЕСПЛАТНО», «ТОП-1»). |
| `short_description.txt` | Краткое описание | **Максимум 80 символов**. Отображается под иконкой и названием на главной странице приложения в Google Play. |
| `full_description.txt` | Полное описание | **Максимум 4000 символов**. Поддерживает списки, переносы строк и базовый текст. |
| `privacy.txt` | Политика конфиденциальности | Валидный URL с протоколом `https://`. |
| `icon.png` | Значок приложения в магазине | **512 × 512 пикселей**, формат PNG (32-бит с альфа-каналом), вес до 1024 КБ. Углы скруглять не нужно (Google скругляет автоматически). |
| `feature_graphic.png` | Баннер (главное изображение) | **1024 × 500 пикселей**, формат PNG (24-бит без прозрачности) или JPEG, вес до 15 МБ. Отображается в шапке карточки приложения. |
| `phoneScreenshots/` | Скриншоты телефона | Формат PNG или JPEG (24-бит без альфы), соотношение сторон **16:9** или **9:16**. Стороны: от **320 px** до **3840 px**. От **2 до 8 файлов**. Сортируются по имени файла (`01_...png`, `02_...png`). |
| `sevenInchScreenshots/` *(опционально)* | Скриншоты планшетов 7" | Формат PNG или JPEG, от 320 до 3840 px, от 2 до 8 штук. |
| `tenInchScreenshots/` *(опционально)* | Скриншоты планшетов 10" | Формат PNG или JPEG, от 1080 до 7680 px, от 2 до 8 штук. |

> [!TIP]
> **Общие материалы (Fallback)**:
> Если вы хотите использовать одинаковую иконку или скриншоты для всех языков, вы можете поместить `icon.png` или папку `phoneScreenshots/` прямо в корень `packaging/store/android/`. Языковые папки, в которых нет собственных файлов графики, автоматически унаследуют общие файлы.

---

## 3. Спецификация для Microsoft Store (`store/windows/`)

Подпапки внутри `store/windows/` соответствуют целевым рынкам Windows (`ru-RU`, `en-US`):

| Файл | Назначение | Требования и формат |
| :--- | :--- | :--- |
| `title.txt` | Название продукта | Строка текста с официальным названием приложения в Partner Center (до 256 знаков). |
| `full_description.txt` | Описание продукта | До **10 000 символов**. Основной текст на странице приложения в Microsoft Store. |
| `release_notes.txt` | Заметки к выпуску | Заметки, отображаемые пользователям Windows в разделе «Что нового» (до 1500 символов). |
| `features.txt` | Ключевые возможности | **Один пункт на строку**. До **20 пунктов**. Каждый пункт до 200 символов. Строки, начинающиеся с `#`, игнорируются. |
| `privacy.txt` | Политика конфиденциальности | Ссылка на страницу политики конфиденциальности разработчика. |
| `icon.png` | Логотип приложения | Квадратное изображение **300 × 300 пикселей** PNG. |
| `banner.png` | Промо-баннер (Hero) | Изображение **1920 × 1080 пикселей** PNG/JPEG (SuperHeroArt). |
| `screenshots/` | Скриншоты для десктопа | Изображения **1920 × 1080** или **1366 × 768 пикселей** PNG/JPEG. |

---

## 4. Заметки о выпуске (`whatsnew/`)

Используются при доставке обновлений на устройства пользователей:
- `whatsnew-ru-RU.txt`: текст для русскоязычных пользователей (макс. 500 символов).
- `whatsnew-en-US.txt`: текст для англоязычных пользователей (макс. 500 символов).

---

## 5. Технология Safe-Merge в OctoPublisher

Встроенный в **OctoPublisher** механизм **Safe-Merge** защищает от случайного затирания метаданных:
1. Google Play API при HTTP `PUT /edits/{id}/listings/{language}` полностью перезаписывает карточку приложения. Если поле отсутствует или равно `null`, Google Play удаляет его.
2. Если в `title.txt` не задано новое название (или файл отсутствует), **OctoPublisher** перед отправкой обновления сначала делает `GET /listings/{language}`, извлекает текущее рабочее название из Google Play и подставляет его в запрос.
3. Это гарантирует, что вы можете обновлять описание, скриншоты или заметки к релизу, не рискуя потерять название приложения и получить ошибку `403 Forbidden: This app does not have a title set for the default language`.

---

## 6. Локальные команды CLI (`octopublish`)

### 6.1. Создать шаблон структуры в проекте:
```bash
octopublish init all --dir packaging
```

### 6.2. Выгрузить существующие метаданные и графику из магазинов (pull):
```bash
# Импорт из Microsoft Store по Product ID (без авторизации):
octopublish pull windows 9NZXP5WR803J --dir packaging

# Импорт из Google Play по имени пакета и ключу:
octopublish pull android com.example.app --service-account ./play-key.json --dir packaging
```

### 6.3. Добавить новый язык:
```bash
octopublish add all fr --dir packaging
```

### 6.4. Проверить готовность перед публикацией (линтер):
```bash
octopublish test --dir packaging
```

### 6.5. Просмотреть сводную таблицу ассетов:
```bash
octopublish list --dir packaging
```

### 6.6. Быстрая синхронизация метаданных со сторами (без пересборки):
```bash
octopublish sync all --dir packaging
```
