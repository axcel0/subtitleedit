using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Controls
{
    sealed partial class AudioVisualizer
    {
        #region Designer Fields

        /// <summary>
        /// Required designer variable.
        /// </summary>
        private IContainer components = null;

        #endregion

        #region Disposal

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                components?.Dispose();
            }
            base.Dispose(disposing);
        }

        #endregion

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            SuspendLayout();
            InitializeControlProperties();
            AttachEventHandlers();
            ResumeLayout(false);
        }

        /// <summary>
        /// Initialize the basic control properties
        /// </summary>
        private void InitializeControlProperties()
        {
            AutoScaleDimensions = new SizeF(6F, 13F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.Black;
            Name = "AudioVisualizer";
            Size = new Size(682, 355);
        }

        /// <summary>
        /// Attach all event handlers for the control
        /// </summary>
        private void AttachEventHandlers()
        {
            // Core events
            Paint += WaveformPaint;
            KeyDown += WaveformKeyDown;
            
            // Mouse events
            MouseClick += WaveformMouseClick;
            MouseDoubleClick += WaveformMouseDoubleClick;
            MouseDown += WaveformMouseDown;
            MouseEnter += WaveformMouseEnter;
            MouseLeave += WaveformMouseLeave;
            MouseMove += WaveformMouseMove;
            MouseUp += WaveformMouseUp;
        }

        #endregion
    }
}
