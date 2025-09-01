using Microsoft.AspNetCore.Mvc;

namespace ReportMail.Controllers
{
    public class ReportsController : Controller
    {
        [HttpGet]
        public IActionResult Index() => View();

        // 先用假資料；之後你再改成查資料庫
        [HttpGet]
        public IActionResult SalesByDay()
        {
            var labels = new[] { "08/24","08/25","08/26","08/27","08/28","08/29","08/30" };
            var data   = new[] { 1200,   1800,   900,   1500,   2100,   1700,   2300 };
            return Json(new { labels, data });
        }
    }
}
