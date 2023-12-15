using System;
using System.Collections.Generic;
using UnityDataTools.Analyzer.SerializedObjects;
using UnityDataTools.FileSystem;

namespace UnityDataTools.Analyzer;

public interface IWriter : IDisposable
{
    void Begin();
    void BeginAssetBundle(string name, long size);
    void EndAssetBundle();
    void WriteSerializedFile(string relativePath, string fullPath, string containingFolder);
    void End();
}
