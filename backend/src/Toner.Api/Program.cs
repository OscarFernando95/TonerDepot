using System.Text;
using FluentValidation;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Toner.Api.Middleware;
using Toner.Api.Serialization;
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
builder.Services.AddDbContextFactory<TonerDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        npgsql => npgsql.MigrationsAssembly("Toner.Infrastructure")));

builder.Services.AddScoped(sp => sp.GetRequiredService<IDbContextFactory<TonerDbContext>>().CreateDbContext());
builder.Services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<TonerDbContext>());
builder.Services.AddScoped<IExceptionLogger, TonerExceptionLogger>();
builder.Services.AddScoped<DataSeeder>();

builder.Services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ICityService, CityService>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<IClientLocationService, ClientLocationService>();
builder.Services.AddScoped<IAssetBrandService, AssetBrandService>();
builder.Services.AddScoped<IAssetService, AssetService>();
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<IContractAssetService, ContractAssetService>();
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
    });

builder.Services.AddAuthorization(options =>
{
    // Por defecto todo endpoint requiere estar autenticado; se abre explícitamente con [AllowAnonymous].
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

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

app.UseHttpsRedirection();

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

RecurringJob.AddOrUpdate<MaintenanceScheduleEvaluationJob>(
    "evaluate-maintenance-schedules",
    job => job.RunAsync(CancellationToken.None),
    Cron.Daily);

app.Run();
