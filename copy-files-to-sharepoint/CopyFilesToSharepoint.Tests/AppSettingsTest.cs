using NUnit.Framework;

namespace CopyFilesToSharePoint.Tests
{
    [TestFixture]
    public class AppSettingsTest
    {
        private readonly AppSettings _appSettings;

        public AppSettingsTest()
        {
            _appSettings = new AppSettings();
        }

        [Test]
        public void Can_read_Environment()
        {
            var result = _appSettings.Environment;

            Assert.AreEqual("dev", result);
        }

        [Test]
        public void Can_read_SourcePath()
        {
            var result = _appSettings.SourcePath;

            Assert.AreEqual("C:\\temp\\source\\Webportal-SDS\\", result);
        }

        [Test]
        public void Can_read_DestinationPublicPath()
        {
            var result = _appSettings.DestinationPublicFolder;

            Assert.AreEqual("Temp_Public", result);
        }

        [Test]
        public void Can_read_DestinationPrivatePath()
        {
            var result = _appSettings.DestinationPrivateFolder;

            Assert.AreEqual("Temp_Private", result);
        }

        [Test]
        public void Can_read_DbUserName()
        {
            var result = _appSettings.DbUserName;

            Assert.AreEqual("LisamRW", result);
        }

        [Test]
        public void Can_read_PollingTimeSeconds()
        {
            var result = _appSettings.PollingTimeSeconds;

            Assert.AreEqual(1, result);
        }
    }
}
