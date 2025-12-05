# 🔧 Критические улучшения и рефакторинг

## 1. **Поддержка запуска с аргументами командной строки**

Сейчас всё жёстко прописано в `appsettings.json`. Нужно **разрешить переопределение через CLI**.

**Что сделать**:

- В `Program.cs` добавить парсинг аргументов или использовать `IConfigurationBuilder.AddCommandLine(args)`
- Приоритет конфигурации:
    1. Переменные окружения
    2. Аргументы CLI
    3. `appsettings.local.json`
    4. `appsettings.json`

**Пример CLI**:

```bash
dotnet LogMonitor.API.dll 
  --host=
  --port=
  --database=
```

> ✅ Это критично для CI/CD, Docker и systemd-юнитов.

* * *

## 2. **Рефакторинг Telegram: избавиться от дублирования**

Сейчас у тебя **два способа отправки**:

- Через `TelegramService.SendErrorAsync(ErrorEntity)` — вызывается из роутера (но **не используется!**)
- Через `TelegramService.SendToAllSubscribersAsync(string)` — используется реально

**Проблема**: `SendErrorAsync` ожидает `ChatId` из конфига, но ты перешёл на **подписку через `/start`** → конфигурационный `ChatId` игнорируется.

**Решение**:

- Удалить `TelegramOptions.ChatId`
- Удалить метод `SendErrorAsync`
- Оставить **только** `SendToAllSubscribersAsync`
- Обновить `NotificationRouter`:

```cs
public async Task RouteErrorAsync(ErrorDto errorDto)
{
    await _hubContext.Clients.All.SendAsync("ReceiveError", errorDto);
    if (_telegramService.IsEnabled) // ← добавить свойство
    {
        var msg = $"🚨 Новая ошибка!...";
        _ = _telegramService.SendToAllSubscribersAsync(msg);
    }
}
```

## 3. **Добавить graceful shutdown**

Сейчас при `Ctrl+C` процесс гасится, **не дождавшись завершения обработки файлов**.

**Что сделать**:

- В `LogMonitoringHostedService.StopAsync` — дождаться завершения `ProcessFileAsync`
- Использовать `CancellationToken` от `IHostApplicationLifetime`
- Сбросить позиции в БД перед выходом

> 🔥 Без этого — при рестарте будут дубликаты или пропуски.

* * *

## 4. **Оптимизация производительности**

- **Проблема**: `MatchesAnyMask` использует `Directory.GetFiles` на **каждое событие** → O(n) на каждый файлик.
- **Решение**: предварительно собрать `HashSet<string>` всех подходящих файлов при старте и обновлять при `Created`/`Deleted`.

```cs
private readonly HashSet<string> _watchedFiles = new();

private void RefreshWatchedFiles()
{
    _watchedFiles.Clear();
    foreach (var mask in _fileMasks)
    {
        foreach (var file in Directory.GetFiles(_directory, mask))
        {
            _watchedFiles.Add(file);
        }
    }
}
```

## 5. **Добавить health-check эндпоинт**

Сейчас есть проверка БД при старте, но **нет `/health` для мониторинга**.

**Добавить в `Program.cs`**:

```cs
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection"));

app.MapHealthChecks("/health");
```

→ Ответ: `{"status":"Healthy"}` — нужно для Kubernetes/Docker/k6-мониторинга.

* * *

## 6. **Фикс CORS**

Сейчас в `appsettings.json` → `"AllowedHosts": "*"` — **недостаточно** для SignalR.

**Добавить в `Program.cs`**:

```cs
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // ← обязательно для SignalR
    });
});

// После app.Build()
app.UseCors("AllowFrontend");
```

## 7. **Логирование: убрать чувствительные данные**

- В логах **никогда не писать** `BotToken`, пароли, полные пути (если не в debug)
- Использовать `SensitiveDataLogging` только в dev
