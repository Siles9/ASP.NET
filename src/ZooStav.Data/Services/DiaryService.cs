using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ZooStav.Web.Data;
using ZooStav.Web.Domain;

namespace ZooStav.Web.Services;

public class DiaryFilter
{
    public int? AnimalId { get; set; }
    public string? Slug { get; set; }
    public DiaryEntryType? Type { get; set; }

    public DateOnly? DateFrom { get; set; }

    public DateOnly? DateTo { get; set; }

    public string? UserName { get; set; }

    public string? UserId { get; set; }

    public string? Search { get; set; }

    public string Sort { get; set; } = "date_desc";

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    public void Normalize()
    {
        Page = Page < 1 ? 1 : Page;
        PageSize = PageSize is < 1 or > 200 ? 20 : PageSize;
        Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
        UserName = string.IsNullOrWhiteSpace(UserName) ? null : UserName.Trim();
        UserId = string.IsNullOrWhiteSpace(UserId) ? null : UserId.Trim();
        Slug = string.IsNullOrWhiteSpace(Slug) ? null : Slug.Trim();
    }
}

public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public interface IDiaryService
{
    IQueryable<DiaryEntry> BuildQuery(DiaryFilter filter);
    Task<PagedResult<DiaryEntry>> GetAsync(DiaryFilter filter, CancellationToken ct = default);
    Task<DiaryEntry> AddAsync(DiaryEntry entry, CancellationToken ct = default);
}

public class DiaryService(ZooDbContext db) : IDiaryService
{
    public IQueryable<DiaryEntry> BuildQuery(DiaryFilter filter)
    {
        filter.Normalize();

        var query = db.DiaryEntries
            .Include(d => d.Animal)
            .Include(d => d.User)
            .AsQueryable();

        if (filter.AnimalId is > 0)
        {
            query = query.Where(d => d.AnimalId == filter.AnimalId);
        }

        if (filter.Slug is not null)
        {
            query = query.Where(d => d.Animal!.Slug == filter.Slug);
        }

        if (filter.Type is not null)
        {
            query = query.Where(d => d.Type == filter.Type);
        }

        if (filter.DateFrom is not null)
        {
            var from = filter.DateFrom.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(d => d.OccurredAtUtc >= from);
        }

        if (filter.DateTo is not null)
        {
            var to = filter.DateTo.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(d => d.OccurredAtUtc <= to);
        }

        if (filter.UserId is not null)
        {
            query = query.Where(d => d.UserId == filter.UserId);
        }

        if (filter.UserName is not null)
        {
            var name = filter.UserName.ToLower();
            query = query.Where(d => d.User!.UserName!.ToLower().Contains(name) ||
                                     d.User!.FullName.ToLower().Contains(name) ||
                                     d.PerformedBy.ToLower().Contains(name));
        }

        if (filter.Search is not null)
        {
            var text = filter.Search.ToLowerInvariant().Replace('ё', 'е');
            query = query.Where(d => d.SearchText.Contains(text));
        }

        return ApplySort(query, filter.Sort);
    }

    public async Task<PagedResult<DiaryEntry>> GetAsync(DiaryFilter filter, CancellationToken ct = default)
    {
        filter.Normalize();

        var query = BuildQuery(filter);
        var total = await query.CountAsync(ct);

        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return new PagedResult<DiaryEntry>(items, total, filter.Page, filter.PageSize);
    }

    public async Task<DiaryEntry> AddAsync(DiaryEntry entry, CancellationToken ct = default)
    {
        db.DiaryEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry;
    }

    private static IQueryable<DiaryEntry> ApplySort(IQueryable<DiaryEntry> query, string sort) => sort switch
    {
        "date_asc" => query.OrderBy(d => d.OccurredAtUtc).ThenBy(d => d.Id),
        "type" => query.OrderBy(d => d.Type).ThenByDescending(d => d.OccurredAtUtc),
        "user" => query.OrderBy(d => d.User!.UserName).ThenByDescending(d => d.OccurredAtUtc),
        "created_desc" => query.OrderByDescending(d => d.CreatedAtUtc),
        _ => query.OrderByDescending(d => d.OccurredAtUtc).ThenByDescending(d => d.Id)
    };
}

public static class DiaryEntryTypeExtensions
{
    public static string Title(this DiaryEntryType type) => type switch
    {
        DiaryEntryType.Feeding => "Кормление",
        DiaryEntryType.Vaccination => "Вакцинация",
        DiaryEntryType.Mating => "Спаривание",
        DiaryEntryType.Offspring => "Потомство",
        DiaryEntryType.Illness => "Болезнь",
        DiaryEntryType.Treatment => "Лечение",
        DiaryEntryType.Measurement => "Измерения",
        DiaryEntryType.Relocation => "Перемещение",
        DiaryEntryType.Observation => "Наблюдение",
        _ => "Прочее"
    };

    public static string CssClass(this DiaryEntryType type) => "type-" + type.ToString().ToLowerInvariant();

}
