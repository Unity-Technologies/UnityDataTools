using System;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityDataTools.FileSystem;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

public class WebBundleSupportTests
{
    private string m_TestOutputFolder;
    private string m_TestDataFolder;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        m_TestOutputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "test_folder");
        m_TestDataFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data");
        Directory.CreateDirectory(m_TestOutputFolder);
        Directory.SetCurrentDirectory(m_TestOutputFolder);
    }

    [TearDown]
    public void Teardown()
    {
        SqliteConnection.ClearAllPools();

        var testDir = new DirectoryInfo(m_TestOutputFolder);
        testDir.EnumerateFiles()
            .ToList().ForEach(f => f.Delete());
        testDir.EnumerateDirectories()
            .ToList().ForEach(d => d.Delete(true));
    }

    [Test]
    public void IsWebBundle_True()
    {
        var webBundlePath = Path.Combine(m_TestDataFolder, "WebBundles", "HelloWorld.data");
        Assert.IsTrue(Archive.IsWebBundle(new FileInfo(webBundlePath)));
    }

    [Test]
    public void IsWebBundle_False()
    {
        var nonWebBundlePath = Path.Combine(m_TestDataFolder, "WebBundles", "NotAWebBundle.txt");
        Assert.IsFalse(Archive.IsWebBundle(new FileInfo(nonWebBundlePath)));
    }

    [Test]
    public async Task ArchiveList_WebBundle_ListFilesCorrectly(
         [Values(
            "HelloWorld.data",
            "HelloWorld.data.gz",
            "HelloWorld.data.br"
        )] string bundlePath)
    {
        var path = Path.Combine(m_TestDataFolder, "WebBundles", bundlePath);
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "archive", "list", path }));

            var actualOutput = sw.ToString();

            // the expectedOutput has "lf" line endings but running on Windows
            // the console output will have "crlr"
            actualOutput = actualOutput.Replace("\r\n", "\n");

            var expectedOutput = (
@"data.unity3d
  Size: 253044

RuntimeInitializeOnLoads.json
  Size: 700

ScriptingAssemblies.json
  Size: 3060

boot.config
  Size: 93

Il2CppData/Metadata/global-metadata.dat
  Size: 1641180

Resources/unity_default_resources
  Size: 607376

"
            );

            Assert.AreEqual(expectedOutput, actualOutput);
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    [Test]
    public async Task ArchiveExtract_WebBundle_FileExtractedSuccessfully(
        [Values("", "-o archive", "--output-path archive")] string options,
        [Values("HelloWorld.data", "HelloWorld.data.gz", "HelloWorld.data.br")] string bundlePath)
    {
        var path = Path.Combine(m_TestDataFolder, "WebBundles", bundlePath);
        string[] expectedFiles = {
            "boot.config",
            "data.unity3d",
            "RuntimeInitializeOnLoads.json",
            "ScriptingAssemblies.json",
            Path.Combine("Il2CppData", "Metadata", "global-metadata.dat"),
            Path.Combine("Resources", "unity_default_resources"),
        };
        Assert.AreEqual(0, await Program.Main(new string[] { "archive", "extract", path }.Concat(options.Split(" ", StringSplitOptions.RemoveEmptyEntries)).ToArray()));
        foreach (var file in expectedFiles)
        {
            Assert.IsTrue(File.Exists(Path.Combine(m_TestOutputFolder, "archive", file)));
        }
    }
}
