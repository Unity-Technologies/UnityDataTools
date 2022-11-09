using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityDataTools.TestCommon;

namespace UnityDataTools.FileSystem.Tests;

#pragma warning disable NUnit2005, NUnit2006

public class ArchiveTests : AssetBundleTestFixture
{
    public ArchiveTests(Context context) : base(context)
    {
    }
    
    protected override void OnLoadExpectedData(Context context)
    {
        // Uncomment to regenerate expected data.
        //ExpectedDataGenerator.Generate(context);
    }
    
    [OneTimeSetUp]
    public void Setup()
    {
        UnityFileSystem.Init();
    }
    
    [OneTimeTearDown]
    public void TearDown()
    {
        UnityFileSystem.Cleanup();
    }

    [Test]
    public void MountArchive_InvalidPath_ThrowsException()
    {
        var ex = Assert.Throws<FileNotFoundException>(() => UnityFileSystem.MountArchive("bad/path", "archive:/"));
        Assert.AreEqual("File not found.", ex.Message);
        Assert.AreEqual("bad/path", ex.FileName);
    }

    [Test]
    public void MountArchive_InvalidArchive_ThrowsException()
    {
        var path = Path.Combine(Context.TestDataFolder, "invalidfile");
        var ex = Assert.Throws<NotSupportedException>(() => UnityFileSystem.MountArchive(path, "archive:/"));
        Assert.AreEqual($"Invalid file format reading {path}.", ex.Message);
    }
        
    public void MountArchive_ValidArchive_ReturnsArchive()
    {
        var path = Path.Combine(Context.UnityDataFolder, "assetbundle");

        UnityArchive archive = null;
        Assert.DoesNotThrow(() => archive = UnityFileSystem.MountArchive(path, "archive:/"));
        Assert.IsNotNull(archive);

        archive.Dispose();
    }
    
    [Ignore("This test doesn't return the expected error, this condition is probably not handled correctly in Unity")]
    public void DisposeArchive_ValidArchive_UnmountsArchive()
    {
        var path = Path.Combine(Context.UnityDataFolder, "assetbundle");
        var archive = UnityFileSystem.MountArchive(path, "archive:/");
        var node = archive.Nodes[0];

        Assert.DoesNotThrow(() => archive.Dispose());
        var ex = Assert.Throws<FileNotFoundException>(() => UnityFileSystem.OpenFile($"archive:/{node.Path}"));

        archive.Dispose();
    }
    
    public void Nodes_Disposed_ThrowsException()
    {
        var path = Path.Combine(Context.UnityDataFolder, "assetbundle");
        var archive = UnityFileSystem.MountArchive(path, "archive:/");
        archive.Dispose();
            
        Assert.Throws<ObjectDisposedException>(() => { var _ = archive.Nodes; });
    }
    
    public void Nodes_ValidArchive_ExpectedContent(string testFolder)
    {
        var path = Path.Combine(testFolder, "AssetBundles", "assetbundle");
        var archive = UnityFileSystem.MountArchive(path, "archive:/");

        var nodes = archive.Nodes;

        Assert.AreEqual(Context.ExpectedData.Get("NodeCount"), nodes.Count);

        Assert.AreEqual("CAB-5d40f7cad7c871cf2ad2af19ac542994", nodes[0].Path);
        Assert.AreEqual(Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994-Size"), nodes[0].Size);
        Assert.AreEqual((ArchiveNodeFlags)Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994-Flags"), nodes[0].Flags);

        Assert.AreEqual("CAB-5d40f7cad7c871cf2ad2af19ac542994.resS", nodes[1].Path);
        Assert.AreEqual(Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994.resS-Size"), nodes[1].Size);
        Assert.AreEqual((ArchiveNodeFlags)Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994.resS-Flags"), nodes[1].Flags);

        Assert.AreEqual("CAB-5d40f7cad7c871cf2ad2af19ac542994.resource", nodes[2].Path);
        Assert.AreEqual(Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994.resource-Size"), nodes[2].Size);
        Assert.AreEqual((ArchiveNodeFlags)Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994.resource-Flags"), nodes[2].Flags);

        archive.Dispose();
    }
}

public class UnityFileTests : AssetBundleTestFixture
{
    private UnityArchive m_Archive;

    public UnityFileTests(Context context) : base(context)
    {
    }

    [OneTimeSetUp]
    public void Setup()
    {
        UnityFileSystem.Init();
            
        var path = Path.Combine(Context.UnityDataFolder, "assetbundle");
        m_Archive = UnityFileSystem.MountArchive(path, "archive:/");
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        m_Archive.Dispose();
            
        UnityFileSystem.Cleanup();
    }

    [Test]
    public void OpenFile_InvalidPath_ThrowsException()
    {
        var ex = Assert.Throws<FileNotFoundException>(() => UnityFileSystem.OpenFile("bad/path"));

        Assert.AreEqual("File not found.", ex.Message);
        Assert.AreEqual("bad/path", ex.FileName);
    }

    [Test]
    public void OpenFile_LocalFileSystem_ReturnsValidFile()
    {
        var path = Path.Combine(Context.TestDataFolder, "TextFile.txt");
        UnityFile file = null;

        Assert.DoesNotThrow(() => file = UnityFileSystem.OpenFile(path));
        Assert.IsNotNull(file);

        Assert.DoesNotThrow(() => file.Dispose());
    }

    [Test]
    public void GetFileSize_LocalFileSystem_ReturnSize()
    {
        var path = Path.Combine(Context.TestDataFolder, "TextFile.txt");
        var file = UnityFileSystem.OpenFile(path);

        Assert.AreEqual(21, file.GetSize());

        file.Dispose();
    }

    [Test]
    public void GetFileSize_InvalidHandle_ThrowsException()
    {
        var path = Path.Combine(Context.TestDataFolder, "TextFile.txt");
        var file = UnityFileSystem.OpenFile(path);
        file.Dispose();

        Assert.Throws<ObjectDisposedException>(() => file.GetSize());
    }

    [Test]
    public void SeekFile_LocalFileSystem_SeekAtExpectedPosition()
    {
        var path = Path.Combine(Context.TestDataFolder, "TextFile.txt");
        var file = UnityFileSystem.OpenFile(path);
        var newPos = 0L;
        var buffer = new Byte[16];
        var actualSize = 0L;

        Assert.DoesNotThrow(() => newPos = file.Seek(16, SeekOrigin.Begin));
        Assert.AreEqual(16, newPos);

        actualSize = file.Read(4, buffer);
        Assert.AreEqual(4, actualSize);
        Assert.AreEqual("file", Encoding.Default.GetString(buffer, 0, (int)actualSize));

        Assert.DoesNotThrow(() => newPos = file.Seek(-4, SeekOrigin.Current));
        Assert.AreEqual(16, newPos);

        buffer = new Byte[16];
        actualSize = file.Read(4, buffer);
        Assert.AreEqual(4, actualSize);
        Assert.AreEqual("file", Encoding.Default.GetString(buffer, 0, (int)actualSize));

        Assert.DoesNotThrow(() => newPos = file.Seek(-5, SeekOrigin.End));
        Assert.AreEqual(16, newPos);

        actualSize = file.Read(4, buffer);
        Assert.AreEqual(4, actualSize);
        Assert.AreEqual("file", Encoding.Default.GetString(buffer, 0, (int)actualSize));

        file.Dispose();
    }

    [Test]
    public void SeekFile_InvalidHandle_ThrowsException()
    {
        var path = Path.Combine(Context.TestDataFolder, "TextFile.txt");
        var file = UnityFileSystem.OpenFile(path);
        file.Dispose();

        Assert.Throws<ObjectDisposedException>(() => file.Seek(0, SeekOrigin.Begin));
    }

    [Test]
    public void ReadFile_LocalFileSystem_ReadExpectedData()
    {
        var path = Path.Combine(Context.TestDataFolder, "TextFile.txt");
        var file = UnityFileSystem.OpenFile(path);
        var buffer = new Byte[1000];
        var actualSize = file.Read(1000, buffer);

        Assert.AreEqual(21, actualSize);
        Assert.AreEqual("This is my text file.", Encoding.Default.GetString(buffer, 0, (int)actualSize));

        file.Dispose();
    }

    [Test]
    public void ReadFile_InvalidHandle_ThrowsException()
    {
        var path = Path.Combine(Context.TestDataFolder, "TextFile.txt");
        var file = UnityFileSystem.OpenFile(path);
        file.Dispose();

        Assert.Throws<ObjectDisposedException>(() => file.Read(10, new byte[10]));
    }

    [Test]
    public void OpenFile_ArchiveFileSystem_ReturnsFile()
    {
        UnityFile file = null;

        Assert.DoesNotThrow(() => file = UnityFileSystem.OpenFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994"));
        Assert.IsNotNull(file);

        file.Dispose();
    }

    [Test]
    public void ReadFile_ArchiveFileSystem_ReadExpectedData()
    {
        var file = UnityFileSystem.OpenFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994");
        var buffer = new Byte[100];
        var actualSize = 0L;

        Assert.DoesNotThrow(() => actualSize = file.Read(100, buffer));
        Assert.AreEqual(100, actualSize);
        Assert.AreEqual(Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994-Data"), buffer);

        file.Dispose();
    }
}

public class SerializedFileTests : AssetBundleTestFixture
{
    private UnityArchive m_Archive;

    public SerializedFileTests(Context context) : base(context)
    {
    }

    [OneTimeSetUp]
    public void Setup()
    {
        UnityFileSystem.Init();
            
        var path = Path.Combine(Context.UnityDataFolder, "assetbundle");
        m_Archive = UnityFileSystem.MountArchive(path, "archive:/");
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        m_Archive.Dispose();
            
        UnityFileSystem.Cleanup();
    }

    [Test]
    public void OpenSerializedFile_InvalidPath_ThrowsException()
    {
        var ex = Assert.Throws<FileNotFoundException>(() => UnityFileSystem.OpenSerializedFile("bad/path"));
        Assert.AreEqual("File not found.", ex.Message);
        Assert.AreEqual("bad/path", ex.FileName);
    }

    [Test]
    [Ignore("This test crashes, this condition is not handled properly in Unity")]
    public void OpenSerializedFile_NotSerializedFile_ThrowsException()
    {
        var ex = Assert.Throws<Exception>(() => UnityFileSystem.OpenSerializedFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994.resS"));
    }

    [Test]
    public void OpenSerializedFile_ValidSerializedFile_ReturnsFile()
    {
        SerializedFile file = null;
            
        Assert.DoesNotThrow(() => file = UnityFileSystem.OpenSerializedFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994"));
        Assert.IsNotNull(file);

        file.Dispose();
    }

    [Test]
    public void ExternalReferences_Disposed_ThrowsException()
    {
        var file = UnityFileSystem.OpenSerializedFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994");
        file.Dispose();

        Assert.Throws<ObjectDisposedException>(() => { var _ = file.ExternalReferences.Count; });
    }

    [Test]
    public void Objects_Disposed_ThrowsException()
    {
        var file = UnityFileSystem.OpenSerializedFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994");
        file.Dispose();

        Assert.Throws<ObjectDisposedException>(() => { var _ = file.Objects.Count; });
    }

    [Test]
    public void GetTypeTreeRoot_Disposed_ThrowsException()
    {
        var file = UnityFileSystem.OpenSerializedFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994");
        file.Dispose();

        Assert.Throws<ObjectDisposedException>(() => { var _ = file.GetTypeTreeRoot(0); });
    }

    [Test]
    public void ExternalReferences_ValidSerializedFile_ReturnExpectedExternalReferenceCount()
    {
        var file = UnityFileSystem.OpenSerializedFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994");

        Assert.AreEqual(Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994-ExtRefCount"), file.ExternalReferences.Count);

        file.Dispose();
    }

    [Test]
    public void ExternalReferences_ValidSerializedFile_ExpectedContent()
    {
        var file = UnityFileSystem.OpenSerializedFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994");

        int i = 0;
        foreach (var extRef in file.ExternalReferences)
        {
            Assert.AreEqual(Context.ExpectedData.Get($"CAB-5d40f7cad7c871cf2ad2af19ac542994-ExtRef{i}-Guid"), extRef.Guid);
            Assert.AreEqual(Context.ExpectedData.Get($"CAB-5d40f7cad7c871cf2ad2af19ac542994-ExtRef{i}-Path"), extRef.Path);
            Assert.AreEqual(Context.ExpectedData.Get($"CAB-5d40f7cad7c871cf2ad2af19ac542994-ExtRef{i}-Type"), (long)extRef.Type);
            ++i;
        }

        file.Dispose();
    }

    [Test]
    public void GetObjectCount_ValidSerializedFile_ReturnExpectedObjectCount()
    {
        var file = UnityFileSystem.OpenSerializedFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994");

        Assert.AreEqual(Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994-ObjCount"), file.Objects.Count);

        file.Dispose();
    }

    [Test]
    public void GetObjectInfo_ValidSerializedFile_ReturnExpectedObjectInfo()
    {
        var file = UnityFileSystem.OpenSerializedFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994");

        // Just make sure that first and last ObjectInfo struct are filled.

        Assert.AreEqual(Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994-FirstObj-Id"), file.Objects.First().Id);
        Assert.AreEqual(Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994-FirstObj-Offset"), file.Objects.First().Offset);
        Assert.AreEqual(Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994-FirstObj-Size"), file.Objects.First().Size);
        Assert.AreEqual(Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994-FirstObj-TypeId"), file.Objects.First().TypeId);

        Assert.AreEqual(Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994-LastObj-Id"), file.Objects.Last().Id);
        Assert.AreEqual(Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994-LastObj-Offset"), file.Objects.Last().Offset);
        Assert.AreEqual(Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994-LastObj-Size"), file.Objects.Last().Size);
        Assert.AreEqual(Context.ExpectedData.Get("CAB-5d40f7cad7c871cf2ad2af19ac542994-LastObj-TypeId"), file.Objects.Last().TypeId);

        file.Dispose();
    }
}

public class TypeTreeTests : AssetBundleTestFixture
{
    private UnityArchive m_Archive;
    private SerializedFile m_SerializedFile;

    public TypeTreeTests(Context context) : base(context)
    {
    }

    [OneTimeSetUp]
    public void Setup()
    {
        UnityFileSystem.Init();
            
        var path = Path.Combine(Context.UnityDataFolder, "assetbundle");
        m_Archive = UnityFileSystem.MountArchive(path, "archive:/");

        m_SerializedFile = UnityFileSystem.OpenSerializedFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994");
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        m_SerializedFile.Dispose();
        m_Archive.Dispose();
            
        UnityFileSystem.Cleanup();
    }

    [Test]
    public void GetTypeTreeRoot_InvalidObjectId_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => m_SerializedFile.GetTypeTreeRoot(0));
    }

    [Test]
    public void GetTypeTreeRoot_ValidSerializedFile_ReturnsNode()
    {
        TypeTreeNode node = null;

        Assert.DoesNotThrow(() => node = m_SerializedFile.GetTypeTreeRoot(m_SerializedFile.Objects[0].Id));
        Assert.IsNotNull(node);
    }

    [Test]
    public void GetTypeTreeRoot_ValidSerializedFile_ReturnsValidData()
    {
        foreach (var obj in m_SerializedFile.Objects)
        {
            TypeTreeNode root = null;

            Assert.DoesNotThrow(() => root = m_SerializedFile.GetTypeTreeRoot(obj.Id));
            Assert.IsNotNull(root);
            Assert.AreNotEqual("", root.Type);
            Assert.AreEqual("Base", root.Name);
            Assert.AreEqual(-1, root.Offset);
            Assert.AreNotEqual(0, root.Size);
            Assert.AreEqual(TypeTreeFlags.None, root.Flags);
        }
    }

    [Test]
    public void TypeTreeNode_IterateAll_ReturnExpectedValues()
    {
        int ProcessNode(TypeTreeNode node)
        {
            int count = 1;

            Assert.IsNotNull(node);
            Assert.AreNotEqual("", node.Type);
            Assert.AreNotEqual("", node.Name);
            Assert.GreaterOrEqual(node.Offset, -1);
            Assert.GreaterOrEqual(node.Size, -1);
            Assert.True(node.IsLeaf == (node.Children.Count == 0));

            foreach (var child in node.Children)
            {
                count += ProcessNode(child);
            }

            return count;
        }

        foreach (var obj in m_SerializedFile.Objects)
        {
            var root = m_SerializedFile.GetTypeTreeRoot(obj.Id);

            var count = ProcessNode(root);

            Assert.Greater(count, 1);
        }
    }

    [Test]
    public void GetRefTypeTypeTree_InvalidFQN_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => m_SerializedFile.GetRefTypeTypeTreeRoot("this", "is", "wrong"));
    }

    [Test]
    public void GetRefTypeTree_ValidSerializedFile_ReturnNode()
    {
        TypeTreeNode node = null;
            
        Assert.DoesNotThrow(() => node = m_SerializedFile.GetRefTypeTypeTreeRoot("SerializeReferencePolymorphismExample/Apple", "", "Assembly-CSharp"));
        Assert.NotNull(node);
    }

    [Test]
    public void GetTypeTreeNodeInfo_RefTypeTypeTree_ReturnExpectedValues()
    {
        var node = m_SerializedFile.GetRefTypeTypeTreeRoot("SerializeReferencePolymorphismExample/Apple", "",
            "Assembly-CSharp");

        Assert.AreEqual(2, node.Children.Count);
        Assert.AreEqual("Apple", node.Type);
        Assert.AreEqual("Base", node.Name);
            
        Assert.AreEqual("int", node.Children[0].Type);
        Assert.AreEqual("m_Data", node.Children[0].Name);
        Assert.AreEqual(4, node.Children[0].Size);
            
        Assert.AreEqual("string", node.Children[1].Type);
        Assert.AreEqual("m_Description", node.Children[1].Name);
    }
}

public class RandomAccessReaderTests : AssetBundleTestFixture
{
    private UnityArchive m_Archive;
    private SerializedFile m_SerializedFile;
    private UnityFileReader m_Reader;

    public RandomAccessReaderTests(Context context) : base(context)
    {
    }

    [OneTimeSetUp]
    public void Setup()
    {
        UnityFileSystem.Init();

        var path = Path.Combine(Context.UnityDataFolder, "assetbundle");
        m_Archive = UnityFileSystem.MountArchive(path, "archive:/");

        m_SerializedFile = UnityFileSystem.OpenSerializedFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994");
        m_Reader = new UnityFileReader("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994", 1024*1024);
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        m_Reader.Dispose();
        m_SerializedFile.Dispose();
        m_Archive.Dispose();

        UnityFileSystem.Cleanup();
    }

    ObjectInfo GetObjectInfo(long id)
    {
        ObjectInfo obj = new ObjectInfo();
        int i;

        for (i = 0; i < m_SerializedFile.Objects.Count; ++i)
        {
            obj = m_SerializedFile.Objects[i];
                
            if (obj.Id == id)
            {
                break;
            }
        }
            
        Assert.Less(i, m_SerializedFile.Objects.Count);

        return obj;
    }

    [Test]
    public void AccessProperty_ValidProperty_ReturnExpectedValues()
    {
        var obj = GetObjectInfo(-7865028809519950684);
            
        var root = m_SerializedFile.GetTypeTreeRoot(obj.Id);
        var reader = new TypeTreeReaders.RandomAccessReader(m_SerializedFile, root, m_Reader, obj.Offset);
            
        Assert.AreEqual("Lame", reader["m_Name"].GetValue<string>());
        Assert.AreEqual(228, reader["m_SubMeshes"][0]["vertexCount"].GetValue<UInt32>());
        Assert.AreEqual(false, reader["m_IsReadable"].GetValue<bool>());
    }
        
    [Test]
    public void AccessProperty_InvalidProperty_ThrowException()
    {
        var obj = GetObjectInfo(-7865028809519950684);
            
        var root = m_SerializedFile.GetTypeTreeRoot(obj.Id);
        var reader = new TypeTreeReaders.RandomAccessReader(m_SerializedFile, root, m_Reader, obj.Offset);
            
        Assert.Throws<KeyNotFoundException>(() => reader["ThisIsAnUnexistingPropertyName"].GetValue<string>());
    }
        
    [Test]
    public void AccessReferencedObject_ValidProperty_ReturnExpectedValues()
    {
        var obj = GetObjectInfo(-4606375687431940004);
            
        var root = m_SerializedFile.GetTypeTreeRoot(obj.Id);
        var reader = new TypeTreeReaders.RandomAccessReader(m_SerializedFile, root, m_Reader, obj.Offset);

        long id0;
        long id1;

        // ManagedReferencesRegistry Version 1
        if (reader["m_Item"].HasChild("id"))
        {
            id0 = reader["m_Item"]["id"].GetValue<int>();
            id1 = reader["m_Item2"]["id"].GetValue<int>();
        }
        // ManagedReferencesRegistry Version 2
        else
        {
            id0 = reader["m_Item"]["rid"].GetValue<long>();
            id1 = reader["m_Item2"]["rid"].GetValue<long>();
        }
            
        Assert.IsTrue(reader["references"].HasChild($"rid({id0})"));
        Assert.IsTrue(reader["references"].HasChild($"rid({id1})"));
            
        Assert.AreEqual(1, reader["references"][$"rid({id0})"]["data"]["m_Data"].GetValue<int>());
        Assert.AreEqual("Ripe", reader["references"][$"rid({id0})"]["data"]["m_Description"].GetValue<string>());
        Assert.AreEqual(1, reader["references"][$"rid({id1})"]["data"]["m_Data"].GetValue<int>());
        Assert.AreEqual(1, reader["references"][$"rid({id1})"]["data"]["m_IsRound"].GetValue<byte>());
    }
}
