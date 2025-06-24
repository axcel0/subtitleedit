using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Controls
{
    /// <summary>
    /// A text box control with enhanced drag-and-drop functionality and word selection capabilities.
    /// Supports intelligent text manipulation, drag-and-drop operations, and improved user interaction.
    /// </summary>
    public sealed class SimpleTextBox : TextBox, IDisposable
    {
        #region Constants
        /// <summary>
        /// Windows message for double-click events.
        /// </summary>
        private const int WM_DBLCLICK = 0xA3;
        
        /// <summary>
        /// Windows message for left button double-click events.
        /// </summary>
        private const int WM_LBUTTONDBLCLK = 0x203;
        
        /// <summary>
        /// Windows message for left button down events.
        /// </summary>
        private const int WM_LBUTTONDOWN = 0x0201;
        
        /// <summary>
        /// Minimum time in milliseconds before drag operation can start.
        /// </summary>
        private const long MinimumDragDelayMs = 400;
        
        /// <summary>
        /// Minimum time in milliseconds after focus before processing click events.
        /// </summary>
        private const long MinimumFocusDelayMs = 10;
        
        /// <summary>
        /// Characters that typically appear at the end of sentences or phrases.
        /// </summary>
        private const string ExpectedEndChars = ":;]<.!?؟";
        
        /// <summary>
        /// Conversion factor from ticks to milliseconds.
        /// </summary>
        private const long TicksToMilliseconds = 10000;
        #endregion

        #region Private Fields
        private string _dragText = string.Empty;
        private int _dragStartFrom;
        private long _dragStartTicks;
        private bool _dragRemoveOld;
        private bool _dragFromThis;
        private long _gotFocusTicks;
        private bool _disposed;
        #endregion

        #region Constructor and Initialization
        /// <summary>
        /// Initializes a new instance of the SimpleTextBox control.
        /// </summary>
        public SimpleTextBox()
        {
            InitializeControl();
            SetupEventHandlers();
        }

        /// <summary>
        /// Initializes the control properties and settings.
        /// </summary>
        private void InitializeControl()
        {
            SetAlignment();
            AllowDrop = true;
        }

        /// <summary>
        /// Sets up all event handlers for the control.
        /// </summary>
        private void SetupEventHandlers()
        {
            DragEnter += OnDragEnter;
            DragDrop += OnDragDrop;
            MouseDown += OnMouseDown;
            MouseUp += OnMouseUp;
            KeyDown += OnKeyDown;

            // Handle focus to fix issue where WM_LBUTTONDOWN got wrong "SelectedText" (only in undocked mode)
            GotFocus += OnGotFocus;
        }

        /// <summary>
        /// Event handler for when the control gains focus.
        /// </summary>
        private void OnGotFocus(object sender, EventArgs args)
        {
            _gotFocusTicks = Stopwatch.GetTimestamp();
        }
        #endregion

        #region Text Alignment
        /// <summary>
        /// Sets the text alignment based on configuration settings.
        /// </summary>
        private void SetAlignment()
        {
            try
            {
                if (Configuration.Settings?.General?.CenterSubtitleInTextBox == true && 
                    TextAlign != HorizontalAlignment.Center)
                {
                    TextAlign = HorizontalAlignment.Center;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error setting text alignment: {ex.Message}");
            }
        }
        #endregion

        #region Event Handlers
        /// <summary>
        /// Handles key down events for special key combinations.
        /// </summary>
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            try
            {
                // Handle Ctrl+A for select all
                if (e.Modifiers == Keys.Control && e.KeyCode == Keys.A)
                {
                    SelectAll();
                    e.SuppressKeyPress = true;
                    return;
                }

                // Handle Ctrl+Backspace for word deletion
                if (e.Modifiers == Keys.Control && e.KeyCode == Keys.Back)
                {
                    UiUtil.ApplyControlBackspace(this);
                    e.SuppressKeyPress = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling key down event: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles mouse up events to reset drag state.
        /// </summary>
        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            ResetDragState();
        }

        /// <summary>
        /// Resets the drag operation state.
        /// </summary>
        private void ResetDragState()
        {
            _dragRemoveOld = false;
            _dragFromThis = false;
        }

        /// <summary>
        /// Handles mouse down events to initiate drag operations.
        /// </summary>
        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if (MouseButtons != MouseButtons.Left || string.IsNullOrEmpty(_dragText))
                return;

            try
            {
                if (IsClickWithinDragText(e.Location))
                {
                    RestoreSelection();
                    StartDragOperation();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling mouse down event: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if the click position is within the draggable text area.
        /// </summary>
        /// <param name="clickLocation">The location of the mouse click.</param>
        /// <returns>True if the click is within the drag text area.</returns>
        private bool IsClickWithinDragText(Point clickLocation)
        {
            var index = GetCharIndexFromPosition(clickLocation);
            return index >= _dragStartFrom && index <= _dragStartFrom + _dragText.Length;
        }

        /// <summary>
        /// Restores the selection to the drag text.
        /// </summary>
        private void RestoreSelection()
        {
            SelectionStart = _dragStartFrom;
            SelectionLength = _dragText.Length;
        }

        /// <summary>
        /// Starts the drag and drop operation.
        /// </summary>
        private void StartDragOperation()
        {
            try
            {
                var dataObject = CreateDragDataObject();
                _dragFromThis = true;

                var effect = ModifierKeys == Keys.Control ? DragDropEffects.Copy : DragDropEffects.Move;
                _dragRemoveOld = effect == DragDropEffects.Move;

                DoDragDrop(dataObject, effect);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error starting drag operation: {ex.Message}");
                ResetDragState();
            }
        }

        /// <summary>
        /// Creates a data object for drag and drop operations.
        /// </summary>
        /// <returns>A DataObject containing the drag text.</returns>
        private DataObject CreateDragDataObject()
        {
            var dataObject = new DataObject();
            dataObject.SetText(_dragText, TextDataFormat.UnicodeText);
            dataObject.SetText(_dragText, TextDataFormat.Text);
            return dataObject;
        }
        #endregion

        #region Drag and Drop Implementation
        /// <summary>
        /// Handles drag drop events to process dropped text.
        /// </summary>
        private void OnDragDrop(object sender, DragEventArgs e)
        {
            try
            {
                var dropLocation = PointToClient(new Point(e.X, e.Y));
                var insertIndex = GetCharIndexFromPosition(dropLocation);
                var newText = ExtractDroppedText(e.Data);

                if (string.IsNullOrWhiteSpace(newText))
                    return;

                ProcessTextDrop(newText, insertIndex);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error handling drag drop: {ex.Message}");
            }
            finally
            {
                ResetDragState();
            }
        }

        /// <summary>
        /// Extracts text from drag and drop data.
        /// </summary>
        /// <param name="data">The drag and drop data object.</param>
        /// <returns>The extracted text or empty string if no valid text found.</returns>
        private static string ExtractDroppedText(IDataObject data)
        {
            if (data.GetDataPresent(DataFormats.UnicodeText))
            {
                return (string)data.GetData(DataFormats.UnicodeText);
            }

            if (data.GetDataPresent(DataFormats.Text))
            {
                return (string)data.GetData(DataFormats.Text);
            }

            return string.Empty;
        }

        /// <summary>
        /// Processes the dropped text and inserts it into the control.
        /// </summary>
        /// <param name="newText">The text to insert.</param>
        /// <param name="insertIndex">The position to insert the text.</param>
        private void ProcessTextDrop(string newText, int insertIndex)
        {
            if (string.IsNullOrWhiteSpace(Text))
            {
                Text = newText;
                return;
            }

            var processedIndex = insertIndex;
            var isAppendOperation = IsAppendOperation(insertIndex);

            if (_dragFromThis)
            {
                if (!IsValidDragOperation(insertIndex))
                    return;

                if (_dragRemoveOld)
                {
                    processedIndex = RemoveOriginalDragText(insertIndex);
                }
            }

            InsertTextAtPosition(newText, processedIndex, isAppendOperation);
            FixTextSpacing(newText, processedIndex);
            SetFinalSelection(processedIndex);
        }

        /// <summary>
        /// Determines if this is an append operation at the end of text.
        /// </summary>
        /// <param name="index">The insertion index.</param>
        /// <returns>True if appending to the end.</returns>
        private bool IsAppendOperation(int index) => 
            index == Text.Length - 1 && index > 0;

        /// <summary>
        /// Validates if the drag operation should proceed.
        /// </summary>
        /// <param name="insertIndex">The insertion index.</param>
        /// <returns>True if the operation is valid.</returns>
        private bool IsValidDragOperation(int insertIndex)
        {
            var milliseconds = (Stopwatch.GetTimestamp() - _dragStartTicks) / TicksToMilliseconds;
            if (milliseconds < MinimumDragDelayMs)
            {
                SelectionLength = 0;
                SelectionStart = IsAppendOperation(insertIndex) ? insertIndex + 1 : insertIndex;
                return false; // Too fast - prevent accidental operations
            }

            // Don't drop same text at same position
            return !(insertIndex >= _dragStartFrom && insertIndex <= _dragStartFrom + _dragText.Length);
        }

        /// <summary>
        /// Removes the original drag text when moving (not copying).
        /// </summary>
        /// <param name="insertIndex">The insertion index.</param>
        /// <returns>The adjusted insertion index.</returns>
        private int RemoveOriginalDragText(int insertIndex)
        {
            var adjustedIndex = insertIndex;
            Text = Text.Remove(_dragStartFrom, _dragText.Length);

            // Fix spacing after removal
            adjustedIndex = FixSpacingAfterRemoval(adjustedIndex);

            // Adjust index if insertion point is after the removed text
            if (adjustedIndex > _dragStartFrom)
            {
                adjustedIndex -= _dragText.Length;
            }

            return Math.Max(0, adjustedIndex);
        }

        /// <summary>
        /// Fixes spacing issues after removing drag text.
        /// </summary>
        /// <param name="insertIndex">The current insertion index.</param>
        /// <returns>The adjusted insertion index.</returns>
        private int FixSpacingAfterRemoval(int insertIndex)
        {
            var adjustedIndex = insertIndex;

            // Remove leading space if text starts with space after removal
            if (_dragStartFrom == 0 && Text.Length > 0 && Text[0] == ' ')
            {
                Text = Text.Remove(0, 1);
                adjustedIndex--;
            }
            // Remove double spaces
            else if (HasDoubleSpaceAt(_dragStartFrom))
            {
                Text = Text.Remove(_dragStartFrom, 1);
                if (_dragStartFrom < adjustedIndex)
                {
                    adjustedIndex--;
                }
            }
            // Remove space before punctuation
            else if (HasSpaceBeforePunctuation(_dragStartFrom))
            {
                Text = Text.Remove(_dragStartFrom, 1);
                if (_dragStartFrom < adjustedIndex)
                {
                    adjustedIndex--;
                }
            }

            return adjustedIndex;
        }

        /// <summary>
        /// Checks if there's a double space at the specified position.
        /// </summary>
        /// <param name="position">The position to check.</param>
        /// <returns>True if double space exists.</returns>
        private bool HasDoubleSpaceAt(int position) =>
            position > 1 && 
            Text.Length > position + 1 && 
            Text[position] == ' ' && 
            Text[position - 1] == ' ';

        /// <summary>
        /// Checks if there's a space before punctuation at the specified position.
        /// </summary>
        /// <param name="position">The position to check.</param>
        /// <returns>True if space before punctuation exists.</returns>
        private bool HasSpaceBeforePunctuation(int position) =>
            position > 0 && 
            Text.Length > position + 1 && 
            Text[position] == ' ' && 
            ExpectedEndChars.Contains(Text[position + 1]);

        /// <summary>
        /// Inserts text at the specified position.
        /// </summary>
        /// <param name="newText">The text to insert.</param>
        /// <param name="index">The insertion index.</param>
        /// <param name="isAppend">Whether this is an append operation.</param>
        private void InsertTextAtPosition(string newText, int index, bool isAppend)
        {
            if (isAppend)
            {
                Text += newText;
            }
            else
            {
                Text = Text.Insert(Math.Min(index, Text.Length), newText);
            }
        }

        /// <summary>
        /// Fixes spacing around the inserted text.
        /// </summary>
        /// <param name="newText">The inserted text.</param>
        /// <param name="index">The insertion index.</param>
        private void FixTextSpacing(string newText, int index)
        {
            var endIndex = index + newText.Length;

            // Fix start spacing
            if (NeedsLeadingSpace(newText, index))
            {
                Text = Text.Insert(index, " ");
                endIndex++;
            }
            else if (HasExtraLeadingSpace(newText, index))
            {
                Text = Text.Remove(index, 1);
                endIndex--;
            }

            // Fix end spacing
            if (NeedsTrailingSpace(newText, endIndex))
            {
                Text = Text.Insert(endIndex, " ");
            }
            else if (HasExtraTrailingSpace(newText, endIndex))
            {
                Text = Text.Remove(endIndex, 1);
            }
        }

        /// <summary>
        /// Determines if a leading space is needed before the inserted text.
        /// </summary>
        private bool NeedsLeadingSpace(string newText, int index) =>
            index > 0 && 
            !newText.StartsWith(' ') && 
            Text[index - 1] != ' ';

        /// <summary>
        /// Determines if there's an extra leading space that should be removed.
        /// </summary>
        private bool HasExtraLeadingSpace(string newText, int index) =>
            index > 0 && 
            newText.StartsWith(' ') && 
            Text[index - 1] == ' ';

        /// <summary>
        /// Determines if a trailing space is needed after the inserted text.
        /// </summary>
        private bool NeedsTrailingSpace(string newText, int endIndex) =>
            endIndex < Text.Length && 
            !newText.EndsWith(' ') && 
            Text[endIndex] != ' ' && 
            !ExpectedEndChars.Contains(Text[endIndex]);

        /// <summary>
        /// Determines if there's an extra trailing space that should be removed.
        /// </summary>
        private bool HasExtraTrailingSpace(string newText, int endIndex) =>
            endIndex < Text.Length && 
            newText.EndsWith(' ') && 
            Text[endIndex] == ' ';

        /// <summary>
        /// Sets the final selection after text insertion.
        /// </summary>
        /// <param name="index">The insertion index.</param>
        private void SetFinalSelection(int index)
        {
            SelectionStart = Math.Min(index + 1, Text.Length);
            UiUtil.SelectWordAtCaret(this);
        }

        /// <summary>
        /// Handles drag enter events to determine drag effects.
        /// </summary>
        private void OnDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.Text) || e.Data.GetDataPresent(DataFormats.UnicodeText))
            {
                e.Effect = ModifierKeys == Keys.Control ? DragDropEffects.Copy : DragDropEffects.Move;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }
        #endregion

        #region Windows Message Processing
        /// <summary>
        /// Processes Windows messages for enhanced mouse and selection handling.
        /// </summary>
        /// <param name="m">The Windows message to process.</param>
        protected override void WndProc(ref Message m)
        {
            try
            {
                switch (m.Msg)
                {
                    case WM_DBLCLICK:
                    case WM_LBUTTONDBLCLK:
                        HandleDoubleClick();
                        return;

                    case WM_LBUTTONDOWN:
                        HandleLeftButtonDown();
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error processing Windows message: {ex.Message}");
            }

            base.WndProc(ref m);
        }

        /// <summary>
        /// Handles double-click events to select the word at the caret.
        /// </summary>
        private void HandleDoubleClick()
        {
            UiUtil.SelectWordAtCaret(this);
        }

        /// <summary>
        /// Handles left button down events to prepare for potential drag operations.
        /// </summary>
        private void HandleLeftButtonDown()
        {
            var timeSinceFocus = (Stopwatch.GetTimestamp() - _gotFocusTicks) / TicksToMilliseconds;
            if (timeSinceFocus > MinimumFocusDelayMs)
            {
                PrepareDragOperation();
            }
        }

        /// <summary>
        /// Prepares the control for a potential drag operation.
        /// </summary>
        private void PrepareDragOperation()
        {
            _dragText = SelectedText ?? string.Empty;
            _dragStartFrom = SelectionStart;
            _dragStartTicks = Stopwatch.GetTimestamp();
        }
        #endregion

        #region IDisposable Implementation
        /// <summary>
        /// Releases all resources used by the SimpleTextBox.
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
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error disposing SimpleTextBox: {ex.Message}");
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
                DragEnter -= OnDragEnter;
                DragDrop -= OnDragDrop;
                MouseDown -= OnMouseDown;
                MouseUp -= OnMouseUp;
                KeyDown -= OnKeyDown;
                GotFocus -= OnGotFocus;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error unsubscribing from events: {ex.Message}");
            }
        }
        #endregion
    }
}
