using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Restaurant.Models;

var builder = WebApplication.CreateBuilder(args);

// Configure culture settings
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { "en-US", "tr-TR" };
    options.SetDefaultCulture("en-US")
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);
    
    // Use invariant culture for number parsing
    options.DefaultRequestCulture = new RequestCulture("en-US");
});

// Add services to the container.
builder.Services.AddControllersWithViews();

// Get and validate connection string
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
}

// Log connection string info (first 50 chars only for security)
var logger = LoggerFactory.Create(config => config.AddConsole()).CreateLogger<Program>();
logger.LogInformation("Using connection string: {ConnectionStringPrefix}...", 
    connectionString.Substring(0, Math.Min(50, connectionString.Length)));

// Entity Framework with SQL Server
builder.Services.AddDbContext<RestaurantContext>(options =>
{
    options.UseSqlServer(connectionString);
    
    // For better performance and debugging
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// Add memory cache for session (must be before AddSession)
builder.Services.AddDistributedMemoryCache();

// Add session support
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Add logging
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    if (builder.Environment.IsDevelopment())
    {
        config.AddDebug();
    }
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Use localization
app.UseRequestLocalization();

app.UseRouting();

// Enable session (must be after UseRouting and before UseAuthorization)
app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Test database connection and ensure database is created
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<RestaurantContext>();
        var scopeLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        
        try
        {
            scopeLogger.LogInformation("Testing database connection...");
            
            // Test connection
            await context.Database.OpenConnectionAsync();
            scopeLogger.LogInformation("Database connection successful!");
            await context.Database.CloseConnectionAsync();
            
            // Ensure database is created
            scopeLogger.LogInformation("Ensuring database is created...");
            var created = await context.Database.EnsureCreatedAsync();
            if (created)
            {
                scopeLogger.LogInformation("Database was created.");
            }
            else
            {
                scopeLogger.LogInformation("Database already exists.");
            }
        }
        catch (Exception ex)
        {
            scopeLogger.LogError(ex, "Database connection or creation failed: {ErrorMessage}", ex.Message);
            
            // Don't stop the app, but log the issue
            scopeLogger.LogWarning("Application will continue but database operations may fail.");
        }
    }
}

app.Run();