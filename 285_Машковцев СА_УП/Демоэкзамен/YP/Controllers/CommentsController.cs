using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YP.Data;
using YP.Models;

namespace YP.Controllers
{
    [Authorize]
    public class CommentsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CommentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // просмотр комментариев
        public async Task<IActionResult> Index()
        {
            var query = _context.Comments
                .Include(c => c.Master)
                .Include(c => c.Request)
                .AsQueryable();

            if (User.IsInRole("Специалист"))
            {
                var userId = int.TryParse(User.FindFirstValue("UserId"), out var id) ? id : 0;
                query = query.Where(c => c.MasterID == userId);
            }
            else if (User.IsInRole("Заказчик"))
            {
                return Forbid();
            }

            return View(await query.OrderByDescending(c => c.CommentID).ToListAsync());
        }

        // добавление комментария к заявке
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToRequest(int requestId, string message)
        {
            if (!User.IsInRole("Специалист"))
            {
                return Forbid();
            }

            var userId = int.TryParse(User.FindFirstValue("UserId"), out var id) ? id : 0;
            if (userId == 0)
            {
                return Challenge();
            }

            message = (message ?? string.Empty).Trim();
            if (message.Length == 0)
            {
                TempData["Info"] = "Введите текст комментария.";
                return RedirectToAction("Details", "Requests", new { id = requestId });
            }

            var request = await _context.Requests.FirstOrDefaultAsync(r => r.RequestID == requestId);
            if (request == null)
            {
                return NotFound();
            }

            if (request.MasterID != userId)
            {
                return Forbid();
            }

            _context.Comments.Add(new Comment
            {
                Message = message,
                MasterID = userId,
                RequestID = requestId
            });

            await _context.SaveChangesAsync();
            TempData["Info"] = "Комментарий добавлен.";
            return RedirectToAction("Details", "Requests", new { id = requestId });
        }

        // редактирование комментария
        [Authorize(Roles = "Оператор,Менеджер")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var comment = await _context.Comments.FindAsync(id);
            if (comment == null)
            {
                return NotFound();
            }
            return View(comment);
        }

        [Authorize(Roles = "Оператор,Менеджер")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("CommentID,Message,MasterID,RequestID")] Comment comment)
        {
            if (id != comment.CommentID)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(comment);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CommentExists(comment.CommentID))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(comment);
        }

        // удаление комментария
        [Authorize(Roles = "Оператор,Менеджер")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var comment = await _context.Comments
                .FirstOrDefaultAsync(m => m.CommentID == id);
            if (comment == null)
            {
                return NotFound();
            }

            return View(comment);
        }

        [Authorize(Roles = "Оператор,Менеджер")]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var comment = await _context.Comments.FindAsync(id);
            if (comment != null)
            {
                _context.Comments.Remove(comment);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool CommentExists(int id)
        {
            return _context.Comments.Any(e => e.CommentID == id);
        }
    }
}
