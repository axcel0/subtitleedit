using System.IO;

namespace SevenZipExtractor
{
    internal sealed class ArchiveStreamCallback : IArchiveExtractCallback, ICryptoGetTextPassword
    {
        private readonly uint _fileNumber;
        private readonly Stream _stream;

        public string Password { get; }

        public ArchiveStreamCallback(uint fileNumber, Stream stream, string password = null)
        {
            _fileNumber = fileNumber;
            _stream = stream ?? throw new System.ArgumentNullException(nameof(stream));
            Password = password ?? string.Empty;
        }

        public void SetTotal(ulong total)
        {
            // Implementation not needed for stream extraction
        }

        public void SetCompleted(ref ulong completeValue)
        {
            // Implementation not needed for stream extraction
        }

        public int CryptoGetTextPassword(out string password)
        {
            password = Password;
            return 0;
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
                outStream = new OutStreamWrapper(_stream);
                return 0;
            }
            catch
            {
                return -1; // Error code
            }
        }

        public void PrepareOperation(AskMode askExtractMode)
        {
            // Implementation not needed for stream extraction
        }

        public void SetOperationResult(OperationResult resultEOperationResult)
        {
            // Stream disposal is handled by the caller
        }
    }
}