using FeedHorn.Data;
using FeedHorn.Middleware;
using FeedHorn.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Add DbContext with SQLite
builder.Services.AddDbContext<FeedHornContext>(options =>
    options.UseSqlite("Data Source=feedhorn.db"));

// Add authentication and encryption services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<EncryptionService>();
builder.Services.AddScoped<PaloAltoService>();
builder.Services.AddDataProtection();

// Add background monitoring services
builder.Services.AddHostedService<UrlMonitoringService>();
builder.Services.AddHostedService<SpeedTestService>();
builder.Services.AddHostedService<SslMonitoringService>();
builder.Services.AddHttpClient();

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Ensure database is created and default user exists
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<FeedHornContext>();
    db.Database.EnsureCreated();

    var authService = scope.ServiceProvider.GetRequiredService<AuthService>();
    await authService.EnsureDefaultUserExists();
}

// Inject footer into HTML responses (compiled into DLL)
// MUST be before UseStaticFiles to intercept the response
app.UseMiddleware<FooterInjectionMiddleware>();

// Authentication middleware - protect all routes except login
app.UseMiddleware<AuthenticationMiddleware>();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run();
