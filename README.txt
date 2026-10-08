ZooStav: страница животного зоопарка (вариант «Енот»)
=====================================================

Учебный проект: ASP.NET Core MVC, .NET 10, EF Core 10.
База данных: SQL Server или SQLite. Вход: cookie (сайт) и JWT (REST API).


1. ТРЕБОВАНИЯ
-------------
  .NET SDK 10.0 (https://dotnet.microsoft.com/download)
  SQL Server LocalDB/Express, либо ничего, если запускать в режиме SQLite.


2. ЗАПУСК
---------
Открыть терминал в папке с файлом ZooStav.sln.

  Режим SQLite (без установки SQL Server):
    Windows PowerShell:  $env:Database__Provider="Sqlite"; dotnet run --project src/ZooStav.Web
    Linux / macOS:       Database__Provider=Sqlite dotnet run --project src/ZooStav.Web

  Режим SQL Server (строка подключения в src/ZooStav.Web/appsettings.json):
    dotnet run --project src/ZooStav.Web

При первом запуске база создаётся автоматически, миграции применяются,
добавляются демо-данные (животное, фото, видео, дневник, донаты).

Docker (приложение + SQL Server):  docker compose up --build  →  http://localhost:8080


3. АДРЕСА
---------
  http://localhost:5045/                  главная зоопарка
  http://raccoon.localhost:5045/          страница животного на поддомене
  http://localhost:5045/raccoon           та же страница по пути
  http://localhost:5045/diary             дневник наблюдений (нужен вход)
  http://localhost:5045/donate/raccoon    донат на корм и лечение
  http://localhost:5045/Account/Login     вход и тестовые логины

На боевом домене: страница животного — https://raccoon.zoostav.ru/,
главная зоопарка — https://zoostav.ru (ссылка есть на странице животного).
Запрос zoostav.ru/raccoon перенаправляется на поддомен.

Поддомен работает в браузере без настроек, потому что *.localhost указывает на 127.0.0.1.


4. ЛОГИНЫ И ПАРОЛИ ДЛЯ ПРОВЕРКИ
---------------------------------
  Работник зоопарка (роль Staff):
    логин:   keeper@zoostav.ru
    пароль:  Keeper#2026

  Посетитель (роль Visitor):
    логин:   visitor@zoostav.ru
    пароль:  Visitor#2026

Новый посетитель регистрируется на странице /Account/Register (роль Visitor).


5. ПРАВА ДОСТУПА
----------------
  Без входа:  описание и история, фото, видео, веб-камера, донаты, игра.
  Visitor:    всё выше + просмотр дневника.
  Staff:      всё выше + создание, изменение и удаление записей дневника,
              запись в дневник через API.

Посетитель, пытающийся создать запись, получает страницу «Доступ запрещен» (в API — 403).
Без входа в API — 401.


6. ДНЕВНИК И ФИЛЬТРЫ
--------------------
Типы записей: кормление, вакцинация, спаривание, потомство, болезнь, лечение,
измерения, перемещение, наблюдение, прочее.

Фильтры (одинаковые на странице и в API):
  type        тип записи (Feeding, Vaccination, Mating, Offspring, Illness,
              Treatment, Measurement, Relocation, Observation, Other)
  dateFrom    дата «с», формат 2026-01-01
  dateTo      дата «по», формат 2026-12-31
  user        автор записи (логин или ФИО)
  search      текст в заголовке или описании
  sort        date_desc | date_asc | type | user | created_desc
  page, pageSize

Пример: http://localhost:5045/diary?type=Vaccination&dateFrom=2026-01-01


7. REST API
-----------
Авторизация: POST /api/auth/login, в ответе data.accessToken.
Дальше заголовок:  Authorization: Bearer <accessToken>

  POST   /api/auth/register                  регистрация
  POST   /api/auth/login                     вход, получение JWT
  POST   /api/auth/refresh                   обновление токена
  GET    /api/auth/me                        текущий пользователь (нужен токен)

  GET    /api/animals                        список животных
  GET    /api/animals/{slug}                 карточка животного
  GET    /api/animals/{slug}/media           фото и видео

  GET    /api/animals/{slug}/diary           дневник животного с фильтрами (открыт)
  POST   /api/animals/{slug}/diary           добавить запись (Staff)
  GET    /api/diary                          все записи с фильтрами (Staff)
  GET    /api/diary/{id}                     запись по номеру (Staff)
  PUT    /api/diary/{id}                     изменить запись (Staff)
  DELETE /api/diary/{id}                     удалить запись (Staff)
  GET    /api/diary/types                    справочник типов

  POST   /api/donations                      сделать донат (без входа)
  GET    /api/donations/{slug}               донаты животного
  GET    /api/donations/{slug}/summary       сводка по назначениям

Пример (Linux / macOS / Git Bash):
  TOKEN=$(curl -s -X POST http://localhost:5045/api/auth/login \
    -H "Content-Type: application/json" \
    -d '{"email":"keeper@zoostav.ru","password":"Keeper#2026"}' \
    | python3 -c "import sys,json;print(json.load(sys.stdin)['data']['accessToken'])")

  curl -X POST http://localhost:5045/api/animals/raccoon/diary \
    -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
    -d '{"type":"Feeding","title":"Вечернее кормление","description":"120 г рыбы"}'


8. ИГРА «ЕНОТ-ЗМЕЙКА»
---------------------
На странице животного, в правой колонке. Стрелки или WASD, на телефоне свайпы.
Рекорд сохраняется в браузере.


9. СТРУКТУРА ПРОЕКТА
--------------------
  ZooStav.sln                       решение
  src/ZooStav.Data/                 модели, DbContext, сервис дневника
  src/ZooStav.Web/                  MVC-страницы, REST API, представления, wwwroot
  src/ZooStav.Migrations.SqlServer/ миграции для SQL Server
  src/ZooStav.Migrations.Sqlite/    миграции для SQLite
  tests/ZooStav.Tests/              автотесты (запуск: dotnet test)


10. НАСТРОЙКИ ДОМЕНА И ПРОДАКШЕНА
---------------------------------
Файл src/ZooStav.Web/appsettings.json, раздел Zoo:
  RootDomain      zoostav.ru
  MainSiteUrl     https://zoostav.ru
  CookieDomain    .zoostav.ru (на боевом сервере, чтобы вход работал на всех поддоменах)

На сервере переопределить через переменные окружения:
  Jwt__Key                     длинный секрет (вместо демонстрационного)
  ConnectionStrings__SqlServer строка подключения к боевой базе
  ASPNETCORE_ENVIRONMENT       Production


11. ЕСЛИ БАЗА СОЗДАЛАСЬ БЕЗ ТАБЛИЦ
----------------------------------
Приложение в режиме разработки останавливается при ошибке инициализации БД
и пишет её причину в консоль. Если таблиц нет, а ошибка повторяется:
  1. Удалите базу ZooStavRaccoon (SQL Server Object Explorer в Visual Studio,
     SSMS или: sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "DROP DATABASE ZooStavRaccoon").
  2. Запустите приложение снова.
