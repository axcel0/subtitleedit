using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace SevenZipExtractor
{
    internal sealed class SevenZipHandle : IDisposable
    {
        private SafeLibraryHandle _sevenZipSafeHandle;
        private bool _disposed;

        public SevenZipHandle(string sevenZipLibPath)
        {
            if (string.IsNullOrWhiteSpace(sevenZipLibPath))
                throw new ArgumentException("Library path cannot be null or empty", nameof(sevenZipLibPath));

            _sevenZipSafeHandle = Kernel32Dll.LoadLibrary(sevenZipLibPath);

            if (_sevenZipSafeHandle.IsInvalid)
            {
                throw new Win32Exception($"Failed to load 7-Zip library: {sevenZipLibPath}");
            }

            // Validate the library by checking for required function
            var functionPtr = Kernel32Dll.GetProcAddress(_sevenZipSafeHandle, "GetHandlerProperty");
            if (functionPtr == IntPtr.Zero)
            {
                _sevenZipSafeHandle.Close();
                throw new ArgumentException($"Invalid 7-Zip library - missing required functions: {sevenZipLibPath}");
            }
        }

        public IInArchive CreateInArchive(Guid classId)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SevenZipHandle));

            var procAddress = Kernel32Dll.GetProcAddress(_sevenZipSafeHandle, "CreateObject");
            if (procAddress == IntPtr.Zero)
                throw new InvalidOperationException("CreateObject function not found in 7-Zip library");

            var createObject = Marshal.GetDelegateForFunctionPointer<CreateObjectDelegate>(procAddress);
            var interfaceId = typeof(IInArchive).GUID;
            
            createObject(ref classId, ref interfaceId, out var result);
            
            return result as IInArchive ?? throw new InvalidOperationException("Failed to create IInArchive instance");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _sevenZipSafeHandle?.Close();
                _sevenZipSafeHandle = null;
                _disposed = true;
            }
        }
    }
}