using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using duzeypromosyonn.Models;
using duzeypromosyonn.Services;

namespace duzeypromosyonn.Controllers
{
    [Authorize]
    public class AdminCantaController : Controller
    {
        private const string CantaFile = "cantas.json";

        public ActionResult Index()
        {
            return View(Store().Load<Canta>(CantaFile).OrderBy(x => x.Baslik).ToList());
        }

        public ActionResult Create()
        {
            return View(new Canta());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult Create(Canta canta, IEnumerable<HttpPostedFileBase> files)
        {
            if (!ModelState.IsValid)
            {
                return View(canta);
            }

            try
            {
                var cantalar = Store().Load<Canta>(CantaFile).ToList();
                canta.Id = cantalar.Any() ? cantalar.Max(x => x.Id) + 1 : 1;
                canta.ImageUrls = SaveImages(files);
                cantalar.Add(canta);
                Store().Save(CantaFile, cantalar);
                TempData["SuccessMessage"] = "VIP Çanta eklendi.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(canta);
            }
        }

        public ActionResult Edit(int id)
        {
            var canta = Store().Load<Canta>(CantaFile).FirstOrDefault(x => x.Id == id);
            return canta == null ? (ActionResult)HttpNotFound() : View(canta);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ValidateInput(false)]
        public ActionResult Edit(Canta canta, IEnumerable<HttpPostedFileBase> files)
        {
            if (!ModelState.IsValid)
            {
                return View(canta);
            }

            try
            {
                var cantalar = Store().Load<Canta>(CantaFile).ToList();
                var existing = cantalar.FirstOrDefault(x => x.Id == canta.Id);
                if (existing == null)
                {
                    return HttpNotFound();
                }

                existing.Baslik = canta.Baslik;
                existing.UrunKodu = canta.UrunKodu;
                existing.Fiyat = canta.Fiyat;
                existing.Aciklama = canta.Aciklama;
                foreach (var image in SaveImages(files))
                {
                    existing.ImageUrls.Add(image);
                }

                Store().Save(CantaFile, cantalar);
                TempData["SuccessMessage"] = "VIP Çanta güncellendi.";
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", ex.Message);
                return View(canta);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RemoveImage(int id, string imageUrl)
        {
            var cantalar = Store().Load<Canta>(CantaFile).ToList();
            var canta = cantalar.FirstOrDefault(x => x.Id == id);
            if (canta != null && canta.ImageUrls.Remove(imageUrl))
            {
                Uploads().DeleteIfLocal(Server, imageUrl);
                Store().Save(CantaFile, cantalar);
            }

            return RedirectToAction("Edit", new { id = id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var cantalar = Store().Load<Canta>(CantaFile).ToList();
            var canta = cantalar.FirstOrDefault(x => x.Id == id);
            if (canta != null)
            {
                foreach (var imageUrl in canta.ImageUrls.ToList())
                {
                    Uploads().DeleteIfLocal(Server, imageUrl);
                }

                cantalar.Remove(canta);
                Store().Save(CantaFile, cantalar);
                TempData["SuccessMessage"] = "VIP Çanta silindi.";
            }

            return RedirectToAction("Index");
        }

        private List<string> SaveImages(IEnumerable<HttpPostedFileBase> files)
        {
            var imageUrls = new List<string>();
            if (files == null)
            {
                return imageUrls;
            }

            foreach (var file in files.Where(x => x != null && x.ContentLength > 0))
            {
                imageUrls.Add(Uploads().SaveImage(Server, file, "~/Content/Uploads"));
            }

            return imageUrls;
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
