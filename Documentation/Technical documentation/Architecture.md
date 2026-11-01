# Архитектура

## Фактическое состояние

На 2026-11-01 реализованы этапы 01/02. Проект `UptimeHarbor.HealthEndpoint` — SDK-style class library на `net10.0` с framework reference `Microsoft.AspNetCore.App`. Существуют публичные options и DI-расширение, внутренние валидатор, нормализатор, singleton-снимок процесса и startup lifecycle service. Изолированный тестовый проект находится в `tests/UptimeHarbor.HealthEndpoint.Tests`. Общий статус — `in-progress`; endpoint, Map, сериализация ответа и samples отсутствуют.

TargetFramework соответствует текущим `UptimeHarbor.Web.csproj` и `UptimeHarbor.Infrastructure.csproj` и согласованной минимальной платформе .NET 10 / ASP.NET Core 10. Версии 8 и 9 не поддерживаются; более новые версии требуют отдельного подтверждения совместимости. Сборка и изолированные тесты подтверждены на Windows x64/.NET 10. Зависимостей production-проекта на сам UptimeHarbor и сторонних NuGet-пакетов нет. Тестовый xUnit не входит в поставку. Согласованная поставка — DLL с прямой ссылкой из `libs` приложения, без NuGet и ProjectReference.

<a id="consumer-contract"></a>
## Подтверждённый контракт существующего потребителя

Источники прочитаны статически 2026-11-01; дальнейшие изменения соседнего репозитория требуют повторной сверки:

- [HttpMonitoringEndpointChecker.cs](../../../uptime-harbor/UptimeHarbor.Infrastructure/Monitoring/HttpMonitoringEndpointChecker.cs).
- [ModelConstraints.cs](../../../uptime-harbor/UptimeHarbor.Domain/ModelConstraints.cs).
- [Monitoring.md](../../../uptime-harbor/Documentation/Technical%20documentation/Monitoring.md).
- [ASP.NET Core container monitoring.md](../../../uptime-harbor/Documentation/Technical%20documentation/ASP.NET%20Core%20container%20monitoring.md).

| Область | Фактическое поведение кода UptimeHarbor |
| --- | --- |
| Запрос | HTTP GET без заголовка авторизации; автоматические redirect отключены. |
| Статус | Принимается любой `2xx`, затем проверяется тело. Любой `3xx` — ошибка. Библиотеке согласован именно `200 OK`. |
| Тип тела | Парсится JSON; HTTP Content-Type в checker не проверяется. Требование библиотеки `application/json` остаётся обязательным. |
| Размер | Максимум 64 KiB (65536 байт): проверяются Content-Length и фактически прочитанные байты, включая streamed/chunked body. |
| Корень и поля | Корень — объект; точные имена `name` и `startedAtUtc` регистрозависимы, оба значения — JSON-строки. Посторонние поля checker не проверяет. |
| Имя | `Trim()`, затем непустое значение длиной не более `OBJECT_NAME_MAX_LENGTH = 256`. Минимум 3 для явного Name — требование библиотеки; код потребителя принимает также 1–2 символа. |
| Время | Строка оканчивается на `Z` (проверка окончания регистронезависима) либо `+00:00`, успешно разбирается `DateTimeOffset.TryParse` с InvariantCulture/RoundtripKind, имеет offset 0 и не позже времени завершения проверки. |
| Отказ | Отсутствие зоны, ненулевое смещение, время в будущем и неверный тип поля отвергаются. Сетевые ошибки, timeout, неверный JSON и слишком большое тело означают неудачную проверку. |

Примеры принимаемого времени: `2026-11-01T03:20:00Z`, `2026-11-01T03:20:00+00:00`, если они не позже часов проверяющего узла. Канонический формат будущего ответа библиотеки следует выбрать в UTC с явным окончанием; не нужно воспроизводить всю допустимую грамматику TryParse.

Checker общий для server/container. Для этой библиотеки согласовано время процесса приложения, соответствующее наблюдению приложения/контейнера; семантику времени загрузки ОС из server-инструкции сюда не переносить. Часы приложения и наблюдателя должны быть синхронизированы, библиотека не подменяет время запуска ради обхода проверки будущего времени.

### Обнаруженные неточности исходной документации

1. В разделе «Цикл» исходного Monitoring.md указан интервал «каждые 30 секунд». [MonitoringWorker.cs](../../../uptime-harbor/UptimeHarbor.Infrastructure/Monitoring/MonitoringWorker.cs) берёт интервал из `MonitoringOptions.IntervalSeconds`; 30 секунд — значение [примера конфигурации](../../../uptime-harbor/UptimeHarbor.Web/Config/config.example.json), а не фиксированная константа worker.
2. Инструкция ASP.NET Core container monitoring.md обозначает обычный режим как `Cluster.Enabled = false`. В [ClusterOptions.cs](../../../uptime-harbor/UptimeHarbor.Infrastructure/Cluster/ClusterOptions.cs) `Enabled` — вычисляемое свойство `Mode != "Disabled"`; фактическая настройка — `Cluster:Mode = Disabled`.

В проверенной части JSON-контракта противоречий между исходной документацией и checker не обнаружено. Указания `200` и `application/json` в инструкции являются требованиями к предлагаемому endpoint; фактический checker принимает все `2xx` и не валидирует Content-Type. Исходные файлы не изменялись.

## Реализованная конфигурационная часть

Один проект библиотеки с ASP.NET Core framework reference. Разделять его на Domain/Application/Infrastructure или вводить Unit of Work не требуется. Ограничение исходного Web-проекта на Minimal API к независимой библиотеке не применяется.

`UptimeHarborHealthServiceCollectionExtensions` в namespace `Microsoft.Extensions.DependencyInjection` предоставляет два Add-overload, регистрирует options с `ValidateOnStart`, singleton-валидатор и `UptimeHarborHealthState`. Публичный `UptimeHarborHealthOptions` находится в `UptimeHarbor.HealthEndpoint`. `UptimeHarborHealthConfiguration` совместно используется валидатором и снимком для нормализации имени; Path проверяется синтаксически без обращения к маршрутам.

`UptimeHarborHealthStartupService` реализует `IHostedLifecycleService`. В StartingAsync он разрешает singleton-снимок из DI: options проходят валидацию, фиксируются Name/Path и читается старт текущего процесса. Фаза StartingAsync предшествует StartAsync всех hosted services, включая HTTP listener приложения. Сам Add только регистрирует сервисы, не запускает хост и не подключает middleware или маршруты. Lifecycle service не выполняет фоновый цикл.

Нормализованные Name и Path сохраняются в снимке; исходные публичные options не изменяются. Изменение options после материализации снимка не меняет будущий ответ. Тестам открыт internal через InternalsVisibleTo; снимок не является частью публичного API DLL.

## Проектируемое подключение HTTP endpoint

Будущий `MapUptimeHarborHealth` размещается в namespace `Microsoft.AspNetCore.Builder`, принимает `IEndpointRouteBuilder` и возвращает `IEndpointConventionBuilder`. Он будет получать готовый снимок из DI, подключать GET и использовать отдельные настройки System.Text.Json для фиксированного ответа. Точные публичные сигнатуры закреплены в [Public API](Public%20API.md); реализации Map пока нет.

Маршрут проектируется через endpoint routing без обязательного MVC controller и вызова `AddControllers`. Внутренняя архитектура UptimeHarbor не определяет этот выбор.

Add и Map вызываются приложением по одному разу, Add предшествует Map. Специальной защиты от повторного подключения и обнаружения конфликтов маршрутов библиотека не создаёт. Разработчик приложения выбирает свободный Path; библиотека не заменяет чужие endpoints и не выбирает другой путь автоматически. Подробности — в [конфигурации](Configuration%20and%20validation.md#route-conflicts).

Options pattern уже используется для настройки; прямого чтения `IConfiguration` нет. Значения фиксируются и валидируются при запуске; динамическое изменение пути и имени не поддерживается. Нормализация и политика автоматического имени описаны в [конфигурации](Configuration%20and%20validation.md).

## Жизненный цикл времени запуска

`UptimeHarborHealthState` один раз получает фактическое время старта текущего процесса через `Process.GetCurrentProcess().StartTime`, переводит в UTC и сохраняет в get-only свойстве StartedAtUtc. Временный Process освобождается через using; библиотека не владеет жизненным циклом процесса или HTTP-хоста.

Чтение происходит при материализации singleton, принудительно на StartingAsync, до обслуживания запросов. Даже при поздней инициализации возвращается начало процесса, а не UtcNow регистрации. Два DI container в одном тестовом процессе дают одинаковое StartedAtUtc; повторное разрешение из одного container возвращает тот же снимок. Реальный перезапуск приложения пока не проверялся.

Win32Exception, NotSupportedException и InvalidOperationException чтения старта преобразуются в понятную InvalidOperationException без fallback на текущее время. Время процесса подтверждено на Windows x64/.NET 10; другие ОС и отказ доступа к Process не воспроизводились. Будущий endpoint должен читать только готовый снимок.

## HTTP, авторизация и кеширование

Обязательны анонимный GET, `200 OK`, `application/json`, точные поля и UTC. Предлагается явная anonymous metadata для стандартной ASP.NET Core authorization, заголовок `Cache-Control: no-store` и исключение endpoint из применяемого response/output caching. Одного атрибута anonymous или заголовка недостаточно для гарантии поведения произвольного middleware и внешнего proxy.

Для JSON endpoint согласованы отдельные настройки System.Text.Json, независимые от настроек MVC/HTTP JSON приложения. Библиотека не использует и не изменяет глобальные options сериализации; naming policies и converters приложения не должны менять поля или время. Собственные настройки задают точные `name`/`startedAtUtc` и UTC с явным окончанием `Z` или `+00:00`. Этот способ выбран, но ещё не реализован и требует контрактных тестов.

Не логировать каждый polling-запрос на Information. Для ошибок запуска использовать безопасную диагностику настройки без полного дампа конфигурации и секретов; сообщения логов на английском. Не добавлять отдельный аудит или логировать успех запуска до завершения валидации. Детали исключений не отдавать публично.

## Границы решения

Порты, listeners, TLS, reverse proxy, firewall, свободный маршрут, однократное подключение, маршрутизация к конкретной реплике и порядок собственных middleware принадлежат приложению. Библиотека не делает исходящих запросов, не опрашивает Docker и не обращается к БД. Согласован только ответ самого приложения; readiness-проверки зависимостей в эту версию не входят.

Выполнены restore из локального кеша, отдельная сборка библиотеки и тестового проекта с нулём предупреждений/ошибок и 57 успешных изолированных тестов через Microsoft Testing Platform (ошибок и пропусков 0). Тесты вручную вызывают IStartupValidator/StartingAsync без запуска Generic Host или WebApplication. Телеметрия runner отключена module initializer тестового проекта.

HTTP endpoint, реальный запуск хоста, Linux, nginx, внешняя сеть, реальный перезапуск процесса и подключение DLL в другое приложение не проверялись. Исходный UptimeHarbor оставлен без изменений.
