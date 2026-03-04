using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using YP.Data;
using YP.Models;

namespace YP.Controllers
{
    [Authorize]
    public class RequestsController : Controller
    {
        private readonly ApplicationDbContext _context;

        // приведение дат к utc
        private static DateTime ToUtcDate(DateTime date) =>
            DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);

        public RequestsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // получение id текущего пользователя
        private int? CurrentUserId()
        {
            var value = User.FindFirstValue("UserId");
            return int.TryParse(value, out var id) ? id : null;
        }

        // проверки роли пользователя
        private bool IsOperatorOrManager() => User.IsInRole("Оператор") || User.IsInRole("Менеджер");
        private bool IsSpecialist() => User.IsInRole("Специалист");
        private bool IsClient() => User.IsInRole("Заказчик");

        // список заявок с фильтрацией
        public async Task<IActionResult> Index(int? requestId, string? status, string? text)
        {
            var userId = CurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }

            var query = _context.Requests
                .Include(r => r.Status)
                .Include(r => r.Client)
                .Include(r => r.Master)
                .AsQueryable();

            if (IsClient())
            {
                query = query.Where(r => r.ClientID == userId.Value);
            }
            else if (IsSpecialist())
            {
                query = query.Where(r => r.MasterID == userId.Value);
            }

            if (requestId != null)
            {
                query = query.Where(r => r.RequestID == requestId.Value);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(r => r.Status != null && r.Status.StatusName == status);
            }

            if (!string.IsNullOrWhiteSpace(text))
            {
                text = text.Trim();
                query = query.Where(r =>
                    r.ClimateTechType.Contains(text) ||
                    r.ClimateTechModel.Contains(text) ||
                    r.ProblemDescription.Contains(text) ||
                    (r.Client != null && r.Client.Fio.Contains(text)));
            }

            ViewBag.RequestId = requestId;
            ViewBag.Status = status;
            ViewBag.Text = text;
            ViewBag.Statuses = await _context.Statuses
                .OrderBy(s => s.StatusName)
                .Select(s => s.StatusName)
                .ToListAsync();

            var items = await query
                .OrderByDescending(r => r.StartDate)
                .ThenByDescending(r => r.RequestID)
                .ToListAsync();

            return View(items);
        }

        // карточка заявки и её комментарии
        public async Task<IActionResult> Details(int id)
        {
            var userId = CurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }

            var request = await _context.Requests
                .Include(r => r.Status)
                .Include(r => r.Client)
                .Include(r => r.Master)
                .FirstOrDefaultAsync(r => r.RequestID == id);

            if (request == null)
            {
                return NotFound();
            }

            if (IsClient() && request.ClientID != userId.Value)
            {
                return Forbid();
            }

            if (IsSpecialist() && request.MasterID != userId.Value)
            {
                return Forbid();
            }

            ViewBag.Comments = await _context.Comments
                .Include(c => c.Master)
                .Where(c => c.RequestID == id)
                .OrderByDescending(c => c.CommentID)
                .ToListAsync();

            ViewBag.CanComment = IsSpecialist() && request.MasterID == userId.Value;

            return View(request);
        }

        // форма создания новой заявки
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            if (!(IsClient() || IsOperatorOrManager()))
            {
                return Forbid();
            }

            var statuses = await _context.Statuses.OrderBy(s => s.StatusName).ToListAsync();
            ViewBag.StatusID = new SelectList(statuses, nameof(Status.StatusID), nameof(Status.StatusName));

            if (IsOperatorOrManager())
            {
                var clients = await _context.Users
                    .Include(u => u.Role)
                    .Where(u => u.Role != null && u.Role.RoleName == "Заказчик")
                    .OrderBy(u => u.Fio)
                    .ToListAsync();
                ViewBag.ClientID = new SelectList(clients, nameof(YP.Models.User.UserID), nameof(YP.Models.User.Fio));
            }

            var model = new Request
            {
                StartDate = DateTime.Today,
                ClimateTechType = string.Empty,
                ClimateTechModel = string.Empty,
                ProblemDescription = string.Empty
            };
            var defaultStatus = statuses.FirstOrDefault(s => s.StatusName == "Новая заявка") ?? statuses.FirstOrDefault();
            if (defaultStatus != null)
            {
                model.StatusID = defaultStatus.StatusID;
            }

            return View(model);
        }

        // сохранение новой заявки
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("StartDate,ClimateTechType,ClimateTechModel,ProblemDescription,StatusID,ClientID")] Request model)
        {
            var userId = CurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }

            if (!(IsClient() || IsOperatorOrManager()))
            {
                return Forbid();
            }

            if (IsClient())
            {
                model.ClientID = userId.Value;
                if (model.StatusID == 0)
                {
                    var status = await _context.Statuses.FirstOrDefaultAsync(s => s.StatusName == "Новая заявка");
                    if (status != null)
                    {
                        model.StatusID = status.StatusID;
                    }
                }
            }

            model.MasterID = null;
            model.CompletionDate = null;
            model.RepairParts = null;

            if (IsOperatorOrManager() && model.ClientID <= 0)
            {
                ModelState.AddModelError("ClientID", "Выберите клиента.");
            }

            if (model.StatusID == 0)
            {
                var firstStatus = await _context.Statuses.OrderBy(s => s.StatusID).FirstOrDefaultAsync();
                if (firstStatus != null)
                {
                    model.StatusID = firstStatus.StatusID;
                }
            }

            model.StartDate = ToUtcDate(model.StartDate);

            if (!ModelState.IsValid)
            {
                await FillCreateEditLists(model);
                return View(model);
            }

            try
            {
                _context.Requests.Add(model);
                await _context.SaveChangesAsync();
                TempData["Info"] = "Заявка создана.";
                return RedirectToAction(nameof(Details), new { id = model.RequestID });
            }
            catch (DbUpdateException ex)
            {
                var reason = ex.InnerException?.Message ?? ex.Message;
                ModelState.AddModelError(string.Empty, "Не удалось сохранить заявку: " + reason);
                await FillCreateEditLists(model);
                return View(model);
            }
        }

        // форма редактирования заявки
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = CurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }

            var request = await _context.Requests
                .Include(r => r.Status)
                .FirstOrDefaultAsync(r => r.RequestID == id);

            if (request == null)
            {
                return NotFound();
            }

            if (IsClient() && request.ClientID != userId.Value)
            {
                return Forbid();
            }

            if (IsSpecialist() && request.MasterID != userId.Value)
            {
                return Forbid();
            }

            await FillCreateEditLists(request);
            return View(request);
        }

        // обновление существующей заявки
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("RequestID,StartDate,ClimateTechType,ClimateTechModel,ProblemDescription,StatusID,CompletionDate,RepairParts,MasterID,ClientID")] Request form)
        {
            var userId = CurrentUserId();
            if (userId == null)
            {
                return Challenge();
            }

            var request = await _context.Requests.FirstOrDefaultAsync(r => r.RequestID == id);
            if (request == null)
            {
                return NotFound();
            }

            if (IsClient() && request.ClientID != userId.Value)
            {
                return Forbid();
            }

            if (IsSpecialist() && request.MasterID != userId.Value)
            {
                return Forbid();
            }

            if (IsClient())
            {
                request.StartDate = ToUtcDate(form.StartDate);
                request.ProblemDescription = form.ProblemDescription;
            }
            else if (IsSpecialist())
            {
                var oldStatusId = request.StatusID;
                request.StatusID = form.StatusID;
                request.RepairParts = form.RepairParts;
                request.CompletionDate = form.CompletionDate.HasValue
                    ? ToUtcDate(form.CompletionDate.Value)
                    : null;
                if (oldStatusId != request.StatusID)
                {
                    TempData["Info"] = "Статус заявки изменён.";
                }
            }
            else if (IsOperatorOrManager())
            {
                var oldStatusId = request.StatusID;
                request.StartDate = ToUtcDate(form.StartDate);
                request.ClimateTechType = form.ClimateTechType;
                request.ClimateTechModel = form.ClimateTechModel;
                request.ProblemDescription = form.ProblemDescription;
                request.StatusID = form.StatusID;
                request.MasterID = form.MasterID;
                request.CompletionDate = form.CompletionDate.HasValue
                    ? ToUtcDate(form.CompletionDate.Value)
                    : null;
                request.RepairParts = form.RepairParts;
                if (oldStatusId != request.StatusID)
                {
                    TempData["Info"] = "Статус заявки изменён.";
                }
            }
            else
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                await FillCreateEditLists(form);
                return View(form);
            }

            try
            {
                await _context.SaveChangesAsync();
                TempData["Info"] ??= "Изменения сохранены.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch
            {
                ModelState.AddModelError(string.Empty, "Не удалось сохранить изменения. Попробуйте ещё раз.");
                await FillCreateEditLists(form);
                return View(form);
            }
        }

        // подтверждение удаления заявки
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            if (!IsOperatorOrManager())
            {
                return Forbid();
            }

            var request = await _context.Requests
                .Include(r => r.Status)
                .Include(r => r.Client)
                .Include(r => r.Master)
                .FirstOrDefaultAsync(r => r.RequestID == id);

            if (request == null)
            {
                return NotFound();
            }

            return View(request);
        }

        // удаление заявки из базы
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (!IsOperatorOrManager())
            {
                return Forbid();
            }

            var request = await _context.Requests.FindAsync(id);
            if (request == null)
            {
                return NotFound();
            }

            try
            {
                _context.Requests.Remove(request);
                await _context.SaveChangesAsync();
                TempData["Info"] = "Заявка удалена.";
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                TempData["Info"] = "Не удалось удалить заявку (возможны связанные данные).";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        // заполнение списков статусов, клиентов и мастеров
        private async Task FillCreateEditLists(Request request)
        {
            var statuses = await _context.Statuses.OrderBy(s => s.StatusName).ToListAsync();
            ViewBag.StatusID = new SelectList(statuses, nameof(Status.StatusID), nameof(Status.StatusName), request.StatusID);

            if (IsOperatorOrManager())
            {
                var clients = await _context.Users
                    .Include(u => u.Role)
                    .Where(u => u.Role != null && u.Role.RoleName == "Заказчик")
                    .OrderBy(u => u.Fio)
                    .ToListAsync();
                ViewBag.ClientID = new SelectList(clients, nameof(YP.Models.User.UserID), nameof(YP.Models.User.Fio), request.ClientID);

                var masters = await _context.Users
                    .Include(u => u.Role)
                    .Where(u => u.Role != null && u.Role.RoleName == "Специалист")
                    .OrderBy(u => u.Fio)
                    .ToListAsync();
                ViewBag.MasterID = new SelectList(masters, nameof(YP.Models.User.UserID), nameof(YP.Models.User.Fio), request.MasterID);
            }
        }
    }
}

