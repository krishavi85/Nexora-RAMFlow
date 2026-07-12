using System.Management;
using System.Text.Json;

namespace Nexora.RAMFlow.App.Services;

public sealed record PageFileChangeResult(bool Success, bool RebootRequired, string Message);

public sealed class PageFileManager
{
    public Task<PageFileChangeResult> SetSystemManagedAsync() => Task.Run(() =>
    {
        EnsureAdministrator();
        var backupPath = BackupCurrentConfiguration();

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT AutomaticManagedPagefile FROM Win32_ComputerSystem");
            using var results = searcher.Get();
            var computer = results.Cast<ManagementObject>().FirstOrDefault()
                ?? throw new InvalidOperationException("Windows computer settings could not be loaded.");

            computer["AutomaticManagedPagefile"] = true;
            computer.Put();
            return new PageFileChangeResult(true, true,
                $"Windows system-managed paging has been enabled. Restart Windows to complete the change. Backup: {backupPath}");
        }
        catch (Exception ex)
        {
            return new PageFileChangeResult(false, false, $"The change failed. The previous configuration backup is at {backupPath}. {ex.Message}");
        }
    });

    public Task<PageFileChangeResult> SetCustomAsync(int initialSizeMegabytes, int maximumSizeMegabytes) => Task.Run(() =>
    {
        EnsureAdministrator();
        ValidateSizes(initialSizeMegabytes, maximumSizeMegabytes);
        ValidateDiskSpace(maximumSizeMegabytes);
        var backupPath = BackupCurrentConfiguration();

        try
        {
            SetAutomaticManagement(false);
            var systemPageFile = $"{Path.GetPathRoot(Environment.SystemDirectory)}pagefile.sys";

            using var searcher = new ManagementObjectSearcher("SELECT Name, InitialSize, MaximumSize FROM Win32_PageFileSetting");
            using var results = searcher.Get();
            var setting = results.Cast<ManagementObject>()
                .FirstOrDefault(item => string.Equals(Convert.ToString(item["Name"]), systemPageFile, StringComparison.OrdinalIgnoreCase));

            if (setting is null)
            {
                using var settingClass = new ManagementClass("Win32_PageFileSetting");
                setting = settingClass.CreateInstance();
                setting["Name"] = systemPageFile;
            }

            using (setting)
            {
                setting["InitialSize"] = initialSizeMegabytes;
                setting["MaximumSize"] = maximumSizeMegabytes;
                setting.Put();
            }

            return new PageFileChangeResult(true, true,
                $"The custom page-file profile was saved. Restart Windows to complete the change. Backup: {backupPath}");
        }
        catch (Exception ex)
        {
            return new PageFileChangeResult(false, false, $"The change failed. The previous configuration backup is at {backupPath}. {ex.Message}");
        }
    });

    private static void SetAutomaticManagement(bool enabled)
    {
        using var searcher = new ManagementObjectSearcher("SELECT AutomaticManagedPagefile FROM Win32_ComputerSystem");
        using var results = searcher.Get();
        var computer = results.Cast<ManagementObject>().FirstOrDefault()
            ?? throw new InvalidOperationException("Windows computer settings could not be loaded.");
        computer["AutomaticManagedPagefile"] = enabled;
        computer.Put();
    }

    private static string BackupCurrentConfiguration()
    {
        var settings = new List<PageFileBackupEntry>();
        using (var searcher = new ManagementObjectSearcher("SELECT Name, InitialSize, MaximumSize FROM Win32_PageFileSetting"))
        using (var results = searcher.Get())
        {
            foreach (ManagementObject item in results)
            {
                settings.Add(new PageFileBackupEntry(
                    Convert.ToString(item["Name"]) ?? string.Empty,
                    ReadUInt32(item["InitialSize"]),
                    ReadUInt32(item["MaximumSize"])));
            }
        }

        var automatic = true;
        using (var searcher = new ManagementObjectSearcher("SELECT AutomaticManagedPagefile FROM Win32_ComputerSystem"))
        using (var results = searcher.Get())
        {
            var computer = results.Cast<ManagementObject>().FirstOrDefault();
            if (computer?["AutomaticManagedPagefile"] is not null)
            {
                automatic = Convert.ToBoolean(computer["AutomaticManagedPagefile"]);
            }
        }

        var backup = new PageFileBackup(DateTimeOffset.Now, automatic, settings);
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nexora", "RAMFlow", "Backups");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"pagefile-{DateTime.Now:yyyyMMdd-HHmmss}.pagefile-backup.json");
        File.WriteAllText(path, JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    private static void ValidateSizes(int initial, int maximum)
    {
        if (initial < 256)
        {
            throw new ArgumentOutOfRangeException(nameof(initial), "The initial page-file size must be at least 256 MB.");
        }

        if (maximum < initial)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum), "The maximum size cannot be smaller than the initial size.");
        }

        if (maximum > 65_536)
        {
            throw new ArgumentOutOfRangeException(nameof(maximum), "RAMFlow limits custom profiles to 64 GB for safety.");
        }
    }

    private static void ValidateDiskSpace(int maximum)
    {
        var systemRoot = Path.GetPathRoot(Environment.SystemDirectory)
            ?? throw new InvalidOperationException("The Windows drive could not be determined.");
        var drive = new DriveInfo(systemRoot);
        var requiredBytes = (long)maximum * 1024L * 1024L;
        const long reserveBytes = 10L * 1024L * 1024L * 1024L;

        if (drive.AvailableFreeSpace - requiredBytes < reserveBytes)
        {
            throw new InvalidOperationException("The requested maximum would leave less than 10 GB free on the Windows drive.");
        }
    }

    private static void EnsureAdministrator()
    {
        if (!AdminService.IsAdministrator())
        {
            throw new UnauthorizedAccessException("Administrator rights are required to change paging-file settings.");
        }
    }

    private static uint ReadUInt32(object? value) => value is null ? 0U : Convert.ToUInt32(value);

    private sealed record PageFileBackup(DateTimeOffset CreatedAt, bool AutomaticManagedPagefile, IReadOnlyList<PageFileBackupEntry> Settings);
    private sealed record PageFileBackupEntry(string Name, uint InitialSizeMegabytes, uint MaximumSizeMegabytes);
}
