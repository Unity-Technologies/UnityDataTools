using System;
using System.IO;

namespace UnityDataTools.FileSystem
{
    // This is the main entry point. Provides methods to mount archives and open files.
    public static class UnityFileSystem
    {
        static UnityFileSystem()
        {
            // Initialize the native library.
            var r = DllWrapper.Init();
            HandleErrors(r);
            
            // TODO: should Cleanup be called by the AppDomain.Unload event or something else?
        }

        public static UnityArchive MountArchive(string path, string mountPoint)
        {
            var r = DllWrapper.MountArchive(path, mountPoint, out var handle);
            HandleErrors(r, path);

            return new UnityArchive() { m_Handle = handle };
        }

        public static UnityFile OpenFile(string path)
        {
            var r = DllWrapper.OpenFile(path, out var handle);
            UnityFileSystem.HandleErrors(r, path);

            return new UnityFile() { m_Handle = handle };
        }

        public static SerializedFile OpenSerializedFile(string path)
        {
            var r = DllWrapper.OpenSerializedFile(path, out var handle);
            UnityFileSystem.HandleErrors(r, path);

            return new SerializedFile() { m_Handle = handle };
        }

        internal static void HandleErrors(ReturnCode returnCode, string filename = "")
        {
            switch (returnCode)
            {
                case ReturnCode.AlreadyInitialized:
                    throw new InvalidOperationException("UnityFileSystem is already initialized.");

                case ReturnCode.NotInitialized:
                    throw new InvalidOperationException("UnityFileSystem is not initialized.");

                case ReturnCode.FileNotFound:
                    throw new FileNotFoundException("File not found.", filename);

                case ReturnCode.FileFormatError:
                    throw new NotSupportedException($"Invalid file format reading {filename}.");

                case ReturnCode.InvalidArgument:
                    throw new ArgumentException();

                case ReturnCode.HigherSerializedFileVersion:
                    throw new NotSupportedException("SerializedFile version not supported.");

                case ReturnCode.DestinationBufferTooSmall:
                    throw new ArgumentException("Destination buffer too small.");

                case ReturnCode.InvalidObjectId:
                    throw new ArgumentException("Invalid object id.");

                case ReturnCode.UnknownError:
                    throw new Exception("Unknown error.");

                case ReturnCode.FileError:
                    throw new IOException("File operation error.");
            }
        }
    }
}
