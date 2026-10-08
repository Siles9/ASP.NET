using System.Text.Encodings.Web;
using System.Text.Unicode;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ZooStav.Web.Data;
using ZooStav.Web.Domain;
using ZooStav.Web.Infrastructure;
using ZooStav.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ZooOptions>(builder.Configuration.GetSection(ZooOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

var zooOptions = builder.Configuration.GetSection(ZooOptions.SectionName).Get<ZooOptions>() ?? new ZooOptions();
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

var dbProvider = builder.Configuration["Database:Provider"] ?? "SqlServer";
var connectionString = dbProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase)
    ? builder.Configuration.GetConnectionString("Sqlite")
    : builder.Configuration.GetConnectionString("SqlServer");

builder.Services.AddDbContext<ZooDbContext>(options =>
{
    if (dbProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(connectionString, m => m.MigrationsAssembly("ZooStav.Migrations.Sqlite"));
    }
    else
    {
        options.UseSqlServer(connectionString, m => m.MigrationsAssembly("ZooStav.Migrations.SqlServer"));
    }
});

builder.Services
    .AddIdentity<ZooUser, IdentityRole>(options =>
    {
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedAccount = false;
        options.Lockout.MaxFailedAccessAttempts = 10;
    })
    .AddEntityFrameworkStores<ZooDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Cookie.Name = "ZooStav.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;

    if (!string.IsNullOrWhiteSpace(zooOptions.CookieDomain))
    {
        options.Cookie.Domain = zooOptions.CookieDomain;
    }

    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = context => WriteApiErrorOrRedirectAsync(context, StatusCodes.Status401Unauthorized,
            "Требуется авторизация. Войдите через /Account/Login или получите JWT-токен: POST /api/auth/login."),
        OnRedirectToAccessDenied = context => WriteApiErrorOrRedirectAsync(context, StatusCodes.Status403Forbidden,
            "Недостаточно прав. Ведение дневника доступно только работникам зоопарка (роль Staff).")
    };
});

static Task WriteApiErrorOrRedirectAsync(RedirectContext<CookieAuthenticationOptions> context, int statusCode, string message)
{
    if (context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new { success = false, error = message }));
    }

    context.Response.Redirect(context.RedirectUri);
    return Task.CompletedTask;
}

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = ZooStav.Web.Infrastructure.AuthSchemes.Smart;
        options.DefaultAuthenticateScheme = ZooStav.Web.Infrastructure.AuthSchemes.Smart;
        options.DefaultChallengeScheme = ZooStav.Web.Infrastructure.AuthSchemes.Smart;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddPolicyScheme(ZooStav.Web.Infrastructure.AuthSchemes.Smart, "Cookie (сайт) или JWT (API)", options =>
    {
        options.ForwardDefaultSelector = context =>
            context.Request.Headers.Authorization.ToString()
                .StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? JwtBearerDefaults.AuthenticationScheme
                : IdentityConstants.ApplicationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(jwtOptions.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            RoleClaimType = System.Security.Claims.ClaimTypes.Role,
            NameClaimType = System.Security.Claims.ClaimTypes.Name
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json; charset=utf-8";
                    var body = JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = "Требуется авторизация. Получите JWT-токен через POST /api/auth/login и передайте его в заголовке Authorization: Bearer <token>."
                    });
                    return context.Response.WriteAsync(body);
                }

                return Task.CompletedTask;
            },
            OnForbidden = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                context.Response.ContentType = "application/json; charset=utf-8";
                var body = JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "Недостаточно прав. Запись дневника доступна только работникам зоопарка (роль Staff)."
                });
                return context.Response.WriteAsync(body);
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("StaffOnly", policy => policy.RequireRole(ZooRoles.Staff));
    options.AddPolicy("Authenticated", policy => policy.RequireAuthenticatedUser());
    options.AddPolicy("ApiStaffOrCookieStaff", policy =>
        policy.AddAuthenticationSchemes(IdentityConstants.ApplicationScheme, JwtBearerDefaults.AuthenticationScheme)
              .RequireRole(ZooRoles.Staff));
});

builder.Services
    .AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ZooSubdomain>();
builder.Services.AddScoped<ITokenService, JwtTokenService>();
builder.Services.AddScoped<IDiaryService, DiaryService>();
builder.Services.AddSingleton<IPaymentService, MockPaymentService>();

builder.Services.AddSingleton<HtmlEncoder>(HtmlEncoder.Create(UnicodeRanges.All));

var app = builder.Build();

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
});

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (app.Configuration.GetValue("Zoo:UseHttpsRedirection", true) && !app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseSubdomainRouting();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

var logger = app.Services.GetRequiredService<ILogger<Program>>();
try
{
    await ZooDbInitializer.InitializeAsync(app.Services, app.Configuration, logger);
}
catch (Exception ex)
{
    logger.LogError(ex,
        "Не удалось инициализировать базу данных (провайдер: {Provider}). " +
        "Проверьте строку подключения ConnectionStrings:{Key} или запустите приложение в демо-режиме SQLite: " +
        "Database__Provider=Sqlite. Подробности в README.txt.",
        dbProvider, dbProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase) ? "Sqlite" : "SqlServer");

    if (app.Configuration.GetValue("Database:FailFast", true))
    {
        throw;
    }
}

app.Run();

public partial class Program
{
}
