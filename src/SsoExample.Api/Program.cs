using SsoExample.Api.Data;
using SsoExample.Api.Endpoints;
using SsoExample.Api.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.Required.json", optional: false, reloadOnChange: true)
    .AddJsonFile("appsettings.Optional.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

builder.Services.AddSingleton<InMemorySsoStore>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials().SetIsOriginAllowed(_ => true)));

var app = builder.Build();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    runtime = ".NET 10",
    service = "SsoExample.Api",
    timestamp = DateTimeOffset.UtcNow
})).WithTags("Health");

app.MapGet("/api/info", (IConfiguration config) => Results.Ok(new
{
    service = "SsoExample.Api",
    provider = config["Authentication:Provider"],
    authority = config["Authentication:MicrosoftEntraId:Authority"],
    audience = config["Authentication:MicrosoftEntraId:Api:Audience"],
    scope = config["Authentication:MicrosoftEntraId:Api:Scopes:AccessAsUser"]
})).WithTags("Health");

app.MapAuthEndpoints();
app.MapSsoEndpoints();
app.MapBusinessEndpoints();
app.MapAdminEndpoints();

app.MapFallbackToFile("index.html");
app.Run();
