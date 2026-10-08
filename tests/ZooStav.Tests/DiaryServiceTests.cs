using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ZooStav.Web.Data;
using ZooStav.Web.Domain;
using ZooStav.Web.Services;
using Xunit;

namespace ZooStav.Tests;

public class DiaryServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ZooDbContext _db;
    private readonly DiaryService _service;

    public DiaryServiceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ZooDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new ZooDbContext(options);
        _db.Database.EnsureCreated();
        _service = new DiaryService(_db);

        Seed();
    }

    private void Seed()
    {
        var keeper = new ZooUser
        {
            Id = "u-keeper",
            UserName = "keeper@zoostav.ru",
            NormalizedUserName = "KEEPER@ZOOSTAV.RU",
            Email = "keeper@zoostav.ru",
            FullName = "Смотритель вольера",
            SecurityStamp = Guid.NewGuid().ToString()
        };

        var vet = new ZooUser
        {
            Id = "u-vet",
            UserName = "vet@zoostav.ru",
            NormalizedUserName = "VET@ZOOSTAV.RU",
            Email = "vet@zoostav.ru",
            FullName = "Ветеринарный врач",
            SecurityStamp = Guid.NewGuid().ToString()
        };

        _db.Users.AddRange(keeper, vet);

        var raccoon = new Animal { Slug = "raccoon", Name = "Енот-полоскун", Species = "Енот-полоскун" };
        _db.Animals.Add(raccoon);
        _db.SaveChanges();

        _db.DiaryEntries.AddRange(
            new DiaryEntry
            {
                AnimalId = raccoon.Id, Type = DiaryEntryType.Feeding, UserId = keeper.Id,
                OccurredAtUtc = new DateTime(2026, 10, 7, 6, 0, 0, DateTimeKind.Utc),
                Title = "Утреннее кормление", Description = "120 г рыбы, 1 яйцо", PerformedBy = "Смотритель",
                CreatedAtUtc = new DateTime(2026, 10, 7, 6, 5, 0, DateTimeKind.Utc)
            },
            new DiaryEntry
            {
                AnimalId = raccoon.Id, Type = DiaryEntryType.Vaccination, UserId = vet.Id,
                OccurredAtUtc = new DateTime(2026, 9, 12, 8, 0, 0, DateTimeKind.Utc),
                Title = "Плановая вакцинация", Description = "Nobivac, реакция отсутствует", PerformedBy = "Ветврач",
                CreatedAtUtc = new DateTime(2026, 9, 12, 8, 10, 0, DateTimeKind.Utc)
            },
            new DiaryEntry
            {
                AnimalId = raccoon.Id, Type = DiaryEntryType.Illness, UserId = vet.Id,
                OccurredAtUtc = new DateTime(2026, 8, 9, 5, 0, 0, DateTimeKind.Utc),
                Title = "Отказ от корма, отит", Description = "Воспаление ушной раковины", PerformedBy = "Ветврач",
                CreatedAtUtc = new DateTime(2026, 8, 9, 5, 30, 0, DateTimeKind.Utc)
            },
            new DiaryEntry
            {
                AnimalId = raccoon.Id, Type = DiaryEntryType.Feeding, UserId = keeper.Id,
                OccurredAtUtc = new DateTime(2026, 8, 20, 17, 30, 0, DateTimeKind.Utc),
                Title = "Вечернее кормление", Description = "Творог, изюм (поощрение)", PerformedBy = "Смотритель",
                CreatedAtUtc = new DateTime(2026, 8, 20, 17, 35, 0, DateTimeKind.Utc)
            });

        _db.SaveChanges();
    }

    [Fact]
    public async Task Без_фильтров_возвращаются_все_записи_по_убыванию_даты()
    {
        var result = await _service.GetAsync(new DiaryFilter());

        Assert.Equal(4, result.TotalCount);
        Assert.Equal("Утреннее кормление", result.Items[0].Title);
        Assert.Equal(DiaryEntryType.Illness, result.Items[^1].Type);
    }

    [Fact]
    public async Task Фильтр_по_типу_записи()
    {
        var result = await _service.GetAsync(new DiaryFilter { Type = DiaryEntryType.Feeding });

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, x => Assert.Equal(DiaryEntryType.Feeding, x.Type));
    }

    [Fact]
    public async Task Фильтр_по_периоду_дат()
    {
        var result = await _service.GetAsync(new DiaryFilter
        {
            DateFrom = new DateOnly(2026, 10, 1),
            DateTo = new DateOnly(2026, 10, 31)
        });

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("Утреннее кормление", result.Items[0].Title);
    }

    [Fact]
    public async Task Фильтр_по_пользователю_автору_записи()
    {
        var result = await _service.GetAsync(new DiaryFilter { UserName = "vet@zoostav.ru" });

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, x => Assert.Equal("u-vet", x.UserId));
    }

    [Fact]
    public async Task Поиск_по_тексту_заголовка_и_описания()
    {
        var result = await _service.GetAsync(new DiaryFilter { Search = "отит" });

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(DiaryEntryType.Illness, result.Items[0].Type);
    }

    [Fact]
    public async Task Сортировка_по_возрастанию_даты()
    {
        var result = await _service.GetAsync(new DiaryFilter { Sort = "date_asc" });

        Assert.Equal("Отказ от корма, отит", result.Items[0].Title);
        Assert.Equal("Утреннее кормление", result.Items[^1].Title);
    }

    [Fact]
    public async Task Сортировка_по_типу_записи()
    {
        var result = await _service.GetAsync(new DiaryFilter { Sort = "type" });

        Assert.Equal(DiaryEntryType.Feeding, result.Items[0].Type);
    }

    [Fact]
    public async Task Комбинация_фильтров_и_пагинация()
    {
        var filter = new DiaryFilter
        {
            Slug = "raccoon",
            UserName = "keeper@zoostav.ru",
            Page = 1,
            PageSize = 1,
            Sort = "date_asc"
        };

        var page1 = await _service.GetAsync(filter);
        Assert.Equal(2, page1.TotalCount);
        Assert.Equal(2, page1.TotalPages);
        Assert.Single(page1.Items);
        Assert.True(page1.HasNext);
        Assert.False(page1.HasPrevious);

        filter.Page = 2;
        var page2 = await _service.GetAsync(filter);
        Assert.Single(page2.Items);
        Assert.True(page2.HasPrevious);
        Assert.NotEqual(page1.Items[0].Id, page2.Items[0].Id);
    }

    [Fact]
    public async Task Фильтр_по_несуществующему_слагу_возвращает_пустой_результат()
    {
        var result = await _service.GetAsync(new DiaryFilter { Slug = "fox" });

        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Добавление_записи_сохраняется_в_базу()
    {
        var entry = await _service.AddAsync(new DiaryEntry
        {
            AnimalId = 1,
            Type = DiaryEntryType.Measurement,
            OccurredAtUtc = DateTime.UtcNow,
            Title = "Взвешивание",
            Description = "7,4 кг",
            CreatedVia = "Api"
        });

        Assert.True(entry.Id > 0);

        var fromDb = await _db.DiaryEntries.FindAsync(entry.Id);
        Assert.NotNull(fromDb);
        Assert.Equal("Взвешивание", fromDb!.Title);
        Assert.Equal("Api", fromDb.CreatedVia);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
