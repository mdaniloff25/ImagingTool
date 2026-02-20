using System;
using System.Data;
using System.Diagnostics;
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

    private const string EVENT_SOURCE = "POSVolumeInit";
    private const string EVENT_LOG = "Application";

    static int Main(string[] args)
    {
        try
        {
            EnsureAudioServiceRunning();
            if (!SetVolumeWithRetry())
            {
                LogFailure("Failed to set volume to zero after multiple attempts.");
            }
            else
            {
                LogSuccess("Audio volume set to zero successfully on login.");
            }
        }
        catch
        {
            // Never fail login because of audio
        }
       
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
            LogFailure("Failed to ensure audio service is running. Audio may not be initialized properly.");
        }
    }

    private static bool SetVolumeWithRetry()
    {
        const int maxAttempts = 30;
        const int delayMs = 1000;

        for (int i = 0; i < maxAttempts; i++)
        {
            if (TrySetVolumeZero())
                return true;

            Thread.Sleep(delayMs);
        }

        return false;
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
        catch
        { 
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

    private static void LogSuccess(string message)
    {
        try
        {
            if (!EventLog.SourceExists(EVENT_SOURCE))
            {
                Console.WriteLine($"Event Source '{EVENT_SOURCE}' does not exist!");
                return;
            }

            EventLog.WriteEntry(EVENT_SOURCE,
                message,
                EventLogEntryType.Information,
                1001);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing to Event Log: {ex.Message}");
        }
    }

    private static void LogFailure(string message, Exception ex = null)
    {
        try
        {
            if (!EventLog.SourceExists(EVENT_SOURCE))
            {
                Console.WriteLine($"Event Source '{EVENT_SOURCE}' does not exist!");
                return;
            }

            string errorMessage = message;
            if (ex != null)
            {
                errorMessage += $"\n\nException: {ex.Message}\nStack Trace: {ex.StackTrace}";
            }

            EventLog.WriteEntry(EVENT_SOURCE,
                errorMessage,
                EventLogEntryType.Error,
                9001);
        }
        catch (Exception logEx)
        {
            Console.WriteLine($"Error writing to Event Log: {logEx.Message}");
        }
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
