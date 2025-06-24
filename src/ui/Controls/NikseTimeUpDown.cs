using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Controls
{
    [Category("NikseTimeUpDown"), Description("Subtitle time with better support for color theme")]
    public sealed class NikseTimeUpDown : Control, IDisposable
    {
        #region Constants and Configuration
        private const int NumericUpDownValue = 50;
        private const int ButtonsWidth = 13;
        private const int RepeatTimerInitialInterval = 300;
        private const int RepeatTimerFastInterval = 10;
        private const int RepeatTimerSlowInterval = 75;
        private const int RepeatTimerThreshold = 8;
        private const double MaxTimeThreshold = 0.1;
        #endregion

        #region Events and Enums
        /// <summary>
        /// Occurs when the time code value changes.
        /// </summary>
        public event EventHandler TimeCodeChanged;

        public enum TimeMode
        {
            HHMMSSMS,
            HHMMSSFF
        }
        #endregion

        #region Private Fields
        private bool _isLoading = true;
        private bool _forceHHMMSSFF;
        private bool _dirty;
        private double _initialTotalMilliseconds;
        private static readonly char[] SplitChars = GetSplitChars();
        
        // Timer fields
        private readonly Timer _repeatTimer;
        private bool _repeatTimerArrowUp;
        private int _repeatCount;
        
        // Mouse state fields
        private bool _buttonUpActive;
        private bool _buttonDownActive;
        private bool _buttonLeftIsDown;
        private int _mouseX;
        private int _mouseY;
        private bool _upDownMouseEntered;
        
        // UI Components
        private readonly MaskedTextBox _maskedTextBox;
        
        // Color and brush fields
        private Color _buttonForeColor;
        private Brush _buttonForeColorBrush;
        private Color _buttonForeColorOver;
        private Brush _buttonForeColorOverBrush;
        private Color _buttonForeColorDown;
        private Brush _buttonForeColorDownBrush;
        private Color _borderColor;
        private Color _backColorDisabled;
        private Color _borderColorDisabled;
        
        private bool _disposed;
        #endregion

        #region Public Properties
        /// <summary>
        /// Gets or sets whether to use video offset for time calculations.
        /// </summary>
        public bool UseVideoOffset { get; set; }

        /// <summary>
        /// Forces the time format to HH:MM:SS:FF.
        /// </summary>
        internal void ForceHHMMSSFF()
        {
            _forceHHMMSSFF = true;
            _maskedTextBox.Mask = "00:00:00:00";
        }

        /// <summary>
        /// Sets the control to auto-adjust its width.
        /// </summary>
        public void SetAutoWidth() => Invalidate();

        /// <summary>
        /// Gets the current time mode based on configuration and force settings.
        /// </summary>
        public TimeMode Mode =>
            _forceHHMMSSFF || Configuration.Settings?.General.UseTimeFormatHHMMSSFF == true
                ? TimeMode.HHMMSSFF
                : TimeMode.HHMMSSMS;

        [Category("NikseTimeUpDown"), Description("Gets or sets the increment value"), DefaultValue(100)]
        public decimal Increment { get; set; }

        [Category("NikseTimeUpDown"), Description("Allow arrow keys to set increment/decrement value")]
        [DefaultValue(true)]
        public bool InterceptArrowKeys { get; set; }
        #endregion

        #region Color Properties
        [Category("NikseTimeUpDown"), Description("Gets or sets the button foreground color"),
         RefreshProperties(RefreshProperties.Repaint)]
        public Color ButtonForeColor
        {
            get => _buttonForeColor;
            set
            {
                if (value.A == 0) return;

                _buttonForeColor = value;
                SetBrush(ref _buttonForeColorBrush, value);
                Invalidate();
            }
        }

        [Category("NikseTimeUpDown"), Description("Gets or sets the button foreground mouse over color"), 
         RefreshProperties(RefreshProperties.Repaint)]
        public Color ButtonForeColorOver
        {
            get => _buttonForeColorOver;
            set
            {
                if (value.A == 0) return;

                _buttonForeColorOver = value;
                SetBrush(ref _buttonForeColorOverBrush, value);
                Invalidate();
            }
        }

        [Category("NikseTimeUpDown"), Description("Gets or sets the button foreground mouse down color"), 
         RefreshProperties(RefreshProperties.Repaint)]
        public Color ButtonForeColorDown
        {
            get => _buttonForeColorDown;
            set
            {
                if (value.A == 0) return;

                _buttonForeColorDown = value;
                SetBrush(ref _buttonForeColorDownBrush, value);
                Invalidate();
            }
        }

        [Category("NikseTimeUpDown"), Description("Gets or sets the border color"), 
         RefreshProperties(RefreshProperties.Repaint)]
        public Color BorderColor
        {
            get => _borderColor;
            set
            {
                if (value.A == 0) return;

                _borderColor = value;
                Invalidate();
            }
        }

        [Category("NikseTimeUpDown"), Description("Gets or sets the disabled background color"),
         RefreshProperties(RefreshProperties.Repaint)]
        public Color BackColorDisabled
        {
            get => _backColorDisabled;
            set
            {
                if (value.A == 0) return;

                _backColorDisabled = value;
                Invalidate();
            }
        }

        [Category("NikseTimeUpDown"), Description("Gets or sets the disabled border color"), 
         RefreshProperties(RefreshProperties.Repaint)]
        public Color BorderColorDisabled
        {
            get => _borderColorDisabled;
            set
            {
                if (value.A == 0) return;

                _borderColorDisabled = value;
                Invalidate();
            }
        }
        #endregion

        #region Overridden Properties
        [RefreshProperties(RefreshProperties.Repaint)]
        public new bool Enabled
        {
            get => base.Enabled;
            set
            {
                if (value == Enabled) return;

                base.Enabled = value;
                Invalidate();
            }
        }

        [RefreshProperties(RefreshProperties.Repaint)]
        public new int Height
        {
            get => base.Height;
            set
            {
                if (_maskedTextBox != null)
                {
                    _maskedTextBox.Height = value - 4;
                }

                base.Height = value;
                Invalidate();
            }
        }

        [RefreshProperties(RefreshProperties.Repaint)]
        public new int Width
        {
            get => base.Width;
            set
            {
                if (_maskedTextBox != null)
                {
                    _maskedTextBox.Width = Width - ButtonsWidth - 3;
                }

                base.Width = value;
                Invalidate();
            }
        }

        [RefreshProperties(RefreshProperties.Repaint)]
        public override RightToLeft RightToLeft
        {
            get => base.RightToLeft;
            set
            {
                base.RightToLeft = value;
                Application.DoEvents();
                Invalidate();
            }
        }

        [RefreshProperties(RefreshProperties.Repaint)]
        public override Color ForeColor
        {
            get => base.ForeColor;
            set
            {
                base.ForeColor = value;
                if (_maskedTextBox != null)
                {
                    _maskedTextBox.ForeColor = value;
                }
                Invalidate();
            }
        }

        [RefreshProperties(RefreshProperties.Repaint), DefaultValue(typeof(Color), "0xFFFFFFFF")]
        public override Color BackColor
        {
            get => base.BackColor;
            set
            {
                base.BackColor = value;
                if (_maskedTextBox != null)
                {
                    _maskedTextBox.BackColor = value;
                }
                Invalidate();
            }
        }
        #endregion

        #region Constructor and Initialization
        public NikseTimeUpDown()
        {
            InitializeComponent();
            InitializeColors();
            InitializeTimer();
            InitializeEventHandlers();
            
            TabStop = false;
            _isLoading = false;
        }

        private void InitializeComponent()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable |
                     ControlStyles.AllPaintingInWmPaint, true);

            _maskedTextBox = new MaskedTextBox
            {
                BorderStyle = BorderStyle.None,
                Font = UiUtil.GetDefaultFont(),
                Left = 2,
                InsertKeyMode = InsertKeyMode.Overwrite
            };

            Height = 23;
            _maskedTextBox.Top = (Height - _maskedTextBox.Height) / 2;
            
            Controls.Add(_maskedTextBox);
            BackColor = SystemColors.Window;
            InterceptArrowKeys = true;
            Increment = 100;
        }

        private void InitializeColors()
        {
            ButtonForeColor = DefaultForeColor;
            ButtonForeColorOver = Color.FromArgb(0, 120, 215);
            ButtonForeColorDown = Color.Orange;
            BorderColor = Color.FromArgb(171, 173, 179);
            BorderColorDisabled = Color.FromArgb(120, 120, 120);
            BackColorDisabled = Color.FromArgb(240, 240, 240);
        }

        private void InitializeTimer()
        {
            _repeatTimer = new Timer();
            _repeatTimer.Tick += OnRepeatTimerTick;
        }

        private void InitializeEventHandlers()
        {
            _maskedTextBox.FontChanged += (o, args) =>
            {
                base.OnFontChanged(args);
                Invalidate();
            };
            
            _maskedTextBox.KeyPress += OnTextBoxKeyPress;
            _maskedTextBox.KeyDown += OnTextBoxKeyDown;
            _maskedTextBox.LostFocus += OnTextBoxLostFocus;
            _maskedTextBox.GotFocus += (sender, args) => Invalidate();
            _maskedTextBox.MouseDown += OnTextBoxMouseDown;
            _maskedTextBox.MouseEnter += (sender, args) => { _upDownMouseEntered = true; };
            _maskedTextBox.MouseLeave += (sender, args) => { _upDownMouseEntered = false; };

            LostFocus += (sender, args) => _repeatTimer.Stop();
            MouseWheel += OnMouseWheel;
        }
        #endregion

        #region Public Methods and Properties
        /// <summary>
        /// Gets the underlying MaskedTextBox control.
        /// </summary>
        public MaskedTextBox MaskedTextBox => _maskedTextBox;

        /// <summary>
        /// Sets the total milliseconds for the time code.
        /// </summary>
        /// <param name="milliseconds">The milliseconds to set.</param>
        public void SetTotalMilliseconds(double milliseconds)
        {
            try
            {
                _dirty = false;
                _initialTotalMilliseconds = milliseconds;
                
                if (UseVideoOffset)
                {
                    milliseconds += Configuration.Settings.General.CurrentVideoOffsetInMs;
                }

                var mask = Mode == TimeMode.HHMMSSMS 
                    ? GetMask(milliseconds) 
                    : GetMaskFrames(milliseconds);

                if (_maskedTextBox.Mask != mask)
                {
                    _maskedTextBox.Mask = mask;
                }

                if (Mode == TimeMode.HHMMSSMS)
                {
                    _maskedTextBox.Text = new TimeCode(milliseconds).ToString();
                }
                else
                {
                    var tc = new TimeCode(milliseconds);
                    var framesPart = Core.SubtitleFormats.SubtitleFormat.MillisecondsToFrames(tc.Milliseconds);
                    _maskedTextBox.Text = $"{tc.ToString().Substring(0, 9)}{framesPart:00}";
                }

                _dirty = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SetTotalMilliseconds: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the total milliseconds from the current time code.
        /// </summary>
        /// <returns>The total milliseconds or null if invalid.</returns>
        public double? GetTotalMilliseconds() => _dirty ? TimeCode?.TotalMilliseconds : _initialTotalMilliseconds;
        #endregion

        #region TimeCode Property
        [RefreshProperties(RefreshProperties.Repaint)]
        public TimeCode TimeCode
        {
            get
            {
                if (_isLoading) return new TimeCode();

                var textContent = _maskedTextBox.Text.RemoveChar('.').Replace(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator, string.Empty).RemoveChar(',', ':');
                if (string.IsNullOrWhiteSpace(textContent))
                {
                    return new TimeCode(TimeCode.MaxTimeTotalMilliseconds);
                }

                if (!_dirty) return new TimeCode(_initialTotalMilliseconds);

                return ParseTimeCode();
            }
            set
            {
                if (_isLoading) return;

                if (value != null)
                {
                    _dirty = false;
                    _initialTotalMilliseconds = value.TotalMilliseconds;
                }

                if (value == null || value.TotalMilliseconds >= TimeCode.MaxTimeTotalMilliseconds - MaxTimeThreshold)
                {
                    _maskedTextBox.Text = string.Empty;
                    return;
                }

                SetTimeCodeValue(value);
                Invalidate();
            }
        }

        private TimeCode ParseTimeCode()
        {
            try
            {
                var startTime = _maskedTextBox.Text;
                var isNegative = startTime.StartsWith('-');
                startTime = startTime.TrimStart('-').Replace(' ', '0');

                if (Mode == TimeMode.HHMMSSMS)
                {
                    return ParseTimeCodeMilliseconds(startTime, isNegative);
                }
                else
                {
                    return ParseTimeCodeFrames(startTime, isNegative);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing time code: {ex.Message}");
                return null;
            }
        }

        private TimeCode ParseTimeCodeMilliseconds(string startTime, bool isNegative)
        {
            if (startTime.EndsWith(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator, StringComparison.Ordinal))
            {
                startTime += "000";
            }

            var times = startTime.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
            if (times.Length != 4) return null;

            if (!int.TryParse(times[0], out var hours) ||
                !int.TryParse(times[1], out var minutes) ||
                !int.TryParse(times[2], out var seconds) ||
                !int.TryParse(times[3].PadRight(3, '0'), out var milliseconds))
            {
                return null;
            }

            minutes = Math.Min(minutes, 59);
            seconds = Math.Min(seconds, 59);

            var tc = new TimeCode(hours, minutes, seconds, milliseconds);
            return ApplyOffsetAndSign(tc, isNegative);
        }

        private TimeCode ParseTimeCodeFrames(string startTime, bool isNegative)
        {
            if (startTime.EndsWith(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator, StringComparison.Ordinal) || startTime.EndsWith(':'))
            {
                startTime += "00";
            }

            var times = startTime.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);
            if (times.Length != 4) return null;

            if (!int.TryParse(times[0], out var hours) ||
                !int.TryParse(times[1], out var minutes) ||
                !int.TryParse(times[2], out var seconds) ||
                !int.TryParse(times[3], out var frames))
            {
                return null;
            }

            var milliseconds = Core.SubtitleFormats.SubtitleFormat.FramesToMillisecondsMax999(frames);
            var tc = new TimeCode(hours, minutes, seconds, milliseconds);
            return ApplyOffsetAndSign(tc, isNegative);
        }

        private TimeCode ApplyOffsetAndSign(TimeCode tc, bool isNegative)
        {
            if (UseVideoOffset)
            {
                tc.TotalMilliseconds -= Configuration.Settings.General.CurrentVideoOffsetInMs;
            }

            if (isNegative)
            {
                tc.TotalMilliseconds *= -1;
            }

            return tc;
        }

        private void SetTimeCodeValue(TimeCode value)
        {
            var v = new TimeCode(value.TotalMilliseconds);
            if (UseVideoOffset)
            {
                v.TotalMilliseconds += Configuration.Settings.General.CurrentVideoOffsetInMs;
            }

            if (Mode == TimeMode.HHMMSSMS)
            {
                _maskedTextBox.Mask = GetMask(v.TotalMilliseconds);
                _maskedTextBox.Text = v.ToString();
            }
            else
            {
                _maskedTextBox.Mask = GetMaskFrames(v.TotalMilliseconds);
                _maskedTextBox.Text = v.ToHHMMSSFF();
            }
        }
        #endregion

        #region Helper Methods
        private static string GetMask(double val) => val >= 0 ? "00:00:00.000" : "-00:00:00.000";

        private static string GetMaskFrames(double val) => val >= 0 ? "00:00:00:00" : "-00:00:00:00";

        private static char[] GetSplitChars()
        {
            var splitChars = new List<char> { ':', ',', '.' };
            var cultureSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            if (cultureSeparator.Length == 1)
            {
                var ch = Convert.ToChar(cultureSeparator);
                if (!splitChars.Contains(ch))
                {
                    splitChars.Add(ch);
                }
            }

            return splitChars.ToArray();
        }

        private void SetBrush(ref Brush brush, Color color)
        {
            brush?.Dispose();
            brush = new SolidBrush(color);
        }
        #endregion

        #region Event Handlers
        private void OnRepeatTimerTick(object sender, EventArgs args)
        {
            if (_repeatTimerArrowUp)
            {
                AddValue(Increment);
            }
            else
            {
                AddValue(-Increment);
            }

            _repeatCount++;
            _repeatTimer.Interval = _repeatCount < RepeatTimerThreshold ? RepeatTimerSlowInterval : RepeatTimerFastInterval;
        }

        private void OnMouseWheel(object sender, MouseEventArgs e)
        {
            if (_maskedTextBox == null) return;

            if (e.Delta > 0)
            {
                AddValue(Increment);
            }
            else if (e.Delta < 0)
            {
                AddValue(-Increment);
            }
        }

        private void OnTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (InterceptArrowKeys && e.KeyCode == Keys.Down)
            {
                AddValue(-Increment);
                e.Handled = true;
            }
            else if (InterceptArrowKeys && e.KeyCode == Keys.Up)
            {
                AddValue(Increment);
                e.Handled = true;
            }
            else if (e.KeyData == Keys.Enter)
            {
                if (!_maskedTextBox.MaskCompleted)
                {
                    AddValue(0);
                }
                Invalidate();
            }
            else if (e.Modifiers == Keys.Control)
            {
                HandleControlKeys(e);
            }
            else if (ShouldMarkDirty(e.KeyData))
            {
                _dirty = true;
                Invalidate();
            }
        }

        private void HandleControlKeys(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.A:
                    _maskedTextBox.SelectAll();
                    e.SuppressKeyPress = true;
                    break;
                case Keys.C:
                    _maskedTextBox.Copy();
                    e.SuppressKeyPress = true;
                    break;
                case Keys.V:
                    _maskedTextBox.Paste();
                    e.SuppressKeyPress = true;
                    break;
            }
        }

        private static bool ShouldMarkDirty(Keys keyData)
        {
            return keyData != (Keys.Tab | Keys.Shift) &&
                   keyData != Keys.Tab &&
                   keyData != Keys.Left &&
                   keyData != Keys.Right;
        }

        private void OnTextBoxLostFocus(object sender, EventArgs args)
        {
            AddValue(0);
            Invalidate();
        }

        private void OnTextBoxMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                _dirty = true;
            }
        }

        /// <summary>
        /// Handles key press events for the text box, allowing only valid characters.
        /// </summary>
        private void OnTextBoxKeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                AddValue(0);
                e.Handled = false;
            }
            else if (char.IsDigit(e.KeyChar) || e.KeyChar == (char)Keys.Back)
            {
                e.Handled = false;
                Invalidate();
            }
            else
            {
                e.Handled = true;
            }
        }
        #endregion

        #region Value Manipulation
        /// <summary>
        /// Increments or decrements the TextBox value.
        /// </summary>
        /// <param name="value">Value to increment/decrement</param>
        private void AddValue(decimal value)
        {
            try
            {
                _dirty = true;
                var milliseconds = GetTotalMilliseconds();
                if (!milliseconds.HasValue) return;

                if (milliseconds.Value >= TimeCode.MaxTimeTotalMilliseconds - MaxTimeThreshold)
                {
                    milliseconds = 0;
                }

                if (Mode == TimeMode.HHMMSSMS)
                {
                    SetTotalMilliseconds(milliseconds.Value + (double)value);
                }
                else
                {
                    ProcessFrameBasedValue(milliseconds.Value, value);
                }

                TimeCodeChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in AddValue: {ex.Message}");
            }
        }

        private void ProcessFrameBasedValue(double milliseconds, decimal value)
        {
            if (value == 0)
            {
                SetTotalMilliseconds(milliseconds);
            }
            else if (value > NumericUpDownValue)
            {
                var frameTime = Core.SubtitleFormats.SubtitleFormat.FramesToMilliseconds(1);
                SetTotalMilliseconds(milliseconds + frameTime);
            }
            else if (value < NumericUpDownValue)
            {
                var frameTime = Core.SubtitleFormats.SubtitleFormat.FramesToMilliseconds(1);
                SetTotalMilliseconds(milliseconds - frameTime);
            }
        }
        #endregion

        #region Overridden Event Handlers
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (!_maskedTextBox.Focused && e.KeyCode == (Keys.Control | Keys.C))
            {
                _maskedTextBox.Copy();
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _buttonUpActive = false;
            _buttonDownActive = false;
            _upDownMouseEntered = true;
            base.OnMouseEnter(e);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _buttonUpActive = false;
            _buttonDownActive = false;
            _upDownMouseEntered = false;
            base.OnMouseLeave(e);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _buttonLeftIsDown = true;

                if (_buttonUpActive)
                {
                    AddValue(Increment);
                    _repeatTimerArrowUp = true;
                    _repeatTimer.Interval = RepeatTimerInitialInterval;
                    _repeatCount = 0;
                    _repeatTimer.Start();
                }
                else if (_buttonDownActive)
                {
                    AddValue(-Increment);
                    _repeatTimerArrowUp = false;
                    _repeatTimer.Interval = RepeatTimerInitialInterval;
                    _repeatCount = 0;
                    _repeatTimer.Start();
                }

                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _repeatTimer.Stop();

            if (_buttonLeftIsDown)
            {
                _buttonLeftIsDown = false;
                Invalidate();
            }
            base.OnMouseUp(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var left = RightToLeft == RightToLeft.Yes ? 0 : Width - ButtonsWidth;
            var right = RightToLeft == RightToLeft.Yes ? ButtonsWidth : Width;
            var height = Height / 2 - 3;
            const int top = 2;

            _mouseX = e.X;
            _mouseY = e.Y;

            if (_mouseX >= left && _mouseX <= right)
            {
                UpdateButtonStates(top, height);
            }
            else
            {
                if (_buttonUpActive || _buttonDownActive)
                {
                    _buttonUpActive = false;
                    _buttonDownActive = false;
                    Invalidate();
                }
                _repeatTimer.Stop();
            }

            base.OnMouseMove(e);
        }

        private void UpdateButtonStates(int top, int height)
        {
            if (_mouseY > top + height)
            {
                if (!_buttonDownActive)
                {
                    _buttonUpActive = false;
                    _buttonDownActive = true;
                    Invalidate();
                }
            }
            else
            {
                if (!_buttonUpActive)
                {
                    _buttonUpActive = true;
                    _buttonDownActive = false;
                    Invalidate();
                }
            }
        }
        #endregion

        #region Painting and Rendering
        protected override void OnPaint(PaintEventArgs e)
        {
            if (_isLoading) return;

            ConfigureRendering(e.Graphics);
            UpdateMaskedTextBoxLayout();

            if (!Enabled)
            {
                DrawDisabled(e);
                return;
            }

            DrawEnabled(e);
        }

        private void ConfigureRendering(Graphics graphics)
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
        }

        private void UpdateMaskedTextBoxLayout()
        {
            _maskedTextBox.BackColor = BackColor;
            _maskedTextBox.ForeColor = ButtonForeColor;
            _maskedTextBox.Left = RightToLeft == RightToLeft.Yes ? ButtonsWidth : 3;
            _maskedTextBox.Width = Width - ButtonsWidth - 3;
            _maskedTextBox.Invalidate();
        }

        private void DrawEnabled(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            
            DrawBorder(e.Graphics);
            DrawUpArrow(e.Graphics);
            DrawDownArrow(e.Graphics);
        }

        private void DrawBorder(Graphics graphics)
        {
            var borderColor = _maskedTextBox.Focused || _upDownMouseEntered ? _buttonForeColorOver : BorderColor;
            using (var pen = new Pen(borderColor, 1f))
            {
                var borderRectangle = new Rectangle(0, 0, Width - 1, Height - 1);
                graphics.DrawRectangle(pen, borderRectangle);
            }
        }

        private void DrawUpArrow(Graphics graphics)
        {
            var brush = GetArrowBrush(_buttonUpActive);
            var left = RightToLeft == RightToLeft.Yes ? 3 : Width - ButtonsWidth;
            var height = Height / 2 - 4;
            const int top = 2;
            
            NikseUpDown.DrawArrowUp(graphics, brush, left, top, height);
        }

        private void DrawDownArrow(Graphics graphics)
        {
            var brush = GetArrowBrush(_buttonDownActive);
            var left = RightToLeft == RightToLeft.Yes ? 3 : Width - ButtonsWidth;
            var height = Height / 2 - 4;
            var top = height + 5;
            
            NikseUpDown.DrawArrowDown(graphics, brush, left, top, height);
        }

        private Brush GetArrowBrush(bool isActive)
        {
            if (!isActive) return _buttonForeColorBrush;
            
            return _buttonLeftIsDown ? _buttonForeColorDownBrush : _buttonForeColorOverBrush;
        }

        private void DrawDisabled(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColorDisabled);
            
            using (var pen = new Pen(BorderColorDisabled, 1f))
            {
                var borderRectangle = new Rectangle(0, 0, Width - 1, Height - 1);
                e.Graphics.DrawRectangle(pen, borderRectangle);
            }

            var left = RightToLeft == RightToLeft.Yes ? 3 : Width - ButtonsWidth;
            var height = Height / 2 - 4;
            const int top = 2;
            
            using (var brush = new SolidBrush(BorderColorDisabled))
            {
                NikseUpDown.DrawArrowUp(e.Graphics, brush, left, top, height);
                NikseUpDown.DrawArrowDown(e.Graphics, brush, left, top + height + 3, height);
            }
        }
        #endregion

        #region IDisposable Implementation
        /// <summary>
        /// Releases all resources used by the NikseTimeUpDown control.
        /// </summary>
        /// <param name="disposing">True if disposing managed resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    _repeatTimer?.Stop();
                    _repeatTimer?.Dispose();
                    
                    _buttonForeColorBrush?.Dispose();
                    _buttonForeColorOverBrush?.Dispose();
                    _buttonForeColorDownBrush?.Dispose();
                    
                    _maskedTextBox?.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error disposing NikseTimeUpDown: {ex.Message}");
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
