# UptimeHarbor.HealthEndpoint

Библиотека для подключения HTTP endpoint мониторинга UptimeHarbor к существующему ASP.NET Core приложению внутри контейнера или вне Docker.

**Текущее состояние на 2026-11-01:** этапы 01–05 завершены в согласованном объёме. Реализован endpoint, подготовлена Release DLL 0.0.1 и инструкция подключения. Прямая DLL-ссылка и копирование в output подтверждены файлами CostVision.Web; ограничения пользовательской приёмки 04 сохранены в плане. Общий статус — `completed`.

## Состав решения

- `UptimeHarbor.HealthEndpoint.slnx` — решение с проектом библиотеки, изолированными тестами и документами как solution items.
- `UptimeHarbor.HealthEndpoint/UptimeHarbor.HealthEndpoint.csproj` — SDK-style class library на `net10.0` с framework reference на `Microsoft.AspNetCore.App`, без production NuGet-зависимостей.
- `tests/UptimeHarbor.HealthEndpoint.Tests/` — изолированные проверки настроек, middleware, JSON и фактического checker через xUnit v3 / Microsoft Testing Platform. Для тестовой компиляции нужны восемь исходных файлов соседнего `../uptime-harbor`; production-проект от него не зависит.
- [Документация](Documentation/README.md) — согласованные требования, подтверждённый контракт потребителя, API и архитектура.
- [План реализации](Documentation/Plans/initial-implementation-2026-11-01-(completed)/main.md) и [открытые вопросы](Documentation/Plans/initial-implementation-2026-11-01-(completed)/open-questions.md).
- [AGENTS.md](AGENTS.md) — границы работы и ответственность каталогов.

Минимальная платформа — .NET 10 / ASP.NET Core 10, цель проекта — `net10.0`. Версии 8 и 9 не поддерживаются; более новые версии требуют проверки. Готовая [Release DLL 0.0.1](UptimeHarbor.HealthEndpoint/artifacts/dll-distribution/Release/net10.0/UptimeHarbor.HealthEndpoint.dll) копируется в libs приложения и подключается прямой ссылкой, без NuGet и ProjectReference. Для работы достаточно одной DLL; PDB необязателен, библиотечный deps.json не нужен. Собственных сторонних production-зависимостей нет.

## Регистрация и подключение маршрута

Регистрация зависимостей уже реализована и вызывается до Build:

```csharp
builder.Services.AddUptimeHarborHealth();
```

После создания app и до его запуска:

```csharp
app.MapUptimeHarborHealth();
```

Один Add не подключает маршрут; Map требует Add и использует существующий pipeline, не создавая listener. Подробности сигнатур — в [публичном API](Documentation/Technical%20documentation/Public%20API.md).

Анонимный GET на `/health` возвращает `200 OK`, `application/json`, ровно `name` и `startedAtUtc` с неизменным временем запуска процесса в UTC (+00:00). Отдельные JSON-настройки сохраняют контракт; no-store и endpoint policy исключают стандартные response/output caches. Настройки порта нет: используется HTTP pipeline приложения. Docker API, Docker socket, БД и отдельный агент не нужны.

Name необязательно: автоматическое имя берётся из `IHostEnvironment.ApplicationName` и при превышении лимита обрезается до 256 символов. Явное имя после Trim должно содержать 3–256 символов, иначе приложение не запускается. JSON использует отдельные настройки библиотеки. Проверки готовности зависимостей, обнаружение конфликтов маршрутов и специальная защита от повторного Add/Map не предусмотрены; свободный Path и однократное подключение обеспечивает разработчик приложения.

[Инструкция интеграции](Documentation/Technical%20documentation/Consumer%20integration.md) содержит проверенную DLL-ссылку, состав поставки, версии и обновление, настройку имени/пути и пример существующего nginx. Практическое подключение подтверждено CostVision; новая Release DLL туда автоматически не копировалась.

## Выполненные проверки

Выполнены restore из локального кеша, отдельные сборки библиотеки и тестового проекта без ошибок и предупреждений. Microsoft Testing Platform: 93 успешных теста, упавших 0, пропущенных 0; телеметрия отключена. Проверены настройки/снимок, настоящий middleware в памяти, независимость JSON, положительный контроль кеша и фактический checker server/container с подставным handler, включая размер 64 KiB. Также проверены XML, ссылки, solution items и UTF-8.

В этапе 05 отдельно собраны Debug/Release DLL и тестовый проект без ошибок/предупреждений, повторно пройдены все 93 теста. В метаданных Release DLL проверены версия и 16 framework references. В существующих файлах CostVision подтверждены Reference из libs с Private=true и одинаковые SHA-256 DLL в libs и двух выходных папках; CostVision не изменялся и не пересобирался. Отдельный sample/проверочный проект не создавался.

Пользователь принял этап 04 по JSON CostVision.Web и стабильности времени при обновлении страницы. Это не подтверждает перезапуск процесса, HTTP-заголовки/анонимность/redirect/отсутствие внешнего кеша, nginx/TLS, доступность наблюдателям, стабильный адрес реплики или Linux. Ограничения сохранены в плане. Агент не запускал приложение/listener; NuGet pack, публикация, deployment и изменение соседнего uptime-harbor не выполнялись.
