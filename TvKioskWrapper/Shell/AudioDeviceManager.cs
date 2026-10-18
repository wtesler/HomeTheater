using System.Diagnostics;
using System.Runtime.InteropServices;
using TvKioskWrapper.Interop;

namespace TvKioskWrapper.Shell;

/// <summary>
/// Manages Windows audio playback endpoints and provides in-order rotation through available devices.
/// </summary>
public sealed class AudioDeviceManager
{
    public sealed record AudioDeviceInfo(string Id, string Name);

    /// <summary>
    /// Enumerates all currently active audio playback (render) endpoints.
    /// </summary>
    public IReadOnlyList<AudioDeviceInfo> GetActivePlaybackDevices()
    {
        var devices = new List<AudioDeviceInfo>();

        try
        {
            var enumerator = (AudioInterop.IMMDeviceEnumerator)new AudioInterop.MMDeviceEnumeratorComObject();
            int hr = enumerator.EnumAudioEndpoints(
                AudioInterop.EDataFlow.eRender,
                AudioInterop.DeviceState.Active,
                out AudioInterop.IMMDeviceCollection collection);

            if (hr != 0 || collection is null)
            {
                return devices;
            }

            collection.GetCount(out uint count);
            for (uint i = 0; i < count; i++)
            {
                collection.Item(i, out AudioInterop.IMMDevice device);
                if (device is null)
                {
                    continue;
                }

                device.GetId(out string id);
                string name = id;

                if (device.OpenPropertyStore(0, out AudioInterop.IPropertyStore store) == 0 && store is not null)
                {
                    var key = AudioInterop.PropertyKey.PKEY_Device_FriendlyName;
                    var pv = new AudioInterop.PropVariant();
                    try
                    {
                        if (store.GetValue(ref key, out pv) == 0 && pv.pwszVal != nint.Zero)
                        {
                            name = Marshal.PtrToStringUni(pv.pwszVal) ?? id;
                        }
                    }
                    finally
                    {
                        NativeMethods.PropVariantClear(ref pv);
                    }
                }

                devices.Add(new AudioDeviceInfo(id, name));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioDeviceManager] Failed to enumerate audio endpoints: {ex.Message}");
        }

        return devices;
    }

    /// <summary>
    /// Gets the endpoint ID of the current default multimedia playback device.
    /// </summary>
    public string? GetDefaultPlaybackDeviceId()
    {
        try
        {
            var enumerator = (AudioInterop.IMMDeviceEnumerator)new AudioInterop.MMDeviceEnumeratorComObject();
            int hr = enumerator.GetDefaultAudioEndpoint(
                AudioInterop.EDataFlow.eRender,
                AudioInterop.ERole.eMultimedia,
                out AudioInterop.IMMDevice endpoint);

            if (hr == 0 && endpoint is not null)
            {
                endpoint.GetId(out string id);
                return id;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioDeviceManager] Failed to get default audio endpoint: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Rotates to the next active audio playback device in-order and sets it as the default.
    /// </summary>
    public AudioDeviceInfo? ToggleNextPlaybackDevice()
    {
        IReadOnlyList<AudioDeviceInfo> devices = GetActivePlaybackDevices();
        if (devices.Count == 0)
        {
            return null;
        }

        string? currentDefaultId = GetDefaultPlaybackDeviceId();
        int currentIndex = -1;
        if (!string.IsNullOrWhiteSpace(currentDefaultId))
        {
            for (int i = 0; i < devices.Count; i++)
            {
                if (string.Equals(devices[i].Id, currentDefaultId, StringComparison.OrdinalIgnoreCase))
                {
                    currentIndex = i;
                    break;
                }
            }
        }

        int nextIndex = (currentIndex + 1) % devices.Count;
        AudioDeviceInfo targetDevice = devices[nextIndex];

        bool success = SetDefaultPlaybackDevice(targetDevice.Id);
        Debug.WriteLine($"[AudioDeviceManager] Toggled to '{targetDevice.Name}' (success={success})");

        return targetDevice;
    }

    /// <summary>
    /// Sets the specified device as the default audio endpoint for Console, Multimedia, and Communications.
    /// </summary>
    public bool SetDefaultPlaybackDevice(string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return false;
        }

        // Try modern Windows 7/10/11 PolicyConfigClient first
        try
        {
            var policy = (AudioInterop.IPolicyConfig)new AudioInterop.PolicyConfigClientWin7();
            policy.SetDefaultEndpoint(deviceId, AudioInterop.ERole.eConsole);
            policy.SetDefaultEndpoint(deviceId, AudioInterop.ERole.eMultimedia);
            policy.SetDefaultEndpoint(deviceId, AudioInterop.ERole.eCommunications);
            return true;
        }
        catch
        {
            // Fallback for older PolicyConfigVista implementations
            try
            {
                var policyVista = (AudioInterop.IPolicyConfigVista)new AudioInterop.PolicyConfigClientVista();
                policyVista.SetDefaultEndpoint(deviceId, AudioInterop.ERole.eConsole);
                policyVista.SetDefaultEndpoint(deviceId, AudioInterop.ERole.eMultimedia);
                policyVista.SetDefaultEndpoint(deviceId, AudioInterop.ERole.eCommunications);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioDeviceManager] Failed to set default endpoint: {ex.Message}");
                return false;
            }
        }
    }
}

