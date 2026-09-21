using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityDataTools.Models;

namespace UnityDataTools.Analyzer.Util
{
    // Source data of a v2 loadable that has no place in the current ContentLayout model, kept for
    // the v2-only database columns. Aligned by index with ContentLayout.LoadableObjectIds.
    internal record LoadableObjectV2Data(string AssetPath, long SourceLfid);

    // Converts a version 2 ContentLayout (Unity 6.6) into the current in-memory model so a single
    // import path handles both versions. v3 only removed and reorganized data, so the conversion
    // is exact: hash-based references become indices and each ContentHash becomes the index of the
    // matching binary artifact. The v2-only source fields are returned separately.
    internal static class ContentLayoutV2Upgrader
    {
        private const string CfidExtension = ".cfid";

        public static ContentLayout Upgrade(Models.V2.ContentLayout v2, out LoadableObjectV2Data[] v2Data)
        {
            var artifactIndexByHash = (v2.BinaryArtifacts ?? [])
                .Where(a => a.Category == Models.V2.BuildArtifactCategory.ContentFile)
                .ToDictionary(a => a.ContentHash, a => a.Index);

            var loadables = v2.LoadableObjectIds ?? [];
            var loadableIndexByHash = new Dictionary<string, int>(loadables.Length);
            for (int i = 0; i < loadables.Length; ++i)
            {
                loadableIndexByHash.Add(loadables[i].ObjectIdHash, i);
            }

            var layout = new ContentLayout
            {
                Version = v2.Version,
                BuildManifestHash = v2.BuildManifestHash,
                SerializedFiles = (v2.SerializedFiles ?? []).Select(f => new SerializedFileLayout
                {
                    Index = f.Index,
                    StableId = StripCfidExtension(f.ID),
                    IsBuiltIn = f.IsBuiltIn,
                    SourceAssets = f.SourceAssets,
                    SerializedFileDependencies = f.SerializedFileDependencies,
                    LoadableDependencies = f.LoadableDependencies?
                        .Select(hash => ResolveLoadable(loadableIndexByHash, hash)).ToArray(),
                    LoadableSceneDependencies = f.LoadableSceneDependencies,
                    ArtifactIndex = ResolveArtifact(artifactIndexByHash, f),
                }).ToArray(),
                RootAssets = v2.RootAssets?
                    .Select(hash => ResolveLoadable(loadableIndexByHash, hash)).ToArray(),
                LoadableObjectIds = loadables.Select(l => new LoadableObjectIdLayout
                {
                    GUID = l.GUID,
                    // The current definition of LFID: the output-file lfid when the object was
                    // placed in the build, the source lfid otherwise.
                    LFID = l.SerializedFile >= 0 ? l.OutputLFID : l.LFID,
                    IdentifierType = l.IdentifierType,
                    SerializedFile = l.SerializedFile,
                }).ToArray(),
                LoadableSceneIds = (v2.LoadableSceneIds ?? []).Select(s => new LoadableSceneIdLayout
                {
                    GUID = s.GUID,
                    Path = s.Path,
                    SerializedFile = s.SerializedFile,
                }).ToArray(),
                BinaryArtifacts = (v2.BinaryArtifacts ?? []).Select(a => new BinaryArtifact
                {
                    Index = a.Index,
                    ContentHash = a.ContentHash,
                    Category = a.Category,
                    Size = a.Size,
                    ArtifactReferences = a.ArtifactReferences,
                }).ToArray(),
            };

            v2Data = loadables.Select(l => new LoadableObjectV2Data(l.AssetPath, l.LFID)).ToArray();
            return layout;
        }

        private static int ResolveLoadable(Dictionary<string, int> loadableIndexByHash, string objectIdHash)
        {
            if (!loadableIndexByHash.TryGetValue(objectIdHash, out var index))
            {
                throw new InvalidDataException(
                    $"ObjectIdHash \"{objectIdHash}\" has no matching LoadableObjectIds entry.");
            }

            return index;
        }

        private static int ResolveArtifact(Dictionary<string, int> artifactIndexByHash, Models.V2.SerializedFileLayout file)
        {
            // Built-in entries are not produced by the build and have no ContentHash.
            if (string.IsNullOrEmpty(file.ContentHash))
                return -1;

            if (!artifactIndexByHash.TryGetValue(file.ContentHash, out var index))
            {
                throw new InvalidDataException(
                    $"SerializedFile {file.Index} has ContentHash \"{file.ContentHash}\" with no matching contentfile BinaryArtifact.");
            }

            return index;
        }

        // v2 IDs carry a ".cfid" extension (built-in entries use a path instead); the current
        // schema uses the bare identity hash, so strip it for a uniform stable_id column.
        private static string StripCfidExtension(string id)
        {
            return id != null && id.EndsWith(CfidExtension, StringComparison.Ordinal)
                ? id.Substring(0, id.Length - CfidExtension.Length)
                : id;
        }
    }
}
