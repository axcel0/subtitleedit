namespace Nikse.SubtitleEdit.Controls
{
    /// <summary>
    /// Designer class for NikseComboBoxPopUp.
    /// </summary>
    sealed partial class NikseComboBoxPopUp
    {
        #region Fields

        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        #endregion

        #region Dispose Pattern

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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            SuspendLayout();
            
            // 
            // NikseComboBoxPopUp
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(800, 450);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            Name = "NikseComboBoxPopUp";
            ShowIcon = false;
            ShowInTaskbar = false;
            Text = "NikseComboBoxPopUp";
            
            ResumeLayout(false);
        }

        #endregion
    }
}