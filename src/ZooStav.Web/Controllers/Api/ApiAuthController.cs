using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ZooStav.Web.Domain;
using ZooStav.Web.Mapping;
using ZooStav.Web.Services;
using ZooStav.Web.ViewModels;
using ZooStav.Web.ViewModels.Api;

namespace ZooStav.Web.Controllers.Api;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class ApiAuthController(
    UserManager<ZooUser> userManager,
    SignInManager<ZooUser> signInManager,
    ITokenService tokens,
    IAuditService audit) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ApiAuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] ApiRegisterRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse.Fail(string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))));
        }

        if (await userManager.FindByEmailAsync(request.Email) is not null)
        {
            return BadRequest(ApiResponse.Fail("Пользователь с таким e-mail уже зарегистрирован."));
        }

        var user = new ZooUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true,
            FullName = request.FullName
        };

        var created = await userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
        {
            return BadRequest(ApiResponse.Fail(string.Join("; ", created.Errors.Select(e => e.Description))));
        }

        await userManager.AddToRoleAsync(user, ZooRoles.Visitor);

        var roles = await userManager.GetRolesAsync(user);
        var (token, expires) = tokens.CreateAccessToken(user, roles);

        await audit.WriteAsync("ApiRegister", true, $"Регистрация через API: {request.Email}", user.Id, user.Email, ZooRoles.Visitor);

        return Ok(ApiResponse<ApiAuthResponse>.Ok(new ApiAuthResponse
        {
            AccessToken = token,
            RefreshToken = tokens.CreateRefreshToken(),
            ExpiresAtUtc = expires,
            User = user.ToDto(roles.ToArray()),
            Roles = roles.ToArray()
        }));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ApiAuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] ApiLoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse.Fail("Некорректные данные запроса."));
        }

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            await audit.WriteAsync("ApiLogin", false, $"Неудачный вход через API: {request.Email}", null, request.Email, null);
            return Unauthorized(ApiResponse.Fail("Неверный e-mail или пароль."));
        }

        var check = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!check.Succeeded)
        {
            await audit.WriteAsync("ApiLogin", false, $"Неудачный вход через API: {request.Email}", user.Id, request.Email, null);
            return Unauthorized(ApiResponse.Fail(check.IsLockedOut
                ? "Учётная запись заблокирована."
                : "Неверный e-mail или пароль."));
        }

        var roles = await userManager.GetRolesAsync(user);
        var (token, expires) = tokens.CreateAccessToken(user, roles);

        await audit.WriteAsync("ApiLogin", true, $"Вход через API: {request.Email}", user.Id, request.Email,
            string.Join(",", roles));

        return Ok(ApiResponse<ApiAuthResponse>.Ok(new ApiAuthResponse
        {
            AccessToken = token,
            RefreshToken = tokens.CreateRefreshToken(),
            ExpiresAtUtc = expires,
            User = user.ToDto(roles.ToArray()),
            Roles = roles.ToArray()
        }));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<ApiAuthResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] ApiRefreshRequest request)
    {
        var header = Request.Headers.Authorization.ToString().Replace("Bearer ", string.Empty);
        var principal = tokens.ValidateToken(header, validateLifetime: false);

        if (principal is null || string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Unauthorized(ApiResponse.Fail("Невозможно обновить токен: требуется корректный access-токен и refreshToken."));
        }

        var user = await userManager.FindByIdAsync(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty);
        if (user is null)
        {
            return Unauthorized(ApiResponse.Fail("Пользователь не найден."));
        }

        var roles = await userManager.GetRolesAsync(user);
        var (token, expires) = tokens.CreateAccessToken(user, roles);

        return Ok(ApiResponse<ApiAuthResponse>.Ok(new ApiAuthResponse
        {
            AccessToken = token,
            RefreshToken = request.RefreshToken,
            ExpiresAtUtc = expires,
            User = user.ToDto(roles.ToArray()),
            Roles = roles.ToArray()
        }));
    }

    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<ApiUserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized(ApiResponse.Fail("Пользователь не найден."));
        }

        var roles = await userManager.GetRolesAsync(user);
        return Ok(ApiResponse<ApiUserDto>.Ok(user.ToDto(roles.ToArray())));
    }
}
