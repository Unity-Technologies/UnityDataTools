using System;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityDataTools.FileSystem;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

public class ArchiveTests
{
    private string m_TestOutputFolder;
    private string m_TestDataFolder;
    private string m_ArchivePath;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        m_TestOutputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "test_folder");
        m_TestDataFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data");
        m_ArchivePath = Path.Combine(m_TestDataFolder, "AssetBundles", "2023.1.0a16", "scenes");
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
    public async Task ArchiveList_TextFormat()
    {
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "archive", "list", m_ArchivePath }));

            var actualOutput = sw.ToString().Replace("\r\n", "\n");

            var expectedOutput =
@"BuildPlayer-SampleScene.sharedAssets
  Size: 90732
  Flags: SerializedFile

BuildPlayer-SampleScene
  Size: 153352
  Flags: SerializedFile

BuildPlayer-OtherScene.sharedAssets
  Size: 136744
  Flags: SerializedFile

BuildPlayer-OtherScene
  Size: 158340
  Flags: SerializedFile

";

            Assert.AreEqual(expectedOutput, actualOutput);
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    [Test]
    public async Task ArchiveList_JsonFormat()
    {
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);

            Assert.AreEqual(0, await Program.Main(new string[] { "archive", "list", m_ArchivePath, "-f", "Json" }));

            var output = sw.ToString();
            var jsonArray = JsonDocument.Parse(output).RootElement;
            Assert.AreEqual(JsonValueKind.Array, jsonArray.ValueKind);
            Assert.AreEqual(4, jsonArray.GetArrayLength());

            foreach (var element in jsonArray.EnumerateArray())
            {
                Assert.IsTrue(element.TryGetProperty("path", out _));
                Assert.IsTrue(element.TryGetProperty("size", out _));
                Assert.IsTrue(element.TryGetProperty("flags", out _));
                Assert.AreEqual("SerializedFile", element.GetProperty("flags").GetString());
            }

            Assert.AreEqual("BuildPlayer-SampleScene.sharedAssets", jsonArray[0].GetProperty("path").GetString());
            Assert.AreEqual(90732, jsonArray[0].GetProperty("size").GetInt64());
        }
        finally
        {
            Console.SetOut(currentOut);
        }
    }

    [Test]
    public async Task ArchiveExtract_FilesExtractedSuccessfully()
    {
        Assert.AreEqual(0, await Program.Main(new string[] { "archive", "extract", m_ArchivePath }));

        string[] expectedFiles =
        {
            "BuildPlayer-SampleScene.sharedAssets",
            "BuildPlayer-SampleScene",
            "BuildPlayer-OtherScene.sharedAssets",
            "BuildPlayer-OtherScene",
        };

        foreach (var file in expectedFiles)
        {
            Assert.IsTrue(File.Exists(Path.Combine(m_TestOutputFolder, "archive", file)), $"Expected file not found: {file}");
        }

        // Verify extracted file size matches the size reported by the list command.
        var extractedFile = new FileInfo(Path.Combine(m_TestOutputFolder, "archive", "BuildPlayer-SampleScene.sharedAssets"));
        Assert.AreEqual(90732, extractedFile.Length);
    }
}
