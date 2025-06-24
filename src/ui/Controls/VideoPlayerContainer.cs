using Nikse.SubtitleEdit.Core.Common;
using Nikse.SubtitleEdit.Core.ContainerFormats.Matroska;
using Nikse.SubtitleEdit.Core.Settings;
using Nikse.SubtitleEdit.Core.SubtitleFormats;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.VideoPlayers;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Controls
{
    /// <summary>
    /// A comprehensive video player container that provides video playback controls,
    /// subtitle overlay, and advanced player management functionality.
    /// </summary>
    public sealed class VideoPlayerContainer : Panel, IDisposable
    {
        #region Constants
        // Control panel constants
        private const int DefaultControlsHeight = 53;
        
        // Button position constants
        private const int PLAY_BUTTON_X = 22;
        private const int BUTTON_Y_OFFSET = 13;
        private static readonly Size BUTTON_SIZE = new Size(29, 29);
        
        private const int STOP_BUTTON_X = 52;
        private const int STOP_BUTTON_Y = 17;
        private static readonly Size SMALL_BUTTON_SIZE = new Size(20, 20);
        
        private const int FULLSCREEN_BUTTON_X = 95;

        // Progress bar constants
        private const int PROGRESS_BAR_X = 43;
        private const int PROGRESS_BAR_BACKGROUND_Y = 1;
        private const int PROGRESS_BAR_Y = 5;
        private const int PROGRESS_BAR_WIDTH = 531;
        private const int PROGRESS_BAR_BACKGROUND_HEIGHT = 12;
        private const int PROGRESS_BAR_HEIGHT = 4;
        private const int PROGRESS_BAR_DEFAULT_WIDTH = 318;

        // Volume control constants
        private const int MUTE_BUTTON_X = 75;
        private const int MUTE_BUTTON_Y = 18;
        private static readonly Size MUTE_BUTTON_SIZE = new Size(19, 19);
        private const int VOLUME_BAR_BACKGROUND_X = 111;
        private const int VOLUME_BAR_BACKGROUND_Y = 22;
        private static readonly Size VOLUME_BAR_BACKGROUND_SIZE = new Size(82, 13);
        private const int VOLUME_BAR_X = 120;
        private const int VOLUME_BAR_Y = 26;
        private static readonly Size VOLUME_BAR_SIZE = new Size(48, 4);

        // Seek control constants
        private const int REVERSE_BUTTON_X = 28;
        private const int REVERSE_BUTTON_Y = 3;
        private static readonly Size REVERSE_BUTTON_SIZE = new Size(16, 8);
        private const int FAST_FORWARD_BUTTON_X = 571;
        private const int FAST_FORWARD_BUTTON_Y = 1;
        private static readonly Size FAST_FORWARD_BUTTON_SIZE = new Size(17, 13);

        // Label constants
        private const int VOLUME_LABEL_X = 120;
        private const int VOLUME_LABEL_Y = 16;
        private const int TIMECODE_LABEL_X = 280;
        private const int TIMECODE_LABEL_Y = 28;
        private const int PLAYER_NAME_LABEL_X = 282;
        private const int PLAYER_NAME_LABEL_Y = 17;
        private const int SMALL_FONT_SIZE = 6;
        private const int NORMAL_FONT_SIZE = 8;

        // Windows message constants
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        #endregion

        #region Nested Classes
        /// <summary>
        /// A panel with optimized double buffering for smooth video rendering.
        /// </summary>
        public sealed class DoubleBufferedPanel : Panel
        {
            /// <summary>
            /// Initializes a new instance of the DoubleBufferedPanel.
            /// </summary>
            public DoubleBufferedPanel()
            {
                InitializeDoubleBuffering();
            }

            /// <summary>
            /// Configures optimal control styles for video rendering.
            /// </summary>
            private void InitializeDoubleBuffering()
            {
                DoubleBuffered = true;
                SetStyle(ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.UserPaint, true);
                UpdateStyles();
            }
        }

        /// <summary>
        /// A read-only RichTextBox optimized for subtitle display with disabled user interaction.
        /// </summary>
        public sealed class RichTextBoxViewOnly : RichTextBox
        {
            /// <summary>
            /// Initializes a new instance of the RichTextBoxViewOnly.
            /// </summary>
            public RichTextBoxViewOnly()
            {
                InitializeViewOnlySettings();
                SetupEventHandlers();
            }

            /// <summary>
            /// Configures the text box for view-only operation.
            /// </summary>
            private void InitializeViewOnlySettings()
            {
                ReadOnly = true;
                BorderStyle = BorderStyle.None;
                TabStop = false;
                SetStyle(ControlStyles.Selectable, false);
                SetStyle(ControlStyles.UserMouse, true);
                ScrollBars = RichTextBoxScrollBars.None;
                Margin = new Padding(0);
            }

            /// <summary>
            /// Sets up event handlers for the view-only text box.
            /// </summary>
            private void SetupEventHandlers()
            {
                MouseEnter += (sender, e) => Cursor = Cursors.Default;
            }

            /// <summary>
            /// Processes Windows messages, filtering out right-click events.
            /// </summary>
            /// <param name="m">The Windows message to process.</param>
            protected override void WndProc(ref Message m)
            {
                // Block right mouse button events
                if (m.Msg == WM_RBUTTONDOWN || m.Msg == WM_RBUTTONUP)
                {
                    return;
                }

                base.WndProc(ref m);
            }
        }
        #endregion

        #region Events
        /// <summary>
        /// Occurs when a control button is clicked.
        /// </summary>
        public event EventHandler OnButtonClicked;
        
        /// <summary>
        /// Occurs when the empty player area is clicked.
        /// </summary>
        public event EventHandler OnEmptyPlayerClicked;
        
        /// <summary>
        /// Occurs when the player area is clicked.
        /// </summary>
        public event EventHandler OnPlayerClicked;
        #endregion

        #region Public Properties
        /// <summary>
        /// Gets the main player panel with double buffering enabled.
        /// </summary>
        public DoubleBufferedPanel PanelPlayer { get; private set; }
        
        /// <summary>
        /// Gets the text box used for subtitle display.
        /// </summary>
        public RichTextBoxViewOnly TextBox { get; private set; }
        
        /// <summary>
        /// Gets or sets the font size factor for subtitle text.
        /// </summary>
        public float FontSizeFactor { get; set; }
        
        /// <summary>
        /// Gets or sets the video width in pixels.
        /// </summary>
        public int VideoWidth { get; set; }
        
        /// <summary>
        /// Gets or sets the video height in pixels.
        /// </summary>
        public int VideoHeight { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the player was played with custom speed.
        /// </summary>
        public bool PlayedWithCustomSpeed { get; set; }
        
        /// <summary>
        /// Gets or sets the height of the control panel.
        /// </summary>
        public int ControlsHeight { get; set; } = DefaultControlsHeight;
        
        /// <summary>
        /// Gets or sets the available Matroska chapters.
        /// </summary>
        public MatroskaChapter[] Chapters { get; set; }
        #endregion

        #region Private Fields
        private Panel _panelSubtitle;
        private string _subtitleText = string.Empty;
        private VideoPlayer _videoPlayer;
        private bool _isMuted;
        private readonly bool _loading;
        private double? _muteOldVolume;
        private readonly System.ComponentModel.ComponentResourceManager _resources;
        private int _subtitlesHeight = GetSubtitlesHeight();
        private readonly Color _backgroundColor = DefaultBackgroundColor;
        private Panel _panelControls;
        private bool _disposed;

        // Bitmap resources for UI elements
        private Bitmap _bitmapFullscreen;
        private Bitmap _bitmapFullscreenDown;
        private Bitmap _bitmapFullscreenOver;
        private Bitmap _bitmapNoFullscreen;
        private Bitmap _bitmapNoFullscreenDown;
        private Bitmap _bitmapNoFullscreenOver;
        private Bitmap _playerIcon;

        // UI Controls
        private PictureBox _pictureBoxBackground;
        private PictureBox _pictureBoxReverse;
        private PictureBox _pictureBoxReverseOver;
        private PictureBox _pictureBoxReverseDown;
        private PictureBox _pictureBoxFastForward;
        private PictureBox _pictureBoxFastForwardOver;
        private PictureBox _pictureBoxFastForwardDown;
        private PictureBox _pictureBoxPlay;
        private PictureBox _pictureBoxPlayOver;
        private PictureBox _pictureBoxPlayDown;
        
        private readonly PictureBox _pictureBoxPause = new PictureBox();
        private readonly PictureBox _pictureBoxPauseOver = new PictureBox();
        private readonly PictureBox _pictureBoxPauseDown = new PictureBox();
        private readonly PictureBox _pictureBoxStop = new PictureBox();
        private readonly PictureBox _pictureBoxStopOver = new PictureBox();
        private readonly PictureBox _pictureBoxStopDown = new PictureBox();
        private readonly PictureBox _pictureBoxFullscreen = new PictureBox();
        private readonly PictureBox _pictureBoxFullscreenOver = new PictureBox();
        private readonly PictureBox _pictureBoxFullscreenDown = new PictureBox();
        private readonly PictureBox _pictureBoxMute = new PictureBox();
        private readonly PictureBox _pictureBoxMuteOver = new PictureBox();
        private readonly PictureBox _pictureBoxMuteDown = new PictureBox();
        private readonly PictureBox _pictureBoxProgressbarBackground = new PictureBox();
        private readonly PictureBox _pictureBoxProgressBar = new PictureBox();
        private readonly PictureBox _pictureBoxVolumeBarBackground = new PictureBox();
        private readonly PictureBox _pictureBoxVolumeBar = new PictureBox();
        
        // Labels and UI text elements
        private readonly NikseLabel _labelTimeCode = new NikseLabel();
        private readonly NikseLabel _labelVideoPlayerName = new NikseLabel();
        private readonly NikseLabel _labelVolume = new NikseLabel();
        
        // Tooltip management
        private readonly ToolTip _currentPositionToolTip = new ToolTip();
        private int _lastCurrentPositionToolTipX;
        private int _lastCurrentPositionToolTipY;
        #endregion

        #region Properties with Enhanced Logic
        /// <summary>
        /// Gets or sets the video player instance.
        /// </summary>
        public VideoPlayer VideoPlayer
        {
            get => _videoPlayer;
            set
            {
                _videoPlayer = value;
                ConfigureVideoPlayer();
            }
        }

        /// <summary>
        /// Gets or sets the text direction for subtitle display.
        /// </summary>
        public RightToLeft TextRightToLeft
        {
            get => TextBox.RightToLeft;
            set
            {
                if (TextBox.RightToLeft != value)
                {
                    TextBox.RightToLeft = value;
                    TextBox.SelectAll();
                    TextBox.SelectionAlignment = HorizontalAlignment.Center;
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the stop button is visible.
        /// </summary>
        public bool ShowStopButton
        {
            get => _pictureBoxStop.Visible || _pictureBoxStopOver.Visible || _pictureBoxStopDown.Visible;
            set
            {
                if (value)
                {
                    _pictureBoxStop.Visible = true;
                    _pictureBoxStop.BringToFront();
                }
                else
                {
                    HideAllStopImages();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the mute button is visible.
        /// </summary>
        public bool ShowMuteButton
        {
            get => _pictureBoxMute.Visible || _pictureBoxMuteOver.Visible || _pictureBoxMuteDown.Visible;
            set
            {
                if (value)
                {
                    _pictureBoxMute.Visible = true;
                    _pictureBoxMute.BringToFront();
                }
                else
                {
                    HideAllMuteImages();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether the fullscreen button is visible.
        /// </summary>
        public bool ShowFullscreenButton
        {
            get => _pictureBoxFullscreen.Visible || _pictureBoxFullscreenOver.Visible || _pictureBoxFullscreenDown.Visible;
            set
            {
                if (value)
                {
                    _pictureBoxFullscreen.Visible = true;
                    _pictureBoxFullscreen.BringToFront();
                }
                else
                {
                    HideAllFullscreenImages();
                }
            }
        }
        #endregion

        /// <summary>
        /// Hides all mute button images.
        /// </summary>
        private void HideAllMuteImages()
        {
            _pictureBoxMute.Visible = false;
            _pictureBoxMuteOver.Visible = false;
            _pictureBoxMuteDown.Visible = false;
        }

        /// <summary>
        /// Hides all fullscreen button images.
        /// </summary>
        private void HideAllFullscreenImages()
        {
            _pictureBoxFullscreen.Visible = false;
            _pictureBoxFullscreenOver.Visible = false;
            _pictureBoxFullscreenDown.Visible = false;
        }

        /// <summary>
        /// Initializes progress bar controls.
        /// </summary>
        private void InitializeProgressBarControls()
        {
            // Progress bar background
            _pictureBoxProgressbarBackground.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            _pictureBoxProgressbarBackground.BackColor = Color.Transparent;
            _pictureBoxProgressbarBackground.Image = (Image)_resources.GetObject("pictureBoxProgressbarBackground.Image");
            _pictureBoxProgressbarBackground.Location = new Point(PROGRESS_BAR_X, PROGRESS_BAR_BACKGROUND_Y);
            _pictureBoxProgressbarBackground.Margin = new Padding(0);
            _pictureBoxProgressbarBackground.Name = "_pictureBoxProgressbarBackground";
            _pictureBoxProgressbarBackground.Size = new Size(PROGRESS_BAR_WIDTH, PROGRESS_BAR_BACKGROUND_HEIGHT);
            _pictureBoxProgressbarBackground.SizeMode = PictureBoxSizeMode.StretchImage;
            _pictureBoxProgressbarBackground.TabStop = false;
            _pictureBoxProgressbarBackground.Paint += PictureBoxProgressbarBackgroundPaint;
            _pictureBoxProgressbarBackground.MouseDown += PictureBoxProgressbarBackgroundMouseDown;
            _pictureBoxProgressbarBackground.MouseLeave += PictureBoxProgressbarBackgroundMouseLeave;
            _pictureBoxProgressbarBackground.MouseMove += PictureBoxProgressbarBackgroundMouseMove;
            _panelControls.Controls.Add(_pictureBoxProgressbarBackground);

            // Progress bar
            _pictureBoxProgressBar.Image = (Image)_resources.GetObject("pictureBoxProgressBar.Image");
            _pictureBoxProgressBar.Location = new Point(PROGRESS_BAR_X + 4, PROGRESS_BAR_Y);
            _pictureBoxProgressBar.Name = "_pictureBoxProgressBar";
            _pictureBoxProgressBar.Size = new Size(PROGRESS_BAR_DEFAULT_WIDTH, PROGRESS_BAR_HEIGHT);
            _pictureBoxProgressBar.SizeMode = PictureBoxSizeMode.StretchImage;
            _pictureBoxProgressBar.TabStop = false;
            _pictureBoxProgressBar.Paint += PictureBoxProgressBarPaint;
            _pictureBoxProgressBar.MouseDown += PictureBoxProgressBarMouseDown;
            _pictureBoxProgressBar.MouseLeave += PictureBoxProgressBarMouseLeave;
            _pictureBoxProgressBar.MouseMove += PictureBoxProgressBarMouseMove;
            _panelControls.Controls.Add(_pictureBoxProgressBar);
            _pictureBoxProgressBar.BringToFront();
        }

        /// <summary>
        /// Initializes volume controls.
        /// </summary>
        private void InitializeVolumeControls()
        {
            // Mute controls
            _pictureBoxMute = CreateControlPictureBox("pictureBoxMute.Image", "pictureBoxMute", 
                new Point(MUTE_BUTTON_X, MUTE_BUTTON_Y), MUTE_BUTTON_SIZE);
            _pictureBoxMute.MouseEnter += PictureBoxMuteMouseEnter;

            _pictureBoxMuteDown = CreateControlPictureBox("pictureBoxMuteDown.Image", "pictureBoxMuteDown", 
                new Point(MUTE_BUTTON_X, MUTE_BUTTON_Y), MUTE_BUTTON_SIZE);
            _pictureBoxMuteDown.Click += PictureBoxMuteDownClick;

            _pictureBoxMuteOver = CreateControlPictureBox("pictureBoxMuteOver.Image", "pictureBoxMuteOver", 
                new Point(MUTE_BUTTON_X, MUTE_BUTTON_Y), MUTE_BUTTON_SIZE);
            _pictureBoxMuteOver.MouseLeave += PictureBoxMuteOverMouseLeave;
            _pictureBoxMuteOver.MouseDown += PictureBoxMuteOverMouseDown;
            _pictureBoxMuteOver.MouseUp += PictureBoxMuteOverMouseUp;

            // Volume bars
            _pictureBoxVolumeBarBackground = CreateControlPictureBox("pictureBoxVolumeBarBackground.Image", 
                "pictureBoxVolumeBarBackground", new Point(VOLUME_BAR_BACKGROUND_X, VOLUME_BAR_BACKGROUND_Y), 
                VOLUME_BAR_BACKGROUND_SIZE, PictureBoxSizeMode.AutoSize);
            _pictureBoxVolumeBarBackground.MouseDown += PictureBoxVolumeBarBackgroundMouseDown;

            _pictureBoxVolumeBar = CreateControlPictureBox("pictureBoxVolumeBar.Image", "pictureBoxVolumeBar", 
                new Point(VOLUME_BAR_X, VOLUME_BAR_Y), VOLUME_BAR_SIZE, PictureBoxSizeMode.StretchImage);
            _pictureBoxVolumeBar.MouseDown += PictureBoxVolumeBarMouseDown;
            _pictureBoxVolumeBar.BringToFront();
        }

        /// <summary>
        /// Initializes seek controls (reverse and fast forward).
        /// </summary>
        private void InitializeSeekControls()
        {
            // Reverse controls
            _pictureBoxReverse = CreateControlPictureBox("pictureBoxReverse.Image", "pictureBoxReverse", 
                new Point(REVERSE_BUTTON_X, REVERSE_BUTTON_Y), REVERSE_BUTTON_SIZE);
            _pictureBoxReverse.MouseEnter += PictureBoxReverseMouseEnter;

            _pictureBoxReverseOver = CreateControlPictureBox("pictureBoxReverseMouseOver.Image", "pictureBoxReverseOver", 
                new Point(REVERSE_BUTTON_X, REVERSE_BUTTON_Y), REVERSE_BUTTON_SIZE);
            _pictureBoxReverseOver.MouseLeave += PictureBoxReverseOverMouseLeave;
            _pictureBoxReverseOver.MouseDown += PictureBoxReverseOverMouseDown;
            _pictureBoxReverseOver.MouseUp += PictureBoxReverseOverMouseUp;

            _pictureBoxReverseDown = CreateControlPictureBox("pictureBoxReverseMouseDown.Image", "pictureBoxReverseDown", 
                new Point(REVERSE_BUTTON_X, REVERSE_BUTTON_Y), REVERSE_BUTTON_SIZE);

            // Fast forward controls
            _pictureBoxFastForward = CreateControlPictureBox("pictureBoxFastForward.Image", "pictureBoxFastForward", 
                new Point(FAST_FORWARD_BUTTON_X, FAST_FORWARD_BUTTON_Y), FAST_FORWARD_BUTTON_SIZE);
            _pictureBoxFastForward.MouseEnter += PictureBoxFastForwardMouseEnter;

            _pictureBoxFastForwardOver = CreateControlPictureBox("pictureBoxFastForwardMouseOver.Image", "pictureBoxFastForwardOver", 
                new Point(FAST_FORWARD_BUTTON_X, FAST_FORWARD_BUTTON_Y), FAST_FORWARD_BUTTON_SIZE);
            _pictureBoxFastForwardOver.MouseLeave += PictureBoxFastForwardOverMouseLeave;
            _pictureBoxFastForwardOver.MouseDown += PictureBoxFastForwardOverMouseDown;
            _pictureBoxFastForwardOver.MouseUp += PictureBoxFastForwardOverMouseUp;

            _pictureBoxFastForwardDown = CreateControlPictureBox("pictureBoxFastForwardMouseDown.Image", "pictureBoxFastForwardDown", 
                new Point(FAST_FORWARD_BUTTON_X, FAST_FORWARD_BUTTON_Y), FAST_FORWARD_BUTTON_SIZE);
        }

        /// <summary>
        /// Initializes control panel labels.
        /// </summary>
        private void InitializeLabels()
        {
            // Volume label
            _labelVolume.Location = new Point(VOLUME_LABEL_X, VOLUME_LABEL_Y);
            _labelVolume.ForeColor = Color.WhiteSmoke;
            _labelVolume.BackColor = Color.FromArgb(67, 75, 93);
            _labelVolume.AutoSize = true;
            _labelVolume.Font = new Font(_labelTimeCode.Font.FontFamily, SMALL_FONT_SIZE);
            _panelControls.Controls.Add(_labelVolume);

            // Time code label
            _labelTimeCode.Location = new Point(TIMECODE_LABEL_X, TIMECODE_LABEL_Y);
            _labelTimeCode.ForeColor = Color.WhiteSmoke;
            _labelTimeCode.Font = new Font(_labelTimeCode.Font.FontFamily, NORMAL_FONT_SIZE, FontStyle.Bold);
            _labelTimeCode.AutoSize = true;
            _panelControls.Controls.Add(_labelTimeCode);

            // Video player name label
            _labelVideoPlayerName.Location = new Point(PLAYER_NAME_LABEL_X, PLAYER_NAME_LABEL_Y);
            _labelVideoPlayerName.ForeColor = Color.WhiteSmoke;
            _labelVideoPlayerName.BackColor = Color.FromArgb(67, 75, 93);
            _labelVideoPlayerName.AutoSize = true;
            _labelVideoPlayerName.Font = new Font(_labelTimeCode.Font.FontFamily, SMALL_FONT_SIZE);
            _panelControls.Controls.Add(_labelVideoPlayerName);

            // Set background colors based on actual background
            SetLabelBackgroundColors();
        }

        /// <summary>
        /// Sets control ordering and panel background color.
        /// </summary>
        private void SetControlOrder()
        {
            _pictureBoxBackground.SendToBack();
            
            // Bring important controls to front
            _pictureBoxFastForwardDown.BringToFront();
            _pictureBoxFastForwardOver.BringToFront();
            _pictureBoxFastForward.BringToFront();
            _pictureBoxPlay.BringToFront();

            _panelControls.BackColor = _backgroundColor;
            
            _pictureBoxPlayDown.BringToFront();
            _pictureBoxPlayOver.BringToFront();
            _pictureBoxPlay.BringToFront();
            _labelTimeCode.BringToFront();
            _labelVolume.BringToFront();
        }
        #endregion

        #region Constructor and Initialization
        /// <summary>
        /// Initializes a new instance of the VideoPlayerContainer.
        /// </summary>
        public VideoPlayerContainer()
        {
            InitializeContainer();
        }

        /// <summary>
        /// Performs the main initialization of the video player container.
        /// </summary>
        private void InitializeContainer()
        {
            _loading = true;
            
            try
            {
                InitializeDefaultSettings();
                InitializeComponents();
                SetupEventHandlers();
                PerformInitialLayout();
                ConfigureLinuxSpecificSettings();
                CompleteInitialization();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing VideoPlayerContainer: {ex.Message}");
            }
            finally
            {
                _loading = false;
            }
        }

        /// <summary>
        /// Initializes default settings and properties.
        /// </summary>
        private void InitializeDefaultSettings()
        {
            Chapters = Array.Empty<MatroskaChapter>();
            FontSizeFactor = 1.0F;
            BorderStyle = BorderStyle.None;
            _resources = new System.ComponentModel.ComponentResourceManager(typeof(VideoPlayerContainer));
            _labelVolume.Text = $"{Configuration.Settings.General.VideoPlayerDefaultVolume}%";
            BackColor = _backgroundColor;
        }

        /// <summary>
        /// Creates and adds the main components to the container.
        /// </summary>
        private void InitializeComponents()
        {
            Controls.Add(MakePlayerPanel());
            Controls.Add(MakeSubtitlesPanel());
            Controls.Add(MakeControlsPanel());
            _panelControls.BringToFront();
            _pictureBoxProgressBar.Width = 0;
        }

        /// <summary>
        /// Sets up all event handlers for the container.
        /// </summary>
        private void SetupEventHandlers()
        {
            Resize += VideoPlayerContainerResize;
            PanelPlayer.MouseDown += PanelPlayerMouseDown;
            _labelTimeCode.Click += LabelTimeCodeClick;
            PanelPlayer.Paint += PanelPlayerPaint;
        }

        /// <summary>
        /// Performs the initial layout and control setup.
        /// </summary>
        private void PerformInitialLayout()
        {
            ShowAllControls();
            
            // Initialize fast forward states
            PictureBoxFastForwardMouseEnter(null, null);
            PictureBoxFastForwardOverMouseLeave(null, null);

            // Bring volume controls to front
            _pictureBoxVolumeBarBackground.BringToFront();
            _pictureBoxVolumeBar.BringToFront();
            _labelVolume.BringToFront();
        }

        /// <summary>
        /// Configures Linux-specific settings with delayed initialization.
        /// </summary>
        private void ConfigureLinuxSpecificSettings()
        {
            if (Configuration.IsRunningOnLinux)
            {
                TaskDelayHelper.RunDelayed(TimeSpan.FromMilliseconds(1500), PerformDelayedLinuxSetup);
            }
        }

        /// <summary>
        /// Performs delayed setup operations for Linux compatibility.
        /// </summary>
        private void PerformDelayedLinuxSetup()
        {
            try
            {
                if (string.IsNullOrEmpty(_labelVideoPlayerName.Text))
                {
                    _labelVideoPlayerName.Text = "...";
                }
                
                FontSizeFactor = 1.0F;
                SetSubtitleFont();
                _labelTimeCode.Text = $"{new TimeCode().ToDisplayString()} / ?";
                ShowAllControls();
                VideoPlayerContainerResize(this, null);
                ShowAllControls();
                Invalidate();
                Refresh();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in delayed Linux setup: {ex.Message}");
            }
        }

        /// <summary>
        /// Completes the initialization process.
        /// </summary>
        private void CompleteInitialization()
        {
            VideoPlayerContainerResize(this, null);
            ShowPlayerLogo();
        }
        #endregion

        #region Player Logo and Painting
        /// <summary>
        /// Shows the video player logo when no video is loaded.
        /// </summary>
        public void ShowPlayerLogo()
        {
            try
            {
                var iconPath = GetPlayerIconPath();
                LoadPlayerIcon(iconPath);
                
                if (_videoPlayer == null)
                {
                    PanelPlayer.Visible = true;
                    PanelPlayer.BringToFront();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing player logo: {ex.Message}");
                CreateFallbackIcon();
            }
        }

        /// <summary>
        /// Gets the path to the player icon based on the current video player setting.
        /// </summary>
        /// <returns>The full path to the player icon file.</returns>
        private static string GetPlayerIconPath()
        {
            var playerName = Configuration.Settings.General.VideoPlayer.ToLowerInvariant();
            return Path.Combine(Configuration.BaseDirectory, "icons", $"{playerName}.png");
        }

        /// <summary>
        /// Loads the player icon from the specified path.
        /// </summary>
        /// <param name="iconPath">The path to the icon file.</param>
        private void LoadPlayerIcon(string iconPath)
        {
            if (File.Exists(iconPath))
            {
                // Dispose previous icon to prevent memory leaks
                _playerIcon?.Dispose();
                _playerIcon = new Bitmap(iconPath);
            }
            else
            {
                CreateFallbackIcon();
            }
        }

        /// <summary>
        /// Creates a fallback icon when the player-specific icon is not available.
        /// </summary>
        private void CreateFallbackIcon()
        {
            _playerIcon?.Dispose();
            _playerIcon = new Bitmap(1, 1);
        }

        /// <summary>
        /// Handles painting the player panel, including the player logo when no video is active.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The paint event arguments.</param>
        private void PanelPlayerPaint(object sender, PaintEventArgs e)
        {
            if (_videoPlayer != null || _playerIcon == null)
            {
                return;
            }

            try
            {
                DrawPlayerLogo(e.Graphics);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error painting player panel: {ex.Message}");
            }
        }

        /// <summary>
        /// Draws the player logo on the specified graphics surface.
        /// </summary>
        /// <param name="graphics">The graphics object to draw on.</param>
        private void DrawPlayerLogo(Graphics graphics)
        {
            const float LogoOpacity = 0.4f;
            
            var logoSize = CalculateLogoSize();
            var logoPosition = CalculateLogoPosition(logoSize);
            
            using var attributes = CreateTransparencyAttributes(LogoOpacity);
            var destRect = new Rectangle(logoPosition.X, logoPosition.Y, logoSize.Width, logoSize.Height);
            
            graphics.DrawImage(_playerIcon, destRect, 0, 0, _playerIcon.Width, _playerIcon.Height, 
                GraphicsUnit.Pixel, attributes);
        }

        /// <summary>
        /// Calculates the optimal size for the logo based on the panel dimensions.
        /// </summary>
        /// <returns>The calculated logo size.</returns>
        private Size CalculateLogoSize()
        {
            var width = _playerIcon.Width;
            var height = _playerIcon.Height;

            // Adjust size if logo is taller than the panel
            if (PanelPlayer.Height < height)
            {
                width -= height - PanelPlayer.Height;
                height = PanelPlayer.Height;
            }

            return new Size(width, height);
        }

        /// <summary>
        /// Calculates the centered position for the logo within the panel.
        /// </summary>
        /// <param name="logoSize">The size of the logo.</param>
        /// <returns>The calculated logo position.</returns>
        private Point CalculateLogoPosition(Size logoSize)
        {
            var left = (PanelPlayer.Width / 2) - (logoSize.Width / 2);
            var top = (PanelPlayer.Height / 2) - (logoSize.Height / 2);
            return new Point(left, top);
        }

        /// <summary>
        /// Creates image attributes for drawing with transparency.
        /// </summary>
        /// <param name="opacity">The opacity level (0.0 to 1.0).</param>
        /// <returns>ImageAttributes configured for transparency.</returns>
        private static ImageAttributes CreateTransparencyAttributes(float opacity)
        {
            var matrix = new ColorMatrix();
            matrix.Matrix33 = opacity; // Set the alpha channel (transparency)

            var attributes = new ImageAttributes();
            attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
            return attributes;
        }
        #endregion

            var offset = 30;
            if (PanelPlayer.Height <= top + offset + h)
            {
                offset -= (top + offset + h) - PanelPlayer.Height;
                if (offset < 0)
                {
                    offset = 0;
                }
            }

            // Draw the image with the modified opacity
            e.Graphics.DrawImage(img,
                                 new Rectangle(left, top + offset, w, h),
                                 0, 0, img.Width, img.Height,
                                 GraphicsUnit.Pixel, attributes);
        }

        private bool _showDuration = true;
        private void LabelTimeCodeClick(object sender, EventArgs e)
        {
            _showDuration = !_showDuration;
            RefreshProgressBar();
        }

        private void ShowAllControls()
        {
            HideAllPlayImages();
            HideAllPauseImages();
            _pictureBoxPlay.Visible = true;
            _pictureBoxPlay.BringToFront();

            HideAllStopImages();
            _pictureBoxStop.Visible = true;
            _pictureBoxStop.BringToFront();

            HideAllStopImages();
            _pictureBoxStop.Visible = true;
            _pictureBoxStop.BringToFront();

            HideAllFullscreenImages();
            _pictureBoxFullscreen.Visible = true;
            _pictureBoxFullscreen.BringToFront();

            HideAllMuteImages();
            _pictureBoxMute.Visible = true;
            _pictureBoxMute.BringToFront();

            HideAllReverseImages();
            _pictureBoxReverse.Visible = true;
            _pictureBoxReverse.BringToFront();

            HideAllFastForwardImages();
            _pictureBoxFastForward.Visible = true;
            _pictureBoxFastForward.BringToFront();

            _pictureBoxProgressbarBackground.Visible = true;
            _pictureBoxProgressbarBackground.BringToFront();
            _pictureBoxProgressBar.Visible = true;
            _pictureBoxProgressBar.BringToFront();

            _labelTimeCode.Visible = true;
            _labelTimeCode.BringToFront();
            _labelVolume.BringToFront();
        }

        public void EnableMouseWheelStep()
        {
            AddMouseWheelEvent(this);
        }

        public void SetPlayerName(string s)
        {
            _labelVideoPlayerName.Text = s;
            _labelVideoPlayerName.Left = Width - _labelVideoPlayerName.Width - 3;
        }

        public void HidePlayerName()
        {
            _labelVideoPlayerName.Visible = false;
        }

        public void UpdatePlayerName()
        {
            if (_videoPlayer != null)
            {
                SetPlayerName(_videoPlayer.PlayerName);
            }
        }

        public void ResetTimeLabel()
        {
            _labelTimeCode.Text = string.Empty;
        }

        private void AddMouseWheelEvent(Control control)
        {
            control.MouseWheel += ControlMouseWheel;
            foreach (Control ctrl in control.Controls)
            {
                AddMouseWheelEvent(ctrl);
            }
        }

        private void ControlMouseWheel(object sender, MouseEventArgs e)
        {
            var delta = e.Delta;
            if (Configuration.Settings.VideoControls.WaveformMouseWheelScrollUpIsForward)
            {
                delta = -delta;
            }

            var newPosition = CurrentPosition - delta / 256.0;

            if (newPosition < 0)
            {
                newPosition = 0;
            }
            else if (newPosition > Duration)
            {
                newPosition = Duration;
            }

            CurrentPosition = newPosition;
        }

        private Control MakeSubtitlesPanel()
        {
            _panelSubtitle = new Panel { BackColor = _backgroundColor, Left = 0, Top = 0, Height = _subtitlesHeight + 1 };
            TextBox = new RichTextBoxViewOnly();
            _panelSubtitle.Controls.Add(TextBox);
            TextBox.BackColor = _backgroundColor;
            TextBox.ForeColor = Color.White;
            TextBox.Dock = DockStyle.Fill;
            SetSubtitleFont();
            TextBox.MouseClick += SubtitleTextBoxMouseClick;
            return _panelSubtitle;
        }

        public void SetSubtitleFont()
        {
            var gs = Configuration.Settings.General;
            if (string.IsNullOrEmpty(gs.SubtitleFontName))
            {
                gs.SubtitleFontName = "Tahoma";
            }

            if (gs.VideoPlayerPreviewFontBold)
            {
                TextBox.Font = new Font(gs.VideoPlayerPreviewFontName, gs.VideoPlayerPreviewFontSize * FontSizeFactor, FontStyle.Bold);
            }
            else
            {
                TextBox.Font = new Font(gs.VideoPlayerPreviewFontName, gs.VideoPlayerPreviewFontSize * FontSizeFactor, FontStyle.Regular);
            }

            SubtitleText = _subtitleText;
        }

        private void SubtitleTextBoxMouseClick(object sender, MouseEventArgs e)
        {
            TogglePlayPause();
            OnPlayerClicked?.Invoke(sender, e);
        }

        public Paragraph LastParagraph { get; set; }

        public void SetSubtitleText(string text, Paragraph p, Subtitle subtitle, SubtitleFormat format)
        {
            var mpv = VideoPlayer as LibMpvDynamic;
            LastParagraph = p;
            if (mpv != null && Configuration.Settings.General.MpvHandlesPreviewText && VideoHeight > 0 && VideoWidth > 0)
            {
                if (_subtitlesHeight > 0)
                {
                    _subtitlesHeight = 0;
                    VideoPlayerContainerResize(null, null);
                }
                _subtitleText = text;
                RefreshMpv(mpv, subtitle, format);
                if (TextBox.Text.Length > 0)
                {
                    TextBox.Text = string.Empty;
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(_mpvTextFileName) || _subtitlesHeight == 0)
                {
                    mpv?.RemoveSubtitle();
                    _subtitlesHeight = 57;
                    VideoPlayerContainerResize(null, null);
                    DeleteTempMpvFileName();
                }
                SubtitleText = text;
            }
        }

        public void UpdateMpvStyle()
        {
            var gs = Configuration.Settings.General;
            var mpvStyle = GetMpvPreviewStyle(gs);

            MpvPreviewStyleHeader = string.Format(AdvancedSubStationAlpha.HeaderNoStyles, "MPV preview file", mpvStyle.ToRawAss(SsaStyle.DefaultAssStyleFormat));
        }

        private static SsaStyle GetMpvPreviewStyle(GeneralSettings gs)
        {
            return new SsaStyle
            {
                Name = "Default",
                FontName = gs.VideoPlayerPreviewFontName,
                FontSize = gs.VideoPlayerPreviewFontSize,
                Bold = gs.VideoPlayerPreviewFontBold,
                Primary = gs.MpvPreviewTextPrimaryColor,
                Outline = gs.MpvPreviewTextOutlineColor,
                Background = gs.MpvPreviewTextBackgroundColor,
                OutlineWidth = gs.MpvPreviewTextOutlineWidth,
                ShadowWidth = gs.MpvPreviewTextShadowWidth,
                BorderStyle = gs.MpvPreviewTextOpaqueBoxStyle,
                Alignment = gs.MpvPreviewTextAlignment,
                MarginVertical = gs.MpvPreviewTextMarginVertical
            };
        }

        private string _mpvPreviewStyleHeader;
        private string MpvPreviewStyleHeader
        {
            get
            {
                if (_mpvPreviewStyleHeader is null)
                {
                    UpdateMpvStyle();
                }

                return _mpvPreviewStyleHeader;
            }
            set => _mpvPreviewStyleHeader = value;
        }

        private Subtitle _subtitlePrev;
        private string _mpvTextOld = string.Empty;
        private int _mpvSubOldHash = -1;
        private string _mpvTextFileName;
        private int _retryCount = 3;
        private void RefreshMpv(LibMpvDynamic mpv, Subtitle subtitle, SubtitleFormat uiFormat)
        {
            if (subtitle == null)
            {
                return;
            }

            try
            {
                subtitle = new Subtitle(subtitle, false);
                if (SmpteMode)
                {
                    foreach (var paragraph in subtitle.Paragraphs)
                    {
                        paragraph.StartTime.TotalMilliseconds *= 1.001;
                        paragraph.EndTime.TotalMilliseconds *= 1.001;
                    }
                }

                SubtitleFormat format = new AdvancedSubStationAlpha();
                string text;

                var uiFormatType = uiFormat.GetType();
                if (uiFormatType == typeof(NetflixImsc11Japanese))
                {
                    text = NetflixImsc11JapaneseToAss.Convert(subtitle, VideoWidth, VideoHeight);
                }
                else if (uiFormatType == typeof(WebVTT) || uiFormatType == typeof(WebVTTFileWithLineNumber))
                {
                    //TODO: add some caching!?
                    var defaultStyle = GetMpvPreviewStyle(Configuration.Settings.General);
                    defaultStyle.BorderStyle = "3";
                    subtitle = new Subtitle(subtitle);
                    subtitle = WebVttToAssa.Convert(subtitle, defaultStyle, VideoWidth, VideoHeight);
                    format = new AdvancedSubStationAlpha();
                    text = subtitle.ToText(format);
                    //    File.WriteAllText(@"c:\data\__a.ass", text);
                }
                else
                {
                    if (subtitle.Header == null || !subtitle.Header.Contains("[V4+ Styles]") || uiFormatType != typeof(AdvancedSubStationAlpha))
                    {
                        if (string.IsNullOrEmpty(subtitle.Header) && uiFormatType == typeof(SubStationAlpha))
                        {
                            subtitle.Header = SubStationAlpha.DefaultHeader;
                        }

                        if (subtitle.Header != null && subtitle.Header.Contains("[V4 Styles]"))
                        {
                            subtitle.Header = AdvancedSubStationAlpha.GetHeaderAndStylesFromSubStationAlpha(subtitle.Header);
                        }

                        var oldSub = subtitle;
                        subtitle = new Subtitle(subtitle);
                        if (TextBox.RightToLeft == RightToLeft.Yes && LanguageAutoDetect.CouldBeRightToLeftLanguage(subtitle))
                        {
                            for (var index = 0; index < subtitle.Paragraphs.Count; index++)
                            {
                                var paragraph = subtitle.Paragraphs[index];
                                if (LanguageAutoDetect.ContainsRightToLeftLetter(paragraph.Text))
                                {
                                    paragraph.Text = Utilities.FixRtlViaUnicodeChars(paragraph.Text);
                                }
                            }
                        }

                        if (subtitle.Header == null || !(subtitle.Header.Contains("[V4+ Styles]") && uiFormatType == typeof(SubStationAlpha)))
                        {
                            subtitle.Header = MpvPreviewStyleHeader;
                        }

                        if (oldSub.Header != null && oldSub.Header.Length > 20 && oldSub.Header.Substring(3, 3) == "STL")
                        {
                            subtitle.Header = subtitle.Header.Replace("Style: Default,", "Style: Box," +
                                Configuration.Settings.General.VideoPlayerPreviewFontName + "," +
                                Configuration.Settings.General.VideoPlayerPreviewFontSize + ",&H00FFFFFF,&H0300FFFF,&H00000000,&H02000000," +
                                (Configuration.Settings.General.VideoPlayerPreviewFontBold ? "-1" : "0") + ",0,0,0,100,100,0,0,3,2,0,2,10,10,10,1" +
                                                                       Environment.NewLine + "Style: Default,");

                            var useBox = false;
                            if (Configuration.Settings.SubtitleSettings.EbuStlTeletextUseBox)
                            {
                                try
                                {
                                    var encoding = Ebu.GetEncoding(oldSub.Header.Substring(0, 3));
                                    var buffer = encoding.GetBytes(oldSub.Header);
                                    var header = Ebu.ReadHeader(buffer);
                                    if (header.DisplayStandardCode != "0")
                                    {
                                        useBox = true;
                                    }
                                }
                                catch
                                {
                                    // ignore
                                }
                            }

                            for (var index = 0; index < subtitle.Paragraphs.Count; index++)
                            {
                                var p = subtitle.Paragraphs[index];

                                p.Extra = useBox ? "Box" : "Default";

                                if (p.Text.Contains("<box>"))
                                {
                                    p.Extra = "Box";
                                    p.Text = p.Text.Replace("<box>", string.Empty).Replace("</box>", string.Empty);
                                }
                            }
                        }
                    }

                    var hash = subtitle.GetFastHashCode(null);
                    if (hash != _mpvSubOldHash || string.IsNullOrEmpty(_mpvTextOld))
                    {
                        text = subtitle.ToText(format);
                        _mpvSubOldHash = hash;
                    }
                    else
                    {
                        text = _mpvTextOld;
                    }
                }


                if (text != _mpvTextOld || _mpvTextFileName == null || _retryCount > 0)
                {
                    if (_retryCount >= 0 || string.IsNullOrEmpty(_mpvTextFileName) || _subtitlePrev == null || _subtitlePrev.FileName != subtitle.FileName || !_mpvTextFileName.EndsWith(format.Extension, StringComparison.Ordinal))
                    {
                        mpv.RemoveSubtitle();
                        DeleteTempMpvFileName();
                        _mpvTextFileName = FileUtil.GetTempFileName(format.Extension);
                        File.WriteAllText(_mpvTextFileName, text);
                        mpv.LoadSubtitle(_mpvTextFileName);
                        _retryCount--;
                    }
                    else
                    {
                        File.WriteAllText(_mpvTextFileName, text);
                        mpv.ReloadSubtitle();
                    }
                    _mpvTextOld = text;
                }
                _subtitlePrev = subtitle;
            }
            catch
            {
                // ignored
            }
        }

        private void DeleteTempMpvFileName()
        {
            try
            {
                if (File.Exists(_mpvTextFileName))
                {
                    File.Delete(_mpvTextFileName);
                    _mpvTextFileName = null;
                }
            }
            catch
            {
                // ignored
            }
        }

        public string SubtitleText
        {
            get => _subtitleText;
            set
            {
                _subtitleText = value;
                SetRtbHtml.SetText(TextBox, value);
            }
        }

        private void PanelPlayerMouseDown(object sender, MouseEventArgs e)
        {
            if (VideoPlayer == null)
            {
                OnEmptyPlayerClicked?.Invoke(sender, e);
            }

            TogglePlayPause();
            OnPlayerClicked?.Invoke(sender, e);
        }

        public void InitializeVolume(double defaultVolume)
        {
            int maxVolume = _pictureBoxVolumeBarBackground.Width - 18;
            _pictureBoxVolumeBar.Width = (int)(maxVolume * defaultVolume / 100.0);
        }

        private Control MakePlayerPanel()
        {
            PanelPlayer = new DoubleBufferedPanel { BackColor = _backgroundColor, Left = 0, Top = 0 };
            return PanelPlayer;
        }

        public void HideControls(bool hideCursor)
        {
            if (_panelControls.Visible)
            {
                _panelSubtitle.Height += ControlsHeight;
                _panelControls.Visible = false;


                var useCompleteFullscreen = VideoPlayer is LibMpvDynamic && Configuration.Settings.General.MpvHandlesPreviewText;
                if (useCompleteFullscreen)
                {
                    PanelPlayer.Dock = DockStyle.Fill;
                }
            }

            if (hideCursor)
            {
                HideCursor();
            }
        }

        public void ShowControls()
        {
            if (!_panelControls.Visible)
            {
                _panelControls.Visible = true;
                _panelControls.BringToFront();

                var useCompleteFullscreen = VideoPlayer is LibMpvDynamic && Configuration.Settings.General.MpvHandlesPreviewText;
                if (useCompleteFullscreen && PanelPlayer.Dock == DockStyle.Fill)
                {
                    // keep fullscreen
                }
                else
                {
                    _panelSubtitle.Height -= ControlsHeight;

                    if (PanelPlayer.Dock == DockStyle.Fill)
                    {
                        PanelPlayer.Dock = DockStyle.None;
                    }
                }
            }

            ShowCursor();
        }

        public void HideCursor()
        {
            if (_cursorStatus < 0)
            {
                return;
            }

            _cursorStatus--;
            if (VideoPlayer != null)
            {
                var mpv = VideoPlayer as LibMpvDynamic;
                mpv?.HideCursor();
            }
            Cursor.Hide();
        }

        private int _cursorStatus;

        public void ShowCursor()
        {
            if (_cursorStatus >= 0)
            {
                return;
            }

            _cursorStatus++;
            if (VideoPlayer != null)
            {
                var mpv = VideoPlayer as LibMpvDynamic;
                mpv?.ShowCursor();
            }
            Cursor.Show();
        }

        private Control MakeControlsPanel()
        {
            _panelControls = new Panel { Left = 0, Height = ControlsHeight };

            _pictureBoxBackground = new PictureBox
            {
                Image = (Image)_resources.GetObject("pictureBoxBar.Image"),
                Location = new Point(0, 0),
                Name = "_pictureBoxBackground",
                Size = new Size(200, 45),
                SizeMode = PictureBoxSizeMode.StretchImage,
                TabStop = false
            };
            _panelControls.Controls.Add(_pictureBoxBackground);

            // Initialize play button controls
            _pictureBoxPlay = CreateControlPictureBox("pictureBoxPlay.Image", "pictureBoxPlay", new Point(PLAY_BUTTON_X, BUTTON_Y_OFFSET), BUTTON_SIZE);
            _pictureBoxPlay.MouseEnter += PictureBoxPlayMouseEnter;

            _pictureBoxPlayDown = CreateControlPictureBox("pictureBoxPlayDown.Image", "pictureBoxPlayDown", new Point(PLAY_BUTTON_X, BUTTON_Y_OFFSET), BUTTON_SIZE);
            
            _pictureBoxPlayOver = CreateControlPictureBox("pictureBoxPlayOver.Image", "pictureBoxPlayOver", new Point(PLAY_BUTTON_X, BUTTON_Y_OFFSET), BUTTON_SIZE);
            _pictureBoxPlayOver.MouseLeave += PictureBoxPlayOverMouseLeave;
            _pictureBoxPlayOver.MouseDown += PictureBoxPlayOverMouseDown;
            _pictureBoxPlayOver.MouseUp += PictureBoxPlayOverMouseUp;

            // Initialize pause button controls
            _pictureBoxPause = CreateControlPictureBox("pictureBoxPause.Image", "pictureBoxPause", new Point(PLAY_BUTTON_X, BUTTON_Y_OFFSET), BUTTON_SIZE);
            _pictureBoxPause.MouseEnter += PictureBoxPauseMouseEnter;

            _pictureBoxPauseDown = CreateControlPictureBox("pictureBoxPauseDown.Image", "pictureBoxPauseDown", new Point(PLAY_BUTTON_X, BUTTON_Y_OFFSET), BUTTON_SIZE);

            _pictureBoxPauseOver = CreateControlPictureBox("pictureBoxPauseOver.Image", "pictureBoxPauseOver", new Point(PLAY_BUTTON_X, BUTTON_Y_OFFSET), BUTTON_SIZE);
            _pictureBoxPauseOver.MouseLeave += PictureBoxPauseOverMouseLeave;
            _pictureBoxPauseOver.MouseDown += PictureBoxPauseOverMouseDown;
            _pictureBoxPauseOver.MouseUp += PictureBoxPauseOverMouseUp;

            // Initialize stop button controls
            _pictureBoxStop = CreateControlPictureBox("pictureBoxStop.Image", "pictureBoxStop", new Point(STOP_BUTTON_X, STOP_BUTTON_Y), SMALL_BUTTON_SIZE);
            _pictureBoxStop.MouseEnter += PictureBoxStopMouseEnter;

            _pictureBoxStopDown = CreateControlPictureBox("pictureBoxStopDown.Image", "pictureBoxStopDown", new Point(STOP_BUTTON_X, STOP_BUTTON_Y), SMALL_BUTTON_SIZE);

            _pictureBoxStopOver = CreateControlPictureBox("pictureBoxStopOver.Image", "pictureBoxStopOver", new Point(STOP_BUTTON_X, STOP_BUTTON_Y), SMALL_BUTTON_SIZE);
            _pictureBoxStopOver.MouseLeave += PictureBoxStopOverMouseLeave;
            _pictureBoxStopOver.MouseDown += PictureBoxStopOverMouseDown;
            _pictureBoxStopOver.MouseUp += PictureBoxStopOverMouseUp;

            // Initialize fullscreen button controls
            _pictureBoxFullscreen = CreateControlPictureBox("pictureBoxFS.Image", "pictureBoxFullscreen", new Point(FULLSCREEN_BUTTON_X, STOP_BUTTON_Y), SMALL_BUTTON_SIZE);
            _pictureBoxFullscreen.MouseEnter += PictureBoxFullscreenMouseEnter;
            _bitmapFullscreen = _pictureBoxFullscreen.Image as Bitmap;

            _pictureBoxFullscreenDown = CreateControlPictureBox("pictureBoxFSDown.Image", "pictureBoxFullscreenDown", new Point(FULLSCREEN_BUTTON_X, STOP_BUTTON_Y), SMALL_BUTTON_SIZE);
            _bitmapFullscreenDown = _pictureBoxFullscreenDown.Image as Bitmap;

            _pictureBoxFullscreenOver = CreateControlPictureBox("pictureBoxFSOver.Image", "pictureBoxFullscreenOver", new Point(FULLSCREEN_BUTTON_X, STOP_BUTTON_Y), SMALL_BUTTON_SIZE);
            _pictureBoxFullscreenOver.MouseLeave += PictureBoxFullscreenOverMouseLeave;
            _pictureBoxFullscreenOver.MouseDown += PictureBoxFullscreenOverMouseDown;
            _pictureBoxFullscreenOver.MouseUp += PictureBoxFullscreenOverMouseUp;
            _bitmapFullscreenOver = _pictureBoxFullscreenOver.Image as Bitmap;

            // Initialize no-fullscreen bitmaps
            _bitmapNoFullscreen = (Image)_resources.GetObject("pictureBoxNoFS.Image") as Bitmap;
            _bitmapNoFullscreenDown = (Image)_resources.GetObject("pictureBoxNoFSDown.Image") as Bitmap;
            _bitmapNoFullscreenOver = (Image)_resources.GetObject("pictureBoxNoFSOver.Image") as Bitmap;

            // Initialize progress bar controls
            InitializeProgressBarControls();

            // Initialize mute and volume controls
            InitializeVolumeControls();

            _pictureBoxReverse = new PictureBox
            {
                Image = (Image)_resources.GetObject("pictureBoxReverse.Image"),
                Location = new Point(28, 3),
                Name = "_pictureBoxReverse",
                Size = new Size(16, 8),
                SizeMode = PictureBoxSizeMode.AutoSize,
                TabStop = false
            };
            _panelControls.Controls.Add(_pictureBoxReverse);
            _pictureBoxReverse.MouseEnter += PictureBoxReverseMouseEnter;

            _pictureBoxReverseOver = new PictureBox
            {
                Image = (Image)_resources.GetObject("pictureBoxReverseMouseOver.Image"),
                Location = _pictureBoxReverse.Location,
                Name = "_pictureBoxReverseOver",
                Size = _pictureBoxReverse.Size,
                SizeMode = PictureBoxSizeMode.AutoSize,
                TabStop = false
            };
            _panelControls.Controls.Add(_pictureBoxReverseOver);
            _pictureBoxReverseOver.MouseLeave += PictureBoxReverseOverMouseLeave;
            _pictureBoxReverseOver.MouseDown += PictureBoxReverseOverMouseDown;
            _pictureBoxReverseOver.MouseUp += PictureBoxReverseOverMouseUp;

            _pictureBoxReverseDown = new PictureBox
            {
                Image = (Image)_resources.GetObject("pictureBoxReverseMouseDown.Image"),
                Location = _pictureBoxReverse.Location,
                Name = "_pictureBoxReverseOver",
                Size = _pictureBoxReverse.Size,
                SizeMode = PictureBoxSizeMode.AutoSize,
                TabStop = false
            };
            _panelControls.Controls.Add(_pictureBoxReverseDown);

            _pictureBoxFastForward = new PictureBox
            {
                Image = (Image)_resources.GetObject("pictureBoxFastForward.Image"),
                Location = new Point(571, 1),
                Name = "_pictureBoxFastForward",
                Size = new Size(17, 13),
                SizeMode = PictureBoxSizeMode.AutoSize,
                TabStop = false
            };
            _panelControls.Controls.Add(_pictureBoxFastForward);
            _pictureBoxFastForward.MouseEnter += PictureBoxFastForwardMouseEnter;

            _pictureBoxFastForwardOver = new PictureBox
            {
                Image = (Image)_resources.GetObject("pictureBoxFastForwardMouseOver.Image"),
                Location = _pictureBoxFastForward.Location,
                Name = "_pictureBoxFastForwardOver",
                Size = _pictureBoxFastForward.Size,
                SizeMode = PictureBoxSizeMode.AutoSize,
                TabStop = false
            };
            _panelControls.Controls.Add(_pictureBoxFastForwardOver);
            _pictureBoxFastForwardOver.MouseLeave += PictureBoxFastForwardOverMouseLeave;
            _pictureBoxFastForwardOver.MouseDown += PictureBoxFastForwardOverMouseDown;
            _pictureBoxFastForwardOver.MouseUp += PictureBoxFastForwardOverMouseUp;

            _pictureBoxFastForwardDown = new PictureBox
            {
                Image = (Image)_resources.GetObject("pictureBoxFastForwardMouseDown.Image"),
                Location = _pictureBoxFastForward.Location,
                Name = "_pictureBoxFastForwardDown",
                Size = _pictureBoxFastForward.Size,
                SizeMode = PictureBoxSizeMode.AutoSize,
                TabStop = false
            };
            _panelControls.Controls.Add(_pictureBoxFastForwardDown);

            _labelVolume.Location = new Point(120, 16);
            _labelVolume.ForeColor = Color.WhiteSmoke;
            _labelVolume.BackColor = Color.FromArgb(67, 75, 93);
            _labelVolume.AutoSize = true;
            _labelVolume.Font = new Font(_labelTimeCode.Font.FontFamily, 6);
            _panelControls.Controls.Add(_labelVolume);

            _labelTimeCode.Location = new Point(280, 28);
            _labelTimeCode.ForeColor = Color.WhiteSmoke;
            _labelTimeCode.Font = new Font(_labelTimeCode.Font.FontFamily, 8, FontStyle.Bold);
            _labelTimeCode.AutoSize = true;
            _panelControls.Controls.Add(_labelTimeCode);

            _labelVideoPlayerName.Location = new Point(282, 17);
            _labelVideoPlayerName.ForeColor = Color.WhiteSmoke;
            _labelVideoPlayerName.BackColor = Color.FromArgb(67, 75, 93);
            _labelVideoPlayerName.AutoSize = true;
            _labelVideoPlayerName.Font = new Font(_labelTimeCode.Font.FontFamily, 6);
            _panelControls.Controls.Add(_labelVideoPlayerName);

            var bg = _pictureBoxBackground.Image as Bitmap;
            _labelVolume.BackColor = bg.GetPixel(_labelVolume.Left, _labelVolume.Top);
            _labelTimeCode.BackColor = bg.GetPixel(_labelTimeCode.Left, _labelTimeCode.Top);
            _labelVideoPlayerName.BackColor = bg.GetPixel(_labelVideoPlayerName.Left, _labelVideoPlayerName.Top);

            _pictureBoxBackground.SendToBack();
            _pictureBoxFastForwardDown.BringToFront();
            _pictureBoxFastForwardOver.BringToFront();
            _pictureBoxFastForward.BringToFront();
            _pictureBoxPlay.BringToFront();

            _panelControls.BackColor = _backgroundColor;
            _pictureBoxPlayDown.BringToFront();
            _pictureBoxPlayOver.BringToFront();
            _pictureBoxPlay.BringToFront();
            _labelTimeCode.BringToFront();
            _labelVolume.BringToFront();
            return _panelControls;
        }

        /// <summary>
        /// Initializes the progress bar controls with proper event handling and styling.
        /// </summary>
        private void InitializeProgressBarControls()
        {
            try
            {
                // Progress bar background
                _pictureBoxProgressbarBackground.Anchor = AnchorStyles.Top | AnchorStyles.Left;
                _pictureBoxProgressbarBackground.BackColor = Color.Transparent;
                _pictureBoxProgressbarBackground.Image = (Image)_resources.GetObject("pictureBoxProgressbarBackground.Image");
                _pictureBoxProgressbarBackground.Location = new Point(PROGRESS_BAR_X, PROGRESS_BAR_BACKGROUND_Y);
                _pictureBoxProgressbarBackground.Margin = new Padding(0);
                _pictureBoxProgressbarBackground.Name = "_pictureBoxProgressbarBackground";
                _pictureBoxProgressbarBackground.Size = new Size(PROGRESS_BAR_WIDTH, PROGRESS_BAR_BACKGROUND_HEIGHT);
                _pictureBoxProgressbarBackground.SizeMode = PictureBoxSizeMode.StretchImage;
                _pictureBoxProgressbarBackground.TabStop = false;
                
                // Add event handlers
                _pictureBoxProgressbarBackground.Paint += PictureBoxProgressbarBackgroundPaint;
                _pictureBoxProgressbarBackground.MouseDown += PictureBoxProgressbarBackgroundMouseDown;
                _pictureBoxProgressbarBackground.MouseLeave += PictureBoxProgressbarBackgroundMouseLeave;
                _pictureBoxProgressbarBackground.MouseMove += PictureBoxProgressbarBackgroundMouseMove;
                
                _panelControls.Controls.Add(_pictureBoxProgressbarBackground);

                // Progress bar
                _pictureBoxProgressBar.Image = (Image)_resources.GetObject("pictureBoxProgressBar.Image");
                _pictureBoxProgressBar.Location = new Point(PROGRESS_BAR_X + 4, PROGRESS_BAR_Y);
                _pictureBoxProgressBar.Name = "_pictureBoxProgressBar";
                _pictureBoxProgressBar.Size = new Size(PROGRESS_BAR_DEFAULT_WIDTH, PROGRESS_BAR_HEIGHT);
                _pictureBoxProgressBar.SizeMode = PictureBoxSizeMode.StretchImage;
                _pictureBoxProgressBar.TabStop = false;
                
                // Add event handlers
                _pictureBoxProgressBar.Paint += PictureBoxProgressBarPaint;
                _pictureBoxProgressBar.MouseDown += PictureBoxProgressBarMouseDown;
                _pictureBoxProgressBar.MouseLeave += PictureBoxProgressBarMouseLeave;
                _pictureBoxProgressBar.MouseMove += PictureBoxProgressBarMouseMove;
                
                _panelControls.Controls.Add(_pictureBoxProgressBar);
                _pictureBoxProgressBar.BringToFront();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing progress bar controls: {ex.Message}");
            }
        }

        /// <summary>
        /// Initializes volume controls.
        /// </summary>
        private void InitializeVolumeControls()
        {
            // Mute controls
            _pictureBoxMute = CreateControlPictureBox("pictureBoxMute.Image", "pictureBoxMute", 
                new Point(MUTE_BUTTON_X, MUTE_BUTTON_Y), MUTE_BUTTON_SIZE);
            _pictureBoxMute.MouseEnter += PictureBoxMuteMouseEnter;

            _pictureBoxMuteDown = CreateControlPictureBox("pictureBoxMuteDown.Image", "pictureBoxMuteDown", 
                new Point(MUTE_BUTTON_X, MUTE_BUTTON_Y), MUTE_BUTTON_SIZE);
            _pictureBoxMuteDown.Click += PictureBoxMuteDownClick;

            _pictureBoxMuteOver = CreateControlPictureBox("pictureBoxMuteOver.Image", "pictureBoxMuteOver", 
                new Point(MUTE_BUTTON_X, MUTE_BUTTON_Y), MUTE_BUTTON_SIZE);
            _pictureBoxMuteOver.MouseLeave += PictureBoxMuteOverMouseLeave;
            _pictureBoxMuteOver.MouseDown += PictureBoxMuteOverMouseDown;
            _pictureBoxMuteOver.MouseUp += PictureBoxMuteOverMouseUp;

            // Volume bars
            _pictureBoxVolumeBarBackground = CreateControlPictureBox("pictureBoxVolumeBarBackground.Image", 
                "pictureBoxVolumeBarBackground", new Point(VOLUME_BAR_BACKGROUND_X, VOLUME_BAR_BACKGROUND_Y), 
                VOLUME_BAR_BACKGROUND_SIZE, PictureBoxSizeMode.AutoSize);
            _pictureBoxVolumeBarBackground.MouseDown += PictureBoxVolumeBarBackgroundMouseDown;

            _pictureBoxVolumeBar = CreateControlPictureBox("pictureBoxVolumeBar.Image", "pictureBoxVolumeBar", 
                new Point(VOLUME_BAR_X, VOLUME_BAR_Y), VOLUME_BAR_SIZE, PictureBoxSizeMode.StretchImage);
            _pictureBoxVolumeBar.MouseDown += PictureBoxVolumeBarMouseDown;
            _pictureBoxVolumeBar.BringToFront();
        }

        /// <summary>
        /// Initializes seek controls (reverse and fast forward).
        /// </summary>
        private void InitializeSeekControls()
        {
            // Reverse controls
            _pictureBoxReverse = CreateControlPictureBox("pictureBoxReverse.Image", "pictureBoxReverse", 
                new Point(REVERSE_BUTTON_X, REVERSE_BUTTON_Y), REVERSE_BUTTON_SIZE);
            _pictureBoxReverse.MouseEnter += PictureBoxReverseMouseEnter;

            _pictureBoxReverseOver = CreateControlPictureBox("pictureBoxReverseMouseOver.Image", "pictureBoxReverseOver", 
                new Point(REVERSE_BUTTON_X, REVERSE_BUTTON_Y), REVERSE_BUTTON_SIZE);
            _pictureBoxReverseOver.MouseLeave += PictureBoxReverseOverMouseLeave;
            _pictureBoxReverseOver.MouseDown += PictureBoxReverseOverMouseDown;
            _pictureBoxReverseOver.MouseUp += PictureBoxReverseOverMouseUp;

            _pictureBoxReverseDown = CreateControlPictureBox("pictureBoxReverseMouseDown.Image", "pictureBoxReverseDown", 
                new Point(REVERSE_BUTTON_X, REVERSE_BUTTON_Y), REVERSE_BUTTON_SIZE);

            // Fast forward controls
            _pictureBoxFastForward = CreateControlPictureBox("pictureBoxFastForward.Image", "pictureBoxFastForward", 
                new Point(FAST_FORWARD_BUTTON_X, FAST_FORWARD_BUTTON_Y), FAST_FORWARD_BUTTON_SIZE);
            _pictureBoxFastForward.MouseEnter += PictureBoxFastForwardMouseEnter;

            _pictureBoxFastForwardOver = CreateControlPictureBox("pictureBoxFastForwardMouseOver.Image", "pictureBoxFastForwardOver", 
                new Point(FAST_FORWARD_BUTTON_X, FAST_FORWARD_BUTTON_Y), FAST_FORWARD_BUTTON_SIZE);
            _pictureBoxFastForwardOver.MouseLeave += PictureBoxFastForwardOverMouseLeave;
            _pictureBoxFastForwardOver.MouseDown += PictureBoxFastForwardOverMouseDown;
            _pictureBoxFastForwardOver.MouseUp += PictureBoxFastForwardOverMouseUp;

            _pictureBoxFastForwardDown = CreateControlPictureBox("pictureBoxFastForwardMouseDown.Image", "pictureBoxFastForwardDown", 
                new Point(FAST_FORWARD_BUTTON_X, FAST_FORWARD_BUTTON_Y), FAST_FORWARD_BUTTON_SIZE);
        }

        /// <summary>
        /// Initializes control panel labels.
        /// </summary>
        private void InitializeLabels()
        {
            // Volume label
            _labelVolume.Location = new Point(VOLUME_LABEL_X, VOLUME_LABEL_Y);
            _labelVolume.ForeColor = Color.WhiteSmoke;
            _labelVolume.BackColor = Color.FromArgb(67, 75, 93);
            _labelVolume.AutoSize = true;
            _labelVolume.Font = new Font(_labelTimeCode.Font.FontFamily, SMALL_FONT_SIZE);
            _panelControls.Controls.Add(_labelVolume);

            // Time code label
            _labelTimeCode.Location = new Point(TIMECODE_LABEL_X, TIMECODE_LABEL_Y);
            _labelTimeCode.ForeColor = Color.WhiteSmoke;
            _labelTimeCode.Font = new Font(_labelTimeCode.Font.FontFamily, NORMAL_FONT_SIZE, FontStyle.Bold);
            _labelTimeCode.AutoSize = true;
            _panelControls.Controls.Add(_labelTimeCode);

            // Video player name label
            _labelVideoPlayerName.Location = new Point(PLAYER_NAME_LABEL_X, PLAYER_NAME_LABEL_Y);
            _labelVideoPlayerName.ForeColor = Color.WhiteSmoke;
            _labelVideoPlayerName.BackColor = Color.FromArgb(67, 75, 93);
            _labelVideoPlayerName.AutoSize = true;
            _labelVideoPlayerName.Font = new Font(_labelTimeCode.Font.FontFamily, SMALL_FONT_SIZE);
            _panelControls.Controls.Add(_labelVideoPlayerName);

            // Set background colors based on actual background
            SetLabelBackgroundColors();
        }

        /// <summary>
        /// Sets control ordering and panel background color.
        /// </summary>
        private void SetControlOrder()
        {
            _pictureBoxBackground.SendToBack();
            
            // Bring important controls to front
            _pictureBoxFastForwardDown.BringToFront();
            _pictureBoxFastForwardOver.BringToFront();
            _pictureBoxFastForward.BringToFront();
            _pictureBoxPlay.BringToFront();

            _panelControls.BackColor = _backgroundColor;
            
            _pictureBoxPlayDown.BringToFront();
            _pictureBoxPlayOver.BringToFront();
            _pictureBoxPlay.BringToFront();
            _labelTimeCode.BringToFront();
            _labelVolume.BringToFront();
        }
        #endregion

        #region Constructor and Initialization
        /// <summary>
        /// Initializes a new instance of the VideoPlayerContainer.
        /// </summary>
        public VideoPlayerContainer()
        {
            InitializeContainer();
        }

        /// <summary>
        /// Performs the main initialization of the video player container.
        /// </summary>
        private void InitializeContainer()
        {
            _loading = true;
            
            try
            {
                InitializeDefaultSettings();
                InitializeComponents();
                SetupEventHandlers();
                PerformInitialLayout();
                ConfigureLinuxSpecificSettings();
                CompleteInitialization();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing VideoPlayerContainer: {ex.Message}");
            }
            finally
            {
                _loading = false;
            }
        }

        /// <summary>
        /// Initializes default settings and properties.
        /// </summary>
        private void InitializeDefaultSettings()
        {
            Chapters = Array.Empty<MatroskaChapter>();
            FontSizeFactor = 1.0F;
            BorderStyle = BorderStyle.None;
            _resources = new System.ComponentModel.ComponentResourceManager(typeof(VideoPlayerContainer));
            _labelVolume.Text = $"{Configuration.Settings.General.VideoPlayerDefaultVolume}%";
            BackColor = _backgroundColor;
        }

        /// <summary>
        /// Creates and adds the main components to the container.
        /// </summary>
        private void InitializeComponents()
        {
            Controls.Add(MakePlayerPanel());
            Controls.Add(MakeSubtitlesPanel());
            Controls.Add(MakeControlsPanel());
            _panelControls.BringToFront();
            _pictureBoxProgressBar.Width = 0;
        }

        /// <summary>
        /// Sets up all event handlers for the container.
        /// </summary>
        private void SetupEventHandlers()
        {
            Resize += VideoPlayerContainerResize;
            PanelPlayer.MouseDown += PanelPlayerMouseDown;
            _labelTimeCode.Click += LabelTimeCodeClick;
            PanelPlayer.Paint += PanelPlayerPaint;
        }

        /// <summary>
        /// Performs the initial layout and control setup.
        /// </summary>
        private void PerformInitialLayout()
        {
            ShowAllControls();
            
            // Initialize fast forward states
            PictureBoxFastForwardMouseEnter(null, null);
            PictureBoxFastForwardOverMouseLeave(null, null);

            // Bring volume controls to front
            _pictureBoxVolumeBarBackground.BringToFront();
            _pictureBoxVolumeBar.BringToFront();
            _labelVolume.BringToFront();
        }

        /// <summary>
        /// Configures Linux-specific settings with delayed initialization.
        /// </summary>
        private void ConfigureLinuxSpecificSettings()
        {
            if (Configuration.IsRunningOnLinux)
            {
                TaskDelayHelper.RunDelayed(TimeSpan.FromMilliseconds(1500), PerformDelayedLinuxSetup);
            }
        }

        /// <summary>
        /// Performs delayed setup operations for Linux compatibility.
        /// </summary>
        private void PerformDelayedLinuxSetup()
        {
            try
            {
                if (string.IsNullOrEmpty(_labelVideoPlayerName.Text))
                {
                    _labelVideoPlayerName.Text = "...";
                }
                
                FontSizeFactor = 1.0F;
                SetSubtitleFont();
                _labelTimeCode.Text = $"{new TimeCode().ToDisplayString()} / ?";
                ShowAllControls();
                VideoPlayerContainerResize(this, null);
                ShowAllControls();
                Invalidate();
                Refresh();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in delayed Linux setup: {ex.Message}");
            }
        }

        /// <summary>
        /// Completes the initialization process.
        /// </summary>
        private void CompleteInitialization()
        {
            VideoPlayerContainerResize(this, null);
            ShowPlayerLogo();
        }
        #endregion

        #region Player Logo and Painting
        /// <summary>
        /// Shows the video player logo when no video is loaded.
        /// </summary>
        public void ShowPlayerLogo()
        {
            try
            {
                var iconPath = GetPlayerIconPath();
                LoadPlayerIcon(iconPath);
                
                if (_videoPlayer == null)
                {
                    PanelPlayer.Visible = true;
                    PanelPlayer.BringToFront();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing player logo: {ex.Message}");
                CreateFallbackIcon();
            }
        }

        /// <summary>
        /// Gets the path to the player icon based on the current video player setting.
        /// </summary>
        /// <returns>The full path to the player icon file.</returns>
        private static string GetPlayerIconPath()
        {
            var playerName = Configuration.Settings.General.VideoPlayer.ToLowerInvariant();
            return Path.Combine(Configuration.BaseDirectory, "icons", $"{playerName}.png");
        }

        /// <summary>
        /// Loads the player icon from the specified path.
        /// </summary>
        /// <param name="iconPath">The path to the icon file.</param>
        private void LoadPlayerIcon(string iconPath)
        {
            if (File.Exists(iconPath))
            {
                // Dispose previous icon to prevent memory leaks
                _playerIcon?.Dispose();
                _playerIcon = new Bitmap(iconPath);
            }
            else
            {
                CreateFallbackIcon();
            }
        }

        /// <summary>
        /// Creates a fallback icon when the player-specific icon is not available.
        /// </summary>
        private void CreateFallbackIcon()
        {
            _playerIcon?.Dispose();
            _playerIcon = new Bitmap(1, 1);
        }

        /// <summary>
        /// Handles painting the player panel, including the player logo when no video is active.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The paint event arguments.</param>
        private void PanelPlayerPaint(object sender, PaintEventArgs e)
        {
            if (_videoPlayer != null || _playerIcon == null)
            {
                return;
            }

            try
            {
                DrawPlayerLogo(e.Graphics);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error painting player panel: {ex.Message}");
            }
        }

        /// <summary>
        /// Draws the player logo on the specified graphics surface.
        /// </summary>
        /// <param name="graphics">The graphics object to draw on.</param>
        private void DrawPlayerLogo(Graphics graphics)
        {
            const float LogoOpacity = 0.4f;
            
            var logoSize = CalculateLogoSize();
            var logoPosition = CalculateLogoPosition(logoSize);
            
            using var attributes = CreateTransparencyAttributes(LogoOpacity);
            var destRect = new Rectangle(logoPosition.X, logoPosition.Y, logoSize.Width, logoSize.Height);
            
            graphics.DrawImage(_playerIcon, destRect, 0, 0, _playerIcon.Width, _playerIcon.Height, 
                GraphicsUnit.Pixel, attributes);
        }

        /// <summary>
        /// Calculates the optimal size for the logo based on the panel dimensions.
        /// </summary>
        /// <returns>The calculated logo size.</returns>
        private Size CalculateLogoSize()
        {
            var width = _playerIcon.Width;
            var height = _playerIcon.Height;

            // Adjust size if logo is taller than the panel
            if (PanelPlayer.Height < height)
            {
                width -= height - PanelPlayer.Height;
                height = PanelPlayer.Height;
            }

            return new Size(width, height);
        }

        /// <summary>
        /// Calculates the centered position for the logo within the panel.
        /// </summary>
        /// <param name="logoSize">The size of the logo.</param>
        /// <returns>The calculated logo position.</returns>
        private Point CalculateLogoPosition(Size logoSize)
        {
            var left = (PanelPlayer.Width / 2) - (logoSize.Width / 2);
            var top = (PanelPlayer.Height / 2) - (logoSize.Height / 2);
            return new Point(left, top);
        }

        /// <summary>
        /// Creates image attributes for drawing with transparency.
        /// </summary>
        /// <param name="opacity">The opacity level (0.0 to 1.0).</param>
        /// <returns>ImageAttributes configured for transparency.</returns>
        private static ImageAttributes CreateTransparencyAttributes(float opacity)
        {
            var matrix = new ColorMatrix();
            matrix.Matrix33 = opacity; // Set the alpha channel (transparency)

            var attributes = new ImageAttributes();
            attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
            return attributes;
        }
        #endregion

            var offset = 30;
            if (PanelPlayer.Height <= top + offset + h)
            {
                offset -= (top + offset + h) - PanelPlayer.Height;
                if (offset < 0)
                {
                    offset = 0;
                }
            }

            // Draw the image with the modified opacity
            e.Graphics.DrawImage(img,
                                 new Rectangle(left, top + offset, w, h),
                                 0, 0, img.Width, img.Height,
                                 GraphicsUnit.Pixel, attributes);
        }

        private bool _showDuration = true;
        private void LabelTimeCodeClick(object sender, EventArgs e)
        {
            _showDuration = !_showDuration;
            RefreshProgressBar();
        }

        private void ShowAllControls()
        {
            HideAllPlayImages();
            HideAllPauseImages();
            _pictureBoxPlay.Visible = true;
            _pictureBoxPlay.BringToFront();

            HideAllStopImages();
            _pictureBoxStop.Visible = true;
            _pictureBoxStop.BringToFront();

            HideAllStopImages();
            _pictureBoxStop.Visible = true;
            _pictureBoxStop.BringToFront();

            HideAllFullscreenImages();
            _pictureBoxFullscreen.Visible = true;
            _pictureBoxFullscreen.BringToFront();

            HideAllMuteImages();
            _pictureBoxMute.Visible = true;
            _pictureBoxMute.BringToFront();

            HideAllReverseImages();
            _pictureBoxReverse.Visible = true;
            _pictureBoxReverse.BringToFront();

            HideAllFastForwardImages();
            _pictureBoxFastForward.Visible = true;
            _pictureBoxFastForward.BringToFront();

            _pictureBoxProgressbarBackground.Visible = true;
            _pictureBoxProgressbarBackground.BringToFront();
            _pictureBoxProgressBar.Visible = true;
            _pictureBoxProgressBar.BringToFront();

            _labelTimeCode.Visible = true;
            _labelTimeCode.BringToFront();
            _labelVolume.BringToFront();
        }

        public void EnableMouseWheelStep()
        {
            AddMouseWheelEvent(this);
        }

        public void SetPlayerName(string s)
        {
            _labelVideoPlayerName.Text = s;
            _labelVideoPlayerName.Left = Width - _labelVideoPlayerName.Width - 3;
        }

        public void HidePlayerName()
        {
            _labelVideoPlayerName.Visible = false;
        }

        public void UpdatePlayerName()
        {
            if (_videoPlayer != null)
            {
                SetPlayerName(_videoPlayer.PlayerName);
            }
        }

        public void ResetTimeLabel()
        {
            _labelTimeCode.Text = string.Empty;
        }

        private void AddMouseWheelEvent(Control control)
        {
            control.MouseWheel += ControlMouseWheel;
            foreach (Control ctrl in control.Controls)
            {
                AddMouseWheelEvent(ctrl);
            }
        }

        private void ControlMouseWheel(object sender, MouseEventArgs e)
        {
            var delta = e.Delta;
            if (Configuration.Settings.VideoControls.WaveformMouseWheelScrollUpIsForward)
            {
                delta = -delta;
            }

            var newPosition = CurrentPosition - delta / 256.0;

            if (newPosition < 0)
            {
                newPosition = 0;
            }
            else if (newPosition > Duration)
            {
                newPosition = Duration;
            }

            CurrentPosition = newPosition;
        }

        private Control MakeSubtitlesPanel()
        {
            _panelSubtitle = new Panel { BackColor = _backgroundColor, Left = 0, Top = 0, Height = _subtitlesHeight + 1 };
            TextBox = new RichTextBoxViewOnly();
            _panelSubtitle.Controls.Add(TextBox);
            TextBox.BackColor = _backgroundColor;
            TextBox.ForeColor = Color.White;
            TextBox.Dock = DockStyle.Fill;
            SetSubtitleFont();
            TextBox.MouseClick += SubtitleTextBoxMouseClick;
            return _panelSubtitle;
        }

        public void SetSubtitleFont()
        {
            var gs = Configuration.Settings.General;
            if (string.IsNullOrEmpty(gs.SubtitleFontName))
            {
                gs.SubtitleFontName = "Tahoma";
            }

            if (gs.VideoPlayerPreviewFontBold)
            {
                TextBox.Font = new Font(gs.VideoPlayerPreviewFontName, gs.VideoPlayerPreviewFontSize * FontSizeFactor, FontStyle.Bold);
            }
            else
            {
                TextBox.Font = new Font(gs.VideoPlayerPreviewFontName, gs.VideoPlayerPreviewFontSize * FontSizeFactor, FontStyle.Regular);
            }

            SubtitleText = _subtitleText;
        }

        private void SubtitleTextBoxMouseClick(object sender, MouseEventArgs e)
        {
            TogglePlayPause();
            OnPlayerClicked?.Invoke(sender, e);
        }

        public Paragraph LastParagraph { get; set; }

        public void SetSubtitleText(string text, Paragraph p, Subtitle subtitle, SubtitleFormat format)
        {
            var mpv = VideoPlayer as LibMpvDynamic;
            LastParagraph = p;
            if (mpv != null && Configuration.Settings.General.MpvHandlesPreviewText && VideoHeight > 0 && VideoWidth > 0)
            {
                if (_subtitlesHeight > 0)
                {
                    _subtitlesHeight = 0;
                    VideoPlayerContainerResize(null, null);
                }
                _subtitleText = text;
                RefreshMpv(mpv, subtitle, format);
                if (TextBox.Text.Length > 0)
                {
                    TextBox.Text = string.Empty;
                }
            }
            else
            {
                if (!string.IsNullOrEmpty(_mpvTextFileName) || _subtitlesHeight == 0)
                {
                    mpv?.RemoveSubtitle();
                    _subtitlesHeight = 57;
                    VideoPlayerContainerResize(null, null);
                    DeleteTempMpvFileName();
                }
                SubtitleText = text;
            }
        }

        public void UpdateMpvStyle()
        {
            var gs = Configuration.Settings.General;
            var mpvStyle = GetMpvPreviewStyle(gs);

            MpvPreviewStyleHeader = string.Format(AdvancedSubStationAlpha.HeaderNoStyles, "MPV preview file", mpvStyle.ToRawAss(SsaStyle.DefaultAssStyleFormat));
        }

        private static SsaStyle GetMpvPreviewStyle(GeneralSettings gs)
        {
            return new SsaStyle
            {
                Name = "Default",
                FontName = gs.VideoPlayerPreviewFontName,
                FontSize = gs.VideoPlayerPreviewFontSize,
                Bold = gs.VideoPlayerPreviewFontBold,
                Primary = gs.MpvPreviewTextPrimaryColor,
                Outline = gs.MpvPreviewTextOutlineColor,
                Background = gs.MpvPreviewTextBackgroundColor,
                OutlineWidth = gs.MpvPreviewTextOutlineWidth,
                ShadowWidth = gs.MpvPreviewTextShadowWidth,
                BorderStyle = gs.MpvPreviewTextOpaqueBoxStyle,
                Alignment = gs.MpvPreviewTextAlignment,
                MarginVertical = gs.MpvPreviewTextMarginVertical
            };
        }

        private string _mpvPreviewStyleHeader;
        private string MpvPreviewStyleHeader
        {
            get
            {
                if (_mpvPreviewStyleHeader is null)
                {
                    UpdateMpvStyle();
                }

                return _mpvPreviewStyleHeader;
            }
            set => _mpvPreviewStyleHeader = value;
        }

        private Subtitle _subtitlePrev;
        private string _mpvTextOld = string.Empty;
        private int _mpvSubOldHash = -1;
        private string _mpvTextFileName;
        private int _retryCount = 3;
        private void RefreshMpv(LibMpvDynamic mpv, Subtitle subtitle, SubtitleFormat uiFormat)
        {
            if (subtitle == null)
            {
                return;
            }

            try
            {
                subtitle = new Subtitle(subtitle, false);
                if (SmpteMode)
                {
                    foreach (var paragraph in subtitle.Paragraphs)
                    {
                        paragraph.StartTime.TotalMilliseconds *= 1.001;
                        paragraph.EndTime.TotalMilliseconds *= 1.001;
                    }
                }

                SubtitleFormat format = new AdvancedSubStationAlpha();
                string text;

                var uiFormatType = uiFormat.GetType();
                if (uiFormatType == typeof(NetflixImsc11Japanese))
                {
                    text = NetflixImsc11JapaneseToAss.Convert(subtitle, VideoWidth, VideoHeight);
                }
                else if (uiFormatType == typeof(WebVTT) || uiFormatType == typeof(WebVTTFileWithLineNumber))
                {
                    //TODO: add some caching!?
                    var defaultStyle = GetMpvPreviewStyle(Configuration.Settings.General);
                    defaultStyle.BorderStyle = "3";
                    subtitle = new Subtitle(subtitle);
                    subtitle = WebVttToAssa.Convert(subtitle, defaultStyle, VideoWidth, VideoHeight);
                    format = new AdvancedSubStationAlpha();
                    text = subtitle.ToText(format);
                    //    File.WriteAllText(@"c:\data\__a.ass", text);
                }
                else
                {
                    if (subtitle.Header == null || !subtitle.Header.Contains("[V4+ Styles]") || uiFormatType != typeof(AdvancedSubStationAlpha))
                    {
                        if (string.IsNullOrEmpty(subtitle.Header) && uiFormatType == typeof(SubStationAlpha))
                        {
                            subtitle.Header = SubStationAlpha.DefaultHeader;
                        }

                        if (subtitle.Header != null && subtitle.Header.Contains("[V4 Styles]"))
                        {
                            subtitle.Header = AdvancedSubStationAlpha.GetHeaderAndStylesFromSubStationAlpha(subtitle.Header);
                        }

                        var oldSub = subtitle;
                        subtitle = new Subtitle(subtitle);
                        if (TextBox.RightToLeft == RightToLeft.Yes && LanguageAutoDetect.CouldBeRightToLeftLanguage(subtitle))
                        {
                            for (var index = 0; index < subtitle.Paragraphs.Count; index++)
                            {
                                var paragraph = subtitle.Paragraphs[index];
                                if (LanguageAutoDetect.ContainsRightToLeftLetter(paragraph.Text))
                                {
                                    paragraph.Text = Utilities.FixRtlViaUnicodeChars(paragraph.Text);
                                }
                            }
                        }

                        if (subtitle.Header == null || !(subtitle.Header.Contains("[V4+ Styles]") && uiFormatType == typeof(SubStationAlpha)))
                        {
                            subtitle.Header = MpvPreviewStyleHeader;
                        }

                        if (oldSub.Header != null && oldSub.Header.Length > 20 && oldSub.Header.Substring(3, 3) == "STL")
                        {
                            subtitle.Header = subtitle.Header.Replace("Style: Default,", "Style: Box," +
                                Configuration.Settings.General.VideoPlayerPreviewFontName + "," +
                                Configuration.Settings.General.VideoPlayerPreviewFontSize + ",&H00FFFFFF,&H0300FFFF,&H00000000,&H02000000," +
                                (Configuration.Settings.General.VideoPlayerPreviewFontBold ? "-1" : "0") + ",0,0,0,100,100,0,0,3,2,0,2,10,10,10,1" +
                                                                       Environment.NewLine + "Style: Default,");

                            var useBox = false;
                            if (Configuration.Settings.SubtitleSettings.EbuStlTeletextUseBox)
                            {
                                try
                                {
                                    var encoding = Ebu.GetEncoding(oldSub.Header.Substring(0, 3));
                                    var buffer = encoding.GetBytes(oldSub.Header);
                                    var header = Ebu.ReadHeader(buffer);
                                    if (header.DisplayStandardCode != "0")
                                    {
                                        useBox = true;
                                    }
                                }
                                catch
                                {
                                    // ignore
                                }
                            }

                            for (var index = 0; index < subtitle.Paragraphs.Count; index++)
                            {
                                var p = subtitle.Paragraphs[index];

                                p.Extra = useBox ? "Box" : "Default";

                                if (p.Text.Contains("<box>"))
                                {
                                    p.Extra = "Box";
                                    p.Text = p.Text.Replace("<box>", string.Empty).Replace("</box>", string.Empty);
                                }
                            }
                        }
                    }

                    var hash = subtitle.GetFastHashCode(null);
                    if (hash != _mpvSubOldHash || string.IsNullOrEmpty(_mpvTextOld))
                    {
                        text = subtitle.ToText(format);
                        _mpvSubOldHash = hash;
                    }
                    else
                    {
                        text = _mpvTextOld;
                    }
                }


                if (text != _mpvTextOld || _mpvTextFileName == null || _retryCount > 0)
                {
                    if (_retryCount >= 0 || string.IsNullOrEmpty(_mpvTextFileName) || _subtitlePrev == null || _subtitlePrev.FileName != subtitle.FileName || !_mpvTextFileName.EndsWith(format.Extension, StringComparison.Ordinal))
                    {
                        mpv.RemoveSubtitle();
                        DeleteTempMpvFileName();
                        _mpvTextFileName = FileUtil.GetTempFileName(format.Extension);
                        File.WriteAllText(_mpvTextFileName, text);
                        mpv.LoadSubtitle(_mpvTextFileName);
                        _retryCount--;
                    }
                    else
                    {
                        File.WriteAllText(_mpvTextFileName, text);
                        mpv.ReloadSubtitle();
                    }
                    _mpvTextOld = text;
                }
                _subtitlePrev = subtitle;
            }
            catch
            {
                // ignored
            }
        }

        private void DeleteTempMpvFileName()
        {
            try
            {
                if (File.Exists(_mpvTextFileName))
                {
                    File.Delete(_mpvTextFileName);
                    _mpvTextFileName = null;
                }
            }
            catch
            {
                // ignored
            }
        }

        public string SubtitleText
        {
            get => _subtitleText;
            set
            {
                _subtitleText = value;
                SetRtbHtml.SetText(TextBox, value);
            }
        }

        private void PanelPlayerMouseDown(object sender, MouseEventArgs e)
        {
            if (VideoPlayer == null)
            {
                OnEmptyPlayerClicked?.Invoke(sender, e);
            }

            TogglePlayPause();
            OnPlayerClicked?.Invoke(sender, e);
        }

        public void InitializeVolume(double defaultVolume)
        {
            int maxVolume = _pictureBoxVolumeBarBackground.Width - 18;
            _pictureBoxVolumeBar.Width = (int)(maxVolume * defaultVolume / 100.0);
        }

        private Control MakePlayerPanel()
        {
            PanelPlayer = new DoubleBufferedPanel { BackColor = _backgroundColor, Left = 0, Top = 0 };
            return PanelPlayer;
        }

        public void HideControls(bool hideCursor)
        {
            if (_panelControls.Visible)
            {
                _panelSubtitle.Height += ControlsHeight;
                _panelControls.Visible = false;


                var useCompleteFullscreen = VideoPlayer is LibMpvDynamic && Configuration.Settings.General.MpvHandlesPreviewText;
                if (useCompleteFullscreen)
                {
                    PanelPlayer.Dock = DockStyle.Fill;
                }
            }

            if (hideCursor)
            {
                HideCursor();
            }
        }

        public void ShowControls()
        {
            if (!_panelControls.Visible)
            {
                _panelControls.Visible = true;
                _panelControls.BringToFront();

                var useCompleteFullscreen = VideoPlayer is LibMpvDynamic && Configuration.Settings.General.MpvHandlesPreviewText;
                if (useCompleteFullscreen && PanelPlayer.Dock == DockStyle.Fill)
                {
                    // keep fullscreen
                }
                else
                {
                    _panelSubtitle.Height -= ControlsHeight;

                    if (PanelPlayer.Dock == DockStyle.Fill)
                    {
                        PanelPlayer.Dock = DockStyle.None;
                    }
                }
            }

            ShowCursor();
        }

        public void HideCursor()
        {
            if (_cursorStatus < 0)
            {
                return;
            }

            _cursorStatus--;
            if (VideoPlayer != null)
            {
                var mpv = VideoPlayer as LibMpvDynamic;
                mpv?.HideCursor();
            }
            Cursor.Hide();
        }

        private int _cursorStatus;

        public void ShowCursor()
        {
            if (_cursorStatus >= 0)
            {
                return;
            }

            _cursorStatus++;
            if (VideoPlayer != null)
            {
                var mpv = VideoPlayer as LibMpvDynamic;
                mpv?.ShowCursor();
            }
            Cursor.Show();
        }

        private Control MakeControlsPanel()
        {
            _panelControls = new Panel { Left = 0, Height = ControlsHeight };

            _pictureBoxBackground = new PictureBox
            {
                Image = (Image)_resources.GetObject("pictureBoxBar.Image"),
                Location = new Point(0, 0),
                Name = "_pictureBoxBackground",
                Size = new Size(200, 45),
                SizeMode = PictureBoxSizeMode.StretchImage,
                TabStop = false
            };
            _panelControls.Controls.Add(_pictureBoxBackground);

            // Initialize play button controls
            _pictureBoxPlay = CreateControlPictureBox("pictureBoxPlay.Image", "pictureBoxPlay", new Point(PLAY_BUTTON_X, BUTTON_Y_OFFSET), BUTTON_SIZE);
            _pictureBoxPlay.MouseEnter += PictureBoxPlayMouseEnter;

            _pictureBoxPlayDown = CreateControlPictureBox("pictureBoxPlayDown.Image", "pictureBoxPlayDown", new Point(PLAY_BUTTON_X, BUTTON_Y_OFFSET), BUTTON_SIZE);
            
            _pictureBoxPlayOver = CreateControlPictureBox("pictureBoxPlayOver.Image", "pictureBoxPlayOver", new Point(PLAY_BUTTON_X, BUTTON_Y_OFFSET), BUTTON_SIZE);
            _pictureBoxPlayOver.MouseLeave += PictureBoxPlayOverMouseLeave;
            _pictureBoxPlayOver.MouseDown += PictureBoxPlayOverMouseDown;
            _pictureBoxPlayOver.MouseUp += PictureBoxPlayOverMouseUp;

            // Initialize pause button controls
            _pictureBoxPause = CreateControlPictureBox("pictureBoxPause.Image", "pictureBoxPause", new Point(PLAY_BUTTON_X, BUTTON_Y_OFFSET), BUTTON_SIZE);
            _pictureBoxPause.MouseEnter += PictureBoxPauseMouseEnter;

            _pictureBoxPauseDown = CreateControlPictureBox("pictureBoxPauseDown.Image", "pictureBoxPauseDown", new Point(PLAY_BUTTON_X, BUTTON_Y_OFFSET), BUTTON_SIZE);

            _pictureBoxPauseOver = CreateControlPictureBox("pictureBoxPauseOver.Image", "pictureBoxPauseOver", new Point(PLAY_BUTTON_X, BUTTON_Y_OFFSET), BUTTON_SIZE);
            _pictureBoxPauseOver.MouseLeave += PictureBoxPauseOverMouseLeave;
            _pictureBoxPauseOver.MouseDown += PictureBoxPauseOverMouseDown;
            _pictureBoxPauseOver.MouseUp += PictureBoxPauseOverMouseUp;

            // Initialize stop button controls
            _pictureBoxStop = CreateControlPictureBox("pictureBoxStop.Image", "pictureBoxStop", new Point(STOP_BUTTON_X, STOP_BUTTON_Y), SMALL_BUTTON_SIZE);
            _pictureBoxStop.MouseEnter += PictureBoxStopMouseEnter;

            _pictureBoxStopDown = CreateControlPictureBox("pictureBoxStopDown.Image", "pictureBoxStopDown", new Point(STOP_BUTTON_X, STOP_BUTTON_Y), SMALL_BUTTON_SIZE);

            _pictureBoxStopOver = CreateControlPictureBox("pictureBoxStopOver.Image", "pictureBoxStopOver", new Point(STOP_BUTTON_X, STOP_BUTTON_Y), SMALL_BUTTON_SIZE);
            _pictureBoxStopOver.MouseLeave += PictureBoxStopOverMouseLeave;
            _pictureBoxStopOver.MouseDown += PictureBoxStopOverMouseDown;
            _pictureBoxStopOver.MouseUp += PictureBoxStopOverMouseUp;

            // Initialize fullscreen button controls
            _pictureBoxFullscreen = CreateControlPictureBox("pictureBoxFS.Image", "pictureBoxFullscreen", new Point(FULLSCREEN_BUTTON_X, STOP_BUTTON_Y), SMALL_BUTTON_SIZE);
            _pictureBoxFullscreen.MouseEnter += PictureBoxFullscreenMouseEnter;
            _bitmapFullscreen = _pictureBoxFullscreen.Image as Bitmap;

            _pictureBoxFullscreenDown = CreateControlPictureBox("pictureBoxFSDown.Image", "pictureBoxFullscreenDown", new Point(FULLSCREEN_BUTTON_X, STOP_BUTTON_Y), SMALL_BUTTON_SIZE);
            _bitmapFullscreenDown = _pictureBoxFullscreenDown.Image as Bitmap;

            _pictureBoxFullscreenOver = CreateControlPictureBox("pictureBoxFSOver.Image", "pictureBoxFullscreenOver", new Point(FULLSCREEN_BUTTON_X, STOP_BUTTON_Y), SMALL_BUTTON_SIZE);
            _pictureBoxFullscreenOver.MouseLeave += PictureBoxFullscreenOverMouseLeave;
            _pictureBoxFullscreenOver.MouseDown += PictureBoxFullscreenOverMouseDown;
            _pictureBoxFullscreenOver.MouseUp += PictureBoxFullscreenOverMouseUp;
            _bitmapFullscreenOver = _pictureBoxFullscreenOver.Image as Bitmap;

            // Initialize no-fullscreen bitmaps
            _bitmapNoFullscreen = (Image)_resources.GetObject("pictureBoxNoFS.Image") as Bitmap;
            _bitmapNoFullscreenDown = (Image)_resources.GetObject("pictureBoxNoFSDown.Image") as Bitmap;
            _bitmapNoFullscreenOver = (Image)_resources.GetObject("pictureBoxNoFSOver.Image") as Bitmap;

            // Initialize progress bar controls
            InitializeProgressBarControls();

            // Initialize mute and volume controls
            InitializeVolumeControls();

            _pictureBoxReverse = new PictureBox
            {
                Image = (Image)_resources.GetObject("pictureBoxReverse.Image"),
                Location = new Point(28, 3),
                Name = "_pictureBoxReverse",
                Size = new Size(16, 8),
                SizeMode = PictureBoxSizeMode.AutoSize,
                TabStop = false
            };
            _panelControls.Controls.Add(_pictureBoxReverse);
            _pictureBoxReverse.MouseEnter += PictureBoxReverseMouseEnter;

            _pictureBoxReverseOver = new PictureBox
            {
                Image = (Image)_resources.GetObject("pictureBoxReverseMouseOver.Image"),
                Location = _pictureBoxReverse.Location,
                Name = "_pictureBoxReverseOver",
                Size = _pictureBoxReverse.Size,
                SizeMode = PictureBoxSizeMode.AutoSize,
                TabStop = false
            };
            _panelControls.Controls.Add(_pictureBoxReverseOver);
            _pictureBoxReverseOver.MouseLeave += PictureBoxReverseOverMouseLeave;
            _pictureBoxReverseOver.MouseDown += PictureBoxReverseOverMouseDown;
            _pictureBoxReverseOver.MouseUp += PictureBoxReverseOverMouseUp;

            _pictureBoxReverseDown = new PictureBox
            {
                Image = (Image)_resources.GetObject("pictureBoxReverseMouseDown.Image"),
                Location = _pictureBoxReverse.Location,
                Name = "_pictureBoxReverseOver",
                Size = _pictureBoxReverse.Size,
                SizeMode = PictureBoxSizeMode.AutoSize,
                TabStop = false
            };
            _panelControls.Controls.Add(_pictureBoxReverseDown);

            _pictureBoxFastForward = new PictureBox
            {
                Image = (Image)_resources.GetObject("pictureBoxFastForward.Image"),
                Location = new Point(571, 1),
                Name = "_pictureBoxFastForward",
                Size = new Size(17, 13),
                SizeMode = PictureBoxSizeMode.AutoSize,
                TabStop = false
            };
            _panelControls.Controls.Add(_pictureBoxFastForward);
            _pictureBoxFastForward.MouseEnter += PictureBoxFastForwardMouseEnter;

            _pictureBoxFastForwardOver = new PictureBox
            {
                Image = (Image)_resources.GetObject("pictureBoxFastForwardMouseOver.Image"),
                Location = _pictureBoxFastForward.Location,
                Name = "_pictureBoxFastForwardOver",
                Size = _pictureBoxFastForward.Size,
                SizeMode = PictureBoxSizeMode.AutoSize,
                TabStop = false
            };
            _panelControls.Controls.Add(_pictureBoxFastForwardOver);
            _pictureBoxFastForwardOver.MouseLeave += PictureBoxFastForwardOverMouseLeave;
            _pictureBoxFastForwardOver.MouseDown += PictureBoxFastForwardOverMouseDown;
            _pictureBoxFastForwardOver.MouseUp += PictureBoxFastForwardOverMouseUp;

            _pictureBoxFastForwardDown = new PictureBox
            {
                Image = (Image)_resources.GetObject("pictureBoxFastForwardMouseDown.Image"),
                Location = _pictureBoxFastForward.Location,
                Name = "_pictureBoxFastForwardDown",
                Size = _pictureBoxFastForward.Size,
                SizeMode = PictureBoxSizeMode.AutoSize,
                TabStop = false
            };
            _panelControls.Controls.Add(_pictureBoxFastForwardDown);

            _labelVolume.Location = new Point(120, 16);
            _labelVolume.ForeColor = Color.WhiteSmoke;
            _labelVolume.BackColor = Color.FromArgb(67, 75, 93);
            _labelVolume.AutoSize = true;
            _labelVolume.Font = new Font(_labelTimeCode.Font.FontFamily, 6);
            _panelControls.Controls.Add(_labelVolume);

            _labelTimeCode.Location = new Point(280, 28);
            _labelTimeCode.ForeColor = Color.WhiteSmoke;
            _labelTimeCode.Font = new Font(_labelTimeCode.Font.FontFamily, 8, FontStyle.Bold);
            _labelTimeCode.AutoSize = true;
            _panelControls.Controls.Add(_labelTimeCode);

            _labelVideoPlayerName.Location = new Point(282, 17);
            _labelVideoPlayerName.ForeColor = Color.WhiteSmoke;
            _labelVideoPlayerName.BackColor = Color.FromArgb(67, 75, 93);
            _labelVideoPlayerName.AutoSize = true;
            _labelVideoPlayerName.Font = new Font(_labelTimeCode.Font.FontFamily, 6);
            _panelControls.Controls.Add(_labelVideoPlayerName);

            var bg = _pictureBoxBackground.Image as Bitmap;
            _labelVolume.BackColor = bg.GetPixel(_labelVolume.Left, _labelVolume.Top);
            _labelTimeCode.BackColor = bg.GetPixel(_labelTimeCode.Left, _labelTimeCode.Top);
            _labelVideoPlayerName.BackColor = bg.GetPixel(_labelVideoPlayerName.Left, _labelVideoPlayerName.Top);

            _pictureBoxBackground.SendToBack();
            _pictureBoxFastForwardDown.BringToFront();
            _pictureBoxFastForwardOver.BringToFront();
            _pictureBoxFastForward.BringToFront();
            _pictureBoxPlay.BringToFront();

            _panelControls.BackColor = _backgroundColor;
            _pictureBoxPlayDown.BringToFront();
            _pictureBoxPlayOver.BringToFront();
            _pictureBoxPlay.BringToFront();
            _labelTimeCode.BringToFront();
            _labelVolume.BringToFront();
            return _panelControls;
        }
    }
}