using Microsoft.Data.Sqlite;
using System.IO;
using UnityDataTools.FileSystem;
using UnityDataTools.TestCommon;

namespace UnityDataTools.UnityDataTool.Tests;

public static class ExpectedDataGenerator
{
    public static void Generate(Context context)
    {
        var expectedData = context.ExpectedData;
                
        UnityFileSystem.Init();
        using (var archive = UnityFileSystem.MountArchive(Path.Combine(context.UnityDataFolder, "assetbundle"), "/"))
        {
            expectedData.Add("NodeCount", archive.Nodes.Count);
                
            foreach (var n in archive.Nodes)
            {
                expectedData.Add(n.Path + "-Size", n.Size);
                expectedData.Add(n.Path + "-Flags", n.Flags);
            }
        }
        UnityFileSystem.Cleanup();
        
        Program.Main(new string[] { "analyze", Path.Combine(context.UnityDataFolder), "-r" });
        
        using var db = new SqliteConnection($"Data Source={Path.Combine(Directory.GetCurrentDirectory(), "database.db")};Version=3;New=True;Foreign Keys=False;");
        db.Open();

        using (var cmd = db.CreateCommand())
        {
            cmd.CommandText =
                @"SELECT 
                    (SELECT COUNT(*) FROM animation_clips),
                    (SELECT COUNT(*) FROM asset_bundles),
                    (SELECT COUNT(*) FROM assets),
                    (SELECT COUNT(*) FROM audio_clips),
                    (SELECT COUNT(*) FROM meshes),
                    (SELECT COUNT(*) FROM objects),
                    (SELECT COUNT(*) FROM refs),
                    (SELECT COUNT(*) FROM serialized_files),
                    (SELECT COUNT(*) FROM shader_subprograms),
                    (SELECT COUNT(*) FROM shaders),
                    (SELECT COUNT(*) FROM shader_keywords),
                    (SELECT COUNT(*) FROM shader_subprogram_keywords),
                    (SELECT COUNT(*) FROM textures),
                    (SELECT COUNT(*) FROM types)";

            using var reader = cmd.ExecuteReader();

            reader.Read();

            expectedData.Add("animation_clips_count", reader.GetInt32(0));
            expectedData.Add("asset_bundles_count", reader.GetInt32(1));
            expectedData.Add("assets_count", reader.GetInt32(2));
            expectedData.Add("audio_clips_count", reader.GetInt32(3));
            expectedData.Add("meshes_count", reader.GetInt32(4));
            expectedData.Add("objects_count", reader.GetInt32(5));
            expectedData.Add("refs_count", reader.GetInt32(6));
            expectedData.Add("serialized_files_count", reader.GetInt32(7));
            expectedData.Add("shader_subprograms_count", reader.GetInt32(8));
            expectedData.Add("shaders_count", reader.GetInt32(9));
            expectedData.Add("shader_keywords_count", reader.GetInt32(10));
            expectedData.Add("shader_subprogram_keywords_count", reader.GetInt32(11));
            expectedData.Add("textures_count", reader.GetInt32(12));
            expectedData.Add("types_count", reader.GetInt32(13));
        }
        
        var csprojFolder = Directory.GetParent(context.TestDataFolder).Parent.Parent.Parent.FullName;
        var outputFolder = Path.Combine(csprojFolder, "ExpectedData", context.UnityDataVersion);

        Directory.CreateDirectory(outputFolder);

        var dumpPath = Path.Combine(outputFolder, "dump");
        Directory.CreateDirectory(dumpPath);
        Program.Main(new string[] { "dump", Path.Combine(context.UnityDataFolder, "assetbundle"), "-o", dumpPath });
            
        dumpPath = Path.Combine(outputFolder, "dump-s");
        Directory.CreateDirectory(dumpPath);
        Program.Main(new string[] { "dump", Path.Combine(context.UnityDataFolder, "assetbundle"), "-o", dumpPath, "-s" });
            
        expectedData.Save(outputFolder);
    }
}
