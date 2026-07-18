using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ExplorerRevolution.Common
{
    public static class ApplicationViewManager
    {
        private static readonly Guid CLSID_ImmersiveShell =
            new Guid("C2F03A33-21F5-47FA-B4BB-156362A2F239");


        private static Guid SID_ApplicationViewCollection =
            new Guid("1841C6D7-4F9D-42C0-AF41-8747538F10E5");


        private static Guid IID_IApplicationView =
            new Guid("372E1D3B-38D3-42E4-A15B-8AB2B178F513");


        private static readonly Guid IID_IObjectArray =
            new Guid("92CA9DCD-5622-4bba-A805-5E9F541BD8C9");



        public static bool IsApplicationView(IntPtr hwnd)
        {
            return GetApplicationView(hwnd) != null;
        }



        public static IApplicationView GetApplicationView(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero)
                return null;


            try
            {
                var collection =
                    GetApplicationViewCollection();


                collection.GetViews(
                    out IObjectArray array);


                array.GetCount(
                    out uint count);



                for (uint i = 0; i < count; i++)
                {
                    array.GetAt(
                        i,
                        ref IID_IApplicationView,
                        out object obj);


                    if (!(obj is  IApplicationView view))
                        continue;


                    Debug.WriteLine(NativeMethods.GetWindowText(hwnd));
                    view.GetThumbnailWindow(
                        out IntPtr viewHwnd);



                    if (viewHwnd == hwnd)
                    {
                        return view;
                    }
                }
        }
            catch
            {

            }


            return null;
        }




        public static List<IntPtr> GetApplicationWindows()
        {
            var result = new List<IntPtr>();

            //try
            //{
                var collection =
                    GetApplicationViewCollection();


                collection.GetViews(
                    out IObjectArray array);


                array.GetCount(
                    out uint count);



                for (uint i = 0; i < count; i++)
                {
                    array.GetAt(
                        i,
                        ref IID_IApplicationView,
                        out object obj);


                    if (obj is IApplicationView view)
                    {
                        view.GetThumbnailWindow(
                            out IntPtr hwnd);


                        if (hwnd != IntPtr.Zero)
                            result.Add(hwnd);
                    }
                }
            //}
            //catch
            //{

            //}


            return result;
        }




        private static IApplicationViewCollection
            GetApplicationViewCollection()
        {

            Type type =
                Type.GetTypeFromCLSID(
                    CLSID_ImmersiveShell);


            object shell =
                Activator.CreateInstance(type);



            var provider =
                (IServiceProvider)shell;



            Guid iid =
                typeof(IApplicationViewCollection)
                .GUID;



            provider.QueryService(
                ref SID_ApplicationViewCollection,
                ref iid,
                out object result);



            return
                (IApplicationViewCollection)result;
        }
    }







    [ComImport]
    [Guid("1841C6D7-4F9D-42C0-AF41-8747538F10E5")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IApplicationViewCollection
    {

        int GetViews(
            [MarshalAs(UnmanagedType.Interface)]
            out IObjectArray array);


        int GetViewsInZOrder(
            [MarshalAs(UnmanagedType.Interface)]
            out IObjectArray array);


        int GetViewForHwnd(
            IntPtr hwnd,
            [MarshalAs(UnmanagedType.Interface)]
            out IApplicationView view);

    }






    [ComImport]
    [Guid("92CA9DCD-5622-4bba-A805-5E9F541BD8C9")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IObjectArray
    {
        int GetCount(
            out uint count);


        int GetAt(
            uint index,
            ref Guid iid,
            [MarshalAs(UnmanagedType.Interface)]
            out object obj);
    }






    [ComImport]
    [Guid("372E1D3B-38D3-42E4-A15B-8AB2B178F513")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IApplicationView
    {

        int SetFocus();


        int SwitchTo();


        int TryInvokeBack(
            IntPtr callback);


        int GetThumbnailWindow(
            out IntPtr hwnd);


        int GetVisibility(
            out int visibility);


        int SetCloak(
            int cloak);


        int GetAppUserModelId(
            out IntPtr id);

    }







    [ComImport]
    [Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IServiceProvider
    {
        int QueryService(
            ref Guid service,
            ref Guid iid,
            [MarshalAs(UnmanagedType.Interface)]
            out object result);
    }
}