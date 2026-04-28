using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Newtonsoft.Json;

namespace duzeypromosyonn.Services
{
    public class JsonDataStore
    {
        private static readonly object StoreLock = new object();
        private readonly string _appDataPath;

        public JsonDataStore(string appDataPath)
        {
            _appDataPath = appDataPath;
        }

        public IList<T> Load<T>(string fileName)
        {
            lock (StoreLock)
            {
                var path = PathFor(fileName);
                if (!File.Exists(path))
                {
                    return new List<T>();
                }

                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<T>();
                }

                return JsonConvert.DeserializeObject<IList<T>>(json) ?? new List<T>();
            }
        }

        public void Save<T>(string fileName, IEnumerable<T> items)
        {
            lock (StoreLock)
            {
                if (!Directory.Exists(_appDataPath))
                {
                    Directory.CreateDirectory(_appDataPath);
                }

                var path = PathFor(fileName);
                var tempPath = path + ".tmp";
                var json = JsonConvert.SerializeObject((items ?? Enumerable.Empty<T>()).ToList(), Formatting.Indented);
                File.WriteAllText(tempPath, json);

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(tempPath, path);
            }
        }

        public T LoadObject<T>(string fileName) where T : new()
        {
            lock (StoreLock)
            {
                var path = PathFor(fileName);
                if (!File.Exists(path))
                {
                    return new T();
                }

                var json = File.ReadAllText(path);
                var value = JsonConvert.DeserializeObject<T>(json);
                return (object)value == null ? new T() : value;
            }
        }

        public void SaveObject<T>(string fileName, T value)
        {
            lock (StoreLock)
            {
                if (!Directory.Exists(_appDataPath))
                {
                    Directory.CreateDirectory(_appDataPath);
                }

                var path = PathFor(fileName);
                var tempPath = path + ".tmp";
                File.WriteAllText(tempPath, JsonConvert.SerializeObject(value, Formatting.Indented));
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
                File.Move(tempPath, path);
            }
        }

        public static JsonDataStore FromHttpContext()
        {
            return new JsonDataStore(HttpContext.Current.Server.MapPath("~/App_Data"));
        }

        private string PathFor(string fileName)
        {
            return Path.Combine(_appDataPath, fileName);
        }
    }
}
