using System;

namespace CopyFilesToSharePoint.Models
{
    public class SharePointToken
    {
        public int Expires_In { get; set; }
        public string Access_Token { get; set; }
        public DateTime ExpiresAt { get; private set; }

        public void SetExpiresAt()
        {
            ExpiresAt = DateTime.UtcNow.AddSeconds(Expires_In);
        }
    }
}
