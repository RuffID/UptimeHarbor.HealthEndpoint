# Проект библиотеки

## Ответственность

Публичный API содержит `UptimeHarborHealthOptions` в namespace `UptimeHarbor.HealthEndpoint` и DI-расширение в `Microsoft.Extensions.DependencyInjection`. Регистрация не подключает маршруты и не запускает хост. Будущий Map относится к этапу 03.

## Внутренние границы

- `UptimeHarborHealthConfiguration` выполняет общую нормализацию имени и синтаксическую проверку Path без I/O.
- `UptimeHarborHealthOptionsValidator` проверяет options через `IValidateOptions` и выдаёт безопасные сообщения без значений конфигурации.
- `UptimeHarborHealthState` — singleton с неизменяемыми Name, Path и фактическим временем текущего процесса в UTC. После создания не читать изменяемые options для ответа.
- `UptimeHarborHealthStartupService` материализует снимок на фазе `IHostedLifecycleService.StartingAsync`, до запуска hosted services, в том числе HTTP listener приложения. `ValidateOnStart` проверяет options независимо от первого запроса.
- Время читать только через `Process.GetCurrentProcess().StartTime` с освобождением временного Process, без запуска процессов и fallback на UtcNow.
- Имя нормализовать одной общей функцией для валидации и снимка. Обрезка автоматического имени не должна разрывать surrogate pair; явное имя не обрезать.
- Проверять синтаксис Path, включая escape-последовательности, но не анализировать чужие endpoints или повторное подключение.
- Не добавлять Information-логи на регистрацию или обычное чтение. Ошибки options выражать через `OptionsValidationException`, без полного дампа настроек.

Тестовая сборка имеет доступ к internal через InternalsVisibleTo; это не делает снимок частью публичного API. Производственные зависимости ограничены framework reference на ASP.NET Core.
