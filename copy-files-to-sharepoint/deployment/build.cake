#tool nuget:?package=NuGet.CommandLine&version=5.9.1
#tool nuget:?package=NUnit.ConsoleRunner&version=3.15.0
#addin nuget:?package=Cake.Json&version=7.0.1
#addin nuget:?package=Cake.FileHelpers&version=5.0.0
#addin nuget:?package=System.ServiceProcess.ServiceController&version=5.0.0
#addin nuget:?package=Microsoft.PowerShell.CoreCLR.Eventing&version=7.2.1
#addin nuget:?package=Microsoft.ApplicationInsights&version=2.18.0
#addin nuget:?package=Microsoft.Management.Infrastructure&version=1.0.0
#addin nuget:?package=System.Management&version=6.0.0
#addin nuget:?package=System.DirectoryServices&version=6.0.0
#addin nuget:?package=System.Management.Automation&version=7.2.1
#addin nuget:?package=System.Security.Permissions&version=6.0.0
#addin nuget:?package=Cake.Powershell&version=2.0.0
#addin nuget:?package=Cake.Services&version=1.0.1

#load "lsa-wrapper.cake"

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Debug");
var environment = Argument("environment", "dev");
var deployProject = Argument("deployProject", "CopyFilesToSharePoint");
var solutionFile = Argument("solutionFile", "CopyFilesToSharePoint.sln");
var deploymentPath = Argument("deploymentPath", "C://Program Files");
var domain = Argument("domain", "TLC");

///////////////////////////////////////////////////////////////////////////////
// SETUP / TEARDOWN
///////////////////////////////////////////////////////////////////////////////

Setup(ctx =>
{
    Information($"Running tasks for {deployProject}...");

    _projects = GetFiles("../**/*.csproj")
        .Select(fp => new ProjectInformation(configuration)
        {
            Name = fp.GetFilenameWithoutExtension().ToString(),
            FullPath = fp.GetDirectory().FullPath,
        })
        .ToList();
});

Teardown(ctx =>
{
    Information("Finished running tasks");
});

///////////////////////////////////////////////////////////////////////////////
// GLOBALS
///////////////////////////////////////////////////////////////////////////////

public class DeploymentSettings
{
    public string ServerName { get; set; }
    public string ServiceAccount { get; set; }
    public string LeberDWSConnectionString { get; set; }
    public string UserName { get; set; }
    public string SourcePath { get; set; }
    public string DestinationPublicFolder { get; set; }
    public string IsOneTimeJob { get; set; }
    public string CanDelete { get; set; }
    public string DestinationPrivateFolder { get; set; }
    public string DbUserName { get; set; }
    public string SiteUrl { get; set; }

    public bool IsLocal => ServerName == "localhost";
}

public class ProjectInformation
{
    public ProjectInformation(string configuration)
    {
        _configuration = configuration;
    }

    private string _configuration;

    public string Name { get; set; }
    public string FullPath { get; set; }

    public bool IsTestProject => Name.EndsWith("Tests");
    public string BuildPath => $"{FullPath}/bin/{_configuration}";
    public string DllPath => $"{BuildPath}/{Name}.dll";
}

private List<ProjectInformation> _projects;

private const string STAGING_FOLDER = $"./src";
private const string PROGRAM_FILES_SUB_FOLDER = "Lebermuth";
private const string PASSWORD_VAULT_PATH ="\\\\lm-srvapp01\\it stuff\\Deploy\\vault";

private void WriteWarning(string message)
{
    var previousColor = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Yellow;
    Warning(message);
    Console.ForegroundColor = previousColor;
}

private void SetAppSettings(string key, string value, string appSettingsFilePath)
{
    Information($"Setting appSetting key: \u001b[36m{key}\u001b[0m value: \u001b[36m{value}\u001b[0m");
    var file = File(appSettingsFilePath);
    XmlPoke(file, $"/configuration/appSettings/add[@key = '{key}']/@value", value);
}

private void SetConnectionString(string name, string connectionString, string appSettingsFilePath)
{
    Information($"Setting connectionStrings name: \u001b[36m{name}\u001b[0m connectionString: \u001b[36m{connectionString}\u001b[0m");

    var file = File(appSettingsFilePath);
    XmlPoke(file, $"/configuration/connectionStrings/add[@name = '{name}']/@connectionString", connectionString);
}

private void WriteException(System.Exception e)
{
    WriteWarning(e.Message);
    var innerException = e.InnerException;
    while (innerException != null)
    {
        WriteWarning(innerException.Message);
        innerException = innerException.InnerException;
    }
}

private string GetPasswordFromVault(string passwordVaultPath, string accountName, string environment)
{
    var vaultPath = $"{passwordVaultPath}\\{environment}\\{accountName}";
    var password = FileReadText(vaultPath);

    return password;
}

private void SetServiceDesctiption(string deployProject, string description, string serverName)
{
    var powerSettings = new PowershellSettings
    {
        FormatOutput = true,
        LogOutput = true,
        OutputToAppConsole = true,
        ComputerName = serverName,
    }
    .WithArguments(args =>
    {
        args.AppendQuoted(deployProject);
        args.AppendQuoted(description);
    });

    StartPowershellScript("& \"sc.exe\" description", powerSettings);
}

///////////////////////////////////////////////////////////////////////////////
// TASKS
///////////////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("Run Tests")
    .IsDependentOn("Stage Build")
    .IsDependentOn("Deploy");

Task("Clean")
    .Does(() =>
    {
        if(DirectoryExists(STAGING_FOLDER))
            CleanDirectory(STAGING_FOLDER);

        foreach(var project in _projects)
        {
            Information($"Cleaning {project.Name} bin and obj folders");
            CleanDirectory($"{project.BuildPath}");
            CleanDirectory($"{project.FullPath}/obj/{configuration}");
        }
    });

Task("Build")
    .IsDependentOn("Clean")
    .Does(() =>
    {
        var solutionPath = $"../{solutionFile}";
        NuGetRestore(solutionPath);
        MSBuild(solutionPath, new MSBuildSettings
        {
            Configuration = configuration,
            ToolVersion = MSBuildToolVersion.VS2022,
        });
    });

Task("Run Tests")
    .IsDependentOn("Build")
    .Does(() =>
    {
        if(_projects.All(p => !p.IsTestProject))
        {
            WriteWarning("No test projects found");
            return;
        }

        foreach(var project in _projects.Where(p => p.IsTestProject))
        {
            Information($"Running tests for \u001b[90m{project.FullPath}\u001b[0m");

            var resultsFile = $"{project.BuildPath}/NUnitResults.xml";
            Information($"Results file \u001b[90m{resultsFile}\u001b[0m");
            var nUnitSettings = new NUnit3Settings
            {
                Results = new[] { new NUnit3Result { FileName = resultsFile } },
                WorkingDirectory = project.BuildPath,
            };
            Information($"Dll path: \u001b[90m\"{project.DllPath}\"\u001b[0m");
            NUnit3(project.DllPath, nUnitSettings);
            Information($"Finished running tests for \u001b[90m{project.FullPath}\u001b[0m");
        }
    });

Task("Stage Build")
    .IsDependentOn("Build")
    .Does(() =>
    {
        Information($"Staging build for {deployProject}");

        var toDeploy = _projects.SingleOrDefault(p => p.Name == deployProject);
        if(toDeploy == null)
            throw new Exception($"Unable to find the project {deployProject}, be sure this value is configured correctly.");

        EnsureDirectoryExists(STAGING_FOLDER);
        CopyFiles(toDeploy.BuildPath + "/*", STAGING_FOLDER);
    });

Task("Deploy")
    .Does(() =>
    {
        var settingsFilePath = $"./settings/{environment}.json";
        if(!FileExists(settingsFilePath))
            throw new System.Exception($"Invalid environment \"{environment}\": unable to find {environment}.json file in Settings folder");

        var settings = DeserializeJsonFromFile<DeploymentSettings>(settingsFilePath);

        try
        {
            if(IsServiceInstalled(deployProject, settings.ServerName))
            {
                StopService(deployProject, settings.ServerName);
                UninstallService(deployProject, settings.ServerName);
            }
        }
        catch (System.Exception e)
        {
            WriteException(e);
            WriteWarning($"Unable to stop/uninstall the {deployProject} service on {settings.ServerName}");
        }

        //string deploymentPath;
        string localPath = $"{deploymentPath}/{PROGRAM_FILES_SUB_FOLDER}/{deployProject}";
        if(settings.IsLocal)
            deploymentPath = localPath;
        else
            deploymentPath = $"\\\\{settings.ServerName}\\C$\\Program Files\\{PROGRAM_FILES_SUB_FOLDER}\\{deployProject}";

        Information($"Deploying \u001b[36m{environment}\u001b[0m version of {deployProject} to \u001b[90m\"{deploymentPath}\"\u001b[0m");
        EnsureDirectoryExists(deploymentPath);
        CopyFiles(STAGING_FOLDER + "/*", deploymentPath);

        var appSettingsFilePath = $"{deploymentPath}/{deployProject}.exe.config";
        if(!FileExists(appSettingsFilePath))
            throw new Exception($"Unable to find app config file {appSettingsFilePath}");

        SetConnectionString("LeberDWS", settings.LeberDWSConnectionString, appSettingsFilePath);

        SetAppSettings("ServiceAccount", settings.ServiceAccount, appSettingsFilePath);
        SetAppSettings("SourcePath", settings.SourcePath, appSettingsFilePath);
        SetAppSettings("DestinationPublicFolder", settings.DestinationPublicFolder, appSettingsFilePath);
        SetAppSettings("DestinationPrivateFolder", settings.DestinationPrivateFolder, appSettingsFilePath);
        SetAppSettings("CanDelete", settings.CanDelete, appSettingsFilePath);
        SetAppSettings("IsOneTimeJob", settings.IsOneTimeJob, appSettingsFilePath);
        SetAppSettings("DbUserName", settings.DbUserName, appSettingsFilePath);
        SetAppSettings("Environment", environment, appSettingsFilePath);
        SetAppSettings("SiteUrl", settings.SiteUrl, appSettingsFilePath);

        var installSettings = new InstallSettings
        {
            DisplayName = deployProject,
            ServiceName = deployProject,
            ExecutablePath = $"{localPath}/{deployProject}.exe",
            Username = $"{domain}\\{settings.ServiceAccount}",
            Password = GetPasswordFromVault(PASSWORD_VAULT_PATH, settings.ServiceAccount, environment),
            StartMode = "auto",
        };
        // boot | system | auto | demand | disabled | delayed-auto

        GrantLogonAsServiceRight(settings.ServiceAccount);
        try
        {
            InstallService(settings.ServerName, installSettings);
            StartService(deployProject, settings.ServerName);
        }
        catch (System.Exception e)
        {
            WriteException(e);
            WriteWarning($"Unable to install/start the {deployProject} service on {settings.ServerName}");
        }
        try
        {
            SetServiceDesctiption(deployProject, deployProject + " application", settings.ServerName);
        }
        catch (System.Exception e)
        {
            WriteException(e);
            WriteWarning($"Unable to set the description for the {deployProject} service on {settings.ServerName}");
        }
    });

//////////////////////////////////////////////////////////////////////
// EXECUTE
//////////////////////////////////////////////////////////////////////

RunTarget(target);
