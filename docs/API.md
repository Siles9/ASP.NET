# REST API приложения «Страница животного зоопарка»

Базовый адрес локально: `http://localhost:5045`, боевой: `https://raccoon.zoostav.ru`.
Интерактивная документация (Swagger UI): **`/swagger`**, спецификация: **`/swagger/v1/swagger.json`**.

Все ответы — JSON вида:

```json
{ "success": true,  "data": { } }
{ "success": false, "error": "текст ошибки" }
```

Аутентификация — JWT: `Authorization: Bearer <accessToken>`.
Токен выдаётся методом `POST /api/auth/login`; срок жизни — 60 минут (настраивается `Jwt:AccessTokenMinutes`).

Коды ответов: `200` — успех, `201` — создано, `400` — ошибка валидации,
`401` — нет/просрочен токен, `403` — недостаточно прав (нужна роль `Staff`), `404` — не найдено.

---

## 1. Аутентификация — `/api/auth`

### POST /api/auth/register — регистрация посетителя

```bash
curl -X POST http://localhost:5045/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.com","password":"Secret#123","fullName":"Иван Петров"}'
```

Ответ `200`:

```json
{
  "success": true,
  "data": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6...",
    "refreshToken": "5e1b...",
    "tokenType": "Bearer",
    "expiresAtUtc": "2026-10-08T13:15:00Z",
    "user": { "id": "…", "email": "user@example.com", "fullName": "Иван Петров", "roles": ["Visitor"] },
    "roles": ["Visitor"]
  }
}
```

Самостоятельная регистрация **всегда** выдаёт роль `Visitor`.

### POST /api/auth/login — вход и получение JWT

```bash
curl -X POST http://localhost:5045/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"keeper@zoostav.ru","password":"Keeper#2026"}'
```

Ошибка: `401 {"success":false,"error":"Неверный e-mail или пароль."}`

### POST /api/auth/refresh — обновление access-токена

Передайте текущий access-токен в заголовке и `refreshToken` в теле:

```bash
curl -X POST http://localhost:5045/api/auth/refresh \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"refreshToken":"<refreshToken>"}'
```

### GET /api/auth/me — текущий пользователь

```bash
curl http://localhost:5045/api/auth/me -H "Authorization: Bearer $TOKEN"
```

---

## 2. Животные — `/api/animals`

Доступны анонимно. Если запрос выполнен с токеном роли `Staff`, в карточке
дополнительно приходит признак `diaryAvailable: true` (для сотрудника доступен дневник).

### GET /api/animals — список животных

```bash
curl http://localhost:5045/api/animals
```

### GET /api/animals/{slug} — карточка животного

```bash
curl http://localhost:5045/api/animals/raccoon
```

```json
{
  "success": true,
  "data": {
    "id": 1,
    "slug": "raccoon",
    "name": "Енот-полоскун",
    "species": "Енот-полоскун",
    "latinName": "Procyon lotor",
    "summary": "Енот-полоскун Тимка — самый узнаваемый обитатель…",
    "history": "Тимка родился 18 апреля 2022 года в питомнике…",
    "habitat": "Природный ареал — Северная Америка…",
    "diet": "Рацион всеядный: рыба, насекомые, лягушки…",
    "enclosure": "Вольер №7 «Лесная тропа», контактная зона",
    "gender": "Самец",
    "status": "Здоров",
    "birthDate": "2022-04-18",
    "ageYears": 4,
    "webcamUrl": "https://www.youtube.com/embed/live_stream?channel=…",
    "subdomainUrl": "https://raccoon.zoostav.ru/",
    "zooMainSiteUrl": "https://zoostav.ru",
    "diaryAvailable": false,
    "photos": [ { "id": 1, "type": "photo", "title": "Тимка на прогулке", "url": "http://…/images/raccoon/raccoon-hero.jpg" } ],
    "videos": [ { "id": 6, "type": "video", "title": "Завтрак Тимки", "url": "https://www.youtube.com/embed/…" } ],
    "donations": { "total": 12200, "count": 3, "foodTotal": 1500, "treatmentTotal": 700, "careTotal": 10000, "recent": [] }
  }
}
```

### GET /api/animals/{slug}/media — медиатека

```bash
curl http://localhost:5045/api/animals/raccoon/media
```

---

## 3. Дневник наблюдений

### GET /api/animals/{slug}/diary — дневник животного (публично)

Параметры фильтрации (все необязательные):

| Параметр | Описание | Пример |
|---|---|---|
| `type` | тип записи: `Feeding`, `Vaccination`, `Mating`, `Offspring`, `Illness`, `Treatment`, `Measurement`, `Relocation`, `Observation`, `Other` | `Feeding` |
| `dateFrom` | дата события «с» (включительно) | `2026-01-01` |
| `dateTo` | дата события «по» (включительно) | `2026-12-31` |
| `user` | логин или ФИО автора записи (частичное совпадение) | `keeper@zoostav.ru` |
| `userId` | идентификатор автора | `d0d28b00-…` |
| `search` | поиск по заголовку и описанию | `отит` |
| `sort` | `date_desc` (по умолчанию), `date_asc`, `type`, `user`, `created_desc` | `date_asc` |
| `page`, `pageSize` | пагинация (`pageSize` ≤ 200) | `2`, `10` |

```bash
curl "http://localhost:5045/api/animals/raccoon/diary?type=Vaccination&dateFrom=2026-01-01&sort=date_desc&pageSize=5"
```

```json
{
  "success": true,
  "data": {
    "animal": { "id": 1, "slug": "raccoon", "name": "Енот-полоскун" },
    "filter": { "type": "Vaccination", "dateFrom": "2026-01-01", "dateTo": null, "userName": null, "search": null, "sort": "date_desc", "page": 1, "pageSize": 5 },
    "totalCount": 1, "totalPages": 1, "page": 1, "pageSize": 5,
    "items": [
      {
        "id": 3, "animalId": 1, "animalSlug": "raccoon",
        "type": "Vaccination", "typeTitle": "Вакцинация",
        "occurredAtUtc": "2026-09-29T03:00:00Z",
        "title": "Плановая вакцинация",
        "description": "Введена вакцина Nobivac…",
        "performedBy": "Ветеринарный врач Кузнецова А. И.",
        "location": "Ветеринарный блок",
        "userId": "d0d28b00-…", "userName": "keeper@zoostav.ru",
        "createdVia": "Web", "createdAtUtc": "2026-10-08T11:58:26Z"
      }
    ]
  }
}
```

### GET /api/diary — все записи (только Staff)

```bash
curl "http://localhost:5045/api/diary?user=vet@zoostav.ru&sort=date_asc" -H "Authorization: Bearer $TOKEN"
```

### GET /api/diary/{id} — запись по идентификатору (только Staff)

```bash
curl http://localhost:5045/api/diary/3 -H "Authorization: Bearer $TOKEN"
```

### POST /api/animals/{slug}/diary — новая запись (только Staff)

Поля: `type` (обязательно), `title` (обязательно), `description`, `performedBy`,
`location`, `occurredAtUtc` (по умолчанию — текущее время UTC).

```bash
curl -X POST http://localhost:5045/api/animals/raccoon/diary \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{
        "type": "Feeding",
        "title": "Вечернее кормление",
        "description": "120 г рыбы, 60 г творога, изюм как поощрение",
        "performedBy": "Смотритель вольера №7",
        "location": "Вольер №7"
      }'
```

`201 Created` — запись создана, `createdVia` = `Api`, автор = владелец токена.
Параллельно всем открытым страницам уходит SignalR-событие `diaryEntryCreated`.

Без токена: `401`. С токеном посетителя: `403`.

### PUT /api/diary/{id} — изменение записи (только Staff)

```bash
curl -X PUT http://localhost:5045/api/diary/12 \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"type":"Feeding","title":"Вечернее кормление (уточнено)","description":"130 г рыбы"}'
```

### DELETE /api/diary/{id} — удаление записи (только Staff)

```bash
curl -X DELETE http://localhost:5045/api/diary/12 -H "Authorization: Bearer $TOKEN"
```

### GET /api/diary/types — справочник типов

```bash
curl http://localhost:5045/api/diary/types
```

```json
{ "success": true, "data": [ { "value": "Feeding", "id": 0, "title": "Кормление" } ] }
```

---

## 4. Донаты — `/api/donations`

### POST /api/donations — оформить донат (анонимно)

Поля: `animalSlug` или `animalId`, `donorName`, `amount` (> 0), `purpose` (`food` | `treatment` | `care`),
`donorEmail` (необязательно), `message` (необязательно).

```bash
curl -X POST http://localhost:5045/api/donations \
  -H "Content-Type: application/json" \
  -d '{"animalSlug":"raccoon","donorName":"Анна К.","amount":500,"purpose":"food","message":"Тимке на рыбку!"}'
```

```json
{
  "success": true,
  "data": {
    "id": 4, "animalId": 1, "donorName": "Анна К.", "amount": 500, "currency": "RUB",
    "purpose": "food", "purposeTitle": "Корм", "message": "Тимке на рыбку!",
    "status": "Succeeded", "paymentReference": "ZOO-20261008120530-4417",
    "createdAtUtc": "2026-10-08T12:05:30Z"
  }
}
```

Оплата проходит через `IPaymentService` (в демо — MockPaymentService, возвращает номер операции).
Для подключения реального эквайринга достаточно заменить реализацию интерфейса.

### GET /api/donations/{slug} — донаты животного (публично)

```bash
curl "http://localhost:5045/api/donations/raccoon?page=1&pageSize=10"
```

### GET /api/donations/{slug}/summary — сводка по назначениям

```bash
curl http://localhost:5045/api/donations/raccoon/summary
```

```json
{ "success": true, "data": { "total": 12700, "count": 4, "foodTotal": 2000, "treatmentTotal": 700, "careTotal": 10000, "recent": [] } }
```

---

## 5. Служебные маршруты

| Маршрут | Назначение |
|---|---|
| `GET /health` | состояние приложения: провайдер БД, корневой домен, основной сайт, время UTC |
| `GET /swagger` | Swagger UI (кнопка Authorize → вставить accessToken) |
| `GET /swagger/v1/swagger.json` | OpenAPI-спецификация |

## 6. Полный сценарий проверки (копировать целиком)

```bash
BASE=http://localhost:5045

# 1. публичная карточка животного
curl -s $BASE/api/animals/raccoon | head -c 400

# 2. публичный дневник с фильтрами
curl -s "$BASE/api/animals/raccoon/diary?type=Feeding&sort=date_desc&pageSize=3"

# 3. вход работника и токен
TOKEN=$(curl -s -X POST $BASE/api/auth/login -H "Content-Type: application/json" \
  -d '{"email":"keeper@zoostav.ru","password":"Keeper#2026"}' \
  | python3 -c "import sys,json;print(json.load(sys.stdin)['data']['accessToken'])")

# 4. создание записи (Staff)
curl -s -X POST $BASE/api/animals/raccoon/diary -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"type":"Observation","title":"Проверка API","description":"Запись создана через REST"}' | head -c 300

# 5. токен посетителя → запись запрещена (403)
VTOKEN=$(curl -s -X POST $BASE/api/auth/login -H "Content-Type: application/json" \
  -d '{"email":"visitor@zoostav.ru","password":"Visitor#2026"}' \
  | python3 -c "import sys,json;print(json.load(sys.stdin)['data']['accessToken'])")
curl -s -o /dev/null -w "%{http_code}\n" -X POST $BASE/api/animals/raccoon/diary \
  -H "Authorization: Bearer $VTOKEN" -H "Content-Type: application/json" -d '{"type":"Other","title":"нет"}'

# 6. анонимный запрос к защищённому методу → 401
curl -s -o /dev/null -w "%{http_code}\n" $BASE/api/diary

# 7. донат
curl -s -X POST $BASE/api/donations -H "Content-Type: application/json" \
  -d '{"animalSlug":"raccoon","donorName":"Тест","amount":300,"purpose":"food"}'

# 8. поддомен
curl -s -H "Host: raccoon.zoostav.ru" $BASE/ | grep -o "Енот-полоскун" | head -1

# 9. редирект с корневого домена на поддомен
curl -s -o /dev/null -w "%{http_code} %{redirect_url}\n" -H "Host: zoostav.ru" $BASE/raccoon
```
