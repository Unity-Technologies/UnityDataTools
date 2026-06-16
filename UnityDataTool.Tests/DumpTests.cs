using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

public class DumpTests
{
    private string m_TestDataFolder;
    private string m_SerializedFilePath;
    private string m_ResourceFilePath;
    private string m_MultiSerializedFileArchivePath;
    private string m_NoTypeTreeSerializedFilePath;
    private string m_NoTypeTreeArchivePath;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        m_TestDataFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data");
        m_SerializedFilePath = Path.Combine(m_TestDataFolder, "PlayerWithTypeTrees", "level0");
        m_ResourceFilePath = Path.Combine(m_TestDataFolder, "PlayerWithTypeTrees", "sharedassets0.assets.resS");
        m_MultiSerializedFileArchivePath = Path.Combine(m_TestDataFolder, "PlayerDataCompressed", "data.unity3d");
        m_NoTypeTreeSerializedFilePath = Path.Combine(m_TestDataFolder, "PlayerNoTypeTree", "level0");
        m_NoTypeTreeArchivePath = Path.Combine(m_TestDataFolder, "AssetBundleTypeTreeVariations", "AssetBundle-NoTypeTree", "small.bundle");
    }

    [Test]
    public async Task Dump_Stdout_DefaultArgs_ContainsExternalReferences()
    {
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);
            Assert.AreEqual(0, await Program.Main(new string[] { "dump", m_SerializedFilePath, "--stdout" }));
        }
        finally
        {
            Console.SetOut(currentOut);
        }

        var output = sw.ToString();
        Assert.That(output, Does.Contain("External References"));
    }

    [Test]
    public async Task Dump_Stdout_FilterByObjectId_DumpsGameObject()
    {
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);
            Assert.AreEqual(0, await Program.Main(new string[] { "dump", m_SerializedFilePath, "--stdout", "-i", "1" }));
        }
        finally
        {
            Console.SetOut(currentOut);
        }

        var output = sw.ToString();
        Assert.That(output, Does.Contain("ID: 1 (ClassID: 1) GameObject"));
        Assert.That(output, Does.Contain("m_Name (string) RefHolder"));
    }

    [Test]
    public async Task Dump_Stdout_FilterByObjectId_DumpsRenderSettings()
    {
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);
            Assert.AreEqual(0, await Program.Main(new string[] { "dump", m_SerializedFilePath, "--stdout", "-i", "3" }));
        }
        finally
        {
            Console.SetOut(currentOut);
        }

        var output = sw.ToString();
        Assert.That(output, Does.Contain("(ClassID: 104)"));
        Assert.That(output, Does.Contain("m_FogColor (ColorRGBA)"));
    }

    [Test]
    public async Task Dump_Stdout_FilterByObjectId_DoesNotIncludeExternalReferences()
    {
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);
            Assert.AreEqual(0, await Program.Main(new string[] { "dump", m_SerializedFilePath, "--stdout", "-i", "1" }));
        }
        finally
        {
            Console.SetOut(currentOut);
        }

        var output = sw.ToString();
        Assert.That(output, Does.Not.Contain("External References"));
    }

    [Test]
    public async Task Dump_Stdout_FilterByType_DoesNotIncludeExternalReferences()
    {
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);
            Assert.AreEqual(0, await Program.Main(new string[] { "dump", m_SerializedFilePath, "--stdout", "-t", "RenderSettings" }));
        }
        finally
        {
            Console.SetOut(currentOut);
        }

        var output = sw.ToString();
        Assert.That(output, Does.Contain("(ClassID: 104)"));
        Assert.That(output, Does.Not.Contain("External References"));
    }

    [Test]
    public async Task Dump_Stdout_WithOutputPath_ReturnsError()
    {
        using var swOut = new StringWriter();
        using var swErr = new StringWriter();
        var currentOut = Console.Out;
        var currentErr = Console.Error;
        try
        {
            Console.SetOut(swOut);
            Console.SetError(swErr);
            Assert.AreNotEqual(0, await Program.Main(new string[] { "dump", m_SerializedFilePath, "--stdout", "-o", m_TestDataFolder }));
        }
        finally
        {
            Console.SetOut(currentOut);
            Console.SetError(currentErr);
        }

        Assert.That(swErr.ToString(), Does.Contain("--stdout and -o/--output-path are mutually exclusive."));
    }

    [Test]
    public async Task Dump_Stdout_MultipleSerializedFilesArchive_Refused()
    {
        using var swOut = new StringWriter();
        using var swErr = new StringWriter();
        var currentOut = Console.Out;
        var currentErr = Console.Error;
        try
        {
            Console.SetOut(swOut);
            Console.SetError(swErr);
            Assert.AreNotEqual(0, await Program.Main(new string[] { "dump", m_MultiSerializedFileArchivePath, "--stdout", "-t", "MonoBehaviour" }));
        }
        finally
        {
            Console.SetOut(currentOut);
            Console.SetError(currentErr);
        }

        var err = swErr.ToString();
        Assert.That(err, Does.Contain("--stdout cannot be used with an archive containing multiple SerializedFiles"));
        Assert.That(err, Does.Contain("(5 found)"));
    }

    [Test]
    public async Task Dump_Stdout_FilterByObjectId_NotFound_PrintsNotFoundMessage()
    {
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);
            Assert.AreEqual(0, await Program.Main(new string[] { "dump", m_SerializedFilePath, "--stdout", "-i", "99999999" }));
        }
        finally
        {
            Console.SetOut(currentOut);
        }

        var output = sw.ToString();
        Assert.That(output, Does.Contain("Object with ID 99999999 not found."));
    }

    [Test]
    public async Task Dump_Stdout_InvalidFileType_Fails()
    {
        using var swOut = new StringWriter();
        using var swErr = new StringWriter();
        var currentOut = Console.Out;
        var currentErr = Console.Error;
        try
        {
            Console.SetOut(swOut);
            Console.SetError(swErr);
            Assert.AreNotEqual(0, await Program.Main(new string[] { "dump", m_ResourceFilePath, "--stdout" }));
        }
        finally
        {
            Console.SetOut(currentOut);
            Console.SetError(currentErr);
        }

        Assert.That(swErr.ToString(), Does.Contain("does not appear to be a valid Unity SerializedFile or Unity Archive"));
    }

    [Test]
    public async Task Dump_NoTypeTreeSerializedFile_ReportsMissingTypeTreesWithoutCrashing()
    {
        using var swOut = new StringWriter();
        using var swErr = new StringWriter();
        var currentOut = Console.Out;
        var currentErr = Console.Error;
        try
        {
            Console.SetOut(swOut);
            Console.SetError(swErr);
            Assert.AreNotEqual(0, await Program.Main(new string[] { "dump", m_NoTypeTreeSerializedFilePath, "--stdout" }));
        }
        finally
        {
            Console.SetOut(currentOut);
            Console.SetError(currentErr);
        }

        var output = swOut.ToString() + swErr.ToString();
        Assert.That(output, Does.Contain("has no TypeTrees"), "Expected a clear missing-TypeTrees message");
        Assert.That(output, Does.Not.Contain("SerializedFileOpenException"), "Should not leak an exception/stack trace");
    }

    [Test]
    public async Task Dump_NoTypeTreeArchive_ReportsMissingTypeTreesWithoutCrashing()
    {
        using var swOut = new StringWriter();
        using var swErr = new StringWriter();
        var currentOut = Console.Out;
        var currentErr = Console.Error;
        try
        {
            Console.SetOut(swOut);
            Console.SetError(swErr);
            Assert.AreNotEqual(0, await Program.Main(new string[] { "dump", m_NoTypeTreeArchivePath, "--stdout" }));
        }
        finally
        {
            Console.SetOut(currentOut);
            Console.SetError(currentErr);
        }

        var output = swOut.ToString() + swErr.ToString();
        Assert.That(output, Does.Contain("has no TypeTrees"), "Expected a clear missing-TypeTrees message");
        Assert.That(output, Does.Not.Contain("SerializedFileOpenException"), "Should not leak an exception/stack trace");
    }
}
