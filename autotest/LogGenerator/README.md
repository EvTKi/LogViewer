# LogGenerator

Генератор лог-файлов для тестирования LogMonitor.

## Запуск

```bash
dotnet run -- --output-dir /tmp/logs --files 3 --lines-per-file 1000 --err-frequency 0.05
--output-dir — директория для логов (по умолчанию /tmp/logs)
--files — сколько файлов создать
--lines-per-file — строк в файле
--err-frequency — доля ошибок (0.05 = 5%)
```


