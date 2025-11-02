using BookLoop.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookLoop.Areas.Mail.Controllers
{
    [Area("Mail")]
    public class MailLogsController : Controller
    {
        private readonly AppDbContext _db;
        public MailLogsController(AppDbContext db) => _db = db;

        public async Task<IActionResult> Index(DateTime? from, DateTime? to, string? status, string? templateKey, long? mailJobId, long? jobRecipientId, int page = 1)
        {
            var q = _db.MailSendLogs.AsQueryable();
            if (from.HasValue) q = q.Where(x => x.SentAt >= from.Value.Date);
            if (to.HasValue) q = q.Where(x => x.SentAt < to.Value.Date.AddDays(1));
            if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.Status == status);
            if (!string.IsNullOrWhiteSpace(templateKey)) q = q.Where(x => x.TemplateKey == templateKey);
            if (mailJobId.HasValue) q = q.Where(x => x.MailJobId == mailJobId);
            if (jobRecipientId.HasValue) q = q.Where(x => x.JobRecipientId == jobRecipientId);

            q = q.OrderByDescending(x => x.LogId);
            var list = await q.OrderByDescending(x => x.LogId).Take(200).ToListAsync();
            return View(list);
        }

        public async Task<IActionResult> Details(long id)
        {
            var item = await _db.MailSendLogs.FindAsync(id);
            if (item == null) return NotFound();
            return View(item);
        }
    }

}
