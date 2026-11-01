# 05. Подготовить DLL-поставку

Статус: `completed`.

## Цель

Подготовить DLL-поставку и проверить прямое подключение библиотеки из libs приложения без NuGet и ProjectReference.

## Зависимости

[Этап 04 — совместимость и приёмка](04-compatibility-and-acceptance.md).

## Работы

Подготовить версию и состав DLL-поставки, правила совместимости API/JSON и инструкцию копирования `UptimeHarbor.HealthEndpoint.dll` в libs целевого приложения. Использовать прямую assembly reference с HintPath и копированием в выходной каталог, без NuGet и ProjectReference. Проверить возможность поставки одной DLL без собственных сторонних зависимостей; ASP.NET Core предоставляет framework приложения. Общую Documentation не дублировать в `.csproj` только для навигации.

## Затрагиваемые части

`.csproj` в пределах метаданных DLL, README и инструкция интеграции. Файлы приложения-потребителя изменяются только по отдельному разрешению.

## Проверки

Отсутствие секретов и случайных зависимостей на соседний репозиторий; состав DLL-поставки и копирование сборки из libs в output/publish приложения без NuGet и ProjectReference. Фактическую сборку и проверку подключения проводить только по отдельному разрешению, без запуска production-приложения.

## Критерии приёмки

Подготовлена DLL и проверенная в разрешённом объёме инструкция прямого подключения из libs, записаны фактические результаты и состав поставки. NuGet pack и публикация не входят в объём.

## Результат

Подготовлена Release DLL версии 0.0.1; AssemblyVersion и FileVersion — 0.0.1.0. Метаданные явно закреплены в csproj и проверены в собранном файле. Файл находится в `UptimeHarbor.HealthEndpoint/artifacts/dll-distribution/Release/net10.0/UptimeHarbor.HealthEndpoint.dll` относительно корня репозитория. Размер — 13824 байта, SHA-256 — `ECFFEECF727C1FCCBCDC622AD28F67C05E72C7E1979B7C7654BB0D74A736380D`.

Состав output: UptimeHarbor.HealthEndpoint.dll, UptimeHarbor.HealthEndpoint.pdb и UptimeHarbor.HealthEndpoint.deps.json. В libs потребителя требуется только DLL; PDB необязателен для диагностики, библиотечный deps.json переносить не нужно. В production csproj нет PackageReference, ProjectReference или встроенных конфигурационных ресурсов. Все 16 assembly references принадлежат framework packs .NET/ASP.NET Core 10; deps.json содержит только саму библиотеку. Сторонние DLL, тестовые пакеты и проекты UptimeHarbor в поставку не входят.

Практическое подключение через DLL ранее подтверждено пользователем в CostVision.Web. Дополнительно статически прочитаны фактические файлы CostVision: `CostVision.Web.csproj` на net10.0 с Reference/HintPath `..\libs\UptimeHarbor.HealthEndpoint.dll` и Private=true, вызовы Add/Map и результаты прежних сборок. SHA-256 DLL в CostVision/libs, CostVision.Web/bin/Debug/net10.0 и CostVision.Web/artifacts/compile-check/Debug/net10.0 совпадает: `D63697FF75ADEEF9AD188E110A260D168649FF5D08E912AD182D07A69231EAD9`. Подключение этой библиотеки через NuGet/ProjectReference отсутствует. Это подтверждает копирование сборки из libs в output; отдельный проверочный проект и sample не создавались.

Инструкция прямой ссылки, состав поставки, обновление и правила совместимости API/JSON завершены в [Consumer integration](../../Technical%20documentation/Consumer%20integration.md). Выполнены локальный restore, отдельные Debug/Release сборки библиотеки и Debug-сборка тестов без ошибок и предупреждений. Microsoft Testing Platform: 93 успешных теста, упавших 0, пропущенных 0; телеметрия отключена.

CostVision и исходный uptime-harbor только прочитаны, их приложения и сборки агентом не запускались. Новая Release DLL в CostVision не копировалась; прежняя рабочая DLL и новая поставка имеют разные хеши. Publish output не проверялся: публикация, NuGet pack, deployment и Git-команды не выполнялись. Ограничения внешней приёмки этапа 04 сохранены без расширения заявленной матрицы платформ.

[Общий план](main.md) · [Открытые вопросы](open-questions.md)
