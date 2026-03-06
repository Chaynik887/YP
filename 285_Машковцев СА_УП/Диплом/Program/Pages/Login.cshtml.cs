using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Program.Data;
using System.Security.Claims;
using BCrypt.Net;

namespace Program.Pages;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<LoginModel> _logger;

    [BindProperty]
    public string Login { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public LoginModel(ApplicationDbContext context, ILogger<LoginModel> logger)
    {
        _context = context;
        _logger = logger;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Login) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Введите логин и пароль";
            return Page();
        }

        var user = await _context.Users
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Login == Login && u.IsActive);

        if (user == null)
        {
            ErrorMessage = "Неверный логин или пароль";
            return Page();
        }

        // Проверка пароля: если это BCrypt хэш - проверяем через BCrypt, иначе сравниваем напрямую
        bool isPasswordValid = false;
        if (user.PasswordHash.StartsWith("$2a$") || user.PasswordHash.StartsWith("$2b$") || 
            user.PasswordHash.StartsWith("$2y$") || user.PasswordHash.StartsWith("$2x$"))
        {
            // Пароль захеширован через BCrypt
            try
            {
                isPasswordValid = BCrypt.Net.BCrypt.Verify(Password, user.PasswordHash);
            }
            catch
            {
                isPasswordValid = false;
            }
        }
        else
        {
            // Пароль хранится в открытом виде (временное решение)
            isPasswordValid = user.PasswordHash == Password;
        }

        if (!isPasswordValid)
        {
            ErrorMessage = "Неверный логин или пароль";
            return Page();
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Role, user.Role?.RoleName ?? ""),
            new Claim("Login", user.Login)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        return RedirectToPage("/Main");
    }
}

