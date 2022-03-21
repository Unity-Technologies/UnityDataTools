using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.Processors
{
    public class AssetBundleProcessor : IProcessor, IDisposable
    {
        SQLiteCommand m_InsertCommand;

        public void Init(SQLiteConnection db)
        {
            using var command = new SQLiteCommand(db);

            command.CommandText = Properties.Resources.AssetBundle;
            command.ExecuteNonQuery();

            m_InsertCommand = new SQLiteCommand(db);
            m_InsertCommand.CommandText = "INSERT INTO assets(object, name) VALUES(@object, @name)";
            m_InsertCommand.Parameters.Add("@object", DbType.Int64);
            m_InsertCommand.Parameters.Add("@name", DbType.String);
        }

        public void Process(AnalyzerTool analyzer, long objectId, Dictionary<int, int> localToDbFileId, RandomAccessReader reader, out string name, out long streamedDataSize)
        {
            streamedDataSize = 0;

            foreach (var asset in reader["m_Container"])
            {
                var pptr = asset["second"]["asset"];
                var fileId = localToDbFileId[pptr["m_FileID"].GetValue<int>()];
                m_InsertCommand.Parameters["@object"].Value = analyzer.GetObjectId(fileId, pptr["m_PathID"].GetValue<long>());
                m_InsertCommand.Parameters["@name"].Value = asset["first"].GetValue<string>();
                m_InsertCommand.ExecuteNonQuery();
            }

            name = reader["m_Name"].GetValue<string>();
        }

        void IDisposable.Dispose()
        {
            m_InsertCommand.Dispose();
        }
    }
}
