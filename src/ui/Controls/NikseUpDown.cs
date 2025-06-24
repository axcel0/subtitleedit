using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Controls
{
    [Category("NikseUpDown"), Description("Numeric Up/Down with better support for color theme")]
    public sealed class NikseUpDown : Control, IDisposable
    {
        #region Constants and Configuration
        private const int ButtonsWidth = 13;
        private const int RepeatTimerInitialInterval = 300;
        private const int RepeatTimerFastInterval = 10;
        private const int RepeatTimerSlowInterval = 75;
        private const int RepeatTimerThreshold = 8;
        private const int MaxTextLength = 25;
        private const int DecimalPlacesMax = 4;
        
        public static readonly Color DefaultBackColorDisabled = Color.FromArgb(240, 240, 240);
        #endregion

        #region Events
        /// <summary>
        /// Occurs when the Value property changes.
        /// </summary>
        public event EventHandler ValueChanged;

        /// <summary>
        /// Occurs when a key is pressed while the control has focus.
        /// </summary>
        public new event KeyEventHandler KeyDown;
        #endregion

        #region Private Fields
        private decimal _value;
        private bool _dirty;
        private int _decimalPlaces;
        private bool _thousandsSeparator;
        
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
        private readonly TextBox _textBox;
        
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
        [Category("NikseUpDown"), Description("Gets or sets the default value in textBox"), 
         RefreshProperties(RefreshProperties.Repaint)]
        public decimal Value
        {
            get => _value;
            set
            {
                if (value == _value) return;

                _value = DecimalPlaces == 0 ? value : Math.Round(value, DecimalPlaces);
                SetText();
                _dirty = false;
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        [Category("NikseUpDown"), Description("Gets or sets the decimal places (max 4)")]
        public int DecimalPlaces
        {
            get => _decimalPlaces;
            set
            {
                _decimalPlaces = value <= 0 ? 0 : Math.Min(value, DecimalPlacesMax);
                Invalidate();
            }
        }

        [Category("NikseUpDown"), Description("Gets or sets the thousand separator")]
        public bool ThousandsSeparator
        {
            get => _thousandsSeparator;
            set
            {
                _thousandsSeparator = value;
                Invalidate();
            }
        }

        [Category("NikseUpDown"), Description("Gets or sets the increment value"), DefaultValue(1)]
        public decimal Increment { get; set; } = 1;

        [Category("NikseUpDown"), Description("Gets or sets the Maximum value (max 25 significant digits)"), DefaultValue(100)]
        public decimal Maximum { get; set; } = 100;

        [Category("NikseUpDown"), Description("Gets or sets the Minimum value"), DefaultValue(0)]
        public decimal Minimum { get; set; } = 0;

        [Category("NikseUpDown"), Description("Allow arrow keys to set increment/decrement value")]
        [DefaultValue(true)]
        public bool InterceptArrowKeys { get; set; }
        #endregion

        #region Color Properties
        [Category("NikseUpDown"), Description("Gets or sets the button foreground color"),
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

        [Category("NikseUpDown"), Description("Gets or sets the button foreground mouse over color"), 
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

        [Category("NikseUpDown"), Description("Gets or sets the button foreground mouse down color"), 
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

        [Category("NikseUpDown"), Description("Gets or sets the border color"), 
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

        [Category("NikseUpDown"), Description("Gets or sets the disabled background color"),
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

        [Category("NikseUpDown"), Description("Gets or sets the disabled border color"), 
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
        public new Font Font
        {
            get => base.Font;
            set
            {
                if (_textBox != null)
                {
                    _textBox.Font = value;
                }

                base.Font = value;
                Invalidate();
            }
        }

        public new bool Enabled
        {
            get => base.Enabled;
            set
            {
                base.Enabled = value;
                Invalidate();
            }
        }

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

        public override Color ForeColor
        {
            get => base.ForeColor;
            set
            {
                base.ForeColor = value;
                if (_textBox != null)
                {
                    _textBox.ForeColor = value;
                }
                Application.DoEvents();
                Invalidate();
            }
        }

        public override Color BackColor
        {
            get => base.BackColor;
            set
            {
                base.BackColor = value;
                if (_textBox != null)
                {
                    _textBox.BackColor = value;
                }
                Application.DoEvents();
                Invalidate();
            }
        }
        #endregion

        #region Constructor and Initialization
        public NikseUpDown()
        {
            InitializeComponent();
            InitializeColors();
            InitializeTimer();
            InitializeEventHandlers();
            
            TabStop = false;
        }

        private void InitializeComponent()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable |
                     ControlStyles.AllPaintingInWmPaint, true);

            InterceptArrowKeys = true;
            Height = 23;

            _textBox = new TextBox
            {
                BorderStyle = BorderStyle.None
            };

            Controls.Add(_textBox);
            BackColor = SystemColors.Window;
        }

        private void InitializeColors()
        {
            ButtonForeColor = DefaultForeColor;
            ButtonForeColorOver = Color.FromArgb(0, 120, 215);
            ButtonForeColorDown = Color.Orange;
            BorderColor = Color.FromArgb(171, 173, 179);
            BorderColorDisabled = Color.FromArgb(120, 120, 120);
            BackColorDisabled = DefaultBackColorDisabled;
        }

        private void InitializeTimer()
        {
            _repeatTimer = new Timer();
            _repeatTimer.Tick += OnRepeatTimerTick;
        }

        private void InitializeEventHandlers()
        {
            _textBox.KeyPress += OnTextBoxKeyPress;
            _textBox.KeyDown += OnTextBoxKeyDown;
            _textBox.LostFocus += OnTextBoxLostFocus;
            _textBox.GotFocus += (sender, args) => Invalidate();
            _textBox.TextChanged += OnTextBoxTextChanged;
            _textBox.MouseEnter += (sender, args) => { _upDownMouseEntered = true; };
            _textBox.MouseLeave += (sender, args) => { _upDownMouseEntered = false; };

            LostFocus += (sender, args) => _repeatTimer.Stop();
            MouseWheel += OnMouseWheel;
        }
        #endregion

        #region Event Handlers
        protected override void OnGotFocus(EventArgs e)
        {
            if (_textBox != null)
            {
                _textBox.Focus();
                return;
            }

            base.OnGotFocus(e);
        }

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
            if (_textBox == null) return;

            if (e.Delta > 0)
            {
                AddValue(Increment);
            }
            else if (e.Delta < 0)
            {
                AddValue(-Increment);
            }
        }

        private void OnTextBoxTextChanged(object sender, EventArgs e)
        {
            if (_dirty) return;

            var text = _textBox.Text.Trim();
            if (!decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, 
                                 CultureInfo.DefaultThreadCurrentCulture, out var result))
                return;

            var v = Math.Round(result, DecimalPlaces);
            if (v == Value) return;

            Value = Math.Max(Minimum, Math.Min(Maximum, v));
            if (Value != v)
            {
                Invalidate();
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
            else if (e.Modifiers == Keys.Control)
            {
                HandleControlKeys(e);
            }
            else if (ShouldMarkDirty(e.KeyData))
            {
                _dirty = true;
                KeyDown?.Invoke(sender, e);
                Invalidate();
            }
            else
            {
                KeyDown?.Invoke(sender, e);
            }
        }

        private void HandleControlKeys(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.A:
                    _textBox.SelectAll();
                    e.SuppressKeyPress = true;
                    break;
                case Keys.C:
                    _textBox.Copy();
                    e.SuppressKeyPress = true;
                    break;
                case Keys.V:
                    _textBox.Paste();
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
            _dirty = false;
            AddValue(0);
            SetText(true);
            Invalidate();
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
            }
            else if ((e.KeyChar == '.' || e.KeyChar == ',') && DecimalPlaces > 0)
            {
                e.Handled = false;
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
                _dirty = false;

                if (string.IsNullOrEmpty(_textBox.Text))
                {
                    Value = IsValueInRange(0) ? 0 : Minimum;
                    SetText(true);
                    return;
                }

                if (_textBox.TextLength > MaxTextLength)
                {
                    Value = Maximum;
                    SetText(true);
                    return;
                }

                var text = _textBox.Text.Trim();
                if (string.IsNullOrEmpty(text))
                {
                    text = "0";
                }

                if (decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, 
                                   CultureInfo.DefaultThreadCurrentCulture, out var result))
                {
                    var newValue = Math.Round(result + value, DecimalPlaces);
                    Value = Math.Max(Minimum, Math.Min(Maximum, newValue));
                    SetText();
                    return;
                }

                SetText();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in AddValue: {ex.Message}");
            }
        }

        private bool IsValueInRange(decimal testValue) => testValue >= Minimum && testValue <= Maximum;
        #endregion

        #region Mouse and Input Handling
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
            ConfigureRendering(e.Graphics);
            UpdateTextBoxLayout();

            if (!_dirty)
            {
                SetText();
            }

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

        private void UpdateTextBoxLayout()
        {
            _textBox.BackColor = BackColor;
            _textBox.ForeColor = ButtonForeColor;
            _textBox.Top = 2;
            _textBox.Left = RightToLeft == RightToLeft.Yes ? ButtonsWidth : 3;
            _textBox.Height = Height - 4;
            _textBox.Width = Width - ButtonsWidth - 3;
            _textBox.Invalidate();
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
            var borderColor = _textBox.Focused || _upDownMouseEntered ? _buttonForeColorOver : BorderColor;
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
            
            DrawArrowUp(graphics, brush, left, top, height);
        }

        private void DrawDownArrow(Graphics graphics)
        {
            var brush = GetArrowBrush(_buttonDownActive);
            var left = RightToLeft == RightToLeft.Yes ? 3 : Width - ButtonsWidth;
            var height = Height / 2 - 4;
            var top = height + 5;
            
            DrawArrowDown(graphics, brush, left, top, height);
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
                DrawArrowUp(e.Graphics, brush, left, top, height);
                DrawArrowDown(e.Graphics, brush, left, top + height + 3, height);
            }
        }
        #endregion

        #region Text Formatting and Helper Methods
        private void SetText(bool leaving = false)
        {
            try
            {
                var selectionStart = _textBox.SelectionStart;

                var newText = FormatValueAsText();

                if (newText == _textBox.Text) return;

                if (!leaving && ShouldPreservePartialInput(newText))
                {
                    return;
                }

                _textBox.Text = newText;
                _textBox.SelectionStart = selectionStart;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SetText: {ex.Message}");
            }
        }

        private string FormatValueAsText()
        {
            return DecimalPlaces switch
            {
                0 => ThousandsSeparator ? $"{Value:#,###,##0}" : $"{Value:########0}",
                1 => ThousandsSeparator ? $"{Value:#,###,##0.0}" : $"{Value:########0.0}",
                2 => ThousandsSeparator ? $"{Value:#,###,##0.00}" : $"{Value:#########0.00}",
                3 => ThousandsSeparator ? $"{Value:#,###,##0.000}" : $"{Value:#########0.000}",
                _ => ThousandsSeparator ? $"{Value:#,###,##0.0000}" : $"{Value:#########0.0000}"
            };
        }

        private bool ShouldPreservePartialInput(string newText)
        {
            return (_textBox.Text.StartsWith(",") || _textBox.Text.StartsWith(".")) &&
                   (newText.StartsWith("0,") || newText.StartsWith("0."));
        }

        private void SetBrush(ref Brush brush, Color color)
        {
            brush?.Dispose();
            brush = new SolidBrush(color);
        }
        #endregion

        #region Static Drawing Methods
        public static void DrawArrowUp(Graphics g, Brush brush, int left, int top, int height)
        {
            var points = new[]
            {
                new Point(left + 5, top + 2), // top
                new Point(left + 1, top + height), // left bottom
                new Point(left + 9, top + height), // right bottom
            };
            
            g.FillPolygon(brush, points);
        }

        public static void DrawArrowDown(Graphics g, Brush brush, int left, int top, int height)
        {
            var points = new[]
            {
                new Point(left + 1, top), // left top
                new Point(left + 9, top), // right top
                new Point(left + 5, top + height - 2), // bottom
            };
            
            g.FillPolygon(brush, points);
        }
        #endregion
        #region IDisposable Implementation
        /// <summary>
        /// Releases all resources used by the NikseUpDown control.
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
                    
                    _textBox?.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error disposing NikseUpDown: {ex.Message}");
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
