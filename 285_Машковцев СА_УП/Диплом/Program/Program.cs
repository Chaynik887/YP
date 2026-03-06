using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Program.Data;
using Microsoft.Extensions.Logging;

namespace Program;

public class WebApp
{
    public static void Main(string[] args)
    {
        // включаем legacy timestamp behavior для Npgsql
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
        
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorPages();

        // подключение к бд
        var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

        // параметры для предотвращения разрыва соединения при простое
        if (!string.IsNullOrEmpty(connectionString) && !connectionString.Contains("Keepalive"))
        {
            connectionString += ";Keepalive=60;Command Timeout=60;Timeout=60;";
        }
        
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null);
            }));

        // настройка авторизации через Cookies
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Login";
                options.LogoutPath = "/Logout";
                options.AccessDeniedPath = "/Login";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
            });

        builder.Services.AddAuthorization();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        // обработчик исключений
        app.UseExceptionHandler("/Error");
        
        // обработка необработанных исключений
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            try
            {
                var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger<WebApp>();
                logger.LogError(e.ExceptionObject as Exception, "Необработанное исключение: {Message}", 
                    e.ExceptionObject is Exception ex ? ex.Message : "Unknown error");
            }
            catch
            {
                Console.WriteLine($"Необработанное исключение: {e.ExceptionObject}");
            }
        };
        
        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
        }

        // app.UseHttpsRedirection() - отключено, используем только HTTP порт 5000
        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapRazorPages();

        app.Run();
    }
}
