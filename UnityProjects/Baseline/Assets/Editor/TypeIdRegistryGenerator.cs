using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// Generates the s_KnownTypes dictionary entries for UnityDataTools' TypeIdRegistry.cs from the
// live engine type list. The authoritative source is the internal UnityEditor.UnityType database,
// which exposes persistentTypeID, name and isEditorOnly for every registered type. Reflection is
// used because UnityType is internal to the UnityEditor assembly.
//
// Runtime (non-editor-only) types are kept. A few editor-only types are force-included because
// UnityDataTools needs them to inspect BuildReports (see s_IncludeEditorOnlyNames). Test/fake
// types that get compiled into a development editor build are dropped (see s_ExcludeNames /
// s_ExcludePatterns) since they don't exist in a shipped editor.
//
// Run from the Editor via Tools/Generate TypeIdRegistry, or headless:
//   Unity.exe -projectPath <thisProject> -batchmode -quit -executeMethod TypeIdRegistryGenerator.Generate
public static class TypeIdRegistryGenerator
{
    // Editor-only types that must be kept because UnityDataTools resolves them when inspecting
    // BuildReports. These don't follow a naming convention, so they are listed explicitly. The
    // set mirrors the IMPLEMENT_REGISTER_CLASS(BuildReporting, ...) macros in
    // Modules/BuildReportingEditor; add new BuildReporting types here when they are introduced.
    static readonly HashSet<string> s_IncludeEditorOnlyNames = new()
    {
        "AudioBuildInfo",
        "BuildReport",
        "BuiltAssetBundleInfoSet",
        "ContentSummary",
        "PackedAssets",
        "PluginBuildInfo",
        "ScenesUsingAssets",
        "VideoBuildInfo",
    };

    // Test/fake types registered by a development editor build that are not real runtime classes
    // and should never appear in the registry.
    static readonly HashSet<string> s_ExcludeNames = new()
    {
        "NativeObjectType",
        "SerializableManagedHost",
        "Derived",
        "SubDerived",
        "SiblingDerived",
        "EmptyObject",
        "BlobObject",
    };

    // Name substrings that identify test scaffolding types to drop.
    static readonly string[] s_ExcludePatterns = { "Test", "Fake", "Mock", "Dummy" };

    [MenuItem("Tools/Generate TypeIdRegistry")]
    public static void Generate()
    {
        // UnityType lives in the same assembly as UnityEditor.Editor.
        Type unityType = typeof(Editor).Assembly.GetType("UnityEditor.UnityType");
        if (unityType == null)
        {
            Debug.LogError("TypeIdRegistryGenerator: could not find type UnityEditor.UnityType.");
            return;
        }

        MethodInfo getTypes = unityType.GetMethod("GetTypes", BindingFlags.Public | BindingFlags.Static);
        if (getTypes == null)
        {
            Debug.LogError("TypeIdRegistryGenerator: could not find UnityType.GetTypes().");
            return;
        }

        PropertyInfo idProp = unityType.GetProperty("persistentTypeID");
        PropertyInfo nameProp = unityType.GetProperty("name");
        PropertyInfo editorOnlyProp = unityType.GetProperty("isEditorOnly");
        if (idProp == null || nameProp == null || editorOnlyProp == null)
        {
            Debug.LogError("TypeIdRegistryGenerator: UnityType is missing an expected property (persistentTypeID/name/isEditorOnly).");
            return;
        }

        var entries = new List<(int id, string name)>();
        foreach (var type in (IEnumerable)getTypes.Invoke(null, null))
        {
            var id = (int)idProp.GetValue(type);
            var name = (string)nameProp.GetValue(type);

            // Id 0 (Object) is treated as an error condition by callers, not a real type to map.
            if (id == 0 || string.IsNullOrEmpty(name))
                continue;

            if (IsExcluded(name))
                continue;

            if ((bool)editorOnlyProp.GetValue(type) && !s_IncludeEditorOnlyNames.Contains(name))
                continue;

            entries.Add((id, name));
        }

        entries.Sort((a, b) => a.id.CompareTo(b.id));

        var sb = new StringBuilder();
        int lastId = int.MinValue;
        foreach (var (id, name) in entries)
        {
            if (id == lastId)
                Debug.LogWarning($"TypeIdRegistryGenerator: duplicate persistentTypeID {id} (\"{name}\").");
            lastId = id;

            sb.AppendLine($"        {{ {id}, \"{name}\" }},");
        }

        var outPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "TypeIdRegistry.block.txt");
        File.WriteAllText(outPath, sb.ToString());

        Debug.Log($"TypeIdRegistryGenerator: wrote {entries.Count} entries to {outPath}");
    }

    static bool IsExcluded(string name)
    {
        if (s_ExcludeNames.Contains(name))
            return true;

        foreach (var pattern in s_ExcludePatterns)
        {
            if (name.Contains(pattern))
                return true;
        }

        return false;
    }
}
