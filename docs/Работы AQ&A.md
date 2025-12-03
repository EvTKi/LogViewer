# 🧪 AUTOQ&A: ПОДРОБНЫЕ ТЕХНИЧЕСКИЕ ЗАДАЧИ
## Структура проекта
```
log-monitor/
├── backend/
│   └── src/
│       ├── LogMonitor.API/                 # ASP.NET Core Web API + SignalR
│       │   ├── Controllers/
│       │   │   ├── ErrorsController.cs
│       │   │   ├── NotificationsController.cs
│       │   │   └── ConfigurationController.cs
│       │   ├── Hubs/
│       │   │   └── ErrorNotificationHub.cs
│       │   ├── Properties/
│       │   ├── appsettings.json
│       │   ├── appsettings.Development.json
│       │   └── Program.cs
│       │
│       ├── LogMonitor.Core/                # Contracts, DTOs, interfaces
│       │   ├── Entities/
│       │   │   ├── ErrorEntity.cs
│       │   │   └── NotificationEntity.cs
│       │   ├── Dtos/
│       │   │   ├── ErrorDto.cs
│       │   │   ├── NotificationDto.cs
│       │   │   └── ConfigureRequest.cs
│       │   └── Services/
│       │       ├── IFileMonitoringService.cs
│       │       ├── INotificationRouter.cs
│       │       └── IErrorDetectionService.cs
│       │
│       └── LogMonitor.Infrastructure/      # Реализация: EF, файлы, уведомления
│           ├── Data/
│           │   ├── LogMonitorDbContext.cs
│           │   └── Configurations/         # Fluent API
│           ├── Services/
│           │   ├── HybridFileWatcher.cs
│           │   ├── EmailService.cs
│           │   ├── TelegramService.cs
│           │   └── NotificationRouter.cs
│           ├── BackgroundServices/
│           │   └── LogMonitoringHostedService.cs
│           └── LogMonitor.Infrastructure.csproj
│
│   └── migrations/                          # (опционально — если не в проекте)
│   └── tests/
│       ├── LogMonitor.UnitTests/
│       └── LogMonitor.IntegrationTests/
│
├── frontend/
│   ├── public/
│   └── src/
│       ├── api/
│       │   ├── endpoints.ts
│       │   └── LogMonitorApi.ts
│       ├── components/
│       │   ├── layout/
│       │   ├── ui/
│       │   │   ├── ErrorList.tsx
│       │   │   ├── NotificationToast.tsx
│       │   │   └── SettingsForm.tsx
│       │   └── RealtimeAlert.tsx
│       ├── hooks/
│       │   └── useErrorNotifications.ts
│       ├── store/                          # Zustand или Context
│       ├── App.tsx
│       ├── index.tsx
│       └── .env.local
│
│   ├── package.json
│   └── tsconfig.json
│
├── autotest/
│   └── LogGenerator/                       # .NET Console App
│       ├── Program.cs
│       ├── LogGenerator.csproj
│       └── README.md                       # Как запускать
│
├── docs/
│   ├── db-schema.sql                       # CREATE TABLE ...
│   ├── api-contract.md                     # Все эндпоинты + примеры
│   ├── architecture.md                     # Диаграмма потоков
│   └── deployment.md                       # Как деплоить на Linux/Windows
│
├── docker/
│   ├── backend.Dockerfile
│   ├── frontend.Dockerfile
│   └── nginx.conf
│
├── docker-compose.yml                      # PostgreSQL + pgAdmin
├── .gitignore
├── README.md                               # Краткий гайд: как запустить
└── LICENSE
```
### 1. **LogGenerator — микросервис для тестов**

- Тип проекта: `dotnet new console`
- Цель: генерить файлы в указанной директории в формате:  
`YYYY:MM:DD HH:MM [Level] - Message`
- Поддерживаемые Level: `INF`, `ERR`, `DGB`
- Параметры запуска
```bash
dotnet run -- \
  --output-dir /tmp/logs \
  --files 3 \
  --lines-per-file 1000 \
  --err-frequency 0.05  # 5% строк — ERR
```
- Дополнительно:
    - Каждый файл — `app_{N}.log`
    - Имитация ротации: после 500 строк — закрыть файл, создать новый
    - Поддержка Windows/Linux (используй `Path.DirectorySeparatorChar`)

### 2. **Автотесты**

Создай `LogMonitor.Tests` (xUnit):

#### Unit-тесты:

- `FileWatcher_DetectsErrInNewLines`
- `FileWatcher_IgnoresDuplicates`
- `ErrorService_CreatesUniqueContentHash`

#### Интеграционные тесты:

- Запуск `LogGenerator` → запуск `FileMonitoringService` → проверка количества записей в БД
- Проверка отправки в Telegram через `MockHttpMessageHandler`

#### E2E-сценарий (через TestServer + Puppeteer или просто API):

- POST /configure → генерация логов → проверка количества уведомлений в БД и через SignalR-клиент

* * *

## 📚 Артефакты, которые ОБЯЗАТЕЛЬНО должны быть в репозитории:

- `docs/db-schema.sql`
- `docs/api-contract.md` (с примерами запросов/ответов)
- `docker-compose.yml` (PostgreSQL + pgAdmin)
- `autotest/LogGenerator/` — готовый .NET проект
- `.env.example` и `appsettings.Development.json.example`
