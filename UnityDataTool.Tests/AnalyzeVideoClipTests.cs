using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

// Tests the video_clips table and video_clip_view (issue #165) against the LeadingEdge reference
// builds, which contain one small clip: Assets/Video/basic_clip_no_audio_160_100_1_1_50_100_H264.mp4
// (160x100, 50 fps, 100 frames, no audio). Its mp4 data is stored unchanged in a .resource file and
// counted in the VideoClip's size.
public class AnalyzeVideoClipTests
{
    const string ClipName = "basic_clip_no_audio_160_100_1_1_50_100_H264";
    const string OriginalPath = "Assets/Video/basic_clip_no_audio_160_100_1_1_50_100_H264.mp4";
    const int Mp4Size = 26720;

    private string m_WorkFolder;
    private string m_AssetBundleDatabase;
    private string m_ContentDirectoryDatabase;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        m_WorkFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "videoclip_test_folder");
        Directory.CreateDirectory(m_WorkFolder);
        m_AssetBundleDatabase = Path.Combine(m_WorkFolder, "assetbundles.db");
        m_ContentDirectoryDatabase = Path.Combine(m_WorkFolder, "contentdirectory.db");

        Assert.AreEqual(0, await Program.Main(new[]
            { "analyze", Path.Combine(LeadingEdgeTestData.Folder, "AssetBundles"), "-o", m_AssetBundleDatabase }));
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

    [Test]
    public void Analyze_AssetBundle_VideoClipViewHasClipProperties()
    {
        using var db = SQLTestHelper.OpenDatabase(m_AssetBundleDatabase);

        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM video_clip_view", 1, "the build has one VideoClip");
        SQLTestHelper.AssertQueryString(db, "SELECT name FROM video_clip_view", ClipName, "name");
        SQLTestHelper.AssertQueryString(db, "SELECT original_path FROM video_clip_view", OriginalPath, "original_path");
        SQLTestHelper.AssertQueryInt(db, "SELECT width FROM video_clip_view", 160, "width");
        SQLTestHelper.AssertQueryInt(db, "SELECT height FROM video_clip_view", 100, "height");
        SQLTestHelper.AssertQueryInt(db, "SELECT frame_rate = 50 FROM video_clip_view", 1, "frame_rate");
        SQLTestHelper.AssertQueryInt(db, "SELECT frame_count FROM video_clip_view", 100, "frame_count");
        SQLTestHelper.AssertQueryInt(db, "SELECT duration = 2 FROM video_clip_view", 1, "duration is frame_count / frame_rate");
        SQLTestHelper.AssertQueryInt(db, "SELECT audio_tracks FROM video_clip_view", 0, "the clip has no audio");
    }

    // The VideoClip object holds only the clip's description; the video itself is in the bundle's
    // .resource file. Its size is the serialized object (as listed in the SerializedFile) plus that data.
    [Test]
    public async Task Analyze_AssetBundle_VideoClipSizeIncludesVideoData()
    {
        var bundle = Path.Combine(LeadingEdgeTestData.Folder, "AssetBundles", ClipName.ToLowerInvariant());
        var (exitCode, stdOut) = await ConsoleTestHelper.RunCapturingStdOut(
            "serialized-file", "objectlist", bundle, "--format", "json");
        Assert.AreEqual(0, exitCode);
        var serializedSize = JsonDocument.Parse(stdOut).RootElement.EnumerateArray()
            .Single(o => o.GetProperty("typeName").GetString() == "VideoClip")
            .GetProperty("size").GetInt32();

        using var db = SQLTestHelper.OpenDatabase(m_AssetBundleDatabase);
        SQLTestHelper.AssertQueryInt(db, "SELECT size FROM video_clip_view", serializedSize + Mp4Size,
            "size should be the serialized VideoClip plus its video data");
    }

    [Test]
    public void Analyze_ContentDirectory_VideoClipViewHasClipProperties()
    {
        using var db = SQLTestHelper.OpenDatabase(m_ContentDirectoryDatabase);

        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM video_clip_view", 1, "the build has one VideoClip");
        SQLTestHelper.AssertQueryString(db, "SELECT original_path FROM video_clip_view", OriginalPath, "original_path");
        SQLTestHelper.AssertQueryInt(db, "SELECT width FROM video_clip_view", 160, "width");
        SQLTestHelper.AssertQueryInt(db, "SELECT frame_count FROM video_clip_view", 100, "frame_count");
    }

    // Unity's build report records the size of each type in the build, including the external data in
    // .resS / .resource files. The analyzed object sizes of the types that have such data must add up
    // to the same totals.
    [TestCase("AudioClip")]
    [TestCase("Texture2D")]
    [TestCase("VideoClip")]
    public void Analyze_ContentDirectory_SizeMatchesBuildReportTypeStats(string typeName)
    {
        using var db = SQLTestHelper.OpenDatabase(m_ContentDirectoryDatabase);

        SQLTestHelper.AssertQueryInt(db,
            $"SELECT resource_count > 0 FROM build_report_content_type_stats_view WHERE type_name = '{typeName}'", 1,
            $"the build report should list {typeName} with external data");
        var reportSize = SQLTestHelper.QueryInt(db,
            $"SELECT size FROM build_report_content_type_stats_view WHERE type_name = '{typeName}'");
        SQLTestHelper.AssertQueryInt(db,
            $@"SELECT SUM(size) FROM object_view
               WHERE type = '{typeName}' AND serialized_file NOT LIKE '%.buildreport'",
            reportSize, $"analyzed {typeName} sizes should match the build report");
    }
}
