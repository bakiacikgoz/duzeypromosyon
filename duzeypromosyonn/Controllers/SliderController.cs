using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using duzeypromosyonn.Models;
using duzeypromosyonn.Services;

namespace duzeypromosyonn.Controllers
{
    [Authorize]
    public class SliderController : Controller
    {
        private const string SliderFile = "sliders.json";

        public ActionResult Index()
        {
            var sliders = Store().Load<Slider>(SliderFile).OrderBy(x => x.DisplayOrder).ToList();
            return View(sliders);
        }

        public ActionResult Create()
        {
            return View(new Slider());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(Slider slider, HttpPostedFileBase imageFile)
        {
            try
            {
                var sliders = Store().Load<Slider>(SliderFile).ToList();
                slider.SliderId = sliders.Any() ? sliders.Max(x => x.SliderId) + 1 : 1;
                slider.ImageUrl = Uploads().SaveImage(Server, imageFile, "~/Content/SliderImages");
                if (string.IsNullOrWhiteSpace(slider.ImageUrl))
                {
                    ModelState.AddModelError("", "Slider görseli zorunludur.");
                    return View(slider);
                }

                sliders.Add(slider);
                Store().Save(SliderFile, sliders);
                TempData["SuccessMessage"] = "Slider eklendi.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(slider);
            }
        }

        public ActionResult Edit(int id)
        {
            var slider = Store().Load<Slider>(SliderFile).FirstOrDefault(x => x.SliderId == id);
            return slider == null ? (ActionResult)HttpNotFound() : View(slider);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(Slider slider, HttpPostedFileBase imageFile)
        {
            try
            {
                var sliders = Store().Load<Slider>(SliderFile).ToList();
                var existing = sliders.FirstOrDefault(x => x.SliderId == slider.SliderId);
                if (existing == null)
                {
                    return HttpNotFound();
                }

                existing.Title = slider.Title;
                existing.Subtitle = slider.Subtitle;
                existing.DisplayOrder = slider.DisplayOrder;

                var newImage = Uploads().SaveImage(Server, imageFile, "~/Content/SliderImages");
                if (!string.IsNullOrWhiteSpace(newImage))
                {
                    Uploads().DeleteIfLocal(Server, existing.ImageUrl);
                    existing.ImageUrl = newImage;
                }

                Store().Save(SliderFile, sliders);
                TempData["SuccessMessage"] = "Slider güncellendi.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(slider);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var sliders = Store().Load<Slider>(SliderFile).ToList();
            var slider = sliders.FirstOrDefault(x => x.SliderId == id);
            if (slider != null)
            {
                Uploads().DeleteIfLocal(Server, slider.ImageUrl);
                sliders.Remove(slider);
                Store().Save(SliderFile, sliders);
                TempData["SuccessMessage"] = "Slider silindi.";
            }

            return RedirectToAction("Index");
        }

        private JsonDataStore Store()
        {
            return new JsonDataStore(Server.MapPath("~/App_Data"));
        }

        private FileUploadService Uploads()
        {
            return new FileUploadService();
        }
    }
}
