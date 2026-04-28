using System.Web.Mvc;

namespace duzeypromosyonn.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return RedirectToAction("Index", "DPromosyon");
        }
    }
}
