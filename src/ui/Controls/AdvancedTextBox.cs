using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.Enums;
using Nikse.SubtitleEdit.Core.Interfaces;
using Nikse.SubtitleEdit.Core.SpellCheck;
using Nikse.SubtitleEdit.Forms;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.SpellCheck;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Controls
{
    /// <summary>
    /// RichTextBox with syntax highlighting and spell check.
    /// </summary>
    public sealed class AdvancedTextBox : RichTextBox, IDoSpell, IDisposable
    {
        #region Constants

        // Windows messages
        private const int WM_PAINT = 0x0F;
        private const int WM_LBUTTONDBLCLK = 0x0203;

        // Spell check constants
        private const int SuggestionTimeoutMs = 3000;
        private const int MinWordLength = 2;
        private const int MinWordLengthSingleChar = 1;
        private const int MaxColorDifference = 60;
        private const int AutoDetectSampleSize = 300;
        private const int SmallAutoDetectSampleSize = 100;
        
        // Character constants
        private const string TrimChars = "'`*#\u200E\u200F\u202A\u202B\u202C\u202D\u202E\u200B\uFEFF";
        private const string UnicodeSpaces = "\u200b\u2060\ufeff";
        
        // HTML/ASSA tag patterns
        private static readonly string[] HtmlTags = { "<i>", "<b>", "<u>", "</i>", "</b>", "</u>", "<box>", "</box>", "</font>" };
        private static readonly string[] CommonDomains = { "com", "org", "net" };

        // Character arrays for performance
        private static readonly char[] SplitChars = { ' ', '.', ',', '?', '!', ':', ';', '"', '"', '"', '(', ')', '[', ']', '{', '}', '|', '<', '>', '/', '+', '¿', '¡', '…', '—', '–', '♪', '♫', '„', '«', '»', '‹', '›', '؛', '،', '؟' };
        private static readonly char[] NumberAndPunctuationChars = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '.', ',', '،' };
        private static readonly char[] BracketEndChars = { '}', '\\', '&' };
        private static readonly char[] HyphenChars = { '-', '‑' };

        #endregion

        #region Fields

        private bool _checkRtfChange = true;
        private int _mouseMoveSelectionLength;
        private bool _isDisposed;

        // Spell check fields
        private Hunspell _hunspell;
        private SpellCheckWordLists _spellCheckWordLists;
        private List<SpellCheckWord> _words;
        private List<SpellCheckWord> _wrongWords;
        private List<string> _skipAllList;
        private HashSet<string> _skipOnceList;
        private SpellCheckWord _currentWord;
        private string _currentDictionary;
        private string _uiTextBoxOldText;

        #endregion

        #region Properties

        private bool IsLiveSpellCheckEnabled => Configuration.Settings.Tools.LiveSpellCheck && Parent?.Name == Main.MainTextBox;

        public int CurrentLineIndex { get; set; }
        public string CurrentLanguage { get; set; }
        public bool LanguageChanged { get; set; }
        public bool IsWrongWord { get; set; }
        public bool IsSpellCheckerInitialized { get; set; }
        public bool IsDictionaryDownloaded { get; set; } = true;
        public bool IsSpellCheckRequested { get; set; }

        public sealed class SuggestionParameter
        {
            public string InputWord { get; init; }
            public List<string> Suggestions { get; init; }
            public Hunspell Hunspell { get; init; }
            public bool Success { get; set; }

            public SuggestionParameter(string word, Hunspell hunspell)
            {
                InputWord = word ?? throw new ArgumentNullException(nameof(word));
                Suggestions = new List<string>();
                Hunspell = hunspell ?? throw new ArgumentNullException(nameof(hunspell));
                Success = false;
            }
        }

        public AdvancedTextBox()
        {
            DetectUrls = false;
            SetTextPosInRtbIfCentered();
            InitializeEvents();
        }

        private void InitializeEvents()
        {
            // Live spell check events
            KeyPress += UiTextBox_KeyPress;
            KeyDown += UiTextBox_KeyDown;
            MouseDown += UiTextBox_MouseDown;
            TextChanged += TextChangedHighlight;
            
            HandleCreated += (sender, args) => SetTextPosInRtbIfCentered();
            
            MouseDown += HandleMouseDownForCentering;
            MouseMove += HandleMouseMove;
            KeyDown += HandleKeyDownNavigation;
        }

        private void HandleMouseDownForCentering(object sender, MouseEventArgs args)
        {
            // Avoid selection when centered and clicking to the left
            var charIndex = GetCharIndexFromPosition(args.Location);
            if (Configuration.Settings.General.CenterSubtitleInTextBox &&
                _mouseMoveSelectionLength == 0 &&
                (charIndex == 0 || (charIndex >= 0 && base.Text[charIndex - 1] == '\n')))
            {
                SelectionLength = 0;
            }
        }

        private void HandleMouseMove(object sender, MouseEventArgs args)
        {
            _mouseMoveSelectionLength = SelectionLength;
        }

        private void HandleKeyDownNavigation(object sender, KeyEventArgs args)
        {
            // Fix annoying "beeps" when moving cursor position
            var startOfLineDirection = Configuration.Settings.General.RightToLeftMode ? Keys.Right : Keys.Left;
            var endOfLineDirection = Configuration.Settings.General.RightToLeftMode ? Keys.Left : Keys.Right;

            var suppressKey = ShouldSuppressKey(args.KeyData, startOfLineDirection, endOfLineDirection);
            if (suppressKey)
            {
                if (args.KeyData == endOfLineDirection && SelectionStart >= Text.Length && IsLiveSpellCheckEnabled)
                {
                    IsSpellCheckRequested = true;
                    TextChangedHighlight(this, EventArgs.Empty);
                }
                args.SuppressKeyPress = true;
            }
        }

        private bool ShouldSuppressKey(Keys keyData, Keys startOfLineDirection, Keys endOfLineDirection)
        {
            var textLength = Text.Length;
            var newlineIndex = Text.IndexOf('\n');
            
            return keyData switch
            {
                _ when keyData == startOfLineDirection || keyData == Keys.PageUp => SelectionStart == 0 && SelectionLength == 0,
                Keys.Up => SelectionStart <= newlineIndex,
                Keys.Home when SelectionStart == 0 || (SelectionStart > 0 && Text[SelectionStart - 1] == '\n') => true,
                Keys.Home | Keys.Control when SelectionStart == 0 => true,
                Keys.End when SelectionStart >= textLength || (SelectionStart + 1 < textLength && Text[SelectionStart + 1] == '\n') => true,
                Keys.End | Keys.Control when SelectionStart >= textLength => true,
                _ when keyData == endOfLineDirection && SelectionStart >= textLength => true,
                Keys.Down when SelectionStart >= textLength => true,
                Keys.PageDown when SelectionStart >= textLength => true,
                _ => false
            };
        }

        #endregion

        #region IDisposable Implementation

        public new void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (!_isDisposed && disposing)
            {
                DisposeHunspellAndDictionaries();
                _isDisposed = true;
            }
            base.Dispose(disposing);
        }

        #endregion

        #region Text Properties Override

        public override string Text
        {
            get => string.Join(Environment.NewLine, base.Text.SplitToLines());
            set
            {
                var text = value ?? string.Empty;
                base.Text = string.Join("\n", text.SplitToLines());
            }
        }

        public new int SelectionStart
        {
            get
            {
                var text = base.Text;
                var extra = 0;
                var target = base.SelectionStart;
                for (var i = 0; i < target && i < text.Length; i++)
                {
                    if (text[i] == '\n')
                    {
                        extra++;
                    }
                }

                return target + extra;
            }
            set
            {
                var text = base.Text;
                var extra = 0;
                for (var i = 0; i < value && i < text.Length; i++)
                {
                    if (text[i] == '\n')
                    {
                        extra++;
                    }
                }

                base.SelectionStart = value - extra;
            }
        }

        public new int SelectionLength
        {
            get
            {
                var target = base.SelectionLength;
                if (target == 0)
                {
                    return 0;
                }

                var text = base.Text;
                var extra = 0;
                var start = SelectionStart;
                for (var i = start; i < target + start && i < text.Length; i++)
                {
                    if (text[i] == '\n')
                    {
                        extra++;
                    }
                }

                return target + extra;
            }
            set
            {
                var target = value;
                if (target == 0)
                {
                    base.SelectionLength = 0;
                    return;
                }

                var text = base.Text;
                var extra = 0;
                var start = SelectionStart;
                for (var i = start; i < target + start && i < text.Length; i++)
                {
                    if (text[i] == '\n')
                    {
                        extra++;
                    }
                }

                base.SelectionLength = target - extra;
            }
        }

        public new string SelectedText
        {
            get => string.Join(Environment.NewLine, base.SelectedText.SplitToLines());
            set => base.SelectedText = value;
        }

        private int GetIndexWithLineBreak(int index)
        {
            var text = base.Text;
            var extra = 0;
            for (var i = 0; i < index && i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    extra++;
                }
            }

            return index - extra;
        }

        private void DoFormattingActionOnRtb(Action formattingAction)
        {
            this.BeginRichTextBoxUpdate();
            var start = SelectionStart;
            var length = SelectionLength;
            formattingAction();
            SelectionStart = start;
            if (SelectionStart < start)
            {
                SelectionStart = start + 1;
            }

            SelectionLength = length;


            this.EndRichTextBoxUpdate();
        }

        private void SetTextPosInRtbIfCentered()
        {
            if (Configuration.Settings.General.CenterSubtitleInTextBox)
            {
                DoFormattingActionOnRtb(() =>
                {
                    SelectAll();
                    SelectionAlignment = HorizontalAlignment.Center;
                });
            }
        }

        private void TagsChangedCheck()
        {
            // Request spell check if there is a change in tags to update highlighting indices.
            if (!string.IsNullOrEmpty(_uiTextBoxOldText)
                && HtmlUtil.RemoveHtmlTags(_uiTextBoxOldText, true) == HtmlUtil.RemoveHtmlTags(Text, true))
            {
                IsSpellCheckRequested = true;
            }

            _uiTextBoxOldText = Text;
        }

        private void TextChangedHighlight(object sender, EventArgs e)
        {
            if (_checkRtfChange)
            {
                _checkRtfChange = false;

                if (IsLiveSpellCheckEnabled)
                {
                    TagsChangedCheck();
                    DoFormattingActionOnRtb(() =>
                    {
                        HighlightHtmlText();
                        HighlightSpellCheckWords();
                    });
                }
                else
                {
                    DoFormattingActionOnRtb(HighlightHtmlText);
                }

                _checkRtfChange = true;
            }
        }

        private void HighlightHtmlText()
        {
            SelectAll();
            SelectionColor = ForeColor;
            SelectionBackColor = BackColor;

            var text = Text;
            var textLength = text.Length;
            
            // State tracking for tags
            var tagState = new TagHighlightState();
            
            for (var i = 0; i < textLength; i++)
            {
                var ch = text[i];
                
                if (tagState.AssaTagOn)
                {
                    ProcessAssaTag(text, i, ch, ref tagState);
                }
                else if (tagState.HtmlTagOn)
                {
                    ProcessHtmlTag(text, i, ch, ref tagState);
                }
                else if (ch == '{' && IsAssaTagStart(text, i, textLength))
                {
                    StartAssaTag(text, i, ref tagState);
                }
                else if (ch == '<' && IsHtmlTagStart(text, i))
                {
                    StartHtmlTag(text, i, ref tagState);
                }
            }
        }

        private static bool IsAssaTagStart(string text, int i, int textLength)
        {
            return i < textLength - 1 && text[i + 1] == '\\' && text.IndexOf('}', i) > 0;
        }

        private bool IsHtmlTagStart(string text, int i)
        {
            var remainingText = text.AsSpan(i);
            
            // Check for known HTML tags
            foreach (var tag in HtmlTags)
            {
                if (remainingText.StartsWith(tag.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            
            // Check for font tag with attributes
            return remainingText.StartsWith("<font ".AsSpan(), StringComparison.OrdinalIgnoreCase) &&
                   text.IndexOf("</font>", i, StringComparison.OrdinalIgnoreCase) > 0;
        }

        private void StartAssaTag(string text, int i, ref TagHighlightState state)
        {
            var tagText = text.AsSpan(i);
            state.AssaTagOn = true;
            state.TagOn = i;
            state.AssaTagStart = i;
            state.AssaPrimaryColorTagOn = tagText.Contains("\\c".AsSpan(), StringComparison.OrdinalIgnoreCase) || 
                                         tagText.Contains("\\1c".AsSpan(), StringComparison.OrdinalIgnoreCase);
            state.AssaSecondaryColorTagOn = tagText.Contains("\\2c".AsSpan(), StringComparison.OrdinalIgnoreCase);
            state.AssaBorderColorTagOn = tagText.Contains("\\3c".AsSpan(), StringComparison.OrdinalIgnoreCase);
            state.AssaShadowColorTagOn = tagText.Contains("\\4c".AsSpan(), StringComparison.OrdinalIgnoreCase);
        }

        private void StartHtmlTag(string text, int i, ref TagHighlightState state)
        {
            state.HtmlTagOn = true;
            state.HtmlTagStart = i;
            state.HtmlTagFontOn = text.AsSpan(i).StartsWith("<font ".AsSpan(), StringComparison.OrdinalIgnoreCase);
            state.TagOn = i;
        }

        private void ProcessAssaTag(string text, int i, char ch, ref TagHighlightState state)
        {
            if (ch == '}' && state.TagOn >= 0)
            {
                state.AssaTagOn = false;
                SelectionStart = state.AssaTagStart;
                SelectionLength = i - state.AssaTagStart + 1;
                SelectionColor = Configuration.Settings.General.SubtitleTextBoxAssColor;
                
                if (state.AssaTagStart >= 0)
                {
                    ProcessAssaColorTags(text, state);
                }
                
                state.AssaTagStart = -1;
            }
        }

        private void ProcessAssaColorTags(string text, TagHighlightState state)
        {
            if (state.AssaPrimaryColorTagOn)
            {
                var colorTag = text.IndexOf("\\c", state.AssaTagStart, StringComparison.OrdinalIgnoreCase) != -1 ? "\\c" : "\\1c";
                SetAssaColor(text, state.AssaTagStart, colorTag);
                state.AssaPrimaryColorTagOn = false;
            }

            if (state.AssaSecondaryColorTagOn)
            {
                SetAssaColor(text, state.AssaTagStart, "\\2c");
                state.AssaSecondaryColorTagOn = false;
            }

            if (state.AssaBorderColorTagOn)
            {
                SetAssaColor(text, state.AssaTagStart, "\\3c");
                state.AssaBorderColorTagOn = false;
            }

            if (state.AssaShadowColorTagOn)
            {
                SetAssaColor(text, state.AssaTagStart, "\\4c");
                state.AssaShadowColorTagOn = false;
            }
        }

        private void ProcessHtmlTag(string text, int i, char ch, ref TagHighlightState state)
        {
            if (ch == '>' && state.TagOn >= 0)
            {
                state.HtmlTagOn = false;
                SelectionStart = state.HtmlTagStart;
                SelectionLength = i - state.HtmlTagStart + 1;
                SelectionColor = Configuration.Settings.General.SubtitleTextBoxHtmlColor;
                
                if (state.HtmlTagFontOn && state.HtmlTagStart >= 0)
                {
                    SetHtmlColor(text, state.HtmlTagStart);
                    state.HtmlTagFontOn = false;
                }
                
                state.HtmlTagStart = -1;
            }
        }

        private struct TagHighlightState
        {
            public bool HtmlTagOn;
            public bool HtmlTagFontOn;
            public int HtmlTagStart;
            public bool AssaTagOn;
            public bool AssaPrimaryColorTagOn;
            public bool AssaSecondaryColorTagOn;
            public bool AssaBorderColorTagOn;
            public bool AssaShadowColorTagOn;
            public int AssaTagStart;
            public int TagOn;
        }

        private void SetHtmlColor(string text, int htmlTagStart)
        {
            var colorStart = text.IndexOf(" color=", htmlTagStart, StringComparison.OrdinalIgnoreCase);
            if (colorStart > 0)
            {
                colorStart += " color=".Length;
                if (text[colorStart] == '"' || text[colorStart] == '\'')
                {
                    colorStart++;
                }

                var colorEnd = text.IndexOf('"', colorStart + 1);
                if (colorEnd > 0)
                {
                    var color = text.Substring(colorStart, colorEnd - colorStart);
                    try
                    {
                        var c = HtmlUtil.GetColorFromString(color);
                        SetForeColorAndChangeBackColorIfClose(colorStart, colorEnd, c);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }

        private void SetAssaColor(string text, int assaTagStart, string colorTag)
        {
            var colorStart = text.IndexOf(colorTag, assaTagStart, StringComparison.OrdinalIgnoreCase);
            if (colorStart > 0)
            {
                colorStart += colorTag.Length;
                if (text[colorStart] == '&')
                {
                    colorStart++;
                }
                if (text[colorStart] == 'H')
                {
                    colorStart++;
                }

                var colorEnd = text.IndexOfAny(new[] { '}', '\\', '&' }, colorStart + 1);
                if (colorEnd > 0)
                {
                    var color = text.Substring(colorStart, colorEnd - colorStart);
                    try
                    {
                        if (color.Length > 0 && color.Length < 6)
                        {
                            color = color.PadLeft(6, '0');
                        }

                        if (color.Length == 6)
                        {
                            var rgbColor = string.Concat("#", color[4], color[5], color[2], color[3], color[0], color[1]); var c = ColorTranslator.FromHtml(rgbColor);
                            SetForeColorAndChangeBackColorIfClose(colorStart, colorEnd, c);
                        }
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }

        private void SetForeColorAndChangeBackColorIfClose(int colorStart, int colorEnd, Color c)
        {
            var backColor = BackColor;
            SelectionStart = colorStart;
            SelectionLength = colorEnd - colorStart;
            SelectionColor = c;

            // Calculate color difference more efficiently
            var diff = Math.Abs(c.R - backColor.R) + Math.Abs(c.G - backColor.G) + Math.Abs(c.B - backColor.B);
            if (diff < MaxColorDifference)
            {
                SelectionBackColor = Color.FromArgb(
                    byte.MaxValue - c.R, 
                    byte.MaxValue - c.G, 
                    byte.MaxValue - c.B, 
                    byte.MaxValue - c.R);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_LBUTTONDBLCLK)
            {
                var text = Text;
                var posStart = SelectionStart;
                if (posStart >= 0 && posStart < text.Length && char.IsLetterOrDigit(text[posStart]))
                {
                    var posEnd = posStart;
                    while (posStart > 0 && char.IsLetterOrDigit(text[posStart - 1]))
                    {
                        posStart--;
                    }

                    while (posEnd < text.Length && char.IsLetterOrDigit(text[posEnd]))
                    {
                        posEnd++;
                    }

                    if (posEnd < text.Length && text[posEnd] == '\r')
                    {
                        posEnd++;
                    }

                    var length = posEnd - posStart;
                    if (length > 0)
                    {
                        SelectionStart = posStart;
                        SelectionLength = length;
                    }
                }
            }
            else
            {
                base.WndProc(ref m);
            }

            if (m.Msg == WM_PAINT && !Enabled && Configuration.Settings.General.UseDarkTheme)
            {
                using (var g = Graphics.FromHwnd(Handle))
                using (var sb = new SolidBrush(BackColor))
                {
                    g.FillRectangle(sb, ClientRectangle);
                }
            }
        }

        #region LiveSpellCheck

        public void CheckForLanguageChange(Subtitle subtitle)
        {
            var detectedLanguage = LanguageAutoDetect.AutoDetectGoogleLanguage(subtitle, SmallAutoDetectSampleSize);
            if (CurrentLanguage != detectedLanguage)
            {
                DisposeHunspellAndDictionaries();
                InitializeLiveSpellCheck(subtitle, CurrentLineIndex);
            }
        }

        private static bool IsDictionaryAvailable(string language)
        {
            foreach (var downloadedDictionary in Utilities.GetDictionaryLanguagesCultureNeutral())
            {
                if (downloadedDictionary.Contains($"[{language}]", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public void InitializeLiveSpellCheck(Subtitle subtitle, int lineNumber)
        {
            if (lineNumber < 0 || _spellCheckWordLists is not null || _hunspell is not null)
            {
                return;
            }

            var detectedLanguage = LanguageAutoDetect.AutoDetectGoogleLanguage(subtitle, AutoDetectSampleSize);
            IsDictionaryDownloaded = false;
            
            if (IsDictionaryAvailable(detectedLanguage))
            {
                var languageName = LanguageAutoDetect.AutoDetectLanguageName(string.Empty, subtitle);
                if (languageName.Split('_', '-')[0] != detectedLanguage)
                {
                    return;
                }

                LoadDictionaries(languageName);
                IsDictionaryDownloaded = true;
                IsSpellCheckerInitialized = true;
                IsSpellCheckRequested = true;
                TextChangedHighlight(this, EventArgs.Empty);
            }

            LanguageChanged = true;
            CurrentLanguage = detectedLanguage;
        }

        private void LoadDictionaries(string languageName)
        {
            if (string.IsNullOrWhiteSpace(languageName))
                throw new ArgumentException("Language name cannot be null or empty", nameof(languageName));

            var dictionaryFolder = Utilities.DictionaryFolder;
            var dictionary = Utilities.DictionaryFolder + languageName;
            
            _spellCheckWordLists = new SpellCheckWordLists(dictionaryFolder, languageName, this);
            _skipAllList = new List<string>();
            _skipOnceList = new HashSet<string>();
            
            LoadHunspell(dictionary);
        }

        private void LoadHunspell(string dictionary)
        {
            if (string.IsNullOrWhiteSpace(dictionary))
                return;

            _currentDictionary = dictionary;
            _hunspell?.Dispose();
            _hunspell = null;
            _hunspell = Hunspell.GetHunspell(dictionary);
        }

        public void DisposeHunspellAndDictionaries()
        {
            if (IsSpellCheckerInitialized)
            {
                _skipAllList = null;
                _skipOnceList = null;
                _spellCheckWordLists = null;
                _words = null;
                _wrongWords = null;
                _currentWord = null;
                _currentDictionary = null;
                CurrentLanguage = null;
                _hunspell?.Dispose();
                _hunspell = null;
                IsWrongWord = false;
                IsSpellCheckerInitialized = false;

                if (Configuration.Settings.General.SubtitleTextBoxSyntaxColor)
                {
                    IsSpellCheckRequested = true;
                    TextChangedHighlight(this, EventArgs.Empty);
                }
            }
        }

        public void DoLiveSpellCheck()
        {
            if (IsSpellCheckerInitialized && IsSpellCheckRequested)
            {
                _words = GetWords(Text);
                SetWrongWords();
            }
        }

        private List<SpellCheckWord> GetWords(string s)
        {
            s = _spellCheckWordLists.ReplaceHtmlTagsWithBlanks(s);
            s = _spellCheckWordLists.ReplaceAssTagsWithBlanks(s);
            s = _spellCheckWordLists.ReplaceKnownWordsOrNamesWithBlanks(s);
            return SpellCheckWordLists.Split(s);
        }

        private void SetWrongWords()
        {
            _wrongWords = new List<SpellCheckWord>();

            var minLength = Configuration.Settings.Tools.CheckOneLetterWords ? MinWordLengthSingleChar : MinWordLength;

            for (var i = 0; i < _words.Count; i++)
            {
                var currentWord = _words[i];
                var currentWordText = currentWord.Text;
                
                var key = $"{CurrentLineIndex}-{currentWordText}-{currentWord.Index}";
                
                if (ShouldSkipWord(currentWordText, key, minLength))
                {
                    continue;
                }

                var (prefix, postfix, trimmedWord) = TrimWordCharacters(currentWordText, minLength);
                currentWordText = trimmedWord;

                if (ShouldSkipPrefixedWord(prefix, currentWordText))
                {
                    continue;
                }

                if (ShouldSkipProcessedWord(currentWordText, i))
                {
                    continue;
                }

                if (ShouldSkipDashConcatenatedWord(currentWordText, i))
                {
                    continue;
                }

                if (ShouldSkipSingleCharacterWord(currentWordText))
                {
                    continue;
                }

                if (ShouldSkipEnglishContractions(currentWordText))
                {
                    continue;
                }

                _wrongWords.Add(currentWord);
            }
        }

        private bool ShouldSkipWord(string word, string key, int minLength)
        {
            return DoSpell(word) || 
                   Utilities.IsNumber(word) || 
                   _skipAllList.Contains(word) ||
                   _skipOnceList.Contains(key) || 
                   _spellCheckWordLists.HasUserWord(word) || 
                   _spellCheckWordLists.HasName(word) ||
                   word.Length < minLength || 
                   word == "&";
        }

        private (string prefix, string postfix, string word) TrimWordCharacters(string word, int minLength)
        {
            string prefix = string.Empty;
            string postfix = string.Empty;
            
            if (word.RemoveControlCharacters().Trim().Length < minLength || word.Length == 0)
            {
                return (prefix, postfix, word);
            }

            var charHit = true;
            while (charHit && word.Length > 0)
            {
                charHit = false;
                foreach (var c in TrimChars)
                {
                    if (word.StartsWith(c))
                    {
                        prefix += c;
                        word = word[1..];
                        charHit = true;
                    }
                    if (word.EndsWith(c))
                    {
                        postfix = c + postfix;
                        word = word[..^1];
                        charHit = true;
                    }
                }
            }

            return (prefix, postfix, word);
        }

        private bool ShouldSkipPrefixedWord(string prefix, string word)
        {
            return prefix == "'" && word.Length >= 1 && 
                   (DoSpell(prefix + word) || _spellCheckWordLists.HasUserWord(prefix + word));
        }

        private bool ShouldSkipProcessedWord(string word, int index)
        {
            if (word.Length <= 1) return false;

            // Check various word patterns
            if ("`'".Contains(word[^1]) && DoSpell(word.TrimEnd('\'', '`')))
            {
                return true;
            }

            if (word.EndsWith("'s", StringComparison.Ordinal) && word.Length > 4 &&
                DoSpell(word.TrimEnd('s').TrimEnd('\'')))
            {
                return true;
            }

            if (word.EndsWith('\'') && DoSpell(word.TrimEnd('\'')))
            {
                return true;
            }

            // Remove Unicode spaces
            var cleanWord = RemoveUnicodeSpaces(word);
            if (cleanWord != word && DoSpell(cleanWord))
            {
                return true;
            }

            // Check URL patterns
            if (IsPartOfUrl(index))
            {
                return true;
            }

            // Arabic number trimming
            if (CurrentLanguage == "ar")
            {
                var trimmed = word.Trim(NumberAndPunctuationChars);
                if (trimmed != word && ShouldSkipWord(trimmed, string.Empty, MinWordLength))
                {
                    return true;
                }
            }

            return false;
        }

        private string RemoveUnicodeSpaces(string word)
        {
            var result = word;
            foreach (var unicodeSpace in UnicodeSpaces)
            {
                result = result.Replace(unicodeSpace.ToString(), string.Empty);
            }
            return result;
        }

        private bool IsPartOfUrl(int index)
        {
            if (index <= 0 || index >= _words.Count - 1) return false;

            var prevWord = _words[index - 1];
            var nextWord = _words[index + 1];
            var currentWord = _words[index];

            return string.Equals(prevWord.Text, "www", StringComparison.InvariantCultureIgnoreCase) &&
                   CommonDomains.Any(domain => string.Equals(nextWord.Text, domain, StringComparison.InvariantCultureIgnoreCase)) &&
                   Text.IndexOf($"{prevWord.Text}.{currentWord.Text}.{nextWord.Text}", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool ShouldSkipDashConcatenatedWord(string word, int index)
        {
            // Check dash concatenated words with previous word
            if (index > 0 && IsCharAtPosition(word, index, HyphenChars, -1))
            {
                var wordWithDash = _words[index - 1].Text + "-" + word;
                if (DoSpell(wordWithDash))
                {
                    return true;
                }

                wordWithDash = _words[index - 1].Text + "‑" + word; // non-break hyphen
                if (DoSpell(wordWithDash) || _spellCheckWordLists.HasUserWord(wordWithDash) ||
                    _spellCheckWordLists.HasUserWord(wordWithDash.Replace("‑", "-")) || 
                    _spellCheckWordLists.HasUserWord("-" + word))
                {
                    return true;
                }
            }

            // Check dash concatenated words with next word
            if (index < _words.Count - 1 && IsCharAtPosition(word, index + 1, HyphenChars, -1))
            {
                var wordWithDash = word + "-" + _words[index + 1].Text;
                if (DoSpell(wordWithDash))
                {
                    return true;
                }

                wordWithDash = word + "‑" + _words[index + 1].Text; // non-break hyphen
                if (DoSpell(wordWithDash) || _spellCheckWordLists.HasUserWord(wordWithDash) || 
                    _spellCheckWordLists.HasUserWord(wordWithDash.Replace("‑", "-")))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsCharAtPosition(string word, int wordIndex, char[] chars, int offset)
        {
            var nextWord = _words[wordIndex + (offset > 0 ? 1 : 0)];
            var charIndex = nextWord.Index + offset;
            return charIndex >= 0 && charIndex < Text.Length && chars.Contains(Text[charIndex]);
        }

        private bool ShouldSkipSingleCharacterWord(string word)
        {
            if (word.Length != 1) return false;

            if (word == "'") return true;

            return CurrentLanguage switch
            {
                "en" => word.Equals("a", StringComparison.OrdinalIgnoreCase) || word == "I",
                "da" => word.Equals("i", StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }

        private bool ShouldSkipEnglishContractions(string word)
        {
            return Configuration.Settings.Tools.SpellCheckEnglishAllowInQuoteAsIng && 
                   CurrentLanguage == "en" &&
                   word.EndsWith("in'", StringComparison.OrdinalIgnoreCase) && 
                   DoSpell(word.TrimEnd('\'') + "g");
        }

        public bool DoSpell(string word)
        {
            return _hunspell.Spell(word);
        }

        private void UiTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (IsLiveSpellCheckEnabled && e.KeyCode == Keys.Apps && _wrongWords?.Count > 0)
            {
                var cursorPos = SelectionStart;
                var wrongWord = _wrongWords.Find(word => cursorPos > word.Index && cursorPos < word.Index + word.Length);
                if (wrongWord != null)
                {
                    IsWrongWord = true;
                    _currentWord = wrongWord;
                }
                else
                {
                    IsWrongWord = false;
                }
            }
        }

        private void UiTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (IsLiveSpellCheckEnabled)
            {
                if (e.KeyChar == '\b' && SelectionStart != Text.Length || e.KeyChar == '\r' || e.KeyChar == '\n')
                {
                    IsSpellCheckRequested = true;
                    TextChangedHighlight(this, EventArgs.Empty);
                }
                else if (SplitChars.Contains(e.KeyChar) && SelectionStart == Text.Length || SelectionStart != Text.Length)
                {
                    IsSpellCheckRequested = true;
                }
            }
        }

        private void UiTextBox_MouseDown(object sender, MouseEventArgs e)
        {
            if (IsLiveSpellCheckEnabled && _wrongWords?.Count > 0 && e.Clicks == 1 && e.Button == MouseButtons.Right)
            {
                var positionToSearch = GetCharIndexFromPosition(new Point(e.X, e.Y));
                var wrongWord = _wrongWords.Find(word => positionToSearch > GetIndexWithLineBreak(word.Index) && positionToSearch < GetIndexWithLineBreak(word.Index) + word.Length);
                if (wrongWord != null)
                {
                    IsWrongWord = true;
                    _currentWord = wrongWord;
                }
                else
                {
                    IsWrongWord = false;
                }
            }
        }

        private List<string> DoSuggest(string word)
        {
            var parameter = new SuggestionParameter(word, _hunspell);
            var suggestThread = new Thread(DoWork);
            suggestThread.Start(parameter);
            suggestThread.Join(SuggestionTimeoutMs); // wait max 3 seconds
            
            if (!parameter.Success)
            {
                LoadHunspell(_currentDictionary);
            }

            return parameter.Suggestions;
        }

        public void AddSuggestionsToMenu()
        {
            if (_currentWord != null)
            {
                var suggestions = DoSuggest(_currentWord.Text);
                if (suggestions?.Count > 0)
                {
                    foreach (var suggestion in suggestions)
                    {
                        ContextMenuStrip.Items.Add(suggestion, null, SuggestionSelected);
                    }
                }
            }
        }

        private static void DoWork(object data)
        {
            var parameter = (SuggestionParameter)data;
            parameter.Suggestions = parameter.Hunspell.Suggest(parameter.InputWord);
            parameter.Success = true;
        }

        private void SuggestionSelected(object sender, EventArgs e)
        {
            IsWrongWord = false;
            _wrongWords.Remove(_currentWord);
            var item = (ToolStripItem)sender;
            var correctWord = item.Text;
            var text = Text;
            var cursorPos = SelectionStart;
            var wordIndex = _currentWord.Index;
            text = text.Remove(wordIndex, _currentWord.Length);
            text = text.Insert(wordIndex, correctWord);
            Text = text;
            SelectionStart = cursorPos;
            IsSpellCheckRequested = true;
            TextChangedHighlight(this, EventArgs.Empty);
        }

        public void DoAction(SpellCheckAction action)
        {
            if (_currentWord != null)
            {
                switch (action)
                {
                    case SpellCheckAction.Skip:
                        string key = CurrentLineIndex + "-" + _currentWord.Text + "-" + _currentWord.Index;
                        _skipOnceList.Add(key);
                        break;
                    case SpellCheckAction.SkipAll:
                        _skipAllList.Add(_currentWord.Text);
                        break;
                    case SpellCheckAction.AddToDictionary:
                        _spellCheckWordLists.AddUserWord(_currentWord.Text);
                        break;
                    case SpellCheckAction.AddToNames:
                        _spellCheckWordLists.AddName(_currentWord.Text);
                        break;
                }

                if (_wrongWords.Contains(_currentWord))
                {
                    IsWrongWord = false;
                    _wrongWords.Remove(_currentWord);
                    IsSpellCheckRequested = true;
                    TextChangedHighlight(this, EventArgs.Empty);
                }
            }
        }

        private void HighlightSpellCheckWords()
        {
            DoLiveSpellCheck();
            if (_wrongWords?.Count > 0)
            {
                foreach (var wrongWord in _wrongWords)
                {
                    Select(GetIndexWithLineBreak(wrongWord.Index), wrongWord.Length);
                    SelectionColor = Configuration.Settings.Tools.ListViewSyntaxErrorColor;
                }
            }

            IsSpellCheckRequested = false;
        }

        #endregion
    }
}
