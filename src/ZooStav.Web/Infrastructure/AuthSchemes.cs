namespace ZooStav.Web.Infrastructure;

/// <summary>Имена схем аутентификации приложения.</summary>
public static class AuthSchemes
{
    /// <summary>
    /// Схема-селектор: запрос с заголовком "Authorization: Bearer …" обслуживается JWT,
    /// остальные запросы (браузер со страницы сайта) — cookie-аутентификацией Identity.
    /// </summary>
    public const string Smart = "ZooAuth";
}
