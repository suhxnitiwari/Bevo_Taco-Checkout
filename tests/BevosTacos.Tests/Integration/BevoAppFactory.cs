using System.Net;
using System.Text.RegularExpressions;
using BevosTacos.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace BevosTacos.Tests.Integration;

//Runs the real app against its own SQLite file with the menu, roles and demo accounts but no sample orders
public class BevoAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"bevostacos-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Sqlite", $"Data Source={_dbPath};Pooling=False");
        builder.UseSetting("Demo:SampleOrders", "false");
        builder.UseSetting("Demo:Enabled", "true");
    }

    //A browser-like client: keeps cookies, doesn't follow redirects so tests can check them
    public TestClient NewClient() =>
        new(CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true }));

    public T WithDb<T>(Func<AppDbContext, T> query)
    {
        using var scope = Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}

public partial class TestClient(HttpClient http)
{
    public HttpClient Http => http;

    public Task<HttpResponseMessage> Get(string url) => http.GetAsync(url);

    //Posts a form the way a browser would, with the anti-forgery token from a page that has one
    public async Task<HttpResponseMessage> Post(string url, Dictionary<string, string> fields, string tokenPage = "/Menu")
    {
        var html = await (await http.GetAsync(tokenPage)).Content.ReadAsStringAsync();
        var token = TokenRegex().Match(html).Groups[1].Value;
        var form = new List<KeyValuePair<string, string>>(fields) { new("__RequestVerificationToken", token) };
        return await http.PostAsync(url, new FormUrlEncodedContent(form));
    }

    public Task<HttpResponseMessage> Post(string url, params (string Key, string Value)[] fields) =>
        Post(url, fields.ToDictionary(f => f.Key, f => f.Value));

    public async Task SignInAs(string demoRole)
    {
        var response = await Post("/Account/Demo", ("role", demoRole));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();
}
