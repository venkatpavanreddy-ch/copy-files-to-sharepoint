using CopyFilesToSharePoint.Repository;
using NUnit.Framework;
using System;
using System.IO;

namespace CopyFilesToSharePoint.Tests
{
    [TestFixture]
    public class EndToEndTest
    {
        private string _fileName;
        private Runner _runner;
        private AppSettings _appSettings;
        private SharePointRepository _sharePointRepository;

        [SetUp]
        public void Setup()
        {
            _fileName = Guid.NewGuid().ToString().Replace("-", "") + ".pdf";

            _appSettings = new AppSettings();

            _sharePointRepository = new SharePointRepository(_appSettings);
            _runner = new Runner(_appSettings, _sharePointRepository);

            CleanDirectory(Path.Combine(_appSettings.SourcePath, ""));
            CleanDirectory(Path.Combine(_appSettings.SourcePath, "Public"));
            CleanDirectory(Path.Combine(_appSettings.SourcePath, "Private"));
        }

        [TestCase(@"00-0000-00", "PublicTestProduct")]
        public void File_copied_in_public_folder(string productCode, string productName)
        {
            string SUBFOLDER = $"Public\\{productCode}";
            var fileName = $"{productCode}_{productName}_{_fileName}";
            var fileInfo = CreateFileUpdatedOnInSourceFolder(DateTime.Today.AddHours(1), SUBFOLDER, fileName);

            _runner.AddPublicSubDirectories();
            _runner.SyncAllPublicFiles();
            System.Threading.Thread.Sleep(2000);
            var result = _runner.IsFileCopied(fileInfo.Name, _appSettings.DestinationPublicFolder);
            Assert.That(result);
        }

        [TestCase(@"00-0000-00", "PrivateTestProduct")]
        public void File_copied_in_private_folder(string productCode, string productName)
        {
            string SUBFOLDER = $"Private\\{productCode}";
            var fileName = $"{productCode}_{productName}_{_fileName}";
            var fileInfo = CreateFileUpdatedOnInSourceFolder(DateTime.Today.AddHours(1), SUBFOLDER, fileName);
            _runner.AddPrivateSubDirectories();
            _runner.SyncAllPrivateFiles();
            System.Threading.Thread.Sleep(2000);
            var result = _runner.IsFileCopied(fileInfo.Name, _appSettings.DestinationPrivateFolder);
            Assert.That(result);
        }

        [TestCase(@"00-0000-00", "PrivateTestProductShouldBeDelete")]
        public void File_copied_in_private_folder_and_removed_from_source_folder(string productCode, string productName)
        {
            string SUBFOLDER = $"Private\\{productCode}";
            var fileName = $"{productCode}_{productName}todelete_{_fileName}";
            var fileInfo =  CreateFileUpdatedOnInSourceFolder(DateTime.Today.AddHours(-26), SUBFOLDER, fileName);
            _runner.AddPrivateSubDirectories();
            _runner.SyncAllPrivateFiles();
            System.Threading.Thread.Sleep(2000);
            var result = _runner.IsFileCopied(fileInfo.Name, _appSettings.DestinationPrivateFolder);
            Assert.That(result);
            _runner.DeleteFile(fileInfo);
            Assert.That(!File.Exists(fileInfo.FullName));
        }

        [TestCase(@"00-0000-00", "PrivateTestProductShouldBeDelete")]
        public void Delete_all_files_from_source_folder_created_or_modified_24hours_before(string productCode, string productName)
        {
            string SUBFOLDER = $"Private\\{productCode}";
            var fileName = $"{productCode}_{productName}todelete_1{_fileName}";
            var fileInfo = CreateFileUpdatedOnInSourceFolder(DateTime.Today.AddHours(-26), SUBFOLDER, fileName);
            _runner.DeleteFile(fileInfo);
            Assert.That(!File.Exists(fileInfo.FullName));
        }

        [TestCase(@"00-0000-00", "PrivateTestProductShouldNotDelete")]
        public void Should_not_delete_files_from_source_folder_created_or_modified_with_in_last_24hours(string productCode, string productName)
        {
            string SUBFOLDER = $"Private\\{productCode}";
            var fileName = $"{productCode}_{productName}todelete_2{_fileName}";
            var fileInfo = CreateFileUpdatedOnInSourceFolder(DateTime.Today.AddHours(-2), SUBFOLDER, fileName);
            _runner.DeleteFile(fileInfo);
            Assert.That(File.Exists(fileInfo.FullName));
        }

        private static void CleanDirectory(string path)
        {
            var sourceDirectory = new DirectoryInfo(path);

            if (sourceDirectory.Exists)
                sourceDirectory.Delete(true);

            sourceDirectory.Create();
        }

        private FileInfo CreateFileUpdatedOnInSourceFolder(DateTime time, string folder,string fileName)
        {
            var folderPath = Path.Combine(_appSettings.SourcePath, folder);
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            var filePath = Path.Combine(folderPath, fileName);

            File.WriteAllText(filePath, "source text");

            var fileInfo = new FileInfo(filePath);
            fileInfo.CreationTime = time;
            fileInfo.LastAccessTime = time;
            fileInfo.LastWriteTime = time;
            return fileInfo;
        }
    }
}
