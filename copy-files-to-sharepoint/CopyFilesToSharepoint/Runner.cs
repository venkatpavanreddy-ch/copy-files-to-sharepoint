using CopyFilesToSharePoint.Common;
using CopyFilesToSharePoint.Repository;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using static CopyFilesToSharePoint.Models.SharePointResponse;

namespace CopyFilesToSharePoint
{
    public class Runner : ServiceBase
    {
        private readonly AppSettings _appSettings;
        private readonly ILog _logger = ConfigureLogging.For<Runner>();
        private readonly ISharePointRepository _sharePointRepository;

        public Runner() : this(new AppSettings(), new SharePointRepository(new AppSettings()))
        {
        }

        public Runner(AppSettings appSettings, ISharePointRepository sharePointRepository)
        {
            _sharePointRepository = sharePointRepository;
            _appSettings = appSettings;
            _directoriesToSync = new ConcurrentQueue<DirectoryInfo>();
            _privateDirectoriesToSync = new ConcurrentQueue<DirectoryInfo>();
        }

        public void ConsoleStart()
        {
            OnStart(null);
        }

        private bool ShouldLoop { get; set; } = true;

        private readonly ConcurrentQueue<DirectoryInfo> _directoriesToSync;
        private readonly ConcurrentQueue<DirectoryInfo> _privateDirectoriesToSync;

        private Thread _findPublicDirectoriesThread;
        private Thread _findPrivateDirectoriesThread;
        private Thread _processPublicDirectoriesThread;
        private Thread _processPrivateDirectoryThread;

        protected override void OnStart(string[] args)
        {
            _logger.Info("Starting application");
            _findPublicDirectoriesThread = new Thread(FindPublicDirectories);
            _findPublicDirectoriesThread.Start();

            _findPrivateDirectoriesThread = new Thread(FindPrivateDirectories);
            _findPrivateDirectoriesThread.Start();

            Thread.Sleep(TimeSpan.FromSeconds(5));

            _processPublicDirectoriesThread = new Thread(ProcessPublicDirectories);
            _processPublicDirectoriesThread.Start();

            _processPrivateDirectoryThread = new Thread(ProcessPrivateDirectory);
            _processPrivateDirectoryThread.Start();
        }

        private void FindPublicDirectories()
        {
            var lastRunTime = DateTime.Now;
            do
            {
                AddPublicSubDirectories();
                lastRunTime = DetermineLastRunTime(lastRunTime, "FindPublicDirectories");
            } while (ShouldLoop);
        }

        public void AddPublicSubDirectories()
        {
            if (_directoriesToSync.Any())
                return;

            var publicFolderPath = Path.Combine(_appSettings.SourcePath, "Public");
            _logger.Info($"Looking for files in \"{publicFolderPath}\"");
            var publicSourceDirectory = new DirectoryInfo(publicFolderPath);
            foreach (var productDirectory in publicSourceDirectory.GetDirectories())
            {
                _directoriesToSync.Enqueue(productDirectory);
            }
        }

        private void FindPrivateDirectories()
        {
            var lastRunTime = DateTime.Now;
            do
            {
                AddPrivateSubDirectories();
                lastRunTime = DetermineLastRunTime(lastRunTime, "FindPrivateDirectories");
            } while (ShouldLoop);
        }

        public void AddPrivateSubDirectories()
        {
            if (_privateDirectoriesToSync.Any())
                return;

            var privateFolderPath = Path.Combine(_appSettings.SourcePath, "Private");
            _logger.Info($"Looking for files in \"{privateFolderPath}\"");
            var privateSourceDirectory = new DirectoryInfo(privateFolderPath);
            foreach (var productDirectory in privateSourceDirectory.GetDirectories())
            {
                _privateDirectoriesToSync.Enqueue(productDirectory);
            }
        }

        private void ProcessPublicDirectories()
        {
            var lastRunTime = DateTime.Now;
            while (ShouldLoop)
            {
                SyncAllPublicFiles();
                lastRunTime = DetermineLastRunTime(lastRunTime, "ProcessPublicDirectories");
            }
        }

        public void SyncAllPublicFiles()
        {
            while (_directoriesToSync.TryDequeue(out var sourceDirectory))
            {
                var sourceFiles = sourceDirectory.GetFiles().Where(x => x.LastWriteTime >= DateTime.Now.AddDays(-1)).ToArray();
                if (!sourceFiles.Any() && !_appSettings.IsOneTimeJob)
                {
                    _logger.Debug($"The source folder \"{sourceDirectory.FullName}\" does not have any files inside");
                    continue;
                }

                if (_appSettings.IsOneTimeJob)
                {
                    sourceFiles = sourceDirectory.GetFiles();
                }
                CopyPublicFilesToSharePoint(sourceFiles);
            }
        }

        private void ProcessPrivateDirectory()
        {
            var lastRunTime = DateTime.Now;
            while (ShouldLoop)
            {
                SyncAllPrivateFiles();
                lastRunTime = DetermineLastRunTime(lastRunTime, "ProcessPrivateDirectory");
            }
        }

        public void SyncAllPrivateFiles()
        {
            while (_privateDirectoriesToSync.TryDequeue(out var sourceDirectory))
            {
                
                    var sourceFiles = sourceDirectory.GetFiles().Where(x => x.LastWriteTime >= DateTime.Now.AddDays(-2)).ToArray();
                    if (!sourceFiles.Any() && !_appSettings.IsOneTimeJob)
                    {
                        _logger.Debug($"The source folder \"{sourceDirectory.FullName}\" does not have any files inside");
                        continue;
                    }

                    if (_appSettings.IsOneTimeJob)
                    {
                        sourceFiles = sourceDirectory.GetFiles();
                    }
                    CopyPrivateFilesToSharePoint(sourceFiles);
               
            }
        }

        public bool IsFileCopied(string fileName, string destinationFolder)
        {
            var response = GetFilesFromSharePoint(destinationFolder, fileName).Result;

            return response != null ? response.IsSuccessStatusCode : false;
        }

        public void DeleteFile(FileInfo sourceFile)
        {
            if (_appSettings.CanDelete && sourceFile.LastWriteTime < DateTime.Now.AddHours(-24))
            {
                sourceFile.Delete();
                _logger.Info($"File {sourceFile.Name} deleted from source.");
            }
        }

        private void CopyPrivateFilesToSharePoint(FileInfo[] sourceFiles)
        {
            foreach (var sourceFile in sourceFiles)
            {
                CheckAndUploadFileExist(_appSettings.DestinationPrivateFolder, sourceFile);
            }
        }

        private void CheckAndUploadFileExist(string folder, FileInfo sourceFile)
        {
            if (_appSettings.IsOneTimeJob)
            {
                CopyFiles(folder, sourceFile);
            }
            else
            {
                try
                {
                    var resp = _sharePointRepository.GetFile(folder.Replace("Temp_",""), sourceFile.Name).Result;
                    var data = resp.Content.ReadAsStringAsync().Result;
                    var doc = XDocument.Parse(data);
                    string jsonText = JsonConvert.SerializeXNode(doc);
                    var myDeserializedClass = JsonConvert.DeserializeObject<Root>(jsonText);
                    if (myDeserializedClass.entry == null || CheckSourceFileIsNewer(myDeserializedClass, sourceFile))
                    {
                        CopyFiles(folder, sourceFile);
                    }
                    else
                    {
                        _logger.Info($"File {sourceFile.Name} existed");
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Exception occurred: {ex}");
                }
            }
        }

        private static bool CheckSourceFileIsNewer(Root myDeserializedClass, FileInfo sourceFile)
        {
            return (myDeserializedClass.entry?.Content?.Mproperties?.DCreated?.text < sourceFile.CreationTime || myDeserializedClass.entry?.Content?.Mproperties?.DModified?.text < sourceFile.LastWriteTime);
        }

        private void CopyFiles(string folder, FileInfo sourceFile)
        {
            _sharePointRepository.UploadFile(sourceFile, folder);
            if (IsFileCopied(sourceFile.Name, folder))
            {
                _logger.Info($"File {sourceFile.Name} copied to SharePoint");
                if (_appSettings.CanDelete)
                {
                    sourceFile.Delete();
                }
            }
        }

        private void CopyPublicFilesToSharePoint(FileInfo[] sourceFiles)
        {
            foreach (var sourceFile in sourceFiles)
            {
                CheckAndUploadFileExist(_appSettings.DestinationPublicFolder, sourceFile);
                break;
            }
        }

        private Task<HttpResponseMessage> GetFilesFromSharePoint(string destinationFolder, string fileName)
        {
            return _sharePointRepository.GetFile(destinationFolder, fileName);
        }

        private DateTime DetermineLastRunTime(DateTime lastRunTime, string loopName)
        {
            var lastRanTimeSpan = DateTime.Now - lastRunTime;
            if (lastRanTimeSpan < TimeSpan.FromSeconds(_appSettings.PollingTimeSeconds))
            {
                var pullingTimeSeconds = _appSettings.PollingTimeSeconds - lastRanTimeSpan.Seconds;
                _logger.Info($"Waiting {pullingTimeSeconds} seconds for next loop of {loopName}");
                Thread.Sleep(pullingTimeSeconds * 1000);
            }
            else
            {
                _logger.Warn($"Processing time of {lastRanTimeSpan} took more than {_appSettings.PollingTimeSeconds} seconds");
            }

            lastRunTime = DateTime.Now;
            return lastRunTime;
        }

        protected override void OnStop()
        {
            ShouldLoop = false;

            _findPublicDirectoriesThread?.Abort();
            _findPublicDirectoriesThread = null;

            _findPrivateDirectoriesThread?.Abort();
            _findPrivateDirectoriesThread = null;

            _processPublicDirectoriesThread?.Abort();
            _processPublicDirectoriesThread = null;

            _processPrivateDirectoryThread?.Abort();
            _processPrivateDirectoryThread = null;

            _logger.Info("Exiting application");
        }
    }
}
