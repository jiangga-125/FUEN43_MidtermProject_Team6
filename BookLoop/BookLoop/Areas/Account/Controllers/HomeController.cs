using System.Diagnostics;
using BookLoop.Data;
using BookLoop;
using BookLoop.Models;
using Microsoft.AspNetCore.Mvc;

namespace Account.Controllers
{
    [Area("Account")]
	public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }


		[HttpGet]
		public IActionResult Claims()
		=> Content(string.Join("\n", User.Claims.Select(c => $"{c.Type} = {c.Value}")),
				   "text/plain; charset=utf-8");
	}
}
