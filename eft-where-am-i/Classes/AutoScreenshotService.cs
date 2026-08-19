using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace eft_where_am_i.Classes
{
    /// <summary>
    /// 게임 창이 활성 상태일 때 스크린샷 키를 주기적으로 눌러줍니다.
    ///
    /// 타르코프는 스크린샷 파일명에 좌표를 적어주기 때문에, 이 앱은 스크린샷이 찍혀야
    /// 위치를 갱신할 수 있습니다. 직접 누르는 대신 일정 간격으로 대신 눌러주는 기능입니다.
    ///
    /// <para>
    /// 주의: 게임에 입력을 주입하는 동작입니다. BSG 의 정책상 자동 입력이 문제가 될 수 있으므로
    /// 기본값은 꺼짐이며, 사용 여부는 사용자가 판단해야 합니다.
    /// 또 게임이 주입된 입력(LLKHF_INJECTED)을 걸러내면 동작하지 않을 수 있습니다.
    /// </para>
    /// </summary>
    public class AutoScreenshotService : IDisposable
    {
        private const string TargetProcessName = "EscapeFromTarkov";

        public const int MinIntervalSeconds = 1;
        public const int MaxIntervalSeconds = 60;
        public const int DefaultIntervalSeconds = 5;

        private System.Threading.Timer _timer;
        private int _intervalSeconds = DefaultIntervalSeconds;
        private bool _disposed;
        private readonly object _gate = new object();

        /// <summary>키를 실제로 눌렀을 때 발생합니다. (UI 스레드가 아님)</summary>
        public event Action ScreenshotKeySent;

        public bool IsRunning { get; private set; }

        /// <summary>누를 키. 기본값은 타르코프 기본 스크린샷 키인 PrintScreen 입니다.</summary>
        public Keys ScreenshotKey { get; set; } = Keys.PrintScreen;

        public int IntervalSeconds
        {
            get => _intervalSeconds;
            set
            {
                _intervalSeconds = Math.Clamp(value, MinIntervalSeconds, MaxIntervalSeconds);
                lock (_gate)
                {
                    if (IsRunning)
                        _timer?.Change(_intervalSeconds * 1000, _intervalSeconds * 1000);
                }
            }
        }

        /// <summary>
        /// 문자열로 된 키 이름("PrintScreen", "F12" 등)을 설정합니다.
        /// 알 수 없는 이름이면 PrintScreen 으로 둡니다.
        /// </summary>
        public void SetKeyFromName(string keyName)
        {
            if (string.IsNullOrWhiteSpace(keyName))
            {
                ScreenshotKey = Keys.PrintScreen;
                return;
            }

            if (Enum.TryParse<Keys>(keyName.Trim(), ignoreCase: true, out var parsed) && parsed != Keys.None)
            {
                ScreenshotKey = parsed;
            }
            else
            {
                AppLogger.Warn("AutoScreenshot", $"알 수 없는 키 이름 '{keyName}', PrintScreen 을 사용합니다.");
                ScreenshotKey = Keys.PrintScreen;
            }
        }

        public void Start()
        {
            lock (_gate)
            {
                if (_disposed || IsRunning) return;

                _timer = new System.Threading.Timer(
                    OnTick, null, _intervalSeconds * 1000, _intervalSeconds * 1000);
                IsRunning = true;
            }

            AppLogger.Info("AutoScreenshot",
                $"시작 (간격 {_intervalSeconds}초, 키 {ScreenshotKey}). 게임이 활성 상태일 때만 동작합니다.");
        }

        public void Stop()
        {
            lock (_gate)
            {
                if (!IsRunning) return;

                _timer?.Dispose();
                _timer = null;
                IsRunning = false;
            }

            AppLogger.Info("AutoScreenshot", "중지");
        }

        private void OnTick(object state)
        {
            try
            {
                // 게임이 활성 상태가 아니면 키를 보내지 않습니다.
                // SendInput 은 포그라운드 창으로 가기 때문에, 이 확인이 없으면
                // 다른 프로그램에 엉뚱한 키가 들어갑니다.
                if (!IsGameForeground()) return;

                SendKey(ScreenshotKey);
                ScreenshotKeySent?.Invoke();
            }
            catch (Exception ex)
            {
                AppLogger.Error("AutoScreenshot", $"키 전송 실패: {ex.Message}");
            }
        }

        private static bool IsGameForeground()
        {
            try
            {
                IntPtr hwnd = GetForegroundWindow();
                if (hwnd == IntPtr.Zero) return false;

                GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == 0) return false;

                using var process = Process.GetProcessById((int)pid);
                return string.Equals(process.ProcessName, TargetProcessName, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 스캔코드 기반으로 키 입력을 주입합니다.
        /// 게임(특히 Unity)은 가상 키코드보다 스캔코드를 더 안정적으로 인식합니다.
        /// </summary>
        private static void SendKey(Keys key)
        {
            ushort vk = (ushort)key;
            ushort scan = (ushort)MapVirtualKey(vk, MAPVK_VK_TO_VSC);

            // PrintScreen 을 비롯한 일부 키는 확장 키 플래그가 필요합니다.
            uint extended = IsExtendedKey(key) ? KEYEVENTF_EXTENDEDKEY : 0u;

            var inputs = new[]
            {
                CreateKeyInput(vk, scan, extended),
                CreateKeyInput(vk, scan, extended | KEYEVENTF_KEYUP),
            };

            uint sent = SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
            if (sent != inputs.Length)
            {
                AppLogger.Warn("AutoScreenshot",
                    $"SendInput 이 일부만 전송했습니다 ({sent}/{inputs.Length}). Win32 오류 {Marshal.GetLastWin32Error()}");
            }
        }

        private static bool IsExtendedKey(Keys key)
        {
            switch (key)
            {
                case Keys.PrintScreen:
                case Keys.Insert:
                case Keys.Delete:
                case Keys.Home:
                case Keys.End:
                case Keys.PageUp:
                case Keys.PageDown:
                case Keys.Left:
                case Keys.Right:
                case Keys.Up:
                case Keys.Down:
                case Keys.NumLock:
                    return true;
                default:
                    return false;
            }
        }

        private static INPUT CreateKeyInput(ushort vk, ushort scan, uint flags)
        {
            // 스캔코드를 구하지 못하면 가상 키코드 방식으로 폴백합니다.
            bool useScanCode = scan != 0;

            return new INPUT
            {
                type = INPUT_KEYBOARD,
                U = new InputUnion
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = useScanCode ? (ushort)0 : vk,
                        wScan = scan,
                        dwFlags = flags | (useScanCode ? KEYEVENTF_SCANCODE : 0u),
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    }
                }
            };
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Stop();
        }

        #region P/Invoke

        private const uint INPUT_KEYBOARD = 1;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_SCANCODE = 0x0008;
        private const uint MAPVK_VK_TO_VSC = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint type;
            public InputUnion U;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        #endregion
    }
}
