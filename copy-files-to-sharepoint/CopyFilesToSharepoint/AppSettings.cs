using CopyFilesToSharePoint.Common;

namespace CopyFilesToSharePoint
{
    public class AppSettings : AppSettingsBase
    {
        public string SourcePath => GetString("SourcePath");
        public string DestinationPublicFolder => GetString("DestinationPublicFolder");
        public string DestinationPrivateFolder => GetString("DestinationPrivateFolder");
        public string DbUserName => GetString("DbUserName");
        public string DbPassword => GetPassword(DbUserName);
        public string Domain => GetString("Domain");
        public string SiteUrl => GetString("SiteUrl");
        public string BaseUrl => GetString("BaseUrl");
        public string LeberDWSConnectionString => GetConnectionString("LeberDWS", DbUserName, DbPassword);
        public int PollingTimeSeconds = GetInt(nameof(PollingTimeSeconds));
        public bool IsOneTimeJob = GetBool("IsOneTimeJob");
        public bool CanDelete = GetBool("CanDelete");
        public string SharePointSettings => GetPassword("SharePointSettings.json");
    }
}
