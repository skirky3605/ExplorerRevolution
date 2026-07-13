using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static ExplorerRevolution.Common.NativeMethods;

namespace ExplorerRevolution.Common
{
    public class TaskbarButtonMonitor : IDisposable
    {
        private IntPtr _hook;
        private readonly WinEventDelegate _callback;
        private readonly SynchronizationContext _syncContext;
        private readonly ConcurrentDictionary<IntPtr, byte> _trackedWindows = new ConcurrentDictionary<IntPtr, byte>();

        // 事件
        public event Action<IntPtr, string> ButtonAdded;
        public event Action<IntPtr, string> ButtonRemoved;
        public event Action<IntPtr, string> ButtonTitleChanged;

        public TaskbarButtonMonitor()
        {
            _syncContext = SynchronizationContext.Current ?? new SynchronizationContext();
            _callback = WinEventProc;
        }

        public void Start()
        {
            // 1. 枚举所有现有顶层窗口（此时它们都已经可见/可用）
            EnumWindows(EnumWindowsCallback, IntPtr.Zero);

            // 2. 安装 WinEvent 钩子，覆盖我们需要的事件范围
            //    EVENT_OBJECT_CREATE(0x8000) 到 EVENT_OBJECT_NAMECHANGE(0x800C)
            _hook = SetWinEventHook(
                EVENT_OBJECT_CREATE, EVENT_OBJECT_NAMECHANGE,
                IntPtr.Zero, _callback,
                0, 0,
                WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        }

        // ===== WinEvent 回调 =====
        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd,
                                  int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (idObject != 0) return; // 只处理窗口对象自身
            if (GetParent(hwnd) != IntPtr.Zero) return; // 只处理顶层窗口

            switch (eventType)
            {
                case EVENT_OBJECT_CREATE:
                case EVENT_OBJECT_SHOW:
                    // 窗口创建或变为可见
                    try
                    {
                        if (WindowHelpers.ShouldShowInTaskbar(hwnd) && _trackedWindows.TryAdd(hwnd, 0))
                        {
                            var title = GetWindowText(hwnd);
                            PostToUI(() => ButtonAdded?.Invoke(hwnd, title));
                        }
                        else
                        {
                            // 对于 UWP 窗口，AUMID 或可见性可能在短时间后才就绪，延迟复查
                            if (WindowHelpers.IsUwpWindow(hwnd) && !_trackedWindows.ContainsKey(hwnd))
                            {
                                Task.Run(async () =>
                                {
                                    await Task.Delay(600);
                                    try
                                    {
                                        if (WindowHelpers.ShouldShowInTaskbar(hwnd) && _trackedWindows.TryAdd(hwnd, 0))
                                        {
                                            var title2 = GetWindowText(hwnd);
                                            PostToUI(() => ButtonAdded?.Invoke(hwnd, title2));
                                        }
                                    }
                                    catch { }
                                });
                            }
                        }
                    }
                    catch { }
                    break;

                case EVENT_OBJECT_HIDE:
                    // 窗口隐藏（包括最小化、关闭前的隐藏等）
                    // 如果窗口被最小化，按钮仍需保留；否则移除
                    if (_trackedWindows.ContainsKey(hwnd) && !WindowHelpers.ShouldShowInTaskbar(hwnd))
                    {
                        _trackedWindows.TryRemove(hwnd, out _);
                        PostToUI(() => ButtonRemoved?.Invoke(hwnd, string.Empty));
                    }
                    break;

                case EVENT_OBJECT_DESTROY:
                    // 窗口彻底销毁，直接清理
                    if (_trackedWindows.TryRemove(hwnd, out _))
                    {
                        PostToUI(() => ButtonRemoved?.Invoke(hwnd, string.Empty));
                    }
                    break;

                case EVENT_OBJECT_NAMECHANGE:
                    // 标题变化（更新已跟踪窗口）或尝试在 namechange 时添加之前未被跟踪但现在满足条件的窗口
                    if (_trackedWindows.ContainsKey(hwnd))
                    {
                        var newTitle = GetWindowText(hwnd);
                        PostToUI(() => ButtonTitleChanged?.Invoke(hwnd, newTitle));
                    }
                    else
                    {
                        try
                        {
                            if (WindowHelpers.ShouldShowInTaskbar(hwnd) && _trackedWindows.TryAdd(hwnd, 0))
                            {
                                var title3 = GetWindowText(hwnd);
                                PostToUI(() => ButtonAdded?.Invoke(hwnd, title3));
                            }
                        }
                        catch { }
                    }
                    break;

                // 其它事件忽略
            }
        }

        // ===== 辅助函数 =====
        private bool EnumWindowsCallback(IntPtr hwnd, IntPtr lParam)
        {
            if (WindowHelpers.ShouldShowInTaskbar(hwnd) && _trackedWindows.TryAdd(hwnd, 0))
            {
                var title = GetWindowText(hwnd);
                // 枚举回调可以直接调用（已是 UI 线程或无需同步）
                ButtonAdded?.Invoke(hwnd, title);
            }
            return true;
        }

        private void PostToUI(Action action) => _syncContext.Post(_ => action(), null);

        public void Stop() { if (_hook != IntPtr.Zero) UnhookWinEvent(_hook); }
        public void Dispose() => Stop();
    }
}
