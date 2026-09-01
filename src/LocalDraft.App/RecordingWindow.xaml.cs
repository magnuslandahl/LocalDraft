using System.IO;
using System.Windows;
using System.Windows.Media;
using LocalDraft.Core;

namespace LocalDraft.App;

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
    private AudioDevice? selectedDevice;
    private bool paused;
    private bool closing;

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
        await FindMicrophoneAndStartAsync();
    }

    private async Task FindMicrophoneAndStartAsync()
    {
        StartButton.IsEnabled = false;
        StartButton.Content = "Startar…";
        try
        {
            var available = await devices.GetInputDevicesAsync();
            if (closing)
            {
                return;
            }

            var settings = await settingsService.LoadAsync();
            if (closing)
            {
                return;
            }

            selectedDevice = available.FirstOrDefault(x => x.Id == settings.SelectedMicrophoneId)
                             ?? available.FirstOrDefault();
            if (selectedDevice is null)
            {
                StateText.Text = "Ingen mikrofon hittades";
                MicrophoneText.Text = "Ingen mikrofon vald";
                StartButton.Content = "Sök efter mikrofon";
                StartButton.IsEnabled = true;
                return;
            }

            MicrophoneText.Text = selectedDevice.Name;
            if (settings.SelectedMicrophoneId != selectedDevice.Id)
            {
                settings.SelectedMicrophoneId = selectedDevice.Id;
                await settingsService.SaveAsync(settings);
            }

            if (!closing)
            {
                await StartRecordingAsync();
            }
        }
        catch (Exception)
        {
            if (closing)
            {
                return;
            }

            selectedDevice = null;
            StateText.Text = "Mikrofonen kunde inte läsas. Kontrollera Windows mikrofonbehörighet.";
            MicrophoneText.Text = "Mikrofonen är inte tillgänglig";
            StartButton.Content = "Försök igen";
            StartButton.IsEnabled = true;
        }
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (recorder.IsRecording)
        {
            await FinishRecordingAsync();
            return;
        }

        if (selectedDevice is null)
        {
            await FindMicrophoneAndStartAsync();
        }
        else
        {
            await StartRecordingAsync();
        }
    }

    private async Task StartRecordingAsync()
    {
        if (closing || selectedDevice is not { } device)
        {
            return;
        }

        StartButton.IsEnabled = false;
        StartButton.Content = "Startar…";
        var directory = Path.Combine(paths.GetDocumentDirectory(documentId), "recordings");
        Directory.CreateDirectory(directory);
        partialPath = Path.Combine(directory, $"{recordingId:D}.partial.wav");
        try
        {
            await recorder.StartAsync(device.Id, partialPath);
            if (closing)
            {
                await recorder.CancelAsync();
                return;
            }

            RecordingDot.Fill = Brushes.Red;
            StateText.Text = "Spelar in…";
            StartButton.Content = "Klar – transkribera";
            StartButton.IsEnabled = true;
            PauseButton.Visibility = Visibility.Visible;
        }
        catch (Exception)
        {
            if (closing)
            {
                return;
            }

            StateText.Text = "Mikrofonen kunde inte startas. Kontrollera behörighet och försök igen.";
            StartButton.Content = "Försök igen";
            StartButton.IsEnabled = true;
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

    private async Task FinishRecordingAsync()
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
        closing = true;
        await recorder.CancelAsync();
        DialogResult = false;
    }

    private async void RecordingWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        closing = true;
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
            LevelBar.Width = AudioLevelMeter.GetWidth(progress.Level, LevelTrack.ActualWidth);
        });
    }

    private void SetControlsEnabled(bool value)
    {
        StartButton.IsEnabled = value;
        PauseButton.IsEnabled = value;
    }
}
