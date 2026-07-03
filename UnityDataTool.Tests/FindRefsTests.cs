using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

// Tests for the "find-refs" command, which traces reference chains from assets to a target object using the
// refs/object_view data produced by "analyze". They run against the LeadingEdge AssetBundle build output, which has
// well-known relationships (see UnityProjects/LeadingEdge/Assets/Editor/GenerateAssets.cs):
//
//   AssetBundleRoot (DirectScriptableObjectReference)
//     -> DirectAudioClipReference        references AudioClips "a" and "6"
//     -> SingleAudioClipDirectReference  references AudioClip "a" only
//     -> SerializationDemo
//
// So AudioClip "a" is referenced by two assets and "6" by one, giving deterministic chain counts to assert on.
public class FindRefsTests
{
    private string m_AssetBundlesPath;
    private string m_WorkFolder;
    private string m_DatabasePath;
    private string m_NoRefsDatabasePath;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        var testDataFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data");
        m_AssetBundlesPath = Path.Combine(testDataFolder, "LeadingEdgeBuilds", "AssetBundles");

        m_WorkFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "findrefs_test_folder");
        Directory.CreateDirectory(m_WorkFolder);

        m_DatabasePath = Path.Combine(m_WorkFolder, "refs.db");
        m_NoRefsDatabasePath = Path.Combine(m_WorkFolder, "norefs.db");

        // A database with references (used by most tests) and one built with --skip-references (empty refs table).
        Assert.AreEqual(0, await Program.Main(new[] { "analyze", m_AssetBundlesPath, "-o", m_DatabasePath }),
            "analyze should succeed on the LeadingEdge AssetBundle build");
        Assert.AreEqual(0, await Program.Main(new[] { "analyze", m_AssetBundlesPath, "-o", m_NoRefsDatabasePath, "-s" }),
            "analyze --skip-references should succeed");
    }

    [OneTimeTearDown]
    public void OneTimeTeardown()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(m_WorkFolder, true);
        }
        catch (Exception)
        {
            // Best effort cleanup; leftover files in the test output folder are harmless.
        }
    }

    // Runs find-refs with --stdout and captures everything it writes (both the reference chains and messages such as
    // "No object found!"), returning the exit code and the combined output.
    private async Task<(int exitCode, string output)> RunFindRefs(params string[] args)
    {
        return await RunFindRefsOn(m_DatabasePath, args);
    }

    private static async Task<(int exitCode, string output)> RunFindRefsOn(string databasePath, string[] args)
    {
        using var sw = new StringWriter();
        var currentOut = Console.Out;
        int exitCode;
        try
        {
            Console.SetOut(sw);
            var fullArgs = new string[args.Length + 3];
            fullArgs[0] = "find-refs";
            fullArgs[1] = databasePath;
            Array.Copy(args, 0, fullArgs, 2, args.Length);
            fullArgs[args.Length + 2] = "--stdout";
            exitCode = await Program.Main(fullArgs);
        }
        finally
        {
            Console.SetOut(currentOut);
        }

        return (exitCode, sw.ToString());
    }

    // Regression test for issue #72: the command previously threw "Connection string keyword 'version' is not
    // supported" when opening any analyze database.
    [Test]
    public async Task FindRefs_OpensAnalyzeDatabase_WithoutConnectionStringError()
    {
        var (exitCode, output) = await RunFindRefs("-n", "a", "-t", "AudioClip");

        Assert.AreEqual(0, exitCode);
        Assert.That(output, Does.Not.Contain("Error opening database"));
        Assert.That(output, Does.Contain("Reference chains to a"));
    }

    [Test]
    public async Task FindRefs_ByName_AudioClipSharedByTwoAssets_FindsBothChains()
    {
        var (exitCode, output) = await RunFindRefs("-n", "a", "-t", "AudioClip");

        Assert.AreEqual(0, exitCode);
        Assert.That(output, Does.Contain("Type:           AudioClip"));
        Assert.That(output, Does.Contain("Found 2 reference chain(s)."));

        // Both referencing assets and a dictionary value property path appear.
        Assert.That(output, Does.Contain("DirectAudioClipReference"));
        Assert.That(output, Does.Contain("SingleAudioClipDirectReference"));
        Assert.That(output, Does.Contain(".value"));
    }

    [Test]
    public async Task FindRefs_ByName_AudioClipReferencedOnce_FindsOneChain()
    {
        var (exitCode, output) = await RunFindRefs("-n", "6", "-t", "AudioClip");

        Assert.AreEqual(0, exitCode);
        Assert.That(output, Does.Contain("Found 1 reference chain(s)."));
        Assert.That(output, Does.Contain("DirectAudioClipReference"));
        Assert.That(output, Does.Not.Contain("SingleAudioClipDirectReference"));
    }

    // "SerializationDemo" is the name of both a MonoBehaviour (the ScriptableObject) and its MonoScript, so searching
    // by name alone matches two objects while adding the type narrows it to one.
    [Test]
    public async Task FindRefs_ByNameOnly_MatchesMonoBehaviourAndMonoScript()
    {
        var (exitCode, output) = await RunFindRefs("-n", "SerializationDemo");

        Assert.AreEqual(0, exitCode);
        Assert.That(output, Does.Contain("Type:           MonoBehaviour"));
        Assert.That(output, Does.Contain("Type:           MonoScript"));
    }

    [Test]
    public async Task FindRefs_ByNameAndType_NarrowsToSingleObject()
    {
        var (exitCode, output) = await RunFindRefs("-n", "SerializationDemo", "-t", "MonoBehaviour");

        Assert.AreEqual(0, exitCode);
        Assert.That(output, Does.Contain("Type:           MonoBehaviour"));
        Assert.That(output, Does.Not.Contain("Type:           MonoScript"));
        // Referenced from the AssetBundleRoot aggregator.
        Assert.That(output, Does.Contain("AssetBundleRoot"));
    }

    // ScriptableObjects are MonoBehaviours whose m_GameObject PPtr is 0; the chain output must render them without
    // throwing on the NULL game_object/script subquery results (a bug found while adding this coverage).
    [Test]
    public async Task FindRefs_ScriptableObjectChain_RendersScriptAndNoGameObject()
    {
        var (exitCode, output) = await RunFindRefs("-n", "a", "-t", "AudioClip");

        Assert.AreEqual(0, exitCode);
        Assert.That(output, Does.Contain("[Script = DirectAudioClipReference]"));
        Assert.That(output, Does.Not.Contain("[Component of"));
    }

    // find-refs reports the immediate containing asset and stops there, so searching for a leaf ScriptableObject
    // asset surfaces AssetBundleRoot (which references it) as a one-hop chain.
    [Test]
    public async Task FindRefs_LeafAsset_ReachesAssetBundleRoot()
    {
        var (exitCode, output) = await RunFindRefs("-n", "DirectAudioClipReference", "-t", "MonoBehaviour");

        Assert.AreEqual(0, exitCode);
        Assert.That(output, Does.Contain("Found 1 reference chain(s)."));
        Assert.That(output, Does.Contain("AssetBundleRoot"));
        Assert.That(output, Does.Contain("[Script = DirectScriptableObjectReference]"));
        Assert.That(output, Does.Contain("references.Array"));
    }

    // The traversal stops at the first asset reached going up the graph. AudioClip "a" is referenced directly by the
    // leaf ScriptableObjects, which are themselves assets, so the chains stop there and never reach AssetBundleRoot
    // further up (even though AssetBundleRoot transitively depends on the clip).
    [Test]
    public async Task FindRefs_AudioClip_StopsAtLeafAsset_DoesNotReachAssetBundleRoot()
    {
        var (exitCode, output) = await RunFindRefs("-n", "a", "-t", "AudioClip");

        Assert.AreEqual(0, exitCode);
        Assert.That(output, Does.Contain("DirectAudioClipReference"));
        Assert.That(output, Does.Not.Contain("AssetBundleRoot"));
    }

    [Test]
    public async Task FindRefs_ByObjectId_ProducesSameChainsAsByName()
    {
        // Look up the AudioClip "a" id, then confirm find-refs -i yields the same chains as find-refs -n.
        long audioClipId;
        using (var db = SQLTestHelper.OpenDatabase(m_DatabasePath))
        {
            audioClipId = QueryLong(db, "SELECT id FROM object_view WHERE name = 'a' AND type = 'AudioClip'");
        }

        var (exitCode, output) = await RunFindRefs("-i", audioClipId.ToString());

        Assert.AreEqual(0, exitCode);
        Assert.That(output, Does.Contain("Reference chains to a"));
        Assert.That(output, Does.Contain("Found 2 reference chain(s)."));
    }

    // --find-all only differs from the default when a single asset reaches the target via more than one property path.
    // In this test data the only such multi-path references come from AssetBundle bookkeeping objects (m_PreloadTable
    // plus m_Container), which are dropped because they are neither assets nor referenced by anything - so the
    // reference chains are identical and only the "Analyzed N object(s)" counter differs.
    [Test]
    public async Task FindRefs_FindAll_ProducesSameChainsAsDefault()
    {
        var (defExit, defOut) = await RunFindRefs("-n", "a", "-t", "AudioClip");
        var (allExit, allOut) = await RunFindRefs("-n", "a", "-t", "AudioClip", "-a");

        Assert.AreEqual(0, defExit);
        Assert.AreEqual(0, allExit);
        Assert.That(allOut, Does.Contain("Found 2 reference chain(s)."));

        string StripAnalyzedCount(string s) => Regex.Replace(s, @"Analyzed \d+ object\(s\)\.", "Analyzed N object(s).");
        Assert.AreEqual(StripAnalyzedCount(defOut), StripAnalyzedCount(allOut),
            "find-all should produce the same chains as the default for this data");
    }

    [Test]
    public async Task FindRefs_NonExistentObject_ReportsNotFound()
    {
        var (exitCode, output) = await RunFindRefs("-n", "ThisObjectDoesNotExist");

        Assert.AreNotEqual(0, exitCode);
        Assert.That(output, Does.Contain("No object found!"));
    }

    [Test]
    public async Task FindRefs_SkipReferencesDatabase_ReportsEmptyRefsTable()
    {
        var (exitCode, output) = await RunFindRefsOn(m_NoRefsDatabasePath, new[] { "-n", "a", "-t", "AudioClip" });

        Assert.AreNotEqual(0, exitCode);
        Assert.That(output, Does.Contain("'refs' table empty"));
    }

    [Test]
    public async Task FindRefs_StdoutAndOutputFile_MutuallyExclusive()
    {
        using var swOut = new StringWriter();
        using var swErr = new StringWriter();
        var currentOut = Console.Out;
        var currentErr = Console.Error;
        int exitCode;
        try
        {
            Console.SetOut(swOut);
            Console.SetError(swErr);
            exitCode = await Program.Main(new[]
                { "find-refs", m_DatabasePath, "-n", "a", "-t", "AudioClip", "--stdout", "-o", "refs.txt" });
        }
        finally
        {
            Console.SetOut(currentOut);
            Console.SetError(currentErr);
        }

        Assert.AreNotEqual(0, exitCode);
        Assert.That(swErr.ToString(), Does.Contain("--stdout and -o/--output-file are mutually exclusive."));
    }

    // Direct checks against the refs/object_view data structures that find-refs depends on, independent of the
    // command's output formatting. The refs table also holds AssetBundle bookkeeping references (m_PreloadTable,
    // m_Container), so counting the asset references means restricting to the MonoBehaviour (ScriptableObject)
    // referrers - which is what find-refs reports as chains.
    [Test]
    public void RefsTable_AudioClipReferenceCounts_MatchKnownRelationships()
    {
        using var db = SQLTestHelper.OpenDatabase(m_DatabasePath);

        var aId = QueryLong(db, "SELECT id FROM object_view WHERE name = 'a' AND type = 'AudioClip'");
        var sixId = QueryLong(db, "SELECT id FROM object_view WHERE name = '6' AND type = 'AudioClip'");

        SQLTestHelper.AssertQueryInt(db,
            $@"SELECT COUNT(*) FROM refs r JOIN object_view ov ON ov.id = r.object
               WHERE r.referenced_object = {aId} AND ov.type = 'MonoBehaviour'", 2,
            "AudioClip 'a' should be referenced by two ScriptableObjects");
        SQLTestHelper.AssertQueryInt(db,
            $@"SELECT COUNT(*) FROM refs r JOIN object_view ov ON ov.id = r.object
               WHERE r.referenced_object = {sixId} AND ov.type = 'MonoBehaviour'", 1,
            "AudioClip '6' should be referenced by one ScriptableObject");
    }

    [Test]
    public void RefsTable_DirectAudioClipReference_ReferencesBothClips()
    {
        using var db = SQLTestHelper.OpenDatabase(m_DatabasePath);

        // The DirectAudioClipReference asset references both AudioClips.
        var count = SQLTestHelper.QueryInt(db, @"
            SELECT COUNT(*) FROM refs
            WHERE object = (SELECT id FROM object_view WHERE name = 'DirectAudioClipReference' AND type = 'MonoBehaviour')
            AND referenced_object IN (SELECT id FROM object_view WHERE type = 'AudioClip')");
        Assert.AreEqual(2, count, "DirectAudioClipReference should reference both AudioClips");
    }

    // The refs table stores ids into property_names/property_types; refs_view rejoins them to expose the
    // original strings. Verify a known MonoBehaviour -> MonoScript reference surfaces correctly through the view.
    [Test]
    public void RefsView_ExposesPropertyPathAndTypeStrings()
    {
        using var db = SQLTestHelper.OpenDatabase(m_DatabasePath);

        var monoScriptRefs = SQLTestHelper.QueryInt(db, @"
            SELECT COUNT(*) FROM refs_view
            WHERE property_type = 'MonoScript' AND property_path = 'm_Script'
            AND object IN (SELECT id FROM object_view WHERE type = 'MonoBehaviour')");
        Assert.Greater(monoScriptRefs, 0,
            "MonoBehaviours should have an m_Script reference of type MonoScript visible through refs_view");
    }

    // Every id stored in refs must resolve through the lookup tables, and the lookup tables must not be larger
    // than the set of strings actually used (dedup should collapse repeats to one row each).
    [Test]
    public void RefsLookupTables_AreConsistentWithRefs()
    {
        using var db = SQLTestHelper.OpenDatabase(m_DatabasePath);

        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM refs WHERE property_path NOT IN (SELECT id FROM property_names)", 0,
            "Every refs.property_path id must exist in property_names");
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM refs WHERE property_type NOT IN (SELECT id FROM property_types)", 0,
            "Every refs.property_type id must exist in property_types");

        SQLTestHelper.AssertQueryInt(db,
            "SELECT (SELECT COUNT(DISTINCT property_path) FROM refs_view) - (SELECT COUNT(*) FROM property_names)", 0,
            "property_names should contain exactly the distinct property paths used by refs");
        SQLTestHelper.AssertQueryInt(db,
            "SELECT (SELECT COUNT(DISTINCT property_type) FROM refs_view) - (SELECT COUNT(*) FROM property_types)", 0,
            "property_types should contain exactly the distinct property types used by refs");
    }

    // find-refs must reject databases created before the normalized refs schema (user_version 0) with a clear
    // message rather than an obscure SQL error.
    [Test]
    public async Task FindRefs_UnsupportedSchemaVersion_FailsCleanly()
    {
        var oldSchemaDb = Path.Combine(m_WorkFolder, "old_schema.db");
        File.Copy(m_DatabasePath, oldSchemaDb, true);
        using (var db = SQLTestHelper.OpenDatabase(oldSchemaDb))
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = "PRAGMA user_version = 0";
            cmd.ExecuteNonQuery();
        }
        SqliteConnection.ClearAllPools();

        var (exitCode, output) = await RunFindRefsOn(oldSchemaDb, new[] { "-n", "a", "-t", "AudioClip" });

        Assert.AreNotEqual(0, exitCode);
        Assert.That(output, Does.Contain("unsupported schema version"));
    }

    private static long QueryLong(SqliteConnection db, string sql)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        using var reader = cmd.ExecuteReader();
        Assert.IsTrue(reader.Read(), $"Query returned no rows: {sql}");
        return reader.GetInt64(0);
    }
}
