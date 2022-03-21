using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace UnityDataTools.FileSystem.Tests
{
    public class ArchiveTests
    {
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
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "invalidfile");
            var ex = Assert.Throws<NotSupportedException>(() => UnityFileSystem.MountArchive(path, "archive:/"));
            Assert.AreEqual($"Invalid file format reading {path}.", ex.Message);
        }

        [Test]
        public void MountArchive_ValidArchive_ReturnsArchive()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "assetbundle");

            UnityArchive archive = null;
            Assert.DoesNotThrow(() => archive = UnityFileSystem.MountArchive(path, "archive:/"));
            Assert.IsNotNull(archive);

            archive.Dispose();
        }

        [Test]
        [Ignore("This test doesn't return the expected error, this condition is probably not handled correctly in Unity")]
        public void DisposeArchive_ValidArchive_UnmountsArchive()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "assetbundle");
            var archive = UnityFileSystem.MountArchive(path, "archive:/");
            var node = archive.Nodes[0];

            Assert.DoesNotThrow(() => archive.Dispose());
            var ex = Assert.Throws<FileNotFoundException>(() => UnityFileSystem.OpenFile($"archive:/{node.Path}"));

            archive.Dispose();
        }

        [Test]
        public void Nodes_Disposed_ThrowsException()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "assetbundle");
            var archive = UnityFileSystem.MountArchive(path, "archive:/");
            archive.Dispose();
            
            Assert.Throws<ObjectDisposedException>(() => { var _ = archive.Nodes; });
        }

        [Test]
        public void Nodes_ValidArchive_ExpectedContent()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "assetbundle");
            var archive = UnityFileSystem.MountArchive(path, "archive:/");

            var nodes = archive.Nodes;

            Assert.AreEqual(3, nodes.Count);

            Assert.AreEqual("CAB-5d40f7cad7c871cf2ad2af19ac542994", nodes[0].Path);
            Assert.AreEqual(199368, nodes[0].Size);
            Assert.AreEqual(ArchiveNodeFlags.SerializedFile, nodes[0].Flags);

            Assert.AreEqual("CAB-5d40f7cad7c871cf2ad2af19ac542994.resS", nodes[1].Path);
            Assert.AreEqual(2833848, nodes[1].Size);
            Assert.AreEqual(ArchiveNodeFlags.None, nodes[1].Flags);

            Assert.AreEqual("CAB-5d40f7cad7c871cf2ad2af19ac542994.resource", nodes[2].Path);
            Assert.AreEqual(5248, nodes[2].Size);
            Assert.AreEqual(ArchiveNodeFlags.None, nodes[2].Flags);

            archive.Dispose();
        }
    }

    public class UnityFileTests
    {
        private UnityArchive archive;

        [OneTimeSetUp]
        public void Setup()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "assetbundle");
            archive = UnityFileSystem.MountArchive(path, "archive:/");
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            archive.Dispose();
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
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "TextFile.txt");
            UnityFile file = null;

            Assert.DoesNotThrow(() => file = UnityFileSystem.OpenFile(path));
            Assert.IsNotNull(file);

            Assert.DoesNotThrow(() => file.Dispose());
        }

        [Test]
        public void GetFileSize_LocalFileSystem_ReturnSize()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "TextFile.txt");
            var file = UnityFileSystem.OpenFile(path);

            Assert.AreEqual(21, file.GetSize());

            file.Dispose();
        }

        [Test]
        public void GetFileSize_InvalidHandle_ThrowsException()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "TextFile.txt");
            var file = UnityFileSystem.OpenFile(path);
            file.Dispose();

            Assert.Throws<ObjectDisposedException>(() => file.GetSize());
        }

        [Test]
        public void SeekFile_LocalFileSystem_SeekAtExpectedPosition()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "TextFile.txt");
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
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "TextFile.txt");
            var file = UnityFileSystem.OpenFile(path);
            file.Dispose();

            Assert.Throws<ObjectDisposedException>(() => file.Seek(0, SeekOrigin.Begin));
        }

        [Test]
        public void ReadFile_LocalFileSystem_ReadExpectedData()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "TextFile.txt");
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
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "TextFile.txt");
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
            Byte[] expectedBuffer = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 22, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 231, 141, 0, 0, 0, 0, 0, 3, 10, 200, 0, 0, 0, 0, 0, 1, 231, 192, 0, 0, 0, 0, 0, 0, 0, 0, 50, 48, 50, 48, 46, 51, 46, 49, 55, 102, 49, 0, 19, 0, 0, 0, 1, 13, 0, 0, 0, 23, 0, 0, 0, 0, 255, 255, 241, 159, 126, 32, 195, 37, 88, 156, 52, 101, 84, 239, 125, 28, 173, 201, 54, 0, 0, 0, 42, 2, 0, 0 };
            var actualSize = 0L;

            Assert.DoesNotThrow(() => actualSize = file.Read(100, buffer));
            //Console.WriteLine("{{{0}}}", string.Join(", ", buffer));
            Assert.AreEqual(100, actualSize);
            Assert.AreEqual(expectedBuffer, buffer);

            file.Dispose();
        }
    }

    public class SerializedFileTests
    {
        private UnityArchive archive;

        [OneTimeSetUp]
        public void Setup()
        {
            Console.WriteLine("Setup!");
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "assetbundle");
            archive = UnityFileSystem.MountArchive(path, "archive:/");
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            Console.WriteLine("TearDown!");
            archive.Dispose();
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

            Assert.AreEqual(3, file.ExternalReferences.Count);

            file.Dispose();
        }

        [Test]
        public void ExternalReferences_ValidSerializedFile_ExpectedContent()
        {
            var file = UnityFileSystem.OpenSerializedFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994");

            Assert.AreEqual("Library/unity default resources", file.ExternalReferences[0].Path);
            Assert.AreEqual("0000000000000000e000000000000000", file.ExternalReferences[0].Guid);
            Assert.AreEqual(ExternalReferenceType.NonAssetType, file.ExternalReferences[0].Type);

            Assert.AreEqual("Resources/unity_builtin_extra", file.ExternalReferences[1].Path);
            Assert.AreEqual("0000000000000000f000000000000000", file.ExternalReferences[1].Guid);
            Assert.AreEqual(ExternalReferenceType.NonAssetType, file.ExternalReferences[1].Type);

            Assert.AreEqual("archive:/CAB-35fce856128a6714740898681ea54bbe/CAB-35fce856128a6714740898681ea54bbe", file.ExternalReferences[2].Path);
            Assert.AreEqual("00000000000000000000000000000000", file.ExternalReferences[2].Guid);
            Assert.AreEqual(ExternalReferenceType.NonAssetType, file.ExternalReferences[2].Type);

            file.Dispose();
        }

        [Test]
        public void GetObjectCount_ValidSerializedFile_ReturnExpectedObjectCount()
        {
            var file = UnityFileSystem.OpenSerializedFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994");

            Assert.AreEqual(41, file.Objects.Count);

            file.Dispose();
        }

        [Test]
        public void GetObjectInfo_ValidSerializedFile_ReturnExpectedObjectInfo()
        {
            var file = UnityFileSystem.OpenSerializedFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994");

            // Just make sure that first and last ObjectInfo struct are filled.

            Assert.AreEqual(-8720048570983375440, file.Objects[0].Id);
            Assert.AreEqual(124864, file.Objects[0].Offset);
            Assert.AreEqual(156, file.Objects[0].Size);
            Assert.AreEqual(23, file.Objects[0].TypeId);

            Assert.AreEqual(9001362461581137807, file.Objects[40].Id);
            Assert.AreEqual(199320, file.Objects[39].Offset);
            Assert.AreEqual(24, file.Objects[39].Size);
            Assert.AreEqual(33, file.Objects[39].TypeId);

            file.Dispose();
        }
    }

    public class TypeTreeTests
    {
        private UnityArchive archive;
        private SerializedFile serializedFile;

        [OneTimeSetUp]
        public void Setup()
        {
            var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "data", "assetbundle");
            archive = UnityFileSystem.MountArchive(path, "archive:/");

            serializedFile = UnityFileSystem.OpenSerializedFile("archive:/CAB-5d40f7cad7c871cf2ad2af19ac542994");
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            serializedFile.Dispose();
            archive.Dispose();
        }

        [Test]
        public void GetTypeTreeRoot_InvalidObjectId_ThrowsException()
        {
            Assert.Throws<ArgumentException>(() => serializedFile.GetTypeTreeRoot(0));
        }

        [Test]
        public void GetTypeTreeRoot_ValidSerializedFile_ReturnsNode()
        {
            TypeTreeNode node = null;

            Assert.DoesNotThrow(() => node = serializedFile.GetTypeTreeRoot(serializedFile.Objects[0].Id));
            Assert.IsNotNull(node);
        }

        [Test]
        public void GetTypeTreeRoot_ValidSerializedFile_ReturnsValidData()
        {
            foreach (var obj in serializedFile.Objects)
            {
                TypeTreeNode root = null;

                Assert.DoesNotThrow(() => root = serializedFile.GetTypeTreeRoot(obj.Id));
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

            foreach (var obj in serializedFile.Objects)
            {
                var root = serializedFile.GetTypeTreeRoot(obj.Id);

                var count = ProcessNode(root);

                Assert.Greater(count, 1);
            }
        }
    }
}
