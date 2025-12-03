
# 🖥️ ЗАДАЧИ ДЛЯ BACKEND-РАЗРАБОТЧИКА
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
│       ├── log/
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

### Этап 1: База данных (выполняется **до всего остального**)

**Цель**: Схема БД готова и доступна.

**Что делать**:

1. Создай файл `docs/db-schema.sql` с содержимым:
```sql
-- Errors
CREATE TABLE "Errors" (
    "Id" SERIAL PRIMARY KEY,
    "FileName" TEXT NOT NULL,
    "Content" TEXT NOT NULL,
    "LinePosition" BIGINT NOT NULL,
    "ContentHash" CHAR(64) NOT NULL,  -- SHA256(content)
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX "IX_Errors_FileName_LinePosition" ON "Errors" ("FileName", "LinePosition");
CREATE INDEX "IX_Errors_CreatedAt" ON "Errors" ("CreatedAt");

-- Notifications
CREATE TABLE "Notifications" (
    "Id" SERIAL PRIMARY KEY,
    "ErrorId" INT NOT NULL REFERENCES "Errors"("Id") ON DELETE CASCADE,
    "SentAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "IsRead" BOOLEAN NOT NULL DEFAULT FALSE,
    "EmailSent" BOOLEAN NOT NULL DEFAULT FALSE,
    "TelegramSent" BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE INDEX "IX_Notifications_ErrorId" ON "Notifications" ("ErrorId");
```

1. Настрой EF Core (`LogMonitor.Infrastructure/Data/LogMonitorDbContext.cs`)
2. Сделай начальную миграцию
```bash
dotnet ef migrations add InitialCreate
```

**Критерий приемки**:

- При запуске `dotnet run` — БД создается автоматически, если не существует.

* * *

### Этап 2: API и DTO

**Цель**: Фронт точно знает, куда стучаться и что получает.

#### REST API (все в `LogMonitor.API/Controllers/`)
| Метод | Эндпоинт | Описание | Тело запроса (если POST) | Ответ |
| --- | --- | --- | --- | --- |
| `GET` | `/api/errors` | Список ошибок | — | `List<ErrorDto>` |
| `GET` | `/api/notifications` | Список уведомлений | — | `List<NotificationDto>` |
| `POST` | `/api/configure` | Настроить мониторинг | `ConfigureRequest` | `{ "success": true }` |
| `PATCH` | `/api/notifications/{id}/read` | Отметить как прочитанное | — | `{ "isRead": true }` |

#### DTO (в `LogMonitor.Core/Dto/`)
```cs
public record ErrorDto(
    int Id,
    string FileName,
    string Content,
    long LinePosition,
    DateTime CreatedAt
);

public record NotificationDto(
    int Id,
    int ErrorId,
    DateTime SentAt,
    bool IsRead,
    bool EmailSent,
    bool TelegramSent
);

public record ConfigureRequest(
    string LogDirectory,            // например: "/var/logs"
    string[] FileMasks,             // например: ["*.log", "error_*.txt"]
    string[] ToEmails = null,       // опционально
    string TelegramChatId = null    // опционально
);
```

**SignalR Hub**

- Hub: `/errorhub`
- Метод подписки: `Subscribe(string clientId)`
- Событие: `ReceiveError(ErrorDto error)` — фронт слушает это

**Критерий приемки**:

- В `docs/api-contract.md` — полное описание всех методов, кодов ответов, примеров
- Swagger ([http://localhost:5000/swagger](http://localhost:5000/swagger)) показывает все эндпоинты

* * *

### Этап 3: Логика мониторинга файлов

**Что делать**:

1. Реализуй `IFileMonitoringService` в `Infrastructure/Services/`
2. Храни **последнюю позицию чтения** по каждому файлу в памяти и в fallback-таблице (если рестарт)
```cs
CREATE TABLE "FilePositions" (
    "FilePath" TEXT PRIMARY KEY,
    "LastPosition" BIGINT NOT NULL
);
```
1. При старте — загружай позиции из этой таблицы
2. При ротации (файл уменьшился или исчез) — удаляй запись из `FilePositions`
3. При обнаружении строки с `"ERR"` (IgnoreCase):
    - Вычисли `SHA256(Content)`
    - Проверь в `Errors`, есть ли уже запись с тем же `FileName + LinePosition` **ИЛИ** `ContentHash`
    - Если новая — сохрани в `Errors`, затем создай запись в `Notifications` с флагами `false`

**Критерий приемки**:

- При добавлении дубликатов в лог — ошибка **не дублируется** в БД

* * *

### Этап 4: Уведомления

**Что делать**:

1. `EmailService`: используй MailKit. Проверяй `appsettings:Smtp:IsEnabled`
2. `TelegramService`: POST `https://api.telegram.org/bot<TOKEN>/sendMessage`  
Тело:
```json
{ "chat_id": "...", "text": "🚨 Новая ошибка в логе!..." }
```

1. После **успешной** отправки — обновляй `EmailSent = true` или `TelegramSent = true`
2. При ошибке — 3 попытки с задержкой: 1с → 2с → 4с

**Критерий приемки**:

- Если SMTP выключен — не пытается отправить, но UI уведомление всё равно летит
