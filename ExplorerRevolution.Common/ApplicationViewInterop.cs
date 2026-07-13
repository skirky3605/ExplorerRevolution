using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ExplorerRevolution.Common
{
    // 基于 ImmersiveShell 的 IApplicationView / IApplicationViewCollection 轻量封装
    public static class ApplicationViewInterop
    {
        // CLSID_ImmersiveShell
        static readonly Guid CLSID_ImmersiveShell = new Guid("C2F03A33-21F5-47FA-B4BB-156362A2F239");
        // IID_IApplicationViewCollection
        static readonly Guid IID_IApplicationViewCollection = new Guid("1841C6D7-4F9D-42C0-AF41-8747538F10E5");
        // IID_IApplicationView
        static readonly Guid IID_IApplicationView = new Guid("372E1D3B-38D3-42E4-A15B-8AB2B178F513");
        // IID_IObjectArray
        static readonly Guid IID_IObjectArray = new Guid("92CA9DCD-5622-4bba-A805-5E9F541BD8C9");

        [ComImport, Guid("6d5140c1-7436-11ce-8034-00aa006009fa"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IServiceProvider
        {
            [PreserveSig]
            int QueryService(ref Guid guidService, ref Guid riid, out IntPtr ppvObject);
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("1841C6D7-4F9D-42C0-AF41-8747538F10E5")]
        interface IApplicationViewCollection
        {
            // 仅声明我们需要的方法
            [PreserveSig]
            int GetViews(out IObjectArray views);

            // 使用原始 IntPtr 以避免由 runtime 将 COM 对象封送为受管接口时发生不兼容导致的访问冲突
            [PreserveSig]
            int GetViewForHwnd(IntPtr hwnd, out IntPtr pView);

            // 其它方法未声明
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("372E1D3B-38D3-42E4-A15B-8AB2B178F513")]
        interface IApplicationView
        {
            // 不明确列出方法；我们只保留对象引用以便进一步查询（如需要）
        }

        [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("92CA9DCD-5622-4bba-A805-5E9F541BD8C9")]
        interface IObjectArray
        {
            void GetCount(out uint pcObjects);
            void GetAt(uint uiIndex, ref Guid riid, out IntPtr ppv);
        }

        // 获取 IApplicationViewCollection 实例（如果可用）
        static IApplicationViewCollection GetViewCollection()
        {
            var shellType = Type.GetTypeFromCLSID(CLSID_ImmersiveShell);
            if (shellType == null) return null;

            object shell = null;
            try
            {
                shell = Activator.CreateInstance(shellType);
                var sp = shell as IServiceProvider;
                if (sp == null)
                {
                    // 尝试直接 Marshal
                    sp = (IServiceProvider)Marshal.GetTypedObjectForIUnknown(Marshal.GetIUnknownForObject(shell), typeof(IServiceProvider));
                }

                IntPtr pViewCollection;
                var guidService = IID_IApplicationViewCollection;
                var riid = IID_IApplicationViewCollection;
                int hr = sp.QueryService(ref guidService, ref riid, out pViewCollection);
                if (hr != 0 || pViewCollection == IntPtr.Zero) return null;

                try
                {
                    var coll = (IApplicationViewCollection)Marshal.GetObjectForIUnknown(pViewCollection);
                    return coll;
                }
                finally
                {
                    Marshal.Release(pViewCollection);
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                if (shell != null)
                {
                    try { Marshal.ReleaseComObject(shell); } catch { }
                }
            }
        }

        // 枚举所有 IApplicationView 对象，返回一组 COM 指针（IntPtr）。调用者应当注意释放这些指针。
        public static List<IntPtr> EnumerateApplicationViews()
        {
            var result = new List<IntPtr>();
            var coll = GetViewCollection();
            if (coll == null) return result;

            try
            {
                if (coll.GetViews(out var objArray) != 0 || objArray == null) return result;
                try
                {
                    objArray.GetCount(out uint count);
                    for (uint i = 0; i < count; i++)
                    {
                        try
                        {
                            var iid = IID_IApplicationView;
                            objArray.GetAt(i, ref iid, out IntPtr pView);
                            if (pView != IntPtr.Zero)
                            {
                                // 增加引用并返回指针（调用者负责释放）
                                Marshal.AddRef(pView);
                                result.Add(pView);
                            }
                        }
                        catch { }
                    }
                }
                finally
                {
                    try { Marshal.ReleaseComObject(objArray); } catch { }
                }
            }
            finally
            {
                try { Marshal.ReleaseComObject(coll); } catch { }
            }

            return result;
        }

        // 获取指定 hwnd 对应的 IApplicationView（如存在），返回 COM 指针（已 AddRef），失败返回 IntPtr.Zero
        public static IntPtr GetApplicationViewForHwnd(IntPtr hwnd)
        {
            var coll = GetViewCollection();
            if (coll == null) return IntPtr.Zero;

            IntPtr pView = IntPtr.Zero;
            try
            {
                // 调用可能抛出 AccessViolation 的本机方法，使用受保护的调用封装来捕获腐败状态异常
                pView = SafeGetViewForHwnd(coll, hwnd);
                if (pView != IntPtr.Zero)
                {
                    // 返回已 AddRef 的原始 IUnknown 指针（调用方负责 Release）
                    return pView;
                }
            }
            catch { }
            finally
            {
                try { Marshal.ReleaseComObject(coll); } catch { }
            }

            return IntPtr.Zero;
        }

        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptions]
        static IntPtr SafeGetViewForHwnd(IApplicationViewCollection coll, IntPtr hwnd)
        {
            try
            {
                int hr = coll.GetViewForHwnd(hwnd, out IntPtr pViewNative);
                if (hr == 0 && pViewNative != IntPtr.Zero)
                {
                    // 增加引用计数并返回
                    Marshal.AddRef(pViewNative);
                    return pViewNative;
                }
            }
            catch (AccessViolationException)
            {
                // 捕获受损状态异常并安全返回
                return IntPtr.Zero;
            }
            catch
            {
                return IntPtr.Zero;
            }

            return IntPtr.Zero;
        }
    }
}
