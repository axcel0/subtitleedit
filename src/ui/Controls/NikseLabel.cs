using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using System;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Controls
{
    /// <summary>
    /// A custom Label control with enhanced theme support and optimized rendering.
    /// </summary>
    public sealed class NikseLabel : Label, IDisposable
    {
        #region Fields

        private bool _disposed;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the NikseLabel class.
        /// </summary>
        public NikseLabel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
        }

        #endregion

        #region Static Helper Methods

        /// <summary>
        /// Creates a StringFormat for drawing text based on control alignment and settings.
        /// </summary>
        /// <param name="control">The control to create the format for.</param>
        /// <param name="contentAlignment">The content alignment.</param>
        /// <param name="showEllipsis">Whether to show ellipsis for long text.</param>
        /// <returns>A configured StringFormat instance.</returns>
        internal static StringFormat CreateStringFormat(Control control, ContentAlignment contentAlignment, bool showEllipsis)
        {
            if (control == null) throw new ArgumentNullException(nameof(control));

            var stringFormat = new StringFormat
            {
                Alignment = GetHorizontalAlignment(contentAlignment),
                LineAlignment = GetVerticalAlignment(contentAlignment),
                HotkeyPrefix = HotkeyPrefix.None
            };

            if (control.RightToLeft == RightToLeft.Yes)
            {
                stringFormat.FormatFlags |= StringFormatFlags.DirectionRightToLeft;
            }

            if (showEllipsis)
            {
                stringFormat.Trimming = StringTrimming.EllipsisCharacter;
                stringFormat.FormatFlags |= StringFormatFlags.LineLimit;
            }

            if (control.AutoSize)
            {
                stringFormat.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces;
            }

            return stringFormat;
        }

        /// <summary>
        /// Creates TextFormatFlags for rendering text based on control alignment and settings.
        /// </summary>
        /// <param name="control">The control to create the format for.</param>
        /// <param name="contentAlignment">The content alignment.</param>
        /// <param name="showEllipsis">Whether to show ellipsis for long text.</param>
        /// <param name="useMnemonic">Whether to use mnemonic processing.</param>
        /// <returns>Configured TextFormatFlags.</returns>
        internal static TextFormatFlags CreateTextFormatFlags(Control control, ContentAlignment contentAlignment, bool showEllipsis, bool useMnemonic)
        {
            if (control == null) throw new ArgumentNullException(nameof(control));

            var textFormatFlags = TextFormatFlags.TextBoxControl | TextFormatFlags.WordBreak;

            if (showEllipsis)
            {
                textFormatFlags |= TextFormatFlags.EndEllipsis;
            }

            // Vertical alignment
            textFormatFlags |= GetVerticalTextFormatFlags(contentAlignment);

            // Horizontal alignment
            textFormatFlags |= GetHorizontalTextFormatFlags(contentAlignment);

            if (control.RightToLeft == RightToLeft.Yes)
            {
                textFormatFlags |= TextFormatFlags.RightToLeft;
            }

            textFormatFlags |= useMnemonic ? TextFormatFlags.HidePrefix : TextFormatFlags.NoPrefix;

            return textFormatFlags;
        }

        private static StringAlignment GetHorizontalAlignment(ContentAlignment contentAlignment)
        {
            return contentAlignment switch
            {
                ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight => StringAlignment.Far,
                ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter => StringAlignment.Center,
                _ => StringAlignment.Near
            };
        }

        private static StringAlignment GetVerticalAlignment(ContentAlignment contentAlignment)
        {
            return contentAlignment switch
            {
                ContentAlignment.MiddleLeft or ContentAlignment.MiddleCenter or ContentAlignment.MiddleRight => StringAlignment.Center,
                ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight => StringAlignment.Far,
                _ => StringAlignment.Near
            };
        }

        private static TextFormatFlags GetVerticalTextFormatFlags(ContentAlignment contentAlignment)
        {
            return contentAlignment switch
            {
                ContentAlignment.MiddleLeft or ContentAlignment.MiddleCenter or ContentAlignment.MiddleRight => TextFormatFlags.VerticalCenter,
                ContentAlignment.BottomLeft or ContentAlignment.BottomCenter or ContentAlignment.BottomRight => TextFormatFlags.Bottom,
                _ => TextFormatFlags.Top
            };
        }

        private static TextFormatFlags GetHorizontalTextFormatFlags(ContentAlignment contentAlignment)
        {
            return contentAlignment switch
            {
                ContentAlignment.TopCenter or ContentAlignment.MiddleCenter or ContentAlignment.BottomCenter => TextFormatFlags.HorizontalCenter,
                ContentAlignment.TopRight or ContentAlignment.MiddleRight or ContentAlignment.BottomRight => TextFormatFlags.Right,
                _ => TextFormatFlags.Left
            };
        }

        #endregion

        #region Rendering

        /// <summary>
        /// Renders the label with theme-aware styling.
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            if (e?.Graphics == null) return;

            try
            {
                if (ShouldUseDefaultPainting())
                {
                    base.OnPaint(e);
                    return;
                }

                RenderDisabledLabel(e);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in NikseLabel.OnPaint: {ex.Message}");
                // Fallback to base implementation
                base.OnPaint(e);
            }
        }

        private bool ShouldUseDefaultPainting()
        {
            return Enabled || !Configuration.Settings.General.UseDarkTheme;
        }

        private void RenderDisabledLabel(PaintEventArgs e)
        {
            var rectangle = new Rectangle(0, 0, Width, Height);

            using var font = CreateDisabledFont();
            
            if (UseCompatibleTextRendering)
            {
                RenderWithGraphics(e, rectangle, font);
            }
            else
            {
                RenderWithTextRenderer(e, rectangle, font);
            }
        }

        private Font CreateDisabledFont()
        {
            return new Font(Font.FontFamily, Math.Max(1, Font.Size - 1), FontStyle.Italic);
        }

        private void RenderWithGraphics(PaintEventArgs e, Rectangle rectangle, Font font)
        {
            using var brush = new SolidBrush(DarkTheme.DarkThemeDisabledColor);
            using var stringFormat = CreateStringFormat(this, TextAlign, AutoEllipsis);
            
            e.Graphics.DrawString(Text, font, brush, rectangle, stringFormat);
        }

        private void RenderWithTextRenderer(PaintEventArgs e, Rectangle rectangle, Font font)
        {
            var textFormatFlags = CreateTextFormatFlags(this, TextAlign, AutoEllipsis, UseMnemonic);
            TextRenderer.DrawText(e.Graphics, Text, font, rectangle, DarkTheme.DarkThemeDisabledColor, textFormatFlags);
        }

        #endregion

        #region Dispose Pattern

        /// <summary>
        /// Releases all resources used by the NikseLabel.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                // No additional resources to dispose for this control
                _disposed = true;
            }

            base.Dispose(disposing);
        }

        #endregion
    }
}
