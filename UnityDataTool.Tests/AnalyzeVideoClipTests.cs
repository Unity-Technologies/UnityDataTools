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
// builds, which contain two small clips in UnityProjects/LeadingEdge/Assets/Video. Each clip's mp4 is
// stored unchanged in a .resource file and counted in the VideoClip's size.
public class AnalyzeVideoClipTests
{
    // Properties of the clips, from the mp4 files themselves.
    public record Clip(string Name, int Width, int Height, int FrameRate, int FrameCount, int AudioTracks,
        string AudioLanguages, int Mp4Size)
    {
        public string OriginalPath => $"Assets/Video/{Name}.mp4";
        public override string ToString() => Name;
    }

    static readonly Clip NoAudio = new("basic_clip_no_audio_160_100_1_1_50_100_H264",
        160, 100, 50, 100, 0, "", 26720);

    // Synthetic test pattern with two sine-tone tracks: English stereo 48 kHz and French mono 44.1 kHz.
    static readonly Clip TwoLanguages = new("clip_audio_eng_stereo_fra_mono_160_100_1_1_25_50_H264",
        160, 100, 25, 50, 2, "en,fr", 34844);

    static readonly Clip[] Clips = { NoAudio, TwoLanguages };

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

    static void AssertClipProperties(SqliteConnection db, Clip clip)
    {
        var where = $"FROM video_clip_view WHERE name = '{clip.Name}'";
        SQLTestHelper.AssertQueryInt(db, $"SELECT COUNT(*) {where}", 1, "the clip should be in the view once");
        SQLTestHelper.AssertQueryString(db, $"SELECT original_path {where}", clip.OriginalPath, "original_path");
        SQLTestHelper.AssertQueryInt(db, $"SELECT width {where}", clip.Width, "width");
        SQLTestHelper.AssertQueryInt(db, $"SELECT height {where}", clip.Height, "height");
        SQLTestHelper.AssertQueryInt(db, $"SELECT frame_rate = {clip.FrameRate} {where}", 1, "frame_rate");
        SQLTestHelper.AssertQueryInt(db, $"SELECT frame_count {where}", clip.FrameCount, "frame_count");
        SQLTestHelper.AssertQueryInt(db, $"SELECT duration = {(double)clip.FrameCount / clip.FrameRate} {where}", 1,
            "duration is frame_count / frame_rate");
        SQLTestHelper.AssertQueryInt(db, $"SELECT audio_tracks {where}", clip.AudioTracks, "audio_tracks");
        SQLTestHelper.AssertQueryString(db, $"SELECT audio_languages {where}", clip.AudioLanguages, "audio_languages");
    }

    [Test]
    public void Analyze_AssetBundle_VideoClipViewHasClipProperties([ValueSource(nameof(Clips))] Clip clip)
    {
        using var db = SQLTestHelper.OpenDatabase(m_AssetBundleDatabase);
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM video_clip_view", Clips.Length, "one row per clip");
        AssertClipProperties(db, clip);
    }

    // The VideoClip object holds only the clip's description; the video (with its audio) is in the
    // bundle's .resource file. Its size is the serialized object (as listed in the SerializedFile) plus
    // that data.
    [Test]
    public async Task Analyze_AssetBundle_VideoClipSizeIncludesVideoData([ValueSource(nameof(Clips))] Clip clip)
    {
        var bundle = Path.Combine(LeadingEdgeTestData.Folder, "AssetBundles", clip.Name.ToLowerInvariant());
        var (exitCode, stdOut) = await ConsoleTestHelper.RunCapturingStdOut(
            "serialized-file", "objectlist", bundle, "--format", "json");
        Assert.AreEqual(0, exitCode);
        var serializedSize = JsonDocument.Parse(stdOut).RootElement.EnumerateArray()
            .Single(o => o.GetProperty("typeName").GetString() == "VideoClip")
            .GetProperty("size").GetInt32();

        using var db = SQLTestHelper.OpenDatabase(m_AssetBundleDatabase);
        SQLTestHelper.AssertQueryInt(db, $"SELECT size FROM video_clip_view WHERE name = '{clip.Name}'",
            serializedSize + clip.Mp4Size, "size should be the serialized VideoClip plus its video data");
    }

    [Test]
    public void Analyze_ContentDirectory_VideoClipViewHasClipProperties([ValueSource(nameof(Clips))] Clip clip)
    {
        using var db = SQLTestHelper.OpenDatabase(m_ContentDirectoryDatabase);
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM video_clip_view", Clips.Length, "one row per clip");
        AssertClipProperties(db, clip);
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
