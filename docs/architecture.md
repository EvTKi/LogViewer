# Архитектура LogMonitor

## Обзор
Система состоит из:
- **Фоновой службы** (`LogMonitoringHostedService`), запускающейся при старте приложения
- **Монитора файлов** (`HybridFileWatcher`), отслеживающего изменения в лог-файлах
- **Детектора ошибок** — ищет строки, содержащие `ERR` (case-insensitive)
- **Дедупликации** — по `(FileName + LinePosition)` или `ContentHash` (SHA256)
- **Сохранения** в PostgreSQL
- **Рассылки уведомлений** через SignalR (в реальном времени)

## Поток данных
1. Приложение стартует → читает `Monitoring:LogDirectory` из `appsettings.json`
2. `HybridFileWatcher` сканирует файлы по маскам
3. При изменении файла — читает новые строки с последней позиции
4. Если найдена строка с `ERR` → проверяет дубликаты
5. Если ошибка новая → сохраняет в `Errors` → создаёт `Notifications` → обновляет `FilePositions`
6. Отправляет `ErrorDto` через `INotificationRouter` → SignalR → все подключённые клиенты

## Компоненты
- **LogMonitor.Core** — DTO, интерфейсы, сущности
- **LogMonitor.Infrastructure** — реализация (EF Core, FileWatcher, логика)
- **LogMonitor.API** — Web API, SignalR, хостинг