# Руководство: Публикация в Microsoft Store

Руководство по настройке интеграции с Microsoft Partner Center через Azure Active Directory (Azure AD).

---

## 1. Регистрация приложения в Azure AD

1. Откройте [Microsoft Entra ID (Azure AD)](https://portal.azure.com/).
2. Перейдите в **App registrations** -> **New registration**.
3. Задайте имя (например, `OctoPublisher Deployer`).
4. В разделе **Certificates & secrets** создайте новый **Client Secret** и скопируйте его значение.
5. Скопируйте **Application (client) ID** и **Directory (tenant) ID**.
6. В [Partner Center](https://partner.microsoft.com/dashboard) перейдите в **Account settings** -> **User management** -> **Azure AD applications** и добавьте созданное приложение с ролью **Manager**.

---

## 2. Локальная настройка и публикация

### Интерактивная настройка:
```bash
octopublish mstore configure
```

### Публикация MSIX пакета:
```bash
octopublish mstore publish \
  --package "./bin/Release/App_1.0.0.0_x64.msix" \
  --notes-dir "./packaging/whatsnew" \
  --desc-dir "./packaging/store/windows"
```
