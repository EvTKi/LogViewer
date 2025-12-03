# Деплой LogMonitor

## Требования
- .NET 8 SDK
- PostgreSQL 12+
- Доступ к порту 5432 (локально)

## Локальный запуск (Windows/Linux)

1. Создайте БД `logmonitor` в PostgreSQL
2. Выполните `docs/db-schema.sql`
3. Убедитесь, что в `LogMonitor.API/appsettings.json` указаны:
   - `Monitoring:LogDirectory` — папка с логами
   - `Connection string` — подключение к БД
4. Запустите:
   ```bash
   cd backend/src/LogMonitor.API
   dotnet run
   ```
5. API доступен на `http://localhost:5000`
6. Логи приложения — в папке `LogMonitor.API/logs/`


## 📄 Документация по развёртыванию LogMonitor на Linux

> 
> **Версия**: 1.0  
> **Целевая ОС**: Ubuntu 22.04 / Debian 12 (или любой дистрибутив с systemd)  
> **Требования**: .NET 8 Runtime, PostgreSQL 12+, доступ по SSH

### 🔧 1. Подготовка сервера

#### 1.1 Установите .NET 8 Runtime
```bash
# Добавьте Microsoft-репозиторий
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Установите ASP.NET Core Runtime
sudo apt update
sudo apt install -y aspnetcore-runtime-8.0
```

## 🗃️ 2. Подготовка базы данных

> 
> Предполагается, что PostgreSQL уже установлен и запущен.

### 2.1 Создайте БД и пользователя (выполните от имени `postgres`)
```sql
CREATE DATABASE logmonitor;
CREATE USER logmonitor_user WITH PASSWORD 'StrongPassword123!';
GRANT ALL PRIVILEGES ON DATABASE logmonitor TO logmonitor_user;
\c logmonitor
-- Выполните скрипт создания таблиц:
\i /path/to/docs/db-schema.sql
```

### 2.2 Убедитесь, что подключение разрешено

В `pg_hba.conf` (обычно `/etc/postgresql/16/main/pg_hba.conf`) добавьте:
```bash
host logmonitor logmonitor_user 127.0.0.1/32 scram-sha-256
```
Перезапустите PostgreSQL:
```bash
sudo systemctl restart postgresql
```

## 🚀 3. Развёртывание приложения

### 3.1 Соберите приложение на своей машине
```ps
# Windows / Linux (на машине разработчика)
cd backend/src/LogMonitor.API
dotnet publish -c Release -r linux-x64 --self-contained false -o ./publish
```
> 
> `-r linux-x64` — обязательно для Linux  
> `--self-contained false` — использует системный runtime

### 3.2 Скопируйте на сервер
```bash
scp -r publish user@your-server:/opt/logmonitor
```

## 🔐 4. Настройка конфигурации

### 4.1 Создайте `appsettings.local.json`
```bash
sudo nano /opt/logmonitor/appsettings.local.json
```

Содержимое:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5432;Database=logmonitor;Username=logmonitor_user;Password=StrongPassword123!"
  },
  "Monitoring": {
    "LogDirectory": "/var/log/myapp",
    "FileMasks": ["*.log", "error_*.txt"]
  }
}
```

### 4.2 Убедитесь, что папка логов доступна
```bash
sudo mkdir -p /var/log/myapp
sudo chown -R www-data:www-data /var/log/myapp
```

## ⚙️ 5. Настройка службы systemd

Создайте файл службы:
```bash
sudo nano /etc/systemd/system/logmonitor.service
```

Содержимое:
```ini
[Unit]
Description=LogMonitor Real-time Log Viewer
After=network.target postgresql.service

[Service]
WorkingDirectory=/opt/logmonitor
ExecStart=/usr/bin/dotnet LogMonitor.API.dll
Restart=always
RestartSec=10
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false

[Install]
WantedBy=multi-user.target
```
Затем:
```bash
sudo systemctl daemon-reload
sudo systemctl enable logmonitor
sudo systemctl start logmonitor
```

## 🔍 6. Проверка работы

### Статус службы:
```bash
sudo systemctl status logmonitor
```

Логи приложения:
```bash
tail -f /opt/logmonitor/log/LogViewer_*.log
```
Ожидаемый вывод при старте:


```
14:30:00 [INF] [Program] - 🔧 Приложение сконфигурировано. Запуск хоста...
14:30:01 [INF] [Program] - ✅ Успешное подключение к базе данных PostgreSQL.
14:30:02 [INF] [LogMonitoringHostedService] - 🔄 Фоновая служба мониторинга логов запускается...
```

Тест через curl (если нужен API без фронта):
```
curl http://localhost:5000/api/errors
# Должен вернуть: []
```

## 🛡️ 7. Безопасность (рекомендуется)

### 7.1 Отключите Swagger в production

Убедитесь, что в `Program.cs`:
```cs
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

### 7.2 Используйте Nginx + HTTPS (опционально)

Если нужен доступ извне — настройте обратный прокси и Let's Encrypt.

## 📁 Структура после развёртывания
```
/opt/logmonitor/
├── LogMonitor.API.dll
├── appsettings.json
├── appsettings.local.json   ← секреты
└── log/
    └── LogViewer_2025-12-04.log
```

