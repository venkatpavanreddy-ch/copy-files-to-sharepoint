namespace CopyFilesToSharePoint.Models
{
    public class SharePointSettings
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string TenantId { get; set; }
        public string Domain { get; set; }
        public string SiteUrl { get; set; }
        public string BaseUrl { get; set; }
    }
}
