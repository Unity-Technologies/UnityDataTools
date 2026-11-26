using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

// Tests the stream_data table and its views (issue #167): the data objects store in external .resS /
// .resource files, as recorded by their StreamingInfo / StreamedResource fields, and its inclusion in
// objects.size.
public class AnalyzeStreamDataTests
{
    const string CabName = "CAB-5d40f7cad7c871cf2ad2af19ac542994";

    private string m_WorkFolder;
    private string m_AssetBundleDatabase;
    private string m_ContentDirectoryDatabase;

    static string DataPath(params string[] parts) =>
        Path.Combine(new[] { TestContext.CurrentContext.TestDirectory, "Data" }.Concat(parts).ToArray());

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        m_WorkFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "streamdata_test_folder");
        Directory.CreateDirectory(m_WorkFolder);
        m_AssetBundleDatabase = Path.Combine(m_WorkFolder, "assetbundles.db");
        m_ContentDirectoryDatabase = Path.Combine(m_WorkFolder, "contentdirectory.db");

        Assert.AreEqual(0, await Program.Main(new[]
            { "analyze", DataPath("AssetBundles", "2019.4.0f1"), "-o", m_AssetBundleDatabase }));
        Assert.AreEqual(0, await Program.Main(new[]
        {
            "analyze", LeadingEdgeTestData.ContentDirectory, LeadingEdgeTestData.ContentLayoutPath,
            LeadingEdgeTestData.ContentDirectoryBuildReportPath, "-o", m_ContentDirectoryDatabase
        }));
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

    // The bundle's Meshes and Texture share one .resS file, one after the other, and its AudioClip has
    // a .resource file.
    [Test]
    public void Analyze_AssetBundle_RecordsEachObjectsStreamData()
    {
        using var db = SQLTestHelper.OpenDatabase(m_AssetBundleDatabase);

        SQLTestHelper.AssertQueryString(db,
            $@"SELECT group_concat(type || ' ' || name || ' ' || offset || ' ' || size, ', ') FROM
               (SELECT * FROM stream_data_view WHERE path = 'archive:/{CabName}/{CabName}.resS' ORDER BY offset)",
            "Mesh Lame 0 5472, Texture2D wood 5472 2796216, Mesh Garde 2801688 19128, " +
            "Mesh Pommeau 2820816 8496, Mesh Manche 2829312 4536",
            "objects in the .resS file, by offset");
        SQLTestHelper.AssertQueryString(db,
            $"SELECT type || ' ' || name || ' ' || offset || ' ' || size FROM stream_data_view WHERE filename = '{CabName}.resource'",
            "AudioClip blip 0 5248", "object in the .resource file");
        SQLTestHelper.AssertQueryString(db,
            @"SELECT group_concat(type || ' ' || property_path, ', ') FROM
              (SELECT DISTINCT type, property_path FROM stream_data_view ORDER BY type)",
            "AudioClip m_Resource, Mesh m_StreamData, Texture2D m_StreamData", "the field of each type");

        SQLTestHelper.AssertQueryString(db,
            "SELECT group_concat(filename || ' ' || objects || ' ' || size, ', ') FROM stream_data_files_view",
            $"{CabName}.resS 5 2833848, {CabName}.resource 1 5248", "data files, largest first");
    }

    // An object's size is its serialized size, as listed in the SerializedFile, plus its stream data.
    [Test]
    public async Task Analyze_AssetBundle_SizeIncludesStreamData()
    {
        var (exitCode, stdOut) = await ConsoleTestHelper.RunCapturingStdOut(
            "serialized-file", "objectlist", DataPath("AssetBundles", "2019.4.0f1", "assetbundle"), "--format", "json");
        Assert.AreEqual(0, exitCode);
        var serializedSizes = JsonDocument.Parse(stdOut).RootElement.EnumerateArray()
            .ToDictionary(o => o.GetProperty("id").GetInt64(), o => o.GetProperty("size").GetInt64());

        using var db = SQLTestHelper.OpenDatabase(m_AssetBundleDatabase);
        using var command = db.CreateCommand();
        command.CommandText = @"SELECT o.object_id, o.size, s.size FROM stream_data s
                                INNER JOIN objects o ON o.id = s.object";
        using var reader = command.ExecuteReader();
        var checkedObjects = 0;
        while (reader.Read())
        {
            var objectId = reader.GetInt64(0);
            Assert.AreEqual(serializedSizes[objectId] + reader.GetInt64(2), reader.GetInt64(1),
                $"size of object {objectId}");
            checkedObjects++;
        }

        Assert.AreEqual(6, checkedObjects);
    }

    // Each object's data is a separate range of its file.
    [TestCase("2019.4.0f1")]
    [TestCase("2020.3.0f1")]
    [TestCase("2021.3.0f1")]
    [TestCase("2022.1.20f1")]
    [TestCase("2023.1.0a16")]
    public async Task Analyze_AssetBundle_StreamDataDoesNotOverlap(string version)
    {
        var databasePath = Path.Combine(m_WorkFolder, $"assetbundles-{version}.db");
        Assert.AreEqual(0, await Program.Main(new[]
            { "analyze", DataPath("AssetBundles", version), "-o", databasePath }));

        using var db = SQLTestHelper.OpenDatabase(databasePath);
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) > 1 FROM stream_data WHERE path LIKE '%.resS'", 1,
            "the .resS file holds several objects");
        SQLTestHelper.AssertQueryInt(db,
            @"SELECT COUNT(*) FROM stream_data a INNER JOIN stream_data b ON a.path = b.path AND a.rowid < b.rowid
              WHERE a.offset < b.offset + b.size AND b.offset < a.offset + a.size",
            0, "overlapping data ranges");
    }

    // In a Content Directory each object's data is a whole content-addressed file, which ContentLayout.json
    // lists as an artifact referenced by the object's content file.
    [Test]
    public void Analyze_ContentDirectory_StreamDataMatchesContentLayout()
    {
        using var db = SQLTestHelper.OpenDatabase(m_ContentDirectoryDatabase);

        SQLTestHelper.AssertQueryString(db,
            @"SELECT group_concat(type || ' ' || property_path, ', ') FROM
              (SELECT DISTINCT type, property_path FROM stream_data_view ORDER BY type)",
            "AudioClip m_Resource, Cubemap m_StreamData, Texture2D m_StreamData, VideoClip m_ExternalResources",
            "the field of each type");
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM stream_data WHERE path NOT LIKE 'cah:/%' OR offset <> 0", 0,
            "every path is content-addressed with offset 0");
        SQLTestHelper.AssertQueryInt(db,
            @"SELECT COUNT(*) FROM stream_data s INNER JOIN objects o ON o.id = s.object
              WHERE NOT EXISTS (
                  SELECT 1 FROM content_layout_serialized_files f
                  INNER JOIN content_layout_artifact_references r ON r.artifact_index = f.artifact_index
                  INNER JOIN content_layout_binary_artifacts a ON a.artifact_index = r.referenced_artifact_index
                  WHERE f.serialized_file = o.serialized_file AND s.path = 'cah:/' || a.content_hash
                        AND s.size = a.size)",
            0, "stream data that is not a same-sized artifact referenced by the object's content file");
        SQLTestHelper.AssertQueryInt(db,
            @"SELECT COUNT(*) FROM content_layout_binary_artifacts a
              WHERE a.category IN ('texture', 'mesh', 'audio', 'video')
                AND NOT EXISTS (SELECT 1 FROM stream_data s WHERE s.path = 'cah:/' || a.content_hash)",
            0, "data artifacts that no object references");
        SQLTestHelper.AssertQueryInt(db,
            "SELECT SUM(size) FROM stream_data_files_view",
            SQLTestHelper.QueryInt(db,
                "SELECT SUM(size) FROM content_layout_binary_artifacts WHERE category IN ('texture', 'mesh', 'audio', 'video')"),
            "total size of the data files");
    }

    // Unity's build report records the size of each type in the build, including the external data in
    // .resS / .resource files. The analyzed object sizes must add up to the same totals, including for
    // types analyze has no specific handling for, such as Cubemap.
    [Test]
    public void Analyze_ContentDirectory_SizeMatchesBuildReportTypeStats()
    {
        using var db = SQLTestHelper.OpenDatabase(m_ContentDirectoryDatabase);

        var types = new List<string>();
        using (var command = db.CreateCommand())
        {
            command.CommandText =
                "SELECT type_name FROM build_report_content_type_stats_view WHERE resource_count > 0 ORDER BY type_name";
            using var reader = command.ExecuteReader();
            while (reader.Read())
                types.Add(reader.GetString(0));
        }

        CollectionAssert.IsSupersetOf(types, new[] { "AudioClip", "Cubemap", "Texture2D", "VideoClip" });
        foreach (var typeName in types)
        {
            var reportSize = SQLTestHelper.QueryInt(db,
                $"SELECT size FROM build_report_content_type_stats_view WHERE type_name = '{typeName}'");
            SQLTestHelper.AssertQueryInt(db,
                $@"SELECT SUM(size) FROM object_view
                   WHERE type = '{typeName}' AND serialized_file NOT LIKE '%.buildreport'",
                reportSize, $"analyzed {typeName} sizes should match the build report");
        }
    }

    // A Player build names the data file directly. The stream data is recorded, and counted in the
    // size, whatever the skip options.
    [TestCase(false)]
    [TestCase(true)]
    public async Task Analyze_Player_RecordsStreamData(bool skipReferencesAndCrc)
    {
        var databasePath = Path.Combine(m_WorkFolder, $"player-{skipReferencesAndCrc}.db");
        var args = new List<string> { "analyze", DataPath("PlayerWithTypeTrees", "sharedassets0.assets"), "-o", databasePath };
        if (skipReferencesAndCrc)
            args.AddRange(new[] { "--skip-references", "--skip-crc" });
        Assert.AreEqual(0, await Program.Main(args.ToArray()));

        using var db = SQLTestHelper.OpenDatabase(databasePath);
        SQLTestHelper.AssertQueryString(db,
            "SELECT path || ' ' || filename || ' ' || offset || ' ' || size FROM stream_data_view WHERE type = 'Cubemap'",
            "sharedassets0.assets.resS sharedassets0.assets.resS 0 524280", "the Cubemap's data");
        SQLTestHelper.AssertQueryInt(db, "SELECT size FROM object_view WHERE type = 'Cubemap'", 240 + 524280,
            "the Cubemap's serialized size plus its data");
    }
}
