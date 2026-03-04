using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using YP.Data;
using YP.Models;
using Microsoft.Extensions.DependencyInjection;


var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString)
           .UseLowerCaseNamingConvention());

// Add services to the container.
builder.Services.AddControllersWithViews();


builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Requests/Index";
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();

    if (!db.Roles.Any())
    {
        db.Roles.AddRange(
            new Role { RoleName = "Менеджер" },
            new Role { RoleName = "Специалист" },
            new Role { RoleName = "Оператор" },
            new Role { RoleName = "Заказчик" }
        );
        db.SaveChanges();
    }

    if (!db.Statuses.Any())
    {
        db.Statuses.AddRange(
            new Status { StatusName = "В процессе ремонта" },
            new Status { StatusName = "Готова к выдаче" },
            new Status { StatusName = "Новая заявка" }
        );
        db.SaveChanges();
    }

    try
    {
        db.Database.ExecuteSqlRaw(
            "SELECT setval(pg_get_serial_sequence('requests','requestid'), COALESCE(MAX(requestid),0) + 1, false) FROM requests;");
    }
    catch
    {
        // если последовательность не найдена, просто пропускаем
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();


app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Requests}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
