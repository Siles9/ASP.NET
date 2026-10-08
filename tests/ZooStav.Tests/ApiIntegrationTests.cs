using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ZooStav.Tests;

public class ApiIntegrationTests : IClassFixture<ZooWebApplicationFactory>
{
    private readonly ZooWebApplicationFactory _factory;

    public ApiIntegrationTests(ZooWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Главная_страница_зоопарка_отдаёт_ссылку_на_zoostav_ru()
    {
        var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/");

        Assert.Contains("https://zoostav.ru", html);
    }

    [Fact]
    public async Task Страница_животного_доступна_по_поддомену_raccoon()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Host = "raccoon.zoostav.ru";

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Енот-полоскун", html);
        Assert.Contains("https://zoostav.ru", html);
        Assert.Contains("id=\"webcam\"", html);
        Assert.Contains("id=\"donate\"", html);
    }

    [Fact]
    public async Task Api_карточка_животного_публична_и_содержит_медиа()
    {
        var client = _factory.CreateClient();

        var json = await client.GetFromJsonAsync<JsonElement>("/api/animals/raccoon");

        Assert.True(json.GetProperty("success").GetBoolean());
        var data = json.GetProperty("data");
        Assert.Equal("raccoon", data.GetProperty("slug").GetString());
        Assert.Equal("https://zoostav.ru", data.GetProperty("zooMainSiteUrl").GetString());
        Assert.Equal("https://raccoon.zoostav.ru/", data.GetProperty("subdomainUrl").GetString());
        Assert.True(data.GetProperty("photos").GetArrayLength() > 0);
        Assert.True(data.GetProperty("diaryAvailable").ValueKind == JsonValueKind.False);
    }

    [Fact]
    public async Task Дневник_недоступен_без_токена()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/diary");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Работник_входит_по_JWT_и_создаёт_запись_дневника()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "keeper@zoostav.ru", "Keeper#2026");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/animals/raccoon/diary", new
        {
            type = "Feeding",
            title = "Тестовое кормление",
            description = "Создано интеграционным тестом",
            performedBy = "xUnit"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var data = created.GetProperty("data");
        Assert.Equal("Feeding", data.GetProperty("type").GetString());
        Assert.Equal("Кормление", data.GetProperty("typeTitle").GetString());
        Assert.Equal("keeper@zoostav.ru", data.GetProperty("userName").GetString());
        Assert.Equal("Api", data.GetProperty("createdVia").GetString());

        var list = await client.GetFromJsonAsync<JsonElement>("/api/animals/raccoon/diary?type=Feeding&search=Тестовое");
        Assert.True(list.GetProperty("data").GetProperty("totalCount").GetInt32() >= 1);
    }

    [Fact]
    public async Task Посетитель_не_может_писать_в_дневник()
    {
        var client = _factory.CreateClient();
        var token = await LoginAsync(client, "visitor@zoostav.ru", "Visitor#2026");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.PostAsJsonAsync("/api/animals/raccoon/diary", new
        {
            type = "Other",
            title = "Попытка посетителя"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Фильтрация_дневника_через_API_по_типу_и_дате()
    {
        var client = _factory.CreateClient();

        var json = await client.GetFromJsonAsync<JsonElement>(
            "/api/animals/raccoon/diary?type=Vaccination&dateFrom=2000-01-01&dateTo=2100-01-01&sort=date_desc&pageSize=5");

        var data = json.GetProperty("data");
        Assert.True(data.GetProperty("totalCount").GetInt32() >= 1);
        foreach (var item in data.GetProperty("items").EnumerateArray())
        {
            Assert.Equal("Vaccination", item.GetProperty("type").GetString());
        }
    }

    [Fact]
    public async Task Донат_принимается_анонимно_и_отражается_в_сводке()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/donations", new
        {
            animalSlug = "raccoon",
            donorName = "Тестовый донатор",
            amount = 1234,
            purpose = "food",
            message = "Интеграционный тест"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var summary = await client.GetFromJsonAsync<JsonElement>("/api/donations/raccoon/summary");
        var data = summary.GetProperty("data");
        Assert.True(data.GetProperty("total").GetDecimal() >= 1234);
        Assert.True(data.GetProperty("foodTotal").GetDecimal() >= 1234);
    }

    [Fact]
    public async Task Регистрация_через_API_выдаёт_роль_посетителя()
    {
        var client = _factory.CreateClient();
        var email = $"test-{Guid.NewGuid():N}@example.com";

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "Test#12345",
            fullName = "Новый Посетитель"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var roles = json.GetProperty("data").GetProperty("roles").EnumerateArray().Select(x => x.GetString()).ToList();

        Assert.Contains("Visitor", roles);
        Assert.DoesNotContain("Staff", roles);
        Assert.False(string.IsNullOrWhiteSpace(json.GetProperty("data").GetProperty("accessToken").GetString()));
    }

    [Fact]
    public async Task Служебный_раздел_недоступен_посетителю()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = await LoginAsync(client, "visitor@zoostav.ru", "Visitor#2026");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/diary");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task<string> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("data").GetProperty("accessToken").GetString()!;
    }
}

public class ZooWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"zoostav-tests-{Guid.NewGuid():N}.db");

    public ZooWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
        Environment.SetEnvironmentVariable("Database__Provider", "Sqlite");
        Environment.SetEnvironmentVariable("Database__FailFast", "true");
        Environment.SetEnvironmentVariable("ConnectionStrings__Sqlite", $"Data Source={_dbPath}");
        Environment.SetEnvironmentVariable("Zoo__RootDomain", "zoostav.ru");
        Environment.SetEnvironmentVariable("Zoo__MainSiteUrl", "https://zoostav.ru");
        Environment.SetEnvironmentVariable("Zoo__UseHttpsRedirection", "false");
        Environment.SetEnvironmentVariable("Zoo__TreatLocalhostAsRoot", "true");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch (IOException) {  }
        }
    }
}
