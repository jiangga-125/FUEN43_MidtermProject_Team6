using Microsoft.AspNetCore.Mvc;

namespace BookSystem.Controllers
{
	[Area("Books")] // 指定此 Controller 屬於 Books Area
	public class HomeController : Controller
	{
		// GET: /Books/Home/Index
		public IActionResult Index()
		{
			ViewData["Title"] = "Books 主頁";
			return View();
		}
	}
}
