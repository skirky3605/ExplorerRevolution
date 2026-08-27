using System;
using System.Runtime.InteropServices;

namespace ExplorerRevolution.Common.Shell.Tray
{
    /// <summary>
    /// 通知区消息与数据结构(移植自 ManagedShell.WindowsTray,Apache-2.0)。
    /// </summary>
    public static class TrayInterop
    {
        [Flags]
        public enum NIM : uint
        {
            NIM_ADD = 0,
            NIM_MODIFY = 1,
            NIM_DELETE = 2,
            NIM_SETFOCUS = 3,
            NIM_SETVERSION = 4
        }

        [Flags]
        public enum NIF : uint
        {
            MESSAGE = 0x0001,
            ICON = 0x0002,
            TIP = 0x0004,
            STATE = 0x0008,
            INFO = 0x0010,
            GUID = 0x0020,
            REALTIME = 0x0040,
            SHOWTIP = 0x0080
        }

        [Flags]
        public enum NIIF : uint
        {
            NONE = 0x00000000,
            INFO = 0x00000001,
            WARNING = 0x00000002,
            ERROR = 0x00000003,
            USER = 0x00000004,
            NOSOUND = 0x00000010,
            LARGE_ICON = 0x00000020,
            NIIF_RESPECT_QUIET_TIME = 0x00000080
        }

        public enum NIN : uint
        {
            SELECT = 0x400,
            KEYSELECT = 0x401,
            BALLOONSHOW = 0x402,
            BALLOONHIDE = 0x403,
            BALLOONTIMEOUT = 0x404,
            BALLOONUSERCLICK = 0x405,
            POPUPOPEN = 0x406,
            POPUPCLOSE = 0x407,
            SNIPPET = 0x408,
            TSF = 0x409,
            OPERATIONSTART = 0x40A,
            OPERATIONFINISHED = 0x40B,
            TERMSERV = 0x40C,
            TERMSERVICEMSG = 0x40D
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPOS
        {
            public IntPtr hWnd;
            public IntPtr hwndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public uint flags;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct NOTIFYICONDATA
        {
            public int cbSize;
            public uint hWnd;
            public uint uID;
            public NIF uFlags;
            public uint uCallbackMessage;
            public uint hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string szInfo;
            public uint uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
            public string szInfoTitle;
            public NIIF dwInfoFlags;
            public Guid guidItem;
            public uint hBalloonIcon;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SHELLTRAYDATA
        {
            public int dwUnknown;
            public uint dwMessage;
            public NOTIFYICONDATA nid;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WINNOTIFYICONIDENTIFIER
        {
            public int dwMagic;
            public int dwMessage;
            public int cbSize;
            public int dwPadding;
            public uint hWnd;
            public uint uID;
            public Guid guidItem;
        }

        /// <summary>跨进程安全包装的 NOTIFYICONDATA(字符串已复制到本进程)。</summary>
        public sealed class SafeNotifyIconData
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uID;
            public NIF uFlags;
            public uint uCallbackMessage;
            public IntPtr hIcon;
            public string szTip;
            public int dwState;
            public int dwStateMask;
            public string szInfo;
            public uint uVersion;
            public string szInfoTitle;
            public NIIF dwInfoFlags;
            public Guid guidItem;
            public uint hBalloonIcon;

            public SafeNotifyIconData()
            {
            }

            public SafeNotifyIconData(NOTIFYICONDATA nid)
            {
                cbSize = nid.cbSize;
                hWnd = new IntPtr(nid.hWnd);
                uID = nid.uID;
                uFlags = nid.uFlags;
                uCallbackMessage = nid.uCallbackMessage;
                hIcon = new IntPtr(nid.hIcon);
                szTip = nid.szTip;
                dwState = nid.dwState;
                dwStateMask = nid.dwStateMask;
                szInfo = nid.szInfo;
                uVersion = nid.uVersion;
                szInfoTitle = nid.szInfoTitle;
                dwInfoFlags = nid.dwInfoFlags;
                guidItem = nid.guidItem;
                hBalloonIcon = nid.hBalloonIcon;
            }
        }

        /// <summary>explorer 托盘工具栏枚举(Windows 10 及更早的经典通知区)。</summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct TrayItem
        {
            public IntPtr hWnd;
            public uint uID;
            public uint uCallbackMessage;
            public uint dwState;
            public uint uVersion;
            public IntPtr hIcon;
            public IntPtr uIconDemoteTimerID;
            public uint dwUserPref;
            public uint dwLastSoundTime;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szExeName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szIconText;
            public uint uNumSeconds;
            public Guid guidItem;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TBBUTTON
        {
            public int iBitmap;
            public int idCommand;
            [StructLayout(LayoutKind.Explicit)]
            private struct TBBUTTON_U
            {
                [FieldOffset(0)] public byte fsState;
                [FieldOffset(1)] public byte fsStyle;
                [FieldOffset(0)] public IntPtr bReserved;
            }

            private TBBUTTON_U union;

            public byte fsState
            {
                get { return union.fsState; }
                set { union.fsState = value; }
            }

            public byte fsStyle
            {
                get { return union.fsStyle; }
                set { union.fsStyle = value; }
            }

            public UIntPtr dwData;
            public IntPtr iString;
        }
    }

    /// <summary>托盘消息回调。</summary>
    public delegate bool SystrayMessageHandler(uint message, TrayInterop.SafeNotifyIconData data);

    /// <summary>图标位置查询回调(Shell_NotifyIconGetRect)。</summary>
    public delegate IntPtr IconDataRequestHandler(int message, uint hWnd, uint uID, Guid guidItem);
}
