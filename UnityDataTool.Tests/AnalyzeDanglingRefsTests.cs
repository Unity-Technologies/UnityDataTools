using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

// Tests the dangling_refs table/view (issue #85): references to objects whose serialized file was
// not part of the analyzed input get recorded instead of leaving unexplained gaps in the object id
// space. Uses the LeadingEdge AssetBundles fixture, where assetbundleroot.manifest declares
// dependencies on three other bundles, so analyzing assetbundleroot alone leaves cross-bundle
// references dangling; analyzing the whole set resolves everything except references into Unity's
// built-in resource files, which are never part of a bundle set.
public class AnalyzeDanglingRefsTests
{
    private string m_TestOutputFolder;
    private string m_AssetBundlesFolder;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        m_TestOutputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "dangling_refs_test_folder");
        m_AssetBundlesFolder = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "LeadingEdgeBuilds", "AssetBundles");
        Directory.CreateDirectory(m_TestOutputFolder);
        Directory.SetCurrentDirectory(m_TestOutputFolder);
    }

    [TearDown]
    public void Teardown()
    {
        SqliteConnection.ClearAllPools();
        new DirectoryInfo(m_TestOutputFolder).EnumerateFiles().ToList().ForEach(f => f.Delete());
    }

    // Every referenced object must resolve to exactly one of objects/dangling_refs: nothing left
    // unaccounted for, and no id in both tables. This is the core guarantee of the feature.
    private static void AssertReferencesFullyAccounted(SqliteConnection db)
    {
        SQLTestHelper.AssertQueryInt(db,
            @"SELECT COUNT(*) FROM refs r
              LEFT JOIN objects o       ON o.id = r.referenced_object
              LEFT JOIN dangling_refs d ON d.id = r.referenced_object
              WHERE o.id IS NULL AND d.id IS NULL",
            0, "every refs.referenced_object should resolve to an objects or dangling_refs row");
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM dangling_refs d INNER JOIN objects o ON o.id = d.id",
            0, "an id must not be in both objects and dangling_refs");
        // LFID 0 is never a real object; a dangling row with object_id 0 means a null PPtr (e.g. a
        // null m_GameObject) was mistakenly resolved to a phantom (file, 0) id.
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM dangling_refs WHERE object_id = 0",
            0, "dangling_refs should not contain phantom object_id 0 entries");
    }

    [Test]
    public async Task Analyze_PartialAssetBundleSet_RecordsCrossBundleDanglingRefs()
    {
        // Analyze only assetbundleroot; its references into the dependency bundles dangle.
        var bundlePath = Path.Combine(m_AssetBundlesFolder, "assetbundleroot");
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", bundlePath, "-o", databasePath }));
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        Assert.Greater(SQLTestHelper.QueryInt(db, "SELECT COUNT(*) FROM dangling_refs"), 0,
            "analyzing a single bundle should leave cross-bundle references dangling");
        Assert.Greater(SQLTestHelper.QueryInt(db, "SELECT COUNT(*) FROM dangling_refs_view"), 0,
            "the dangling references should be visible in dangling_refs_view");

        // The dangling targets live in files that were referenced but not analyzed, so those files
        // get a serialized_files row with no objects of their own.
        SQLTestHelper.AssertQueryInt(db,
            @"SELECT COUNT(*) FROM dangling_refs d
              WHERE d.serialized_file IN (SELECT serialized_file FROM objects)
                AND (SELECT COUNT(*) FROM refs r WHERE r.referenced_object = d.id) > 0",
            0, "referenced dangling targets should be in files that were not analyzed");

        AssertReferencesFullyAccounted(db);
    }

    [Test]
    public async Task Analyze_FullAssetBundleSet_ResolvesCrossBundleReferences()
    {
        // Analyzing the whole set brings the dependency bundles in, so no reference dangles into an
        // un-analyzed file. The only exceptions are the scene bundle's references into Unity's
        // built-in resource files, which are never part of a bundle set:
        // - 'unity default resources' (the RenderSettings spot cookie) ships complete with every
        //   player, so these references always resolve at runtime.
        // - 'unity_builtin_extra' (the Sprites/Default shader of the copied sprite material) is
        //   generated per player build and contains only the GraphicsSettings "Always Included
        //   Shaders", so a bundle's built-in shader reference resolves only if that list covers it
        //   (Sprites/Default is in it by default). A real AssetBundle limitation, unlike content
        //   directory builds, which copy the referenced unity_builtin_extra objects into the output.
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);

        Assert.AreEqual(0, await Program.Main(new string[] { "analyze", m_AssetBundlesFolder, "-o", databasePath }));
        using var db = SQLTestHelper.OpenDatabase(databasePath);

        SQLTestHelper.AssertQueryInt(db,
            @"SELECT COUNT(*) FROM dangling_refs_view
              WHERE target_serialized_file NOT IN ('unity_builtin_extra', 'unity default resources')",
            0, "analyzing the full set should resolve every cross-bundle reference except built-ins");

        AssertReferencesFullyAccounted(db);
    }
}
