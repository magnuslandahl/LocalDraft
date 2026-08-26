using System.Diagnostics;
using System.IO;
using System.Windows;
using LokalDiktering.Core;

namespace LokalDiktering.App;

public partial class SettingsWindow : Window
{
    private readonly IAudioDeviceService devices;
    private readonly ISettingsService settingsService;
    private readonly IAppPathService paths;
    private readonly IAudioRecorder recorder;
    private readonly bool firstRun;
    private AppSettings settings = new();
    private bool testing;

    public SettingsWindow(
        IAudioDeviceService devices,
        ISettingsService settingsService,
        IAppPathService paths,
        IAudioRecorder recorder,
        bool firstRun)
    {
        InitializeComponent();
        this.devices = devices;
        this.settingsService = settingsService;
        this.paths = paths;
        this.recorder = recorder;
        this.firstRun = firstRun;
        PathText.Text = paths.AppRoot;
        if (firstRun)
        {
            Title = "Välkommen";
            HeadingText.Text = "Välkommen till Lokal Diktering";
            IntroText.Text = "Allt sker lokalt på datorn. Börja med att välja och testa den mikrofon du vill diktera med.";
            CancelButton.Content = "Avsluta";
        }
        Loaded += SettingsWindow_Loaded;
        Closing += SettingsWindow_Closing;
        recorder.Progress += Recorder_Progress;
    }

    private async void SettingsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        settings = await settingsService.LoadAsync();
        var available = await devices.GetInputDevicesAsync();
        DeviceBox.ItemsSource = available;
        DeviceBox.SelectedItem = available.FirstOrDefault(x => x.Id == settings.SelectedMicrophoneId)
                                 ?? available.FirstOrDefault();
        if (available.Count == 0)
        {
            TestButton.IsEnabled = false;
            IntroText.Text = "Ingen mikrofon hittades. Kontrollera Windows mikrofonbehörighet och anslut en mikrofon.";
        }
    }

    private async void TestButton_Click(object sender, RoutedEventArgs e)
    {
        if (testing)
        {
            await StopTestAsync();
            return;
        }

        if (DeviceBox.SelectedItem is not AudioDevice device)
        {
            return;
        }

        var partial = Path.Combine(paths.TempRoot, "microphone-test.partial.wav");
        try
        {
            await recorder.StartAsync(device.Id, partial);
            testing = true;
            DeviceBox.IsEnabled = false;
            TestButton.Content = "Stoppa test";
        }
        catch (Exception)
        {
            MessageBox.Show(
                "Mikrofonen kunde inte startas. Kontrollera mikrofonbehörigheten i Windows och försök igen.",
                "Mikrofontest",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private async Task StopTestAsync()
    {
        if (!testing)
        {
            return;
        }

        await recorder.CancelAsync();
        testing = false;
        DeviceBox.IsEnabled = true;
        TestButton.Content = "Testa mikrofon";
        LevelBar.Width = 0;
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        await StopTestAsync();
        settings.SelectedMicrophoneId = (DeviceBox.SelectedItem as AudioDevice)?.Id;
        settings.FirstRunCompleted = settings.FirstRunCompleted || firstRun;
        await settingsService.SaveAsync(settings);
        DialogResult = true;
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = false };
        start.ArgumentList.Add(paths.AppRoot);
        Process.Start(start);
    }

    private void AboutButton_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show(
            "Lokal Diktering\n\nSvensk diktering och textredigering som körs helt lokalt.\n\nModeller: Whisper small q5_1 och Qwen3 1.7B Q4_K_M.\nLicenser finns i appmappen.",
            "Om Lokal Diktering",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

    private async void SettingsWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        recorder.Progress -= Recorder_Progress;
        await StopTestAsync();
        await recorder.DisposeAsync();
    }

    private void Recorder_Progress(object? sender, RecordingProgress progress) =>
        Dispatcher.Invoke(() => LevelBar.Width = Math.Max(2, Math.Min(570, progress.Level * 570)));
}
