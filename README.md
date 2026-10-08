# ZooStav — страница животного зоопарка (вариант «Енот»)

Учебный веб-приложение на **ASP.NET Core MVC (net8.0)**: персональная страница животного зоопарка
на **поддомене** сайта зоопарка, с аутентификацией/авторизацией (cookie + JWT), REST API,
дневником наблюдений с фильтрацией и приёмом донатов.

* Корневой домен зоопарка: **https://zoostav.ru** (главная страница зоопарка — внешний сайт)
* Страница животного (поддомен): **https://raccoon.zoostav.ru/**
* Локальная проверка поддомена: **http://raccoon.localhost:5045/**
* Реализация: `https://github.com/Siles9/ASP.NET.git`

---

## 1. Логины и пароли для проверки

Учётные записи создаются автоматически при первом запуске (инициализация БД).

| Роль | Логин (e-mail) | Пароль | Кто это | Что может |
|---|---|---|---|---|
| **Staff** (работник зоопарка) | `keeper@zoostav.ru` | `Keeper#2026` | Смотритель вольера Енотов | Всё, что посетитель, **плюс** ведение дневника (создание/правка/удаление записей), журнал действий, управление ролями, запись через API |
| **Visitor** (посетитель) | `visitor@zoostav.ru` | `Visitor#2026` | Посетитель Иван | Текстовое описание и история особи, фото, видео, веб-камера, донаты, просмотр дневника. Запись в дневник — **запрещена** (403) |
| Регистрация | любая почта | любой пароль от 6 символов | Новый посетитель | После регистрации выдаётся роль `Visitor` |

Те же данные есть прямо в приложении: страница **`/Account/Demo`** («Тестовые логины для проверки»).

Пример получения JWT для API:

```bash
curl -X POST http://localhost:5045/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"keeper@zoostav.ru","password":"Keeper#2026"}'
```

---

## 2. Быстрый старт

### 2.1. Требования

* .NET SDK 8.0
* SQL Server (LocalDB/Express/Developer) — **или** ничего дополнительно, если запускать в режиме SQLite

### 2.2. Вариант А. SQL Server (основной режим)

```bash
git clone https://github.com/Siles9/ASP.NET.git
cd ASP.NET

# строка подключения — в src/ZooStav.Web/appsettings.json
# по умолчанию: Server=(localdb)\MSSQLLocalDB;Database=ZooStavRaccoon;Trusted_Connection=True
dotnet run --project src/ZooStav.Web
```

Первый запуск сам создаёт базу (миграции применяются автоматически) и наполняет её демо-данными.

### 2.3. Вариант Б. SQLite (если SQL Server не установлен)

```bash
dotnet run --project src/ZooStav.Web --launch-profile "ZooStav.Web (SQLite — без установки SQL Server)"
```

или без профиля запуска:

```bash
# Linux / macOS
Database__Provider=Sqlite dotnet run --project src/ZooStav.Web

# Windows PowerShell
$env:Database__Provider="Sqlite"; dotnet run --project src/ZooStav.Web
```

Оба варианта дают одинаковый функционал: всё работает через EF Core, отличается только провайдер
(миграции лежат в отдельных сборках — `ZooStav.Migrations.SqlServer` и `ZooStav.Migrations.Sqlite`).

### 2.4. Адреса после запуска

| Адрес | Что это |
|---|---|
| `http://localhost:5045/` и `https://localhost:7117/` | главная страница приложения (витрина зоопарка, ссылки на поддомены) |
| `http://raccoon.localhost:5045/` | **страница животного на поддомене** (браузеры резолвят `*.localhost` в 127.0.0.1) |
| `http://localhost:5045/raccoon` | тот же контент по пути (резервный вариант для демо-стендов) |
| `http://localhost:5045/diary` | дневник наблюдений с фильтрами (нужен вход) |
| `http://localhost:5045/routing` | страница с объяснением схемы поддомен-роутинга |
| `http://localhost:5045/swagger` | Swagger UI для REST API |
| `http://localhost:5045/health` | health-check |

### 2.5. Запуск в Docker (приложение + SQL Server)

```bash
docker compose up --build     # http://localhost:8080  (страница животного: /raccoon)
```

---

## 3. Соответствие заданию

| Требование задания | Реализация |
|---|---|
| ASP.NET MVC или Razor Pages | **ASP.NET Core MVC** (`src/ZooStav.Web/Controllers` + `Views`) |
| Страница животного по варианту «Енот» | Страница **Енот-полоскун** (`Slug = raccoon`), БД + демо-данные |
| Страничка и роутинг как поддомен зоопарка | `raccoon.zoostav.ru` → middleware `UseSubdomainRouting()` переписывает `/` в `/animal/raccoon` (см. `/routing`) |
| Ссылка на главную зоопарка https://zoostav.ru | В шапке, в отдельном CTA-блоке на странице животного, в подвале (всего 8+ ссылок на страницу) |
| Посетители: описание, история особи | Разделы «Кто это» и «История особи» (+ ареал и рацион) |
| Посетители: фото | Фотогалерея (5 фото) |
| Посетители: видео | 2 видеоролика (встроенный плеер) |
| Посетители: наблюдение через веб-камеру | Раздел «Наблюдение через веб-камеру»: онлайн-трансляция + стоп-кадр из вольера |
| Посетители: донат на корм/лечение | Форма доната (free donation, от 1 ₽), назначения «корм / лечение / уход», сводка собранных сумм, публичный список донатов |
| Работники: дневник особи/популяции | Раздел «Дневник»: кормление, вакцинация, спаривание, потомство, болезнь, лечение, измерения, перемещение, наблюдение, прочее |
| Доступ к информации и дневнику через API | REST API `api/animals`, `api/animals/{slug}/diary`, `api/diary`, `api/donations` + Swagger |
| Хранение в БД | EF Core 8 (SQL Server / SQLite), 6 таблиц + таблицы Identity |
| Фильтрация дневника по дате, типу, пользователю и пр. | Страница `/diary` и API: `type`, `dateFrom`, `dateTo`, `user`, `userId`, `search`, `sort`, `page`, `pageSize`; фильтр по животному задаётся поддоменом/слагом |
| Аутентификация и авторизация (можно JWT) | Cookie-аутентификация Identity для сайта **и** JWT для API; роли `Visitor` и `Staff`; страница `/Account/AccessDenied` |
| Единая папка в удалённом репозитории | Решение в одной папке: `ZooStav.sln` + `src/` + `tests/` |
| Логины и пароли в документации | Раздел 1 этого файла и страница `/Account/Demo` |

Дополнительно сделано: SignalR-уведомления о новых записях дневника в реальном времени,
журнал действий пользователей (`/staff/audit`), управление ролями (`/staff/users`),
35+ автотестов (xUnit + интеграционные тесты через `WebApplicationFactory`),
Swagger-документация, Docker/docker-compose, готовые конфиги nginx и IIS.

---

## 4. Структура решения

```
ASP.NET/                                  ← единая папка проекта (репозиторий)
├── ZooStav.sln
├── README.md                             ← этот файл (логины/пароли, запуск)
├── docs/
│   ├── API.md                            ← описание REST API + примеры curl
│   └── DEPLOY.md                         ← публикация и настройка поддоменов (DNS, nginx, IIS)
├── docker-compose.yml , Dockerfile
├── src/
│   ├── ZooStav.Data/                     ← предметная область и доступ к данным
│   │   ├── Domain/Entities.cs            ZooUser, Animal, MediaItem, DiaryEntry, Donation, AuditLog
│   │   ├── Data/ZooDbContext.cs          DbContext (Identity + доменные сущности)
│   │   ├── Services/DiaryService.cs      фильтрация/сортировка/пагинация дневника
│   │   └── Infrastructure/               ZooOptions (домен, JWT), SlugChecker
│   ├── ZooStav.Migrations.SqlServer/     миграции EF Core для SQL Server
│   ├── ZooStav.Migrations.Sqlite/        миграции EF Core для SQLite
│   └── ZooStav.Web/                      веб-приложение
│       ├── Program.cs                    DI, БД, Identity, JWT, роутинг, Swagger, SignalR
│       ├── Controllers/                  MVC: Home, Animal, Diary, Donation, Account, Staff
│       ├── Controllers/Api/              REST API: Auth, Animals, Diary, Donations
│       ├── Infrastructure/               ZooSubdomain, UseSubdomainRouting, ZooDbInitializer
│       ├── Hubs/ZooHub.cs                SignalR-хаб живых обновлений
│       ├── Mapping/ZooMapper.cs          сущность → DTO
│       ├── Services/                     JwtTokenService, DiaryService, AuditService, PaymentService
│       ├── ViewModels/                   модели страниц и DTO API
│       ├── Views/                        Razor-представления (layout, страница животного, дневник…)
│       └── wwwroot/                      css/site.css, js/site.js, images/raccoon/*.jpg
└── tests/ZooStav.Tests/                  37 тестов: роутинг поддоменов, фильтры дневника, API
```

---

## 5. Роутинг поддомена — как это работает

```
https://zoostav.ru                    →  главная страница зоопарка (внешний сайт, ссылка с нашей страницы)
https://raccoon.zoostav.ru/           →  персональная страница енота «Тимка»   ← целевой адрес по заданию
https://raccoon.zoostav.ru/diary      →  дневник, автоматически отфильтрованный по этому животному
https://zoostav.ru/raccoon            →  301-редирект на https://raccoon.zoostav.ru/
https://zoostav.ru/api/animals/raccoon →  REST API карточки животного (JWT для защищённых методов)
https://www.zoostav.ru/...            →  служебные поддомены (www, api, admin) исключены из поддомен-роутинга
```

Как устроено в коде:

1. `ZooSubdomain.GetAnimalSlug(host)` — определяет животное по имени поддомена
   (`raccoon.zoostav.ru` → `raccoon`), игнорируя `www`, `api`, `admin` и учитывая порт.
2. `UseSubdomainRouting()` — middleware **до** `UseRouting()`: переписывает путь `/`
   в `/animal/{slug}`, адрес в браузере при этом не меняется.
3. `AnimalController.Page` — отдаёт страницу животного; запрос на корневом домене (`zoostav.ru/raccoon`)
   перенаправляет 301 на поддомен.
4. Настройки домена — в `appsettings.json`: `Zoo:RootDomain`, `Zoo:MainSiteUrl`, `Zoo:CookieDomain`.
   Чтобы поднять приложение на другом домене, достаточно поменять эти значения (код не меняется).

Локальная проверка поддоменов без DNS: браузеры сами резолвят `*.localhost` в `127.0.0.1`,
поэтому работает `http://raccoon.localhost:5045/`. Для произвольных имён (`raccoon.zoostav.test`)
достаточно добавить строку в `hosts`. Подробности — в [docs/DEPLOY.md](docs/DEPLOY.md).

---

## 6. Аутентификация и авторизация

Используются **две схемы одновременно**, выбор — по виду запроса (схема-селектор `ZooAuth`):

| Схема | Для чего | Как работает |
|---|---|---|
| Cookie (ASP.NET Core Identity) | страницы сайта (Razor/MVC) | форма `/Account/Login`, `SignInManager`, cookie `ZooStav.Auth`; для боевого домена задаётся `Zoo:CookieDomain=.zoostav.ru`, чтобы вход работал на всех поддоменах |
| JWT Bearer | REST API | `POST /api/auth/login` → `accessToken` (+ `refreshToken`), далее заголовок `Authorization: Bearer <token>` |

Разграничение прав:

* **Анонимно** — страница животного: описание, история, фото, видео, веб-камера, форма доната, публичный список донатов.
* **Требуется вход** (`Visitor` или `Staff`) — чтение дневника (`/diary`, `GET /api/animals/{slug}/diary`).
* **Только `Staff`** — создание/правка/удаление записей дневника (`/diary/create`, `POST|PUT|DELETE /api/...`),
  журнал действий `/staff/audit`, управление ролями `/staff/users`.

Ответы на отказы корректны для обоих типов клиентов: страницы → редирект на `/Account/Login`
(или страница «Доступ запрещён»), API → JSON `401`/`403`.

Все попытки входа, регистрации, изменения дневника, донаты и отказы в доступе попадают в
**журнал действий** (`/staff/audit`, таблица `AuditLogs`).

---

## 7. Дневник наблюдений и фильтрация

Типы записей: `Feeding` (кормление), `Vaccination` (вакцинация), `Mating` (спаривание),
`Offspring` (потомство), `Illness` (болезнь), `Treatment` (лечение), `Measurement` (измерения),
`Relocation` (перемещение), `Observation` (наблюдение), `Other` (прочее).

Фильтры (одинаковы на странице `/diary` и в API):

| Параметр | Значение | Пример |
|---|---|---|
| `type` | тип записи | `Vaccination` |
| `dateFrom`, `dateTo` | период по дате события (включительно) | `2026-01-01`, `2026-12-31` |
| `user` | логин или ФИО автора записи | `keeper@zoostav.ru` |
| `userId` | идентификатор автора | `d0d28b00-…` |
| `search` | поиск по заголовку и описанию | `отит` |
| `slug` / поддомен | животное | `raccoon` |
| `sort` | `date_desc` (по умолчанию), `date_asc`, `type`, `user`, `created_desc` | `date_asc` |
| `page`, `pageSize` | постраничный вывод | `1`, `10` |

Пример страницы: `http://localhost:5045/diary?type=Vaccination&dateFrom=2026-01-01&user=keeper@zoostav.ru&sort=date_desc`.

> Техническая деталь: для поиска по тексту используется нормализованное поле `DiaryEntry.SearchText`,
> которое заполняется в `ZooDbContext.SaveChanges`. Это сделано потому, что `LOWER()` в SQLite
> не обрабатывает кириллицу — так поиск по русскому тексту работает одинаково на SQL Server и SQLite.

---

## 8. REST API (кратко)

Полное описание с примерами — [docs/API.md](docs/API.md), интерактивная документация — `/swagger`.

| Метод | Маршрут | Доступ | Назначение |
|---|---|---|---|
| POST | `/api/auth/register` | анонимно | регистрация (роль `Visitor`) |
| POST | `/api/auth/login` | анонимно | вход, выдача JWT |
| POST | `/api/auth/refresh` | анонимно + токен | обновление access-токена |
| GET | `/api/auth/me` | по токену | текущий пользователь |
| GET | `/api/animals` | анонимно | список животных |
| GET | `/api/animals/{slug}` | анонимно | карточка: описание, история, медиа, веб-камера, донаты |
| GET | `/api/animals/{slug}/media` | анонимно | медиатека (фото/видео) |
| GET | `/api/animals/{slug}/diary` | анонимно | дневник животного с фильтрами |
| POST | `/api/animals/{slug}/diary` | **Staff** | новая запись дневника |
| GET | `/api/diary` | **Staff** | все записи по всем животным с фильтрами |
| GET | `/api/diary/{id}` | **Staff** | запись по идентификатору |
| PUT | `/api/diary/{id}` | **Staff** | изменение записи |
| DELETE | `/api/diary/{id}` | **Staff** | удаление записи |
| GET | `/api/diary/types` | анонимно | справочник типов записей |
| POST | `/api/donations` | анонимно | донат (free donation) |
| GET | `/api/donations/{slug}` | анонимно | донаты животного |
| GET | `/api/donations/{slug}/summary` | анонимно | сводка по назначениям |

Пример: добавить запись в дневник от имени работника

```bash
TOKEN=$(curl -s -X POST http://localhost:5045/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"keeper@zoostav.ru","password":"Keeper#2026"}' \
  | python -c "import sys,json;print(json.load(sys.stdin)['data']['accessToken'])")

curl -X POST http://localhost:5045/api/animals/raccoon/diary \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"type":"Feeding","title":"Вечернее кормление","description":"120 г рыбы, творог","performedBy":"Смотритель"}'
```

---

## 9. Тесты

```bash
dotnet test
```

37 тестов (xUnit):

* `SubdomainRoutingTests` — определение животного по поддомену, служебные поддомены, слаг-валидация;
* `DiaryServiceTests` — фильтры по типу/дате/автору/тексту, сортировки, пагинация (реальный SQLite in-memory);
* `ApiIntegrationTests` — поднятое приложение целиком: страница животного на поддомене, публичность карточки,
  401 без токена, 403 для посетителя, создание и чтение записей `Staff`, донаты, регистрация, ссылки на zoostav.ru.

---

## 10. Полезные сведения по реализации

* **Инициализация БД** (`Infrastructure/ZooDbInitializer.cs`): применяет миграции нужного провайдера,
  создаёт роли `Visitor`/`Staff`, тестовых пользователей, животное «Енот-полоскун» с 5 фото, 2 видео,
  11 записями дневника и 3 донатами.
* **Фото/видео**: иллюстрации енота лежат в `wwwroot/images/raccoon/` (подготовлены для проекта),
  видео подключаются как внешние embed-ссылки (YouTube) или локальные файлы `<video>`.
* **Веб-камера**: адрес трансляции хранится в БД (`Animal.WebcamUrl`) и выводится в `<iframe>`;
  сейчас там демонстрационная ссылка на YouTube Live + стоп-кадр вольера. Для реального вольера
  достаточно подставить адрес HLS/WebRTC-шлюза (например, `https://camera.zoostav.ru/hls/raccoon.m3u8`).
* **Донаты**: `IPaymentService` — демонстрационный шлюз, который возвращает номер операции.
  Для реального приёма платежей реализация заменяется на ЮKassa/CloudPayments (интерфейс и таблица `Donations` не меняются).
* **Реальное время**: SignalR-хаб `/hubs/zoo` — новая запись дневника, добавленная кем-то другим
  (в том числе через API), сразу появляется на открытой странице животного и в дневнике.
* **Кириллица в HTML**: включён `HtmlEncoder.Create(UnicodeRanges.All)`, чтобы разметка была читаемой.
* **HTTP → HTTPS**: включается настройкой `Zoo:UseHttpsRedirection` (в локальной отладке выключено,
  на боевом домене включается вместе с HSTS).

---

## 11. Роли и демонстрационные сценарии для защиты работы

1. **Анонимный пользователь** открывает `http://raccoon.localhost:5045/` — страница животного на поддомене,
   видит описание, историю, фото, видео, веб-камеру, кнопку «Донат». Ссылка на `https://zoostav.ru` — в шапке страницы.
2. **Анонимный пользователь** пытается открыть `/diary` → редирект на форму входа.
3. **Вход `visitor@zoostav.ru`** → `/diary` открывается в режиме просмотра; кнопки «Изменить/Удалить» отсутствуют;
   при попытке `GET /diary/create` — страница «Доступ запрещён»; `POST /api/animals/raccoon/diary` с его токеном — `403`.
4. **Вход `keeper@zoostav.ru`** → в дневнике доступны «Новая запись», «Изменить», «Удалить»;
   созданная запись появляется в списке, о ней пишется строка в журнале `/staff/audit`.
5. **Фильтрация**: `/diary?type=Vaccination`, `/diary?dateFrom=2026-01-01&dateTo=2026-12-31`, `/diary?user=keeper@zoostav.ru`, `/diary?search=отит`.
6. **API**: `GET /api/animals/raccoon` (публично), `POST /api/auth/login` → `POST /api/animals/raccoon/diary` (с токеном) → `GET /api/animals/raccoon/diary?type=Feeding&sort=date_desc`.
7. **Поддомен**: `curl -H "Host: raccoon.zoostav.ru" http://localhost:5045/` вернёт страницу енота, а
   `curl -I -H "Host: zoostav.ru" http://localhost:5045/raccoon` — `301` на `https://raccoon.zoostav.ru/`.

---

## 12. Автор и лицензия

Учебный проект (вариант «Енот»). Материалы проекта можно свободно использовать в учебных целях.
Иллюстрации енота находятся в папке wwwroot/images/raccoon.
