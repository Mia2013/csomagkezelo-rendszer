using Microsoft.AspNetCore.Mvc;

namespace Csomagkuldo.Controllers
{
    public class AdminController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
