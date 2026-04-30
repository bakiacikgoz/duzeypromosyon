using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using duzeypromosyonn.Models;

namespace duzeypromosyonn.Services
{
    public class ProductImageCacheService
    {
        private const int CardMaxSize = 600;
        private const int DetailMaxSize = 1200;
        private const int MaxSourceImageBytes = 8 * 1024 * 1024;
        private const int RequestTimeoutMilliseconds = 8000;
        private const long JpegQuality = 82L;

        private static readonly ConcurrentDictionary<string, object> FileLocks = new ConcurrentDictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private readonly string _cacheFolder;
        private readonly string _placeholderPath;

        public ProductImageCacheService(string cacheFolder, string placeholderPath)
        {
            _cacheFolder = cacheFolder;
            _placeholderPath = placeholderPath;
        }

        public ProductImageFile GetImage(Product product, string requestedSize)
        {
            var size = NormalizeSize(requestedSize);
            if (product == null || string.IsNullOrWhiteSpace(product.ImageUrl))
            {
                return Placeholder();
            }

            Uri sourceUri;
            if (!Uri.TryCreate(product.ImageUrl, UriKind.Absolute, out sourceUri) || !IsAllowedImageHost(sourceUri))
            {
                return Placeholder();
            }

            Directory.CreateDirectory(_cacheFolder);
            var cachedPath = Path.Combine(_cacheFolder, CachedFileName(product, size));
            var fileLock = FileLocks.GetOrAdd(cachedPath, _ => new object());

            lock (fileLock)
            {
                if (!File.Exists(cachedPath))
                {
                    if (!TryDownloadAndResize(sourceUri, cachedPath, MaxDimension(size)))
                    {
                        return Placeholder();
                    }
                }
            }

            return new ProductImageFile(cachedPath, "image/jpeg", false);
        }

        public void WarmCardImages(System.Collections.Generic.IEnumerable<Product> products)
        {
            if (products == null)
            {
                return;
            }

            foreach (var product in products)
            {
                try
                {
                    GetImage(product, "card");
                }
                catch
                {
                    // Warm-up must never affect XML refresh or page rendering.
                }
            }
        }

        private ProductImageFile Placeholder()
        {
            return new ProductImageFile(_placeholderPath, "image/jpeg", true);
        }

        private static string NormalizeSize(string requestedSize)
        {
            return string.Equals(requestedSize, "detail", StringComparison.OrdinalIgnoreCase)
                ? "detail"
                : "card";
        }

        private static int MaxDimension(string size)
        {
            return string.Equals(size, "detail", StringComparison.OrdinalIgnoreCase)
                ? DetailMaxSize
                : CardMaxSize;
        }

        private static string CachedFileName(Product product, string size)
        {
            var version = string.IsNullOrWhiteSpace(product.ImageVersion)
                ? ShortHash(product.ImageUrl)
                : product.ImageVersion;

            return product.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "-"
                + size
                + "-"
                + version
                + ".jpg";
        }

        private static bool TryDownloadAndResize(Uri sourceUri, string targetPath, int maxDimension)
        {
            var tempPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                var request = (HttpWebRequest)WebRequest.Create(sourceUri);
                request.Timeout = RequestTimeoutMilliseconds;
                request.ReadWriteTimeout = RequestTimeoutMilliseconds;
                request.UserAgent = "DuzeyPromosyon/1.0";

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    if ((int)response.StatusCode < 200 || (int)response.StatusCode >= 300)
                    {
                        return false;
                    }

                    var contentType = response.ContentType ?? string.Empty;
                    if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    using (var input = response.GetResponseStream())
                    using (var memory = new MemoryStream())
                    {
                        CopyWithLimit(input, memory, MaxSourceImageBytes);
                        memory.Position = 0;

                        using (var sourceImage = Image.FromStream(memory))
                        using (var resized = ResizeImage(sourceImage, maxDimension))
                        {
                            SaveJpeg(resized, tempPath);
                        }
                    }
                }

                if (File.Exists(targetPath))
                {
                    File.Delete(targetPath);
                }

                File.Move(tempPath, targetPath);
                return true;
            }
            catch
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                return false;
            }
        }

        private static void CopyWithLimit(Stream input, Stream output, int maxBytes)
        {
            if (input == null)
            {
                throw new InvalidOperationException("Image response stream is empty.");
            }

            var buffer = new byte[81920];
            var total = 0;
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                total += read;
                if (total > maxBytes)
                {
                    throw new InvalidOperationException("Image is larger than the allowed cache size.");
                }

                output.Write(buffer, 0, read);
            }
        }

        private static Image ResizeImage(Image sourceImage, int maxDimension)
        {
            var ratio = Math.Min(maxDimension / (double)sourceImage.Width, maxDimension / (double)sourceImage.Height);
            if (ratio > 1)
            {
                ratio = 1;
            }

            var width = Math.Max(1, (int)Math.Round(sourceImage.Width * ratio));
            var height = Math.Max(1, (int)Math.Round(sourceImage.Height * ratio));
            var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            bitmap.SetResolution(sourceImage.HorizontalResolution, sourceImage.VerticalResolution);

            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.White);
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(sourceImage, 0, 0, width, height);
            }

            return bitmap;
        }

        private static void SaveJpeg(Image image, string path)
        {
            var codec = ImageCodecInfo.GetImageDecoders().FirstOrDefault(x => x.FormatID == ImageFormat.Jpeg.Guid);
            if (codec == null)
            {
                image.Save(path, ImageFormat.Jpeg);
                return;
            }

            using (var parameters = new EncoderParameters(1))
            {
                parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, JpegQuality);
                image.Save(path, codec, parameters);
            }
        }

        private static bool IsAllowedImageHost(Uri uri)
        {
            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var host = uri.Host.ToLowerInvariant();
            return host == "b2b.ilpen.com.tr" || host.EndsWith(".ilpen.com.tr", StringComparison.Ordinal);
        }

        private static string ShortHash(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "img-v2-no-image";
            }

            using (var sha1 = SHA1.Create())
            {
                var bytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(value));
                return "img-v2-" + BitConverter.ToString(bytes).Replace("-", string.Empty).Substring(0, 12).ToLowerInvariant();
            }
        }
    }

    public class ProductImageFile
    {
        public ProductImageFile(string path, string contentType, bool isPlaceholder)
        {
            Path = path;
            ContentType = contentType;
            IsPlaceholder = isPlaceholder;
        }

        public string Path { get; private set; }
        public string ContentType { get; private set; }
        public bool IsPlaceholder { get; private set; }
    }
}
