using System.Runtime.InteropServices;

namespace TvKioskWrapper.Interop;

internal static class AudioInterop
{
    internal enum EDataFlow
    {
        eRender = 0,
        eCapture = 1,
        eAll = 2
    }

    internal enum ERole
    {
        eConsole = 0,
        eMultimedia = 1,
        eCommunications = 2
    }

    [Flags]
    internal enum DeviceState : uint
    {
        Active = 0x00000001,
        Disabled = 0x00000002,
        NotPresent = 0x00000004,
        Unplugged = 0x00000008,
        All = 0x0000000F
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PropertyKey
    {
        public Guid fmtid;
        public uint pid;

        public static readonly PropertyKey PKEY_Device_FriendlyName = new()
        {
            fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),
            pid = 14
        };
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct PropVariant
    {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(2)] public ushort wReserved1;
        [FieldOffset(4)] public ushort wReserved2;
        [FieldOffset(6)] public ushort wReserved3;
        [FieldOffset(8)] public nint pwszVal;
    }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(EDataFlow dataFlow, DeviceState stateMask, out IMMDeviceCollection deviceCollection);
        [PreserveSig] int GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice endpoint);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice endpoint);
        [PreserveSig] int RegisterEndpointNotificationCallback(nint pClient);
        [PreserveSig] int UnregisterEndpointNotificationCallback(nint pClient);
    }

    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint dwClsCtx, nint pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        [PreserveSig] int OpenPropertyStore(uint stgmAccess, out IPropertyStore properties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string strId);
        [PreserveSig] int GetState(out DeviceState state);
    }

    [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint iProp, out PropertyKey pkey);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant pv);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant pv);
        [PreserveSig] int Commit();
    }

    [Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat(string pszDeviceName, nint ppFormat);
        [PreserveSig] int GetDeviceFormat(string pszDeviceName, int bDefault, nint ppFormat);
        [PreserveSig] int ResetDeviceFormat(string pszDeviceName);
        [PreserveSig] int SetDeviceFormat(string pszDeviceName, nint pEndpointFormat, nint ppFormat);
        [PreserveSig] int GetProcessingPeriod(string pszDeviceName, int bDefault, nint pmftDefaultPeriod, nint pmftMinimumPeriod);
        [PreserveSig] int SetProcessingPeriod(string pszDeviceName, nint pmftPeriod);
        [PreserveSig] int GetShareMode(string pszDeviceName, nint pMode);
        [PreserveSig] int SetShareMode(string pszDeviceName, nint pMode);
        [PreserveSig] int GetPropertyValue(string pszDeviceName, int bFxStore, nint pKey, nint pv);
        [PreserveSig] int SetPropertyValue(string pszDeviceName, int bFxStore, nint pKey, nint pv);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ERole eRole);
        [PreserveSig] int SetEndpointVisibility(string pszDeviceName, int bVisible);
    }

    [Guid("568b9108-44bf-40b4-9006-86afe5b5a620"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPolicyConfigVista
    {
        [PreserveSig] int GetMixFormat(string pszDeviceName, nint ppFormat);
        [PreserveSig] int GetDeviceFormat(string pszDeviceName, int bDefault, nint ppFormat);
        [PreserveSig] int ResetDeviceFormat(string pszDeviceName);
        [PreserveSig] int SetDeviceFormat(string pszDeviceName, nint pEndpointFormat, nint ppFormat);
        [PreserveSig] int GetProcessingPeriod(string pszDeviceName, int bDefault, nint pmftDefaultPeriod, nint pmftMinimumPeriod);
        [PreserveSig] int SetProcessingPeriod(string pszDeviceName, nint pmftPeriod);
        [PreserveSig] int GetShareMode(string pszDeviceName, nint pMode);
        [PreserveSig] int SetShareMode(string pszDeviceName, nint pMode);
        [PreserveSig] int GetPropertyValue(string pszDeviceName, int bFxStore, nint pKey, nint pv);
        [PreserveSig] int SetPropertyValue(string pszDeviceName, int bFxStore, nint pKey, nint pv);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ERole eRole);
        [PreserveSig] int SetEndpointVisibility(string pszDeviceName, int bVisible);
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumeratorComObject { }

    [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    internal class PolicyConfigClientWin7 { }

    [ComImport, Guid("294935ce-f637-4e7c-a41b-ab255460b862")]
    internal class PolicyConfigClientVista { }
}

