using System;
using System.IO;

namespace SevenZipExtractor
{
    internal sealed class ArchiveFileCallback : IArchiveExtractCallback, IDisposable
    {
        private readonly string _fileName;
        private readonly uint _fileNumber;
        private OutStreamWrapper _fileStream;
        private bool _disposed;

        public ArchiveFileCallback(uint fileNumber, string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be null or empty", nameof(fileName));

            _fileNumber = fileNumber;
            _fileName = fileName;
        }

        public void SetTotal(ulong total)
        {
            // Implementation not needed for file extraction
        }

        public void SetCompleted(ref ulong completeValue)
        {
            // Implementation not needed for file extraction
        }

        public int GetStream(uint index, out ISequentialOutStream outStream, AskMode askExtractMode)
        {
            outStream = null;

            if (index != _fileNumber || askExtractMode != AskMode.kExtract)
            {
                return 0;
            }

            try
            {
                var fileDir = Path.GetDirectoryName(_fileName);
                if (!string.IsNullOrEmpty(fileDir))
                {
                    Directory.CreateDirectory(fileDir);
                }

                _fileStream = new OutStreamWrapper(File.Create(_fileName));
                outStream = _fileStream;
                return 0;
            }
            catch
            {
                _fileStream?.Dispose();
                _fileStream = null;
                return -1; // Error code
            }
        }

        public void PrepareOperation(AskMode askExtractMode)
        {
            // Implementation not needed for file extraction
        }

        public void SetOperationResult(OperationResult resultEOperationResult)
        {
            _fileStream?.Dispose();
            _fileStream = null;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _fileStream?.Dispose();
                _fileStream = null;
                _disposed = true;
            }
        }
    }
}