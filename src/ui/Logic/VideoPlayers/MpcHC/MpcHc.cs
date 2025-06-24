using Nikse.SubtitleEdit.Core.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Nikse.SubtitleEdit.Logic.VideoPlayers.MpcHC
{
    public sealed class MpcHc : VideoPlayer, IDisposable
    {
        private readonly object _locker = new();
        private readonly StringBuilder _classNameBuffer = new(256);

        private const string ModePlay = "0";
        private const string ModePause = "1";
        private const int PositionTimerInterval = 100;
        private const int HideMpcTimerInterval = 100;
        private const int HideMpcTimerMaxCount = 20;
        private const double DefaultDuration = 5000.0;
        private const double MinPlayRate = 0.0;
        private const double MaxPlayRate = 3.0;
        private const int VolumeStep = 5;
        private const int MaxVolume = 100;

        // MPC-HC video window class name patterns
        private static readonly string[] VideoClassPatterns = 
        {
            ":b:0000000000010003:0000000000000006:0000000000000000", // MPC-HC 64-bit
            ":b:0000000000010004:0000000000000006:0000000000000000", // MPC-HC 64-bit
            ":b:0000000000010005:0000000000000006:0000000000000000", // MPC-HC 64-bit
            ":b:0000000000010006:0000000000000006:0000000000000000", // MPC-HC 64-bit
            ":b:0000000000010007:0000000000000006:0000000000000000", // MPC-HC 64-bit
            ":b:00010003:00000006:00000000"                          // MPC-HC 32-bit
        };

        private string _playMode = string.Empty;
        private int _loaded;
        private IntPtr _mpcHandle = IntPtr.Zero;
        private IntPtr _videoHandle = IntPtr.Zero;
        private IntPtr _videoPanelHandle = IntPtr.Zero;
        private ProcessStartInfo _startInfo;
        private Process _process;
        private IntPtr _messageHandlerHandle = IntPtr.Zero;
        private string _videoFileName;
        private Timer _positionTimer;
        private double _positionInSeconds;
        private double _durationInSeconds;
        private double _currentPlayRate = 1.0;
        private MessageHandlerWindow _form;
        private int _initialWidth;
        private int _initialHeight;
        private Timer _hideMpcTimer;
        private int _hideMpcTimerCount;
        private bool _disposed;

        public override string PlayerName => "MPC-HC";

        private int _volume = 75;

        public override int Volume
        {
            get => _volume;
            set
            {
                if (_disposed || value < 0 || value > MaxVolume)
                    return;

                var targetVolume = (value / VolumeStep) * VolumeStep; // Round to nearest step
                
                // Reset volume to 0
                var currentStep = _volume / VolumeStep;
                for (int i = 0; i < currentStep; i++)
                {
                    SendMpcMessage(MpcHcCommand.DecreaseVolume);
                }

                // Set to target volume
                var targetSteps = targetVolume / VolumeStep;
                for (int i = 0; i < targetSteps; i++)
                {
                    SendMpcMessage(MpcHcCommand.IncreaseVolume);
                }

                _volume = targetVolume;
            }
        }

        public override double Duration => _durationInSeconds;

        public override double CurrentPosition
        {
            get => _positionInSeconds;
            set
            {
                if (_disposed) return;
                SendMpcMessage(MpcHcCommand.SetPosition, value.ToString("0.000", CultureInfo.InvariantCulture));
            }
        }

        public override double PlayRate
        {
            get => _currentPlayRate;
            set
            {
                if (_disposed) return;
                if (value >= MinPlayRate && value <= MaxPlayRate)
                {
                    SendMpcMessage(MpcHcCommand.SetSpeed, value.ToString(CultureInfo.InvariantCulture));
                    _currentPlayRate = value;
                }
            }
        }

        public override void Play()
        {
            if (_disposed) return;
            _playMode = ModePlay;
            SendMpcMessage(MpcHcCommand.Play);
        }

        public override void Pause()
        {
            if (_disposed) return;
            _playMode = ModePause;
            SendMpcMessage(MpcHcCommand.Pause);
        }

        public override void Stop()
        {
            if (_disposed) return;
            SendMpcMessage(MpcHcCommand.Stop);
        }

        public override bool IsPaused => _playMode == ModePause;

        public override bool IsPlaying => _playMode == ModePlay;

        public override void Initialize(Control ownerControl, string videoFileName, EventHandler onVideoLoaded, EventHandler onVideoEnded)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MpcHc));
                
            if (ownerControl?.Handle == IntPtr.Zero)
            {
                throw new ArgumentException("Owner control must be valid and have a handle", nameof(ownerControl));
            }

            VideoFileName = videoFileName;
            OnVideoLoaded = onVideoLoaded;
            OnVideoEnded = onVideoEnded;

            _initialWidth = ownerControl.Width;
            _initialHeight = ownerControl.Height;
            _form = new MessageHandlerWindow();
            _form.OnCopyData += OnCopyData;
            _form.Show();
            _form.Hide();
            _videoPanelHandle = ownerControl.Handle;
            _messageHandlerHandle = _form.Handle;
            _videoFileName = videoFileName;
            
            _startInfo = new ProcessStartInfo
            {
                FileName = GetMpcFileName(),
                Arguments = $"/new /minimized /slave {_messageHandlerHandle}",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            _process = Process.Start(_startInfo);
            _process?.WaitForInputIdle();

            _positionTimer = new Timer { Interval = PositionTimerInterval };
            _positionTimer.Tick += PositionTimerTick;
        }

        private void PositionTimerTick(object sender, EventArgs e)
        {
            SendMpcMessage(MpcHcCommand.GetCurrentPosition);
        }

        private void OnCopyData(object sender, EventArgs e)
        {
            var message = (Message)sender;
            var cds = (NativeMethods.CopyDataStruct)Marshal.PtrToStructure(message.LParam, typeof(NativeMethods.CopyDataStruct));
            var command = cds.dwData.ToUInt32();
            var param = Marshal.PtrToStringAuto(cds.lpData);
            string[] multiParam = param?.Split('|') ?? Array.Empty<string>();

            switch (command)
            {
                case MpcHcCommand.Connect:
                    _positionTimer.Stop();
                    _mpcHandle = (IntPtr)Convert.ToInt64(Marshal.PtrToStringAuto(cds.lpData));
                    SendMpcMessage(MpcHcCommand.OpenFile, _videoFileName);
                    _positionTimer.Start();
                    break;
                    
                case MpcHcCommand.PlayMode:
                    _playMode = param ?? string.Empty;
                    if (param == ModePlay && _loaded == 0)
                    {
                        _loaded = 1;
                        if (!HijackMpcHc())
                        {
                            Application.DoEvents();
                            HijackMpcHc();
                        }
                    }
                    Application.DoEvents();
                    HideMpcPlayerWindow();
                    break;
                    
                case MpcHcCommand.NowPlaying:
                    if (_loaded == 1)
                    {
                        _loaded = 2;

                        _durationInSeconds = DefaultDuration;
                        if (multiParam.Length >= 5 && TryParseDouble(multiParam[4], out var d))
                        {
                            _durationInSeconds = d;
                        }
                        else if (multiParam.Length >= 1 && TryParseDouble(multiParam[^1], out d))
                        {
                            _durationInSeconds = d;
                        }

                        Resize(_initialWidth, _initialHeight);
                        OnVideoLoaded?.Invoke(this, EventArgs.Empty);

                        SendMpcMessage(MpcHcCommand.SetSubtitleTrack, "-1");

                        // Ensure MPC is hidden
                        StartHideMpcTimer();
                        Pause();
                    }
                    break;
                    
                case MpcHcCommand.NotifyEndOfStream:
                    OnVideoEnded?.Invoke(this, EventArgs.Empty);
                    break;
                    
                case MpcHcCommand.CurrentPosition:
                    if (!string.IsNullOrWhiteSpace(param) && TryParseDouble(param, out var position))
                    {
                        _positionInSeconds = position;
                    }
                    break;
            }
        }

        private static bool TryParseDouble(string input, out double result)
        {
            return double.TryParse(input?.Replace(",", ".").Trim(), 
                NumberStyles.AllowDecimalPoint, 
                CultureInfo.InvariantCulture, 
                out result);
        }

        private void StartHideMpcTimer()
        {
            _hideMpcTimerCount = HideMpcTimerMaxCount;
            _hideMpcTimer = new Timer { Interval = HideMpcTimerInterval };
            _hideMpcTimer.Tick += (o, args) =>
            {
                _hideMpcTimer.Stop();
                if (_hideMpcTimerCount > 0)
                {
                    Application.DoEvents();
                    HideMpcPlayerWindow();
                    _hideMpcTimerCount--;
                    _hideMpcTimer.Start();
                }
                else
                {
                    _hideMpcTimer?.Dispose();
                    _hideMpcTimer = null;
                }
            };
            _hideMpcTimer.Start();
        }

        private void HideMpcPlayerWindow()
        {
            NativeMethods.ShowWindow(_process.MainWindowHandle, NativeMethods.ShowWindowCommands.Hide);
            NativeMethods.SetWindowPos(_process.MainWindowHandle, (IntPtr)NativeMethods.SpecialWindowHandles.HWND_TOP, -9999, -9999, 0, 0, NativeMethods.SetWindowPosFlags.SWP_NOACTIVATE);
        }

        internal static bool GetWindowHandle(IntPtr windowHandle, IList<IntPtr> windowHandles)
        {
            windowHandles.Add(windowHandle);
            return true;
        }

        private IList<IntPtr> GetChildWindows()
        {
            var windowHandles = new List<IntPtr>();
            NativeMethods.EnumedWindow callBackPtr = GetWindowHandle;
            NativeMethods.EnumChildWindows(_process.MainWindowHandle, callBackPtr, windowHandles);
            return windowHandles;
        }

        private static bool IsWindowMpcHcVideo(IntPtr hWnd)
        {
            var className = new StringBuilder(256);
            int returnCode = NativeMethods.GetClassName(hWnd, className, className.Capacity);
            if (returnCode != 0)
            {
                var cName = className.ToString();
                return VideoClassPatterns.Any(pattern => cName.EndsWith(pattern, StringComparison.Ordinal));
            }

            return false;
        }

        private bool HijackMpcHc()
        {
            if (_process?.MainWindowHandle == IntPtr.Zero)
            {
                return false;
            }

            IntPtr handle = _process.MainWindowHandle;
            var handles = GetChildWindows();
            
            foreach (var h in handles)
            {
                if (IsWindowMpcHcVideo(h))
                {
                    _videoHandle = h;
                    NativeMethods.SetParent(h, _videoPanelHandle);
                    NativeMethods.SetWindowPos(handle, (IntPtr)NativeMethods.SpecialWindowHandles.HWND_TOP, -9999, -9999, 0, 0, NativeMethods.SetWindowPosFlags.SWP_NOACTIVATE);
                    return true;
                }
            }

            SeLogger.Error("Unable to find MPC video window");
            return false;
        }

        public override void Resize(int width, int height)
        {
            if (_process?.MainWindowHandle == IntPtr.Zero || _videoHandle == IntPtr.Zero)
            {
                return;
            }

            NativeMethods.ShowWindow(_process.MainWindowHandle, NativeMethods.ShowWindowCommands.ShowNoActivate);
            NativeMethods.SetWindowPos(_videoHandle, (IntPtr)NativeMethods.SpecialWindowHandles.HWND_TOP, 0, 0, width, height, NativeMethods.SetWindowPosFlags.SWP_NOREPOSITION);
            HideMpcPlayerWindow();
        }

        public static string GetMpcFileName()
        {
            return GetMpcFileName("_nvo") ?? GetMpcFileName(string.Empty);
        }

        private static string GetMpcFileNameInner(string fileNameSuffix, string prefix)
        {
            var is64Bit = IntPtr.Size == 8;
            var fileName = is64Bit ? $"{prefix}64{fileNameSuffix}.exe" : $"mpc-hc{fileNameSuffix}.exe";
            var registryKey = is64Bit 
                ? @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{2ACBF1FA-F5C3-4B19-A774-B22A31F231B9}_is1"
                : @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{2624B969-7135-4EB1-B0F6-2D8C397B45F7}_is1";

            // Search paths in order of preference
            var searchPaths = new List<string>
            {
                Path.Combine(Configuration.BaseDirectory, prefix.ToUpperInvariant(), fileName)
            };

            // Add custom location paths
            if (!string.IsNullOrEmpty(Configuration.Settings.General.MpcHcLocation))
            {
                var customLocation = Configuration.Settings.General.MpcHcLocation;
                if (File.Exists(customLocation) && customLocation.EndsWith(fileName, StringComparison.OrdinalIgnoreCase))
                {
                    searchPaths.Add(customLocation);
                }

                if (Directory.Exists(customLocation))
                {
                    searchPaths.Add(Path.Combine(customLocation, fileName));
                    searchPaths.Add(Path.Combine(customLocation, prefix.ToUpperInvariant(), fileName));
                }
            }

            // Add registry location
            var registryPath = RegistryUtil.GetValue(registryKey, "InstallLocation");
            if (!string.IsNullOrEmpty(registryPath))
            {
                searchPaths.Add(Path.Combine(registryPath, fileName));
            }

            // Add standard program files locations
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            searchPaths.Add(Path.Combine(programFiles, prefix.ToUpperInvariant(), fileName));
            searchPaths.Add($@"C:\Program Files\{prefix.ToUpperInvariant()}\{fileName}");
            
            if (is64Bit)
            {
                searchPaths.Add($@"C:\Program Files\{prefix.ToUpperInvariant()} x64\{fileName}");
                searchPaths.Add($@"C:\Program Files (x86)\{prefix.ToUpperInvariant()}\{fileName}");
            }

            // Add K-Lite codec pack locations
            var kLiteLocations = new[]
            {
                Path.Combine(programFiles, $@"K-Lite Codec Pack\{prefix.ToUpperInvariant()}", fileName),
                Path.Combine(programFiles, $@"K-Lite\{prefix.ToUpperInvariant()}", fileName)
            };

            if (is64Bit)
            {
                searchPaths.Add($@"C:\Program Files (x86)\K-Lite Codec Pack\{prefix.ToUpperInvariant()}64\{fileName}");
                searchPaths.Add(Path.Combine(programFiles, $@"K-Lite\{prefix.ToUpperInvariant()}64\", fileName));
            }

            searchPaths.AddRange(kLiteLocations);

            // Find first existing file
            return searchPaths.FirstOrDefault(File.Exists);
        }

        private static string GetMpcFileName(string fileNameSuffix)
        {
            if (!Configuration.IsRunningOnWindows) // Short circuit on Linux to resolve issues with read-only filesystems
            {
                return null;
            }

            return GetMpcFileNameInner(fileNameSuffix, "mpc-hc") ?? GetMpcFileNameInner(fileNameSuffix, "mpc-be");
        }

        public static bool IsInstalled => true;

        public override void DisposeVideoPlayer()
        {
            Dispose();
        }

        public override event EventHandler OnVideoLoaded;
        public override event EventHandler OnVideoEnded;

        private void ReleaseUnmanagedResources()
        {
            try
            {
                lock (_locker)
                {
                    if (_mpcHandle != IntPtr.Zero)
                    {
                        SendMpcMessage(MpcHcCommand.CloseApplication);
                        _mpcHandle = IntPtr.Zero;
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but don't throw during cleanup
                SeLogger.Error(ex, "Error releasing unmanaged resources");
            }
        }

        ~MpcHc()
        {
            Dispose(false);
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

            try
            {
                if (disposing)
                {
                    // Release managed resources
                    if (_positionTimer != null)
                    {
                        _positionTimer.Stop();
                        _positionTimer.Dispose();
                        _positionTimer = null;
                    }

                    if (_hideMpcTimer != null)
                    {
                        _hideMpcTimer.Stop();
                        _hideMpcTimer.Dispose();
                        _hideMpcTimer = null;
                    }

                    if (_form != null)
                    {
                        _form.OnCopyData -= OnCopyData;
                        // Note: _form.Dispose() gives an error when doing File -> Exit
                        _form = null;
                    }

                    if (_process != null)
                    {
                        try
                        {
                            if (!_process.HasExited)
                            {
                                _process.Kill();
                            }
                        }
                        catch (Exception ex)
                        {
                            SeLogger.Error(ex, "Error terminating MPC process");
                        }
                        finally
                        {
                            _process.Dispose();
                            _process = null;
                        }
                    }
                    
                    _startInfo = null;
                }

                ReleaseUnmanagedResources();
            }
            catch (Exception ex)
            {
                SeLogger.Error(ex, "Error during disposal");
            }
            finally
            {
                _disposed = true;
            }
        }

        private void SendMpcMessage(uint command)
        {
            SendMpcMessage(command, string.Empty);
        }

        private void SendMpcMessage(uint command, string parameter)
        {
            if (_mpcHandle == IntPtr.Zero || _messageHandlerHandle == IntPtr.Zero)
            {
                return;
            }

            parameter ??= string.Empty;
            parameter += '\0'; // Null terminator

            var cds = new NativeMethods.CopyDataStruct
            {
                dwData = (UIntPtr)command,
                cbData = parameter.Length * Marshal.SystemDefaultCharSize,
                lpData = Marshal.StringToCoTaskMemAuto(parameter)
            };

            try
            {
                NativeMethods.SendMessage(_mpcHandle, NativeMethods.WindowsMessageCopyData, _messageHandlerHandle, ref cds);
            }
            finally
            {
                // Free the allocated memory
                if (cds.lpData != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(cds.lpData);
                }
            }
        }

    }
}
