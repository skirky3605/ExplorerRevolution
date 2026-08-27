using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using static ExplorerRevolution.Common.NativeMethods;

namespace ExplorerRevolution.UI
{
    /// <summary>
    /// 全屏检测:仅当"前台窗口铺满任务栏所在显示器"时才隐藏任务栏。
    /// 桌面 / 开始菜单 / 任务视图 / 搜索等系统壳 UI 窗口会被排除,避免误隐藏;
    /// 恢复显示时把任务栏显式提升到最顶层,避免残留的置顶全屏窗口盖住它。
    /// 通过前台事件 + 1 秒兜底轮询。
    /// </summary>
    public sealed class ShellFullscreenMonitor : IDisposable
    {
        private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;

        private readonly Form _taskbar;
        private readonly Timer _timer;
        private readonly Dictionary<uint, string> _processNames = new Dictionary<uint, string>();
        private WinEventDelegate _proc;
        private IntPtr _hook;
        private bool _isHidden;

        public ShellFullscreenMonitor(Form taskbar)
        {
            _taskbar = taskbar;

            _proc = OnForegroundChanged;
            _hook = SetWinEventHook(
                EVENT_SYSTEM_FOREGROUND,
                EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                _proc,
                0,
                0,
                WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);

            // 兜底轮询:个别游戏可能不触发前台事件
            _timer = new Timer { Interval = 1000 };
            _timer.Tick += (s, e) => Check();
            _timer.Start();

            Check();
        }

        private void OnForegroundChanged(
            IntPtr hWinEventHook,
            uint eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime)
        {
            Check();
        }

        private void Check()
        {
            try
            {
                IntPtr fg = GetForegroundWindow();
                bool fullscreen = false;

                if (fg != IntPtr.Zero)
                {
                    GetWindowThreadProcessId(fg, out uint pid);

                    // 本进程与系统壳 UI(桌面/开始菜单/任务视图/搜索/任务栏)不算全屏
                    if (pid != (uint)Process.GetCurrentProcess().Id && !IsShellUiProcess(pid))
                    {
                        if (GetWindowRect(fg, out RECT r))
                        {
                            // 以任务栏所在显示器为准:副屏全屏游戏不隐藏主屏任务栏
                            var bounds = Screen.FromHandle(_taskbar.Handle).Bounds;
                            fullscreen = r.Left <= bounds.Left && r.Top <= bounds.Top &&
                                         r.Right >= bounds.Right && r.Bottom >= bounds.Bottom;
                        }
                    }
                }

                SetHidden(fullscreen);
            }
            catch
            {
                // 检测异常不影响任务栏
            }
        }

        private bool IsShellUiProcess(uint pid)
        {
            string name = GetProcessName(pid);
            return name == "explorer"
                || name == "startmenuexperiencehost"
                || name == "searchhost"
                || name == "shellexperiencehost"
                || name == "shellhost";
        }

        private string GetProcessName(uint pid)
        {
            if (_processNames.TryGetValue(pid, out string cached))
            {
                return cached;
            }

            if (_processNames.Count > 512)
            {
                _processNames.Clear();
            }

            string name = "";
            try
            {
                using (var process = Process.GetProcessById((int)pid))
                {
                    name = process.ProcessName.ToLowerInvariant();
                }
            }
            catch
            {
                // 进程可能已退出
            }

            _processNames[pid] = name;
            return name;
        }

        private void SetHidden(bool fullscreen)
        {
            if (_taskbar == null || _taskbar.IsDisposed)
            {
                return;
            }

            if (fullscreen && !_isHidden)
            {
                _isHidden = true;
                RunOnUi(() =>
                {
                    _taskbar.Hide();
                });
            }
            else if (!fullscreen && _isHidden)
            {
                _isHidden = false;
                RunOnUi(() =>
                {
                    _taskbar.Show();

                    // 提升到最顶层,避免残留的置顶全屏窗口盖住任务栏
                    SetWindowPos(_taskbar.Handle, HWND_TOP, 0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
                });
            }
        }

        private void RunOnUi(Action action)
        {
            try
            {
                if (_taskbar.InvokeRequired)
                {
                    _taskbar.BeginInvoke(action);
                }
                else
                {
                    action();
                }
            }
            catch
            {
                // 窗体可能已销毁
            }
        }

        public void Dispose()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Dispose();
            }

            if (_hook != IntPtr.Zero)
            {
                UnhookWinEvent(_hook);
                _hook = IntPtr.Zero;
            }

            _proc = null;
        }
    }
}
