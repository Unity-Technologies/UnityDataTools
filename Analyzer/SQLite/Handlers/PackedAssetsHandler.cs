using System;
using Microsoft.Data.Sqlite;
using UnityDataTools.Analyzer.SerializedObjects;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.SQLite.Handlers;

public class PackedAssetsHandler : ISQLiteHandler
{
    private SqliteCommand m_InsertPackedAssetsCommand;
    private SqliteCommand m_InsertContentsCommand;

    public void Init(SqliteConnection db)
    {
        using var command = db.CreateCommand();
        command.CommandText = Properties.Resources.PackedAssets ?? throw new InvalidOperationException("PackedAssets resource not found");
        command.ExecuteNonQuery();

        m_InsertPackedAssetsCommand = db.CreateCommand();
        m_InsertPackedAssetsCommand.CommandText = @"INSERT INTO build_report_packed_assets(
            id, path, file_header_size
        ) VALUES(
            @id, @path, @file_header_size
        )";

        m_InsertPackedAssetsCommand.Parameters.Add("@id", SqliteType.Integer);
        m_InsertPackedAssetsCommand.Parameters.Add("@path", SqliteType.Text);
        m_InsertPackedAssetsCommand.Parameters.Add("@file_header_size", SqliteType.Integer);

        m_InsertContentsCommand = db.CreateCommand();
        m_InsertContentsCommand.CommandText = @"INSERT INTO build_report_packed_asset_info(
            packed_assets_id, object_id, type, size, offset, source_asset_guid, build_time_asset_path
        ) VALUES(
            @packed_assets_id, @object_id, @type, @size, @offset, @source_asset_guid, @build_time_asset_path
        )";

        m_InsertContentsCommand.Parameters.Add("@packed_assets_id", SqliteType.Integer);
        m_InsertContentsCommand.Parameters.Add("@object_id", SqliteType.Integer);
        m_InsertContentsCommand.Parameters.Add("@type", SqliteType.Integer);
        m_InsertContentsCommand.Parameters.Add("@size", SqliteType.Integer);
        m_InsertContentsCommand.Parameters.Add("@offset", SqliteType.Integer);
        m_InsertContentsCommand.Parameters.Add("@source_asset_guid", SqliteType.Text);
        m_InsertContentsCommand.Parameters.Add("@build_time_asset_path", SqliteType.Text);
    }

    public void Process(Context ctx, long objectId, RandomAccessReader reader, out string name, out long streamDataSize)
    {
        var packedAssets = PackedAssets.Read(reader);
        
        m_InsertPackedAssetsCommand.Transaction = ctx.Transaction;
        m_InsertPackedAssetsCommand.Parameters["@id"].Value = objectId;
        m_InsertPackedAssetsCommand.Parameters["@path"].Value = packedAssets.Path;
        m_InsertPackedAssetsCommand.Parameters["@file_header_size"].Value = (long)packedAssets.FileHeaderSize;
        m_InsertPackedAssetsCommand.ExecuteNonQuery();

        // Insert contents
        foreach (var content in packedAssets.Contents)
        {
            m_InsertContentsCommand.Transaction = ctx.Transaction;
            m_InsertContentsCommand.Parameters["@packed_assets_id"].Value = objectId;
            m_InsertContentsCommand.Parameters["@object_id"].Value = content.ObjectID;
            m_InsertContentsCommand.Parameters["@type"].Value = content.Type;
            m_InsertContentsCommand.Parameters["@size"].Value = (long)content.Size;
            m_InsertContentsCommand.Parameters["@offset"].Value = (long)content.Offset;
            m_InsertContentsCommand.Parameters["@source_asset_guid"].Value = content.SourceAssetGUID;
            m_InsertContentsCommand.Parameters["@build_time_asset_path"].Value = content.BuildTimeAssetPath;
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
        m_InsertContentsCommand?.Dispose();
    }
}

