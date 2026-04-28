using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;

namespace duzeypromosyonn.Services
{
    public class FileUploadService
    {
        private static readonly HashSet<string> AllowedImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg",
            ".jpeg",
            ".png",
            ".webp",
            ".gif"
        };

        private const int MaxImageBytes = 5 * 1024 * 1024;

        public string SaveImage(HttpServerUtilityBase server, HttpPostedFileBase file, string virtualFolder)
        {
            if (file == null || file.ContentLength == 0)
            {
                return null;
            }

            if (file.ContentLength > MaxImageBytes)
            {
                throw new InvalidOperationException("Görsel en fazla 5 MB olabilir.");
            }

            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension) || !AllowedImageExtensions.Contains(extension))
            {
                throw new InvalidOperationException("Sadece JPG, PNG, WEBP veya GIF görseller yüklenebilir.");
            }

            var cleanFolder = virtualFolder.TrimEnd('/');
            var physicalFolder = server.MapPath(cleanFolder);
            if (!Directory.Exists(physicalFolder))
            {
                Directory.CreateDirectory(physicalFolder);
            }

            var fileName = Guid.NewGuid().ToString("N") + extension.ToLowerInvariant();
            var physicalPath = Path.Combine(physicalFolder, fileName);
            file.SaveAs(physicalPath);

            return cleanFolder + "/" + fileName;
        }

        public void DeleteIfLocal(HttpServerUtilityBase server, string virtualPath)
        {
            if (string.IsNullOrWhiteSpace(virtualPath) || virtualPath.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var physicalPath = server.MapPath(virtualPath);
            if (File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
            }
        }
    }
}
