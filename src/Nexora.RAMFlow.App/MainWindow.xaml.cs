using System.Windows;
using System.Windows.Threading;
using Nexora.RAMFlow.App.Services;
using Nexora.RAMFlow.Core.Models;
using Nexora.RAMFlow.Core.Services;

namespace Nexora.RAMFlow.App;

public partial class MainWindow : Window
{
    private readonly SystemMemoryReader _memoryReader = new();
    private readonly PageFileManager _pageFileManager = new();
    private readonly VirtualMemoryAdvisor _advisor = new();
    private readonly DispatcherTimer _refreshTimer;
    private MemorySnapshot? _snapshot;
    private VirtualMemoryRecommendation? _recommendation;
    private bool _refreshing;

    public MainWindow()
    {
        InitializeComponent();
        WorkloadProfileCombo.ItemsSource = Enum.GetValues<WorkloadProfile>();
        WorkloadProfileCombo.SelectedItem = WorkloadProfile.Balanced;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
        Loaded += async (_, _) =>
        {
            ElevateButton.Visibility = AdminService.IsAdministrator() ? Visibility.Collapsed : Visibility.Visible;
            await RefreshAsync();
            _refreshTimer.Start();
        };
        Closed += (_, _) => _refreshTimer.Stop();
    }

    private async Task RefreshAsync()
    {
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;
        try
        {
            _snapshot = await _memoryReader.CaptureAsync();
            PhysicalUsageText.Text = $"{_snapshot.PhysicalUsagePercent:0}% used";
            PhysicalDetailText.Text = $"{ByteSizeFormatter.Format(_snapshot.AvailablePhysicalBytes)} available of {ByteSizeFormatter.Format(_snapshot.TotalPhysicalBytes)}";
            VirtualUsageText.Text = $"{_snapshot.VirtualUsagePercent:0}% used";
            VirtualDetailText.Text = $"{ByteSizeFormatter.Format(_snapshot.AvailableVirtualBytes)} available of {ByteSizeFormatter.Format(_snapshot.TotalVirtualBytes)}";
            DiskText.Text = $"{ByteSizeFormatter.Format(_snapshot.SystemDriveFreeBytes)} free";
            PressureText.Text = PressureLabel(_snapshot.PhysicalUsagePercent);

            var allocated = _snapshot.PageFiles.Aggregate(0UL, (sum, item) => sum + item.AllocatedMegabytes);
            var used = _snapshot.PageFiles.Aggregate(0UL, (sum, item) => sum + item.CurrentUsageMegabytes);
            PageFileText.Text = _snapshot.PageFiles.Count == 0 ? "Not detected" : $"{allocated:N0} MB / {used:N0} MB";
            ProcessListView.ItemsSource = _snapshot.TopProcesses;
            LastUpdatedText.Text = $"Updated {_snapshot.CapturedAt.LocalDateTime:T}";
            StatusBanner.Text = "Monitoring is active. No settings are changed without confirmation.";
        }
        catch (Exception ex)
        {
            StatusBanner.Text = $"Unable to read all Windows memory data: {ex.Message}";
        }
        finally
        {
            _refreshing = false;
        }
    }

    private static string PressureLabel(double percent) => percent switch
    {
        >= 90 => "Memory pressure: critical",
        >= 75 => "Memory pressure: high",
        >= 55 => "Memory pressure: moderate",
        _ => "Memory pressure: normal"
    };

    private async void RefreshButton_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private void AnalyzeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_snapshot is null || WorkloadProfileCombo.SelectedItem is not WorkloadProfile profile)
        {
            StatusBanner.Text = "Wait for the first system scan to finish.";
            return;
        }

        _recommendation = _advisor.Recommend(_snapshot, profile);
        RecommendationTitle.Text = _recommendation.Title;
        RecommendationSize.Text = _recommendation.UseSystemManaged
            ? "Automatically sized by Windows"
            : $"{_recommendation.InitialSizeMegabytes:N0}–{_recommendation.MaximumSizeMegabytes:N0} MB";
        RecommendationBody.Text = _recommendation.Explanation;
        RecommendationWarning.Text = _recommendation.Warning;
        ApplyRecommendationButton.IsEnabled = !_recommendation.UseSystemManaged;
        StatusBanner.Text = "Recommendation ready. Review it before applying any change.";
    }

    private async void ApplyRecommendationButton_Click(object sender, RoutedEventArgs e)
    {
        if (_recommendation is null || _recommendation.UseSystemManaged)
        {
            return;
        }

        if (!AdminService.IsAdministrator())
        {
            MessageBox.Show("Restart RAMFlow as administrator before changing the page file.", "Administrator rights required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirmation = MessageBox.Show(
            $"Set the Windows page file to {_recommendation.InitialSizeMegabytes:N0} MB initial and {_recommendation.MaximumSizeMegabytes:N0} MB maximum?\n\nA backup will be created first. A Windows restart may be required.",
            "Apply virtual-memory profile",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        SetBusy(true, "Creating backup and applying the page-file profile…");
        try
        {
            var result = await _pageFileManager.SetCustomAsync(_recommendation.InitialSizeMegabytes, _recommendation.MaximumSizeMegabytes);
            MessageBox.Show(result.Message, result.Success ? "RAMFlow" : "RAMFlow could not apply the change", MessageBoxButton.OK,
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
            StatusBanner.Text = result.Message;
            await RefreshAsync();
        }
        finally
        {
            SetBusy(false, StatusBanner.Text);
        }
    }

    private async void UseSystemManagedButton_Click(object sender, RoutedEventArgs e)
    {
        if (!AdminService.IsAdministrator())
        {
            MessageBox.Show("Restart RAMFlow as administrator before changing the page file.", "Administrator rights required", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirmation = MessageBox.Show(
            "Return paging-file management to Windows? A backup will be created first and a restart may be required.",
            "Use Windows system-managed paging",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
        {
            return;
        }

        SetBusy(true, "Creating backup and restoring Windows-managed paging…");
        try
        {
            var result = await _pageFileManager.SetSystemManagedAsync();
            MessageBox.Show(result.Message, "RAMFlow", MessageBoxButton.OK,
                result.Success ? MessageBoxImage.Information : MessageBoxImage.Error);
            StatusBanner.Text = result.Message;
            await RefreshAsync();
        }
        finally
        {
            SetBusy(false, StatusBanner.Text);
        }
    }

    private void ElevateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            AdminService.RestartElevated();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Windows did not restart RAMFlow as administrator: {ex.Message}", "RAMFlow", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetBusy(bool isBusy, string message)
    {
        ApplyRecommendationButton.IsEnabled = !isBusy && _recommendation is { UseSystemManaged: false };
        WorkloadProfileCombo.IsEnabled = !isBusy;
        StatusBanner.Text = message;
    }
}
