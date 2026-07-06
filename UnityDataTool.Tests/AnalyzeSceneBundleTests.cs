using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

// Tests that analyze represents scenes in a scene AssetBundle correctly (issue #81): a synthetic
// Scene object is created, its assetbundle_assets row resolves (no dangling reference), and its
// dependencies show up in preload_dependencies_view.
//
// The available scene-bundle test data (LegacyFormats/AssetBundles/v22.scene.bundle) is a
// BuildPipeline.BuildAssetBundles bundle ("BuildPlayer-Scene1"). The Scriptable Build Pipeline /
// Addressables path shares the same scene machinery but is exercised manually against large
// Addressables builds, as the repo has no small SBP scene-bundle fixture.
public class AnalyzeSceneBundleTests
{
    private string m_TestOutputFolder;
    private string m_SceneBundlePath;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        m_TestOutputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "scene_bundle_test_folder");
        m_SceneBundlePath = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "LegacyFormats", "AssetBundles", "v22.scene.bundle");
        Directory.CreateDirectory(m_TestOutputFolder);
        Directory.SetCurrentDirectory(m_TestOutputFolder);
    }

    [TearDown]
    public void Teardown()
    {
        SqliteConnection.ClearAllPools();
        new DirectoryInfo(m_TestOutputFolder).EnumerateFiles().ToList().ForEach(f => f.Delete());
    }

    [Test]
    public async Task Analyze_SceneBundle_SceneResolvesWithDependencies()
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", m_SceneBundlePath, "-o", databasePath }));
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        // A synthetic Scene object is created for the scene, and its assetbundle_assets row resolves
        // to it (no dangling reference), so the scene appears in assetbundle_asset_view.
        Assert.Greater(SQLTestHelper.QueryInt(db, "SELECT COUNT(*) FROM objects WHERE type = -1"), 0,
            "expected at least one synthetic Scene object");
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM assetbundle_assets a LEFT JOIN objects o ON o.id = a.object WHERE o.id IS NULL",
            0, "no assetbundle_assets row should be dangling (issue #81)");
        Assert.Greater(SQLTestHelper.QueryInt(db, "SELECT COUNT(*) FROM assetbundle_asset_view WHERE type = 'Scene'"), 0,
            "the scene should appear in assetbundle_asset_view");

        // The scene's dependencies are attached to the scene object and show up in the view.
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM preload_dependencies WHERE object = -1",
            0, "preload_dependencies.object should never be -1 (issue #81)");
        Assert.Greater(SQLTestHelper.QueryInt(db, "SELECT COUNT(*) FROM preload_dependencies_view WHERE type = 'Scene'"), 0,
            "the scene should have dependencies in preload_dependencies_view");
    }
}
