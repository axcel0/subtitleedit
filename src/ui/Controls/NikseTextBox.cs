using Nikse.SubtitleEdit.Logic;
using System;
using System.Drawing;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls.Interfaces;

namespace Nikse.SubtitleEdit.Controls
{
    /// <summary>
    /// A custom TextBox control with enhanced focus styling and keyboard support.
    /// </summary>
    public sealed class NikseTextBox : TextBox, ISelectedText, IDisposable
    {
        #region Constants

        private const int WM_NCPAINT = 0x85;
        private const int WM_PAINT = 0x0f;
        private static readonly Color DefaultFocusedColor = Color.FromArgb(0, 120, 215);

        #endregion

        #region Fields

        private Color _focusedColor = DefaultFocusedColor;
        private bool _disposed;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the color used to highlight the border when the control has focus.
        /// </summary>
        public Color FocusedColor
        {
            get => _focusedColor;
            set
            {
                if (_focusedColor != value)
                {
                    _focusedColor = value;
                    Invalidate();
                }
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the NikseTextBox class.
        /// </summary>
        public NikseTextBox()
        {
            InitializeComponent();
        }

        #endregion

        #region Initialization

        private void InitializeComponent()
        {
            KeyDown += OnNikseTextBoxKeyDown;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles the KeyDown event to support enhanced keyboard shortcuts.
        /// </summary>
        private void OnNikseTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e == null) return;

            try
            {
                if (e.Control && e.KeyCode == Keys.Back)
                {
                    e.SuppressKeyPress = true;
                    UiUtil.ApplyControlBackspace(this);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in NikseTextBox KeyDown: {ex.Message}");
            }
        }

        #endregion

        #region Window Message Processing

        /// <summary>
        /// Processes Windows messages and adds custom focus border rendering.
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (ShouldDrawFocusBorder(m))
            {
                DrawFocusBorder();
            }
        }

        private bool ShouldDrawFocusBorder(Message m)
        {
            return Focused && (m.Msg == WM_PAINT || m.Msg == WM_NCPAINT);
        }

        private void DrawFocusBorder()
        {
            try
            {
                using var graphics = CreateGraphics();
                using var pen = new Pen(FocusedColor);
                
                var borderRect = CalculateBorderRectangle();
                graphics.DrawRectangle(pen, borderRect);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error drawing focus border: {ex.Message}");
            }
        }

        private Rectangle CalculateBorderRectangle()
        {
            var widthSubtract = 1;
            
            if (HasVerticalScrollBar())
            {
                widthSubtract += SystemInformation.VerticalScrollBarWidth + SystemInformation.BorderSize.Width;
            }

            return new Rectangle(0, 0, Width - widthSubtract, Height - 1);
        }

        private bool HasVerticalScrollBar()
        {
            return ScrollBars == ScrollBars.Vertical || ScrollBars == ScrollBars.Both;
        }

        #endregion

        #region Dispose Pattern

        /// <summary>
        /// Releases all resources used by the NikseTextBox.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                KeyDown -= OnNikseTextBoxKeyDown;
                _disposed = true;
            }

            base.Dispose(disposing);
        }

        #endregion
    }
}
