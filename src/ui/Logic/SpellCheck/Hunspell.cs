using Nikse.SubtitleEdit.Core.Common;
using System;
using System.Collections.Generic;

namespace Nikse.SubtitleEdit.Logic.SpellCheck
{
    public abstract class Hunspell : IDisposable
    {
        private static readonly Dictionary<string, WeakReference> _spellCheckCache = new();
        private static readonly object _cacheLock = new();

        public static Hunspell GetHunspell(string dictionary)
        {
            if (string.IsNullOrWhiteSpace(dictionary))
            {
                throw new ArgumentException("Dictionary cannot be null or empty", nameof(dictionary));
            }

            // Check cache first
            lock (_cacheLock)
            {
                if (_spellCheckCache.TryGetValue(dictionary, out var weakRef) && 
                    weakRef.Target is Hunspell cachedSpellCheck)
                {
                    return cachedSpellCheck;
                }
            }

            // Create new instance
            var spellCheck = CreateSpellCheck(dictionary);
            
            // Cache the instance
            lock (_cacheLock)
            {
                _spellCheckCache[dictionary] = new WeakReference(spellCheck);
            }

            return spellCheck;
        }

        private static Hunspell CreateSpellCheck(string dictionary)
        {
            // Finnish uses Voikko (not available via hunspell)
            if (dictionary.EndsWith("fi_fi", StringComparison.OrdinalIgnoreCase))
            {
                return new VoikkoSpellCheck(Configuration.BaseDirectory, Configuration.DictionariesDirectory);
            }

            var affFile = dictionary + ".aff";
            var dicFile = dictionary + ".dic";

            return Configuration.IsRunningOnLinux
                ? new LinuxHunspell(affFile, dicFile)
                : Configuration.IsRunningOnMac
                    ? new MacHunspell(affFile, dicFile)
                    : new WindowsHunspell(affFile, dicFile);
        }

        public abstract bool Spell(string word);
        public abstract List<string> Suggest(string word);

        public virtual void Dispose()
        {
        }

        protected static void AddIShouldBeLowercaseLSuggestion(List<string> suggestions, string word)
        {
            if (suggestions?.Count == 0 || string.IsNullOrEmpty(word) || word.Length <= 1)
            {
                return;
            }

            // "I" can often be an OCR bug - should really be "l"
            if (word[0] == 'I')
            {
                var lowercaseVariant = "l" + word.Substring(1);
                if (!suggestions.Contains(lowercaseVariant))
                {
                    // Use a static instance to avoid creating new spell checkers
                    // This is a simple heuristic check
                    suggestions.Add(lowercaseVariant);
                }
            }
        }

        public virtual bool IsWordValid(string word)
        {
            return !string.IsNullOrWhiteSpace(word) && word.Length > 0;
        }

        protected virtual void ThrowIfDisposed()
        {
            // Override in derived classes if needed
        }

    }
}
