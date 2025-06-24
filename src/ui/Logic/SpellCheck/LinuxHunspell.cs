using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MessageBox = Nikse.SubtitleEdit.Forms.SeMsgBox.MessageBox;

namespace Nikse.SubtitleEdit.Logic.SpellCheck
{
    public sealed class LinuxHunspell : Hunspell
    {
        private IntPtr _hunspellHandle = IntPtr.Zero;
        private bool _disposed;
        private readonly object _lockObject = new();

        public LinuxHunspell(string affDirectory, string dicDirectory)
        {
            if (string.IsNullOrWhiteSpace(affDirectory))
                throw new ArgumentException("AFF directory path cannot be null or empty", nameof(affDirectory));
            if (string.IsNullOrWhiteSpace(dicDirectory))
                throw new ArgumentException("DIC directory path cannot be null or empty", nameof(dicDirectory));

            try
            {
                _hunspellHandle = NativeMethods.Hunspell_create(affDirectory, dicDirectory);
                if (_hunspellHandle == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to create Hunspell handle");
                }
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                var message = "Unable to start hunspell spell checker - make sure hunspell is installed!" + Environment.NewLine +
                             "E.g. install package 'libhunspell-dev' for Ubuntu, 'hunspell-devel' for Red Hat";
                
                MessageBox.Show(message);
                throw new InvalidOperationException(message, ex);
            }
        }

        public override bool Spell(string word)
        {
            ThrowIfDisposed();
            
            if (!IsWordValid(word))
                return false;

            lock (_lockObject)
            {
                return NativeMethods.Hunspell_spell(_hunspellHandle, word) != 0;
            }
        }

        public override List<string> Suggest(string word)
        {
            ThrowIfDisposed();
            
            if (!IsWordValid(word))
                return new List<string>();

            lock (_lockObject)
            {
                return GetSuggestionsInternal(word);
            }
        }

        private List<string> GetSuggestionsInternal(string word)
        {
            var pointerToAddressStringArray = IntPtr.Zero;
            var results = new List<string>();

            try
            {
                pointerToAddressStringArray = Marshal.AllocHGlobal(IntPtr.Size);
                var resultCount = NativeMethods.Hunspell_suggest(_hunspellHandle, pointerToAddressStringArray, word);
                
                if (resultCount <= 0)
                    return results;

                var addressStringArray = Marshal.ReadIntPtr(pointerToAddressStringArray);
                results.Capacity = resultCount; // Pre-size the list

                for (int i = 0; i < resultCount; i++)
                {
                    var addressCharArray = Marshal.ReadIntPtr(addressStringArray, i * IntPtr.Size);
                    var suggestion = Marshal.PtrToStringAuto(addressCharArray);
                    
                    if (!string.IsNullOrEmpty(suggestion))
                    {
                        results.Add(suggestion);
                    }
                }

                NativeMethods.Hunspell_free_list(_hunspellHandle, pointerToAddressStringArray, resultCount);
            }
            finally
            {
                if (pointerToAddressStringArray != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(pointerToAddressStringArray);
                }
            }

            return results;
        }

        protected override void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LinuxHunspell));
        }

        ~LinuxHunspell()
        {
            Dispose(false);
        }

        private void ReleaseUnmanagedResources()
        {
            if (_hunspellHandle != IntPtr.Zero)
            {
                try
                {
                    NativeMethods.Hunspell_destroy(_hunspellHandle);
                }
                catch
                {
                    // Ignore errors during cleanup
                }
                finally
                {
                    _hunspellHandle = IntPtr.Zero;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                ReleaseUnmanagedResources();
                _disposed = true;
            }
        }
    }
}
