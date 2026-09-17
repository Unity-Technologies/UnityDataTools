using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

// From SerializedFile version 25 the [SerializeReference] registry is a frame in the object's data
// that no TypeTree node describes, so analyze has to step over it to read the object's own fields
// and walk into it to find the references its instances hold.
public class AnalyzeV26Tests
{
    private string m_TestOutputFolder;
    private string m_ManagedReferencesBundle;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        m_TestOutputFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "v26_test_folder");
        m_ManagedReferencesBundle = Path.Combine(TestContext.CurrentContext.TestDirectory,
            "Data", "AssetBundleTypeTreeVariations", "v26", "managedreferences.bundle");
        Directory.CreateDirectory(m_TestOutputFolder);
        Directory.SetCurrentDirectory(m_TestOutputFolder);
    }

    [TearDown]
    public void Teardown()
    {
        SqliteConnection.ClearAllPools();
        var testDir = new DirectoryInfo(m_TestOutputFolder);
        testDir.EnumerateFiles().ToList().ForEach(f => f.Delete());
        testDir.EnumerateDirectories().ToList().ForEach(d => d.Delete(true));
    }

    async Task<SqliteConnection> Analyze(string input)
    {
        var databasePath = SQLTestHelper.GetDatabasePath(m_TestOutputFolder);
        Assert.AreEqual(0, await Program.Main(new[] { "analyze", input, "-o", databasePath }));
        return SQLTestHelper.OpenDatabase(databasePath);
    }

    [Test]
    public async Task Analyze_Version26_FindsReferencesInsideTheRegistry()
    {
        using var db = await Analyze(m_ManagedReferencesBundle);

        // A PPtr held by a [SerializeReference] instance, whose data is laid out by that instance's
        // own type tree rather than the object's.
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM refs_view WHERE property_path LIKE 'references.rid(%).data.material' AND property_type = '$Material'",
            1, "PPtr held by a [SerializeReference] instance");
    }

    // In a version 26 file a PPtr is usually reached through a shared subtree, which is a node with
    // a byte size and no children of its own - the shape of a basic type. A reader that takes it at
    // face value lands in the right place afterwards and simply never sees the reference, so the
    // reference count is what catches it. The version 22 build of the same scene says what to
    // expect.
    [TestCase("PlayerWithTypeTrees")]
    [TestCase("PlayerWithTypeTreesV26")]
    public async Task Analyze_FindsTheSceneReferences_WhicheverFormat(string folder)
    {
        var level0 = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data", folder, "level0");

        using var db = await Analyze(level0);

        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM objects", 7, "objects in the scene");
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM refs_view", 13, "references in the scene");

        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM refs_view WHERE property_path = 'm_GameObject'", 3,
            "component back-references to their GameObject");

        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM refs_view WHERE property_path LIKE 'm_Component%'", 3,
            "GameObject references to its components");
    }
}
