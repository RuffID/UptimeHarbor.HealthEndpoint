# Изолированные тесты библиотеки

Тестовый проект на .NET 10 использует xUnit v3 и Microsoft Testing Platform. ProjectReference здесь служит только разработке и проверке библиотеки; производственное приложение подключает DLL из libs.

- NameConfigurationTests проверяют нормализацию, длины и Unicode; PathConfigurationTests — синтаксис пути и безопасные ошибки; StartupStateTests — инициализацию и неизменность сведений о процессе.
- HealthTestServices собирает только DI container и вручную вызывает startup validator и фазу StartingAsync. Не строить и не запускать Generic Host/WebApplication, HTTP listener, приложение, Docker, внешние процессы, сеть или БД.
- EndpointTestPipeline собирает ApplicationBuilder с настоящими routing/authorization/response-cache/output-cache middleware и вызывает RequestDelegate на DefaultHttpContext. Это не хост и не HTTP transport. Каждый вызов получает отдельный DI scope. Кеширующие проверки должны содержать обычный маршрут, который действительно кешируется, и счётчик выполнения health.
- EndpointJsonTests проверяет изоляцию от HTTP/MVC настроек и сохранение пользовательской сериализации обычного ответа. EndpointMappingTests проверяет также route group, PathBase и кодированный путь.
- ConsumerCompatibilityTests компилирует восемь точечных linked source файлов фактического checker и его чистых контрактов из соседнего uptime-harbor. Не заменять checker копией алгоритма и не добавлять production reference; отсутствие исходников должно останавливать сборку этих тестов. Соседний репозиторий только читать, не собирать и не изменять.
- ConsumerCheckerHarness подставляет HttpMessageHandler с локальным HttpContent. HttpClient не получает сетевого handler; CreatePrimaryHandler checker не вызывается. Проверки длины должны охватывать Content-Length и тело без объявленной длины, включая отрицательные примеры.
- TestHostEnvironment использует NullFileProvider. Разрешено чтение времени текущего тестового процесса; новые процессы не создавать.
- TestEnvironment отключает телеметрию Microsoft Testing Platform в текущем процессе до запуска runner; не удалять эту настройку из изолированного набора.
- Использовать AAA-комментарии в тестах; helpers не требуют AAA.
- Прямой вызов lifecycle не доказывает реальный перезапуск ОС-процесса. Вызовы middleware в памяти подтверждают только проверенную композицию ASP.NET Core 10, без доступности реального endpoint, Linux, nginx, TLS, redirect или конкретного pipeline потребителя.
- Тестовые NuGet-зависимости не входят в DLL-поставку библиотеки. Restore из локального источника, compile-check и выполнение изолированного набора допустимы по действующему пользовательскому исключению для C#/.NET, после статической проверки проектов; не запрашивать разрешение повторно.
