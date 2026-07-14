using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

// Sanity tests that the scenes are present in the LeadingEdge AssetBundle build output (issue #97):
// the scenes bundle contains a SerializedFile per scene plus its sharedAssets file, the scene files
// hold the expected objects, and the shared GreenStatic texture is included exactly once.
// The ContentDirectory build output gets deeper coverage once ContentLayout.json support lands.
public class SceneBundleContentTests
{
    private string m_TestOutputFolder;
    private string m_ScenesBundlePath;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        m_TestOutputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "scene_content_test_folder");
        m_ScenesBundlePath = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "LeadingEdgeBuilds", "AssetBundles", "scenes");
        Directory.CreateDirectory(m_TestOutputFolder);
        Directory.SetCurrentDirectory(m_TestOutputFolder);
    }

    [TearDown]
    public void Teardown()
    {
        var testDir = new DirectoryInfo(m_TestOutputFolder);
        testDir.EnumerateFiles().ToList().ForEach(f => f.Delete());
        testDir.EnumerateDirectories().ToList().ForEach(d => d.Delete(true));
    }

    private static async Task<string> RunAndCaptureStdout(params string[] args)
    {
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        try
        {
            Console.SetOut(sw);
            Assert.AreEqual(0, await Program.Main(args), $"command failed: {string.Join(' ', args)}");
        }
        finally
        {
            Console.SetOut(currentOut);
        }

        return sw.ToString();
    }

    [Test]
    public async Task ScenesBundle_ContainsOneSerializedFilePerScene()
    {
        var output = await RunAndCaptureStdout("archive", "list", m_ScenesBundlePath);

        foreach (var name in new[]
                 {
                     "BuildPlayer-Scene1", "BuildPlayer-Scene1.sharedAssets",
                     "BuildPlayer-Scene2", "BuildPlayer-Scene2.sharedAssets"
                 })
        {
            Assert.That(output, Does.Contain(name), $"scenes bundle should contain {name}");
        }
    }

    [Test]
    public async Task ScenesBundle_SceneFilesContainSceneObjects_TextureIncludedOnce()
    {
        Assert.AreEqual(0, await Program.Main(new[] { "archive", "extract", m_ScenesBundlePath }));
        var extractedFolder = Path.Combine(m_TestOutputFolder, "archive");

        // Each scene file holds the scene's own objects: the generated GameObject with its
        // SpriteRenderer, plus the scene settings objects.
        foreach (var sceneFile in new[] { "BuildPlayer-Scene1", "BuildPlayer-Scene2" })
        {
            var objectList = await RunAndCaptureStdout(
                "serialized-file", "objectlist", Path.Combine(extractedFolder, sceneFile));
            Assert.That(objectList, Does.Contain("GameObject"), $"{sceneFile} should contain a GameObject");
            Assert.That(objectList, Does.Contain("SpriteRenderer"), $"{sceneFile} should contain a SpriteRenderer");
            Assert.That(objectList, Does.Contain("RenderSettings"), $"{sceneFile} should contain scene settings");
        }

        // Both scenes show the same texture, so it is included exactly once in the bundle:
        // the Texture2D and its Sprite land in one sharedAssets file and the other scene
        // references them from there.
        var textureCount = 0;
        foreach (var sharedFile in new[] { "BuildPlayer-Scene1.sharedAssets", "BuildPlayer-Scene2.sharedAssets" })
        {
            var objectList = await RunAndCaptureStdout(
                "serialized-file", "objectlist", Path.Combine(extractedFolder, sharedFile));
            textureCount += objectList.Split('\n').Count(line => line.Contains("Texture2D"));
        }

        Assert.AreEqual(1, textureCount, "the shared texture should be included exactly once in the bundle");
    }
}
