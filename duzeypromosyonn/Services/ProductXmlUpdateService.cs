using System;
using System.IO;
using System.Net;
using duzeypromosyonn.Models;

namespace duzeypromosyonn.Services
{
    public class ProductXmlUpdateService
    {
        public const string DefaultSourceUrl = "https://webservice.ilpen.com.tr/xml/xml_list_all_products";
        private readonly string _xmlPath;
        private readonly string _statusPath;
        private readonly string _sourceUrl;

        public ProductXmlUpdateService(string xmlPath, string statusPath, string sourceUrl)
        {
            _xmlPath = xmlPath;
            _statusPath = statusPath;
            _sourceUrl = string.IsNullOrWhiteSpace(sourceUrl) ? DefaultSourceUrl : sourceUrl;
        }

        public XmlUpdateStatus GetStatus()
        {
            var store = new JsonDataStore(Path.GetDirectoryName(_statusPath));
            var status = store.LoadObject<XmlUpdateStatus>(Path.GetFileName(_statusPath));
            status.SourceUrl = _sourceUrl;
            return status;
        }

        public XmlUpdateStatus UpdateNow()
        {
            var status = GetStatus();
            status.LastAttemptAt = DateTime.Now;
            status.SourceUrl = _sourceUrl;

            try
            {
                var folder = Path.GetDirectoryName(_xmlPath);
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                var tempPath = _xmlPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                using (var client = new WebClient())
                {
                    client.Encoding = System.Text.Encoding.UTF8;
                    var xmlContent = client.DownloadString(_sourceUrl);
                    File.WriteAllText(tempPath, xmlContent, System.Text.Encoding.UTF8);
                }

                var productCount = ProductCatalogService.ParseProducts(tempPath).Count;
                if (productCount == 0)
                {
                    throw new InvalidOperationException("XML indirildi ancak ürün bulunamadı.");
                }

                if (File.Exists(_xmlPath))
                {
                    File.Replace(tempPath, _xmlPath, null);
                }
                else
                {
                    File.Move(tempPath, _xmlPath);
                }
                status.LastSuccessAt = DateTime.Now;
                status.LastProductCount = productCount;
                status.LastError = null;
            }
            catch (Exception ex)
            {
                status.LastError = ex.Message;
            }

            SaveStatus(status);
            return status;
        }

        private void SaveStatus(XmlUpdateStatus status)
        {
            var store = new JsonDataStore(Path.GetDirectoryName(_statusPath));
            store.SaveObject(Path.GetFileName(_statusPath), status);
        }
    }
}
