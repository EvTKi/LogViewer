# LogMonitor API Contract

## Базовый URL
`http://localhost:5000`

## Эндпоинты

### `GET /api/errors`
**Описание**: Возвращает список всех обнаруженных ошибок.  
**Ответ**:
```json
[
  {
    "id": 1,
    "fileName": "D:\\temp\\logs\\app.log",
    "content": "ERR: Критическая ошибка!",
    "linePosition": 42,
    "createdAt": "2025-12-04T14:30:00Z"
  }
]
```

### `GET /api/notifications`

**Описание**: Список уведомлений.  
**Ответ**:
```json
[
  {
    "id": 1,
    "errorId": 1,
    "sentAt": "2025-12-04T14:30:01Z",
    "isRead": false,
    "emailSent": false,
    "telegramSent": false
  }
]
```

### `POST /api/configure`

**Описание**: Настройка мониторинга (временно не используется — настройка через `appsettings.json`).  
**Тело запроса**:
```json
{
  "logDirectory": "D:\\logs",
  "fileMasks": ["*.log", "error_*.txt"],
  "toEmails": ["admin@example.com"],
  "telegramChatId": "123456789"
}
```

**Ответ**: `{"success": true}`

### `PATCH /api/notifications/{id}/read`

**Описание**: Отметить уведомление как прочитанное.  
**Ответ**: `{"isRead": true}`

* * *

## SignalR Hub

- **URL**: `/errorhub`
- **Подписка**: не требуется — все клиенты получают события автоматически
- **Событие**: `ReceiveError(ErrorDto)`
- **Структура `ErrorDto`**: см. выше


