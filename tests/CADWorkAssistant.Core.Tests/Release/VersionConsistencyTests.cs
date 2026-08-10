using System.Xml.Linq;

namespace CADWorkAssistant.Core.Tests.Release;

public class VersionConsistencyTests
{
    [Fact]
    public void ProductVersion_IsConsistentAcrossReleaseFiles()
    {
        var root = FindRepositoryRoot();
        var props = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
        var version = props.Descendants("CwaVersion").Single().Value;
        var channel = props.Descendants("ReleaseChannel").Single().Value;

        Assert.Equal("0.9.0", version);
        Assert.Equal("RC", channel);

        var installerScript = File.ReadAllText(Path.Combine(root, "installer", "CADWorkAssistant.iss"));
        Assert.Contains($"#define AppVersion \"{version}\"", installerScript);
        Assert.Contains($"#define ReleaseChannel \"{channel}\"", installerScript);
        Assert.Contains("OutputBaseFilename=CADWorkAssistant-Setup-{#AppVersion}-{#ReleaseChannel}-x64", installerScript);

        var bundleManifest = XDocument.Load(Path.Combine(root, "installer", "CADWorkAssistant.bundle", "PackageContents.xml"));
        Assert.Equal(version, bundleManifest.Root?.Attribute("AppVersion")?.Value);

        var releaseNotes = File.ReadAllText(Path.Combine(root, "docs", "releases", "RELEASE_NOTES_0.9.0-RC.md"));
        Assert.Contains("CAD Work Assistant 0.9.0 Release Candidate", releaseNotes);
    }

    [Fact]
    public void ReleaseScripts_UseRcArtifactNames()
    {
        var root = FindRepositoryRoot();
        var buildScript = File.ReadAllText(Path.Combine(root, "scripts", "build-release.ps1"));
        var verifyScript = File.ReadAllText(Path.Combine(root, "scripts", "verify-distribution.ps1"));

        Assert.Contains("\"CADWorkAssistant-$version-$releaseChannel\"", buildScript);
        Assert.Contains("CADWorkAssistant-$version-$releaseChannel-x64.zip", buildScript);
        Assert.Contains("CADWorkAssistant-Setup-$version-$releaseChannel-x64.exe", buildScript);
        Assert.Contains("[string]$Version = \"0.9.0\"", verifyScript);
        Assert.Contains("[string]$ReleaseChannel = \"RC\"", verifyScript);
        Assert.Contains("CADWorkAssistant-$Version-$ReleaseChannel-x64.zip", verifyScript);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CADWorkAssistant.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
