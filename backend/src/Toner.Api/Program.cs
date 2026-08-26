using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Toner.Api.Auth;
using Toner.Api.Middleware;
using Toner.Api.Serialization;
using Toner.Application.Common;
using Toner.Application.Assets;
using Toner.Application.Assignment;
using Toner.Application.Auth;
using Toner.Application.Cities;
using Toner.Application.Clients;
using Toner.Application.Common.Interfaces;
using Toner.Application.Contracts;
using Toner.Application.Dashboard;
using Toner.Application.Maintenance;
using Toner.Application.Technicians;
using Toner.Application.Tickets;
using Toner.Application.Users;
using Toner.Infrastructure.Auth;
using Toner.Infrastructure.Health;
using Toner.Infrastructure.Jobs;
using Toner.Infrastructure.Logging;
using Toner.Infrastructure.Persistence;
using Toner.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
    options.JsonSerializerOptions.Converters.Add(new UtcNullableDateTimeConverter());
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingresa el token JWT (sin el prefijo 'Bearer ')."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// HSTS: se activa condicionalmente más abajo (fuera de Development a propósito — en dev, el
// navegador cachearía la política HSTS contra localhost, complicando volver a probar por HTTP plano).
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

// Prerequisito real para que el rate limiting por IP (hallazgo #2) siga funcionando por-cliente-real
// el día que haya un reverse proxy delante — sin esto, RemoteIpAddress vería la IP del proxy para
// todos. KnownProxies/KnownNetworks se leen de "ReverseProxy" en configuración, vacíos por defecto:
// sin configurarlos, ASP.NET Core mantiene su default (solo confía en el header si la conexión
// INMEDIATA viene de loopback), así que hoy, sin proxy, el header simplemente se ignora — no hay
// forma de spoofear la IP aceptando X-Forwarded-For de cualquier origen sin restricción.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1; // Un solo salto: no confiar en una cadena de proxies arbitraria.

    foreach (var proxy in builder.Configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? Array.Empty<string>())
    {
        if (IPAddress.TryParse(proxy, out var address))
        {
            options.KnownProxies.Add(address);
        }
    }

    foreach (var network in builder.Configuration.GetSection("ReverseProxy:KnownNetworks").Get<string[]>() ?? Array.Empty<string>())
    {
        var parts = network.Split('/');
        if (parts.Length == 2 && IPAddress.TryParse(parts[0], out var prefix) && int.TryParse(parts[1], out var prefixLength))
        {
            options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, prefixLength));
        }
    }
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Se registra vía IDbContextFactory (en vez del AddDbContext tradicional) para que
// TonerExceptionLogger pueda crear un DbContext nuevo e independiente del scoped de la request/job
// actual — necesario para poder loguear un error incluso si ese DbContext scoped quedó en un estado
// inválido por la misma excepción que se está registrando. AddDbContext y AddDbContextFactory no
// pueden convivir para el mismo TContext (ambos compiten por DbContextOptions<TContext> con lifetimes
// distintos), así que el TonerDbContext scoped de siempre se deriva de la factory en vez de registrarse
// aparte.
// El contexto de tenencia se propaga a Postgres como variables de sesión para las políticas RLS
// (ver TenantContextInterceptor y la migración AddRowLevelSecurity — SECURITY_AUDIT.md hallazgo #5).
// Singleton porque se apoya en AsyncLocal: tiene que seguir al flujo asíncrono hasta el interceptor,
// que no tiene acceso al scope de la request.
builder.Services.AddSingleton<ITenantContextAccessor, TenantContextAccessor>();
builder.Services.AddSingleton<TenantContextInterceptor>();

// DefaultConnection usa el rol toner_app (sin privilegios de DDL y sujeto a RLS). Las migraciones
// usan MigrationsConnection (owner) vía TonerDbContextFactory, no esta configuración.
builder.Services.AddDbContextFactory<TonerDbContext>((sp, options) =>
    options
        .UseNpgsql(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            npgsql => npgsql
                .MigrationsAssembly("Toner.Infrastructure")
                // Tolera un blip de red o un reinicio breve de Postgres, no una caída prolongada
                // (hallazgo #13) — sin transacciones explícitas en todo el código (verificado), así
                // que no hay conflicto con el requisito de EF Core de envolverlas en un execution
                // strategy.
                .EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null))
        .AddInterceptors(sp.GetRequiredService<TenantContextInterceptor>()));

builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<TonerDbContext>>().CreateDbContext());
builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<TonerDbContext>());
builder.Services.AddScoped<IExceptionLogger, TonerExceptionLogger>();
builder.Services.AddScoped<DataSeeder>();

builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgres");

// Caché in-memory para catálogos y el resumen del dashboard (CODE_QUALITY_AUDIT.md hallazgo #10).
// Sin SizeLimit a propósito: lo que se cachea está acotado por construcción — una entrada para
// ciudades, una para marcas, una por marca para modelos, y una por (tenant, período) para el
// dashboard. No hay ninguna clave derivada de entrada libre del usuario que pueda hacerla crecer.
builder.Services.AddMemoryCache();

builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICityService, CityService>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IClientLocationService, ClientLocationService>();
builder.Services.AddScoped<IAssetBrandService, AssetBrandService>();
builder.Services.AddScoped<IAssetModelService, AssetModelService>();
builder.Services.AddScoped<IAssetService, AssetService>();
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<IContractAssetService, ContractAssetService>();
builder.Services.AddScoped<IMaintenanceScheduleEngine, MaintenanceScheduleEngine>();
builder.Services.AddScoped<IMaintenanceScheduleService, MaintenanceScheduleService>();
builder.Services.AddScoped<IMaintenanceOrderService, MaintenanceOrderService>();
builder.Services.AddScoped<MaintenanceScheduleEvaluationJob>();
builder.Services.AddScoped<IServiceTicketService, ServiceTicketService>();
builder.Services.AddScoped<ITechnicianService, TechnicianService>();
builder.Services.AddScoped<ITechnicianCheckInService, TechnicianCheckInService>();
builder.Services.AddScoped<IAssignmentEngine, AssignmentEngine>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

builder.Services.AddValidatorsFromAssembly(typeof(IAuthService).Assembly);

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options =>
        options.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"))));
builder.Services.AddHangfireServer();

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("Falta la sección de configuración 'Jwt'.");
JwtSettingsValidator.EnsureValid(jwtSettings);
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // Sin esto, un JWT ya emitido sigue siendo válido hasta su expiración natural aunque el
        // usuario cambie/reseteen su contraseña o se desactive su cuenta (ver SECURITY_AUDIT.md
        // hallazgo #6). Hace una consulta a BD por request autenticado a propósito: se evaluó
        // cachear el SecurityStamp en memoria, pero se descartó por ahora — es un SELECT indexado
        // por PK (marginal frente al resto del trabajo por request) y evita cualquier ventana de
        // staleness, incluida la que introduciría un caché por proceso si la API llegara a correr
        // en más de una instancia.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = SecurityStampValidator.ValidateAsync
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Por defecto todo endpoint requiere estar autenticado; se abre explícitamente con [AllowAnonymous].
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            new
            {
                title = "Demasiadas solicitudes. Intenta de nuevo más tarde.",
                status = StatusCodes.Status429TooManyRequests
            },
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);
    };

    // Partición por IP de conexión. UseForwardedHeaders ya está configurado y corre ANTES que este
    // middleware en el pipeline, así que RemoteIpAddress refleja la IP real del cliente incluso
    // detrás de un reverse proxy — siempre que ese proxy esté declarado en ReverseProxy:KnownProxies
    // (o :KnownNetworks). Si no se declara, ASP.NET Core ignora el X-Forwarded-For y esto degrada a
    // un límite compartido por todos los clientes detrás del proxy, en vez de uno por IP real.
    static string PartitionKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    // Política estricta para login y cambio de contraseña: 5 intentos por minuto por IP, sin cola
    // (el exceso se rechaza de inmediato con 429 en vez de esperar turno).
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        PartitionKey(context),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));

    // Límite global (aplica a toda la API, incluidos los endpoints anónimos) de 100 requests por
    // minuto por IP, como tope general contra floods que no sean específicamente de auth.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            PartitionKey(context),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

var app = builder.Build();

// AllowedHosts ya es configurable por entorno (variable de entorno "AllowedHosts", que sobreescribe
// el "*" de appsettings.json por la precedencia estándar de configuración) — no hay nada hardcodeado
// en código. Esta advertencia hace visible en el arranque si producción quedó con el default
// permisivo, en vez de que pase inadvertido (hallazgo #16).
if (app.Environment.IsProduction() && builder.Configuration["AllowedHosts"] == "*")
{
    app.Logger.LogWarning(
        "AllowedHosts está en '*' en producción. Fija el dominio real vía la variable de entorno " +
        "AllowedHosts para mitigar ataques de Host header.");
}

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
    await seeder.SeedAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    // El dashboard de Hangfire no tiene forma limpia de compartir el JWT Bearer de la SPA con una
    // navegación de navegador directa. Por ahora queda abierto solo en Development; antes de exponerlo
    // en producción hace falta un puente de auth propio (cookie de sesión de Administrador) o restringirlo
    // a nivel de red (ej. Access Restrictions de Azure App Service).
    app.UseHangfireDashboard("/hangfire");
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Antes que casi todo lo demás a propósito: los middlewares siguientes (rate limiting por IP,
// HttpsRedirection) necesitan ver la IP/esquema ya corregidos si hay un reverse proxy delante.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseCors("Frontend");

app.UseRateLimiter();

// Antes de UseAuthentication a propósito: la validación del token (SecurityStampValidator) consulta
// la tabla Users y abre una conexión, así que ya tiene que haber un TenantContext establecido o el
// interceptor lanzaría. El contexto se evalúa de forma perezosa contra HttpContext.User, así que esa
// consulta temprana ve Anonymous y el controller posterior ve el contexto autenticado real.
app.UseMiddleware<TenantContextMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// Después de UseAuthentication a propósito — ver OpenDbConnectionMiddleware para el razonamiento
// completo (CODE_QUALITY_AUDIT.md hallazgo #10).
app.UseMiddleware<OpenDbConnectionMiddleware>();

app.UseMiddleware<MustChangePasswordMiddleware>();

app.MapControllers();

// AllowAnonymous explícito: el FallbackPolicy de arriba exige autenticación por defecto en TODO
// endpoint, y un orquestador (Docker/K8s) que verifica liveness/readiness no puede autenticarse.
app.MapHealthChecks("/health").AllowAnonymous();

RecurringJob.AddOrUpdate<MaintenanceScheduleEvaluationJob>(
    "evaluate-maintenance-schedules",
    job => job.RunAsync(CancellationToken.None),
    Cron.Daily);

app.Run();
