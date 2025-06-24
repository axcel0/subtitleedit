using Nikse.SubtitleEdit.Core.Common;
using QuartzTypeLib;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

//https://docs.microsoft.com/en-us/windows/win32/directshow/directshow
//https://docs.microsoft.com/en-us/previous-versions//dd387928(v=vs.85)

namespace Nikse.SubtitleEdit.Logic.VideoPlayers
{
    public sealed class QuartsPlayer : VideoPlayer, IDisposable
    {
        private const int WsChild = 0x40000000;
        private const int VolumeMultiplier = 35;
        private const int VolumeFull = 100;
        private const int VolumeSilent = -10000;
        private const int DefaultTimerInterval = 500;
        private const double MaxPlayRate = 3.0;

        private IVideoWindow _quartzVideo;
        private FilgraphManager _quartzFilgraphManager;
        private IMediaPosition _mediaPosition;
        private bool _isPaused;
        private Control _owner;
        private Timer _videoEndTimer;
        private BackgroundWorker _videoLoader;
        private int _sourceWidth;
        private int _sourceHeight;
        private bool _disposed;

        public override string PlayerName => "DirectShow";

        public override event EventHandler OnVideoLoaded;
        public override event EventHandler OnVideoEnded;

        public override string PlayerName => "DirectShow";

        /// <summary>
        /// In DirectX -10000 is silent and 0 is full volume.
        /// Also, -3500 to 0 seems to be all you can hear! Not much use for -3500 to -9999...
        /// </summary>
        public override int Volume
        {
            get
            {
                if (_disposed || _quartzFilgraphManager == null)
                    return 0;

                try
                {
                    return ((IBasicAudio)_quartzFilgraphManager).Volume / VolumeMultiplier + VolumeFull;
                }
                catch (Exception ex)
                {
                    SeLogger.Error(ex, "Error getting DirectShow volume");
                    return 0;
                }
            }
            set
            {
                if (_disposed || _quartzFilgraphManager == null)
                    return;

                try
                {
                    var volume = value == 0 ? VolumeSilent : (value - VolumeFull) * VolumeMultiplier;
                    ((IBasicAudio)_quartzFilgraphManager).Volume = volume;
                }
                catch (Exception ex)
                {
                    SeLogger.Error(ex, "Error setting DirectShow volume");
                }
            }
        }

        public override double Duration
        {
            get
            {
                if (_disposed || _mediaPosition == null)
                    return 0;

                try
                {
                    return _mediaPosition.Duration;
                }
                catch (Exception ex)
                {
                    SeLogger.Error(ex, "Error getting DirectShow duration");
                    return 0;
                }
            }
        }

        public override double CurrentPosition
        {
            get
            {
                if (_disposed || _mediaPosition == null)
                    return 0;

                try
                {
                    return _mediaPosition.CurrentPosition;
                }
                catch (Exception ex)
                {
                    SeLogger.Error(ex, "Error getting DirectShow position");
                    return 0;
                }
            }
            set
            {
                if (_disposed || _mediaPosition == null || value < 0)
                    return;

                try
                {
                    var duration = Duration;
                    if (value <= duration)
                    {
                        _mediaPosition.CurrentPosition = value;
                    }
                }
                catch (Exception ex)
                {
                    SeLogger.Error(ex, "Error setting DirectShow position");
                }
            }
        }

        public override double PlayRate
        {
            get
            {
                if (_disposed || _mediaPosition == null)
                    return 1.0;

                try
                {
                    return _mediaPosition.Rate;
                }
                catch (Exception ex)
                {
                    SeLogger.Error(ex, "Error getting DirectShow play rate");
                    return 1.0;
                }
            }
            set
            {
                if (_disposed || _mediaPosition == null || value < 0 || value > MaxPlayRate)
                    return;

                try
                {
                    _mediaPosition.Rate = value;
                }
                catch (Exception ex)
                {
                    SeLogger.Error(ex, "Error setting DirectShow play rate");
                }
            }
        }

        public override void Play()
        {
            if (_disposed || _quartzFilgraphManager == null)
                return;

            try
            {
                _quartzFilgraphManager.Run();
                _isPaused = false;
            }
            catch (Exception ex)
            {
                SeLogger.Error(ex, "Error playing DirectShow video");
            }
        }

        public override void Pause()
        {
            if (_disposed || _quartzFilgraphManager == null)
                return;

            try
            {
                _quartzFilgraphManager.Pause();
                _isPaused = true;
            }
            catch (Exception ex)
            {
                SeLogger.Error(ex, "Error pausing DirectShow video");
            }
        }

        public override void Stop()
        {
            if (_disposed || _quartzFilgraphManager == null)
                return;

            try
            {
                _quartzFilgraphManager.Stop();
                _isPaused = true;
            }
            catch (Exception ex)
            {
                SeLogger.Error(ex, "Error stopping DirectShow video");
            }
        }

        public override bool IsPaused => _isPaused;

        public override bool IsPlaying => !IsPaused;

        public override void Initialize(Control ownerControl, string videoFileName, EventHandler onVideoLoaded, EventHandler onVideoEnded)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(QuartsPlayer));

            if (ownerControl?.Handle == IntPtr.Zero)
                throw new ArgumentException("Owner control must be valid and have a handle", nameof(ownerControl));

            if (string.IsNullOrEmpty(videoFileName) || !File.Exists(videoFileName))
                throw new FileNotFoundException("Video file not found", videoFileName);

            var ext = Path.GetExtension(videoFileName)?.ToLowerInvariant();
            var isAudio = !string.IsNullOrEmpty(ext) && Utilities.AudioFileExtensions.Contains(ext);

            OnVideoLoaded = onVideoLoaded;
            OnVideoEnded = onVideoEnded;
            VideoFileName = videoFileName;
            _owner = ownerControl;

            try
            {
                // Hack for Windows 10 DirectShow issues
                if (!isAudio && Configuration.Settings.General.DirectShowDoubleLoad)
                {
                    PerformDoubleLoad(videoFileName);
                }

                _quartzFilgraphManager = new FilgraphManager();
                _quartzFilgraphManager.RenderFile(VideoFileName);

                if (!isAudio)
                {
                    SetupVideoWindow(ownerControl);
                    GetVideoSize();
                }

                SetupEvents(ownerControl, isAudio);
            }
            catch (Exception ex)
            {
                SeLogger.Error(ex, "Error initializing DirectShow player");
                throw;
            }
        }

        private void PerformDoubleLoad(string videoFileName)
        {
            try
            {
                var quartzFilterGraphManager = new FilgraphManager();
                quartzFilterGraphManager.RenderFile(videoFileName);
                if (quartzFilterGraphManager is IVideoWindow quartzVideo)
                {
                    quartzVideo.Visible = 0;
                    quartzVideo.Owner = (int)IntPtr.Zero;
                }
                Marshal.ReleaseComObject(quartzFilterGraphManager);
            }
            catch (Exception ex)
            {
                SeLogger.Error(ex, "Error during DirectShow double load");
            }
        }

        private void SetupVideoWindow(Control ownerControl)
        {
            _quartzVideo = _quartzFilgraphManager as IVideoWindow;
            if (_quartzVideo != null)
            {
                _quartzVideo.Owner = (int)ownerControl.Handle;
                _quartzVideo.SetWindowPosition(0, 0, ownerControl.Width, ownerControl.Height);
                _quartzVideo.WindowStyle = WsChild;
            }
        }

        private void GetVideoSize()
        {
            if (_quartzFilgraphManager is IBasicVideo basicVideo)
            {
                basicVideo.GetVideoSize(out _sourceWidth, out _sourceHeight);
            }
        }

        private void SetupEvents(Control ownerControl, bool isAudio)
        {
            _owner.Resize += OwnerControlResize;
            _mediaPosition = (IMediaPosition)_quartzFilgraphManager;

            if (OnVideoLoaded != null)
            {
                _videoLoader = new BackgroundWorker();
                _videoLoader.RunWorkerCompleted += VideoLoaderRunWorkerCompleted;
                _videoLoader.DoWork += VideoLoaderDoWork;
                _videoLoader.RunWorkerAsync();
            }

            OwnerControlResize(this, null);
            _videoEndTimer = new Timer { Interval = DefaultTimerInterval };
            _videoEndTimer.Tick += VideoEndTimerTick;
            _videoEndTimer.Start();

            if (!isAudio && _quartzVideo != null)
            {
                _quartzVideo.MessageDrain = (int)ownerControl.Handle;
            }
        }

        public static VideoInfo GetVideoInfo(string videoFileName)
        {
            var info = new VideoInfo { Success = false };

            if (string.IsNullOrEmpty(videoFileName) || !File.Exists(videoFileName))
            {
                return info;
            }

            FilgraphManager quartzFilgraphManager = null;
            try
            {
                quartzFilgraphManager = new FilgraphManager();
                quartzFilgraphManager.RenderFile(videoFileName);

                if (quartzFilgraphManager is IBasicVideo basicVideo)
                {
                    basicVideo.GetVideoSize(out var width, out var height);
                    info.Width = width;
                    info.Height = height;
                }

                if (quartzFilgraphManager is IBasicVideo2 basicVideo2 && basicVideo2.AvgTimePerFrame > 0)
                {
                    info.FramesPerSecond = 1 / basicVideo2.AvgTimePerFrame;
                }

                if (quartzFilgraphManager is IMediaPosition mediaPosition)
                {
                    info.TotalSeconds = mediaPosition.Duration;
                    info.TotalMilliseconds = mediaPosition.Duration * 1000;
                    info.TotalFrames = info.TotalSeconds * info.FramesPerSecond;
                }

                info.VideoCodec = string.Empty; // TODO: Get real codec names from quartzFilgraphManager.FilterCollection
                info.Success = true;
            }
            catch (Exception ex)
            {
                SeLogger.Error(ex, $"Error getting video info for {videoFileName}");
            }
            finally
            {
                if (quartzFilgraphManager != null)
                {
                    try
                    {
                        Marshal.ReleaseComObject(quartzFilgraphManager);
                    }
                    catch (Exception ex)
                    {
                        SeLogger.Error(ex, "Error releasing DirectShow COM object");
                    }
                }
            }

            return info;
        }

        private void VideoLoaderDoWork(object sender, DoWorkEventArgs e)
        {
            Application.DoEvents();
        }

        private void VideoLoaderRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {
                OnVideoLoaded?.Invoke(_quartzFilgraphManager, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                SeLogger.Error(ex, "Error in video loaded callback");
            }
        }

        private void VideoEndTimerTick(object sender, EventArgs e)
        {
            if (_disposed || _isPaused || _quartzFilgraphManager == null)
                return;

            try
            {
                if (CurrentPosition >= Duration)
                {
                    _isPaused = true;
                    OnVideoEnded?.Invoke(_quartzFilgraphManager, EventArgs.Empty);
                }
            }
            catch (Exception ex)
            {
                SeLogger.Error(ex, "Error in video end timer");
            }
        }

        private void OwnerControlResize(object sender, EventArgs e)
        {
            if (_disposed || _quartzVideo == null || _owner == null || _sourceWidth <= 0 || _sourceHeight <= 0)
                return;

            try
            {
                // Calculate new scaled size with correct aspect ratio
                float factorX = _owner.Width / (float)_sourceWidth;
                float factorY = _owner.Height / (float)_sourceHeight;
                float factor = Math.Min(factorX, factorY);

                var newWidth = (int)(_sourceWidth * factor);
                var newHeight = (int)(_sourceHeight * factor);

                _quartzVideo.Width = newWidth;
                _quartzVideo.Height = newHeight;
                _quartzVideo.Left = (_owner.Width - newWidth) / 2;
                _quartzVideo.Top = (_owner.Height - newHeight) / 2;
            }
            catch (Exception ex)
            {
                SeLogger.Error(ex, "Error resizing DirectShow video window");
            }
        }

        public override void DisposeVideoPlayer()
        {
            if (!_disposed)
            {
                ThreadPool.QueueUserWorkItem(DisposeQuarts, _quartzFilgraphManager);
            }
        }

        private static void DisposeQuarts(object player)
        {
            // Method stub for background disposal
        }

        private void ReleaseUnmanagedResources()
        {
            try
            {
                if (_quartzVideo != null)
                {
                    _quartzVideo.Owner = -1;
                    _quartzVideo = null;
                }

                if (_quartzFilgraphManager != null)
                {
                    try
                    {
                        _quartzFilgraphManager.Stop();
                    }
                    catch (Exception ex)
                    {
                        SeLogger.Error(ex, "Error stopping DirectShow filter graph");
                    }

                    try
                    {
                        Marshal.ReleaseComObject(_quartzFilgraphManager);
                    }
                    catch (Exception ex)
                    {
                        SeLogger.Error(ex, "Error releasing DirectShow COM object");
                    }
                    finally
                    {
                        _quartzFilgraphManager = null;
                    }
                }

                _mediaPosition = null;
            }
            catch (Exception ex)
            {
                SeLogger.Error(ex, "Error releasing DirectShow unmanaged resources");
            }
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _videoEndTimer?.Stop();
                _videoEndTimer?.Dispose();
                _videoEndTimer = null;

                _videoLoader?.Dispose();
                _videoLoader = null;

                if (_owner != null)
                {
                    _owner.Resize -= OwnerControlResize;
                    _owner = null;
                }
            }

            ReleaseUnmanagedResources();
            _disposed = true;
        }

        ~QuartsPlayer()
        {
            Dispose(false);
        }
    }
}
