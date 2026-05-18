using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using SsoExample.Api.Data;
using SsoExample.Api.Security;

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

// Fail fast nếu Required còn placeholder
var tenantId = RequireConfig(builder.Configuration, "Authentication:MicrosoftEntraId:TenantId");
var authority = RequireConfig(builder.Configuration, "Authentication:MicrosoftEntraId:Authority");
var apiClientId = RequireConfig(builder.Configuration, "Authentication:MicrosoftEntraId:Api:ClientId");
var audience = RequireConfig(builder.Configuration, "Authentication:MicrosoftEntraId:Api:Audience");
var allowedClientIds = builder.Configuration
    .GetSection("Authentication:MicrosoftEntraId:AllowedClientApplications")
    .GetChildren()
    .Select(c => c["ClientId"])
    .Where(id => !string.IsNullOrWhiteSpace(id) && !id.Contains('<'))
    .Cast<string>()
    .ToArray();

if (allowedClientIds.Length == 0)
{
    throw new InvalidOperationException(
        "Authentication:MicrosoftEntraId:AllowedClientApplications phải có ít nhất một ClientId thật (không phải placeholder).");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.MetadataAddress = $"{authority.TrimEnd('/')}/.well-known/openid-configuration";
        options.RequireHttpsMetadata = true;
        options.MapInboundClaims = false; // giữ tên claim gốc: oid, roles, preferred_username, ...
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = new[]
            {
                $"https://login.microsoftonline.com/{tenantId}/v2.0",
                $"https://sts.windows.net/{tenantId}/"
            },
            ValidateAudience = true,
            ValidAudiences = new[] { audience, apiClientId },
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
            NameClaimType = "name",
            RoleClaimType = "roles"
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = ctx =>
            {
                var principal = ctx.Principal;
                if (principal is null)
                {
                    ctx.Fail("Token không có claims principal.");
                    return Task.CompletedTask;
                }

                var azp = principal.FindFirstValue("azp") ?? principal.FindFirstValue("appid");
                if (string.IsNullOrEmpty(azp) || !allowedClientIds.Contains(azp, StringComparer.OrdinalIgnoreCase))
                {
                    ctx.Fail($"Client app '{azp}' không nằm trong AllowedClientApplications.");
                }
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "SSOExample.Api",
        Version = "v1",
        Description = ".NET 10 Resource API · Microsoft Entra ID protected, login-as và audit log."
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT access token (Microsoft Entra ID hoặc local login-as token)."
    });

    options.AddSecurityRequirement(document =>
    {
        var requirement = new OpenApiSecurityRequirement();
        requirement.Add(new OpenApiSecuritySchemeReference("Bearer", document, null), new List<string>());
        return requirement;
    });

    var xmlFile = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
    if (File.Exists(xmlFile))
    {
        options.IncludeXmlComments(xmlFile);
    }
});

builder.Services.AddSingleton<InMemorySsoStore>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials().SetIsOriginAllowed(_ => true)));

var app = builder.Build();

app.UseCors();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "SSOExample.Api v1");
    options.RoutePrefix = string.Empty;
    options.DocumentTitle = "SSOExample.Api — Swagger UI";
});

app.UseMiddleware<RequestLogMiddleware>();

app.MapControllers();

app.Run();

static string RequireConfig(IConfiguration config, string key)
{
    var value = config[key];
    if (string.IsNullOrWhiteSpace(value) || value.Contains('<') || value.Contains('>'))
    {
        throw new InvalidOperationException(
            $"Configuration '{key}' chưa được cấu hình (giá trị hiện tại: '{value}'). Xem docs/azure-setup.md.");
    }
    return value;
}
