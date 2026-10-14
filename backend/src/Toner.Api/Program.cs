using Toner.Infrastructure.Realtime;
using Toner.Infrastructure.Push;
using Toner.Application.Push;
using Toner.Application.Realtime;
using Toner.Api.Hubs;
using Toner.Infrastructure.Storage;
using Toner.Application.Geo;
using Toner.Application.Evidences;
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
using Toner.Application.Calendar;
using Toner.Application.Cities;
using Toner.Application.Inventory;
using Toner.Application.Inventory.Analytics;
using Toner.Application.Zones;
using Toner.Application.Clients;
using Toner.Application.Common.Interfaces;
using Toner.Application.Contracts;
using Toner.Application.Dashboard;
using Toner.Application.Maintenance;
using Toner.Application.Technicians;
using Toner.Application.Tickets;
using Toner.Application.Users;
using Toner.Infrastructure.Auth;
using Toner.Infrastructure.Email;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Toner.Api.Health;
using Toner.Api.RateLimiting;
using Toner.Infrastructure.Health;
using Toner.Infrastructure.Jobs;
using Toner.Infrastructure.Logging;
using Toner.Infrastructure.Notifications;
using Toner.Infrastructure.Persistence;
using Toner.Infrastructure.Persistence.Seed;
using Toner.Infrastructure.Security;

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

// IP real del cliente detrás del proxy (App Service incluido): ver ForwardedHeadersSetup y README.
builder.Services.Configure<ForwardedHeadersOptions>(options => ForwardedHeadersSetup.Configure(options, builder.Configuration));

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
builder.Services.AddSignalR();
builder.Services.AddSingleton<RealtimeConnectionTracker>();
builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();
builder.Services.AddSingleton<RealtimeChangeInterceptor>();
// Aviso inmediato al despachador del outbox de correos de contraseña (AUDIT.md H-19).
builder.Services.AddSingleton<PasswordEmailOutboxKickInterceptor>();

// Notificaciones push (FCM). Sin Firebase:CredentialsPath quedan apagadas (el transporte lo avisa en el log al arrancar).
builder.Services.Configure<FirebaseSettings>(builder.Configuration.GetSection(FirebaseSettings.SectionName));
builder.Services.AddSingleton<IPushTransport, FirebasePushTransport>();
builder.Services.AddSingleton<IPushNotifier, PushNotifier>();
builder.Services.AddSingleton(sp => new PushChangeInterceptor(
    () => sp.GetRequiredService<IPushNotifier>(),
    sp.GetRequiredService<ILogger<PushChangeInterceptor>>()));

// DefaultConnection usa el rol toner_app (sin privilegios de DDL y sujeto a RLS). Las migraciones
// usan MigrationsConnection (owner) vía TonerDbContextFactory, no esta configuración.
builder.Services.AddDbContextFactory<TonerDbContext>((sp, options) =>
    options
        .UseNpgsql(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            npgsql => npgsql
                .MigrationsAssembly("Toner.Infrastructure")
                // Tolera un blip de red o un reinicio breve de Postgres, no una caída prolongada
                // (hallazgo #13). La única transacción explícita del código (PostgresStockLock, AUDIT.md
                // H-17) ya va envuelta en el execution strategy, como exige EF Core con este retry.
                .EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null))
        .AddInterceptors(sp.GetRequiredService<TenantContextInterceptor>(), sp.GetRequiredService<RealtimeChangeInterceptor>(),
            sp.GetRequiredService<PushChangeInterceptor>(), sp.GetRequiredService<PasswordEmailOutboxKickInterceptor>()));

builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<TonerDbContext>>().CreateDbContext());
builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<TonerDbContext>());
builder.Services.AddScoped<IExceptionLogger, TonerExceptionLogger>();
builder.Services.AddScoped<DataSeeder>();

// Singletons: CachedHealthCheck recuerda el último resultado unos segundos (AUDIT.md H-15).
builder.Services.AddSingleton<BlobStorageHealthCheck>();
builder.Services.AddSingleton<SmtpHealthCheck>();
builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgres", tags: new[] { HealthEndpoints.ReadyTag })
    .AddCheck<BlobStorageHealthCheck>("blob", tags: new[] { HealthEndpoints.ReadyTag })
    // Degraded y no Unhealthy: el correo de contraseña temporal se reintenta por Hangfire, así que un
    // SMTP caído no debe poner la app en 503.
    .AddCheck<SmtpHealthCheck>("smtp", failureStatus: HealthStatus.Degraded, tags: new[] { HealthEndpoints.ReadyTag });

// Caché in-memory para catálogos y el resumen del dashboard (CODE_QUALITY_AUDIT.md hallazgo #10).
// Sin SizeLimit a propósito: lo que se cachea está acotado por construcción — una entrada para
// ciudades, una para marcas, una por marca para modelos, y una por (tenant, período) para el
// dashboard. No hay ninguna clave derivada de entrada libre del usuario que pueda hacerla crecer.
builder.Services.AddMemoryCache();

builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.Configure<UserSessionOptions>(builder.Configuration.GetSection("Session"));
builder.Services.AddScoped<ISessionService, SessionService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICityService, CityService>();
builder.Services.AddScoped<IZoneService, ZoneService>();
builder.Services.AddScoped<IDeviceTokenService, DeviceTokenService>();
builder.Services.AddScoped<IStockLock, PostgresStockLock>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IBaseKitService, BaseKitService>();
builder.Services.AddScoped<IInventoryConsumptionService, InventoryConsumptionService>();
builder.Services.AddScoped<ITonerAnalyticsService, TonerAnalyticsService>();
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
builder.Services.AddScoped<ITechnicianHomeService, TechnicianHomeService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<WorkCalendarOptions>(builder.Configuration.GetSection("WorkCalendar"));
builder.Services.AddScoped<IWorkCalendarService, WorkCalendarService>();
builder.Services.AddScoped<ITechnicianScheduleService, TechnicianScheduleService>();
builder.Services.AddScoped<IHolidayService, HolidayService>();
builder.Services.Configure<GeoOptions>(builder.Configuration.GetSection("Geo"));
builder.Services.Configure<EvidenceOptions>(builder.Configuration.GetSection("Evidence"));
var evidenceStorageSettings = builder.Configuration.GetSection("EvidenceStorage").Get<EvidenceStorageSettings>() ?? new EvidenceStorageSettings();
EvidenceStorageSettingsValidator.EnsureValid(evidenceStorageSettings);
builder.Services.Configure<EvidenceStorageSettings>(builder.Configuration.GetSection("EvidenceStorage"));
builder.Services.AddSingleton<AzureBlobEvidenceStorage>();
builder.Services.AddSingleton<IEvidenceStorage>(sp => sp.GetRequiredService<AzureBlobEvidenceStorage>());
builder.Services.AddHostedService<EvidenceStorageInitializer>();
builder.Services.AddScoped<IEvidenceService, EvidenceService>();
builder.Services.AddScoped<IAssignmentEngine, AssignmentEngine>();
builder.Services.AddScoped<PendingAssignmentService>();
builder.Services.AddScoped<PendingAssignmentJob>();

// Plazo de retención de datos operativos (AUDIT.md H-27). Falla al arrancar si es menor al mínimo.
var dataRetentionOptions = builder.Configuration.GetSection("Retention").Get<DataRetentionOptions>() ?? new DataRetentionOptions();
dataRetentionOptions.EnsureValid();
builder.Services.Configure<DataRetentionOptions>(builder.Configuration.GetSection("Retention"));
builder.Services.AddScoped<DataRetentionJob>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

// Contraseña generada al crear/resetear un usuario (hallazgo #3 de SECURITY_AUDIT.md).
builder.Services.AddSingleton<IJobPayloadEncryptor, AesGcmJobPayloadEncryptor>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IBackgroundJobScheduler, OutboxPasswordEmailStager>();
builder.Services.AddScoped<PasswordEmailOutboxDispatcher>();
builder.Services.AddScoped<SendGeneratedPasswordEmailJob>();

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

var smtpSettings = builder.Configuration.GetSection("Smtp").Get<SmtpSettings>()
    ?? throw new InvalidOperationException("Falta la sección de configuración 'Smtp'.");
SmtpSettingsValidator.EnsureValid(smtpSettings, builder.Environment.IsDevelopment());
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));

var passwordDeliverySettings = builder.Configuration.GetSection("PasswordDelivery").Get<PasswordDeliverySettings>()
    ?? throw new InvalidOperationException("Falta la sección de configuración 'PasswordDelivery'.");
PasswordDeliverySettingsValidator.EnsureValid(passwordDeliverySettings);
builder.Services.Configure<PasswordDeliverySettings>(builder.Configuration.GetSection("PasswordDelivery"));

var jobPayloadEncryptionSettings = builder.Configuration.GetSection("JobPayloadEncryption").Get<JobPayloadEncryptionSettings>()
    ?? throw new InvalidOperationException("Falta la sección de configuración 'JobPayloadEncryption'.");
JobPayloadEncryptionSettingsValidator.EnsureValid(jobPayloadEncryptionSettings);
builder.Services.Configure<JobPayloadEncryptionSettings>(builder.Configuration.GetSection("JobPayloadEncryption"));

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
            // Los navegadores no pueden mandar cabeceras en un WebSocket: el token del canal en vivo viaja en la
            // query (?access_token=). Solo se acepta ahí para /hubs, nunca para la API REST.
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
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

// Por usuario (sub) con IP de respaldo, y login por IP + cédula (AUDIT.md H-13): ver RateLimitingSetup.
builder.Services.AddTonerRateLimiting(builder.Configuration);


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

// Antes de UseAuthentication a propósito: la validación del token (SecurityStampValidator) consulta
// la tabla Users y abre una conexión, así que ya tiene que haber un TenantContext establecido o el
// interceptor lanzaría. El contexto se evalúa de forma perezosa contra HttpContext.User, así que esa
// consulta temprana ve Anonymous y el controller posterior ve el contexto autenticado real.
app.UseMiddleware<TenantContextMiddleware>();

app.UseAuthentication();

// DESPUÉS de autenticar (el límite global se reparte por el sub de un token YA validado) y antes de autorizar
// (una request rechazada no llega a OpenDbConnectionMiddleware ni abre conexión). AUDIT.md H-13.
app.UseTonerRateLimiting();

app.UseAuthorization();

// Después de UseAuthentication a propósito — ver OpenDbConnectionMiddleware para el razonamiento
// completo (CODE_QUALITY_AUDIT.md hallazgo #10).
app.UseMiddleware<OpenDbConnectionMiddleware>();

app.UseMiddleware<MustChangePasswordMiddleware>();

app.MapControllers();
app.MapHub<UpdatesHub>("/hubs/updates");

app.MapTonerHealthChecks();

RecurringJob.AddOrUpdate<PendingAssignmentJob>(
    "retry-pending-assignments",
    job => job.RunAsync(CancellationToken.None),
    "*/10 * * * *");

// Outbox del correo de contraseña (AUDIT.md H-19): el recurrente es la garantía de entrega; en el caso normal
// PasswordEmailOutboxKickInterceptor lo dispara al momento.
RecurringJob.AddOrUpdate<PasswordEmailOutboxDispatcher>(
    "dispatch-password-emails",
    job => job.RunAsync(CancellationToken.None),
    "* * * * *");

// Purga diaria de sesiones terminadas, a las 03:00 hora de Bogotá (fuera del horario laboral).
RecurringJob.AddOrUpdate<DataRetentionJob>(
    "data-retention",
    job => job.RunAsync(CancellationToken.None),
    "0 3 * * *",
    new RecurringJobOptions
    {
        TimeZone = RecurringJobTimeZone.Resolve(builder.Configuration["WorkCalendar:TimeZoneId"])
    });

// Cron.Daily en la zona del calendario laboral (00:00 hora de Bogotá), no en UTC (AUDIT.md H-26).
RecurringJob.AddOrUpdate<MaintenanceScheduleEvaluationJob>(
    "evaluate-maintenance-schedules",
    job => job.RunAsync(CancellationToken.None),
    Cron.Daily,
    new RecurringJobOptions
    {
        TimeZone = RecurringJobTimeZone.Resolve(builder.Configuration["WorkCalendar:TimeZoneId"])
    });

app.Run();
