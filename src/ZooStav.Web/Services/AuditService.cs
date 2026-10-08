using Microsoft.EntityFrameworkCore;
using ZooStav.Web.Data;
using ZooStav.Web.Domain;

namespace ZooStav.Web.Services;

public interface IAuditService
{
    Task WriteAsync(string action, bool success, string details, CancellationToken ct = default);
    Task WriteAsync(string action, bool success, string details, string? userId, string? userName, string? role, CancellationToken ct = default);
    IQueryable<AuditLog> Query();
}

public class AuditService(ZooDbContext db, IHttpContextAccessor accessor) : IAuditService
{
    public Task WriteAsync(string action, bool success, string details, CancellationToken ct = default)
    {
        var user = accessor.HttpContext?.User;
        return WriteAsync(action, success, details, user?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            user?.Identity?.Name, user?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value, ct);
    }

    public async Task WriteAsync(string action, bool success, string details, string? userId, string? userName,
        string? role, CancellationToken ct = default)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Action = Trim(action, 64),
            Success = success,
            Details = Trim(details, 512),
            UserId = userId,
            UserName = Trim(userName, 128),
            Role = role,
            IpAddress = Trim(accessor.HttpContext?.Connection.RemoteIpAddress?.ToString(), 64)
        });

        await db.SaveChangesAsync(ct);
    }

    public IQueryable<AuditLog> Query() => db.AuditLogs.AsNoTracking().OrderByDescending(a => a.TimestampUtc);

    private static string Trim(string? value, int max) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Length <= max ? value : value[..max];
}
