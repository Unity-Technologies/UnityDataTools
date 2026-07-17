using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using UnityDataTools.Analyzer.SerializedObjects;
using UnityDataTools.FileSystem;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SQLite.Handlers;

public class PackedAssetsHandler : ISQLiteHandler
{
    private SqliteConnection m_Database;
    private bool m_SchemaCreated;
    private SqliteCommand m_InsertPackedAssetsCommand;
    private SqliteCommand m_InsertSourceAssetCommand;
    private SqliteCommand m_GetSourceAssetIdCommand;
    private SqliteCommand m_InsertContentsCommand;
    private SqliteCommand m_InsertTypeCommand;
    private Dictionary<(string guid, string path), long> m_SourceAssetCache = new();

    // Type ids we have already tried to add to the types table. A single BuildReport can list
    // thousands of objects sharing a handful of types, so this skips the redundant INSERT calls.
    // The handler instance lives for the whole analyze, so this dedups across all reports.
    private HashSet<int> m_InsertedTypes = new();

    public void Init(SqliteConnection db)
    {
        // The build_report_packed_* tables and views are created lazily, on the first PackedAssets
        // object (see EnsureSchema). A build report without any PackedAssets object (or a database
        // analyzed without any build report) therefore does not get these tables.
        m_Database = db;

        m_InsertPackedAssetsCommand = db.CreateCommand();
        m_InsertPackedAssetsCommand.CommandText = @"INSERT INTO build_report_packed_assets(
            id, path, file_header_size
        ) VALUES(
            @id, @path, @file_header_size
        )";

        m_InsertPackedAssetsCommand.Parameters.Add("@id", SqliteType.Integer);
        m_InsertPackedAssetsCommand.Parameters.Add("@path", SqliteType.Text);
        m_InsertPackedAssetsCommand.Parameters.Add("@file_header_size", SqliteType.Integer);

        m_InsertSourceAssetCommand = db.CreateCommand();
        m_InsertSourceAssetCommand.CommandText = @"INSERT OR IGNORE INTO build_report_source_assets(
            source_asset_guid, build_time_asset_path
        ) VALUES(
            @source_asset_guid, @build_time_asset_path
        )";
        m_InsertSourceAssetCommand.Parameters.Add("@source_asset_guid", SqliteType.Text);
        m_InsertSourceAssetCommand.Parameters.Add("@build_time_asset_path", SqliteType.Text);

        m_GetSourceAssetIdCommand = db.CreateCommand();
        m_GetSourceAssetIdCommand.CommandText = @"SELECT id FROM build_report_source_assets 
            WHERE source_asset_guid = @source_asset_guid AND build_time_asset_path = @build_time_asset_path";
        m_GetSourceAssetIdCommand.Parameters.Add("@source_asset_guid", SqliteType.Text);
        m_GetSourceAssetIdCommand.Parameters.Add("@build_time_asset_path", SqliteType.Text);

        m_InsertContentsCommand = db.CreateCommand();
        m_InsertContentsCommand.CommandText = @"INSERT INTO build_report_packed_asset_info(
            packed_assets_id, object_id, type, size, offset, source_asset_id
        ) VALUES(
            @packed_assets_id, @object_id, @type, @size, @offset, @source_asset_id
        )";

        m_InsertContentsCommand.Parameters.Add("@packed_assets_id", SqliteType.Integer);
        m_InsertContentsCommand.Parameters.Add("@object_id", SqliteType.Integer);
        m_InsertContentsCommand.Parameters.Add("@type", SqliteType.Integer);
        m_InsertContentsCommand.Parameters.Add("@size", SqliteType.Integer);
        m_InsertContentsCommand.Parameters.Add("@offset", SqliteType.Integer);
        m_InsertContentsCommand.Parameters.Add("@source_asset_id", SqliteType.Integer);

        // A BuildReport records object types by numeric id only. When we can map an id to a
        // human-readable name via TypeIdRegistry, we add it to the shared types table so the
        // build report views can show the name. INSERT OR IGNORE keeps any name already inserted
        // by TypeTree analysis of the actual build output (which is authoritative).
        m_InsertTypeCommand = db.CreateCommand();
        m_InsertTypeCommand.CommandText = "INSERT OR IGNORE INTO types(id, name) VALUES(@id, @name)";
        m_InsertTypeCommand.Parameters.Add("@id", SqliteType.Integer);
        m_InsertTypeCommand.Parameters.Add("@name", SqliteType.Text);
    }

    // Creates the build_report_packed_* schema on first use. Runs inside the current file's
    // transaction, so it is committed together with the rows it is about to receive. Its views
    // reference build_report_archive_contents (created by BuildReportHandler); SQLite does not
    // resolve view references until query time, and every build report containing PackedAssets
    // objects also contains the BuildReport object, so that table exists before any query.
    private void EnsureSchema(SqliteTransaction transaction)
    {
        if (m_SchemaCreated)
            return;

        using var command = m_Database.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = Properties.Resources.PackedAssets ?? throw new InvalidOperationException("PackedAssets resource not found");
        command.ExecuteNonQuery();

        m_SchemaCreated = true;
    }

    public void Process(Context ctx, long objectId, RandomAccessReader reader, out string name, out long streamDataSize)
    {
        EnsureSchema(ctx.Transaction);

        var packedAssets = PackedAssets.Read(reader);

        m_InsertPackedAssetsCommand.Transaction = ctx.Transaction;
        m_InsertPackedAssetsCommand.Parameters["@id"].Value = objectId;
        m_InsertPackedAssetsCommand.Parameters["@path"].Value = packedAssets.Path;
        m_InsertPackedAssetsCommand.Parameters["@file_header_size"].Value = (long)packedAssets.FileHeaderSize;
        m_InsertPackedAssetsCommand.ExecuteNonQuery();

        // Insert contents
        foreach (var content in packedAssets.Contents)
        {
            // Get or create source asset ID
            var cacheKey = (content.SourceAssetGUID, content.BuildTimeAssetPath);
            if (!m_SourceAssetCache.TryGetValue(cacheKey, out long sourceAssetId))
            {
                // Insert the source asset (will be ignored if it already exists)
                m_InsertSourceAssetCommand.Transaction = ctx.Transaction;
                m_InsertSourceAssetCommand.Parameters["@source_asset_guid"].Value = content.SourceAssetGUID;
                m_InsertSourceAssetCommand.Parameters["@build_time_asset_path"].Value = content.BuildTimeAssetPath;
                m_InsertSourceAssetCommand.ExecuteNonQuery();

                // Get the ID (whether just inserted or already existing)
                m_GetSourceAssetIdCommand.Transaction = ctx.Transaction;
                m_GetSourceAssetIdCommand.Parameters["@source_asset_guid"].Value = content.SourceAssetGUID;
                m_GetSourceAssetIdCommand.Parameters["@build_time_asset_path"].Value = content.BuildTimeAssetPath;
                sourceAssetId = (long)m_GetSourceAssetIdCommand.ExecuteScalar();

                m_SourceAssetCache[cacheKey] = sourceAssetId;
            }

            // Populate the types table so views can display the type name even when the build
            // output (and its TypeTrees) is not analyzed alongside the report.
            if (m_InsertedTypes.Add(content.Type) &&
                TypeIdRegistry.TryGetTypeName(content.Type, out var typeName))
            {
                m_InsertTypeCommand.Transaction = ctx.Transaction;
                m_InsertTypeCommand.Parameters["@id"].Value = content.Type;
                m_InsertTypeCommand.Parameters["@name"].Value = typeName;
                m_InsertTypeCommand.ExecuteNonQuery();
            }

            m_InsertContentsCommand.Transaction = ctx.Transaction;
            m_InsertContentsCommand.Parameters["@packed_assets_id"].Value = objectId;
            m_InsertContentsCommand.Parameters["@object_id"].Value = content.ObjectID;
            m_InsertContentsCommand.Parameters["@type"].Value = content.Type;
            m_InsertContentsCommand.Parameters["@size"].Value = (long)content.Size;
            m_InsertContentsCommand.Parameters["@offset"].Value = (long)content.Offset;
            m_InsertContentsCommand.Parameters["@source_asset_id"].Value = sourceAssetId;
            m_InsertContentsCommand.ExecuteNonQuery();
        }

        streamDataSize = 0;
        name = packedAssets.Path;
    }

    public void Finalize(SqliteConnection db)
    {
    }

    void IDisposable.Dispose()
    {
        m_InsertPackedAssetsCommand?.Dispose();
        m_InsertSourceAssetCommand?.Dispose();
        m_GetSourceAssetIdCommand?.Dispose();
        m_InsertContentsCommand?.Dispose();
        m_InsertTypeCommand?.Dispose();
    }
}

