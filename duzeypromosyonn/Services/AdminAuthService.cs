using System.Configuration;
using System.Web.Helpers;
using duzeypromosyonn.Models;

namespace duzeypromosyonn.Services
{
    public class AdminAuthService
    {
        public bool Validate(AdminLoginViewModel model)
        {
            if (model == null)
            {
                return false;
            }

            var configuredUser = ConfigurationManager.AppSettings["DuzeyAdminUser"] ?? "admin";
            var configuredPasswordHash = ConfigurationManager.AppSettings["DuzeyAdminPasswordHash"];
            var configuredPassword = ConfigurationManager.AppSettings["DuzeyAdminPassword"];

            if (!string.Equals(model.UserName, configuredUser, System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(configuredPasswordHash))
            {
                return Crypto.VerifyHashedPassword(configuredPasswordHash, model.Password);
            }

            return !string.IsNullOrWhiteSpace(configuredPassword)
                && string.Equals(model.Password, configuredPassword);
        }
    }
}
