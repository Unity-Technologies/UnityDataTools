using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.Sqlite;
using UnityDataTools.Analyzer.SQLite.Commands.ContentLayout;
using UnityDataTools.Analyzer.Util;
using UnityDataTools.Models;

namespace UnityDataTools.Analyzer.SQLite.Writers
{
    // Populates the content_layout* tables from a ContentLayout.json (see
    // Documentation/contentlayout.md). The tables mirror the json structure, with two adjustments
    // that make the data natural to query: the top-level RootAssets list is folded into the
    // is_root_asset flag, and the json's sentinel values (-1 for "dropped from build", missing
    // ContentHash for built-ins) are stored as NULL.
    internal class ContentLayoutSQLWriter : IDisposable
    {
        private AddContentLayout m_AddContentLayout = new();
        private AddContentLayoutSerializedFile m_AddSerializedFile = new();
        private AddContentLayoutSourceAsset m_AddSourceAsset = new();
        private AddContentLayoutSerializedFileDependency m_AddSerializedFileDependency = new();
        private AddContentLayoutLoadableDependency m_AddLoadableDependency = new();
        private AddContentLayoutLoadableSceneDependency m_AddLoadableSceneDependency = new();
        private AddContentLayoutLoadableObject m_AddLoadableObject = new();
        private AddContentLayoutLoadableScene m_AddLoadableScene = new();
        private AddContentLayoutBinaryArtifact m_AddBinaryArtifact = new();
        private AddContentLayoutArtifactReference m_AddArtifactReference = new();

        private bool m_Initialized;
        private SqliteConnection m_Database;

        // Shared with the serialized-file analysis (see AnalyzerTool): file ids must agree
        // between the layout and the analyzed content, and the dependency map is what resolves
        // the external references of the analyzed .cf files.
        private IdProvider<string> m_SerializedFileIdProvider;
        private ContentFileDependencyMap m_ContentFileDependencies;

        // The layout files that can have a .cf file on disk, kept for LinkSerializedFiles.
        private List<(int Index, string ContentHash)> m_ImportedFiles = new();

        public ContentLayoutSQLWriter(SqliteConnection database,
            IdProvider<string> serializedFileIdProvider, ContentFileDependencyMap contentFileDependencies)
        {
            m_Database = database;
            m_SerializedFileIdProvider = serializedFileIdProvider;
            m_ContentFileDependencies = contentFileDependencies;
        }

        // Creates the content_layout tables and views. Called lazily on the first import so that
        // analyzing other content (AssetBundles, Player builds) doesn't create empty tables.
        public void Init()
        {
            if (m_Initialized)
                return;

            m_Initialized = true;

            m_AddContentLayout.CreateCommand(m_Database);
            m_AddSerializedFile.CreateCommand(m_Database);
            m_AddSourceAsset.CreateCommand(m_Database);
            m_AddSerializedFileDependency.CreateCommand(m_Database);
            m_AddLoadableDependency.CreateCommand(m_Database);
            m_AddLoadableSceneDependency.CreateCommand(m_Database);
            m_AddLoadableObject.CreateCommand(m_Database);
            m_AddLoadableScene.CreateCommand(m_Database);
            m_AddBinaryArtifact.CreateCommand(m_Database);
            m_AddArtifactReference.CreateCommand(m_Database);

            ExecuteDDL(Properties.Resources.ContentLayoutViews);
        }

        public void WriteContentLayout(string filename, ContentLayout layout)
        {
            using var transaction = m_Database.BeginTransaction();
            SetTransaction(transaction);

            try
            {
                m_AddContentLayout.SetValue("id", 0);
                m_AddContentLayout.SetValue("name", filename);
                m_AddContentLayout.SetValue("version", layout.Version);
                m_AddContentLayout.SetValue("build_manifest_hash", layout.BuildManifestHash);
                m_AddContentLayout.ExecuteNonQuery();

                foreach (var file in layout.SerializedFiles)
                {
                    m_AddSerializedFile.SetValue("file_index", file.Index);
                    m_AddSerializedFile.SetValue("cfid", file.ID);
                    m_AddSerializedFile.SetValue("is_builtin", file.IsBuiltIn ? 1 : 0);
                    m_AddSerializedFile.SetValue("content_hash",
                        string.IsNullOrEmpty(file.ContentHash) ? null : file.ContentHash);
                    // Filled in by LinkSerializedFiles when the analyzed input also contains the
                    // build content.
                    m_AddSerializedFile.SetValue("serialized_file", null);
                    m_AddSerializedFile.ExecuteNonQuery();

                    if (!string.IsNullOrEmpty(file.ContentHash))
                    {
                        m_ImportedFiles.Add((file.Index, file.ContentHash));
                    }

                    // Empty arrays can be omitted from the json, leaving the fields null.
                    foreach (var assetPath in file.SourceAssets ?? [])
                    {
                        m_AddSourceAsset.SetValue("serialized_file_index", file.Index);
                        m_AddSourceAsset.SetValue("asset_path", assetPath);
                        m_AddSourceAsset.ExecuteNonQuery();
                    }

                    // The array order is significant (a PPtr's m_FileID resolves positionally),
                    // so it is preserved in the 1-based position column.
                    var dependencies = file.SerializedFileDependencies ?? [];
                    for (int i = 0; i < dependencies.Length; ++i)
                    {
                        m_AddSerializedFileDependency.SetValue("serialized_file_index", file.Index);
                        m_AddSerializedFileDependency.SetValue("position", i + 1);
                        m_AddSerializedFileDependency.SetValue("dependency_index", dependencies[i]);
                        m_AddSerializedFileDependency.ExecuteNonQuery();
                    }

                    foreach (var objectIdHash in file.LoadableDependencies ?? [])
                    {
                        m_AddLoadableDependency.SetValue("serialized_file_index", file.Index);
                        m_AddLoadableDependency.SetValue("object_id_hash", objectIdHash);
                        m_AddLoadableDependency.ExecuteNonQuery();
                    }

                    foreach (var scenePath in file.LoadableSceneDependencies ?? [])
                    {
                        m_AddLoadableSceneDependency.SetValue("serialized_file_index", file.Index);
                        m_AddLoadableSceneDependency.SetValue("scene_path", scenePath);
                        m_AddLoadableSceneDependency.ExecuteNonQuery();
                    }
                }

                var rootAssets = new HashSet<string>(layout.RootAssets ?? []);

                foreach (var loadable in layout.LoadableObjectIds ?? [])
                {
                    m_AddLoadableObject.SetValue("object_id_hash", loadable.ObjectIdHash);
                    m_AddLoadableObject.SetValue("guid", loadable.GUID);
                    m_AddLoadableObject.SetValue("asset_path", loadable.AssetPath);
                    m_AddLoadableObject.SetValue("lfid", loadable.LFID);
                    m_AddLoadableObject.SetValue("identifier_type", loadable.IdentifierType);
                    m_AddLoadableObject.SetValue("serialized_file_index",
                        loadable.SerializedFile < 0 ? null : loadable.SerializedFile);
                    m_AddLoadableObject.SetValue("output_lfid", loadable.OutputLFID);
                    m_AddLoadableObject.SetValue("is_root_asset", rootAssets.Contains(loadable.ObjectIdHash) ? 1 : 0);
                    m_AddLoadableObject.ExecuteNonQuery();
                }

                foreach (var scene in layout.LoadableSceneIds ?? [])
                {
                    m_AddLoadableScene.SetValue("guid", scene.GUID);
                    m_AddLoadableScene.SetValue("path", scene.Path);
                    m_AddLoadableScene.SetValue("serialized_file_index",
                        scene.SerializedFile < 0 ? null : scene.SerializedFile);
                    m_AddLoadableScene.ExecuteNonQuery();
                }

                foreach (var artifact in layout.BinaryArtifacts ?? [])
                {
                    m_AddBinaryArtifact.SetValue("artifact_index", artifact.Index);
                    m_AddBinaryArtifact.SetValue("content_hash", artifact.ContentHash);
                    m_AddBinaryArtifact.SetValue("category", artifact.Category);
                    m_AddBinaryArtifact.SetValue("size", artifact.Size);
                    m_AddBinaryArtifact.ExecuteNonQuery();

                    foreach (var referencedIndex in artifact.ArtifactReferences ?? [])
                    {
                        m_AddArtifactReference.SetValue("artifact_index", artifact.Index);
                        m_AddArtifactReference.SetValue("referenced_artifact_index", referencedIndex);
                        m_AddArtifactReference.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }

            // Indexes are created after the bulk insert so that a very large layout imports fast.
            ExecuteDDL(Properties.Resources.ContentLayoutIndexes);

            PopulateContentFileDependencies(layout);
        }

        // Fills the shared dependency map that resolves the external references of the analyzed
        // .cf files (see ContentFileDependencyMap): for each content file, the dependency indexes
        // become the target filenames, in order. Built-in dependencies (no content hash) map to
        // null so the reference falls back to the external table path.
        private void PopulateContentFileDependencies(ContentLayout layout)
        {
            var hashByIndex = (layout.SerializedFiles ?? []).ToDictionary(f => f.Index, f => f.ContentHash);

            foreach (var file in layout.SerializedFiles ?? [])
            {
                if (string.IsNullOrEmpty(file.ContentHash))
                    continue;

                var resolved = (file.SerializedFileDependencies ?? [])
                    .Select(i => hashByIndex.TryGetValue(i, out var hash) && !string.IsNullOrEmpty(hash)
                        ? (hash + ".cf").ToLowerInvariant()
                        : null)
                    .ToArray();

                m_ContentFileDependencies.Add((file.ContentHash + ".cf").ToLowerInvariant(), resolved);
            }
        }

        // Fills in the serialized_file column, linking each layout entry to its serialized_files
        // row. Called after all files are processed. Ids come from the shared IdProvider, so
        // entries whose .cf file was analyzed link to the row the analyze pass wrote (loose or
        // inside an archive, with its archive column intact). For files never encountered on the
        // input (a layout-only analyze, or a subset of a build) a placeholder serialized_files
        // row is written - name only, archive NULL, no objects - so the link is always valid,
        // mirroring what the dangling-refs finalize does for referenced-but-unanalyzed files.
        public void LinkSerializedFiles()
        {
            var existingIds = new HashSet<int>();
            using (var select = m_Database.CreateCommand())
            {
                select.CommandText = "SELECT id FROM serialized_files";
                using var reader = select.ExecuteReader();
                while (reader.Read())
                {
                    existingIds.Add(reader.GetInt32(0));
                }
            }

            using var transaction = m_Database.BeginTransaction();
            using var update = m_Database.CreateCommand();
            update.Transaction = transaction;
            update.CommandText = "UPDATE content_layout_serialized_files SET serialized_file = @id WHERE file_index = @file_index";
            update.Parameters.Add("@id", SqliteType.Integer);
            update.Parameters.Add("@file_index", SqliteType.Integer);

            var addStubRow = new Commands.SerializedFile.AddSerializedFile();
            addStubRow.CreateCommand(m_Database);
            addStubRow.SetTransaction(transaction);

            try
            {
                foreach (var file in m_ImportedFiles)
                {
                    var fileName = (file.ContentHash + ".cf").ToLowerInvariant();
                    var id = m_SerializedFileIdProvider.GetId(fileName);

                    update.Parameters["@id"].Value = id;
                    update.Parameters["@file_index"].Value = file.Index;
                    update.ExecuteNonQuery();

                    if (existingIds.Add(id))
                    {
                        addStubRow.SetValue("id", id);
                        addStubRow.SetValue("archive", null);
                        addStubRow.SetValue("name", fileName);
                        addStubRow.ExecuteNonQuery();
                    }
                }

                transaction.Commit();
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }
            finally
            {
                addStubRow.Dispose();
            }
        }

        private void SetTransaction(SqliteTransaction transaction)
        {
            m_AddContentLayout.SetTransaction(transaction);
            m_AddSerializedFile.SetTransaction(transaction);
            m_AddSourceAsset.SetTransaction(transaction);
            m_AddSerializedFileDependency.SetTransaction(transaction);
            m_AddLoadableDependency.SetTransaction(transaction);
            m_AddLoadableSceneDependency.SetTransaction(transaction);
            m_AddLoadableObject.SetTransaction(transaction);
            m_AddLoadableScene.SetTransaction(transaction);
            m_AddBinaryArtifact.SetTransaction(transaction);
            m_AddArtifactReference.SetTransaction(transaction);
        }

        private void ExecuteDDL(string sql)
        {
            using var command = m_Database.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        public void Dispose()
        {
            m_AddContentLayout.Dispose();
            m_AddSerializedFile.Dispose();
            m_AddSourceAsset.Dispose();
            m_AddSerializedFileDependency.Dispose();
            m_AddLoadableDependency.Dispose();
            m_AddLoadableSceneDependency.Dispose();
            m_AddLoadableObject.Dispose();
            m_AddLoadableScene.Dispose();
            m_AddBinaryArtifact.Dispose();
            m_AddArtifactReference.Dispose();
        }
    }
}
