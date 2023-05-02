using CopyFilesToSharePoint.Models;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace CopyFilesToSharePoint.Repository
{
    public interface ISharePointRepository
    {
        Task<SharePointToken> GetAccessToken();
        Task UploadFile(FileInfo sourceFile, string destinationFolder);
        Task<HttpResponseMessage> GetFiles(string destinationFolder);
        Task<HttpResponseMessage> GetFile(string folder, string fileName);
    }
}
