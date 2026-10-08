# Публикация приложения и настройка поддоменов

Документ описывает, как поднять приложение на боевом домене `zoostav.ru`
так, чтобы страница животного открывалась по адресу `https://raccoon.zoostav.ru/`,
а главная страница зоопарка оставалась на `https://zoostav.ru`.

---

## 1. Что нужно на стороне приложения

Всё уже реализовано; настройки задаются в `appsettings.json` / переменными окружения:

```json
{
  "Zoo": {
    "RootDomain": "zoostav.ru",        // корневой домен зоопарка
    "MainSiteUrl": "https://zoostav.ru", // обязательная ссылка на главную страницу
    "Scheme": "https",
    "IgnoredSubdomains": [ "www", "api", "admin", "static", "cdn" ],
    "CookieDomain": ".zoostav.ru",      // общая cookie входа для всех поддоменов
    "UseHttpsRedirection": true
  }
}
```

* `RootDomain` — единственное, что нужно поменять при переезде на другой домен.
* `CookieDomain = ".zoostav.ru"` позволяет сотруднику, вошедшему на `raccoon.zoostav.ru`,
  оставаться авторизованным и на `zoostav.ru` (и наоборот).
* `MainSiteUrl` подставляется в обязательную ссылку на главную страницу зоопарка.

---

## 2. DNS

Одна wildcard-запись закрывает все текущие и будущие страницы животных:

```
A     zoostav.ru          → <IP сервера>
A     www                 → <IP сервера>
A     raccoon             → <IP сервера>
A     api                 → <IP сервера>
CNAME *.zoostav.ru        → zoostav.ru     (или отдельные A-записи по числу животных)
```

Вариант с `*.zoostav.ru` удобнее: новое животное (например, `lynx`) заработает без правок DNS —
достаточно добавить запись в таблицу `Animals` с `Slug = 'lynx'`.

Проверка поддоменов:

```bash
dig +short raccoon.zoostav.ru
curl -I https://raccoon.zoostav.ru/
```

---

## 3. Обратный прокси: nginx

Одно приложение обслуживает и корневой домен, и все поддомены — прокси передаёт исходный `Host`.

```nginx
server {
    listen 80;
    listen 443 ssl http2;
    server_name zoostav.ru www.zoostav.ru *.zoostav.ru;

    ssl_certificate     /etc/letsencrypt/live/zoostav.ru/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/zoostav.ru/privkey.pem;

    location / {
        proxy_pass         http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;              # ВАЖНО: передаём поддомен
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
        proxy_set_header   X-Forwarded-Host  $host;

        # WebSocket для SignalR (живые обновления дневника)
        proxy_set_header   Upgrade    $http_upgrade;
        proxy_set_header   Connection "upgrade";
        proxy_read_timeout 3600s;
    }
}
```

HTTP-сертификат для wildcard-домена выписывается так:

```bash
certbot certonly --dns-<провайдер> -d zoostav.ru -d '*.zoostav.ru'
```

Приложение уже настроено на доверие заголовкам прокси (`UseForwardedHeaders`), поэтому
схема, хост и IP клиента определяются корректно (в том числе для журнала действий).

---

## 4. Обратный прокси: IIS (Windows)

1. Установите **ASP.NET Core 8 Hosting Bundle**.
2. Создайте сайт с привязкой (bindings): `zoostav.ru`, `www.zoostav.ru`, `*.zoostav.ru` (порт 80 и 443),
   физический путь — папка публикации, пул приложений — «Без управляемого кода».
3. Установите **URL Rewrite** и **Application Request Routing (ARR)**, включите проксирование на pool `http://localhost:8080`.

`web.config` (создаётся при публикации; пример для ARR-сценария) должен содержать:

```xml
<system.webServer>
  <rewrite>
    <rules>
      <rule name="ASP.NET Core reverse proxy" stopProcessing="true">
        <match url="(.*)" />
        <action type="Rewrite" url="http://localhost:8080/{R:1}" />
        <serverVariables>
          <set name="HTTP_X_FORWARDED_HOST" value="{HTTP_HOST}" />
          <set name="HTTP_X_FORWARDED_PROTO" value="https" />
        </serverVariables>
      </rule>
    </rules>
  </rewrite>
</system.webServer>
```

Публикация:

```bash
dotnet publish src/ZooStav.Web/ZooStav.Web.csproj -c Release -o C:\inetpub\zoostav
```

Для хостинга прямо в IIS (in-process) достаточно одного сайта с wildcard-привязкой —
поддомен-роутинг работает так же, потому что `Host`-заголовок доходит до приложения.

---

## 5. Локальная проверка поддоменов на своей машине

| Способ | Что делать | Адрес |
|---|---|---|
| Профиль запуска (ничего настраивать не нужно) | `dotnet run --launch-profile "ZooStav.Web (SQLite — без установки SQL Server)"` | `http://raccoon.localhost:5045/` |
| Свои имена (`*.test`) | добавить в `hosts` строку `127.0.0.1 raccoon.zoostav.test` и запустить с `Zoo__RootDomain=zoostav.test` | `http://raccoon.zoostav.test:5045/` |
| Без DNS вообще | открыть путь-резерв `/raccoon` или `/animal/raccoon` | `http://localhost:5045/raccoon` |
| Реальный домен | опубликовать по инструкции выше | `https://raccoon.zoostav.ru/` |

Проверка через `curl` без DNS:

```bash
curl -H "Host: raccoon.zoostav.ru" http://localhost:5045/          # страница животного
curl -I -H "Host: zoostav.ru" http://localhost:5045/raccoon        # 301 → https://raccoon.zoostav.ru/
curl -I -H "Host: www.zoostav.ru" http://localhost:5045/            # служебный поддомен: обычная главная
```

---

## 6. Docker

```bash
docker compose up --build     # приложение + SQL Server
# страница животного:  http://localhost:8080/raccoon
# swagger:             http://localhost:8080/swagger
```

В контейнере приложение слушает `0.0.0.0:8080`; для боевого домена перед ним ставится
nginx (см. п. 3) или cloud-балансировщик с wildcard-сертификатом.

---

## 7. Продакшен-настройки, которые стоит переопределить

| Настройка | Значение по умолчанию | Рекомендация |
|---|---|---|
| `Jwt:Key` | демонстрационный ключ | задать длинный случайный секрет через переменные окружения / секреты |
| `ConnectionStrings:SqlServer` | LocalDB | строка подключения к боевому SQL Server |
| `Zoo:CookieDomain` | пусто | `.zoostav.ru` (единый вход на всех поддоменах) |
| `Database:FailFast` | `true` | оставить `true`, чтобы деплой падал при недоступной БД |
| `ASPNETCORE_ENVIRONMENT` | Development локально | `Production` на сервере |

Пример запуска на сервере:

```bash
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__SqlServer="Server=sql01;Database=ZooStavRaccoon;User Id=zoo_app;Password=***;TrustServerCertificate=True"
export Jwt__Key="$(openssl rand -base64 48)"
export Zoo__CookieDomain=".zoostav.ru"
export Zoo__UseHttpsRedirection=true
dotnet ZooStav.Web.dll --urls http://127.0.0.1:8080
```

Миграции применяются автоматически при старте приложения (для обоих провайдеров
собраны отдельные сборки миграций: `ZooStav.Migrations.SqlServer` и `ZooStav.Migrations.Sqlite`).
Ручное применение, если нужно:

```bash
dotnet ef database update --project src/ZooStav.Migrations.SqlServer --startup-project src/ZooStav.Web
```
