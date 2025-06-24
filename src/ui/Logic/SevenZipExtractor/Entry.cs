﻿using System;
using System.IO;

namespace SevenZipExtractor
{
    public sealed class Entry
    {
        private readonly IInArchive _archive;
        private readonly uint _index;

        internal Entry(IInArchive archive, uint index)
        {
            _archive = archive ?? throw new ArgumentNullException(nameof(archive));
            _index = index;
        }

        /// <summary>
        /// Name of the file with its relative path within the archive
        /// </summary>
        public string FileName { get; internal set; }

        /// <summary>
        /// True if entry is a folder, false if it is a file
        /// </summary>
        public bool IsFolder { get; internal set; }

        /// <summary>
        /// Original entry size
        /// </summary>
        public ulong Size { get; internal set; }

        /// <summary>
        /// Entry size in a archived state
        /// </summary>
        public ulong PackedSize { get; internal set; }

        /// <summary>
        /// Date and time of the file (entry) creation
        /// </summary>
        public DateTime CreationTime { get; internal set; }

        /// <summary>
        /// Date and time of the last change of the file (entry)
        /// </summary>
        public DateTime LastWriteTime { get; internal set; }

        /// <summary>
        /// Date and time of the last access of the file (entry)
        /// </summary>
        public DateTime LastAccessTime { get; internal set; }

        /// <summary>
        /// CRC hash of the entry
        /// </summary>
        public uint CRC { get; internal set; }

        /// <summary>
        /// Attributes of the entry
        /// </summary>
        public uint Attributes { get; internal set; }

        /// <summary>
        /// True if entry is encrypted, otherwise false
        /// </summary>
        public bool IsEncrypted { get; internal set; }

        /// <summary>
        /// Comment of the entry
        /// </summary>
        public string Comment { get; internal set; }

        /// <summary>
        /// Compression method of the entry
        /// </summary>
        public string Method { get; internal set; }

        /// <summary>
        /// Host operating system of the entry
        /// </summary>
        public string HostOS { get; internal set; }

        /// <summary>
        /// True if there are parts of this file in previous split archive parts
        /// </summary>
        public bool IsSplitBefore { get; set; }

        /// <summary>
        /// True if there are parts of this file in next split archive parts
        /// </summary>
        public bool IsSplitAfter { get; set; }

        public void Extract(string fileName, bool preserveTimestamp = true)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be null or empty", nameof(fileName));

            if (IsFolder)
            {
                Directory.CreateDirectory(fileName);
                return;
            }

            var directoryName = Path.GetDirectoryName(fileName);
            if (!string.IsNullOrWhiteSpace(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }

            using (var fileStream = File.Create(fileName))
            {
                Extract(fileStream);
            }

            if (preserveTimestamp && LastWriteTime != default)
            {
                try
                {
                    File.SetLastWriteTime(fileName, LastWriteTime);
                }
                catch
                {
                    // Ignore timestamp setting errors
                }
            }
        }

        public void Extract(Stream stream, string password = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            _archive.Extract(new[] { _index }, 1, 0, new ArchiveStreamCallback(_index, stream, password));
        }
    }
}
