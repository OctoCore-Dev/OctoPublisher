# Документация OctoPublisher

Добро пожаловать в официальную техническую документацию **OctoPublisher** — универсального высокопроизводительного движка развёртывания приложений в Google Play Store и Microsoft Store с поддержкой парадигмы **Store as Code**.

---

## Разделы документации

1. [Руководство по Store as Code](STORE_AS_CODE_GUIDE.md)
   - Структура каталогов `packaging/`
   - Спецификация файлов для Google Play и Microsoft Store
   - Лимиты символов и требования к геометрии скриншотов
   - Технология защиты от потери данных Safe-Merge
2. [Быстрый старт](GETTING_STARTED.md)
   - Установка через `dotnet tool`
   - Инициализация ассетов в проекте
   - Локальная настройка авторизации
3. [Справочник команд CLI](CLI_REFERENCE.md)
   - Глобальные флаги
   - Команды `init`, `pull`, `add`, `test`, `list`, `sync`
   - Модули `playstore` и `mstore`
4. [Предполётный линтер и валидация](STORE_AS_CODE_GUIDE.md#63-проверить-готовность-перед-публикацией-линтер)
   - 5 уровней проверки перед деплоем
   - Блокировка пайплайнов CI/CD при ошибках в ассетах
5. [Руководство: Google Play Store](GOOGLE_PLAY_GUIDE.md)
   - Создание сервисного аккаунта Google Cloud
   - Настройка прав в Google Play Console
   - Публикация AAB по трекам (internal, alpha, beta, production)
6. [Руководство: Microsoft Store](MICROSOFT_STORE_GUIDE.md)
   - Регистрация приложения в Azure AD
   - Привязка в Partner Center
   - Блочная загрузка MSIX в Azure Blob
7. [Интеграция с GitHub Actions](GITHUB_ACTIONS_GUIDE.md)
   - Секреты репозитория
   - Готовый релизный workflow YAML
8. [Архитектура движка](ARCHITECTURE.md)
   - Модули решения
   - Генератор PNG без зависимостей (`ImageTemplateGenerator`)
