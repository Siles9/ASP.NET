using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ZooStav.Web.Infrastructure;

/// <summary>
/// Настройка поддомен-роутинга:
///   raccoon.zoostav.ru/        -> внутренний маршрут /animal/raccoon  (URL в браузере не меняется)
///   raccoon.zoostav.ru/diary   -> /diary (с автоматической фильтрацией по животному)
/// Корневой домен zoostav.ru и служебные поддомены (www, api) не затрагиваются.
/// </summary>
public static class SubdomainRoutingExtensions
{
    public static IApplicationBuilder UseSubdomainRouting(this WebApplication app)
    {
        return app.Use(async (context, next) =>
        {
            var subdomains = context.RequestServices.GetRequiredService<ZooSubdomain>();
            var slug = subdomains.GetAnimalSlug(context.Request.Host.Host);

            if (slug is not null)
            {
                var path = context.Request.Path.Value ?? "/";

                if (path is "/" or "")
                {
                    // Внутренняя перезапись пути: главная поддомена — это страница животного.
                    context.Request.Path = "/animal/" + slug;
                }
                else if (path is "/raccoon" or "/index")
                {
                    context.Request.Path = "/animal/" + slug;
                }

                context.Items["AnimalSlug"] = slug;
            }

            await next();
        });
    }
}
