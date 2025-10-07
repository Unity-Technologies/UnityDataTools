using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace UnityDataTools.Analyzer.Build
{
    public class BuildLayout
    {
        /// <summary>
        /// Build Platform Addressables build is targeting
        /// </summary>
        public int BuildTarget;

        /// <summary>
        /// Hash of the build results
        /// </summary>
        public string BuildResultHash;

        /// <summary>
        /// If the build was a new build or an update for a previous build
        /// </summary>
        public int BuildType;

        /// <summary>
        /// DateTime at the start of building Addressables
        /// </summary>
        public string BuildStartTime;

        /// <summary>
        /// Time in seconds taken to build Addressables Content
        /// </summary>
        public double Duration;

        /// <summary>
        /// Null or Empty if the build completed successfully, else contains error causing the failure
        /// </summary>
        public string BuildError;

        /// <summary>
        /// Version of the Unity edtior used to perform the build.
        /// </summary>
        public string UnityVersion;

        /// <summary>
        /// Version of the Addressables package used to perform the build.
        /// </summary>
        public string PackageVersion;

        /// <summary>
        /// Player build version for the build, this is a timestamp if PlayerVersionOverride is not set in the settings
        /// </summary>
        public string PlayerBuildVersion;

        /// <summary>
        /// Name of the build script to build
        /// </summary>
        public string BuildScript;

        public References references;

    }

    public class References
    {
        public List<Reference> RefIds = new();
    }

    public class ReferenceId
    {
        public int rid;
    }

    public class Reference : ReferenceId
    {
        public ReferenceData data;

        public ReferenceType type;
    }

    public class ReferenceData
    {
        public ReferenceId Bundle;
        public ReferenceId File;
        public AssetHash AssetHash;
        public string AssetPath;
        public string AddressableName;
        public string AddressableGuid;
        public ReferenceId[] ExternallyReferencedAssets;
        public string GroupGuid;
        public string Guid;
        public string InternalId;
        public ReferenceId[] InternalReferencedExplicitAssets;
        public ReferenceId[] InternalReferencedOtherAssets;
        public string[] Labels;
        public int MainAssetType;
        public int StreamedSize;
        public int SerializedSize;
        public string Name;
        public string CRC;

        // For BuildLayout/Bundle
        public int AssetCount;
        public int BuildStatus;
        public ReferenceId[] BundleDependencies;
        public string Compression;
        public ReferenceId[] Dependencies;
        public int DependencyFileSize;
        public ReferenceId[] DependentBundles;
        public ReferenceId[] ExpandedDependencies;
        public int ExpandedDependencyFileSize;
        public int FileSize;
        public ReferenceId[] Files;
        public ReferenceId Group;
        public object Hash; // Complex object containing Hash and serializedVersion
        public string InternalName;
        public string LoadPath;
        public string Provider;
        public string ResultType;

        // For BuildLayout/DataFromOtherAsset
        public string AssetGuid;
        public int ObjectCount;
        public AssetObject[] Objects; // Array of object data
        public ReferenceId[] ReferencingAssets;

        // For BuildLayout/File  
        public ReferenceId[] Assets;
        public BundleObjectInfo BundleObjectInfo; // Object with Size property
        public ReferenceId[] ExternalReferences;
        public int MonoScriptCount;
        public int MonoScriptSize;
        public ReferenceId[] OtherAssets;
        public int PreloadInfoSize;
        public ReferenceId[] SubFiles;
        public string WriteResultFilename;

        // For BuildLayout/Group
        public ReferenceId[] Bundles;
        public string PackingMode;
        public ReferenceId[] Schemas;

        // For BuildLayout/SchemaData
        public SchemaDataPair[] SchemaDataPairs; // Array of Key-Value pairs
        public string Type;

        // For BuildLayout/SubFile
        public bool IsSerializedFile;
        public int Size;
    }

    public class ReferenceType
    {
        public string Asm;
        public string Class;
        public string NS;
    }

    public class AssetHash
    {
        public string Hash;
        public string serializedVersion;
    }

    public class BundleObjectInfo
    {
        public int Size;
    }

    public class AssetObject
    {
        public int AssetType;
        public string ComponentName;
        public long LocalIdentifierInFile;
        public string ObjectName;
        public int SerializedSize;
        public ObjectReference[] References; // Array of object references
        public int StreamedSize;
    }

    public class ObjectReference
    {
        public int AssetId;
        public int ObjectId;
    }

    public class SchemaDataPair
    {
        public string Key;
        public string Value;
    }
}
