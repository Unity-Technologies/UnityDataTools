using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityDataTools.FileSystem;
using UnityDataTools.FileSystem.TypeTreeReaders;

namespace UnityDataTools.FileSystem.Tests;

// Unity 6.7 (SerializedFile version 26) changes how a type tree and an object's data are laid out:
// a compound the file shares between types appears as a single node standing in for it, and the
// [SerializeReference] registry moves into the object's data as a frame no node describes.
//
// The fixtures are two builds of the same project, PlayerWithTypeTrees (version 22) and
// PlayerWithTypeTreesV26, so each test can check that the two report the same content.
public class SerializedFileV26Tests
{
    const int MonoBehaviourClassId = 114;

    string m_V22Folder;
    string m_V26Folder;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var data = Path.Combine(TestContext.CurrentContext.TestDirectory, "Data");
        m_V22Folder = Path.Combine(data, "PlayerWithTypeTrees");
        m_V26Folder = Path.Combine(data, "PlayerWithTypeTreesV26");

        UnityFileSystem.Init();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        UnityFileSystem.Cleanup();
    }

    static ObjectInfo FindObjectOfType(SerializedFile sf, int classId)
    {
        var obj = sf.Objects.FirstOrDefault(o => o.TypeId == classId);
        Assert.That(obj.Size, Is.Not.EqualTo(0), $"No object of ClassID {classId} in the file");
        return obj;
    }

    static void ForEachNode(TypeTreeNode node, int depth, System.Action<TypeTreeNode> action)
    {
        action(node);

        // The trees are recursive through shared subtrees, so the walk is depth limited.
        if (depth == 0)
            return;

        foreach (var child in node.Children)
            ForEachNode(child, depth - 1, action);
    }

    [Test]
    public void TypeTree_Version26_UsesSharedSubtreeReferences()
    {
        using var sf = UnityFileSystem.OpenSerializedFile(Path.Combine(m_V26Folder, "level0"));

        var refs = 0;

        foreach (var obj in sf.Objects)
        {
            ForEachNode(sf.GetTypeTreeRoot(obj.Id), 6, node =>
            {
                if (!node.IsSharedSubtreeRef)
                    return;

                refs++;

                // The failure this guards against is silent: a shared subtree reference has a byte
                // size and no children of its own, which is how a basic type is recognised, so a
                // fixed-size compound would be read as a primitive of the same width and its
                // fields - PPtrs among them - simply dropped.
                Assert.That(node.IsBasicType, Is.False, $"{node.Name} ({node.Type}) read as a basic type");
            });
        }

        Assert.That(refs, Is.GreaterThan(0), "A version 26 file is expected to share its compound subtrees");
    }

    [Test]
    public void TypeTree_Version26_ResolvesPPtrThroughSharedSubtree()
    {
        using var sf = UnityFileSystem.OpenSerializedFile(Path.Combine(m_V26Folder, "level0"));

        TypeTreeNode pptr = null;

        foreach (var obj in sf.Objects)
        {
            ForEachNode(sf.GetTypeTreeRoot(obj.Id), 6, node =>
            {
                if (pptr == null && node.IsSharedSubtreeRef && node.Type.StartsWith("PPtr<"))
                    pptr = node;
            });
        }

        Assert.That(pptr, Is.Not.Null, "Expected a PPtr shared through a subtree");
        Assert.That(pptr.Children.Count, Is.EqualTo(2));
        Assert.That(pptr.Children[0].Name, Is.EqualTo("m_FileID"));
        Assert.That(pptr.Children[1].Name, Is.EqualTo("m_PathID"));
    }

    [Test]
    public void TypeTree_Version22_HasNoSharedSubtreeReferences()
    {
        using var sf = UnityFileSystem.OpenSerializedFile(Path.Combine(m_V22Folder, "level0"));

        foreach (var obj in sf.Objects)
        {
            ForEachNode(sf.GetTypeTreeRoot(obj.Id), 6, node =>
                Assert.That(node.IsSharedSubtreeRef, Is.False, $"{node.Name} ({node.Type}) in a version 22 file"));
        }
    }

    // Reads the one [SerializeReference] instance the fixture holds, whichever registry layout the
    // file uses. The expected values are the same for both builds, since it is the same asset.
    void AssertRegistryContents(string folder, int expectedVersion)
    {
        var path = Path.Combine(folder, "sharedassets1.assets");

        using var sf = UnityFileSystem.OpenSerializedFile(path);
        using var fileReader = new UnityFileReader(path, 1024 * 1024);

        var obj = FindObjectOfType(sf, MonoBehaviourClassId);
        var reader = new RandomAccessReader(sf, sf.GetTypeTreeRoot(obj.Id), fileReader, obj.Offset);

        var registry = reader.Registry;

        Assert.That(registry, Is.Not.Null);
        Assert.That(registry.Version, Is.EqualTo(expectedVersion));
        Assert.That(registry.Entries.Count, Is.EqualTo(1));

        var entry = registry.Entries[0];

        Assert.That(entry.IsNull, Is.False);
        Assert.That(entry.ClassName, Is.EqualTo("Data"));
        Assert.That(entry.Namespace, Is.EqualTo("MyNamespace"));
        Assert.That(entry.AssemblyName, Is.EqualTo("Assembly-CSharp"));

        // The field that points at the instance stores only its rid. Reading the two consistently
        // is what catches a reader that does not step over the registry frame: it would take the
        // frame's first bytes as the rid rather than failing outright.
        Assert.That(reader["reference"]["rid"].GetValue<long>(), Is.EqualTo(entry.Rid));

        // Fields after the registry must not be shifted either.
        Assert.That(reader["m_Name"].GetValue<string>(), Is.EqualTo("ScriptableObjectWIthSerializeReference"));

        var data = new RandomAccessReader(sf,
            sf.GetRefTypeTypeTreeRoot(entry.ClassName, entry.Namespace, entry.AssemblyName),
            fileReader, entry.DataOffset);

        Assert.That(data["Info"].GetValue<string>(), Is.EqualTo("Some info"));
        Assert.That(data["Flag"].GetValue<byte>(), Is.EqualTo(1));
    }

    [Test]
    public void Registry_Version26_ReadsFrameEntries()
    {
        AssertRegistryContents(m_V26Folder, ManagedReferenceRegistry.FrameVersion);
    }

    [Test]
    public void Registry_Version22_ReadsNodeEntriesAsTheSameShape()
    {
        AssertRegistryContents(m_V22Folder, 2);
    }

    [Test]
    public void Registry_ObjectWithoutReferences_IsNull()
    {
        var path = Path.Combine(m_V26Folder, "sharedassets1.assets");

        using var sf = UnityFileSystem.OpenSerializedFile(path);
        using var fileReader = new UnityFileReader(path, 1024 * 1024);

        // PreloadData holds no [SerializeReference] instances.
        var obj = FindObjectOfType(sf, 150);
        var reader = new RandomAccessReader(sf, sf.GetTypeTreeRoot(obj.Id), fileReader, obj.Offset);

        Assert.That(reader.Registry, Is.Null);
    }
}
