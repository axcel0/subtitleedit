﻿using System;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Logic.VideoPlayers.MpcHC
{
    internal sealed partial class MessageHandlerWindow : Form
    {
        public event EventHandler OnCopyData;

        public MessageHandlerWindow()
        {
            InitializeComponent();
            Text = Guid.NewGuid().ToString();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WindowsMessageCopyData)
            {
                OnCopyData?.Invoke(m, EventArgs.Empty);
            }
            base.WndProc(ref m);
        }

        protected override void OnLoad(EventArgs e)
        {
            Visible = false;
            ShowInTaskbar = false;
            base.OnLoad(e);
        }

        protected override void SetVisibleCore(bool value)
        {
            // Prevent the window from ever becoming visible
            base.SetVisibleCore(false);
        }
    }
}
