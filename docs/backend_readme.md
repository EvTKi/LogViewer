## 📄 1. Инструкция по запуску (для `README.md`)

### Требования

- .NET 8 SDK
- PostgreSQL 12+
- Git

### Локальный запуск

1. **Склонируй репозитори**
```
git clone <repo-url>
cd log-monitor/backend/src/LogMonitor.API
```
2. **Создай БД и таблицы**
- Подключись к PostgreSQL через `psql` или pgAdmin
- Выполни скрипт: [`docs/db-schema.sql`](https://chat.qwen.ai/docs/db-schema.sql)
```sql
CREATE DATABASE logmonitor;
\c logmonitor
-- Вставь содержимое docs/db-schema.sql
```
3. **Настрой конфигурацию**Создай файл `appsettings.local.json` рядом с `appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5432;Database=logmonitor;Username=postgres;Password=your_password"
  },
  "Monitoring": {
    "LogDirectory": "C:\\temp\\logs",
    "FileMasks": ["*.log", "error_*.txt"]
  },
  "Telegram": {
    "IsEnabled": true,
    "BotToken": "123456:ABC...",
    "ChatId": null
  }
}
```
4. **Запусти**
```bash
dotnet run
```
1. **Проверь**
    - API: [http://localhost:5000/swagger<svg width="1em" height="1em" fill="currentColor" aria-hidden="true" focusable="false" class=""><use xlink:href="#icon-line-arrow-up-right"></use></svg>](http://localhost:5000/swagger)
    - SignalR: подключись через JavaScript-клиент (см. демо-сценарий)

* * *

## 🎥 3. Демо-сценарий (что проверял)

### 🧪 Тест 1: Обнаружение ошибок

- Запустил `LogGenerator` → генерирует `live_app.log` в `autotest/LogGenerator/log`
- Указал эту папку в `appsettings.local.json`
- Добавил строку: `2025:12:05 12:00 [ERR] - Test error`
- ✅ В логах появилось:  
`Найдена ошибка в файле ...: ...`
- ✅ В БД (`Errors`) — новая запись

### 🧪 Тест 2: Дедупликация

- Добавил **ту же строку** второй раз
- ✅ В БД — **не создалась** вторая запись (проверено по `ContentHash` и `LinePosition`)

### 🧪 Тест 3: Telegram (через ngrok)

- Запустил `ngrok http 5000`
- Настроил Webhook у бота через `@BotFather` → `Set webhook`
- Написал `/start` → ✅ запись в `TelegramSubscribers`
- Добавил `ERR` → ✅ получил уведомление в Telegram
- Проверил БД → `TelegramSent = true`

> 
> 💡 В Непале Telegram **не работает локально** (NTA), поэтому использовал внешний сервер и ngrok.

* * *

## 📋 4. Логи запуска

### Консоль при старте
```
info: LogMonitor.API[0]
      🔧 Приложение сконфигурировано. Запуск хоста...
info: LogMonitor.API[0]
      ✅ Успешное подключение к базе данных PostgreSQL.
info: LogMonitor.Infrastructure.BackgroundServices.LogMonitoringHostedService[0]
      🔄 Фоновая служба мониторинга логов запускается...
info: LogMonitor.Infrastructure.BackgroundServices.LogMonitoringHostedService[0]
      📁 Отслеживаемая директория: C:\temp\logs, маски: *.log,error_*.txt
```
### Лог Serilog (`log/LogViewer_*.log`)
```
12:00:05 [INF] [HybridFileWatcher] - Найдена ошибка в файле C:\temp\logs\live_app.log: 2025:12:05 12:00 [ERR] - Test error
12:00:06 [INF] [TelegramService] - ✅ Уведомление отправлено в Telegram (ошибка ID: 42)
```
## 🤖 Особое внимание — Telegram

### 📦 Используемые технологии

- **HttpClient** напрямую (без сторонних библиотек)
- `IHttpClientFactory` для управления подключениями

### 🔁 Обработка ошибок и повторные попытки

- При ошибке HTTP (4xx/5xx) или таймауте:
    - **3 попытки** с экспоненциальной задержкой: 1s → 2s → 4s
    - Логирование каждой попытки (`Debug`) и итоговой ошибки (`Error`)

### 💾 Обновление `TelegramSent = true`

- После **успешного** `POST` в Telegram:
```cs
var notification = await _dbContext.Notifications
    .FirstOrDefaultAsync(n => n.ErrorId == errorId);
if (notification != null)
{
    notification.TelegramSent = true;
    await _dbContext.SaveChangesAsync();
}
```
- Обновление происходит **только при успехе**, даже если SignalR-уведомление уже ушло