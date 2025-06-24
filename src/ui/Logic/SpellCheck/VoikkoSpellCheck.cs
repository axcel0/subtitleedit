using Nikse.SubtitleEdit.Core.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Nikse.SubtitleEdit.Logic.SpellCheck
{
    public sealed class VoikkoSpellCheck : Hunspell
    {
        // Voikko function delegates
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr VoikkoInit(ref IntPtr error, byte[] languageCode, byte[] path);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void VoikkoTerminate(IntPtr handle);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int VoikkoSpell(IntPtr handle, byte[] word);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr VoikkoSuggest(IntPtr handle, byte[] word);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr VoikkoFreeCstrArray(IntPtr array);

        private readonly VoikkoInit _voikkoInit;
        private readonly VoikkoTerminate _voikkoTerminate;
        private readonly VoikkoSpell _voikkoSpell;
        private readonly VoikkoSuggest _voikkoSuggest;
        private readonly VoikkoFreeCstrArray _voikkoFreeCstrArray;

        private IntPtr _libDll = IntPtr.Zero;
        private IntPtr _libVoikko = IntPtr.Zero;
        private bool _disposed;
        private readonly object _lockObject = new();

        private static readonly Encoding Utf8Encoding = new UTF8Encoding(false);

        public VoikkoSpellCheck(string baseFolder, string dictionaryFolder)
        {
            if (string.IsNullOrWhiteSpace(baseFolder))
                throw new ArgumentException("Base folder cannot be null or empty", nameof(baseFolder));
            if (string.IsNullOrWhiteSpace(dictionaryFolder))
                throw new ArgumentException("Dictionary folder cannot be null or empty", nameof(dictionaryFolder));

            try
            {
                LoadLibVoikkoDynamic(baseFolder);
                
                // Get function pointers
                _voikkoInit = GetDllFunction<VoikkoInit>("voikkoInit");
                _voikkoTerminate = GetDllFunction<VoikkoTerminate>("voikkoTerminate");
                _voikkoSpell = GetDllFunction<VoikkoSpell>("voikkoSpellCstr");
                _voikkoSuggest = GetDllFunction<VoikkoSuggest>("voikkoSuggestCstr");
                _voikkoFreeCstrArray = GetDllFunction<VoikkoFreeCstrArray>("voikkoFreeCstrArray");

                // Initialize Voikko
                var error = IntPtr.Zero;
                _libVoikko = _voikkoInit(ref error, StringToBytes("fi"), StringToBytesAnsi(dictionaryFolder));
                
                if (_libVoikko == IntPtr.Zero)
                {
                    var errorMessage = error != IntPtr.Zero ? PtrToString(error) : "Unknown error";
                    throw new InvalidOperationException($"Failed to initialize Voikko: {errorMessage}");
                }
            }
            catch
            {
                ReleaseUnmanagedResources();
                throw;
            }
        }

        private static string PtrToString(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
                return null;

            var bytes = new List<byte>();
            unsafe
            {
                for (byte* p = (byte*)ptr; *p != 0; p++)
                {
                    bytes.Add(*p);
                }
            }
            return bytes.Count > 0 ? Utf8Encoding.GetString(bytes.ToArray()) : null;
        }

        private static byte[] StringToBytes(string str) => 
            str == null ? null : Utf8Encoding.GetBytes(str + '\0');

        private static byte[] StringToBytesAnsi(string str) => 
            str == null ? null : Encoding.Default.GetBytes(str + '\0');

        private T GetDllFunction<T>(string functionName) where T : class
        {
            var address = NativeMethods.CrossGetProcAddress(_libDll, functionName);
            if (address == IntPtr.Zero)
                throw new InvalidOperationException($"Function '{functionName}' not found in Voikko library");
            
            return Marshal.GetDelegateForFunctionPointer(address, typeof(T)) as T;
        }

        private void LoadLibVoikkoDynamic(string baseFolder)
        {
            var dllFile = GetVoikkoLibraryPath(baseFolder);
            
            if (!File.Exists(dllFile))
            {
                throw new FileNotFoundException($"Voikko library not found: {dllFile}");
            }

            _libDll = NativeMethods.CrossLoadLibrary(dllFile);
            if (_libDll == IntPtr.Zero)
            {
                throw new FileLoadException($"Unable to load Voikko library: {dllFile}");
            }
        }

        private static string GetVoikkoLibraryPath(string baseFolder)
        {
            if (Configuration.IsRunningOnWindows)
            {
                return Path.Combine(baseFolder, IntPtr.Size == 8 ? "Voikkox64.dll" : "Voikkox86.dll");
            }
            
            return Path.Combine(baseFolder, "libvoikko.so");
        }

        public override bool Spell(string word)
        {
            ThrowIfDisposed();
            
            if (!IsWordValid(word))
                return false;

            lock (_lockObject)
            {
                return _voikkoSpell(_libVoikko, StringToBytes(word)) != 0;
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
            var suggestions = new List<string>();
            var voikkoSuggestCstr = _voikkoSuggest(_libVoikko, StringToBytes(word));
            
            if (voikkoSuggestCstr == IntPtr.Zero)
                return suggestions;

            try
            {
                unsafe
                {
                    for (byte** cStr = (byte**)voikkoSuggestCstr; *cStr != (byte*)0; cStr++)
                    {
                        var suggestion = PtrToString(new IntPtr(*cStr));
                        if (!string.IsNullOrEmpty(suggestion))
                        {
                            suggestions.Add(suggestion);
                        }
                    }
                }
            }
            finally
            {
                _voikkoFreeCstrArray(voikkoSuggestCstr);
            }

            return suggestions;
        }

        protected override void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(VoikkoSpellCheck));
        }

        private void ReleaseUnmanagedResources()
        {
            try
            {
                if (_libVoikko != IntPtr.Zero)
                {
                    _voikkoTerminate(_libVoikko);
                    _libVoikko = IntPtr.Zero;
                }

                if (_libDll != IntPtr.Zero)
                {
                    NativeMethods.CrossFreeLibrary(_libDll);
                    _libDll = IntPtr.Zero;
                }
            }
            catch
            {
                // Ignore cleanup errors
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
