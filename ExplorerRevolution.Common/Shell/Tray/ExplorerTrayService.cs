using Microsoft.Win32;
using System;
using System.Runtime.InteropServices;
using static ExplorerRevolution.Common.NativeMethods;
using static ExplorerRevolution.Common.Shell.Tray.TrayInterop;

namespace ExplorerRevolution.Common.Shell.Tray
{
    /// <summary>
    /// 在本进程还不是托盘宿主时,从 explorer 的经典通知区工具栏(ToolbarWindow32)枚举
    /// 已存在的托盘图标作为初始集合。Windows 11 24H2 通知区已改为 XAML 渲染,
    /// 找不到工具栏时自动跳过(新图标由 TrayService 的 TaskbarCreated 广播补齐)。
    /// 移植自 ManagedShell.WindowsTray.ExplorerTrayService,Apache-2.0。
    /// </summary>
    public sealed class ExplorerTrayService
    {
        private const byte TBSTATE_HIDDEN = 8;
        private const string ExplorerKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer";

        public void Run(SystrayMessageHandler callback)
        {
            if (callback == null)
            {
                return;
            }

            bool autoTrayEnabled = GetAutoTrayEnabled();
            TrayNotify trayNotify = null;

            try
            {
                if (autoTrayEnabled)
                {
                    // 自动隐藏开启时,隐藏图标不在工具栏上;先临时关闭再枚举
                    trayNotify = new TrayNotify();
                    SetAutoTrayEnabled(trayNotify, false);
                }

                GetTrayItems(callback);
            }
            catch (Exception ex)
            {
                ShellLog.Debug($"ExplorerTrayService: Unable to get tray items: {ex.Message}");
            }
            finally
            {
                if (trayNotify != null)
                {
                    try
                    {
                        SetAutoTrayEnabled(trayNotify, true);
                    }
                    catch
                    {
                        // 恢复失败不阻断
                    }

                    Marshal.ReleaseComObject(trayNotify);
                }
            }
        }

        private void GetTrayItems(SystrayMessageHandler callback)
        {
            IntPtr toolbarHwnd = FindExplorerTrayToolbarHwnd();
            if (toolbarHwnd == IntPtr.Zero)
            {
                ShellLog.Debug("ExplorerTrayService: Explorer tray toolbar not found (Win11 24H2+), skipping initial enumeration");
                return;
            }

            int count = (int)SendMessage(toolbarHwnd, TB_BUTTONCOUNT, IntPtr.Zero, IntPtr.Zero);
            if (count < 1)
            {
                return;
            }

            GetWindowThreadProcessId(toolbarHwnd, out uint processId);
            IntPtr hProcess = OpenProcess(
                PROCESS_VM_OPERATION | PROCESS_VM_READ | PROCESS_VM_WRITE | PROCESS_QUERY_INFORMATION,
                false,
                processId);

            if (hProcess == IntPtr.Zero)
            {
                ShellLog.Debug($"ExplorerTrayService: Unable to open explorer process ({processId})");
                return;
            }

            IntPtr hBuffer = IntPtr.Zero;
            try
            {
                hBuffer = VirtualAllocEx(hProcess, IntPtr.Zero, (uint)Marshal.SizeOf(typeof(TBBUTTON)), MEM_COMMIT, PAGE_READWRITE);
                if (hBuffer == IntPtr.Zero)
                {
                    return;
                }

                for (int i = 0; i < count; i++)
                {
                    TrayItem trayItem = GetTrayItem(i, hBuffer, hProcess, toolbarHwnd);

                    if (trayItem.hWnd == IntPtr.Zero || !IsWindow(trayItem.hWnd))
                    {
                        continue;
                    }

                    SafeNotifyIconData nid = GetTrayItemIconData(trayItem);
                    callback((uint)NIM.NIM_ADD, nid);
                }
            }
            catch (Exception ex)
            {
                ShellLog.Debug($"ExplorerTrayService: Enumeration failed: {ex.Message}");
            }
            finally
            {
                if (hBuffer != IntPtr.Zero)
                {
                    VirtualFreeEx(hProcess, hBuffer, 0, MEM_RELEASE);
                }

                CloseHandle(hProcess);
            }
        }

        private IntPtr FindExplorerTrayToolbarHwnd()
        {
            IntPtr hwnd = FindWindow("Shell_TrayWnd", "");
            if (hwnd != IntPtr.Zero)
            {
                hwnd = FindWindowEx(hwnd, IntPtr.Zero, "TrayNotifyWnd", "");
            }

            if (hwnd != IntPtr.Zero)
            {
                hwnd = FindWindowEx(hwnd, IntPtr.Zero, "SysPager", "");
            }

            if (hwnd != IntPtr.Zero)
            {
                hwnd = FindWindowEx(hwnd, IntPtr.Zero, "ToolbarWindow32", null);
            }

            return hwnd;
        }

        private TrayItem GetTrayItem(int i, IntPtr hBuffer, IntPtr hProcess, IntPtr toolbarHwnd)
        {
            TrayItem trayItem = new TrayItem();
            IntPtr hTBButton = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TBBUTTON)));
            IntPtr hTrayItem = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(TrayItem)));

            try
            {
                SendMessage(toolbarHwnd, TB_GETBUTTON, (IntPtr)i, hBuffer);

                if (ReadProcessMemory(hProcess, hBuffer, hTBButton, Marshal.SizeOf(typeof(TBBUTTON)), out _))
                {
                    TBBUTTON tbButton = (TBBUTTON)Marshal.PtrToStructure(hTBButton, typeof(TBBUTTON));

                    if (tbButton.dwData != UIntPtr.Zero &&
                        ReadProcessMemory(hProcess, new IntPtr((long)tbButton.dwData.ToUInt64()), hTrayItem, Marshal.SizeOf(typeof(TrayItem)), out _))
                    {
                        trayItem = (TrayItem)Marshal.PtrToStructure(hTrayItem, typeof(TrayItem));

                        trayItem.dwState = (tbButton.fsState & TBSTATE_HIDDEN) != 0 ? 1u : 0u;
                    }
                }
            }
            catch
            {
                // 单个图标读取失败跳过
            }
            finally
            {
                Marshal.FreeHGlobal(hTBButton);
                Marshal.FreeHGlobal(hTrayItem);
            }

            return trayItem;
        }

        private static SafeNotifyIconData GetTrayItemIconData(TrayItem trayItem)
        {
            SafeNotifyIconData nid = new SafeNotifyIconData
            {
                hWnd = trayItem.hWnd,
                uID = trayItem.uID,
                uCallbackMessage = trayItem.uCallbackMessage,
                szTip = trayItem.szIconText,
                hIcon = trayItem.hIcon,
                uVersion = trayItem.uVersion,
                guidItem = trayItem.guidItem,
                dwState = (int)trayItem.dwState,
                uFlags = NIF.GUID | NIF.MESSAGE | NIF.TIP | NIF.STATE
            };

            if (nid.hIcon != IntPtr.Zero)
            {
                nid.uFlags |= NIF.ICON;
            }

            return nid;
        }

        private static bool GetAutoTrayEnabled()
        {
            int enableAutoTray = 1;
            try
            {
                using (RegistryKey explorerKey = Registry.CurrentUser.OpenSubKey(ExplorerKey, false))
                {
                    object value = explorerKey?.GetValue("EnableAutoTray");
                    if (value != null)
                    {
                        enableAutoTray = Convert.ToInt32(value);
                    }
                }
            }
            catch
            {
                // 默认开启
            }

            return enableAutoTray == 1;
        }

        private static void SetAutoTrayEnabled(TrayNotify trayNotify, bool enabled)
        {
            try
            {
                ITrayNotify trayNotifyInstance = (ITrayNotify)trayNotify;
                trayNotifyInstance.EnableAutoTray(enabled);
            }
            catch
            {
                ShellLog.Debug("ExplorerTrayService: ITrayNotify not available");
            }
        }
    }
}
