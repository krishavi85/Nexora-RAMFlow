using System.Diagnostics;
using System.Security.Principal;
using System.Windows;

namespace Nexora.RAMFlow.App.Services;

public static class AdminService
{
    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static void RestartElevated()
    {
        var executable = Environment.ProcessPath
            ?? throw new InvalidOperationException("The executable path could not be determined.");

        Process.Start(new ProcessStartInfo(executable)
        {
            UseShellExecute = true,
            Verb = "runas"
        });

        Application.Current.Shutdown();
    }
}
