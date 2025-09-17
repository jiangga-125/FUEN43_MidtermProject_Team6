using Microsoft.AspNetCore.Mvc;

namespace BookLoop.Areas.Books.Controllers
{
    [Area("Books")]
    public class BooksController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
