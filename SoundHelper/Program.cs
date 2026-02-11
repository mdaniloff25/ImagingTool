using System;
using System.Data;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Threading;
using NAudio.CoreAudioApi;

class Program
{
    private const int CLSCTX_ALL = 23;
    private static readonly Guid CLSID_MMDeviceEnumerator =
        new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
    private static readonly Guid IID_IAudioEndpointVolume =
        new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");

    static int Main(string[] args)
    {
        try
        {
            EnsureAudioServiceRunning();
            SetVolumeWithRetry();
        }
        catch
        {
            // Never fail login because of audio
        }
        Console.WriteLine("Audio volume set to zero successfully or may have failed.");
        Console.ReadLine(); // Keep console open for debugging
        return 0; // Always exit cleanly
    }

    private static void EnsureAudioServiceRunning()
    {
        try
        {
            using (var sc = new ServiceController("Audiosrv"))
            {
                if (sc.Status != ServiceControllerStatus.Running)
                {
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running,
                        TimeSpan.FromSeconds(10));
                }
            }
        }
        catch
        {
            // Service not present or cannot start — ignore
            Console.WriteLine("Warning: Unable to ensure audio service is running.");
        }
    }

    private static void SetVolumeWithRetry()
    {
        const int maxAttempts = 30;
        const int delayMs = 1000;

        for (int i = 0; i < maxAttempts; i++)
        {
            if (TrySetVolumeZero())
                return;

            Thread.Sleep(delayMs);
        }
    }

    private static bool TrySetVolumeZero()
    {
        try
        {
            using (var enumerator = new MMDeviceEnumerator())
            {
                var device = TryGetAnyDefaultEndpoint(enumerator);

                if (device == null)
                    return false;

                device.AudioEndpointVolume.Mute = true;
                device.AudioEndpointVolume.MasterVolumeLevelScalar = 0f;

                return true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Attempt to set volume to zero failed: {ex.Message}");
            Console.WriteLine(ex.ToString());
            Console.WriteLine("Warning: Unable to set volume to zero.");
            return false; // Retry
        }
    }

    private static MMDevice TryGetAnyDefaultEndpoint(MMDeviceEnumerator enumerator)
    {
        Role[] roles = new[] { Role.Console, Role.Multimedia, Role.Communications };

        foreach (var role in roles)
        {
            try
            {
                return enumerator.GetDefaultAudioEndpoint(DataFlow.Render, role);
            }
            catch
            {
                // ignore
            }
        }

        return null;
    }
}

enum EDataFlow { eRender, eCapture, eAll }
enum ERole { eConsole, eMultimedia, eCommunications }

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceEnumerator
{
    void NotImpl1();
    void GetDefaultAudioEndpoint(
        EDataFlow dataFlow,
        ERole role,
        out IMMDevice ppDevice);
}

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDevice
{
    void Activate(
        ref Guid iid,
        int dwClsCtx,
        IntPtr pActivationParams,
        out object ppInterface);
}

[ComImport]
[Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioEndpointVolume
{
    void RegisterControlChangeNotify(IntPtr pNotify);
    void UnregisterControlChangeNotify(IntPtr pNotify);
    void GetChannelCount(out uint channelCount);
    void SetMasterVolumeLevel(float levelDB, Guid eventContext);
    void SetMasterVolumeLevelScalar(float level, Guid eventContext);
}
