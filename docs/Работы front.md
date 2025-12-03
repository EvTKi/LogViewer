
# 🎯 ЗАДАЧИ ДЛЯ FRONTEND-РАЗРАБОТЧИКА
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

### 1. **API-клиент**

Создай `src/api/LogMonitorApi.ts`:
```ts
const BASE = process.env.REACT_APP_API_URL || 'http://localhost:5000';

export const LogMonitorApi = {
  getErrors: (page = 1, size = 20) =>
    fetch(`${BASE}/api/errors?page=${page}&size=${size}`).then(r => r.json()),

  markAsRead: (id: number) =>
    fetch(`${BASE}/api/notifications/${id}/read`, { method: 'PATCH' }).then(r => r.json()),

  configure: (config: { logDirectory: string; fileMasks: string[] }) =>
    fetch(`${BASE}/api/configure`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(config)
    }).then(r => r.json())
};
```

### 2. **SignalR-подключение**

Создай хук `src/hooks/useErrorNotifications.ts`:
```ts
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { useEffect } from 'react';

export const useErrorNotifications = (onNewError: (err: ErrorDto) => void) => {
  useEffect(() => {
    const connection = new HubConnectionBuilder()
      .withUrl(`${process.env.REACT_APP_API_URL}/errorhub`)
      .build();

    connection.start().then(() => {
      connection.invoke('Subscribe', 'web-' + Date.now());
      connection.on('ReceiveError', onNewError);
    });

    return () => { connection.stop(); };
  }, [onNewError]);
};
```

### **UI-компоненты**

- `ErrorList.tsx` — таблица MUI с колонками: файл, содержимое, дата, статус прочтения
- `NotificationToast.tsx` — используй `notistack` или `react-hot-toast` для всплываний
- `SettingsForm.tsx` — поля:
    - `Log Directory` (string)
    - `File Masks` (массив, можно через `ChipInput`)
    - Кнопка **Apply**

### 4. **Интеграция**

В `App.tsx`:
```ts
const [errors, setErrors] = useState<ErrorDto[]>([]);

useEffect(() => {
  LogMonitorApi.getErrors().then(data => setErrors(data.items));
}, []);

useErrorNotifications((newErr) => {
  toast.error(`Ошибка в ${newErr.fileName}: ${newErr.content.substring(0, 60)}...`);
  setErrors(prev => [newErr, ...prev]); // добавляем наверх
});
```

### ✅ Критерии приёмки frontend:

- Подключение к SignalR без ошибок
- Всплывающее уведомление при новой ошибке
- Таблица с пагинацией
- Настройка пути и масок через UI → отправка POST в `/api/configure`
- CORS разрешён с `localhost:3000`