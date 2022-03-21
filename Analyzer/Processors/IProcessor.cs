using System.Collections.Generic;
using System.Data.SQLite;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.Analyzer.Processors
{
    public interface IProcessor
    {
        void Init(SQLiteConnection db);
        void Process(AnalyzerTool analyzer, long objectId, Dictionary<int, int> localToDbFileId, RandomAccessReader reader, out string name, out long streamedDataSize);
    }
}
