using Nikse.SubtitleEdit.Logic;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Controls
{
    /// <summary>
    /// A custom ListBox wrapper with enhanced theme support and improved styling.
    /// </summary>
    public sealed class NikseListBox : Panel, IDisposable
    {
        #region Fields

        private readonly ListBox _listBox;
        private readonly bool _loadingDone;
        private bool _disposed;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the NikseListBox class.
        /// </summary>
        public NikseListBox()
        {
            InitializeComponent();
            _loadingDone = true;
        }

        #endregion

        #region Initialization

        private void InitializeComponent()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            
            _listBox = CreateListBox();
            ConfigureContainer();
        }

        private ListBox CreateListBox()
        {
            return new ListBox
            {
                BorderStyle = BorderStyle.None,
                Padding = new Padding(0),
                Dock = DockStyle.Fill
            };
        }

        private void ConfigureContainer()
        {
            BorderStyle = BorderStyle.FixedSingle;
            TabStop = false;
            
            Controls.Clear();
            Controls.Add(_listBox);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the background color of the ListBox.
        /// </summary>
        public new Color BackColor
        {
            get => GetSafeBackColor();
            set => SetSafeBackColor(value);
        }

        /// <summary>
        /// Gets or sets the foreground color of the ListBox.
        /// </summary>
        public new Color ForeColor
        {
            get => GetSafeForeColor();
            set => SetSafeForeColor(value);
        }

        /// <summary>
        /// Gets or sets the font of the ListBox.
        /// </summary>
        public new Font Font
        {
            get => GetSafeFont();
            set => SetSafeFont(value);
        }

        /// <summary>
        /// Gets the collection of items in the ListBox.
        /// </summary>
        public ListBox.ObjectCollection Items => _listBox?.Items ?? new ListBox.ObjectCollection(new ListBox());

        /// <summary>
        /// Gets or sets the right-to-left layout.
        /// </summary>
        public override RightToLeft RightToLeft
        {
            get => GetSafeRightToLeft();
            set => SetSafeRightToLeft(value);
        }

        /// <summary>
        /// Gets or sets the selected index.
        /// </summary>
        public int SelectedIndex
        {
            get => _listBox?.SelectedIndex ?? -1;
            set
            {
                if (_listBox != null)
                {
                    _listBox.SelectedIndex = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the top index.
        /// </summary>
        public int TopIndex
        {
            get => _listBox?.TopIndex ?? 0;
            set
            {
                if (_listBox != null)
                {
                    _listBox.TopIndex = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the selected item.
        /// </summary>
        public object SelectedItem
        {
            get => _listBox?.SelectedItem;
            set
            {
                if (_listBox != null)
                {
                    _listBox.SelectedItem = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the items are sorted.
        /// </summary>
        public bool Sorted
        {
            get => _listBox?.Sorted ?? false;
            set
            {
                if (_listBox != null)
                {
                    _listBox.Sorted = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the text of the ListBox.
        /// </summary>
        public override string Text
        {
            get => _listBox?.Text ?? string.Empty;
            set
            {
                if (_listBox != null)
                {
                    _listBox.Text = value;
                }
            }
        }

        /// <summary>
        /// Gets the selected indices collection.
        /// </summary>
        public ListBox.SelectedIndexCollection SelectedIndices => _listBox?.SelectedIndices;

        /// <summary>
        /// Gets or sets a value indicating whether formatting is enabled.
        /// </summary>
        public bool FormattingEnabled
        {
            get => _listBox?.FormattingEnabled ?? false;
            set
            {
                if (_listBox != null)
                {
                    _listBox.FormattingEnabled = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the selection mode.
        /// </summary>
        public SelectionMode SelectionMode
        {
            get => _listBox?.SelectionMode ?? SelectionMode.None;
            set
            {
                if (_listBox != null)
                {
                    _listBox.SelectionMode = value;
                }
            }
        }

        /// <summary>
        /// Gets the selected items collection.
        /// </summary>
        public ListBox.SelectedObjectCollection SelectedItems => _listBox?.SelectedItems;

        /// <summary>
        /// Gets or sets the item height.
        /// </summary>
        public int ItemHeight
        {
            get => _listBox?.ItemHeight ?? 0;
            set
            {
                if (_listBox != null)
                {
                    _listBox.ItemHeight = value;
                }
            }
        }

        #endregion

        #region Property Helpers

        private Color GetSafeBackColor()
        {
            if (!_loadingDone) return DefaultBackColor;
            return _listBox?.BackColor ?? DefaultBackColor;
        }

        private void SetSafeBackColor(Color value)
        {
            if (!_loadingDone) return;
            if (_listBox != null)
            {
                _listBox.BackColor = value;
            }
        }

        private Color GetSafeForeColor()
        {
            if (!_loadingDone) return DefaultForeColor;
            return _listBox?.ForeColor ?? DefaultForeColor;
        }

        private void SetSafeForeColor(Color value)
        {
            if (!_loadingDone) return;
            if (_listBox != null)
            {
                _listBox.ForeColor = value;
            }
        }

        private Font GetSafeFont()
        {
            return !_loadingDone ? DefaultFont : base.Font;
        }

        private void SetSafeFont(Font value)
        {
            if (!_loadingDone) return;
            
            if (_listBox != null)
            {
                _listBox.Font = value;
            }
            base.Font = value;
        }

        private RightToLeft GetSafeRightToLeft()
        {
            return !_loadingDone ? RightToLeft.Inherit : base.RightToLeft;
        }

        private void SetSafeRightToLeft(RightToLeft value)
        {
            if (_listBox != null)
            {
                _listBox.RightToLeft = value;
            }
            base.RightToLeft = value;
        }

        #endregion

        #region Events

        // ReSharper disable once InconsistentNaming
        public new event EventHandler TextChanged
        {
            add => _listBox.TextChanged += value;
            remove => _listBox.TextChanged -= value;
        }

        // ReSharper disable once InconsistentNaming
        public new event EventHandler Click
        {
            add => _listBox.Click += value;
            remove => _listBox.Click -= value;
        }

        // ReSharper disable once InconsistentNaming
        public event EventHandler SelectedIndexChanged
        {
            add => _listBox.SelectedIndexChanged += value;
            remove => _listBox.SelectedIndexChanged -= value;
        }

        // ReSharper disable once InconsistentNaming
        public new event MouseEventHandler MouseClick
        {
            add => _listBox.MouseClick += value;
            remove => _listBox.MouseClick -= value;
        }

        // ReSharper disable once InconsistentNaming
        public new event MouseEventHandler MouseDoubleClick
        {
            add => _listBox.MouseDoubleClick += value;
            remove => _listBox.MouseDoubleClick -= value;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Maintains performance while updating the ListBox.
        /// </summary>
        public void BeginUpdate() => _listBox?.BeginUpdate();

        /// <summary>
        /// Resumes painting the ListBox after updating.
        /// </summary>
        public void EndUpdate() => _listBox?.EndUpdate();

        /// <summary>
        /// Sets the selection state of the specified item.
        /// </summary>
        public void SetSelected(int index, bool value) => _listBox?.SetSelected(index, value);

        /// <summary>
        /// Selects all items in the ListBox.
        /// </summary>
        public void SelectAll() => _listBox?.SelectAll();

        /// <summary>
        /// Inverts the current selection.
        /// </summary>
        public void InverseSelection() => _listBox?.InverseSelection();

        #endregion

        #region Theme Support

        /// <summary>
        /// Applies dark theme styling to the ListBox.
        /// </summary>
        public void SetDarkTheme()
        {
            if (_listBox == null) return;

            try
            {
                ConfigureDarkThemeColors();
                ConfigureDarkThemeEvents();
                ConfigureDarkThemeDrawing();
                ApplyDarkThemeToWindows();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting dark theme: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes dark theme styling and restores default appearance.
        /// </summary>
        public void UndoDarkTheme()
        {
            if (_listBox == null) return;

            try
            {
                RestoreDefaultColors();
                RemoveDarkThemeEvents();
                RestoreDefaultDrawing();
                ApplyNormalThemeToWindows();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error undoing dark theme: {ex.Message}");
            }
        }

        private void ConfigureDarkThemeColors()
        {
            _listBox.BackColor = DarkTheme.BackColor;
            _listBox.ForeColor = DarkTheme.ForeColor;
        }

        private void ConfigureDarkThemeEvents()
        {
            _listBox.HandleCreated += OnNikseListBoxHandleCreated;
        }

        private void ConfigureDarkThemeDrawing()
        {
            _listBox.DrawMode = DrawMode.OwnerDrawFixed;
            _listBox.DrawItem += OnListBoxDrawItem;
        }

        private void ApplyDarkThemeToWindows()
        {
            DarkTheme.SetWindowThemeDark(_listBox);
            DarkTheme.SetWindowThemeDark(this);
        }

        private void RestoreDefaultColors()
        {
            _listBox.BackColor = DefaultBackColor;
            _listBox.ForeColor = DefaultForeColor;
        }

        private void RemoveDarkThemeEvents()
        {
            _listBox.HandleCreated -= OnNikseListBoxHandleCreated;
        }

        private void RestoreDefaultDrawing()
        {
            _listBox.DrawMode = DrawMode.Normal;
            _listBox.DrawItem -= OnListBoxDrawItem;
        }

        private void ApplyNormalThemeToWindows()
        {
            DarkTheme.SetWindowThemeNormal(_listBox);
            DarkTheme.SetWindowThemeNormal(this);
        }

        private void OnListBoxDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            try
            {
                var modifiedArgs = ModifyDrawItemArgs(e);
                modifiedArgs.DrawBackground();

                using var brush = new SolidBrush(DarkTheme.ForeColor);
                var itemText = _listBox.Items[e.Index]?.ToString() ?? string.Empty;
                e.Graphics.DrawString(itemText, e.Font, brush, e.Bounds, StringFormat.GenericDefault);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error drawing list item: {ex.Message}");
            }
        }

        private static DrawItemEventArgs ModifyDrawItemArgs(DrawItemEventArgs e)
        {
            if ((e.State & DrawItemState.Selected) == DrawItemState.Selected)
            {
                return new DrawItemEventArgs(
                    e.Graphics,
                    e.Font,
                    e.Bounds,
                    e.Index,
                    e.State ^ DrawItemState.Selected,
                    e.ForeColor,
                    DarkTheme.DarkThemeSelectedBackgroundColor);
            }
            return e;
        }

        private static void OnNikseListBoxHandleCreated(object sender, EventArgs e)
        {
            if (sender is Control control)
            {
                DarkTheme.SetWindowThemeDark(control);
            }
        }

        #endregion

        #region Dispose Pattern

        /// <summary>
        /// Releases all resources used by the NikseListBox.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    UndoDarkTheme(); // Clean up theme-related resources
                    _listBox?.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error disposing NikseListBox: {ex.Message}");
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
