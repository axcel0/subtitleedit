using Nikse.SubtitleEdit.Core.Common;
using System;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Logic.VideoPlayers
{
    internal sealed class LibVlcMono : VideoPlayer, IDisposable
    {
        private const int DefaultTimerInterval = 500;
        private const int MaxLoadAttempts = 50;
        private const int LoadAttemptDelay = 100;
        private const int PausedState = 4;
        private const int PlayingState = 3;
        private const int EndedState = 6;
        private const double MaxPlayRate = 2.0;

        private Timer _videoLoadedTimer;
        private Timer _videoEndTimer;
        private IntPtr _libVlcDll;
        private IntPtr _libVlc;
        private IntPtr _mediaPlayer;
        private Control _ownerControl;
        private Form _parentForm;
        private bool _disposed;

        public override string PlayerName => "VLC Lib Mono";

        public override int Volume
        {
            get
            {
                if (_mediaPlayer == IntPtr.Zero)
                    return 0;
                return NativeMethods.libvlc_audio_get_volume(_mediaPlayer);
            }
            set
            {
                if (_mediaPlayer != IntPtr.Zero)
                {
                    NativeMethods.libvlc_audio_set_volume(_mediaPlayer, value);
                }
            }
        }

        public override double Duration
        {
            get
            {
                if (_mediaPlayer == IntPtr.Zero)
                    return 0;
                return NativeMethods.libvlc_media_player_get_length(_mediaPlayer) / TimeCode.BaseUnit;
            }
        }

        public override double CurrentPosition
        {
            get
            {
                if (_mediaPlayer == IntPtr.Zero)
                    return 0;
                return NativeMethods.libvlc_media_player_get_time(_mediaPlayer) / TimeCode.BaseUnit;
            }
            set
            {
                if (_mediaPlayer != IntPtr.Zero && value >= 0)
                {
                    NativeMethods.libvlc_media_player_set_time(_mediaPlayer, (long)(value * TimeCode.BaseUnit));
                }
            }
        }

        public override double PlayRate
        {
            get
            {
                if (_mediaPlayer == IntPtr.Zero)
                    return 1.0;
                return NativeMethods.libvlc_media_player_get_rate(_mediaPlayer);
            }
            set
            {
                if (_mediaPlayer != IntPtr.Zero && value >= 0 && value <= MaxPlayRate)
                {
                    NativeMethods.libvlc_media_player_set_rate(_mediaPlayer, (float)value);
                }
            }
        }

        public override void Play()
        {
            if (_disposed || _mediaPlayer == IntPtr.Zero)
                return;
            NativeMethods.libvlc_media_player_play(_mediaPlayer);
        }

        public override void Pause()
        {
            if (_disposed || _mediaPlayer == IntPtr.Zero || IsPaused)
                return;
            NativeMethods.libvlc_media_player_pause(_mediaPlayer);
        }

        public override void Stop()
        {
            if (_disposed || _mediaPlayer == IntPtr.Zero)
                return;
            NativeMethods.libvlc_media_player_stop(_mediaPlayer);
        }

        public override bool IsPaused
        {
            get
            {
                if (_mediaPlayer == IntPtr.Zero)
                    return true;
                int state = NativeMethods.libvlc_media_player_get_state(_mediaPlayer);
                return state == PausedState;
            }
        }

        public override bool IsPlaying
        {
            get
            {
                if (_mediaPlayer == IntPtr.Zero)
                    return false;
                int state = NativeMethods.libvlc_media_player_get_state(_mediaPlayer);
                return state == PlayingState;
            }
        }

        public int AudioTrackCount
        {
            get
            {
                if (_mediaPlayer == IntPtr.Zero)
                    return 0;
                return Math.Max(0, NativeMethods.libvlc_audio_get_track_count(_mediaPlayer) - 1);
            }
        }

        public int AudioTrackNumber
        {
            get
            {
                if (_mediaPlayer == IntPtr.Zero)
                    return 0;
                return Math.Max(0, NativeMethods.libvlc_audio_get_track(_mediaPlayer) - 1);
            }
            set
            {
                if (_mediaPlayer != IntPtr.Zero && value >= 0)
                {
                    NativeMethods.libvlc_audio_set_track(_mediaPlayer, value + 1);
                }
            }
        }

        public LibVlcMono MakeSecondMediaPlayer(Control ownerControl, string videoFileName, EventHandler onVideoLoaded, EventHandler onVideoEnded)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LibVlcMono));

            var newVlc = new LibVlcMono
            {
                _libVlc = _libVlc,
                _libVlcDll = _libVlcDll,
                _ownerControl = ownerControl,
                _parentForm = ownerControl?.FindForm(),
                OnVideoLoaded = onVideoLoaded,
                OnVideoEnded = onVideoEnded
            };

            if (!string.IsNullOrEmpty(videoFileName))
            {
                var videoFileNameBytes = Encoding.UTF8.GetBytes(videoFileName + "\0");
                IntPtr media = NativeMethods.libvlc_media_new_path(_libVlc, videoFileNameBytes);
                newVlc._mediaPlayer = NativeMethods.libvlc_media_player_new_from_media(media);
                NativeMethods.libvlc_media_release(media);

                var ownerHandle = ownerControl?.Handle ?? IntPtr.Zero;
                NativeMethods.libvlc_media_player_set_hwnd(newVlc._mediaPlayer, ownerHandle);

                if (onVideoEnded != null)
                {
                    newVlc._videoEndTimer = new Timer { Interval = DefaultTimerInterval };
                    newVlc._videoEndTimer.Tick += VideoEndTimerTick;
                    newVlc._videoEndTimer.Start();
                }

                NativeMethods.libvlc_media_player_play(newVlc._mediaPlayer);
                newVlc._videoLoadedTimer = new Timer { Interval = DefaultTimerInterval };
                newVlc._videoLoadedTimer.Tick += newVlc.VideoLoadedTimer_Tick;
                newVlc._videoLoadedTimer.Start();
                newVlc.VideoFileName = videoFileName;
            }

            return newVlc;
        }

        private void VideoLoadedTimer_Tick(object sender, EventArgs e)
        {
            var attempts = 0;
            while (!IsPlaying && attempts < MaxLoadAttempts)
            {
                Thread.Sleep(LoadAttemptDelay);
                attempts++;
            }
            
            if (_mediaPlayer != IntPtr.Zero)
            {
                NativeMethods.libvlc_media_player_pause(_mediaPlayer);
            }
            
            _videoLoadedTimer?.Stop();
            OnVideoLoaded?.Invoke(_mediaPlayer, EventArgs.Empty);
        }

        public override void Initialize(Control ownerControl, string videoFileName, EventHandler onVideoLoaded, EventHandler onVideoEnded)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(LibVlcMono));

            _ownerControl = ownerControl;
            _parentForm = ownerControl?.FindForm();
            OnVideoLoaded = onVideoLoaded;
            OnVideoEnded = onVideoEnded;

            if (!string.IsNullOrEmpty(videoFileName))
            {
                string[] initParameters = { "--no-sub-autodetect-file" };
                _libVlc = NativeMethods.libvlc_new(initParameters.Length, initParameters);
                
                var videoFileNameBytes = Encoding.UTF8.GetBytes(videoFileName + "\0");
                IntPtr media = NativeMethods.libvlc_media_new_path(_libVlc, videoFileNameBytes);
                _mediaPlayer = NativeMethods.libvlc_media_player_new_from_media(media);
                NativeMethods.libvlc_media_release(media);

                var ownerHandle = ownerControl?.Handle ?? IntPtr.Zero;
                NativeMethods.libvlc_media_player_set_hwnd(_mediaPlayer, ownerHandle);

                if (onVideoEnded != null)
                {
                    _videoEndTimer = new Timer { Interval = DefaultTimerInterval };
                    _videoEndTimer.Tick += VideoEndTimerTick;
                    _videoEndTimer.Start();
                }

                NativeMethods.libvlc_media_player_play(_mediaPlayer);
                _videoLoadedTimer = new Timer { Interval = DefaultTimerInterval };
                _videoLoadedTimer.Tick += VideoLoadedTimer_Tick;
                _videoLoadedTimer.Start();
                VideoFileName = videoFileName;
            }
        }

        private void VideoEndTimerTick(object sender, EventArgs e)
        {
            if (_mediaPlayer == IntPtr.Zero)
                return;

            int state = NativeMethods.libvlc_media_player_get_state(_mediaPlayer);
            if (state == EndedState)
            {
                // Hack to make sure VLC is in ready state
                Stop();
                Play();
                Pause();
                OnVideoEnded?.Invoke(_mediaPlayer, EventArgs.Empty);
            }
        }

        public override void DisposeVideoPlayer()
        {
            if (_disposed)
                return;

            _videoLoadedTimer?.Stop();
            _videoEndTimer?.Stop();
            ThreadPool.QueueUserWorkItem(DisposeVLC, this);
        }

        private static void DisposeVLC(object player)
        {
            if (player is LibVlcMono vlcPlayer)
            {
                vlcPlayer.ReleaseUnmanagedResources();
            }
        }

        public override event EventHandler OnVideoLoaded;
        public override event EventHandler OnVideoEnded;

        ~LibVlcMono()
        {
            Dispose(false);
        }

        private void ReleaseUnmanagedResources()
        {
            try
            {
                if (_mediaPlayer != IntPtr.Zero)
                {
                    NativeMethods.libvlc_media_player_stop(_mediaPlayer);
                    NativeMethods.libvlc_media_player_release(_mediaPlayer);
                    _mediaPlayer = IntPtr.Zero;
                }

                if (_libVlc != IntPtr.Zero)
                {
                    NativeMethods.libvlc_release(_libVlc);
                    _libVlc = IntPtr.Zero;
                }
            }
            catch (Exception ex)
            {
                // Log but don't throw during cleanup
                SeLogger.Error(ex, "Error releasing VLC resources");
            }
        }

        public void Dispose()
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
                _videoLoadedTimer?.Stop();
                _videoLoadedTimer?.Dispose();
                _videoLoadedTimer = null;

                _videoEndTimer?.Stop();
                _videoEndTimer?.Dispose();
                _videoEndTimer = null;
            }

            ReleaseUnmanagedResources();
            _disposed = true;
        }
    }
}
