using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.Enums;
using Nikse.SubtitleEdit.Logic;
using System;
using System.Drawing;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls.Interfaces;

namespace Nikse.SubtitleEdit.Controls
{
    /// <summary>
    /// A versatile text box control that can function as either a normal text box or a rich text box
    /// with syntax highlighting and spell checking capabilities.
    /// </summary>
    public sealed class SETextBox : Panel, ISelectedText, IDisposable
    {
        #region Constants and Configuration
        /// <summary>
        /// The default padding size for the control border.
        /// </summary>
        private const int DefaultPaddingSize = 1;
        
        // Error message constants for consistent error reporting
        private const string ErrorInitializingMessage = "Error initializing SETextBox: {0}";
        private const string ErrorUpdatingFontMessage = "Error updating font and colors: {0}";
        private const string ErrorApplyingThemeMessage = "Error applying dark theme: {0}";
        private const string ErrorRemovingThemeMessage = "Error removing dark theme: {0}";
        private const string ErrorSettingPropertyMessage = "Error setting text box property: {0}";
        private const string ErrorExecutingActionMessage = "Error executing action on text box: {0}";
        private const string ErrorDisposingMessage = "Error disposing SETextBox: {0}";
        #endregion

        #region Events
        /// <summary>
        /// Occurs when the text content changes.
        /// </summary>
        public new event EventHandler TextChanged;

        /// <summary>
        /// Occurs when a key is pressed while the control has focus.
        /// </summary>
        public new event KeyEventHandler KeyDown;

        /// <summary>
        /// Occurs when the control is clicked with the mouse.
        /// </summary>
        public new event MouseEventHandler MouseClick;

        /// <summary>
        /// Occurs when the control receives focus.
        /// </summary>
        public new event EventHandler Enter;

        /// <summary>
        /// Occurs when a key is released while the control has focus.
        /// </summary>
        public new event KeyEventHandler KeyUp;

        /// <summary>
        /// Occurs when the control loses focus.
        /// </summary>
        public new event EventHandler Leave;

        /// <summary>
        /// Occurs when the mouse is moved over the control.
        /// </summary>
        public new event MouseEventHandler MouseMove;
        #endregion

        #region Private Fields
        private AdvancedTextBox _uiTextBox;
        private SimpleTextBox _simpleTextBox;
        private bool _disposed;
        private Font _cachedTextBoxFont;
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the SETextBox with default syntax coloring settings.
        /// </summary>
        public SETextBox()
        {
            InitializeControlStyles();
            var useSyntaxColor = Configuration.Settings?.General?.SubtitleTextBoxSyntaxColor ?? false;
            Initialize(useSyntaxColor, false);
        }

        /// <summary>
        /// Initializes a new instance of the SETextBox with specified text box mode.
        /// </summary>
        /// <param name="justTextBox">If true, creates a simple text box without syntax highlighting.</param>
        public SETextBox(bool justTextBox)
        {
            InitializeControlStyles();
            Initialize(false, justTextBox);
        }

        private void InitializeControlStyles()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable |
                     ControlStyles.AllPaintingInWmPaint, true);
        }
        #endregion


        #region Initialization and Configuration
        /// <summary>
        /// Initializes the text box with the specified settings.
        /// </summary>
        /// <param name="useSyntaxColoring">Whether to use syntax highlighting.</param>
        /// <param name="justTextBox">Whether to create a simple text box only.</param>
        public void Initialize(bool useSyntaxColoring, bool justTextBox)
        {
            try
            {
                var preservedSettings = PreserveCurrentSettings();
                
                ClearCurrentControls();
                ConfigureBaseProperties();
                
                CreateBackingControl(useSyntaxColoring, justTextBox);
                RestoreSettings(preservedSettings);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ErrorInitializingMessage, ex.Message);
                // Ensure we have at least a simple text box in case of initialization failure
                if (_simpleTextBox == null && _uiTextBox == null)
                {
                    CreateFallbackTextBox();
                }
            }
        }

        private (ContextMenuStrip contextMenu, bool enabled, string text) PreserveCurrentSettings()
        {
            ContextMenuStrip oldContextMenuStrip = null;
            var oldEnabled = true;
            var oldText = string.Empty;

            if (_simpleTextBox != null)
            {
                oldContextMenuStrip = _simpleTextBox.ContextMenuStrip;
                oldEnabled = _simpleTextBox.Enabled;
                oldText = _simpleTextBox.Text;
            }
            else if (_uiTextBox != null)
            {
                oldContextMenuStrip = _uiTextBox.ContextMenuStrip;
                oldEnabled = _uiTextBox.Enabled;
                oldText = _uiTextBox.Text;
            }

            return (oldContextMenuStrip, oldEnabled, oldText);
        }

        private void ClearCurrentControls()
        {
            Controls.Clear();
            
            DisposeTextBoxSafely(_simpleTextBox);
            DisposeTextBoxSafely(_uiTextBox);
            
            _simpleTextBox = null;
            _uiTextBox = null;
        }

        private static void DisposeTextBoxSafely(IDisposable textBox)
        {
            try
            {
                textBox?.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error disposing text box: {0}", ex.Message);
            }
        }

        private void ConfigureBaseProperties()
        {
            BorderStyle = BorderStyle.None;
            Padding = new Padding(DefaultPaddingSize);
            BackColor = SystemColors.WindowFrame;
        }

        private void CreateBackingControl(bool useSyntaxColoring, bool justTextBox)
        {
            if (useSyntaxColoring && !justTextBox)
            {
                _uiTextBox = new AdvancedTextBox 
                { 
                    BorderStyle = BorderStyle.None, 
                    Multiline = true 
                };
                InitializeBackingControl(_uiTextBox);
                UpdateFontAndColors(_uiTextBox);
            }
            else
            {
                _simpleTextBox = new SimpleTextBox 
                { 
                    BorderStyle = BorderStyle.None, 
                    Multiline = true 
                };
                InitializeBackingControl(_simpleTextBox);
                
                if (justTextBox)
                {
                    ConfigureSimpleTextBoxTheme();
                }
                else
                {
                    UpdateFontAndColors(_simpleTextBox);
                }
            }
        }

        private void ConfigureSimpleTextBoxTheme()
        {
            _simpleTextBox.ForeColor = UiUtil.ForeColor;
            _simpleTextBox.BackColor = UiUtil.BackColor;
            BackColor = Color.DarkGray;
            _simpleTextBox.VisibleChanged += OnSimpleTextBoxVisibleChanged;
        }

        private void RestoreSettings((ContextMenuStrip contextMenu, bool enabled, string text) settings)
        {
            try
            {
                if (settings.contextMenu != null)
                {
                    ContextMenuStrip = settings.contextMenu;
                }

                Enabled = settings.enabled;
                Text = settings.text;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error restoring settings: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Creates a fallback simple text box in case of initialization failure.
        /// </summary>
        private void CreateFallbackTextBox()
        {
            try
            {
                _simpleTextBox = new SimpleTextBox 
                { 
                    BorderStyle = BorderStyle.None, 
                    Multiline = true 
                };
                InitializeBackingControl(_simpleTextBox);
                ConfigureSimpleTextBoxTheme();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error creating fallback text box: {0}", ex.Message);
            }
        }

        private void OnSimpleTextBoxVisibleChanged(object sender, EventArgs e)
        {
            Padding = new Padding(DefaultPaddingSize);
            BackColor = Color.DarkGray;
            Invalidate();
        }

        private void InitializeBackingControl(Control textBox)
        {
            if (textBox == null) return;

            Controls.Add(textBox);
            textBox.Dock = DockStyle.Fill;
            
            // Set up event forwarding with better error handling
            SetupEventForwarding(textBox);
        }

        /// <summary>
        /// Sets up event forwarding for the text box control.
        /// </summary>
        /// <param name="textBox">The text box control to set up events for.</param>
        private void SetupEventForwarding(Control textBox)
        {
            try
            {
                textBox.Enter += OnTextBoxEnter;
                textBox.Leave += OnTextBoxLeave;
                textBox.TextChanged += OnTextBoxTextChanged;
                textBox.KeyDown += OnTextBoxKeyDown;
                textBox.MouseClick += OnTextBoxMouseClick;
                textBox.KeyUp += OnTextBoxKeyUp;
                textBox.MouseMove += OnTextBoxMouseMove;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error setting up event forwarding: {0}", ex.Message);
            }
        }

        // Event handler methods for better organization and error handling
        private void OnTextBoxEnter(object sender, EventArgs args)
        {
            BackColor = SystemColors.Highlight;
            Enter?.Invoke(sender, args);
        }

        private void OnTextBoxLeave(object sender, EventArgs args)
        {
            BackColor = SystemColors.WindowFrame;
            Leave?.Invoke(sender, args);
        }

        private void OnTextBoxTextChanged(object sender, EventArgs args) => 
            TextChanged?.Invoke(sender, args);

        private void OnTextBoxKeyDown(object sender, KeyEventArgs args) => 
            KeyDown?.Invoke(sender, args);

        private void OnTextBoxMouseClick(object sender, MouseEventArgs args) => 
            MouseClick?.Invoke(sender, args);

        private void OnTextBoxKeyUp(object sender, KeyEventArgs args) => 
            KeyUp?.Invoke(sender, args);

        private void OnTextBoxMouseMove(object sender, MouseEventArgs args) => 
            MouseMove?.Invoke(sender, args);

        /// <summary>
        /// Unsubscribes from text box events to prevent memory leaks.
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            try
            {
                var activeControl = GetActiveControl();
                if (activeControl != null)
                {
                    activeControl.Enter -= OnTextBoxEnter;
                    activeControl.Leave -= OnTextBoxLeave;
                    activeControl.TextChanged -= OnTextBoxTextChanged;
                    activeControl.KeyDown -= OnTextBoxKeyDown;
                    activeControl.MouseClick -= OnTextBoxMouseClick;
                    activeControl.KeyUp -= OnTextBoxKeyUp;
                    activeControl.MouseMove -= OnTextBoxMouseMove;
                }

                if (_simpleTextBox != null)
                {
                    _simpleTextBox.VisibleChanged -= OnSimpleTextBoxVisibleChanged;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error unsubscribing from events: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Updates the font and colors for the specified text box control.
        /// </summary>
        /// <param name="textBox">The text box control to update.</param>
        public void UpdateFontAndColors(Control textBox)
        {
            if (textBox == null) return;

            try
            {
                var gs = Configuration.Settings.General;
                EnsureDefaultFontName(gs);
                
                var font = CreateTextBoxFont(gs);
                ApplyFontAndColors(textBox, font, gs);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ErrorUpdatingFontMessage, ex.Message);
                // Apply fallback font if font creation fails
                ApplyFallbackFont(textBox);
            }
        }

        private static void EnsureDefaultFontName(GeneralSettings gs)
        {
            if (string.IsNullOrEmpty(gs.SubtitleFontName))
            {
                gs.SubtitleFontName = SystemFonts.DefaultFont.Name;
            }
        }

        private static Font CreateTextBoxFont(GeneralSettings gs)
        {
            var fontStyle = gs.SubtitleTextBoxFontBold ? FontStyle.Bold : FontStyle.Regular;
            return new Font(gs.SubtitleFontName, gs.SubtitleTextBoxFontSize, fontStyle);
        }

        private void ApplyFontAndColors(Control textBox, Font font, GeneralSettings gs)
        {
            _cachedTextBoxFont = font;
            TextBoxFont = font;
            textBox.Font = font;
            textBox.ForeColor = gs.SubtitleFontColor;
            textBox.BackColor = gs.SubtitleBackgroundColor;
        }

        /// <summary>
        /// Applies a fallback font when the primary font fails to load.
        /// </summary>
        /// <param name="textBox">The text box to apply the fallback font to.</param>
        private void ApplyFallbackFont(Control textBox)
        {
            try
            {
                var fallbackFont = SystemFonts.DefaultFont;
                _cachedTextBoxFont = fallbackFont;
                TextBoxFont = fallbackFont;
                textBox.Font = fallbackFont;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error applying fallback font: {0}", ex.Message);
            }
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets or sets the font used by the text box.
        /// </summary>
        public Font TextBoxFont
        {
            get => _cachedTextBoxFont ?? GetActiveControl()?.Font ?? Font;
            set
            {
                _cachedTextBoxFont = value;
                
                if (_simpleTextBox != null)
                {
                    _simpleTextBox.Font = value;
                }

                if (_uiTextBox != null)
                {
                    _uiTextBox.Font = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the text content of the control.
        /// </summary>
        public override string Text
        {
            get => GetActiveControl()?.Text ?? string.Empty;
            set
            {
                var activeControl = GetActiveControl();
                if (activeControl != null)
                {
                    activeControl.Text = value ?? string.Empty;
                }
            }
        }

        /// <summary>
        /// Gets or sets the starting position of text selected in the control.
        /// </summary>
        public int SelectionStart
        {
            get => GetTextBoxProperty(tb => tb.SelectionStart, 0);
            set => SetTextBoxProperty(tb => tb.SelectionStart = value);
        }

        /// <summary>
        /// Gets or sets the number of characters selected in the control.
        /// </summary>
        public int SelectionLength
        {
            get => GetTextBoxProperty(tb => tb.SelectionLength, 0);
            set => SetTextBoxProperty(tb => tb.SelectionLength = value);
        }

        /// <summary>
        /// Gets or sets whether the selection is hidden when the control loses focus.
        /// </summary>
        public bool HideSelection
        {
            get => GetTextBoxProperty(tb => tb.HideSelection, false);
            set => SetTextBoxProperty(tb => tb.HideSelection = value);
        }

        /// <summary>
        /// Gets or sets the currently selected text in the control.
        /// </summary>
        public string SelectedText
        {
            get => GetTextBoxProperty(tb => tb.SelectedText, string.Empty);
            set => SetTextBoxProperty(tb => tb.SelectedText = value);
        }

        /// <summary>
        /// Gets or sets whether the control supports multiline text.
        /// </summary>
        public bool Multiline
        {
            get => GetTextBoxProperty(tb => tb.Multiline, false);
            set => SetTextBoxProperty(tb => tb.Multiline = value);
        }

        /// <summary>
        /// Gets or sets whether the control is enabled.
        /// </summary>
        public new bool Enabled
        {
            get => GetActiveControl()?.Enabled ?? true;
            set => SetTextBoxProperty(tb => tb.Enabled = value);
        }

        /// <summary>
        /// Gets whether the control currently has focus.
        /// </summary>
        public override bool Focused => GetActiveControl()?.Focused ?? false;

        /// <summary>
        /// Gets or sets the maximum number of characters that can be entered.
        /// </summary>
        public int MaxLength
        {
            get => GetTextBoxProperty(tb => tb.MaxLength, 0);
            set => SetTextBoxProperty(tb => tb.MaxLength = value);
        }

        /// <summary>
        /// Gets or sets whether the text should be displayed as a password.
        /// </summary>
        public bool UseSystemPasswordChar
        {
            get => _simpleTextBox?.UseSystemPasswordChar ?? false;
            set
            {
                if (_simpleTextBox != null)
                {
                    _simpleTextBox.UseSystemPasswordChar = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the text is read-only.
        /// </summary>
        public bool ReadOnly
        {
            get => GetTextBoxProperty(tb => tb.ReadOnly, false);
            set => SetTextBoxProperty(tb => tb.ReadOnly = value);
        }

        /// <summary>
        /// Gets the lines of text in the control.
        /// </summary>
        public string[] Lines => GetTextBoxProperty(tb => tb.Lines, Array.Empty<string>());

        /// <summary>
        /// Gets or sets the context menu strip for the control.
        /// </summary>
        public override ContextMenuStrip ContextMenuStrip
        {
            get => GetActiveControl() switch
            {
                SimpleTextBox simple => simple.ContextMenuStrip,
                AdvancedTextBox advanced => advanced.ContextMenuStrip,
                _ => null
            };
            set => SetTextBoxProperty(tb => tb.ContextMenuStrip = value);
        }
        #endregion

        #region ScrollBars Property
        /// <summary>
        /// Gets or sets the scrollbar settings for the control.
        /// </summary>
        public RichTextBoxScrollBars ScrollBars
        {
            get
            {
                if (_simpleTextBox == null && _uiTextBox == null)
                {
                    return RichTextBoxScrollBars.None;
                }

                if (_simpleTextBox != null)
                {
                    return _simpleTextBox.ScrollBars switch
                    {
                        System.Windows.Forms.ScrollBars.Both => RichTextBoxScrollBars.Both,
                        System.Windows.Forms.ScrollBars.Horizontal => RichTextBoxScrollBars.Horizontal,
                        System.Windows.Forms.ScrollBars.Vertical => RichTextBoxScrollBars.Vertical,
                        _ => RichTextBoxScrollBars.None
                    };
                }

                return _uiTextBox.ScrollBars;
            }
            set
            {
                if (_simpleTextBox != null)
                {
                    _simpleTextBox.ScrollBars = value switch
                    {
                        RichTextBoxScrollBars.Both or RichTextBoxScrollBars.ForcedBoth => System.Windows.Forms.ScrollBars.Both,
                        RichTextBoxScrollBars.Horizontal or RichTextBoxScrollBars.ForcedHorizontal => System.Windows.Forms.ScrollBars.Horizontal,
                        RichTextBoxScrollBars.Vertical or RichTextBoxScrollBars.ForcedVertical => System.Windows.Forms.ScrollBars.Vertical,
                        _ => System.Windows.Forms.ScrollBars.None
                    };
                }
                // Rich text box auto show/hide scrollbars work automatically
            }
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Selects all text in the control.
        /// </summary>
        public void SelectAll() => ExecuteOnActiveControl(tb => tb.SelectAll());

        /// <summary>
        /// Clears all text from the control.
        /// </summary>
        public void Clear() => ExecuteOnActiveControl(tb => tb.Clear());

        /// <summary>
        /// Undoes the last operation.
        /// </summary>
        public void Undo() => ExecuteOnActiveControl(tb => tb.Undo());

        /// <summary>
        /// Clears the undo buffer.
        /// </summary>
        public void ClearUndo() => ExecuteOnActiveControl(tb => tb.ClearUndo());

        /// <summary>
        /// Copies the selected text to the clipboard.
        /// </summary>
        public void Copy() => ExecuteOnActiveControl(tb => tb.Copy());

        /// <summary>
        /// Cuts the selected text to the clipboard.
        /// </summary>
        public void Cut() => ExecuteOnActiveControl(tb => tb.Cut());

        /// <summary>
        /// Pastes text from the clipboard.
        /// </summary>
        public void Paste() => ExecuteOnActiveControl(tb => tb.Paste());

        /// <summary>
        /// Sets focus to the control.
        /// </summary>
        public new void Focus() => GetActiveControl()?.Focus();

        /// <summary>
        /// Scrolls the control to show the caret position.
        /// </summary>
        public void ScrollToCaret() => ExecuteOnActiveControl(tb => tb.ScrollToCaret());
        #endregion

        #region Live Spell Check Properties and Methods
        /// <summary>
        /// Gets or sets the current line index for spell checking.
        /// </summary>
        public int CurrentLineIndex
        {
            get => _uiTextBox?.CurrentLineIndex ?? 0;
            set
            {
                if (_uiTextBox != null)
                {
                    _uiTextBox.CurrentLineIndex = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the current language for spell checking.
        /// </summary>
        public string CurrentLanguage
        {
            get => _uiTextBox?.CurrentLanguage ?? string.Empty;
            set
            {
                if (_uiTextBox != null)
                {
                    _uiTextBox.CurrentLanguage = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the language has changed.
        /// </summary>
        public bool LanguageChanged
        {
            get => _uiTextBox?.LanguageChanged ?? false;
            set
            {
                if (_uiTextBox != null)
                {
                    _uiTextBox.LanguageChanged = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the current word is misspelled.
        /// </summary>
        public bool IsWrongWord
        {
            get => _uiTextBox?.IsWrongWord ?? false;
            set
            {
                if (_uiTextBox != null)
                {
                    _uiTextBox.IsWrongWord = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the spell checker is initialized.
        /// </summary>
        public bool IsSpellCheckerInitialized
        {
            get => _uiTextBox?.IsSpellCheckerInitialized ?? false;
            set
            {
                if (_uiTextBox != null)
                {
                    _uiTextBox.IsSpellCheckerInitialized = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the dictionary is downloaded.
        /// </summary>
        public bool IsDictionaryDownloaded
        {
            get => _uiTextBox?.IsDictionaryDownloaded ?? true;
            set
            {
                if (_uiTextBox != null)
                {
                    _uiTextBox.IsDictionaryDownloaded = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets whether spell check is requested.
        /// </summary>
        public bool IsSpellCheckRequested
        {
            get => _uiTextBox?.IsSpellCheckRequested ?? false;
            set
            {
                if (_uiTextBox != null)
                {
                    _uiTextBox.IsSpellCheckRequested = value;
                }
            }
        }

        /// <summary>
        /// Checks for language changes in the subtitle.
        /// </summary>
        /// <param name="subtitle">The subtitle to check.</param>
        public void CheckForLanguageChange(Subtitle subtitle) => _uiTextBox?.CheckForLanguageChange(subtitle);

        /// <summary>
        /// Initializes live spell checking for a specific line.
        /// </summary>
        /// <param name="subtitle">The subtitle containing the line.</param>
        /// <param name="lineNumber">The line number to check.</param>
        public void InitializeLiveSpellCheck(Subtitle subtitle, int lineNumber) => 
            _uiTextBox?.InitializeLiveSpellCheck(subtitle, lineNumber);

        /// <summary>
        /// Disposes spell check resources and dictionaries.
        /// </summary>
        public void DisposeHunspellAndDictionaries() => _uiTextBox?.DisposeHunspellAndDictionaries();

        /// <summary>
        /// Adds spell check suggestions to the context menu.
        /// </summary>
        public void AddSuggestionsToMenu() => _uiTextBox?.AddSuggestionsToMenu();

        /// <summary>
        /// Performs a spell check action.
        /// </summary>
        /// <param name="action">The action to perform.</param>
        public void DoAction(SpellCheckAction action) => _uiTextBox?.DoAction(action);
        #endregion

        #region Theme Support
        /// <summary>
        /// Applies dark theme styling to the control.
        /// </summary>
        public void SetDarkTheme()
        {
            try
            {
                if (_uiTextBox != null)
                {
                    ApplyDarkTheme(_uiTextBox);
                }
                
                if (_simpleTextBox != null)
                {
                    ApplyDarkTheme(_simpleTextBox);
                }
                
                DarkTheme.SetWindowThemeDark(this);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ErrorApplyingThemeMessage, ex.Message);
            }
        }

        /// <summary>
        /// Removes dark theme styling from the control.
        /// </summary>
        public void UndoDarkTheme()
        {
            try
            {
                if (_uiTextBox != null)
                {
                    RemoveDarkTheme(_uiTextBox);
                }
                
                if (_simpleTextBox != null)
                {
                    RemoveDarkTheme(_simpleTextBox);
                }
                
                DarkTheme.SetWindowThemeNormal(this);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ErrorRemovingThemeMessage, ex.Message);
            }
        }

        private static void ApplyDarkTheme(Control control)
        {
            control.BackColor = DarkTheme.BackColor;
            control.ForeColor = DarkTheme.ForeColor;
            control.HandleCreated += OnControlHandleCreated;
            DarkTheme.SetWindowThemeDark(control);
        }

        private static void RemoveDarkTheme(Control control)
        {
            control.BackColor = SystemColors.Window;
            control.ForeColor = SystemColors.ControlText;
            control.HandleCreated -= OnControlHandleCreated;
            DarkTheme.SetWindowThemeNormal(control);
        }

        private static void OnControlHandleCreated(object sender, EventArgs e) => 
            DarkTheme.SetWindowThemeDark((Control)sender);
        #endregion
        #region Helper Methods
        /// <summary>
        /// Gets the currently active text box control.
        /// </summary>
        /// <returns>The active text box control or null if none exists.</returns>
        private Control GetActiveControl()
        {
            // Use pattern matching for better performance
            return _simpleTextBox as Control ?? _uiTextBox as Control;
        }

        /// <summary>
        /// Gets a property value from the active text box control.
        /// </summary>
        /// <typeparam name="T">The type of the property value.</typeparam>
        /// <param name="propertyGetter">Function to get the property from a text box.</param>
        /// <param name="defaultValue">Default value if no active control exists.</param>
        /// <returns>The property value or default value.</returns>
        private T GetTextBoxProperty<T>(Func<dynamic, T> propertyGetter, T defaultValue)
        {
            var activeControl = GetActiveControl();
            return activeControl != null ? propertyGetter(activeControl) : defaultValue;
        }

        /// <summary>
        /// Sets a property value on the active text box control.
        /// </summary>
        /// <param name="propertySetter">Action to set the property on a text box.</param>
        private void SetTextBoxProperty(Action<dynamic> propertySetter)
        {
            var activeControl = GetActiveControl();
            if (activeControl != null)
            {
                try
                {
                    propertySetter(activeControl);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ErrorSettingPropertyMessage, ex.Message);
                }
            }
        }

        /// <summary>
        /// Executes an action on the active text box control.
        /// </summary>
        /// <param name="action">The action to execute.</param>
        private void ExecuteOnActiveControl(Action<dynamic> action)
        {
            var activeControl = GetActiveControl();
            if (activeControl != null)
            {
                try
                {
                    action(activeControl);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ErrorExecutingActionMessage, ex.Message);
                }
            }
        }
        #endregion

        #region IDisposable Implementation
        /// <summary>
        /// Releases all resources used by the SETextBox control.
        /// </summary>
        /// <param name="disposing">True if disposing managed resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    // Unsubscribe from events to prevent memory leaks
                    UnsubscribeFromEvents();
                    
                    // Dispose text box controls
                    DisposeTextBoxSafely(_simpleTextBox);
                    DisposeTextBoxSafely(_uiTextBox);
                    
                    // Dispose cached font
                    _cachedTextBoxFont?.Dispose();
                    
                    // Clear references
                    _simpleTextBox = null;
                    _uiTextBox = null;
                    _cachedTextBoxFont = null;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ErrorDisposingMessage, ex.Message);
                }
                finally
                {
                    _disposed = true;
                }
            }

            base.Dispose(disposing);
        }
        #endregion
    }
}
