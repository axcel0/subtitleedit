using System;
using System.Collections.Generic;
using System.IO;

namespace SevenZipExtractor
{
    internal sealed class ArchiveStreamsCallback : IArchiveExtractCallback, ICryptoGetTextPassword
    {
        private readonly IList<Stream> _streams;

        public string Password { get; }

        public ArchiveStreamsCallback(IList<Stream> streams, string password = null)
        {
            _streams = streams ?? throw new ArgumentNullException(nameof(streams));
            Password = password ?? string.Empty;
        }

        public int CryptoGetTextPassword(out string password)
        {
            password = Password;
            return 0;
        }

        public void SetTotal(ulong total)
        {
            // Implementation not needed for streams extraction
        }

        public void SetCompleted(ref ulong completeValue)
        {
            // Implementation not needed for streams extraction
        }

        public int GetStream(uint index, out ISequentialOutStream outStream, AskMode askExtractMode)
        {
            outStream = null;

            if (askExtractMode != AskMode.kExtract)
            {
                return 0;
            }

            if (index >= _streams.Count)
            {
                return -1; // Index out of range
            }

            var stream = _streams[(int)index];
            if (stream == null)
            {
                return -1; // Null stream
            }

            try
            {
                outStream = new OutStreamWrapper(stream);
                return 0;
            }
            catch
            {
                return -1; // Error creating wrapper
            }
        }

        public void PrepareOperation(AskMode askExtractMode)
        {
            // Implementation not needed for streams extraction
        }

        public void SetOperationResult(OperationResult resultEOperationResult)
        {
            // Stream disposal is handled by the caller
        }
    }
}