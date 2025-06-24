using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Controls
{
    /// <summary>
    /// A ToolStrip-hosted NikseComboBox control that provides enhanced styling and functionality
    /// for use in toolbars and menu strips.
    /// </summary>
    public sealed class ToolStripNikseComboBox : ToolStripControlHost, IDisposable
    {
        #region Constants
        /// <summary>
        /// Default width for the combo box control.
        /// </summary>
        private const int DefaultWidth = 100;
        
        /// <summary>
        /// Default height for the combo box control.
        /// </summary>
        private const int DefaultHeight = 22;
        
        /// <summary>
        /// Default padding around the control.
        /// </summary>
        private const int DefaultPadding = 2;
        #endregion

        #region Events
        /// <summary>
        /// Occurs when the selected index changes.
        /// </summary>
        public event EventHandler SelectedIndexChanged;

        /// <summary>
        /// Occurs when the text content changes.
        /// </summary>
        public new event EventHandler TextChanged;

        /// <summary>
        /// Occurs when the dropdown is opened.
        /// </summary>
        public event EventHandler DropDown;

        /// <summary>
        /// Occurs when the dropdown is closed.
        /// </summary>
        public event EventHandler DropDownClosed;
        #endregion

        #region Private Fields
        private bool _disposed;
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes a new instance of the ToolStripNikseComboBox with the specified control.
        /// </summary>
        /// <param name="c">The control to host.</param>
        public ToolStripNikseComboBox(Control c) : base(c)
        {
            InitializeControl();
        }

        /// <summary>
        /// Initializes a new instance of the ToolStripNikseComboBox with a default control.
        /// </summary>
        public ToolStripNikseComboBox() : base(CreateControlInstance())
        {
            InitializeControl();
        }

        /// <summary>
        /// Initializes a new instance of the ToolStripNikseComboBox with the specified control and name.
        /// </summary>
        /// <param name="c">The control to host.</param>
        /// <param name="name">The name of the control.</param>
        public ToolStripNikseComboBox(Control c, string name) : base(c, name)
        {
            InitializeControl();
        }
        #endregion

        #region Initialization
        /// <summary>
        /// Initializes the control and sets up event handlers.
        /// </summary>
        private void InitializeControl()
        {
            if (DesignMode)
            {
                return;
            }

            try
            {
                ConfigureControlHost();
                SetupEventHandlers();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing ToolStripNikseComboBox: {ex.Message}");
            }
        }

        /// <summary>
        /// Configures the control host properties.
        /// </summary>
        private void ConfigureControlHost()
        {
            if (Control is ToolStripNikseComboBoxControl comboBoxControl)
            {
                comboBoxControl.Owner = this;
                BackColor = comboBoxControl.BackColor;
            }

            Padding = new Padding(DefaultPadding);
        }

        /// <summary>
        /// Sets up all event handlers for the combo box.
        /// </summary>
        private void SetupEventHandlers()
        {
            var comboBox = ComboBox;
            if (comboBox == null) return;

            // Forward combo box events
            comboBox.SelectedIndexChanged += OnSelectedIndexChanged;
            comboBox.TextChanged += OnTextChanged;
            comboBox.DropDown += OnDropDown;
            comboBox.DropDownClosed += OnDropDownClosed;
            comboBox.LostFocus += OnComboBoxLostFocus;

            // Handle control focus events
            LostFocus += OnControlLostFocus;
        }

        /// <summary>
        /// Creates a new instance of the combo box control.
        /// </summary>
        /// <returns>A new ToolStripNikseComboBoxControl instance.</returns>
        private static Control CreateControlInstance() => new ToolStripNikseComboBoxControl();
        #endregion

        #region Event Handlers
        /// <summary>
        /// Handles the SelectedIndexChanged event of the combo box.
        /// </summary>
        private void OnSelectedIndexChanged(object sender, EventArgs args) => 
            SelectedIndexChanged?.Invoke(sender, args);

        /// <summary>
        /// Handles the TextChanged event of the combo box.
        /// </summary>
        private void OnTextChanged(object sender, EventArgs args) => 
            TextChanged?.Invoke(sender, args);

        /// <summary>
        /// Handles the DropDown event of the combo box.
        /// </summary>
        private void OnDropDown(object sender, EventArgs args) => 
            DropDown?.Invoke(sender, args);

        /// <summary>
        /// Handles the DropDownClosed event of the combo box.
        /// </summary>
        private void OnDropDownClosed(object sender, EventArgs args) => 
            DropDownClosed?.Invoke(sender, args);

        /// <summary>
        /// Handles the LostFocus event of the combo box.
        /// </summary>
        private void OnComboBoxLostFocus(object sender, EventArgs args) => Invalidate();

        /// <summary>
        /// Handles the LostFocus event of the control.
        /// </summary>
        private void OnControlLostFocus(object sender, EventArgs args) => Invalidate();
        #endregion

        #region Size and Layout
        /// <summary>
        /// Gets the default size for the control.
        /// </summary>
        protected override Size DefaultSize => new Size(DefaultWidth, DefaultHeight);

        /// <summary>
        /// Gets the preferred size of the control within the specified constraints.
        /// </summary>
        /// <param name="constrainingSize">The constraining size.</param>
        /// <returns>The preferred size.</returns>
        public override Size GetPreferredSize(Size constrainingSize)
        {
            var preferredSize = base.GetPreferredSize(constrainingSize);
            preferredSize.Width = Width;
            return preferredSize;
        }
        #endregion

        #region Properties
        /// <summary>
        /// Gets the items collection of the combo box.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [Editor("System.Windows.Forms.Design.ListControlStringCollectionEditor, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", typeof(UITypeEditor))]
        public NikseComboBoxCollection Items => ComboBox?.Items;

        /// <summary>
        /// Gets the underlying NikseComboBox control.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public NikseComboBox ComboBox => Control as NikseComboBox;

        /// <summary>
        /// Gets or sets the selected index of the combo box.
        /// </summary>
        public int SelectedIndex
        {
            get => ComboBox?.SelectedIndex ?? -1;
            set
            {
                if (ComboBox != null)
                {
                    ComboBox.SelectedIndex = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the selected text in the combo box.
        /// </summary>
        public string SelectedText
        {
            get => ComboBox?.SelectedText ?? string.Empty;
            set
            {
                if (ComboBox != null)
                {
                    ComboBox.SelectedText = value ?? string.Empty;
                }
            }
        }

        /// <summary>
        /// Gets or sets the drop-down style of the combo box.
        /// </summary>
        public ComboBoxStyle DropDownStyle
        {
            get => ComboBox?.DropDownStyle ?? ComboBoxStyle.DropDown;
            set
            {
                if (ComboBox != null)
                {
                    ComboBox.DropDownStyle = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the height of the drop-down portion of the combo box.
        /// </summary>
        public int DropDownHeight
        {
            get => ComboBox?.DropDownHeight ?? 0;
            set
            {
                if (ComboBox != null)
                {
                    ComboBox.DropDownHeight = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the width of the drop-down portion of the combo box.
        /// </summary>
        public int DropDownWidth
        {
            get => ComboBox?.DropDownWidth ?? 0;
            set
            {
                if (ComboBox != null)
                {
                    ComboBox.DropDownWidth = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the foreground color of the button.
        /// </summary>
        public Color ButtonForeColor
        {
            get => ComboBox?.ButtonForeColor ?? Color.Black;
            set
            {
                if (ComboBox != null)
                {
                    ComboBox.ButtonForeColor = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the foreground color of the button when hovered.
        /// </summary>
        public Color ButtonForeColorOver
        {
            get => ComboBox?.ButtonForeColorOver ?? Color.Black;
            set
            {
                if (ComboBox != null)
                {
                    ComboBox.ButtonForeColorOver = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the foreground color of the button when pressed.
        /// </summary>
        public Color ButtonForeColorDown
        {
            get => ComboBox?.ButtonForeColorDown ?? Color.Black;
            set
            {
                if (ComboBox != null)
                {
                    ComboBox.ButtonForeColorDown = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the border color of the combo box.
        /// </summary>
        public Color BorderColor
        {
            get => ComboBox?.BorderColor ?? Color.Gray;
            set
            {
                if (ComboBox != null)
                {
                    ComboBox.BorderColor = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the background color when the control is disabled.
        /// </summary>
        public Color BackColorDisabled
        {
            get => ComboBox?.BackColorDisabled ?? SystemColors.Control;
            set
            {
                if (ComboBox != null)
                {
                    ComboBox.BackColorDisabled = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the background color of the control.
        /// </summary>
        public new Color BackColor
        {
            get => ComboBox?.BorderColor ?? Color.Gray;
            set
            {
                if (ComboBox != null)
                {
                    ComboBox.BorderColor = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the selected item in the combo box.
        /// </summary>
        public object SelectedItem
        {
            get => ComboBox?.SelectedItem;
            set
            {
                if (ComboBox != null)
                {
                    ComboBox.SelectedItem = value;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether the drop-down is currently open.
        /// </summary>
        public bool DroppedDown => ComboBox?.DroppedDown ?? false;
        #endregion

        #region Public Methods
        /// <summary>
        /// Begins updating the combo box to prevent redraws during bulk operations.
        /// </summary>
        public void BeginUpdate()
        {
            ComboBox?.BeginUpdate();
        }

        /// <summary>
        /// Ends updating the combo box and resumes drawing.
        /// </summary>
        public void EndUpdate()
        {
            ComboBox?.EndUpdate();
        }
        #endregion

        #region Nested Class
        /// <summary>
        /// Internal control class that extends NikseComboBox for use in ToolStrip.
        /// </summary>
        internal sealed class ToolStripNikseComboBoxControl : NikseComboBox
        {
            /// <summary>
            /// Initializes a new instance of the ToolStripNikseComboBoxControl.
            /// </summary>
            public ToolStripNikseComboBoxControl()
            {
                SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
            }

            /// <summary>
            /// Gets or sets the owner ToolStripNikseComboBox.
            /// </summary>
            public ToolStripNikseComboBox Owner { get; set; }
        }
        #endregion

        #region IDisposable Implementation
        /// <summary>
        /// Releases all resources used by the ToolStripNikseComboBox.
        /// </summary>
        /// <param name="disposing">True if disposing managed resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    UnsubscribeFromEvents();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error disposing ToolStripNikseComboBox: {ex.Message}");
                }
                finally
                {
                    _disposed = true;
                }
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// Unsubscribes from all event handlers to prevent memory leaks.
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            try
            {
                var comboBox = ComboBox;
                if (comboBox != null)
                {
                    comboBox.SelectedIndexChanged -= OnSelectedIndexChanged;
                    comboBox.TextChanged -= OnTextChanged;
                    comboBox.DropDown -= OnDropDown;
                    comboBox.DropDownClosed -= OnDropDownClosed;
                    comboBox.LostFocus -= OnComboBoxLostFocus;
                }

                LostFocus -= OnControlLostFocus;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error unsubscribing from events: {ex.Message}");
            }
        }
        #endregion
    }
}
