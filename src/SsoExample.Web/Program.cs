var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.Required.json", optional: false, reloadOnChange: true);

if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Optional.json", optional: true, reloadOnChange: true);
}

builder.Configuration
    .AddEnvironmentVariables()
    .AddCommandLine(args);
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok", runtime = ".NET 10", service = "SsoExample.Web" }));
app.MapGet("/config", (IConfiguration config) => Results.Ok(new
{
    microsoftEntraId = new
    {
        authority = config["MicrosoftEntraId:Authority"],
        clientId = config["MicrosoftEntraId:ClientId"],
        redirectUris = config.GetSection("MicrosoftEntraId:RedirectUris").GetChildren().Select(x => x.Value).Where(x => x is not null),
        scopes = config.GetSection("MicrosoftEntraId:Scopes").GetChildren().Select(x => x.Value).Where(x => x is not null),
        cacheLocation = config["MicrosoftEntraId:CacheLocation"]
    },
    api = new
    {
        baseUrl = config["Api:BaseUrl"],
        localDemoClientId = config["Api:LocalDemoClientId"],
        requiredScope = config["Api:RequiredScope"]
    }
}));
app.MapFallbackToFile("index.html");

app.Run();
