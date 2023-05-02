using System.Configuration;
using System.IO;

namespace CopyFilesToSharePoint.Common
{
    public abstract class AppSettingsBase
    {
        protected const string PASSWORD_VAULT_PATH = @"\\lm-srvapp01\IT Stuff\Deploy\vault\{environment}\{userNameKey}";

        public string Environment => GetString("Environment");

        protected static string GetConnectionString(string name, string userName, string password)
        {
            return ConfigurationManager.ConnectionStrings[name].ConnectionString
                .Replace("{UserName}", userName)
                .Replace("{Password}", password);
        }

        protected string GetPassword(string userName)
        {
            return File.ReadAllText(PASSWORD_VAULT_PATH.Replace("{environment}", Environment).Replace("{userNameKey}", userName));
        }

        protected static string GetString(string key)
        {
            return ConfigurationManager.AppSettings[key];
        }

        protected static int GetInt(string key)
        {
            return int.Parse(GetString(key));
        }

        protected static bool GetBool(string key)
        {
            return bool.Parse(GetString(key));
        }
    }
}
