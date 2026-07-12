using System.Diagnostics;
using System.Reflection;
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

        var startInfo = new ProcessStartInfo(executable)
        {
            UseShellExecute = true,
            Verb = "runas"
        };

        // `dotnet run` starts the app through dotnet.exe. Preserve the application DLL in that development scenario.
        if (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var entryAssembly = Assembly.GetEntryAssembly()?.Location;
            if (string.IsNullOrWhiteSpace(entryAssembly))
            {
                throw new InvalidOperationException("The application assembly path could not be determined.");
            }

            startInfo.Arguments = $"\"{entryAssembly}\"";
        }

        Process.Start(startInfo);
        Application.Current.Shutdown();
    }
}
