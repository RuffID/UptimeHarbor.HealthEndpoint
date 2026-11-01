# UptimeHarbor.HealthEndpoint

Библиотека для подключения HTTP endpoint мониторинга UptimeHarbor к существующему ASP.NET Core приложению внутри контейнера или вне Docker.

**Текущее состояние на 2026-11-01:** этапы 01/02 реализованы: технический контракт, options, Add, стартовая валидация и неизменяемые сведения о процессе. Общий статус — `in-progress`. HTTP endpoint, Map и JSON-ответ пока отсутствуют и относятся к этапу 03.

## Состав решения

- `UptimeHarbor.HealthEndpoint.slnx` — решение с проектом библиотеки, изолированными тестами и документами как solution items.
- `UptimeHarbor.HealthEndpoint/UptimeHarbor.HealthEndpoint.csproj` — SDK-style class library на `net10.0` с framework reference на `Microsoft.AspNetCore.App`, без production NuGet-зависимостей.
- `tests/UptimeHarbor.HealthEndpoint.Tests/` — проверки настроек и снимка процесса через xUnit v3 / Microsoft Testing Platform.
- [Документация](Documentation/README.md) — согласованные требования, подтверждённый контракт потребителя, предлагаемые API и архитектура.
- [План реализации](Documentation/Plans/initial-implementation-2026-11-01-(in-progress)/main.md) и [открытые вопросы](Documentation/Plans/initial-implementation-2026-11-01-(in-progress)/open-questions.md).
- [AGENTS.md](AGENTS.md) — границы работы и ответственность каталогов.

Минимальная платформа — .NET 10 / ASP.NET Core 10, цель проекта — `net10.0`. Версии 8 и 9 не поддерживаются; совместимость с более новыми версиями требует проверки. Библиотека и тесты собраны на Windows x64/.NET 10 без ошибок и предупреждений. Будущая поставка — `UptimeHarbor.HealthEndpoint.dll`, копируемая в `libs` приложения и подключаемая прямой ссылкой, без NuGet и ProjectReference. Полная приёмка такой поставки относится к этапу 05.

## Регистрация и будущее подключение

Регистрация зависимостей уже реализована и вызывается до Build:

```csharp
builder.Services.AddUptimeHarborHealth();
```

Будущий `app.MapUptimeHarborHealth()` ещё не реализован. Один Add не подключает маршрут и не создаёт HTTP listener. Подробности текущих сигнатур и будущего mapping — в [публичном API](Documentation/Technical%20documentation/Public%20API.md).

По требованиям анонимный GET на `/health` будет возвращать `200 OK`, `application/json`, имя приложения и неизменное в пределах процесса время его запуска в UTC. Настройки порта в библиотеке нет: используется HTTP pipeline приложения. Docker API, Docker socket, БД и отдельный агент не нужны.

Name необязательно: автоматическое имя берётся из `IHostEnvironment.ApplicationName` и при превышении лимита обрезается до 256 символов. Явное имя после Trim должно содержать 3–256 символов, иначе приложение не запускается. JSON использует отдельные настройки библиотеки. Проверки готовности зависимостей, обнаружение конфликтов маршрутов и специальная защита от повторного Add/Map не предусмотрены; свободный Path и однократное подключение обеспечивает разработчик приложения.

[Будущая инструкция подключения](Documentation/Technical%20documentation/Consumer%20integration.md) описывает также настройку имени, пути и публикацию через существующий nginx. Перечисленные там проверки ещё не выполнялись.

## Выполненные проверки

Выполнены разрешённый restore из локального кеша, отдельная сборка библиотеки и тестового проекта, 57 изолированных проверок: успешно 57, ошибок и пропусков 0. Проверяются длины/Unicode имени, Path, безопасные ошибки, StartingAsync и снимок реального времени текущего процесса. Также проверены структура, XML, ссылки, solution items и UTF-8.

Хост и HTTP listener не запускались. Endpoint, nginx, сеть, Linux, реальный перезапуск приложения и DLL-поставка в другое приложение не проверены. Упаковка, публикация и Git-операции в рамках реализации не выполнялись.
