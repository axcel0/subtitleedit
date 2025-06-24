using System;
using System.Runtime.Serialization;

namespace SevenZipExtractor
{
    [Serializable]
    public sealed class SevenZipException : Exception
    {
        public SevenZipException()
        {
        }

        public SevenZipException(string message) : base(message)
        {
        }

        public SevenZipException(string message, Exception innerException) : base(message, innerException)
        {
        }

        private SevenZipException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}