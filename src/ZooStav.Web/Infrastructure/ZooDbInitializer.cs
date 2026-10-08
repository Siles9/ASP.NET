using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ZooStav.Web.Data;
using ZooStav.Web.Domain;

namespace ZooStav.Web.Infrastructure;

public static class ZooDbInitializer
{
    public static readonly (string Email, string Password, string Role, string FullName)[] SeedUsers =
    {
        ("keeper@zoostav.ru", "Keeper#2026", ZooRoles.Staff, "Смотритель вольера Енотов"),
        ("visitor@zoostav.ru", "Visitor#2026", ZooRoles.Visitor, "Посетитель Иван")
    };

    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration,
        ILogger logger, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        var db = sp.GetRequiredService<ZooDbContext>();
        var userManager = sp.GetRequiredService<UserManager<ZooUser>>();
        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole>>();
        var env = sp.GetRequiredService<IWebHostEnvironment>();

        var provider = db.Database.ProviderName ?? string.Empty;

        if (db.Database.IsRelational())
        {
            var migrationsAssembly = provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase)
                ? "ZooStav.Migrations.SqlServer"
                : "ZooStav.Migrations.Sqlite";

            await db.Database.MigrateAsync(ct);

            if (env.IsDevelopment())
            {
                logger.LogInformation("Применены миграции ({Provider}, сборка {Assembly})", provider, migrationsAssembly);
            }
        }
        else
        {
            await db.Database.EnsureCreatedAsync(ct);
        }

        foreach (var role in ZooRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        foreach (var (email, password, role, fullName) in SeedUsers)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ZooUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = fullName
                };

                var created = await userManager.CreateAsync(user, password);
                if (!created.Succeeded)
                {
                    logger.LogWarning("Не удалось создать пользователя {Email}: {Errors}", email,
                        string.Join("; ", created.Errors.Select(e => e.Description)));
                    continue;
                }
            }

            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }

        if (await db.Animals.AnyAsync(ct))
        {
            return;
        }

        var keeper = await userManager.FindByEmailAsync("keeper@zoostav.ru");
        var visitor = await userManager.FindByEmailAsync("visitor@zoostav.ru");

        var raccoon = new Animal
        {
            Slug = "raccoon",
            Name = "Енот-полоскун",
            Species = "Енот-полоскун",
            LatinName = "Procyon lotor",
            Gender = "Самец",
            BirthDate = new DateOnly(2022, 4, 18),
            Status = "Здоров",
            Enclosure = "Вольер №7 «Лесная тропа», контактная зона",
            WebcamUrl = "https://www.youtube.com/embed/live_stream?channel=UCCnH7nUuYvXKPR4pPqEfHqQ",
            Summary = "Енот-полоскун Тимка — самый узнаваемый обитатель контактной зоны зоопарка в Парке Победы: " +
                      "любопытный, сообразительный и невероятно ловкий. Своё второе имя — «полоскун» — он получил " +
                      "за привычку тщательно «стирать» в воде всё съедобное и не очень, что попадёт в ловкие передние лапы.",
            Habitat = "Природный ареал — Северная Америка: от юга Канады до Панамы. Еноты предпочитают " +
                      "лиственные леса у воды, поймы рек, болота, но легко осваивают и пригороды. Отлично лазают " +
                      "по деревьям, уверенно плавают, активны преимущественно в сумерках и ночью.",
            Diet = "Рацион всеядный: рыба, насекомые, лягушки, раки, яйца, ягоды, орехи, жёлуди, фрукты, " +
                   "корневища растений. В зоопарке основной рацион — рыба, курица, творог, яйца, сезонные " +
                   "фрукты и овощи; изюм и орехи используются как поощрение при тренинге.",
            History = "Тимка родился 18 апреля 2022 года в питомнике под Москвой и в возрасте четырёх месяцев " +
                      "переехал в Ставрополь. Первое время жил в карантинном вольере, а с весны 2023 года " +
                      "постоянно живёт в вольере №7 контактной зоны. Тимка прошёл курс мягкой адаптации к людям, " +
                      "выполняет простые команды за лакомство и охотно позирует посетителям. В марте 2025 года у " +
                      "Тимки и самки Лоры родилось три щенка — сейчас они подрастают в соседнем вольере."
        };

        var media = new List<MediaItem>
        {
            new()
            {
                Type = MediaType.Photo,
                Title = "Тимка на прогулке по вольеру",
                Url = "/images/raccoon/raccoon-hero.jpg"
            },
            new()
            {
                Type = MediaType.Photo,
                Title = "Портрет: маска енота в деталях",
                Url = "/images/raccoon/raccoon-portrait.jpg"
            },
            new()
            {
                Type = MediaType.Photo,
                Title = "«Стирка» яблока в поилке",
                Url = "/images/raccoon/raccoon-feeding.jpg"
            },
            new()
            {
                Type = MediaType.Photo,
                Title = "Щенки 2025 года рождения",
                Url = "/images/raccoon/raccoon-babies.jpg"
            },
            new()
            {
                Type = MediaType.Photo,
                Title = "Вольер №7 сегодня (стоп-кадр с веб-камеры)",
                Url = "/images/raccoon/raccoon-enclosure.jpg"
            },
            new()
            {
                Type = MediaType.Video,
                Title = "Завтрак Тимки: кормление с рук",
                Url = "https://www.youtube.com/embed/P73REgj-3UE",
                PosterUrl = "/images/raccoon/raccoon-feeding.jpg"
            },
            new()
            {
                Type = MediaType.Video,
                Title = "Ночная жизнь вольера №7",
                Url = "https://www.youtube.com/embed/Y8Wp3dafaMQ",
                PosterUrl = "/images/raccoon/raccoon-enclosure.jpg"
            }
        };

        raccoon.Media = media;

        var diary = new List<DiaryEntry>
        {
            new()
            {
                Type = DiaryEntryType.Feeding,
                OccurredAtUtc = DateTime.UtcNow.Date.AddDays(-0).AddHours(4.5),
                Title = "Утреннее кормление",
                Description = "Рацион: 120 г рыбы, 60 г куриного филе, 1 варёное яйцо, творог. Воду в поилке заменили дважды.",
                PerformedBy = "Смотритель вольера №7",
                Location = "Вольер №7",
                UserId = keeper?.Id,
                CreatedVia = "Web"
            },
            new()
            {
                Type = DiaryEntryType.Observation,
                OccurredAtUtc = DateTime.UtcNow.Date.AddDays(-0).AddHours(2),
                Title = "Наблюдение за поведением",
                Description = "Активно исследовал новые ветки, 12 минут «полоскал» еловую шишку в поилке. Аппетит хороший.",
                PerformedBy = "Зоолог",
                Location = "Вольер №7",
                UserId = keeper?.Id,
                CreatedVia = "Api"
            },
            new()
            {
                Type = DiaryEntryType.Vaccination,
                OccurredAtUtc = DateTime.UtcNow.Date.AddDays(-9).AddHours(3),
                Title = "Плановая вакцинация",
                Description = "Введена вакцина Nobivac против чумы плотоядных и лептоспироза. Реакции не наблюдалось.",
                PerformedBy = "Ветеринарный врач Кузнецова А. И.",
                Location = "Ветеринарный блок",
                UserId = keeper?.Id,
                CreatedVia = "Web"
            },
            new()
            {
                Type = DiaryEntryType.Measurement,
                OccurredAtUtc = DateTime.UtcNow.Date.AddDays(-9).AddHours(3.5),
                Title = "Взвешивание и замер",
                Description = "Масса 7,4 кг, длина тела 61 см, обхват груди 38 см. Показатели в норме для возраста.",
                PerformedBy = "Ветеринарный врач Кузнецова А. И.",
                Location = "Ветеринарный блок",
                UserId = keeper?.Id,
                CreatedVia = "Web"
            },
            new()
            {
                Type = DiaryEntryType.Offspring,
                OccurredAtUtc = DateTime.UtcNow.Date.AddDays(-180).AddHours(6),
                Title = "Рождение потомства",
                Description = "У пары Тимка × Лора родилось 3 щенка. Все щенки активные, вес 95–110 г, вскармливание самостоятельное.",
                PerformedBy = "Зоолог Мамедов Р. О.",
                Location = "Родильный вольер №6",
                UserId = keeper?.Id,
                CreatedVia = "Web"
            },
            new()
            {
                Type = DiaryEntryType.Mating,
                OccurredAtUtc = DateTime.UtcNow.Date.AddDays(-230).AddHours(7),
                Title = "Спаривание",
                Description = "Зафиксировано спаривание с самкой Лорой. Пара подобрана по рекомендации координатора ЕАРАЗА.",
                PerformedBy = "Зоолог Мамедов Р. О.",
                Location = "Вольер №6",
                UserId = keeper?.Id,
                CreatedVia = "Api"
            },
            new()
            {
                Type = DiaryEntryType.Illness,
                OccurredAtUtc = DateTime.UtcNow.Date.AddDays(-60).AddHours(1),
                Title = "Отказ от корма, вялость",
                Description = "Утром отказался от рыбы, температура 39,4 °C. Осмотр: незначительное воспаление ушной раковины (отит).",
                PerformedBy = "Ветеринарный врач Кузнецова А. И.",
                Location = "Вольер №7",
                UserId = keeper?.Id,
                CreatedVia = "Web"
            },
            new()
            {
                Type = DiaryEntryType.Treatment,
                OccurredAtUtc = DateTime.UtcNow.Date.AddDays(-59).AddHours(2),
                Title = "Курс лечения отита",
                Description = "Назначен курс: капли «Отибиовин» 3 раза в день, 7 дней. Кормление переведено на мягкую пищу.",
                PerformedBy = "Ветеринарный врач Кузнецова А. И.",
                Location = "Карантинный блок",
                UserId = keeper?.Id,
                CreatedVia = "Web"
            },
            new()
            {
                Type = DiaryEntryType.Treatment,
                OccurredAtUtc = DateTime.UtcNow.Date.AddDays(-52).AddHours(2),
                Title = "Завершение курса лечения",
                Description = "Осмотр: ушные раковины чистые, животное активное, питание восстановлено полностью. Признан здоровым.",
                PerformedBy = "Ветеринарный врач Кузнецова А. И.",
                Location = "Вольер №7",
                UserId = keeper?.Id,
                CreatedVia = "Web"
            },
            new()
            {
                Type = DiaryEntryType.Relocation,
                OccurredAtUtc = DateTime.UtcNow.Date.AddDays(-400).AddHours(5),
                Title = "Перевод в вольер контактной зоны",
                Description = "После успешной адаптации переведён из карантина в вольер №7 «Лесная тропа».",
                PerformedBy = "Смотритель вольера №7",
                Location = "Вольер №7",
                UserId = keeper?.Id,
                CreatedVia = "Web"
            },
            new()
            {
                Type = DiaryEntryType.Other,
                OccurredAtUtc = DateTime.UtcNow.Date.AddDays(-14).AddHours(6),
                Title = "Обогащение среды",
                Description = "Установлены новые деревянные платформы и «кормушка-головоломка» с изюмом.",
                PerformedBy = "Смотритель вольера №7",
                Location = "Вольер №7",
                UserId = keeper?.Id,
                CreatedVia = "Web"
            }
        };

        raccoon.DiaryEntries = diary;

        raccoon.Donations = new List<Donation>
        {
            new()
            {
                DonorName = "Анна К.",
                DonorEmail = "anna@example.com",
                Amount = 1500m,
                Purpose = DonationPurposes.Food,
                Message = "Тимке на рыбку! Спасибо, что пускаете нас к нему каждые выходные.",
                Status = DonationStatus.Succeeded,
                PaymentMethod = "card",
                PaymentReference = "DEMO-0001",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-21)
            },
            new()
            {
                DonorName = "Школа №18, 3 «Б»",
                Amount = 10000m,
                Purpose = DonationPurposes.Care,
                Message = "На обустройство вольера от третьеклассников.",
                Status = DonationStatus.Succeeded,
                PaymentMethod = "card",
                PaymentReference = "DEMO-0002",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-12)
            },
            new()
            {
                DonorName = "Иван П.",
                Amount = 700m,
                Purpose = DonationPurposes.Treatment,
                Message = "Пусть не болеет больше.",
                Status = DonationStatus.Succeeded,
                PaymentMethod = "free",
                PaymentReference = "DEMO-0003",
                CreatedAtUtc = DateTime.UtcNow.AddDays(-5)
            }
        };

        db.Animals.Add(raccoon);

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Демонстрационные данные созданы: животное {Slug}, записей дневника: {Count}",
            raccoon.Slug, diary.Count);

        _ = visitor;
    }
}
