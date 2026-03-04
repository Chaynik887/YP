using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using YP.Data;
using YP.Models;

namespace YP.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index() => RedirectToAction("Index", "Requests");

        [Authorize(Roles = "????????,????????")]
        public async Task<IActionResult> Users()
        {
            var users = await _context.Users
                .Include(u => u.Role)
                .ToListAsync();
            return View(users);
        }

        [Authorize(Roles = "????????,????????")]
        public async Task<IActionResult> Statuses()
        {
            var statuses = await _context.Statuses.ToListAsync();
            return View(statuses);
        }

        [Authorize(Roles = "????????,????????")]
        public async Task<IActionResult> Roles()
        {
            var roles = await _context.Roles.ToListAsync();
            return View(roles);
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}