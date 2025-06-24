using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Logic;

namespace Nikse.SubtitleEdit.Controls
{
    /// <summary>
    /// A specialized panel for previewing subtitle cues with timing zones visualization.
    /// </summary>
    public sealed class CuesPreviewView : Panel, IDisposable
    {
        #region Constants

        private const float DefaultFrameRate = 25f;
        private const string DefaultPreviewText = "Subtitle text.";
        private const int DefaultLeftRedZone = 7;
        private const int DefaultLeftGreenZone = 12;
        private const int DefaultRightRedZone = 7;
        private const int DefaultRightGreenZone = 12;
        private const float ShotChangeLineWidth = 3f;
        private const double ReferenceTime = 3000.0;
        private const double DefaultEndTime = 5000.0;
        private const int VisibilityThreshold = 12;
        private const float PixelScalingFactor = 3f;
        private const int TextPadding = 5;
        private const int RectangleMargin = 10;
        private const int Transparency = 153;

        #endregion

        #region Fields

        private float _frameRate = DefaultFrameRate;
        private string _previewText = DefaultPreviewText;
        private bool _showShotChange = true;

        private int _leftGap;
        private int _leftRedZone = DefaultLeftRedZone;
        private int _leftGreenZone = DefaultLeftGreenZone;

        private int _rightGap;
        private int _rightRedZone = DefaultRightRedZone;
        private int _rightGreenZone = DefaultRightGreenZone;

        // Cached values for performance
        private readonly Lazy<Font> _defaultFont = new Lazy<Font>(() => UiUtil.GetDefaultFont());
        private bool _disposed;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the frame rate for timing calculations.
        /// </summary>
        public float FrameRate
        {
            get => _frameRate;
            set 
            { 
                if (Math.Abs(_frameRate - value) > float.Epsilon)
                {
                    _frameRate = value;
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets the preview text displayed in subtitles.
        /// </summary>
        public string PreviewText
        {
            get => _previewText;
            set 
            { 
                if (_previewText != value)
                {
                    _previewText = value ?? DefaultPreviewText;
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to show the shot change line.
        /// </summary>
        public bool ShowShotChange
        {
            get => _showShotChange;
            set 
            { 
                if (_showShotChange != value)
                {
                    _showShotChange = value;
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets the left gap in frames.
        /// </summary>
        public int LeftGap
        {
            get => _leftGap;
            set 
            { 
                if (_leftGap != value)
                {
                    _leftGap = value;
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets the left red zone in frames.
        /// </summary>
        public int LeftRedZone
        {
            get => _leftRedZone;
            set 
            { 
                if (_leftRedZone != value)
                {
                    _leftRedZone = value;
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets the left green zone in frames.
        /// </summary>
        public int LeftGreenZone
        {
            get => _leftGreenZone;
            set 
            { 
                if (_leftGreenZone != value)
                {
                    _leftGreenZone = value;
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets the right gap in frames.
        /// </summary>
        public int RightGap
        {
            get => _rightGap;
            set 
            { 
                if (_rightGap != value)
                {
                    _rightGap = value;
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets the right red zone in frames.
        /// </summary>
        public int RightRedZone
        {
            get => _rightRedZone;
            set 
            { 
                if (_rightRedZone != value)
                {
                    _rightRedZone = value;
                    Invalidate();
                }
            }
        }

        /// <summary>
        /// Gets or sets the right green zone in frames.
        /// </summary>
        public int RightGreenZone
        {
            get => _rightGreenZone;
            set 
            { 
                if (_rightGreenZone != value)
                {
                    _rightGreenZone = value;
                    Invalidate();
                }
            }
        }

        #endregion

        #region Rendering

        /// <summary>
        /// Paints the cues preview with timing zones and subtitle text.
        /// </summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            if (e?.Graphics == null) return;

            base.OnPaint(e);

            try
            {
                e.Graphics.CompositingQuality = CompositingQuality.HighQuality;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

                var width = Size.Width;
                var height = Size.Height;
                var halfWidth = width / 2f;

                using var brush = new SolidBrush(Color.White);

                // Draw layers in optimal order
                DrawBackground(e.Graphics, brush, width, height);
                DrawGreenZones(e.Graphics, brush, halfWidth, height);
                DrawRedZones(e.Graphics, brush, halfWidth, height);
                DrawSubtitleRectangles(e.Graphics, brush, width, height, halfWidth);
                DrawSubtitleText(e.Graphics, brush, width, height, halfWidth);
                
                if (ShowShotChange)
                {
                    DrawShotChange(e.Graphics, brush, halfWidth, height);
                }
            }
            catch (Exception ex)
            {
                // Log error if logging is available
                System.Diagnostics.Debug.WriteLine($"Error in CuesPreviewView.OnPaint: {ex.Message}");
            }
        }

        private void DrawBackground(Graphics graphics, SolidBrush brush, float width, float height)
        {
            brush.Color = Color.Gray;
            graphics.FillRectangle(brush, 0, 0, width, height);
        }

        private void DrawGreenZones(Graphics graphics, SolidBrush brush, float halfWidth, float height)
        {
            brush.Color = Color.Green;
            
            var leftGreenWidth = FramesToPixels(LeftGreenZone);
            var rightGreenWidth = FramesToPixels(RightGreenZone);
            
            graphics.FillRectangle(brush, halfWidth - leftGreenWidth, 0, leftGreenWidth, height);
            graphics.FillRectangle(brush, halfWidth, 0, rightGreenWidth, height);
        }

        private void DrawRedZones(Graphics graphics, SolidBrush brush, float halfWidth, float height)
        {
            brush.Color = Color.Firebrick;
            
            var leftRedWidth = FramesToPixels(LeftRedZone);
            var rightRedWidth = FramesToPixels(RightRedZone);
            
            graphics.FillRectangle(brush, halfWidth - leftRedWidth, 0, leftRedWidth, height);
            graphics.FillRectangle(brush, halfWidth, 0, rightRedWidth, height);
        }

        private void DrawSubtitleRectangles(Graphics graphics, SolidBrush brush, float width, float height, float halfWidth)
        {
            brush.Color = Color.FromArgb(Transparency, 0, 0, 0);
            
            var leftGapWidth = FramesToPixels(LeftGap);
            var rightGapWidth = FramesToPixels(RightGap);
            
            var leftRectWidth = halfWidth - leftGapWidth;
            var rightRectWidth = halfWidth - rightGapWidth;
            
            graphics.FillRectangle(brush, 0, 0, leftRectWidth, height);
            graphics.FillRectangle(brush, halfWidth + rightGapWidth, 0, rightRectWidth, height);
        }

        private void DrawSubtitleText(Graphics graphics, SolidBrush brush, float width, float height, float halfWidth)
        {
            brush.Color = Color.White;
            
            if (LeftGap <= VisibilityThreshold)
            {
                var leftRectWidth = halfWidth - FramesToPixels(LeftGap) - RectangleMargin;
                var leftRect = new RectangleF(TextPadding, TextPadding, leftRectWidth, height - RectangleMargin);
                var leftLabel = GetSubtitleLabel(1000, GetLeftOutCue());
                graphics.DrawString(leftLabel, _defaultFont.Value, brush, leftRect);
            }
            
            if (RightGap <= VisibilityThreshold)
            {
                var rightStartX = halfWidth + FramesToPixels(RightGap) + TextPadding;
                var rightRectWidth = halfWidth - FramesToPixels(RightGap) - RectangleMargin;
                var rightRect = new RectangleF(rightStartX, TextPadding, rightRectWidth, height - RectangleMargin);
                var rightLabel = GetSubtitleLabel(GetRightInCue(), DefaultEndTime);
                graphics.DrawString(rightLabel, _defaultFont.Value, brush, rightRect);
            }
        }

        private void DrawShotChange(Graphics graphics, SolidBrush brush, float halfWidth, float height)
        {
            brush.Color = Color.PaleGreen;
            var shotChangeX = halfWidth - (ShotChangeLineWidth / 2f);
            graphics.FillRectangle(brush, shotChangeX, 0, ShotChangeLineWidth, height);
        }

        #endregion

        #region Calculations

        /// <summary>
        /// Converts frames to pixels based on the current frame rate and control width.
        /// </summary>
        private float FramesToPixels(float frames)
        {
            if (Math.Abs(FrameRate) < float.Epsilon) return 0f;
            return (Size.Width / (FrameRate * PixelScalingFactor)) * frames;
        }

        /// <summary>
        /// Calculates the left out cue time in milliseconds.
        /// </summary>
        private double GetLeftOutCue()
        {
            if (Math.Abs(FrameRate) < float.Epsilon) return ReferenceTime;
            return ReferenceTime - (LeftGap * (1000.0 / FrameRate));
        }

        /// <summary>
        /// Calculates the right in cue time in milliseconds.
        /// </summary>
        private double GetRightInCue()
        {
            if (Math.Abs(FrameRate) < float.Epsilon) return ReferenceTime;
            return ReferenceTime + (RightGap * (1000.0 / FrameRate));
        }

        /// <summary>
        /// Creates a subtitle label with timecode and preview text.
        /// </summary>
        private string GetSubtitleLabel(double start, double end)
        {
            try
            {
                var timeCodeStart = new TimeCode(start);
                var timeCodeEnd = new TimeCode(end);
                return $"{timeCodeStart} --> {timeCodeEnd}{Environment.NewLine}{PreviewText}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating subtitle label: {ex.Message}");
                return PreviewText;
            }
        }

        #endregion

        #region Dispose Pattern

        /// <summary>
        /// Releases all resources used by the CuesPreviewView.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                if (_defaultFont.IsValueCreated)
                {
                    _defaultFont.Value?.Dispose();
                }
                _disposed = true;
            }
            
            base.Dispose(disposing);
        }

        #endregion
    }
}