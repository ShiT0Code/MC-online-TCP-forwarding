using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;

namespace McOnlineApp;
public class Program
{
    [STAThread]
    public static void Main()
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        bool isContinue = DecideContinue();

        if (isContinue)
        {
            Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App();
            });
        }
    }

    private static bool DecideContinue()
    {
        AppInstance keyInstance = AppInstance.FindOrRegisterForKey("main");
        if (keyInstance.IsCurrent)
            return true;
        else
        {
            RedirectActivationTo(keyInstance);
            return false;
        }
    }

    public static void RedirectActivationTo(AppInstance keyInstance)
    {
        // 将窗口恢复并置于前台
        Process process = Process.GetProcessById((int)keyInstance.ProcessId);
        if (!IsZoomed(process.MainWindowHandle))
            ShowWindow(process.MainWindowHandle, SW_RESTORE);
        SetForegroundWindow(process.MainWindowHandle);
    }

    // Windows API 外部方法调用
    [DllImport("user32.dll")]
    static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    const int SW_RESTORE = 9;

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(IntPtr hWnd);
}