using System.IO;
using System.Text;
using LogMonitor.Core.Entities;
using LogMonitor.Core.Services;
using LogMonitor.Core.Dtos;
using LogMonitor.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace LogMonitor.Infrastructure.Services;
// TODO: прикрутить маску по строке сообщения
public class HybridFileWatcher : IFileMonitoringService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IErrorDetectionService _errorDetection;
    private readonly INotificationRouter _notificationRouter;
    private readonly ILogger<HybridFileWatcher> _logger;
    private readonly Dictionary<string, long> _filePositions = new();
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly HashSet<string> _watchedFiles = new(StringComparer.OrdinalIgnoreCase);
    private string _directory = "";
    private string[] _fileMasks = Array.Empty<string>();
    private bool _isRunning;

    public HybridFileWatcher(
        IServiceProvider serviceProvider,
        IErrorDetectionService errorDetection,
        INotificationRouter notificationRouter,
        ILogger<HybridFileWatcher> logger)
    {
        _serviceProvider = serviceProvider;
        _errorDetection = errorDetection;
        _notificationRouter = notificationRouter;
        _logger = logger;
    }

    public async Task StartMonitoringAsync(string directory, string[] fileMasks)
    {
        if (_isRunning) return;

        _directory = directory;
        _fileMasks = fileMasks;
        RefreshWatchedFiles();
        _isRunning = true;
        

        // Загружаем позиции из БД
        Dictionary<string, long> savedPositions;
        using (var scope = _serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<LogMonitorDbContext>();
            savedPositions = await dbContext.FilePositions.ToDictionaryAsync(fp => fp.FilePath, fp => fp.LastPosition);
        }
        foreach (var pos in savedPositions)
            _filePositions[pos.Key] = pos.Value;

        var watcher = new FileSystemWatcher(_directory)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.LastWrite,
            IncludeSubdirectories = false
        };

        watcher.Changed += OnFileChanged;
        watcher.Created += OnFileChanged;
        watcher.Renamed += OnFileRenamed;
        watcher.Deleted += OnFileDeleted;
        watcher.EnableRaisingEvents = true;

        _watchers.Add(watcher);

        await ScanExistingFilesAsync();
    }

    public async Task StopMonitoringAsync()
    {
        _isRunning = false;
        foreach (var w in _watchers)
        {
            w.EnableRaisingEvents = false;
            w.Dispose();
        }
        _watchers.Clear();

        // Сохраняем позиции в БД
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<LogMonitorDbContext>();
        foreach (var (filePath, position) in _filePositions)
        {
            var existing = await dbContext.FilePositions.FindAsync(filePath);
            if (existing != null)
            {
                existing.LastPosition = position;
            }
            else
            {
                dbContext.FilePositions.Add(new FilePositionEntity
                {
                    FilePath = filePath,
                    LastPosition = position
                });
            }
        }
        await dbContext.SaveChangesAsync();
    }

    private async void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        try
        {
            RefreshWatchedFiles(); // ← добавь
            if (!_isRunning || !MatchesAnyMask(e.FullPath)) return;
            await ProcessFileAsync(e.FullPath).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в OnFileChanged для файла {File}", e.FullPath);
        }
    }

    private async void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        try
        {
            if (!_isRunning) return;
            if (MatchesAnyMask(e.OldFullPath))
                _filePositions.Remove(e.OldFullPath);
            if (MatchesAnyMask(e.FullPath))
                _ = ProcessFileAsync(e.FullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка в OnFileRenamed для файла {File}", e.FullPath);
        }
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        RefreshWatchedFiles();
        if (MatchesAnyMask(e.FullPath))
            _filePositions.Remove(e.FullPath);
    }
    private bool MatchesAnyMask(string fullPath)
    {
        return _watchedFiles.Contains(fullPath);
    }
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

    private async Task ScanExistingFilesAsync()
    {
        foreach (var mask in _fileMasks)
        {
            var files = Directory.GetFiles(_directory, mask);
            foreach (var file in files)
            {
                await ProcessFileAsync(file);
            }
        }
    }

    private async Task ProcessFileAsync(string filePath)
    {
        if (!File.Exists(filePath)) return;

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0) return;

        long lastPosition;
        using (var scope = _serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<LogMonitorDbContext>();
            var saved = await dbContext.FilePositions.FindAsync(filePath);
            lastPosition = saved?.LastPosition ?? 0;
        }

        if (lastPosition > fileInfo.Length)
        {
            lastPosition = 0;
        }

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        fs.Seek(lastPosition, SeekOrigin.Begin);

        using var sr = new StreamReader(fs, Encoding.UTF8);
        string? line;
        long currentPosition = lastPosition;

        var errorDtos = new List<ErrorDto>();

        while ((line = await sr.ReadLineAsync()) != null)
        {
            currentPosition += Encoding.UTF8.GetByteCount(line) + 1; // +1 for \n

            if (_errorDetection.IsErrorLine(line))
            {
                var contentHash = _errorDetection.ComputeHash(line);

                bool exists;
                using (var scope = _serviceProvider.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<LogMonitorDbContext>();
                    exists = await dbContext.Errors.AnyAsync(e =>
                        (e.FileName == filePath && e.LinePosition == currentPosition) ||
                        e.ContentHash == contentHash
                    );
                }

                if (!exists)
                {
                    _logger.LogInformation("Найдена ошибка в файле {FilePath}: {ErrorContent}", filePath, line);
                    // --- Сохраняем ОДИН раз: ошибка + уведомление + позиция ---
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var dbContext = scope.ServiceProvider.GetRequiredService<LogMonitorDbContext>();

                        var error = new ErrorEntity
                        {
                            FileName = filePath,
                            Content = line,
                            LinePosition = currentPosition,
                            ContentHash = contentHash,
                            CreatedAt = DateTime.UtcNow
                        };

                        dbContext.Errors.Add(error);
                        await dbContext.SaveChangesAsync(); // ← теперь error.Id известен

                        
                        var notification = new NotificationEntity
                        {
                            ErrorId = error.Id,
                            IsRead = false,
                            EmailSent = false,
                            TelegramSent = false
                        };

                        dbContext.Notifications.Add(notification);

                        // Обновляем позицию
                        var existingPos = await dbContext.FilePositions.FindAsync(filePath);
                        if (existingPos != null)
                        {
                            existingPos.LastPosition = currentPosition;
                        }
                        else
                        {
                            dbContext.FilePositions.Add(new FilePositionEntity
                            {
                                FilePath = filePath,
                                LastPosition = currentPosition
                            });
                        }

                        await dbContext.SaveChangesAsync();

                        // Подготавливаем DTO для отправки
                        errorDtos.Add(new ErrorDto(
                            error.Id,
                            error.FileName,
                            error.Content,
                            error.LinePosition,
                            error.CreatedAt
                        ));
                    }
                }
            }
        }

        // Отправляем уведомления (вне DB scope)
        foreach (var dto in errorDtos)
        {
            await _notificationRouter.RouteErrorAsync(dto);
        }
    }
    
}