using Nikse.SubtitleEdit.Logic;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Controls
{
    /// <summary>
    /// A popup form for displaying NikseComboBox dropdown items.
    /// </summary>
    public sealed partial class NikseComboBoxPopUp : Form
    {
        #region Constants

        private const int TimerInitialInterval = 1000;
        private const int TimerActiveInterval = 100;
        private const int BoundsPadding = 25;

        #endregion

        #region Fields

        private readonly ListView _listView;
        private readonly Timer _closeTimer;
        private bool _hasMouseOver;
        private bool _disposed;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether the popup should close.
        /// </summary>
        public bool DoClose { get; set; }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the NikseComboBoxPopUp class.
        /// </summary>
        /// <param name="listView">The ListView to display in the popup.</param>
        /// <param name="selectedIndex">The initially selected item index.</param>
        /// <param name="x">The X coordinate of the popup location.</param>
        /// <param name="y">The Y coordinate of the popup location.</param>
        public NikseComboBoxPopUp(ListView listView, int selectedIndex, int x, int y)
        {
            if (listView == null)
                throw new ArgumentNullException(nameof(listView));

            InitializeComponent();

            _listView = listView;
            InitializePopup(x, y);
            SetupSelectedItem(selectedIndex);
            SetupEventHandlers();
            _closeTimer = CreateCloseTimer();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Determines whether the popup bounds contain the cursor position with padding.
        /// </summary>
        /// <returns>true if the cursor is within the extended bounds; otherwise, false.</returns>
        public bool BoundsContainsCursorPosition()
        {
            var coordinates = Cursor.Position;
            var extendedBounds = new Rectangle(
                Bounds.Left - BoundsPadding,
                Bounds.Top - BoundsPadding,
                Bounds.Width + (BoundsPadding * 2),
                Bounds.Height + (BoundsPadding * 2));
            return extendedBounds.Contains(coordinates);
        }

        #endregion

        #region Private Methods

        private void InitializePopup(int x, int y)
        {
            Controls.Add(_listView);
            BackColor = UiUtil.BackColor;

            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.None;
            Location = new Point(x, y);
            Width = _listView.Width + 2;
            Height = _listView.Height + 2;
            _listView.Dock = DockStyle.Fill;

            _listView.BringToFront();
            KeyPreview = true;
        }

        private void SetupSelectedItem(int selectedIndex)
        {
            if (selectedIndex >= 0 && selectedIndex < _listView.Items.Count)
            {
                _listView.Focus();
                _listView.Items[selectedIndex].Selected = true;
                _listView.EnsureVisible(selectedIndex);
                _listView.Items[selectedIndex].Focused = true;
            }
        }

        private void SetupEventHandlers()
        {
            KeyDown += NikseComboBoxPopUp_KeyDown;

            MouseEnter += (sender, args) => _hasMouseOver = true;
            MouseMove += (sender, args) => _hasMouseOver = true;
            MouseLeave += (sender, args) => _hasMouseOver = false;
        }

        private Timer CreateCloseTimer()
        {
            var timer = new Timer { Interval = TimerInitialInterval };
            timer.Tick += OnCloseTimerTick;
            timer.Start();
            return timer;
        }

        private void OnCloseTimerTick(object sender, EventArgs args)
        {
            var timer = (Timer)sender;
            timer.Interval = TimerActiveInterval;

            if (DoClose)
            {
                ClosePopup(timer, DialogResult.Cancel);
                return;
            }

            if (IsMouseButtonPressed() && !IsMouseInValidArea())
            {
                ClosePopup(timer, DialogResult.Cancel);
            }
        }

        private bool IsMouseButtonPressed()
        {
            return MouseButtons == MouseButtons.Left || MouseButtons == MouseButtons.Right;
        }

        private bool IsMouseInValidArea()
        {
            return _hasMouseOver || Bounds.Contains(Cursor.Position);
        }

        private void ClosePopup(Timer timer, DialogResult result)
        {
            timer.Stop();
            DialogResult = result;
            Controls.Remove(_listView);
        }

        private void NikseComboBoxPopUp_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Escape:
                    HandleEscapeKey(e);
                    break;
                case Keys.Enter:
                    HandleEnterKey(e);
                    break;
            }
        }

        private void HandleEscapeKey(KeyEventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            e.Handled = true;
            Controls.Remove(_listView);
        }

        private void HandleEnterKey(KeyEventArgs e)
        {
            DialogResult = DialogResult.OK;
            e.Handled = true;
            Controls.Remove(_listView);
        }

        #endregion

        #region Dispose Pattern

        /// <summary>
        /// Releases all resources used by the NikseComboBoxPopUp.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _closeTimer?.Stop();
                _closeTimer?.Dispose();
                _disposed = true;
            }

            base.Dispose(disposing);
        }

        #endregion
    }
}
