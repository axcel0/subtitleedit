using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Controls
{
    /// <summary>
    /// A custom separator control for ToolStrip that provides enhanced visual styling
    /// and improved layout behavior compared to the standard ToolStripSeparator.
    /// </summary>
    public sealed class ToolStripNikseSeparator : ToolStripItem, IDisposable
    {
        #region Constants
        /// <summary>
        /// Default width and height for the separator.
        /// </summary>
        private const int DefaultSeparatorSize = 6;
        
        /// <summary>
        /// Default margin values for the separator.
        /// </summary>
        private const int DefaultMarginLeft = 2;
        private const int DefaultMarginTop = 5;
        private const int DefaultMarginRight = 2;
        private const int DefaultMarginBottom = 6;
        
        /// <summary>
        /// Default pen width for drawing the separator line.
        /// </summary>
        private const float DefaultPenWidth = 1f;
        
        /// <summary>
        /// Minimum alpha value for valid colors.
        /// </summary>
        private const byte MinimumAlphaValue = 1;
        
        /// <summary>
        /// Width adjustment for dropdown menus.
        /// </summary>
        private const int DropdownMenuWidthAdjustment = 4;
        private const int DropdownMenuXOffset = 2;
        
        /// <summary>
        /// Constraining size for certain layout styles.
        /// </summary>
        private const int ConstrainingSize = 23;
        #endregion

        #region Private Fields
        private Color _foreColor;
        private bool _disposed;
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the ToolStripNikseSeparator.
        /// </summary>
        public ToolStripNikseSeparator()
        {
            InitializeSeparator();
        }

        /// <summary>
        /// Initializes the separator with default settings.
        /// </summary>
        private void InitializeSeparator()
        {
            ForeColor = SystemColors.ControlDark;
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets a value indicating whether the separator can be selected.
        /// Only selectable in design mode.
        /// </summary>
        public override bool CanSelect => DesignMode;

        /// <summary>
        /// Gets the default size of the separator.
        /// </summary>
        protected override Size DefaultSize => new Size(DefaultSeparatorSize, DefaultSeparatorSize);

        /// <summary>
        /// Gets the default margin for the separator.
        /// </summary>
        protected override Padding DefaultMargin => 
            new Padding(DefaultMarginLeft, DefaultMarginTop, DefaultMarginRight, DefaultMarginBottom);

        /// <summary>
        /// Gets a value indicating whether the separator is oriented vertically.
        /// </summary>
        private bool IsVertical
        {
            get
            {
                var toolStrip = Owner;
                if (toolStrip is ToolStripDropDownMenu)
                {
                    return false;
                }

                return toolStrip?.LayoutStyle switch
                {
                    ToolStripLayoutStyle.VerticalStackWithOverflow => false,
                    _ => true
                };
            }
        }

        /// <summary>
        /// Gets or sets the foreground color of the separator line.
        /// </summary>
        [Category("ToolStripNikseSeparator")]
        [Description("Gets or sets the foreground color")]
        [RefreshProperties(RefreshProperties.Repaint)]
        public new Color ForeColor
        {
            get => _foreColor;
            set
            {
                if (value.A < MinimumAlphaValue)
                {
                    return;
                }

                if (_foreColor != value)
                {
                    _foreColor = value;
                    Invalidate();
                }
            }
        }
        #endregion

        #region Hidden Properties and Events
        /// <summary>
        /// Gets or sets whether double-click is enabled. Hidden from designer.
        /// </summary>
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new bool DoubleClickEnabled
        {
            get => base.DoubleClickEnabled;
            set => base.DoubleClickEnabled = value;
        }

        /// <summary>
        /// Gets or sets whether the separator is enabled. Hidden from designer.
        /// </summary>
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override bool Enabled
        {
            get => base.Enabled;
            set => base.Enabled = value;
        }

        /// <summary>
        /// Occurs when the Enabled property changes. Hidden from designer.
        /// </summary>
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public new event EventHandler EnabledChanged
        {
            add => base.EnabledChanged += value;
            remove => base.EnabledChanged -= value;
        }

        /// <summary>
        /// Gets or sets the display style. Hidden from designer.
        /// </summary>
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public new ToolStripItemDisplayStyle DisplayStyle
        {
            get => base.DisplayStyle;
            set => base.DisplayStyle = value;
        }

        /// <summary>
        /// Occurs when the DisplayStyle property changes. Hidden from designer.
        /// </summary>
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public new event EventHandler DisplayStyleChanged
        {
            add => base.DisplayStyleChanged += value;
            remove => base.DisplayStyleChanged -= value;
        }

        /// <summary>
        /// Gets or sets the font. Hidden from designer.
        /// </summary>
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public override Font Font
        {
            get => base.Font;
            set => base.Font = value;
        }

        /// <summary>
        /// Gets or sets the text direction. Hidden from designer.
        /// </summary>
        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        [DefaultValue(ToolStripTextDirection.Horizontal)]
        public override ToolStripTextDirection TextDirection
        {
            get => base.TextDirection;
            set => base.TextDirection = value;
        }
        #endregion

        #region Size and Layout
        /// <summary>
        /// Gets the preferred size of the separator within the specified constraints.
        /// </summary>
        /// <param name="constrainingSize">The constraining size.</param>
        /// <returns>The preferred size for the separator.</returns>
        public override Size GetPreferredSize(Size constrainingSize)
        {
            var toolStrip = Owner;
            if (toolStrip == null)
            {
                return new Size(DefaultSeparatorSize, DefaultSeparatorSize);
            }

            // Adjust constraining size for specific layout styles
            if (ShouldUseConstrainingSize(toolStrip.LayoutStyle))
            {
                constrainingSize.Width = ConstrainingSize;
                constrainingSize.Height = ConstrainingSize;
            }

            return IsVertical 
                ? new Size(DefaultSeparatorSize, constrainingSize.Height) 
                : new Size(constrainingSize.Width, DefaultSeparatorSize);
        }

        /// <summary>
        /// Determines if the constraining size should be applied for the given layout style.
        /// </summary>
        /// <param name="layoutStyle">The layout style to check.</param>
        /// <returns>True if constraining size should be used.</returns>
        private static bool ShouldUseConstrainingSize(ToolStripLayoutStyle layoutStyle) =>
            layoutStyle != ToolStripLayoutStyle.HorizontalStackWithOverflow && 
            layoutStyle != ToolStripLayoutStyle.VerticalStackWithOverflow;

        /// <summary>
        /// Sets the bounds of the separator, with special handling for dropdown menus.
        /// </summary>
        /// <param name="rect">The rectangle that represents the bounds.</param>
        protected override void SetBounds(Rectangle rect)
        {
            if (Owner is ToolStripDropDownMenu owner)
            {
                rect.X = DropdownMenuXOffset;
                rect.Width = owner.Width - DropdownMenuWidthAdjustment;
            }
            base.SetBounds(rect);
        }
        #endregion

        #region Painting
        /// <summary>
        /// Paints the separator line.
        /// </summary>
        /// <param name="e">The paint event arguments.</param>
        protected override void OnPaint(PaintEventArgs e)
        {
            if (Owner == null)
            {
                return;
            }

            base.OnPaint(e);

            try
            {
                DrawSeparatorLine(e.Graphics);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error painting ToolStripNikseSeparator: {ex.Message}");
            }
        }

        /// <summary>
        /// Draws the separator line using the specified graphics object.
        /// </summary>
        /// <param name="graphics">The graphics object to draw with.</param>
        private void DrawSeparatorLine(Graphics graphics)
        {
            using var pen = new Pen(_foreColor, DefaultPenWidth);
            
            var startX = Bounds.Width / 2;
            var startY = Padding.Top;
            var endX = Bounds.Width / 2;
            var endY = Height - Padding.Bottom;
            
            graphics.DrawLine(pen, startX, startY, endX, endY);
        }
        #endregion

        #region Accessibility Support
        /// <summary>
        /// Accessibility object for the ToolStripNikseSeparator.
        /// </summary>
        [ComVisible(true)]
        internal sealed class ToolStripSeparatorAccessibleObject : ToolStripItem.ToolStripItemAccessibleObject
        {
            private readonly ToolStripSeparator _ownerItem;

            /// <summary>
            /// Initializes a new instance of the ToolStripSeparatorAccessibleObject.
            /// </summary>
            /// <param name="ownerItem">The owner separator item.</param>
            public ToolStripSeparatorAccessibleObject(ToolStripSeparator ownerItem) : base(ownerItem)
            {
                _ownerItem = ownerItem ?? throw new ArgumentNullException(nameof(ownerItem));
            }

            /// <summary>
            /// Gets the accessible role of the separator.
            /// </summary>
            public override AccessibleRole Role
            {
                get
                {
                    var accessibleRole = _ownerItem.AccessibleRole;
                    return accessibleRole != AccessibleRole.Default ? accessibleRole : AccessibleRole.Separator;
                }
            }
        }
        #endregion

        #region IDisposable Implementation
        /// <summary>
        /// Releases all resources used by the ToolStripNikseSeparator.
        /// </summary>
        /// <param name="disposing">True if disposing managed resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    // No specific resources to dispose for this control
                    // Base class handles standard cleanup
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error disposing ToolStripNikseSeparator: {ex.Message}");
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

