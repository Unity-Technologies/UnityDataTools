using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;
using UnityDataTools.Analyzer.SQLite.Commands.AddressablesBuildReport;
using UnityDataTools.Analyzer.SQLite.Parsers.Models;

namespace UnityDataTools.Analyzer.SQLite.Writers
{
    internal class AddressablesBuildLayoutSQLWriter : IDisposable
    {
        private AddressablesBuild m_AddressablesBuild = new AddressablesBuild();
        private AddressablesBuildBundle m_AddressablesBuildBundle = new AddressablesBuildBundle();
        private AddressablesBuildBundleDependency m_AddressablesBuildBundleDependency = new AddressablesBuildBundleDependency();
        private AddressablesBuildBundleExpandedDependency m_AddressablesBuildBundleExpandedDependency = new AddressablesBuildBundleExpandedDependency();
        private AddressablesBuildBundleRegularDependency m_AddressablesBuildBundleRegularDependency = new AddressablesBuildBundleRegularDependency();
        private AddressablesBuildBundleDependentBundle m_AddressablesBuildBundleDependentBundle = new AddressablesBuildBundleDependentBundle();
        private AddressablesBuildBundleFile m_AddressablesBuildBundleFile = new AddressablesBuildBundleFile();

        private AddressablesBuildDataFromOtherAsset m_AddressablesDataFromOtherAsset = new AddressablesBuildDataFromOtherAsset();
        private AddressablesBuildDataFromOtherAssetObject m_AddressablesBuildDataFromOtherAssetObject = new AddressablesBuildDataFromOtherAssetObject();
        private AddressablesBuildDataFromOtherAssetObjectReference m_AddressablesBuildDataFromOtherAssetObjectReference = new AddressablesBuildDataFromOtherAssetObjectReference();
        private AddressablesBuildDataFromOtherAssetReferencingAsset m_AddressablesBuildDataFromOtherAssetReferencingAsset = new AddressablesBuildDataFromOtherAssetReferencingAsset();

        private AddressablesBuildExplicitAsset m_AddressablesExplicitAsset = new AddressablesBuildExplicitAsset();
        private AddressablesBuildExplicitAssetExternallyReferencedAsset m_AddressablesBuildExplicitAssetExternallyReferencedAsset = new AddressablesBuildExplicitAssetExternallyReferencedAsset();
        private AddressablesBuildExplicitAssetInternalReferencedExplicitAsset m_AddressablesBuildExplicitAssetInternalReferencedExplicitAsset = new AddressablesBuildExplicitAssetInternalReferencedExplicitAsset();
        private AddressablesBuildExplicitAssetInternalReferencedOtherAsset m_AddressablesBuildExplicitAssetInternalReferencedOtherAsset = new AddressablesBuildExplicitAssetInternalReferencedOtherAsset();
        private AddressablesBuildExplicitAssetLabel m_AddressablesBuildExplicitAssetLabel = new AddressablesBuildExplicitAssetLabel();

        private AddressablesBuildFile m_AddressablesBuildFile = new AddressablesBuildFile();
        private AddressablesBuildFileAsset m_AddressablesBuildFileAsset = new AddressablesBuildFileAsset();
        private AddressablesBuildFileExternalReference m_AddressablesBuildFileExternalReference = new AddressablesBuildFileExternalReference();
        private AddressablesBuildFileOtherAsset m_AddressablesBuildFileOtherAsset = new AddressablesBuildFileOtherAsset();
        private AddressablesBuildFileSubFile m_AddressablesBuildFileSubFile = new AddressablesBuildFileSubFile();

        private AddressablesBuildGroup m_AddressablesBuildGroup = new AddressablesBuildGroup();
        private AddressablesBuildGroupBundle m_AddressablesBuildGroupBundle = new AddressablesBuildGroupBundle();
        private AddressablesBuildGroupSchema m_AddressablesBuildGroupSchema = new AddressablesBuildGroupSchema();

        private AddressablesBuildSchema m_AddressablesBuildSchema = new AddressablesBuildSchema();
        private AddressablesBuildSchemaDataPair m_AddressablesBuildSchemaDataPair = new AddressablesBuildSchemaDataPair();

        private AddressablesBuildSubFile m_AddressablesBuildSubFile = new AddressablesBuildSubFile();

        private SqliteCommand m_LastId = new SqliteCommand();

        private bool m_Initialized;
        private SqliteConnection m_Database;
        public bool Verbose { get; set; }

        public AddressablesBuildLayoutSQLWriter(SqliteConnection database)
        {
            m_Initialized = false;
            m_Database = database;
        }

        public void Init()
        {
            if (m_Initialized)
                return;

            m_Initialized = true;
            // build addressables file commands
            m_AddressablesBuild.CreateCommand(m_Database);
            // Build Bundle Tables
            m_AddressablesBuildBundle.CreateCommand(m_Database);
            m_AddressablesBuildBundleDependency.CreateCommand(m_Database);
            m_AddressablesBuildBundleExpandedDependency.CreateCommand(m_Database);
            m_AddressablesBuildBundleRegularDependency.CreateCommand(m_Database);
            m_AddressablesBuildBundleDependentBundle.CreateCommand(m_Database);
            m_AddressablesBuildBundleFile.CreateCommand(m_Database);

            // Data From Other Asset Tables
            m_AddressablesDataFromOtherAsset.CreateCommand(m_Database);
            m_AddressablesBuildDataFromOtherAssetObject.CreateCommand(m_Database);
            m_AddressablesBuildDataFromOtherAssetObjectReference.CreateCommand(m_Database);
            m_AddressablesBuildDataFromOtherAssetReferencingAsset.CreateCommand(m_Database);

            // Explicit Asset Tables
            m_AddressablesExplicitAsset.CreateCommand(m_Database);
            m_AddressablesBuildExplicitAssetExternallyReferencedAsset.CreateCommand(m_Database);
            m_AddressablesBuildExplicitAssetInternalReferencedExplicitAsset.CreateCommand(m_Database);
            m_AddressablesBuildExplicitAssetInternalReferencedOtherAsset.CreateCommand(m_Database);
            m_AddressablesBuildExplicitAssetLabel.CreateCommand(m_Database);

            // File Tables
            m_AddressablesBuildFile.CreateCommand(m_Database);
            m_AddressablesBuildFileAsset.CreateCommand(m_Database);
            m_AddressablesBuildFileExternalReference.CreateCommand(m_Database);
            m_AddressablesBuildFileOtherAsset.CreateCommand(m_Database);
            m_AddressablesBuildFileSubFile.CreateCommand(m_Database);

            // Group Tables
            m_AddressablesBuildGroup.CreateCommand(m_Database);
            m_AddressablesBuildGroupBundle.CreateCommand(m_Database);
            m_AddressablesBuildGroupSchema.CreateCommand(m_Database);

            // Schema Tables
            m_AddressablesBuildSchema.CreateCommand(m_Database);
            m_AddressablesBuildSchemaDataPair.CreateCommand(m_Database);

            // SubFile Tables
            m_AddressablesBuildSubFile.CreateCommand(m_Database);

            m_LastId = m_Database.CreateCommand();
            m_LastId.CommandText = "SELECT last_insert_rowid()";
        }

        public void Dispose()
        {
            // build addressables file commands
            m_AddressablesBuild.Dispose();
            // Build Bundle Tables
            m_AddressablesBuildBundle.Dispose();
            m_AddressablesBuildBundleDependency.Dispose();
            m_AddressablesBuildBundleExpandedDependency.Dispose();
            m_AddressablesBuildBundleRegularDependency.Dispose();
            m_AddressablesBuildBundleDependentBundle.Dispose();
            m_AddressablesBuildBundleFile.Dispose();

            // Data From Other Asset Tables
            m_AddressablesDataFromOtherAsset.Dispose();
            m_AddressablesBuildDataFromOtherAssetObject.Dispose();
            m_AddressablesBuildDataFromOtherAssetObjectReference.Dispose();
            m_AddressablesBuildDataFromOtherAssetReferencingAsset.Dispose();

            // Explicit Asset Tables
            m_AddressablesExplicitAsset.Dispose();
            m_AddressablesBuildExplicitAssetExternallyReferencedAsset.Dispose();
            m_AddressablesBuildExplicitAssetInternalReferencedExplicitAsset.Dispose();
            m_AddressablesBuildExplicitAssetInternalReferencedOtherAsset.Dispose();
            m_AddressablesBuildExplicitAssetLabel.Dispose();

            // File Tables
            m_AddressablesBuildFile.Dispose();
            m_AddressablesBuildFileAsset.Dispose();
            m_AddressablesBuildFileExternalReference.Dispose();
            m_AddressablesBuildFileOtherAsset.Dispose();
            m_AddressablesBuildFileSubFile.Dispose();

            // Group Tables
            m_AddressablesBuildGroup.Dispose();
            m_AddressablesBuildGroupBundle.Dispose();
            m_AddressablesBuildGroupSchema.Dispose();

            // Schema Tables
            m_AddressablesBuildSchema.Dispose();
            m_AddressablesBuildSchemaDataPair.Dispose();

            // SubFile Tables
            m_AddressablesBuildSubFile.Dispose();

            m_LastId.Dispose();
        }

        public void WriteAddressablesBuild(string filename, BuildLayout buildLayout)
        {
            using var transaction = m_Database.BeginTransaction();

            try
            {
                m_AddressablesBuild.SetTransaction(transaction);
                m_AddressablesBuild.SetValue("name", Path.GetFileName(filename));
                m_AddressablesBuild.SetValue("build_target", buildLayout.BuildTarget);
                m_AddressablesBuild.SetValue("start_time", buildLayout.BuildStartTime);
                m_AddressablesBuild.SetValue("duration", buildLayout.Duration);
                m_AddressablesBuild.SetValue("error", buildLayout.BuildError);
                m_AddressablesBuild.SetValue("package_version", buildLayout.PackageVersion);
                m_AddressablesBuild.SetValue("player_version", buildLayout.PlayerBuildVersion);
                m_AddressablesBuild.SetValue("build_script", buildLayout.BuildScript);
                m_AddressablesBuild.SetValue("result_hash", buildLayout.BuildResultHash);
                m_AddressablesBuild.SetValue("type", buildLayout.BuildType);
                m_AddressablesBuild.SetValue("unity_version", buildLayout.UnityVersion);
                m_AddressablesBuild.ExecuteNonQuery();

                m_LastId.Transaction = transaction;
                long buildId = (long)m_LastId.ExecuteScalar();
                if (Verbose)
                    Console.WriteLine($"Assigned report build ID: {buildId}");

                foreach (var reference in buildLayout.references.RefIds)
                {
                    switch (reference.type.Class)
                    {
                        case "BuildLayout/Bundle":
                            WriteBuildLayoutBundle(reference, buildId, transaction);
                            break;

                        case "BuildLayout/DataFromOtherAsset":
                            WriteBuildLayoutDataFromOtherAsset(reference, buildId, transaction);
                            break;

                        case "BuildLayout/ExplicitAsset":
                            WriteBuildLayoutExplicitAsset(reference, buildId, transaction);
                            break;

                        case "BuildLayout/File":
                            WriteBuildLayoutFile(reference, buildId, transaction);
                            break;

                        case "BuildLayout/Group":
                            WriteBuildLayoutGroup(reference, buildId, transaction);
                            break;

                        case "BuildLayout/SchemaData":
                            WriteBuildLayoutSchemaData(reference, buildId, transaction);
                            break;

                        case "BuildLayout/SubFile":
                            WriteBuildLayoutSubFile(reference, buildId, transaction);
                            break;
                    }
                }

                // do the stuff
                transaction.Commit();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.StackTrace);
                transaction.Rollback();
                throw;
            }
        }

        private void WriteBuildLayoutBundle(Reference reference, long buildId, SqliteTransaction transaction)
        {
            m_AddressablesBuildBundle.SetTransaction(transaction);
            m_AddressablesBuildBundle.SetValue("id", reference.rid);
            m_AddressablesBuildBundle.SetValue("build_id", buildId);
            m_AddressablesBuildBundle.SetValue("asset_count", reference.data.AssetCount);
            m_AddressablesBuildBundle.SetValue("build_status", reference.data.BuildStatus);
            m_AddressablesBuildBundle.SetValue("crc", reference.data.CRC);
            m_AddressablesBuildBundle.SetValue("compression", reference.data.Compression);
            m_AddressablesBuildBundle.SetValue("dependency_file_size", reference.data.DependencyFileSize);
            m_AddressablesBuildBundle.SetValue("expanded_dependency_file_size", reference.data.ExpandedDependencyFileSize);
            m_AddressablesBuildBundle.SetValue("file_size", reference.data.FileSize);
            m_AddressablesBuildBundle.SetValue("group_rid", reference.data.Group.rid);
            m_AddressablesBuildBundle.SetValue("hash", JsonConvert.SerializeObject(reference.data.Hash));
            m_AddressablesBuildBundle.SetValue("internal_name", reference.data.InternalName);
            m_AddressablesBuildBundle.SetValue("load_path", reference.data.LoadPath);
            m_AddressablesBuildBundle.SetValue("name", reference.data.Name);
            m_AddressablesBuildBundle.SetValue("provider", reference.data.Provider);
            m_AddressablesBuildBundle.SetValue("result_type", reference.data.ResultType);
            m_AddressablesBuildBundle.ExecuteNonQuery();

            var visited = new Dictionary<string, bool>();
            // Insert bundle dependencies
            if (reference.data.BundleDependencies != null)
            {
                foreach (var dep in reference.data.BundleDependencies)
                {
                    var key = $"{buildId}_{reference.rid}_{dep.rid}";
                    if (visited.ContainsKey(key))
                        continue;
                    visited.Add(key, true);
                    m_AddressablesBuildBundleDependency.SetTransaction(transaction);
                    m_AddressablesBuildBundleDependency.SetValue("bundle_id", reference.rid);
                    m_AddressablesBuildBundleDependency.SetValue("build_id", buildId);
                    m_AddressablesBuildBundleDependency.SetValue("dependency_rid", dep.rid);
                    m_AddressablesBuildBundleDependency.ExecuteNonQuery();
                }
            }

            // Insert regular dependencies
            if (reference.data.Dependencies != null)
            {
                foreach (var dep in reference.data.Dependencies)
                {
                    m_AddressablesBuildBundleRegularDependency.SetTransaction(transaction);
                    m_AddressablesBuildBundleRegularDependency.SetValue("bundle_id", reference.rid);
                    m_AddressablesBuildBundleRegularDependency.SetValue("build_id", buildId);
                    m_AddressablesBuildBundleRegularDependency.SetValue("dependency_rid", dep.rid);
                    m_AddressablesBuildBundleRegularDependency.ExecuteNonQuery();
                }
            }

            // Insert dependent bundles
            if (reference.data.DependentBundles != null)
            {
                foreach (var depBundle in reference.data.DependentBundles)
                {
                    m_AddressablesBuildBundleDependentBundle.SetTransaction(transaction);
                    m_AddressablesBuildBundleDependentBundle.SetValue("bundle_id", reference.rid);
                    m_AddressablesBuildBundleDependentBundle.SetValue("build_id", buildId);
                    m_AddressablesBuildBundleDependentBundle.SetValue("dependent_bundle_rid", depBundle.rid);
                    m_AddressablesBuildBundleDependentBundle.ExecuteNonQuery();
                }
            }

            // Insert expanded dependencies
            if (reference.data.ExpandedDependencies != null)
            {
                foreach (var dep in reference.data.ExpandedDependencies)
                {
                    m_AddressablesBuildBundleExpandedDependency.SetTransaction(transaction);
                    m_AddressablesBuildBundleExpandedDependency.SetValue("bundle_id", reference.rid);
                    m_AddressablesBuildBundleExpandedDependency.SetValue("build_id", buildId);
                    m_AddressablesBuildBundleExpandedDependency.SetValue("dependency_rid", dep.rid);
                    m_AddressablesBuildBundleExpandedDependency.ExecuteNonQuery();
                }
            }

            // Insert files
            if (reference.data.Files != null)
            {
                foreach (var file in reference.data.Files)
                {
                    m_AddressablesBuildBundleFile.SetTransaction(transaction);
                    m_AddressablesBuildBundleFile.SetValue("bundle_id", reference.rid);
                    m_AddressablesBuildBundleFile.SetValue("build_id", buildId);
                    m_AddressablesBuildBundleFile.SetValue("file_rid", file.rid);
                    m_AddressablesBuildBundleFile.ExecuteNonQuery();
                }
            }
        }

        private void WriteBuildLayoutDataFromOtherAsset(Reference reference, long buildId, SqliteTransaction transaction)
        {
            m_AddressablesDataFromOtherAsset.SetTransaction(transaction);
            m_AddressablesDataFromOtherAsset.SetValue("id", reference.rid);
            m_AddressablesDataFromOtherAsset.SetValue("build_id", buildId);
            m_AddressablesDataFromOtherAsset.SetValue("asset_guid", reference.data.AssetGuid);
            m_AddressablesDataFromOtherAsset.SetValue("asset_path", reference.data.AssetPath);
            m_AddressablesDataFromOtherAsset.SetValue("file", reference.data.File.rid);
            m_AddressablesDataFromOtherAsset.SetValue("main_asset_type", reference.data.MainAssetType);
            m_AddressablesDataFromOtherAsset.SetValue("object_count", reference.data.ObjectCount);
            m_AddressablesDataFromOtherAsset.SetValue("serialized_size", reference.data.SerializedSize);
            m_AddressablesDataFromOtherAsset.SetValue("streamed_size", reference.data.StreamedSize);
            m_AddressablesDataFromOtherAsset.ExecuteNonQuery();

            // Insert objects
            if (reference.data.Objects != null)
            {
                foreach (var obj in reference.data.Objects)
                {
                    m_AddressablesBuildDataFromOtherAssetObject.SetTransaction(transaction);
                    m_AddressablesBuildDataFromOtherAssetObject.SetValue("data_from_other_asset_id", reference.rid);
                    m_AddressablesBuildDataFromOtherAssetObject.SetValue("build_id", buildId);
                    m_AddressablesBuildDataFromOtherAssetObject.SetValue("asset_type", obj.AssetType);
                    m_AddressablesBuildDataFromOtherAssetObject.SetValue("component_name", obj.ComponentName ?? "");
                    m_AddressablesBuildDataFromOtherAssetObject.SetValue("local_identifier_in_file", obj.LocalIdentifierInFile);
                    m_AddressablesBuildDataFromOtherAssetObject.SetValue("object_name", obj.ObjectName ?? "");
                    m_AddressablesBuildDataFromOtherAssetObject.SetValue("serialized_size", obj.SerializedSize);
                    m_AddressablesBuildDataFromOtherAssetObject.SetValue("streamed_size", obj.StreamedSize);
                    m_AddressablesBuildDataFromOtherAssetObject.ExecuteNonQuery();

                    // Insert object references
                    if (obj.References != null)
                    {
                        foreach (var objRef in obj.References)
                        {
                            m_AddressablesBuildDataFromOtherAssetObjectReference.SetTransaction(transaction);
                            m_AddressablesBuildDataFromOtherAssetObjectReference.SetValue("data_from_other_asset_id", reference.rid);
                            m_AddressablesBuildDataFromOtherAssetObjectReference.SetValue("build_id", buildId);
                            m_AddressablesBuildDataFromOtherAssetObjectReference.SetValue("local_identifier_in_file", obj.LocalIdentifierInFile);
                            m_AddressablesBuildDataFromOtherAssetObjectReference.SetValue("asset_id", objRef.AssetId);
                            m_AddressablesBuildDataFromOtherAssetObjectReference.SetValue("object_id", objRef.ObjectId);
                            m_AddressablesBuildDataFromOtherAssetObjectReference.ExecuteNonQuery();
                        }
                    }
                }
            }

            // Insert referencing assets
            if (reference.data.ReferencingAssets != null)
            {
                foreach (var refAsset in reference.data.ReferencingAssets)
                {
                    m_AddressablesBuildDataFromOtherAssetReferencingAsset.SetTransaction(transaction);
                    m_AddressablesBuildDataFromOtherAssetReferencingAsset.SetValue("data_from_other_asset_id", reference.rid);
                    m_AddressablesBuildDataFromOtherAssetReferencingAsset.SetValue("build_id", buildId);
                    m_AddressablesBuildDataFromOtherAssetReferencingAsset.SetValue("referencing_asset_rid", refAsset.rid);
                    m_AddressablesBuildDataFromOtherAssetReferencingAsset.ExecuteNonQuery();
                }
            }
        }

        private void WriteBuildLayoutExplicitAsset(Reference reference, long buildId, SqliteTransaction transaction)
        {
            m_AddressablesExplicitAsset.SetTransaction(transaction);
            m_AddressablesExplicitAsset.SetValue("id", reference.rid);
            m_AddressablesExplicitAsset.SetValue("build_id", buildId);
            m_AddressablesExplicitAsset.SetValue("bundle", reference.data.Bundle.rid);
            m_AddressablesExplicitAsset.SetValue("file", reference.data.File.rid);
            m_AddressablesExplicitAsset.SetValue("asset_hash", reference.data.AssetHash.Hash);
            m_AddressablesExplicitAsset.SetValue("asset_path", reference.data.AssetPath);
            m_AddressablesExplicitAsset.SetValue("addressable_name", reference.data.AddressableName);
            m_AddressablesExplicitAsset.SetValue("group_guid", reference.data.GroupGuid);
            m_AddressablesExplicitAsset.SetValue("guid", reference.data.Guid);
            m_AddressablesExplicitAsset.SetValue("internal_id", reference.data.InternalId);
            m_AddressablesExplicitAsset.SetValue("streamed_size", reference.data.StreamedSize);
            m_AddressablesExplicitAsset.SetValue("serialized_size", reference.data.SerializedSize);
            m_AddressablesExplicitAsset.SetValue("main_asset_type", reference.data.MainAssetType);
            m_AddressablesExplicitAsset.ExecuteNonQuery();

            // Insert externally referenced assets
            if (reference.data.ExternallyReferencedAssets != null)
            {
                foreach (var extRefAsset in reference.data.ExternallyReferencedAssets)
                {
                    m_AddressablesBuildExplicitAssetExternallyReferencedAsset.SetTransaction(transaction);
                    m_AddressablesBuildExplicitAssetExternallyReferencedAsset.SetValue("explicit_asset_id", reference.rid);
                    m_AddressablesBuildExplicitAssetExternallyReferencedAsset.SetValue("build_id", buildId);
                    m_AddressablesBuildExplicitAssetExternallyReferencedAsset.SetValue("externally_referenced_asset_rid", extRefAsset.rid);
                    m_AddressablesBuildExplicitAssetExternallyReferencedAsset.ExecuteNonQuery();
                }
            }

            // Insert internal referenced explicit assets
            if (reference.data.InternalReferencedExplicitAssets != null)
            {
                foreach (var intRefExplicitAsset in reference.data.InternalReferencedExplicitAssets)
                {
                    m_AddressablesBuildExplicitAssetInternalReferencedExplicitAsset.SetTransaction(transaction);
                    m_AddressablesBuildExplicitAssetInternalReferencedExplicitAsset.SetValue("explicit_asset_id", reference.rid);
                    m_AddressablesBuildExplicitAssetInternalReferencedExplicitAsset.SetValue("build_id", buildId);
                    m_AddressablesBuildExplicitAssetInternalReferencedExplicitAsset.SetValue("internal_referenced_explicit_asset_rid", intRefExplicitAsset.rid);
                    m_AddressablesBuildExplicitAssetInternalReferencedExplicitAsset.ExecuteNonQuery();
                }
            }

            // Insert internal referenced other assets
            if (reference.data.InternalReferencedOtherAssets != null)
            {
                foreach (var intRefOtherAsset in reference.data.InternalReferencedOtherAssets)
                {
                    m_AddressablesBuildExplicitAssetInternalReferencedOtherAsset.SetTransaction(transaction);
                    m_AddressablesBuildExplicitAssetInternalReferencedOtherAsset.SetValue("explicit_asset_id", reference.rid);
                    m_AddressablesBuildExplicitAssetInternalReferencedOtherAsset.SetValue("build_id", buildId);
                    m_AddressablesBuildExplicitAssetInternalReferencedOtherAsset.SetValue("internal_referenced_other_asset_rid", intRefOtherAsset.rid);
                    m_AddressablesBuildExplicitAssetInternalReferencedOtherAsset.ExecuteNonQuery();
                }
            }

            // Insert labels
            if (reference.data.Labels != null)
            {
                foreach (var label in reference.data.Labels)
                {
                    m_AddressablesBuildExplicitAssetLabel.SetTransaction(transaction);
                    m_AddressablesBuildExplicitAssetLabel.SetValue("explicit_asset_id", reference.rid);
                    m_AddressablesBuildExplicitAssetLabel.SetValue("build_id", buildId);
                    m_AddressablesBuildExplicitAssetLabel.SetValue("label", label);
                    m_AddressablesBuildExplicitAssetLabel.ExecuteNonQuery();
                }
            }
        }

        private void WriteBuildLayoutFile(Reference reference, long buildId, SqliteTransaction transaction)
        {
            m_AddressablesBuildFile.SetTransaction(transaction);
            m_AddressablesBuildFile.SetValue("id", reference.rid);
            m_AddressablesBuildFile.SetValue("build_id", buildId);
            m_AddressablesBuildFile.SetValue("bundle", reference.data.Bundle.rid);
            m_AddressablesBuildFile.SetValue("bundle_object_info_size", reference.data.BundleObjectInfo.Size);
            m_AddressablesBuildFile.SetValue("mono_script_count", reference.data.MonoScriptCount);
            m_AddressablesBuildFile.SetValue("mono_script_size", reference.data.MonoScriptSize);
            m_AddressablesBuildFile.SetValue("name", reference.data.Name);
            m_AddressablesBuildFile.SetValue("preload_info_size", reference.data.PreloadInfoSize);
            m_AddressablesBuildFile.SetValue("write_result_filename", reference.data.WriteResultFilename);
            m_AddressablesBuildFile.ExecuteNonQuery();

            // Insert assets
            if (reference.data.Assets != null)
            {
                foreach (var asset in reference.data.Assets)
                {
                    m_AddressablesBuildFileAsset.SetTransaction(transaction);
                    m_AddressablesBuildFileAsset.SetValue("file_id", reference.rid);
                    m_AddressablesBuildFileAsset.SetValue("build_id", buildId);
                    m_AddressablesBuildFileAsset.SetValue("asset_rid", asset.rid);
                    m_AddressablesBuildFileAsset.ExecuteNonQuery();
                }
            }

            // Insert external references
            if (reference.data.ExternalReferences != null)
            {
                foreach (var extRef in reference.data.ExternalReferences)
                {
                    m_AddressablesBuildFileExternalReference.SetTransaction(transaction);
                    m_AddressablesBuildFileExternalReference.SetValue("file_id", reference.rid);
                    m_AddressablesBuildFileExternalReference.SetValue("build_id", buildId);
                    m_AddressablesBuildFileExternalReference.SetValue("external_reference_rid", extRef.rid);
                    m_AddressablesBuildFileExternalReference.ExecuteNonQuery();
                }
            }

            // Insert other assets
            if (reference.data.OtherAssets != null)
            {
                foreach (var otherAsset in reference.data.OtherAssets)
                {
                    m_AddressablesBuildFileOtherAsset.SetTransaction(transaction);
                    m_AddressablesBuildFileOtherAsset.SetValue("file_id", reference.rid);
                    m_AddressablesBuildFileOtherAsset.SetValue("build_id", buildId);
                    m_AddressablesBuildFileOtherAsset.SetValue("other_asset_rid", otherAsset.rid);
                    m_AddressablesBuildFileOtherAsset.ExecuteNonQuery();
                }
            }

            // Insert sub files
            if (reference.data.SubFiles != null)
            {
                foreach (var subFile in reference.data.SubFiles)
                {
                    m_AddressablesBuildFileSubFile.SetTransaction(transaction);
                    m_AddressablesBuildFileSubFile.SetValue("file_id", reference.rid);
                    m_AddressablesBuildFileSubFile.SetValue("build_id", buildId);
                    m_AddressablesBuildFileSubFile.SetValue("sub_file_rid", subFile.rid);
                    m_AddressablesBuildFileSubFile.ExecuteNonQuery();
                }
            }
        }

        private void WriteBuildLayoutGroup(Reference reference, long buildId, SqliteTransaction transaction)
        {
            m_AddressablesBuildGroup.SetTransaction(transaction);
            m_AddressablesBuildGroup.SetValue("id", reference.rid);
            m_AddressablesBuildGroup.SetValue("build_id", buildId);
            m_AddressablesBuildGroup.SetValue("guid", reference.data.Guid);
            m_AddressablesBuildGroup.SetValue("name", reference.data.Name);
            m_AddressablesBuildGroup.SetValue("packing_mode", reference.data.PackingMode);
            m_AddressablesBuildGroup.ExecuteNonQuery();

            // Insert bundles
            if (reference.data.Bundles != null)
            {
                foreach (var bundle in reference.data.Bundles)
                {
                    m_AddressablesBuildGroupBundle.SetTransaction(transaction);
                    m_AddressablesBuildGroupBundle.SetValue("group_id", reference.rid);
                    m_AddressablesBuildGroupBundle.SetValue("build_id", buildId);
                    m_AddressablesBuildGroupBundle.SetValue("bundle_rid", bundle.rid);
                    m_AddressablesBuildGroupBundle.ExecuteNonQuery();
                }
            }

            // Insert schemas
            if (reference.data.Schemas != null)
            {
                foreach (var schema in reference.data.Schemas)
                {
                    m_AddressablesBuildGroupSchema.SetTransaction(transaction);
                    m_AddressablesBuildGroupSchema.SetValue("group_id", reference.rid);
                    m_AddressablesBuildGroupSchema.SetValue("build_id", buildId);
                    m_AddressablesBuildGroupSchema.SetValue("schema_rid", schema.rid);
                    m_AddressablesBuildGroupSchema.ExecuteNonQuery();
                }
            }
        }

        private void WriteBuildLayoutSchemaData(Reference reference, long buildId, SqliteTransaction transaction)
        {
            m_AddressablesBuildSchema.SetTransaction(transaction);
            m_AddressablesBuildSchema.SetValue("id", reference.rid);
            m_AddressablesBuildSchema.SetValue("build_id", buildId);
            m_AddressablesBuildSchema.SetValue("guid", reference.data.Guid);
            m_AddressablesBuildSchema.SetValue("type", reference.data.Type);
            m_AddressablesBuildSchema.ExecuteNonQuery();

            // Insert schema data pairs
            if (reference.data.SchemaDataPairs != null)
            {
                foreach (var dataPair in reference.data.SchemaDataPairs)
                {
                    m_AddressablesBuildSchemaDataPair.SetTransaction(transaction);
                    m_AddressablesBuildSchemaDataPair.SetValue("schema_id", reference.rid);
                    m_AddressablesBuildSchemaDataPair.SetValue("build_id", buildId);
                    m_AddressablesBuildSchemaDataPair.SetValue("key", dataPair.Key);
                    m_AddressablesBuildSchemaDataPair.SetValue("value", dataPair.Value);
                    m_AddressablesBuildSchemaDataPair.ExecuteNonQuery();
                }
            }
        }

        private void WriteBuildLayoutSubFile(Reference reference, long buildId, SqliteTransaction transaction)
        {
            m_AddressablesBuildSubFile.SetTransaction(transaction);
            m_AddressablesBuildSubFile.SetValue("id", reference.rid);
            m_AddressablesBuildSubFile.SetValue("build_id", buildId);
            m_AddressablesBuildSubFile.SetValue("is_serialized_file", reference.data.IsSerializedFile ? 1 : 0);
            m_AddressablesBuildSubFile.SetValue("name", reference.data.Name);
            m_AddressablesBuildSubFile.SetValue("size", reference.data.Size);
            m_AddressablesBuildSubFile.ExecuteNonQuery();
        }
    }
}
