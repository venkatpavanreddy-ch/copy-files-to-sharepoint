using CopyFilesToSharePoint.Common;
using CopyFilesToSharePoint.Models;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static CopyFilesToSharePoint.Models.SharePointResponse;

namespace CopyFilesToSharePoint.Repository
{
    public class SharePointRepository : ISharePointRepository
    {
        private readonly AppSettings _appSettings;
        private readonly SharePointSettings _sharePointSettings;
        private HttpClient _httpClient = new HttpClient();
        private readonly ILog _logger = ConfigureLogging.For<SharePointRepository>();

        public SharePointRepository(AppSettings appSettings)
        {
            _appSettings = appSettings;
            _sharePointSettings = GetSharePointSettings();
        }

        public async Task UploadFile(FileInfo sourceFile, string destinationFolder)
        {
            var sharePointToken = await GetAccessToken();
            try
            {
                if (sharePointToken != null && !string.IsNullOrEmpty(sharePointToken.Access_Token))
                {
                    var path = Path.Combine(Directory.GetCurrentDirectory(), "upload", sourceFile.FullName);
                    byte[] docAsBytes = File.ReadAllBytes(sourceFile.FullName);
                    var imageBytes = new ByteArrayContent(docAsBytes);

                    var url = $"{_sharePointSettings.SiteUrl}/_api/web/GetFolderByServerRelativeUrl('{destinationFolder}')/Files/add(Overwrite='true',url='{sourceFile.Name}')";
                    var request = new HttpRequestMessage(HttpMethod.Post, url);
                    _httpClient.DefaultRequestHeaders.Clear();
                    _httpClient.DefaultRequestHeaders.Add("Authorization", new AuthenticationHeaderValue("bearer", sharePointToken.Access_Token).ToString());

                    /// Upload file with content
                    var response = await _httpClient.PostAsync(url, imageBytes);

                    if (response.IsSuccessStatusCode)
                    {
                        var updateResponse = await UpdateMetadata(sourceFile, destinationFolder);
                        if (updateResponse != null && updateResponse.IsSuccessStatusCode)
                        {
                            _logger.Info($"Properties are updated successfully: {updateResponse.StatusCode}");
                        }
                        else
                        {
                            _logger.Info($"Properties update failed: {updateResponse.ReasonPhrase}");
                        }
                    }
                    else
                    {
                        _logger.Info($"File Upload failed for the file: {sourceFile.Name}, with the following reason: {response.ReasonPhrase}");
                    }
                }
                else
                {
                    _logger.Error($"SharePoint token is not generated: {sharePointToken}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Exception Occurred while uploading the file {sourceFile.Name}");
                _logger.Error($"Exception: {ex}");
            }
        }

        public async Task<HttpResponseMessage> GetFiles(string destinationFolder)
        {
            var sharePointToken = await GetAccessToken();
            try
            {
                if (sharePointToken != null && !string.IsNullOrEmpty(sharePointToken.Access_Token))
                {
                    var getUrl = $"{_sharePointSettings.SiteUrl}/_api/web/GetFolderByServerRelativeUrl('{destinationFolder}')/Files";

                    var request = new HttpRequestMessage(HttpMethod.Get, getUrl);
                    _httpClient.DefaultRequestHeaders.Clear();
                    _logger.Info($"Calling SharePoint api to get all files from folder: {destinationFolder} started");
                    _httpClient.DefaultRequestHeaders.Add("Authorization", new AuthenticationHeaderValue("bearer", sharePointToken.Access_Token).ToString());

                    var getResponse = await _httpClient.SendAsync(request);
                    _logger.Info($"Calling SharePoint api to get all files from folder: {destinationFolder} completed");
                    return getResponse;
                }

                _logger.Error($"SharePoint Token is not generated: {sharePointToken}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                return null;
            }
        }

        public async Task<HttpResponseMessage> GetFile(string folder, string fileName)
        {
            var sharePointToken = await GetAccessToken();
            try
            {
                if (sharePointToken != null && !string.IsNullOrEmpty(sharePointToken.Access_Token))
                {
                    var getUrl = $"{_sharePointSettings.SiteUrl}/_api/web/GetFileByServerRelativePath(decodedurl='/{_sharePointSettings.SiteUrl.Replace(_sharePointSettings.BaseUrl, "")}/{folder}/{fileName}')/ListItemAllFields";

                    var request = new HttpRequestMessage(HttpMethod.Get, getUrl);
                    _httpClient.DefaultRequestHeaders.Clear();
                    _logger.Info("calling SharePoint api to get file details started");
                    _httpClient.DefaultRequestHeaders.Add("Authorization", new AuthenticationHeaderValue("bearer", sharePointToken.Access_Token).ToString());

                    var getResponse = await _httpClient.SendAsync(request);
                    _logger.Info("calling SharePoint api to get file details completed");

                    return getResponse;
                }

                _logger.Error($"SharePoint Token is not generated: {sharePointToken}");
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex);
                return null;
            }
        }

        public async Task<SharePointToken> GetAccessToken()
        {
            SharePointToken sharePointToken = null;
            try
            {
                string requestUrl = $"https://accounts.accesscontrol.windows.net/{_sharePointSettings.TenantId}/tokens/OAuth/2";
                var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);
                request.Content = new FormUrlEncodedContent(GetParameters(_sharePointSettings));
                var defaultRequestHeaders = _httpClient.DefaultRequestHeaders;
                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    string content = response.Content.ReadAsStringAsync().Result;
                    sharePointToken = JsonConvert.DeserializeObject<SharePointToken>(content);
                    sharePointToken.SetExpiresAt();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"{ex}");

            }
            return sharePointToken;
        }

        public async Task<HttpResponseMessage> UpdateMetadata(FileInfo file, string destinationFolder)
        {
            if (!string.IsNullOrEmpty(destinationFolder))
            {
                var sharePointToken = await GetAccessToken();
                var response = await GetFile(destinationFolder, file.Name);

                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var data = response.Content.ReadAsStringAsync().Result;
                        var doc = XDocument.Parse(data);
                        string jsonText = JsonConvert.SerializeXNode(doc);
                        var myDeserializedClass = JsonConvert.DeserializeObject<FileRefRoot>(jsonText);

                        var itemPayload = GetPropertyValues(file);
                        var url = $"{_sharePointSettings.SiteUrl}/_api/{(myDeserializedClass?.Entry?.Link).First(x => x.Rel.ToLower() == "edit").Href}";

                        var request = new HttpRequestMessage(HttpMethod.Post, url);
                        _httpClient.DefaultRequestHeaders.Clear();
                        _httpClient.DefaultRequestHeaders.Add("Authorization", new AuthenticationHeaderValue("bearer", sharePointToken.Access_Token).ToString());
                        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        request.Content = new StringContent(itemPayload, Encoding.UTF8, "application/json");//CONTENT-TYPE header
                        request.Headers.Add("X-HTTP-Method", "PATCH");
                        request.Headers.Add("If-Match", "*");
                        var updateResponse = await _httpClient.SendAsync(request);
                        return updateResponse;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Exception occurred while updating properties: { ex}");
                        return null;
                    }
                }

                _logger.Error($"Given file '{ file.Name}' is not found in the SharePoint");
                return null;
            }

            _logger.Error("file name is empty");
            return null;
        }

        private static string GetPropertyValues(FileInfo file)
        {
            if (!string.IsNullOrEmpty(file.Name))
            {
                var splitStringArray = file.Name.Split('_');
                string productCode = splitStringArray.Length > 0 ? splitStringArray[0] : file.Name;
                string productName = splitStringArray.Length > 0 ? splitStringArray[1] : file.Name;
                string documentName = splitStringArray.Length > 2 ? string.Join("_", splitStringArray.ToList().Skip(2)) : file.Name;
                string fileCreatedDate = file.CreationTime.ToString("yyyy-MM-ddTHH:mm:ssZ");//This format is expecting by the SharePoint Edm.DateTime
                string content = $" 'Product_x0020_Name':'{productName}', 'Document_x0020_Name':'{documentName}', 'Product_x0020_Code':'{productCode}', 'Document_x0020_Version':'1', 'Original_x0020_created_x0020_date':'{fileCreatedDate}'";
                return "{" + content + "}";
            }

            return null;
        }

        private static IEnumerable<KeyValuePair<string, string>> GetParameters(SharePointSettings sharePointSettings)
        {
            var paramList = new List<KeyValuePair<string, string>>();
            paramList.Add(new KeyValuePair<string, string>("grant_type", "client_credentials"));
            paramList.Add(new KeyValuePair<string, string>("client_id", $"{sharePointSettings.ClientId}@{sharePointSettings.TenantId}"));
            paramList.Add(new KeyValuePair<string, string>("client_secret", sharePointSettings.ClientSecret));
            paramList.Add(new KeyValuePair<string, string>("resource", $"00000003-0000-0ff1-ce00-000000000000/{sharePointSettings.Domain}@{sharePointSettings.TenantId}"));
            return paramList;
        }

        private SharePointSettings GetSharePointSettings()
        {
            var sharePointSettings = JsonConvert.DeserializeObject<SharePointSettings>(_appSettings.SharePointSettings);
            sharePointSettings.BaseUrl = _appSettings.BaseUrl;
            sharePointSettings.SiteUrl = _appSettings.SiteUrl;
            sharePointSettings.Domain = _appSettings.Domain;
            return sharePointSettings;
        }
    }
}
