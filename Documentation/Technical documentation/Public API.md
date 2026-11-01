# Публичный API

**Состояние:** options и оба overload Add реализованы в этапах 01/02. Map и HTTP endpoint пока отсутствуют. Примеры с Map описывают будущий этап 03 и целиком пока не компилируются.

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
| `MapUptimeHarborHealth` | Будущее расширение `IEndpointRouteBuilder`, возвращающее `IEndpointConventionBuilder`; путь берётся из неизменяемого снимка валидированной конфигурации. Не реализовано. |

Реализованные сигнатуры находятся в `Microsoft.Extensions.DependencyInjection`, класс `UptimeHarborHealthServiceCollectionExtensions`:

```csharp
public static IServiceCollection AddUptimeHarborHealth(this IServiceCollection services);
public static IServiceCollection AddUptimeHarborHealth(
    this IServiceCollection services,
    Action<UptimeHarborHealthOptions> configureOptions);
```

Будущая сигнатура этапа 03 находится в namespace `Microsoft.AspNetCore.Builder`, класс `UptimeHarborHealthEndpointRouteBuilderExtensions`:

```csharp
public static IEndpointConventionBuilder MapUptimeHarborHealth(
    this IEndpointRouteBuilder endpoints);
```

Это описание сигнатур, не отдельный исходный файл. Builder mapping предусматривает стандартные conventions; гарантии anonymous и фиксированного контракта не должны незаметно разрушаться внешними conventions. Map без Add должен давать понятную ошибку, а не скрыто регистрировать зависимости; эта проверка относится к ещё не реализованному Map.

Add возвращает исходный IServiceCollection. Null services и null делегат явного overload вызывают ArgumentNullException. Значения Name/Path проверяются через IValidateOptions и ValidateOnStart; ошибки сообщаются OptionsValidationException без исходных значений. Нормализованные имя/путь и время процесса фиксируются во внутреннем singleton на StartingAsync. Options не изменяются нормализацией и не предназначены для runtime reload.

Согласован однократный вызов Add и однократный Map, причём Add должен предшествовать Map. Повторные вызовы — ответственность разработчика приложения; специальной проверки библиотека не выполняет. Конфликты с маршрутами приложения также не обнаруживаются. Для выбора свободного адреса служит Path.

Способ подключения маршрута закреплён как endpoint routing без обязательного MVC controller. Map ещё не создан; один Add не публикует endpoint. Внутренний запрет Minimal API в Web-проекте исходного UptimeHarbor здесь не действует.

## HTTP-контракт библиотеки

| Свойство | Согласованное поведение |
| --- | --- |
| Метод и доступ | Анонимный GET без перенаправления на вход. |
| Путь | `/health` по умолчанию либо валидированный пользовательский путь. |
| Статус и тип | `200 OK`, `application/json` (допустимо уточнение charset). |
| `name` | JSON-строка, имя приложения по правилам [настроек](Configuration%20and%20validation.md). |
| `startedAtUtc` | JSON-строка с временем запуска текущего процесса в UTC; `Z` или `+00:00` принимаются существующим checker. |
| Кеш | Ответ не кешируется; проектируемый заголовок — `Cache-Control: no-store`, с учётом output/response cache приложения. |
| Данные | Только публичные имя и время; без секретов, параметров подключений и подробностей исключений. |

```json
{
  "name": "orders-api",
  "startedAtUtc": "2026-11-01T03:20:00Z"
}
```

Форма ответа не должна зависеть от глобального snake_case/PascalCase, настроек MVC или пользовательского converter даты. Согласованы отдельные настройки System.Text.Json только для endpoint, без наследования или изменения настроек приложения. Они должны сохранять точные поля и явное UTC-время. Проверка потребителя регистрозависима, envelope вида `{ "data": ... }` не подходит. Реализация сериализации пока отсутствует.

Для других HTTP-методов отдельного продуктового контракта пока нет; будущая реализация должна явно проверить и документировать стандартное поведение выбранного mapping. Библиотека подтверждает только способность приложения отвечать; проверки готовности БД и других зависимостей не поддерживаются в согласованном объёме.

## Ошибки и совместимость

Ошибочные options не должны откладываться до первого мониторингового запроса: явный Name вне 3–256 после Trim и некорректный Path вызывают ошибку запуска. Автоматическое имя длиннее 256 обрезается до предела. Проверки конфликтов маршрутов нет; ответственность приложения — в [валидации](Configuration%20and%20validation.md#route-conflicts). Библиотека не перехватывает все исключения приложения и не превращает ошибки запуска в фиктивный успешный health.

Минимальная платформа — .NET 10 / ASP.NET Core 10, цель — `net10.0`. .NET 8 и 9 не поддерживаются; более новые версии требуют подтверждения совместимости. Доставка — DLL прямой ссылкой из `libs`, без NuGet и ProjectReference. Правила версионирования уточняются при подготовке DLL-поставки; изменение имён полей и смысла времени потенциально несовместимо с существующим потребителем.
