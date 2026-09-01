using System.IO;
using System.Windows;
using LocalDraft.Core;

namespace LocalDraft.App;

public partial class RecordingsWindow : Window
{
    private readonly Guid documentId;
    private readonly IRecordingRepository recordings;
    private readonly IAudioPlaybackService playback;
    private readonly ITranscriptionService transcription;
    private readonly IAppPathService paths;

    public RecordingsWindow(
        Guid documentId,
        IRecordingRepository recordings,
        IAudioPlaybackService playback,
        ITranscriptionService transcription,
        IAppPathService paths,
        bool hasSelection)
    {
        InitializeComponent();
        this.documentId = documentId;
        this.recordings = recordings;
        this.playback = playback;
        this.transcription = transcription;
        this.paths = paths;
        ReplaceButton.Tag = hasSelection;
        Loaded += async (_, _) => await RefreshAsync();
        Closed += async (_, _) => await playback.DisposeAsync();
        RecordingGrid.SelectionChanged += RecordingGrid_SelectionChanged;
    }

    public string? TranscriptToInsert { get; private set; }
    public bool ReplaceSelection { get; private set; }

    private async Task RefreshAsync()
    {
        var items = await recordings.ListAsync(documentId);
        RecordingGrid.ItemsSource = items;
        EmptyStateText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RecordingActionsPanel.Visibility = Visibility.Collapsed;
    }

    private void RecordingGrid_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        RecordingActionsPanel.Visibility = RecordingGrid.SelectedItem is null
            ? Visibility.Collapsed
            : Visibility.Visible;

    private async void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        if (RecordingGrid.SelectedItem is not RecordingMetadata item)
        {
            return;
        }

        try
        {
            await playback.PlayAsync(GetWavPath(item.Id));
        }
        catch (Exception)
        {
            MessageBox.Show("Inspelningen kunde inte spelas upp.", "Inspelningar", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void RetranscribeButton_Click(object sender, RoutedEventArgs e)
    {
        if (RecordingGrid.SelectedItem is not RecordingMetadata item)
        {
            return;
        }

        RecordingGrid.IsEnabled = false;
        try
        {
            var result = await transcription.TranscribeAsync(GetWavPath(item.Id));
            PreviewBox.Text = result.Text;
            PreviewBox.Visibility = Visibility.Visible;
            InsertButton.Visibility = Visibility.Visible;
            ReplaceButton.Visibility = ReplaceButton.Tag is true ? Visibility.Visible : Visibility.Collapsed;
            CopyButton.Visibility = Visibility.Visible;
            item.State = RecordingState.Transcribed;
            await recordings.SaveAsync(item);
        }
        catch (Exception)
        {
            item.State = RecordingState.Failed;
            await recordings.SaveAsync(item);
            MessageBox.Show(
                "Transkriberingen misslyckades. Ljudfilen finns kvar och kan provas igen.",
                "Inspelningar",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            RecordingGrid.IsEnabled = true;
            await RefreshAsync();
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (RecordingGrid.SelectedItem is not RecordingMetadata item)
        {
            return;
        }

        var answer = MessageBox.Show(
            "Vill du ta bort inspelningen permanent? Ljudfilen går inte att återställa. Text som redan har lagts in i dokumentet påverkas inte.",
            "Ta bort permanent",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel);
        if (answer != MessageBoxResult.OK)
        {
            return;
        }

        playback.Stop();
        try
        {
            await recordings.DeleteAsync(documentId, item.Id);
            await RefreshAsync();
        }
        catch (Exception)
        {
            MessageBox.Show(
                "Inspelningen kunde inte tas bort. Kontrollera att den inte används och försök igen.",
                "Borttagningen misslyckades",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void InsertButton_Click(object sender, RoutedEventArgs e)
    {
        TranscriptToInsert = PreviewBox.Text;
        DialogResult = true;
    }

    private void ReplaceButton_Click(object sender, RoutedEventArgs e)
    {
        TranscriptToInsert = PreviewBox.Text;
        ReplaceSelection = true;
        DialogResult = true;
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e) => Clipboard.SetText(PreviewBox.Text);

    private string GetWavPath(Guid recordingId) =>
        paths.EnsureContainedPath(
            Path.Combine(paths.GetDocumentDirectory(documentId), "recordings", $"{recordingId:D}.wav"),
            paths.DocumentsRoot);
}
