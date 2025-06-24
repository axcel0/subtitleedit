using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Design;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls.Interfaces;

namespace Nikse.SubtitleEdit.Controls
{
    /// <summary>
    /// A custom ComboBox control with enhanced theme support and improved functionality.
    /// </summary>
    [Category("NikseComboBox"), Description("ComboBox with better support for color theme")]
    public class NikseComboBox : Control, ISelectedText, IDisposable
    {
        #region Constants

        private const int ButtonsWidth = 13;
        private const int TimerInterval = 200;
        private const int ListViewTimerInterval = 50;
        private const int FocusDelayMs = 25;
        private const int MaxPageNavigationStep = 10;

        #endregion

        #region Events

        // ReSharper disable once InconsistentNaming
        public event EventHandler SelectedIndexChanged;

        // ReSharper disable once InconsistentNaming
        public event EventHandler SelectedValueChanged;

        // ReSharper disable once InconsistentNaming
        public event EventHandler DropDown;

        // ReSharper disable once InconsistentNaming
        public event EventHandler DropDownClosed;

        // ReSharper disable once InconsistentNaming
        public new event KeyEventHandler KeyDown;

        // ReSharper disable once InconsistentNaming
        public new event EventHandler TextChanged;

        #endregion

        #region Fields

        private readonly InnerTextBox _textBox;
        private readonly NikseComboBoxCollection _items;
        private readonly Timer _mouseLeaveTimer;
        private readonly Timer _listViewMouseLeaveTimer;

        private NikseComboBoxPopUp _popUp;
        private ListView _listView;
        private ComboBoxStyle _dropDownStyle;
        private bool _sorted;
        private int _selectedIndex = -1;
        private int? _dropDownWidth;
        private bool _buttonDownActive;
        private bool _buttonLeftIsDown;
        private int _mouseX;
        private bool _hasItemsMouseOver;
        private bool _comboBoxMouseEntered;
        private bool _listViewShown;
        private bool _skipPaint;
        private readonly bool _loading;
        private bool _disposed;

        // Color fields with lazy-initialized brushes
        private Color _buttonForeColor;
        private Brush _buttonForeColorBrush;
        private Color _buttonForeColorOver;
        private Brush _buttonForeColorOverBrush;
        private Color _buttonForeColorDown;
        private Brush _buttonForeColorDownBrush;
        private Color _borderColor;
        private Color _backColorDisabled;
        private Color _borderColorDisabled;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the dropdown style of the combo box.
        /// </summary>
        [Category("NikseComboBox"), Description("Gets or sets DropDownStyle"), RefreshProperties(RefreshProperties.Repaint)]
        public ComboBoxStyle DropDownStyle
        {
            get => _dropDownStyle;
            set
            {
                if (_dropDownStyle != value)
                {
                    _dropDownStyle = value;

                    if (_textBox != null)
                    {
                        _textBox.ReadOnly = value == ComboBoxStyle.DropDownList;
                        TabStop = value == ComboBoxStyle.DropDownList;
                    }
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether items are automatically sorted.
        /// </summary>
        [Category("NikseComboBox"), Description("Gets or sets if elements are auto sorted"), DefaultValue(false)]
        public bool Sorted
        {
            get => _sorted;
            set
            {
                if (_sorted != value)
                {
                    _sorted = value;
                    if (_sorted && _items != null)
                    {
                        _items.SortBy(p => p.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Gets the collection of items in the combo box.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        [Editor("System.Windows.Forms.Design.ListControlStringCollectionEditor, System.Design, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", typeof(UITypeEditor))]
        [MergableProperty(false)]
        public NikseComboBoxCollection Items => _items;

        /// <summary>
        /// Gets or sets the index of the selected item.
        /// </summary>
        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (value == _selectedIndex || _textBox == null || _items == null)
                {
                    return;
                }

                SetSelectedIndex(value);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the control has focus.
        /// </summary>
        public override bool Focused => _comboBoxMouseEntered || _listViewShown || (_textBox?.Focused == true) || base.Focused;

        /// <summary>
        /// Gets or sets the selected item.
        /// </summary>
        public object SelectedItem
        {
            get => _selectedIndex < 0 ? null : _items[_selectedIndex];
            set
            {
                var idx = _items.IndexOf(value);
                if (idx >= 0)
                {
                    SelectedIndex = idx;
                }
            }
        }

        /// <summary>
        /// Gets or sets the text associated with this control.
        /// </summary>
        public override string Text
        {
            get => GetValue(_textBox.Text);
            set
            {
                if (HasValueChanged(_textBox.Text, value))
                {
                    _textBox.Text = value;
                    NotifyTextChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the selected text.
        /// </summary>
        public string SelectedText
        {
            get => GetValue(_textBox.SelectedText);
            set
            {
                if (HasValueChanged(_textBox.SelectedText, value))
                {
                    _textBox.SelectedText = value;
                    NotifyTextChanged();
                }
            }
        }

        /// <summary>
        /// Gets the dropdown control.
        /// </summary>
        public Control DropDownControl => _listView;

        /// <summary>
        /// Gets or sets the button foreground color.
        /// </summary>
        [Category("NikseComboBox"), Description("Gets or sets the button foreground color"), RefreshProperties(RefreshProperties.Repaint)]
        public Color ButtonForeColor
        {
            get => _buttonForeColor;
            set
            {
                if (value.A == 0) return;

                _buttonForeColor = value;
                UpdateBrush(ref _buttonForeColorBrush, value);
                
                if (_textBox != null)
                {
                    _textBox.ForeColor = value;
                }

                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the button foreground mouse over color.
        /// </summary>
        [Category("NikseComboBox"), Description("Gets or sets the button foreground mouse over color"), RefreshProperties(RefreshProperties.Repaint)]
        public Color ButtonForeColorOver
        {
            get => _buttonForeColorOver;
            set
            {
                if (value.A == 0) return;

                _buttonForeColorOver = value;
                UpdateBrush(ref _buttonForeColorOverBrush, value);
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the button foreground mouse down color.
        /// </summary>
        [Category("NikseComboBox"), Description("Gets or sets the button foreground mouse down color"), RefreshProperties(RefreshProperties.Repaint)]
        public Color ButtonForeColorDown
        {
            get => _buttonForeColorDown;
            set
            {
                if (value.A == 0) return;

                _buttonForeColorDown = value;
                UpdateBrush(ref _buttonForeColorDownBrush, value);
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the border color.
        /// </summary>
        [Category("NikseComboBox"), Description("Gets or sets the border color"), RefreshProperties(RefreshProperties.Repaint)]
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

        /// <summary>
        /// Gets or sets the disabled background color.
        /// </summary>
        [Category("NikseComboBox"), Description("Gets or sets the disabled background color"), RefreshProperties(RefreshProperties.Repaint)]
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

        /// <summary>
        /// Gets or sets the background color.
        /// </summary>
        [Category("NikseComboBox"), Description("Gets or sets the background color"), RefreshProperties(RefreshProperties.Repaint)]
        public new Color BackColor
        {
            get => base.BackColor;
            set
            {
                if (value.A == 0) return;

                base.BackColor = value;
                if (_textBox != null)
                {
                    _textBox.BackColor = value;
                }

                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets the disabled border color.
        /// </summary>
        [Category("NikseComboBox"), Description("Gets or sets the disabled border color"), RefreshProperties(RefreshProperties.Repaint)]
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

        /// <summary>
        /// Gets or sets the dropdown width.
        /// </summary>
        public int DropDownWidth
        {
            get => _dropDownWidth ?? Width;
            set => _dropDownWidth = value;
        }

        /// <summary>
        /// Gets or sets the maximum dropdown height.
        /// </summary>
        public int DropDownHeight { get; set; } = 400;

        /// <summary>
        /// Gets a value indicating whether the dropdown is currently shown.
        /// </summary>
        public bool DroppedDown => _listViewShown;

        /// <summary>
        /// Gets or sets a value indicating whether formatting is enabled.
        /// </summary>
        public bool FormattingEnabled { get; set; }

        /// <summary>
        /// Gets or sets the maximum length of text that can be entered.
        /// </summary>
        public int MaxLength
        {
            get => _textBox?.MaxLength ?? 0;
            set
            {
                if (_textBox != null)
                {
                    _textBox.MaxLength = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to use a popup window for the dropdown.
        /// </summary>
        public bool UsePopupWindow { get; set; }

        /// <summary>
        /// Gets or sets the text alignment.
        /// </summary>
        public override RightToLeft RightToLeft
        {
            get => base.RightToLeft;
            set
            {
                if (_textBox != null)
                {
                    _textBox.RightToLeft = value;
                }

                base.RightToLeft = value;
                Invalidate();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the control is enabled.
        /// </summary>
        public new bool Enabled
        {
            get => base.Enabled;
            set
            {
                base.Enabled = value;
                Invalidate();
            }
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the NikseComboBox class.
        /// </summary>
        public NikseComboBox()
        {
            _loading = true;
            _textBox = new InnerTextBox(this);
            _textBox.Visible = false;
            _items = new NikseComboBoxCollection(this);

            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);

            InitializeEventHandlers();
            InitializeTimers();
            InitializeDefaultColors();

            Controls.Add(_textBox);
            DropDownStyle = ComboBoxStyle.DropDown;

            _loading = false;
        }

        #endregion

        #region Initialization Methods

        private void InitializeEventHandlers()
        {
            base.KeyDown += OnBaseKeyDown;
            MouseWheel += OnMouseWheel;
            _textBox.KeyDown += OnTextBoxKeyDown;
            _textBox.LostFocus += (sender, args) => Invalidate();
            _textBox.GotFocus += (sender, args) => Invalidate();
            _textBox.TextChanged += TextBoxTextChanged;
            LostFocus += (sender, args) => Invalidate();
        }

        private void InitializeTimers()
        {
            _mouseLeaveTimer = new Timer { Interval = TimerInterval };
            _mouseLeaveTimer.Tick += OnMouseLeaveTimerTick;

            _listViewMouseLeaveTimer = new Timer { Interval = ListViewTimerInterval };
            _listViewMouseLeaveTimer.Tick += OnListViewMouseLeaveTimerTick;
        }

        private void InitializeDefaultColors()
        {
            BackColor = SystemColors.Window;
            ButtonForeColor = DefaultForeColor;
            ButtonForeColorOver = Color.FromArgb(0, 120, 215);
            ButtonForeColorDown = Color.Orange;
            BorderColor = Color.FromArgb(171, 173, 179);
            BorderColorDisabled = Color.FromArgb(120, 120, 120);
            BackColorDisabled = Color.FromArgb(240, 240, 240);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Sets the SelectedIndex without raising events.
        /// </summary>
        internal void SelectedIndexReset()
        {
            _selectedIndex = -1;
            _textBox.Text = string.Empty;
            Invalidate();
        }

        #endregion

        #region Private Helper Methods

        private void SetSelectedIndex(int value)
        {
            if (value == -1)
            {
                _selectedIndex = value;
                _textBox.Text = string.Empty;
                NotifyTextChanged();

                if (!_skipPaint)
                {
                    Invalidate();
                }
                return;
            }

            _selectedIndex = value;
            _textBox.Text = Items[_selectedIndex].ToString();

            if (!_loading)
            {
                UpdateListViewSelection();
                NotifyTextChanged();
            }

            if (!_skipPaint)
            {
                Invalidate();
            }
        }

        private void UpdateListViewSelection()
        {
            if (_listViewShown && _listView != null)
            {
                if (_listView.SelectedItems.Count > 0)
                {
                    _listView.SelectedItems[0].Selected = false;
                }
                _listView.Items[_selectedIndex].Selected = true;
                _listView.Items[_selectedIndex].EnsureVisible();
                _listView.Items[_selectedIndex].Focused = true;
            }
        }

        private void UpdateBrush(ref Brush brush, Color color)
        {
            brush?.Dispose();
            brush = new SolidBrush(color);
        }

        private string GetValue(string textOrSelectedText)
        {
            return DropDownStyle == ComboBoxStyle.DropDown 
                ? textOrSelectedText 
                : (_selectedIndex < 0 ? string.Empty : _items[_selectedIndex].ToString());
        }

        private bool HasValueChanged(string preValue, string value)
        {
            if (DropDownStyle == ComboBoxStyle.DropDown)
            {
                return !preValue.Equals(value, StringComparison.Ordinal);
            }

            var count = _items.Count;
            for (var i = 0; i < count; i++)
            {
                if (i == _selectedIndex) continue;

                if (_items[i].ToString().Equals(value, StringComparison.Ordinal))
                {
                    _selectedIndex = i;
                    return true;
                }
            }

            return false;
        }

        private void NotifyTextChanged()
        {
            if (_loading) return;

            SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
            SelectedValueChanged?.Invoke(this, EventArgs.Empty);
            TextChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Event Handlers

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            if (_textBox != null && DropDownStyle == ComboBoxStyle.DropDown)
            {
                try
                {
                    Application.DoEvents();
                    TaskDelayHelper.RunDelayed(TimeSpan.FromMilliseconds(FocusDelayMs), () => _textBox.Focus());
                }
                catch
                {
                    // ignore
                }
            }
            Invalidate();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Down || keyData == Keys.Up || base.IsInputKey(keyData);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _buttonDownActive = false;
            _comboBoxMouseEntered = true;
            base.OnMouseEnter(e);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _comboBoxMouseEntered = false;
            _buttonDownActive = false;
            if (_listView != null)
            {
                _mouseLeaveTimer.Start();
                _listViewMouseLeaveTimer.Start();
                _hasItemsMouseOver = false;
            }

            base.OnMouseLeave(e);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();

            if (e.Button == MouseButtons.Left)
            {
                if (!_buttonLeftIsDown)
                {
                    _buttonLeftIsDown = true;
                    Invalidate();
                }

                if (_listViewShown)
                {
                    HideDropDown();
                    return;
                }

                if (_buttonDownActive || _dropDownStyle != ComboBoxStyle.DropDown)
                {
                    ShowListView();
                }
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
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

            _mouseX = e.X;

            if (_mouseX >= left && _mouseX <= right)
            {
                if (!_buttonDownActive)
                {
                    _buttonDownActive = true;
                    Invalidate();
                }
            }
            else if (_buttonDownActive)
            {
                _buttonDownActive = false;
                Invalidate();
            }

            base.OnMouseMove(e);
        }

        private void OnBaseKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Up:
                    NavigateUp();
                    e.SuppressKeyPress = true;
                    break;
                case Keys.Down:
                    NavigateDown();
                    e.SuppressKeyPress = true;
                    break;
                case Keys.PageUp:
                    NavigatePageUp();
                    e.SuppressKeyPress = true;
                    break;
                case Keys.PageDown:
                    NavigatePageDown();
                    e.SuppressKeyPress = true;
                    break;
                default:
                    HandleAlphaNumericKey(e);
                    break;
            }
        }

        private void OnMouseWheel(object sender, MouseEventArgs e)
        {
            if (_textBox == null || _listViewShown) return;

            if (e.Delta > 0)
            {
                NavigateUp();
            }
            else if (e.Delta < 0)
            {
                NavigateDown();
            }
        }

        private void OnTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Up:
                    NavigateUp();
                    e.Handled = true;
                    break;
                case Keys.Down:
                    NavigateDown();
                    e.Handled = true;
                    break;
                default:
                    KeyDown?.Invoke(this, e);
                    break;
            }
        }

        private void OnMouseLeaveTimerTick(object sender, EventArgs args)
        {
            if (_popUp != null) return;

            if (!_hasItemsMouseOver && _listView != null)
            {
                HideDropDown();
            }

            _mouseLeaveTimer.Stop();
        }

        private void OnListViewMouseLeaveTimerTick(object sender, EventArgs args)
        {
            var form = FindForm();
            if (form == null || _listView == null) return;

            var coordinates = form.PointToClient(Cursor.Position);
            
            if (_popUp != null)
            {
                if (_hasItemsMouseOver && !(_popUp.BoundsContainsCursorPosition() || Bounds.Contains(coordinates)) || !_listViewShown)
                {
                    HideDropDown();
                    return;
                }
            }
            else
            {
                var listViewBounds = new Rectangle(
                    _listView.Bounds.X,
                    _listView.Bounds.Y - 25,
                    _listView.Bounds.Width + 50,
                    _listView.Bounds.Height + 75);
                
                if (_hasItemsMouseOver && !(listViewBounds.Contains(coordinates) || Bounds.Contains(coordinates)) || !_listViewShown)
                {
                    HideDropDown();
                    return;
                }
            }

            _hasItemsMouseOver = true;
        }

        private void TextBoxTextChanged(object sender, EventArgs e)
        {
            Invalidate();
            TextChanged?.Invoke(sender, e);
        }

        #endregion

        #region Navigation Methods

        private void NavigateUp()
        {
            if (_selectedIndex > 0)
            {
                Navigate(_selectedIndex - 1);
            }
        }

        private void NavigateDown()
        {
            if (_selectedIndex < _items.Count - 1)
            {
                Navigate(_selectedIndex + 1);
            }
        }

        private void NavigatePageUp()
        {
            if (_selectedIndex > 0)
            {
                Navigate(Math.Max(0, _selectedIndex - MaxPageNavigationStep));
            }
        }

        private void NavigatePageDown()
        {
            if (_selectedIndex < _items.Count - 1)
            {
                Navigate(Math.Min(_items.Count - 1, _selectedIndex + MaxPageNavigationStep));
            }
        }

        private void Navigate(int index)
        {
            if (index < 0 || index >= _items.Count) return;

            _selectedIndex = index;
            _textBox.Text = Items[_selectedIndex].ToString();

            if (!_skipPaint)
            {
                Invalidate();
            }

            _textBox.SelectionStart = 0;
            _textBox.SelectionLength = _textBox.Text.Length;
            NotifyTextChanged();
        }

        private void HandleAlphaNumericKey(KeyEventArgs e)
        {
            if (!IsAlphaNumericKey(e.KeyCode) || _items.Count == 0) return;

            var letter = GetKeyLetter(e.KeyCode);
            var startIndex = GetSearchStartIndex(letter);

            // Search from start index to end
            for (var idx = startIndex; idx < _items.Count; idx++)
            {
                if (_items[idx].ToString().StartsWith(letter, StringComparison.OrdinalIgnoreCase))
                {
                    SelectedIndex = idx;
                    e.SuppressKeyPress = true;
                    return;
                }
            }

            // Search from beginning if not found
            var item = _items.FirstOrDefault(p => p.ToString().StartsWith(letter, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                var idx = _items.IndexOf(item);
                if (idx != _selectedIndex)
                {
                    SelectedIndex = idx;
                    e.SuppressKeyPress = true;
                    return;
                }
            }

            KeyDown?.Invoke(this, e);
        }

        private bool IsAlphaNumericKey(Keys keyCode)
        {
            return (keyCode >= Keys.A && keyCode <= Keys.Z) ||
                   (keyCode >= Keys.D0 && keyCode <= Keys.D9) ||
                   (keyCode >= Keys.NumPad0 && keyCode <= Keys.NumPad9);
        }

        private string GetKeyLetter(Keys keyCode)
        {
            var letter = keyCode.ToString();
            
            if (letter.Length == 2 && letter.StartsWith("D"))
            {
                letter = letter.Substring(1);
            }
            else if (letter.Length == 7 && letter.StartsWith("NumPad", StringComparison.Ordinal))
            {
                letter = letter.Substring(6);
            }

            return letter;
        }

        private int GetSearchStartIndex(string letter)
        {
            if (_selectedIndex >= 0 && _items[_selectedIndex].ToString().StartsWith(letter, StringComparison.OrdinalIgnoreCase))
            {
                return _selectedIndex + 1;
            }
            return 0;
        }

        #endregion

        #region Dropdown Management

        private void HideDropDown()
        {
            try
            {
                if (_popUp != null)
                {
                    _popUp.DoClose = true;
                }

                _listViewMouseLeaveTimer?.Stop();
                _mouseLeaveTimer?.Stop();
                
                if (_listViewShown)
                {
                    DropDownClosed?.Invoke(this, EventArgs.Empty);
                    _listViewShown = false;
                }

                var form = _listView?.FindForm() ?? FindForm();
                if (form != null && _listView != null)
                {
                    form.Controls.Remove(_listView);
                    form.Invalidate();
                }

                Invalidate();

                if (_textBox?.Visible == true)
                {
                    _textBox.Focus();
                    _textBox.SelectionLength = 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in HideDropDown: {ex.Message}");
            }
        }

        private void ShowListView()
        {
            try
            {
                _textBox.Focus();
                _listViewShown = true;
                EnsureListViewInitialized();

                PopulateListView();

                var form = FindForm();
                var isOverflow = Parent?.GetType() == typeof(ToolStripOverflow);
                
                if (isOverflow || form == null || UsePopupWindow)
                {
                    HandleOverflowMode(form);
                }
                else
                {
                    HandleNormalMode(form);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ShowListView: {ex.Message}");
            }
        }

        private void PopulateListView()
        {
            _listView.BeginUpdate();
            _listView.Items.Clear();
            
            var listViewItems = new List<ListViewItem>(_items.Count);
            foreach (var item in _items)
            {
                listViewItems.Add(new ListViewItem(item.ToString()));
            }

            _listView.Items.AddRange(listViewItems.ToArray());
            _listView.Width = DropDownWidth > 0 ? DropDownWidth : Width;
            _listView.EndUpdate();
        }

        private void HandleNormalMode(Form form)
        {
            var position = CalculateDropdownPosition(form);
            var height = CalculateDropdownHeight(position.spaceBottom, position.spaceTop);
            
            _listView.Height = height.finalHeight;
            _listView.Left = position.x;
            _listView.Top = position.y;
            
            if (form.Width < _listView.Left + _listView.Width)
            {
                _listView.Left = Math.Max(0, _listView.Left - (_listView.Left + _listView.Width - form.Width + 20));
            }

            form.Controls.Add(_listView);
            _listView.BringToFront();

            ConfigureListViewScrolling(height.hasScrollBar);
            TriggerDropDownEvent();
            SelectCurrentItem();
        }

        private (int x, int y, int spaceBottom, int spaceTop) CalculateDropdownPosition(Form form)
        {
            var ctl = (Control)this;
            var totalX = ctl.Left;
            var totalY = ctl.Top;
            
            while (ctl.Parent != form)
            {
                ctl = ctl.Parent;
                totalX += ctl.Left;
                totalY += ctl.Top;
            }
            
            var spaceBottom = form.Height - (totalY + Height);
            var spaceTop = totalY;
            var y = totalY + Height;
            
            return (totalX, y, spaceBottom, spaceTop);
        }

        private (int finalHeight, bool hasScrollBar) CalculateDropdownHeight(int spaceBottom, int spaceTop)
        {
            if (_items.Count == 0) return (18, false);

            var itemHeight = _listView.GetItemRect(0).Height;
            var virtualHeight = itemHeight * _items.Count + 9;
            var maxHeight = DropDownHeight;
            
            int finalHeight;
            if (spaceBottom >= DropDownHeight || spaceBottom * 1.2 > spaceTop)
            {
                maxHeight = Math.Min(maxHeight, spaceBottom - 18 - SystemInformation.CaptionHeight);
                finalHeight = Math.Min(virtualHeight, maxHeight);
            }
            else
            {
                maxHeight = Math.Min(maxHeight, spaceTop - 18 - SystemInformation.CaptionHeight);
                finalHeight = Math.Min(virtualHeight, maxHeight);
            }

            return (finalHeight, virtualHeight > finalHeight);
        }

        private void HandleOverflowMode(Form form)
        {
            UpdateColorsForOverflow();
            
            var height = CalculateOverflowHeight();
            _listView.Height = height.finalHeight;
            
            ConfigureListViewScrolling(height.hasScrollBar);
            TriggerDropDownEvent();
            SelectCurrentItem();
            
            ShowPopupWindow(form);
        }

        private void UpdateColorsForOverflow()
        {
            BackColor = UiUtil.BackColor;
            ForeColor = UiUtil.ForeColor;
            if (Parent != null)
            {
                Parent.BackColor = BackColor;
                Parent.ForeColor = ForeColor;
                Parent.Invalidate();
            }
        }

        private (int finalHeight, bool hasScrollBar) CalculateOverflowHeight()
        {
            if (_items.Count == 0) return (18, false);

            var itemHeight = _listView.GetItemRect(0).Height;
            var virtualHeight = itemHeight * _items.Count + 16;
            var finalHeight = Math.Min(virtualHeight, DropDownHeight);
            
            return (finalHeight, virtualHeight > finalHeight);
        }

        private void ConfigureListViewScrolling(bool hasScrollBar)
        {
            if (hasScrollBar)
            {
                _listView.AutoResizeColumns(ColumnHeaderAutoResizeStyle.None);
            }
            else
            {
                _listView.Scrollable = false;
                _listView.Columns[0].Width = -2;
            }
        }

        private void TriggerDropDownEvent()
        {
            DropDown?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }

        private void SelectCurrentItem()
        {
            if (_selectedIndex >= 0 && _selectedIndex < _listView.Items.Count)
            {
                _listView.Focus();
                _listView.Items[_selectedIndex].Selected = true;
                _listView.EnsureVisible(_selectedIndex);
                _listView.Items[_selectedIndex].Focused = true;
            }
        }

        private void ShowPopupWindow(Form form)
        {
            _popUp?.Dispose();
            
            var (x, y) = CalculatePopupPosition(form);
            
            _popUp = new NikseComboBoxPopUp(_listView, SelectedIndex, x, y);
            var result = _popUp.ShowDialog(Parent);
            
            if (result == DialogResult.OK && _listView.SelectedItems.Count > 0)
            {
                SelectedIndex = _listView.SelectedItems[0].Index;
            }
            
            CleanupAfterPopup();
        }

        private (int x, int y) CalculatePopupPosition(Form form)
        {
            var x = Cursor.Position.X - (DropDownWidth / 2);
            var y = Cursor.Position.Y;
            
            if (UsePopupWindow && form != null)
            {
                var ctl = (Control)this;
                var totalX = ctl.Left;
                var totalY = ctl.Top;
                
                while (ctl.Parent != form)
                {
                    ctl = ctl.Parent;
                    totalX += ctl.Left;
                    totalY += ctl.Top;
                }

                var p = PointToScreen(new Point(Left - totalX, Bottom - totalY));
                x = p.X;
                y = p.Y;
            }
            
            return (x, y);
        }

        private void CleanupAfterPopup()
        {
            _listView?.Dispose();
            _listView = null;
            _listViewShown = false;
            Invalidate();
        }

        #region ListView Initialization

        private void EnsureListViewInitialized()
        {
            if (_listView != null) return;

            try
            {
                _listView = CreateListView();
                ConfigureListViewProperties();
                AttachListViewEventHandlers();
                ApplyTheme();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing ListView: {ex.Message}");
            }
        }

        private ListView CreateListView()
        {
            var listView = new ListView
            {
                View = View.Details,
                HeaderStyle = ColumnHeaderStyle.None,
                FullRowSelect = true,
                MultiSelect = false,
                HideSelection = false,
                GridLines = false,
                Font = Font
            };

            var w = DropDownWidth > 0 ? DropDownWidth : Width;
            var widthNoScrollBar = w - SystemInformation.VerticalScrollBarWidth - (SystemInformation.BorderSize.Width * 4);
            listView.Columns.Add("text", widthNoScrollBar);

            return listView;
        }

        private void ConfigureListViewProperties()
        {
            if (_listView == null) return;

            _listView.Font = Font;
        }

        private void AttachListViewEventHandlers()
        {
            if (_listView == null) return;

            _listView.MouseEnter += OnListViewMouseEnter;
            _listView.KeyDown += OnListViewKeyDown;
            _listView.MouseClick += OnListViewMouseClick;
            _listView.LostFocus += OnListViewLostFocus;
        }

        private void ApplyTheme()
        {
            if (_listView != null && Configuration.Settings.General.UseDarkTheme)
            {
                DarkTheme.SetDarkTheme(_listView);
            }
        }

        private void OnListViewMouseEnter(object sender, EventArgs args)
        {
            _hasItemsMouseOver = true;
        }

        private void OnListViewKeyDown(object sender, KeyEventArgs args)
        {
            switch (args.KeyCode)
            {
                case Keys.Escape:
                    HandleListViewEscape(args);
                    break;
                case Keys.Enter:
                    HandleListViewEnter(args);
                    break;
                default:
                    KeyDown?.Invoke(this, args);
                    break;
            }
        }

        private void HandleListViewEscape(KeyEventArgs args)
        {
            args.SuppressKeyPress = true;
            args.Handled = true;
            HideDropDown();
        }

        private void HandleListViewEnter(KeyEventArgs args)
        {
            if (_listView.SelectedItems.Count == 0) return;

            _listViewMouseLeaveTimer.Stop();
            var item = _listView.SelectedItems[0];
            _selectedIndex = item.Index;
            _textBox.Text = item.Text;

            HideDropDown();
            args.SuppressKeyPress = true;

            if (!_skipPaint)
            {
                Invalidate();
            }

            NotifyTextChanged();
        }

        private void OnListViewMouseClick(object sender, MouseEventArgs mouseArgs)
        {
            if (mouseArgs == null || _listView == null) return;

            var cachedCount = _listView.Items.Count;
            for (var i = 0; i < cachedCount; i++)
            {
                var rectangle = _listView.GetItemRect(i);
                if (rectangle.Contains(mouseArgs.Location))
                {
                    _listViewMouseLeaveTimer.Stop();
                    _selectedIndex = i;
                    _textBox.Text = _listView.Items[i].Text;

                    HideDropDown();
                    _textBox.Focus();
                    _textBox.SelectionLength = 0;

                    NotifyTextChanged();
                    return;
                }
            }
        }

        private void OnListViewLostFocus(object sender, EventArgs e)
        {
            if (_textBox != null && _listViewShown && !Focused && !_textBox.Focused)
            {
                HideDropDown();
            }
        }

        #endregion

        #region Painting

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_skipPaint || _textBox == null || e?.Graphics == null) return;

            try
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                ConfigureTextBox();

                if (!Enabled)
                {
                    DrawDisabled(e);
                    return;
                }

                DrawEnabledState(e);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in OnPaint: {ex.Message}");
            }
        }

        private void ConfigureTextBox()
        {
            _textBox.BackColor = BackColor;
            _textBox.BorderStyle = BorderStyle.None;
            _textBox.Top = 2;
            _textBox.Left = RightToLeft == RightToLeft.Yes ? ButtonsWidth : 3;
            _textBox.Height = Height - 4;
            _textBox.Width = Width - ButtonsWidth - 3;
        }

        private void DrawEnabledState(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            DrawBorder(e);
            HandleTextBoxVisibility(e);
            DrawDropdownButton(e);
        }

        private void DrawBorder(PaintEventArgs e)
        {
            var isFocused = Focused || _textBox.Focused || (_listView?.Focused == true);
            var borderColor = isFocused ? _buttonForeColorOver : BorderColor;
            
            using var pen = new Pen(borderColor, 1f);
            var borderRectangle = new Rectangle(0, 0, Width - 1, Height - 1);
            e.Graphics.DrawRectangle(pen, borderRectangle);
        }

        private void HandleTextBoxVisibility(PaintEventArgs e)
        {
            if (DropDownStyle == ComboBoxStyle.DropDown)
            {
                if (!_textBox.Visible)
                {
                    _textBox.Visible = true;
                }
                _textBox.Invalidate();
            }
            else
            {
                if (_textBox.Visible)
                {
                    _textBox.Visible = false;
                }
                DrawText(e, ButtonForeColor);
            }
        }

        private void DrawDropdownButton(PaintEventArgs e)
        {
            var brush = GetButtonBrush();
            var left = RightToLeft == RightToLeft.Yes ? 3 : Width - ButtonsWidth;
            var height = Height / 2 - 4;
            var top = (height / 2) + 5;
            
            DrawArrow(e, brush, left, top, height);
        }

        private Brush GetButtonBrush()
        {
            if (_buttonDownActive)
            {
                return _buttonLeftIsDown ? _buttonForeColorDownBrush : _buttonForeColorOverBrush;
            }
            return _buttonForeColorBrush;
        }

        private void DrawArrow(PaintEventArgs e, Brush brush, int left, int top, int height)
        {
            if (_listViewShown)
            {
                NikseUpDown.DrawArrowUp(e.Graphics, brush, left, top - 1, height);
            }
            else
            {
                NikseUpDown.DrawArrowDown(e.Graphics, brush, left, top, height);
            }
        }

        private void DrawDisabled(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColorDisabled);

            if (!_textBox.Visible)
            {
                _textBox.Visible = true;
            }

            using var pen = new Pen(BorderColorDisabled, 1f);
            var borderRectangle = new Rectangle(0, 0, Width - 1, Height - 1);
            e.Graphics.DrawRectangle(pen, borderRectangle);

            _textBox.Invalidate();

            var left = RightToLeft == RightToLeft.Yes ? 3 : Width - ButtonsWidth;
            var height = Height / 2 - 4;
            var top = (height / 2) + 5;
            
            using var brush = new SolidBrush(BorderColorDisabled);
            DrawArrow(e, brush, left, top, height);
        }

        private void DrawText(PaintEventArgs e, Color textColor)
        {
            var textFormatFlags = CreateTextFormatFlags(this, _textBox.TextAlign, false);

            TextRenderer.DrawText(e.Graphics,
                _textBox.Text,
                _textBox.Font,
                new Rectangle(_textBox.Left, _textBox.Top + 1, _textBox.Width, _textBox.Height),
                textColor,
                textFormatFlags);
        }

        internal static TextFormatFlags CreateTextFormatFlags(Control control, HorizontalAlignment contentAlignment, bool useMnemonic)
        {
            var textFormatFlags = TextFormatFlags.TextBoxControl | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;

            if (contentAlignment == HorizontalAlignment.Left)
            {
                textFormatFlags |= TextFormatFlags.Left;
            }
            else if (contentAlignment == HorizontalAlignment.Center)
            {
                textFormatFlags |= TextFormatFlags.HorizontalCenter;
            }
            else if (contentAlignment == HorizontalAlignment.Right)
            {
                textFormatFlags |= TextFormatFlags.Right;
            }

            if (control.RightToLeft == RightToLeft.Yes)
            {
                textFormatFlags |= TextFormatFlags.RightToLeft;
            }

            textFormatFlags |= !useMnemonic ? TextFormatFlags.NoPrefix : TextFormatFlags.HidePrefix;

            return textFormatFlags;
        }

        #endregion
            else if (contentAlignment == HorizontalAlignment.Center)
            {
                textFormatFlags |= TextFormatFlags.HorizontalCenter;
            }
            else if (contentAlignment == HorizontalAlignment.Right)
            {
                textFormatFlags |= TextFormatFlags.Right;
            }

            if (control.RightToLeft == RightToLeft.Yes)
            {
                textFormatFlags |= TextFormatFlags.RightToLeft;
            }

            textFormatFlags |= !useMnemonic ? TextFormatFlags.NoPrefix : TextFormatFlags.HidePrefix;

            return textFormatFlags;
        }

        private void DrawText(PaintEventArgs e, Color textColor)
        {
            var textFormatFlags = CreateTextFormatFlags(this, _textBox.TextAlign, false);

            TextRenderer.DrawText(e.Graphics,
                _textBox.Text,
                _textBox.Font,
                new Rectangle(_textBox.Left, _textBox.Top + 1, _textBox.Width, _textBox.Height),
                textColor,
                textFormatFlags);
        }

        public override RightToLeft RightToLeft
        {
            get => base.RightToLeft;
            set
            {
                if (_textBox != null)
                {
                    _textBox.RightToLeft = value;
                }

                base.RightToLeft = value;
                Invalidate();
            }
        }

        public bool DroppedDown => _listViewShown;

        public bool FormattingEnabled { get; set; }

        public int MaxLength
        {
            get
            {
                if (_textBox == null)
                {
                    return 0;
                }

                return _textBox.MaxLength;
            }
            set
            {
                if (_textBox == null)
                {
                    return;
                }

                _textBox.MaxLength = value;
            }
        }

        private void DrawArrow(PaintEventArgs e, Brush brush, int left, int top, int height)
        {
            if (_listViewShown)
            {
                NikseUpDown.DrawArrowUp(e.Graphics, brush, left, top - 1, height);
            }
            else
            {
                NikseUpDown.DrawArrowDown(e.Graphics, brush, left, top, height);
            }
        }

        private void DrawDisabled(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColorDisabled);

            if (!_textBox.Visible)
            {
                _textBox.Visible = true;
            }

            using (var pen = new Pen(BorderColorDisabled, 1f))
            {
                var borderRectangle = new Rectangle(0, 0, Width - 1, Height - 1);
                e.Graphics.DrawRectangle(pen, borderRectangle);
            }

            _textBox.Invalidate();

            var left = RightToLeft == RightToLeft.Yes ? 3 : Width - ButtonsWidth;
            var height = Height / 2 - 4;
            var top = (height / 2) + 5;
            using (var brush = new SolidBrush(BorderColorDisabled))
            {
                DrawArrow(e, brush, left, top, height);
            }
        }

        private bool _skipPaint;
        private readonly bool _loading;

        public void BeginUpdate()
        {
            _skipPaint = true;
        }

        public void EndUpdate()
        {
            _skipPaint = false;
            Invalidate();
        }

        public void SelectAll()
        {
            if (_textBox != null && DropDownStyle == ComboBoxStyle.DropDown)
            {
                _textBox.SelectAll();
            }
        }

        private class InnerTextBox : TextBox
        {
            private readonly NikseComboBox _owner;

            public InnerTextBox(NikseComboBox owner)
            {
                _owner = owner;
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == 0x0204) // WM_RBUTTONDOWN
                {
                    var x = _owner.Location.X + (short)m.LParam.ToInt32();
                    var y = _owner.Location.Y + (short)m.LParam.ToInt32() >> 16;
                    _owner.ContextMenuStrip?.Show(_owner, new Point(x, y));
                }
                else // this "else" is important as we don't want to show the OS default context menu
                {
                    base.WndProc(ref m);
                }
            }
        }
    }
}
