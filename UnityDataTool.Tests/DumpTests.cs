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
    private string m_SerializationDemoBundlePath;
    private string m_ContentDirectoryBuildReportPath;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        m_TestDataFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data");
        m_SerializedFilePath = Path.Combine(m_TestDataFolder, "PlayerWithTypeTrees", "level0");
        m_ResourceFilePath = Path.Combine(m_TestDataFolder, "PlayerWithTypeTrees", "sharedassets0.assets.resS");
        m_MultiSerializedFileArchivePath = Path.Combine(m_TestDataFolder, "PlayerDataCompressed", "data.unity3d");
        m_NoTypeTreeSerializedFilePath = Path.Combine(m_TestDataFolder, "PlayerNoTypeTree", "level0");
        m_NoTypeTreeArchivePath = Path.Combine(m_TestDataFolder, "AssetBundleTypeTreeVariations", "AssetBundle-NoTypeTree", "small.bundle");
        m_SerializationDemoBundlePath = Path.Combine(m_TestDataFolder, "LeadingEdgeBuilds", "AssetBundles", "serializationdemo");
        m_ContentDirectoryBuildReportPath = Path.Combine(m_TestDataFolder, "LeadingEdgeBuilds", "BuildReport-ContentDirectory", "f64157fb08bb9f645971d39c1203bd03.buildreport");
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

    // Dumps the SerializationDemo ScriptableObject from the LeadingEdge AssetBundle build and confirms the
    // serialized field layout and default values (see UnityProjects/LeadingEdge/Assets/Scripts/SerializationDemo.cs).
    // Uses substring checks against the pseudo-YAML text output; once JSON output is supported this can parse and
    // assert more precisely.
    [Test]
    public async Task Dump_Stdout_AssetBundle_SerializationDemo_ContainsExpectedFields()
    {
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);
            Assert.AreEqual(0, await Program.Main(new string[] { "dump", m_SerializationDemoBundlePath, "--stdout", "--type", "MonoBehaviour" }));
        }
        finally
        {
            Console.SetOut(currentOut);
        }

        var output = sw.ToString();

        // The MonoBehaviour (ScriptableObject) and its SerializeReference-held data object.
        Assert.That(output, Does.Contain("(ClassID: 114) MonoBehaviour"));
        Assert.That(output, Does.Contain("m_Name (string) SerializationDemo"));
        Assert.That(output, Does.Contain("data (managedReference)"));
        Assert.That(output, Does.Contain("references (ManagedReferencesRegistry)"));
        Assert.That(output, Does.Contain("class (string) SerializationDemo/SerializedData"));

        // Scalar fields: name, serialized type and value.
        Assert.That(output, Does.Contain("longValue (SInt64) -1234567890123456789"));
        Assert.That(output, Does.Contain("ulongValue (UInt64) 12345678901234567890"));
        Assert.That(output, Does.Contain("intValue (int) -2000000000"));
        Assert.That(output, Does.Contain("uintValue (unsigned int) 4000000000"));
        Assert.That(output, Does.Contain("shortValue (SInt16) -12345"));
        Assert.That(output, Does.Contain("ushortValue (UInt16) 54321"));
        Assert.That(output, Does.Contain("signedCharValue (SInt8) -123"));
        Assert.That(output, Does.Contain("unsignedCharValue (UInt8) 234"));
        Assert.That(output, Does.Contain("boolValue (UInt8) 1"));
        Assert.That(output, Does.Contain("floatValue (float) 3.1415927"));
        Assert.That(output, Does.Contain("doubleValue (double) 2.718281828459045"));
        Assert.That(output, Does.Contain("charValue (UInt16) 90"));
        Assert.That(output, Does.Contain("stringValue (string) SerializationDemo string value"));

        // Int array of 512 values (0..511), above the large-array threshold so it is
        // summarized with a hash by default (CRC32 of the 2048 little-endian bytes).
        Assert.That(output, Does.Contain("Array<int>[512]"));
        Assert.That(output, Does.Contain("ArrayDataHash 6feca6e2"));
        Assert.That(output, Does.Not.Contain("293, 294, 295, 296,"));
    }

    // The expected bit patterns are the well-known IEEE 754 representations of the
    // SerializationDemo field values (also verified against python struct.pack).
    [Test]
    public async Task Dump_Stdout_HexFloat_PrintsBitExactRepresentation(
        [Values("-x", "--hexfloat")] string options)
    {
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);
            Assert.AreEqual(0, await Program.Main(new string[] { "dump", m_SerializationDemoBundlePath, "--stdout", "--type", "MonoBehaviour", options }));
        }
        finally
        {
            Console.SetOut(currentOut);
        }

        var output = sw.ToString();

        Assert.That(output, Does.Contain("floatValue (float) 3.1415927(0x40490fdb)"));
        Assert.That(output, Does.Contain("doubleValue (double) 2.718281828459045(0x4005bf0a8b145769)"));
    }

    [Test]
    public async Task Dump_Stdout_ShowLargeArrays_PrintsFullArrayContent()
    {
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);
            Assert.AreEqual(0, await Program.Main(new string[] { "dump", m_SerializationDemoBundlePath, "--stdout", "--type", "MonoBehaviour", "--show-large-arrays" }));
        }
        finally
        {
            Console.SetOut(currentOut);
        }

        var output = sw.ToString();

        // The 512-element int array (0..511) is fully dumped. Check the header and a slice of the sequence.
        Assert.That(output, Does.Contain("Array<int>[512]"));
        Assert.That(output, Does.Contain("293, 294, 295, 296,"));
        Assert.That(output, Does.Not.Contain("ArrayDataHash"));
    }

    // GUID and Hash128 fields are printed as a single hex string instead of their serialized fields.
    // The expected values can be verified independently: buildSessionGUID matches the build report
    // file name, and the Scene1 sourceAssetGUID matches Assets/Scenes/Scene1.unity.meta in the
    // LeadingEdge project.
    [Test]
    public async Task Dump_Stdout_BuildReport_PrintsGuidAndHash128AsHexStrings()
    {
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);
            Assert.AreEqual(0, await Program.Main(new string[] { "dump", m_ContentDirectoryBuildReportPath, "--stdout" }));
        }
        finally
        {
            Console.SetOut(currentOut);
        }

        var output = sw.ToString();

        Assert.That(output, Does.Contain("buildSessionGUID (GUID) f64157fb08bb9f645971d39c1203bd03"));
        Assert.That(output, Does.Contain("sourceAssetGUID (GUID) 162c015549f8733449ac70ae78ad3aa5"));
        Assert.That(output, Does.Contain("buildManifestHash (Hash128) baff06b928d147276f2245dd3b19216a"));

        // The individual fields of these compound types are no longer dumped.
        Assert.That(output, Does.Not.Contain("data[0] (unsigned int)"));
        Assert.That(output, Does.Not.Contain("bytes[0] (UInt8)"));
    }
}
