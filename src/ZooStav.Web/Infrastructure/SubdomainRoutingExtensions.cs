using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ZooStav.Web.Infrastructure;

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
