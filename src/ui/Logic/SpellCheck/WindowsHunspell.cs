using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Nikse.SubtitleEdit.Logic.SpellCheck
{
    public sealed class WindowsHunspell : Hunspell
    {
        private static readonly Regex SurrogateRegex = new(@"\p{Cs}", RegexOptions.Compiled);
        
        private NHunspell.Hunspell _hunspell;
        private bool _disposed;

        public WindowsHunspell(string affDictionary, string dicDictionary)
        {
            if (string.IsNullOrWhiteSpace(affDictionary))
                throw new ArgumentException("AFF dictionary path cannot be null or empty", nameof(affDictionary));
            if (string.IsNullOrWhiteSpace(dicDictionary))
                throw new ArgumentException("DIC dictionary path cannot be null or empty", nameof(dicDictionary));

            try
            {
                _hunspell = new NHunspell.Hunspell(affDictionary, dicDictionary);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize Hunspell with dictionaries: {affDictionary}, {dicDictionary}", ex);
            }
        }

        public override bool Spell(string word)
        {
            ThrowIfDisposed();
            
            if (!IsWordValid(word))
                return false;

            return _hunspell.Spell(word);
        }

        public override List<string> Suggest(string word)
        {
            ThrowIfDisposed();
            
            if (!IsWordValid(word))
                return new List<string>();

            var filtered = SurrogateRegex.Replace(word, string.Empty);
            var suggestions = _hunspell.Suggest(filtered);
            
            AddIShouldBeLowercaseLSuggestion(suggestions, filtered);
            return suggestions;
        }

        protected override void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(WindowsHunspell));
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                if (_hunspell?.IsDisposed == false)
                {
                    _hunspell.Dispose();
                }
                _hunspell = null;
                _disposed = true;
            }
        }
    }
}
