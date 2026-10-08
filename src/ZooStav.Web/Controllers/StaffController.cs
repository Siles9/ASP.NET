using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZooStav.Web.Data;
using ZooStav.Web.Domain;
using ZooStav.Web.Services;

namespace ZooStav.Web.Controllers;

/// <summary>
/// Служебный раздел зоопарка. Доступен только роли Staff —
/// демонстрация авторизации по ролям (посетитель получит 403 / страницу «Доступ запрещён»).
/// </summary>
[Authorize(Roles = ZooRoles.Staff)]
[Route("staff")]
public class StaffController(
    ZooDbContext db,
    UserManager<ZooUser> userManager,
    IAuditService audit) : Controller
{
    /// <summary>Журнал действий пользователей (кто, когда, что делал).</summary>
    [HttpGet("audit")]
    public async Task<IActionResult> Audit(string? action = null, bool? success = null, int page = 1, CancellationToken ct = default)
    {
        const int pageSize = 25;

        var query = db.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(a => a.Action == action);
        }

        if (success is not null)
        {
            query = query.Where(a => a.Success == success);
        }

        var total = await query.CountAsync(ct);

        ViewBag.Actions = await db.AuditLogs.AsNoTracking().Select(a => a.Action).Distinct().OrderBy(a => a).ToListAsync(ct);
        ViewBag.Action = action;
        ViewBag.Success = success;
        ViewBag.Page = page;
        ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
        ViewBag.Total = total;

        var items = await query
            .OrderByDescending(a => a.TimestampUtc)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return View(items);
    }

    /// <summary>Управление пользователями и ролями.</summary>
    [HttpGet("users")]
    public async Task<IActionResult> Users(CancellationToken ct)
    {
        var users = await db.Users.AsNoTracking().OrderBy(u => u.Email).ToListAsync(ct);
        var result = new List<(ZooUser User, IList<string> Roles)>();

        foreach (var user in users)
        {
            result.Add((user, await userManager.GetRolesAsync(user)));
        }

        return View(result);
    }

    [HttpPost("users/{id}/role")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(string id, string role, CancellationToken ct)
    {
        if (!ZooRoles.All.Contains(role))
        {
            return BadRequest("Неизвестная роль");
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        var current = await userManager.GetRolesAsync(user);
        await userManager.RemoveFromRolesAsync(user, current);
        await userManager.AddToRoleAsync(user, role);
        await audit.WriteAsync("RoleChange", true, $"Пользователю {user.Email} назначена роль {role}", ct: ct);

        TempData["Toast"] = $"Пользователю {user.Email} назначена роль «{role}».";
        return RedirectToAction(nameof(Users));
    }
}
