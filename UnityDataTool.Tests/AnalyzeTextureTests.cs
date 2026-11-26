using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using NUnit.Framework;

namespace UnityDataTools.UnityDataTool.Tests;

#pragma warning disable NUnit2005, NUnit2006

// Tests the textures table and texture_view for Texture2D and Cubemap (issue #168) against the
// LeadingEdge reference builds. The Content Directory build contains the default skybox Cubemap.
public class AnalyzeTextureTests
{
    private string m_WorkFolder;
    private string m_AssetBundleDatabase;
    private string m_ContentDirectoryDatabase;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        m_WorkFolder = Path.Combine(TestContext.CurrentContext.TestDirectory, "texture_test_folder");
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
    public void Analyze_ContentDirectory_CubemapIsInTextureView()
    {
        using var db = SQLTestHelper.OpenDatabase(m_ContentDirectoryDatabase);
        var where = "FROM texture_view WHERE type = 'Cubemap' AND name = 'Default-Skybox-Cubemap'";

        SQLTestHelper.AssertQueryInt(db, $"SELECT COUNT(*) {where}", 1, "the skybox Cubemap should be listed once");
        SQLTestHelper.AssertQueryInt(db, $"SELECT image_count {where}", 6, "a Cubemap has six faces");
        SQLTestHelper.AssertQueryInt(db, $"SELECT width > 0 AND width = height {where}", 1, "faces are square");
        SQLTestHelper.AssertQueryInt(db, $"SELECT format IS NOT NULL {where}", 1, "format should be resolved to a name");
        SQLTestHelper.AssertQueryInt(db, $"SELECT mip_count >= 1 {where}", 1, "mip_count");
    }

    // The Cubemap's size includes its .resS data (#167), so it matches what stream_data records.
    [Test]
    public void Analyze_ContentDirectory_CubemapSizeCoversItsStreamData()
    {
        using var db = SQLTestHelper.OpenDatabase(m_ContentDirectoryDatabase);
        SQLTestHelper.AssertQueryInt(db,
            @"SELECT t.size >= s.size FROM texture_view t INNER JOIN stream_data s ON s.object = t.id
              WHERE t.type = 'Cubemap'",
            1, "Cubemap size should include its stream data");
    }

    [TestCase("assetbundles")]
    [TestCase("contentdirectory")]
    public void Analyze_ImageCountIsOneForTexture2D(string database)
    {
        using var db = SQLTestHelper.OpenDatabase(
            database == "assetbundles" ? m_AssetBundleDatabase : m_ContentDirectoryDatabase);
        SQLTestHelper.AssertQueryInt(db, "SELECT COUNT(*) FROM texture_view WHERE type = 'Texture2D'",
            SQLTestHelper.QueryInt(db, "SELECT COUNT(*) FROM objects o INNER JOIN types t ON o.type = t.id WHERE t.name = 'Texture2D'"),
            "every Texture2D should have a textures row");
        SQLTestHelper.AssertQueryInt(db,
            "SELECT COUNT(*) FROM texture_view WHERE type = 'Texture2D' AND image_count <> 1", 0,
            "a Texture2D has a single image");
    }

    // Only Cubemap shares the textures table; types that derive from Texture, not Texture2D, do not.
    [Test]
    public void Analyze_TextureViewHoldsOnlyTexture2DAndCubemap()
    {
        using var db = SQLTestHelper.OpenDatabase(m_ContentDirectoryDatabase);
        SQLTestHelper.AssertQueryString(db,
            "SELECT group_concat(type, ', ') FROM (SELECT DISTINCT type FROM texture_view ORDER BY type)",
            "Cubemap, Texture2D", "types in texture_view");
    }
}
