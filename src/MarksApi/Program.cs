using System.Text;
using MarksApi.Authentication;
using MarksApi.Configuration;
using MarksApi.Data;
using MarksApi.Endpoints;
using MarksApi.Interfaces;
using MarksApi.Middleware;
using MarksApi.Services;
using MarksApi.SmokeTests;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Configuration Settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.Configure<MarkingSettings>(builder.Configuration.GetSection(MarkingSettings.SectionName));

var jwtKey = builder.Configuration["Jwt:Key"] ?? "DefaultFallbackSecretKeyForDevelopmentOnlyAtLeast32Chars!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "MarksApi";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "MarksApiUsers";

// 2. Database Context with PostgreSQL and snake_case naming convention
var connectionString = builder.Configuration.GetConnectionString("Database");
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString)
           .UseSnakeCaseNamingConvention();
});

// 3. Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

// 4. Authentication & Authorization
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false; // set to false for local development ease
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// 5. Dependency Injection Services
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<ICourseService, CourseService>();
builder.Services.AddScoped<IEnrollmentService, EnrollmentService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<SmokeTestRunner>();

// 6. CORS Policy
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
        else
        {
            policy.AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

// 7. Swagger / OpenAPI Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Marks API",
        Version = "v1",
        Description = "Production-style Student Marks Management API using ASP.NET Core and PostgreSQL"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Seed database on startup if database exists
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();
    try
    {
        if (await context.Database.CanConnectAsync())
        {
            await DbSeeder.SeedAsync(context, passwordService);
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not seed database on startup (may be waiting for migrations).");
    }
}

// Global Exception Handler
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Configure HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Marks API v1");
    c.RoutePrefix = string.Empty; // Serve Swagger at app root
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Map Endpoints
app.MapAuthEndpoints();
app.MapStudentEndpoints();
app.MapCourseEndpoints();
app.MapEnrollmentEndpoints();
app.MapReportEndpoints();
app.MapHealthEndpoints();

// Check if running in smoke-test mode
var isSmokeTest = args.Contains("--smoke-test") ||
                  string.Equals(Environment.GetEnvironmentVariable("SMOKE_TEST"), "true", StringComparison.OrdinalIgnoreCase);

if (isSmokeTest)
{
    await app.StartAsync();

    var server = app.Services.GetRequiredService<IServer>();
    var addressFeature = server.Features.Get<IServerAddressesFeature>();
    var address = addressFeature?.Addresses.FirstOrDefault() ?? "http://localhost:5000";

    using var scope = app.Services.CreateScope();
    var runner = scope.ServiceProvider.GetRequiredService<SmokeTestRunner>();
    var exitCode = await runner.RunAsync(address);

    await app.StopAsync();
    Environment.Exit(exitCode);
}
else
{
    await app.RunAsync();
}
