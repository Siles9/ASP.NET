using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ZooStav.Web.Domain;
using ZooStav.Web.Infrastructure;
using ZooStav.Web.Services;
using ZooStav.Web.ViewModels;

namespace ZooStav.Web.Controllers;

public class AccountController(
    SignInManager<ZooUser> signInManager,
    UserManager<ZooUser> userManager,
    IAuditService audit,
    IOptions<ZooOptions> zooOptions) : Controller
{
    private readonly ZooOptions _zoo = zooOptions.Value;

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new LoginViewModel
        {
            ReturnUrl = returnUrl,
            AnimalHost = Request.Host.Value
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        model.AnimalHost = Request.Host.Value;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            await audit.WriteAsync("Login", true, "Вход в веб-интерфейс", model.Email, model.Email, null);

            var user = await userManager.FindByEmailAsync(model.Email);
            var roles = user is null ? Array.Empty<string>() : (await userManager.GetRolesAsync(user)).ToArray();
            var isStaff = roles.Contains(ZooRoles.Staff);

            TempData["Toast"] = isStaff
                ? $"Здравствуйте, {user?.FullName ?? model.Email}! Доступ к дневнику наблюдений открыт."
                : $"Здравствуйте, {user?.FullName ?? model.Email}! Вы вошли как посетитель.";

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return isStaff
                ? RedirectToAction("Index", "Diary")
                : RedirectToAction("Index", "Home");
        }

        await audit.WriteAsync("Login", false, $"Неудачная попытка входа: {model.Email}", null, model.Email, null);

        ModelState.AddModelError(string.Empty, result.IsLockedOut
            ? "Учётная запись заблокирована. Попробуйте позже."
            : "Неверный e-mail или пароль.");

        return View(model);
    }

    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!model.Consent)
        {
            ModelState.AddModelError(nameof(model.Consent), "Необходимо согласие на обработку персональных данных.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ZooUser
        {
            UserName = model.Email,
            Email = model.Email,
            EmailConfirmed = true,
            FullName = model.FullName
        };

        var result = await userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await audit.WriteAsync("Register", false, $"Ошибка регистрации {model.Email}", null, model.Email, null);
            return View(model);
        }

        await userManager.AddToRoleAsync(user, ZooRoles.Visitor);
        await signInManager.SignInAsync(user, isPersistent: true);
        await audit.WriteAsync("Register", true, $"Регистрация пользователя {model.Email}", user.Id, model.Email, ZooRoles.Visitor);

        TempData["Toast"] = "Регистрация завершена. Добро пожаловать в зоопарк!";
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await audit.WriteAsync("Logout", true, "Выход из веб-интерфейса");
        await signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return NotFound();
        }

        ViewBag.Roles = await userManager.GetRolesAsync(user);
        return View(user);
    }

    [HttpGet]
    public async Task<IActionResult> AccessDenied(string? returnUrl = null)
    {
        var model = new AccessDeniedViewModel
        {
            ReturnUrl = returnUrl,
            IsAnonymous = User.Identity?.IsAuthenticated != true,
            AttemptedAction = HttpContext.Request.Query["action"].ToString()
        };

        await audit.WriteAsync("AccessDenied", false,
            $"Отказано в доступе: {returnUrl ?? Request.Path}", User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name, User.FindFirstValue(ClaimTypes.Role));

        return View(model);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Demo() => View(_zoo);
}
