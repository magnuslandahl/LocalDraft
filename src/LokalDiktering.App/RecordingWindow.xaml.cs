using System.IO;
using System.Windows;
using System.Windows.Media;
using LokalDiktering.Core;

namespace LokalDiktering.App;

public sealed record RecordingOutcome(Guid RecordingId, string WavPath, TimeSpan Duration);

public partial class RecordingWindow : Window
{
    private readonly IAudioDeviceService devices;
    private readonly IAudioRecorder recorder;
    private readonly IAppPathService paths;
    private readonly ISettingsService settingsService;
    private readonly Guid documentId;
    private readonly Guid recordingId = Guid.NewGuid();
    private string? partialPath;
    private bool paused;

    public RecordingWindow(
        IAudioDeviceService devices,
        IAudioRecorder recorder,
        IAppPathService paths,
        ISettingsService settingsService,
        Guid documentId)
    {
        InitializeComponent();
        this.devices = devices;
        this.recorder = recorder;
        this.paths = paths;
        this.settingsService = settingsService;
        this.documentId = documentId;
        Loaded += RecordingWindow_Loaded;
        Closing += RecordingWindow_Closing;
        recorder.Progress += Recorder_Progress;
    }

    public RecordingOutcome? Outcome { get; private set; }

    private async void RecordingWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var available = await devices.GetInputDevicesAsync();
            var settings = await settingsService.LoadAsync();
            DeviceBox.ItemsSource = available;
            DeviceBox.SelectedItem = available.FirstOrDefault(x => x.Id == settings.SelectedMicrophoneId)
                                     ?? available.FirstOrDefault();
            StartButton.IsEnabled = DeviceBox.SelectedIndex >= 0;
            if (DeviceBox.Items.Count == 0)
            {
                StateText.Text = "Ingen mikrofon hittades";
            }
        }
        catch (Exception)
        {
            StateText.Text = "Mikrofonen kunde inte läsas. Kontrollera Windows mikrofonbehörighet.";
        }
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (DeviceBox.SelectedItem is not AudioDevice device)
        {
            return;
        }

        var directory = Path.Combine(paths.GetDocumentDirectory(documentId), "recordings");
        Directory.CreateDirectory(directory);
        partialPath = Path.Combine(directory, $"{recordingId:D}.partial.wav");
        try
        {
            var settings = await settingsService.LoadAsync();
            settings.SelectedMicrophoneId = device.Id;
            await settingsService.SaveAsync(settings);
            await recorder.StartAsync(device.Id, partialPath);
            RecordingDot.Fill = Brushes.Red;
            StateText.Text = "Spelar in…";
            StartButton.IsEnabled = false;
            DeviceBox.IsEnabled = false;
            PauseButton.IsEnabled = true;
            DoneButton.IsEnabled = true;
        }
        catch (Exception)
        {
            StateText.Text = "Mikrofonen kunde inte startas. Kontrollera behörighet och försök igen.";
            StartButton.Content = "Försök igen";
        }
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e)
    {
        paused = !paused;
        if (paused)
        {
            recorder.Pause();
            PauseButton.Content = "Fortsätt";
            StateText.Text = "Pausad";
            RecordingDot.Fill = Brushes.DarkRed;
        }
        else
        {
            recorder.Resume();
            PauseButton.Content = "Pausa";
            StateText.Text = "Spelar in…";
            RecordingDot.Fill = Brushes.Red;
        }
    }

    private async void DoneButton_Click(object sender, RoutedEventArgs e)
    {
        if (partialPath is null)
        {
            return;
        }

        SetControlsEnabled(false);
        StateText.Text = "Sparar inspelningen…";
        var finalPath = Path.Combine(Path.GetDirectoryName(partialPath)!, $"{recordingId:D}.wav");
        try
        {
            var duration = await recorder.StopAsync(finalPath);
            Outcome = new RecordingOutcome(recordingId, finalPath, duration);
            DialogResult = true;
        }
        catch (Exception)
        {
            StateText.Text = "Inspelningen kunde inte slutföras. Försök igen.";
            SetControlsEnabled(true);
        }
    }

    private async void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        await recorder.CancelAsync();
        DialogResult = false;
    }

    private async void RecordingWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        recorder.Progress -= Recorder_Progress;
        if (Outcome is null && recorder.IsRecording)
        {
            await recorder.CancelAsync();
        }
        await recorder.DisposeAsync();
    }

    private void Recorder_Progress(object? sender, RecordingProgress progress)
    {
        Dispatcher.Invoke(() =>
        {
            ElapsedText.Text = progress.Elapsed.ToString(@"mm\:ss");
            LevelBar.Width = Math.Max(2, Math.Min(480, progress.Level * 480));
        });
    }

    private void SetControlsEnabled(bool value)
    {
        PauseButton.IsEnabled = value;
        DoneButton.IsEnabled = value;
    }
}
