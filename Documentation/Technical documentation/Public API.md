# Публичный API

**Состояние:** options, оба overload Add, Map и HTTP endpoint реализованы. Подготовлена Release DLL 0.0.1. Изолированные проверки пройдены на Windows x64 / .NET 10 / ASP.NET Core 10; DLL-подключение CostVision.Web подтверждено. Приёмка этапа 04 и её внешние ограничения записаны в завершённом плане.

## Согласованные точки подключения

Минимальный вариант в существующем ASP.NET Core приложении:

```csharp
builder.Services.AddUptimeHarborHealth();

app.MapUptimeHarborHealth();
```

С переопределением настроек:

```csharp
builder.Services.AddUptimeHarborHealth(options =>
{
    options.Name = "orders-api";
    options.Path = "/health";
});

app.MapUptimeHarborHealth();
```

Регистрация выполняется до `builder.Build()`, mapping — после создания `app` и до запуска приложения. Примеры не заменяют существующий Program.cs и не требуют второго HTTP-хоста.

## Закреплённая форма API

| Элемент | Контракт и состояние |
| --- | --- |
| `AddUptimeHarborHealth` | Расширение `IServiceCollection`, возвращающее `IServiceCollection`; вариант без аргументов и вариант с делегатом настройки options. |
| Options | Реализованный `UptimeHarbor.HealthEndpoint.UptimeHarborHealthOptions`: `string? Name`, `string Path` со значением `/health`. |
| `MapUptimeHarborHealth` | Расширение `IEndpointRouteBuilder`, возвращающее `IEndpointConventionBuilder`; путь берётся из неизменяемого снимка валидированной конфигурации. |

Реализованные сигнатуры находятся в `Microsoft.Extensions.DependencyInjection`, класс `UptimeHarborHealthServiceCollectionExtensions`:

```csharp
public static IServiceCollection AddUptimeHarborHealth(this IServiceCollection services);
public static IServiceCollection AddUptimeHarborHealth(
    this IServiceCollection services,
    Action<UptimeHarborHealthOptions> configureOptions);
```

Реализованная сигнатура Map находится в namespace `Microsoft.AspNetCore.Builder`, класс `UptimeHarborHealthEndpointRouteBuilderExtensions`:

```csharp
public static IEndpointConventionBuilder MapUptimeHarborHealth(
    this IEndpointRouteBuilder endpoints);
```

Это описание сигнатур, не отдельный исходный файл. Builder поддерживает стандартные conventions. Завершающая convention библиотеки добавляет anonymous metadata и запрет output cache после обычных conventions. Произвольные дополнительные финальные conventions/замена RequestDelegate требуют проверки приложения. Map без Add выбрасывает InvalidOperationException с безопасным сообщением `Call AddUptimeHarborHealth before MapUptimeHarborHealth.`, не регистрируя зависимости; null builder вызывает ArgumentNullException.

Add возвращает исходный IServiceCollection. Null services и null делегат явного overload вызывают ArgumentNullException. Значения Name/Path проверяются через IValidateOptions и ValidateOnStart; ошибки сообщаются OptionsValidationException без исходных значений. Нормализованные имя/путь и время процесса фиксируются во внутреннем singleton при Map либо не позднее StartingAsync. Options не изменяются нормализацией и не предназначены для runtime reload.

Согласован однократный вызов Add и однократный Map, причём Add должен предшествовать Map. Повторные вызовы — ответственность разработчика приложения; специальной проверки библиотека не выполняет. Конфликты с маршрутами приложения также не обнаруживаются. Для выбора свободного адреса служит Path.

Маршрут подключается через endpoint routing без обязательного MVC controller. Один Add не публикует endpoint. Внутренний запрет Minimal API в Web-проекте исходного UptimeHarbor здесь не действует.

## HTTP-контракт библиотеки

| Свойство | Согласованное поведение |
| --- | --- |
| Метод и доступ | Анонимный GET без перенаправления на вход. |
| Путь | `/health` по умолчанию либо валидированный пользовательский путь. |
| Статус и тип | `200 OK`, `application/json` (допустимо уточнение charset). |
| `name` | JSON-строка, имя приложения по правилам [настроек](Configuration%20and%20validation.md). |
| `startedAtUtc` | JSON-строка с временем запуска текущего процесса в UTC с `+00:00`; checker принимает также `Z`. |
| Кеш | `Cache-Control: no-store` для response cache; отдельная IOutputCachePolicy запрещает output cache lookup/storage/locking. |
| Данные | Ровно два публичных поля name/startedAtUtc; без Path, секретов, параметров подключений и подробностей исключений. |

```json
{
  "name": "orders-api",
  "startedAtUtc": "2026-11-01T03:20:00+00:00"
}
```

Форма ответа не зависит от глобального snake_case/PascalCase, настроек MVC или пользовательского converter даты. Используются отдельные read-only JsonSerializerOptions библиотеки и JsonPropertyName в модели, без наследования или изменения настроек приложения. Изоляция и сохранение обычного ответа приложения подтверждены тестами. Проверка потребителя регистрозависима, envelope вида `{ "data": ... }` не подходит.

Для других HTTP-методов отдельного продуктового контракта нет: MapGet с RequestDelegate регистрирует только GET. Без fallback authorization HEAD/POST/PUT дают стандартные 405 и Allow: GET; при fallback policy неподходящий метод может получить отказ авторизации приложения. Библиотека подтверждает только способность приложения отвечать; проверки готовности БД и других зависимостей не поддерживаются.

## Ошибки и совместимость

Ошибочные options не должны откладываться до первого мониторингового запроса: явный Name вне 3–256 после Trim и некорректный Path вызывают ошибку запуска. Автоматическое имя длиннее 256 обрезается до предела. Проверки конфликтов маршрутов нет; ответственность приложения — в [валидации](Configuration%20and%20validation.md#route-conflicts). Библиотека не перехватывает все исключения приложения и не превращает ошибки запуска в фиктивный успешный health.

Минимальная платформа — .NET 10 / ASP.NET Core 10, цель — `net10.0`. .NET 8 и 9 не поддерживаются; более новые версии требуют подтверждения совместимости. Доставка — DLL версии 0.0.1 прямой ссылкой из libs, без NuGet и ProjectReference. Состав поставки и правила версионирования — в [Consumer integration](Consumer%20integration.md#версия-совместимость-и-обновление). Изменение состава/имён/типов полей или смысла времени является несовместимым изменением JSON-контракта.
