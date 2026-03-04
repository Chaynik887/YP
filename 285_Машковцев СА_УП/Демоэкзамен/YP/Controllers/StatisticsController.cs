using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YP.Data;

namespace YP.Controllers
{
    [Authorize(Roles = "Оператор,Менеджер")]
    public class StatisticsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StatisticsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // расчёт статистики по заявкам
        public async Task<IActionResult> Index()
        {
            var requests = await _context.Requests
                .Include(r => r.Status)
                .ToListAsync();

            var completed = requests
                .Where(r => r.CompletionDate != null)
                .ToList();

            var avgDays = completed.Count == 0
                ? (double?)null
                : completed.Average(r => (r.CompletionDate!.Value.Date - r.StartDate.Date).TotalDays);

            ViewBag.CompletedCount = completed.Count;
            ViewBag.AverageDays = avgDays;

            ViewBag.ByTechType = requests
                .GroupBy(r => r.ClimateTechType)
                .OrderByDescending(g => g.Count())
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToList();

            ViewBag.ByStatus = requests
                .GroupBy(r => r.Status != null ? r.Status.StatusName : "(нет)")
                .OrderByDescending(g => g.Count())
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToList();

            ViewBag.ByProblem = requests
                .Where(r => !string.IsNullOrWhiteSpace(r.ProblemDescription))
                .GroupBy(r => r.ProblemDescription)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => new { Name = g.Key, Count = g.Count() })
                .ToList();

            return View();
        }
    }
}

