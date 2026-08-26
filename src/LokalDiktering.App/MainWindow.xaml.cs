using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LokalDiktering.Core;
using Microsoft.Extensions.DependencyInjection;

namespace LokalDiktering.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel viewModel;
    private readonly IVersionService versions;
    private readonly IRecordingRepository recordings;
    private readonly IAudioDeviceService audioDevices;
    private readonly Func<IAudioRecorder> recorderFactory;
    private readonly Func<IAudioPlaybackService> playbackFactory;
    private readonly ITranscriptionService transcription;
    private readonly ITextAssistantService assistant;
    private readonly IAssistantHistoryService assistantHistory;
    private readonly IAppPathService paths;
    private readonly IServiceProvider services;
    private readonly DispatcherTimer autosaveTimer;
    private readonly DispatcherTimer versionTimer;
    private bool loading;
    private CancellationTokenSource? operationCancellation;
    private TextPointer? assistantSelectionStart;
    private TextPointer? assistantSelectionEnd;
    private AssistantAction pendingAssistantAction;
    private bool closingAfterSave;
    private readonly SemaphoreSlim saveLock = new(1, 1);

    public MainWindow(
        MainWindowViewModel viewModel,
        IVersionService versions,
        IRecordingRepository recordings,
        IAudioDeviceService audioDevices,
        ITranscriptionService transcription,
        ITextAssistantService assistant,
        IAssistantHistoryService assistantHistory,
        IAppPathService paths,
        IServiceProvider services)
    {
        InitializeComponent();
        this.viewModel = viewModel;
        this.versions = versions;
        this.recordings = recordings;
        this.audioDevices = audioDevices;
        recorderFactory = services.GetRequiredService<IAudioRecorder>;
        playbackFactory = services.GetRequiredService<IAudioPlaybackService>;
        this.transcription = transcription;
        this.assistant = assistant;
        this.assistantHistory = assistantHistory;
        this.paths = paths;
        this.services = services;
        DataContext = viewModel;
        autosaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        versionTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        autosaveTimer.Tick += async (_, _) => await AutosaveAsync(false);
        versionTimer.Tick += async (_, _) => await AutosaveAsync(true);
        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await viewModel.InitializeAsync();
        LoadCurrentDocument();
    }

    private async void NewDocumentButton_Click(object sender, RoutedEventArgs e)
    {
        await AutosaveAsync(true);
        await viewModel.CreateAsync();
        LoadCurrentDocument();
    }

    private async void DocumentList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (loading || DocumentList.SelectedItem is not DocumentSummary summary ||
            viewModel.Current?.Metadata.Id == summary.Id)
        {
            return;
        }

        await AutosaveAsync(true);
        await viewModel.OpenAsync(summary.Id);
        LoadCurrentDocument();
    }

    private async void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (SearchHint is not null)
        {
            SearchHint.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        await viewModel.RefreshAsync();
    }

    private void DocumentMenuButton_Click(object sender, RoutedEventArgs e)
    {
        DocumentMenu.PlacementTarget = DocumentMenuButton;
        DocumentMenu.IsOpen = true;
    }

    private async void DeleteDocumentButton_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.Current is not { } current)
        {
            return;
        }

        var versionCount = (await versions.ListAsync(current.Metadata.Id)).Count;
        var recordingCount = (await recordings.ListAsync(current.Metadata.Id)).Count;
        var answer = MessageBox.Show(
            $"Vill du ta bort dokumentet permanent?\n\n{current.Metadata.Title}\n{versionCount} versioner och {recordingCount} inspelningar tas bort.\n\nDokumenttext, alla versioner, alla inspelningar och all chatthistorik tas bort. Detta går inte att ångra.",
            "Ta bort allt permanent",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel);
        if (answer != MessageBoxResult.OK)
        {
            return;
        }

        try
        {
            await viewModel.DeleteCurrentAsync();
            LoadCurrentDocument();
        }
        catch (Exception)
        {
            MessageBox.Show(
                "Dokumentet kunde inte tas bort helt. Kontrollera att inga andra program använder filerna och försök igen.",
                "Borttagningen misslyckades",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void LoadCurrentDocument()
    {
        if (viewModel.Current is not { } current)
        {
            return;
        }

        loading = true;
        try
        {
            TitleBox.Text = current.Metadata.Title;
            RichTextContent.Load(Editor, current.Content.Rtf);
            Editor.IsReadOnly = false;
            PreviewPanel.Visibility = Visibility.Collapsed;
            AssistantComposerPanel.Visibility = Visibility.Visible;
            SelectionAssistantBar.Visibility = Visibility.Collapsed;
            _ = RefreshAssistantHistoryAsync();
        }
        finally
        {
            loading = false;
        }
    }

    private void EditorChanged(object sender, TextChangedEventArgs e)
    {
        if (loading)
        {
            return;
        }

        autosaveTimer.Stop();
        versionTimer.Stop();
        autosaveTimer.Start();
        versionTimer.Start();
        viewModel.Status = "Ändringar väntar på att sparas…";
    }

    private async Task<bool> AutosaveAsync(bool commitVersion)
    {
        if (loading || viewModel.Current is null)
        {
            return true;
        }

        autosaveTimer.Stop();
        if (commitVersion)
        {
            versionTimer.Stop();
        }

        var documentId = viewModel.Current.Metadata.Id;
        var title = string.IsNullOrWhiteSpace(TitleBox.Text) ? "Namnlöst dokument" : TitleBox.Text;
        var content = RichTextContent.Read(Editor);
        try
        {
            await SaveDocumentAsync(
                documentId,
                title,
                content,
                commitVersion ? "Manuell redigering" : null);
            return true;
        }
        catch (Exception)
        {
            viewModel.Status = "Ändringarna kunde inte sparas – försök igen";
            return false;
        }
    }

    private void BoldButton_Click(object sender, RoutedEventArgs e) =>
        EditingCommands.ToggleBold.Execute(null, Editor);

    private void ItalicButton_Click(object sender, RoutedEventArgs e) =>
        EditingCommands.ToggleItalic.Execute(null, Editor);

    private void BulletButton_Click(object sender, RoutedEventArgs e) =>
        EditingCommands.ToggleBullets.Execute(null, Editor);

    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        if (Editor.CanUndo) Editor.Undo();
    }

    private void RedoButton_Click(object sender, RoutedEventArgs e)
    {
        if (Editor.CanRedo) Editor.Redo();
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e) => Editor.Copy();
    private void PasteButton_Click(object sender, RoutedEventArgs e) => Editor.Paste();
    private void CopyAllButton_Click(object sender, RoutedEventArgs e) => RichTextContent.CopyAll(Editor);

    private void StyleBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || StyleBox.SelectedIndex < 0)
        {
            return;
        }

        var size = StyleBox.SelectedIndex switch { 1 => 28d, 2 => 21d, _ => 15d };
        Editor.Selection.ApplyPropertyValue(TextElement.FontSizeProperty, size);
        Editor.Selection.ApplyPropertyValue(
            TextElement.FontWeightProperty,
            StyleBox.SelectedIndex == 0 ? FontWeights.Normal : FontWeights.SemiBold);
    }

    private void Editor_SelectionChanged(object sender, RoutedEventArgs e)
    {
        AssistantScope.Text = Editor.Selection.IsEmpty ? "Hela dokumentet" : "Markerad text";
        SelectionAssistantBar.Visibility =
            !Editor.Selection.IsEmpty && AssistantPanel.Visibility != Visibility.Visible
                ? Visibility.Visible
                : Visibility.Collapsed;
    }

    private void OpenAssistantButton_Click(object sender, RoutedEventArgs e)
    {
        SelectionAssistantBar.Visibility = Visibility.Collapsed;
        AssistantPanel.Visibility = Visibility.Visible;
        CustomInstruction.Focus();
    }

    private void CloseAssistantPanelButton_Click(object sender, RoutedEventArgs e)
    {
        CloseAssistantPreview();
        AssistantPanel.Visibility = Visibility.Collapsed;
        SelectionAssistantBar.Visibility = Editor.Selection.IsEmpty
            ? Visibility.Collapsed
            : Visibility.Visible;
        Editor.Focus();
    }

    private async void DictateButton_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.Current is not { } current)
        {
            return;
        }

        var documentId = current.Metadata.Id;
        SetWorkspaceEnabled(false);
        try
        {
            await AutosaveAsync(true);
            var caretOffset = Editor.Document.ContentStart.GetOffsetToPosition(Editor.CaretPosition);
            var dialog = new RecordingWindow(
                audioDevices,
                recorderFactory(),
                paths,
                services.GetRequiredService<ISettingsService>(),
                documentId)
            {
                Owner = this
            };
            if (dialog.ShowDialog() != true || dialog.Outcome is not { } outcome)
            {
                return;
            }

            var metadata = new RecordingMetadata
            {
                Id = outcome.RecordingId,
                DocumentId = documentId,
                CreatedUtc = DateTimeOffset.UtcNow,
                Duration = outcome.Duration,
                State = RecordingState.Transcribing
            };
            await recordings.SaveAsync(metadata);
            await RunBusyAsync("Transkriberar lokalt…", async token =>
            {
                try
                {
                    var result = await transcription.TranscribeAsync(outcome.WavPath, token);
                    metadata.State = RecordingState.Transcribed;
                    await recordings.SaveAsync(metadata, token);
                    if (viewModel.Current?.Metadata.Id != documentId)
                    {
                        throw new InvalidOperationException("Dokumentet byttes under transkriberingen.");
                    }
                    var insertion = Editor.Document.ContentStart.GetPositionAtOffset(caretOffset, LogicalDirection.Forward)
                        ?? Editor.Document.ContentEnd;
                    insertion.InsertTextInRun(AddSensibleWhitespace(insertion, result.Text));
                    await SaveDocumentAsync(documentId, TitleBox.Text, RichTextContent.Read(Editor), "Diktering");
                    viewModel.Status = "Dikteringen har lagts in";
                }
                catch (OperationCanceledException)
                {
                    metadata.State = RecordingState.Ready;
                    await recordings.SaveAsync(metadata);
                    viewModel.Status = "Transkriberingen avbröts – inspelningen finns kvar";
                }
                catch (Exception)
                {
                    metadata.State = RecordingState.Failed;
                    metadata.FailureCategory = "transcription";
                    await recordings.SaveAsync(metadata);
                    MessageBox.Show(
                        "Transkriberingen misslyckades. Inspelningen finns kvar och kan transkriberas igen.",
                        "Kunde inte transkribera",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            });
        }
        finally
        {
            SetWorkspaceEnabled(true);
        }
    }

    private async void AssistantActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string tag } ||
            !Enum.TryParse<AssistantAction>(tag, out var action))
        {
            return;
        }

        var selected = !Editor.Selection.IsEmpty;
        var source = selected
            ? Editor.Selection.Text
            : new TextRange(Editor.Document.ContentStart, Editor.Document.ContentEnd).Text.TrimEnd();
        if (string.IsNullOrWhiteSpace(source))
        {
            MessageBox.Show("Skriv eller diktera text först.", "Textassistent", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        assistantSelectionStart = selected ? Editor.Selection.Start : null;
        assistantSelectionEnd = selected ? Editor.Selection.End : null;
        pendingAssistantAction = action;
        AssistantPanel.Visibility = Visibility.Visible;
        SelectionAssistantBar.Visibility = Visibility.Collapsed;
        await RunBusyAsync("Bearbetar text lokalt…", async token =>
        {
            var result = await assistant.ProcessAsync(
                new AssistantRequest(action, source, CustomInstruction.Text),
                token);
            if (result.MissingProtectedTokens.Count > 0)
            {
                MessageBox.Show(
                    "Förslaget saknar ett eller flera namn eller exakta värden och kan därför inte användas. Prova igen med en tydligare instruktion.",
                    "Förslaget stoppades",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            AssistantPreviewBox.Text = result.Text;
            ApplyAssistantButton.Content = selected ? "Använd på markerad text" : "Använd i hela dokumentet";
            AssistantComposerPanel.Visibility = Visibility.Collapsed;
            PreviewPanel.Visibility = Visibility.Visible;
            await assistantHistory.AddAsync(
                viewModel.Current!.Metadata.Id,
                new AssistantHistoryEntry
                {
                    Id = Guid.NewGuid(),
                    CreatedUtc = DateTimeOffset.UtcNow,
                    Action = AssistantActionLabel(action),
                    Result = result.Text
                },
                token);
            await RefreshAssistantHistoryAsync();
        });
    }

    private async void ApplyAssistantButton_Click(object sender, RoutedEventArgs e)
    {
        await versions.CommitAsync(viewModel.Current!.Metadata.Id, RichTextContent.Read(Editor), "Före AI-ändring");
        if (assistantSelectionStart is not null && assistantSelectionEnd is not null)
        {
            new TextRange(assistantSelectionStart, assistantSelectionEnd).Text = AssistantPreviewBox.Text;
        }
        else
        {
            Editor.Document = MarkdownFlowDocument.Parse(AssistantPreviewBox.Text);
        }

        await ApplyAssistantResultAsync();
    }

    private async void InsertAssistantButton_Click(object sender, RoutedEventArgs e)
    {
        Editor.CaretPosition.InsertTextInRun(AssistantPreviewBox.Text);
        await ApplyAssistantResultAsync();
    }

    private void CopyAssistantButton_Click(object sender, RoutedEventArgs e) =>
        Clipboard.SetText(AssistantPreviewBox.Text);

    private void CancelAssistantButton_Click(object sender, RoutedEventArgs e) =>
        CloseAssistantPreview();

    private async void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.Current is null ||
            MessageBox.Show(
                "Vill du rensa textassistentens historik? Dokumentet och dess versioner påverkas inte.",
                "Rensa chatt",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning) != MessageBoxResult.OK)
        {
            return;
        }

        await assistantHistory.ClearAsync(viewModel.Current.Metadata.Id);
        await RefreshAssistantHistoryAsync();
    }

    private async Task RefreshAssistantHistoryAsync()
    {
        if (viewModel.Current is null)
        {
            AssistantHistoryList.ItemsSource = null;
            return;
        }

        var entries = (await assistantHistory.ListAsync(viewModel.Current.Metadata.Id))
            .OrderByDescending(x => x.CreatedUtc)
            .ToArray();
        AssistantHistoryList.ItemsSource = entries;
        AssistantHistorySection.Visibility = entries.Length == 0
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private async Task ApplyAssistantResultAsync()
    {
        var reason = pendingAssistantAction == AssistantAction.Custom
            ? "AI: Egen instruktion"
            : $"AI: {AssistantActionLabel(pendingAssistantAction)}";
        await SaveDocumentAsync(viewModel.Current!.Metadata.Id, TitleBox.Text, RichTextContent.Read(Editor), reason);
        CloseAssistantPreview();
        viewModel.Status = "Textassistentens förslag har tillämpats";
    }

    private async void VersionsButton_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.Current is not { } current)
        {
            return;
        }

        await AutosaveAsync(true);
        var dialog = new VersionsWindow(await versions.ListAsync(current.Metadata.Id)) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.SelectedVersion is null)
        {
            return;
        }

        var content = await versions.LoadAsync(current.Metadata.Id, dialog.SelectedVersion.Id);
        RichTextContent.Load(Editor, content.Rtf);
        await SaveDocumentAsync(current.Metadata.Id, TitleBox.Text, content, "Återställd version");
    }

    private async void RecordingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (viewModel.Current is not { } current)
        {
            return;
        }

        var selectionStart = Editor.Selection.IsEmpty ? null : Editor.Selection.Start;
        var selectionEnd = Editor.Selection.IsEmpty ? null : Editor.Selection.End;
        var dialog = new RecordingsWindow(
            current.Metadata.Id,
            recordings,
            playbackFactory(),
            transcription,
            paths,
            selectionStart is not null)
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.TranscriptToInsert))
        {
            if (dialog.ReplaceSelection && selectionStart is not null && selectionEnd is not null)
            {
                new TextRange(selectionStart, selectionEnd).Text = dialog.TranscriptToInsert;
            }
            else
            {
                Editor.CaretPosition.InsertTextInRun(dialog.TranscriptToInsert);
            }
            await SaveDocumentAsync(
                current.Metadata.Id,
                TitleBox.Text,
                RichTextContent.Read(Editor),
                "Diktering (återanvänd inspelning)");
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(
            services.GetRequiredService<IAudioDeviceService>(),
            services.GetRequiredService<ISettingsService>(),
            services.GetRequiredService<IAppPathService>(),
            services.GetRequiredService<IAudioRecorder>(),
            false)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private async Task RunBusyAsync(string status, Func<CancellationToken, Task> action)
    {
        operationCancellation = new CancellationTokenSource();
        viewModel.IsBusy = true;
        viewModel.Status = status;
        Editor.IsReadOnly = true;
        SetWorkspaceEnabled(false);
        CancelOperationButton.Visibility = Visibility.Visible;
        try
        {
            await action(operationCancellation.Token);
        }
        finally
        {
            Editor.IsReadOnly = PreviewPanel.Visibility == Visibility.Visible;
            SetWorkspaceEnabled(true);
            CancelOperationButton.Visibility = Visibility.Collapsed;
            operationCancellation.Dispose();
            operationCancellation = null;
            viewModel.IsBusy = false;
        }
    }

    private void CancelOperationButton_Click(object sender, RoutedEventArgs e) => operationCancellation?.Cancel();

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (closingAfterSave)
        {
            return;
        }

        e.Cancel = true;
        autosaveTimer.Stop();
        versionTimer.Stop();
        if (operationCancellation is not null)
        {
            operationCancellation.Cancel();
            viewModel.Status = "Avbryter pågående arbete…";
            return;
        }

        SetWorkspaceEnabled(false);
        if (!await AutosaveAsync(false) &&
            MessageBox.Show(
                "De senaste ändringarna kunde inte sparas. Vill du avsluta ändå?",
                "Ändringarna är inte sparade",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No) != MessageBoxResult.Yes)
        {
            SetWorkspaceEnabled(true);
            return;
        }

        closingAfterSave = true;
        Close();
    }

    private async Task SaveDocumentAsync(
        Guid documentId,
        string title,
        DocumentContent content,
        string? reason)
    {
        await saveLock.WaitAsync();
        try
        {
            await viewModel.SaveAsync(documentId, title, content, reason);
        }
        finally
        {
            saveLock.Release();
        }
    }

    private void SetWorkspaceEnabled(bool enabled)
    {
        DocumentSidebar.IsEnabled = enabled;
        EditorPanel.IsEnabled = enabled;
        AssistantPanel.IsEnabled = enabled;
    }

    private void CloseAssistantPreview()
    {
        PreviewPanel.Visibility = Visibility.Collapsed;
        AssistantComposerPanel.Visibility = Visibility.Visible;
        Editor.IsReadOnly = false;
        assistantSelectionStart = null;
        assistantSelectionEnd = null;
    }

    private static string AddSensibleWhitespace(TextPointer insertion, string text)
    {
        var before = insertion.GetTextInRun(LogicalDirection.Backward);
        var prefix = before.Length > 0 && !char.IsWhiteSpace(before[^1]) ? " " : string.Empty;
        return prefix + text.Trim() + " ";
    }

    private static string AssistantActionLabel(AssistantAction action) => action switch
    {
        AssistantAction.Cleanup => "Renskriv",
        AssistantAction.Summarize => "Sammanfatta",
        AssistantAction.Structure => "Strukturera",
        AssistantAction.Improve => "Förbättra språket",
        AssistantAction.BulletList => "Gör punktlista",
        _ => "Egen instruktion"
    };
}

internal static class RichTextContent
{
    public static DocumentContent Read(RichTextBox editor)
    {
        var range = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd);
        using var stream = new MemoryStream();
        range.Save(stream, DataFormats.Rtf);
        return new DocumentContent(
            System.Text.Encoding.UTF8.GetString(stream.ToArray()),
            range.Text.TrimEnd('\r', '\n'));
    }

    public static void Load(RichTextBox editor, string rtf)
    {
        var document = new FlowDocument();
        var range = new TextRange(document.ContentStart, document.ContentEnd);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(rtf));
        range.Load(stream, DataFormats.Rtf);
        editor.Document = document;
    }

    public static void CopyAll(RichTextBox editor)
    {
        var range = new TextRange(editor.Document.ContentStart, editor.Document.ContentEnd);
        using var stream = new MemoryStream();
        range.Save(stream, DataFormats.Rtf);
        var data = new DataObject();
        data.SetData(DataFormats.Rtf, System.Text.Encoding.UTF8.GetString(stream.ToArray()));
        data.SetData(DataFormats.UnicodeText, range.Text.TrimEnd('\r', '\n'));
        Clipboard.SetDataObject(data, true);
    }
}

internal static class MarkdownFlowDocument
{
    public static FlowDocument Parse(string text)
    {
        var document = new FlowDocument
        {
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 15
        };
        List? activeList = null;
        foreach (var line in text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                activeList ??= new List { MarkerStyle = TextMarkerStyle.Disc };
                if (!document.Blocks.Contains(activeList))
                {
                    document.Blocks.Add(activeList);
                }
                activeList.ListItems.Add(new ListItem(CreateParagraph(line[2..], 15, FontWeights.Normal)));
                continue;
            }

            activeList = null;
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                document.Blocks.Add(CreateParagraph(line[3..], 21, FontWeights.SemiBold));
            }
            else if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                document.Blocks.Add(CreateParagraph(line[2..], 28, FontWeights.SemiBold));
            }
            else
            {
                document.Blocks.Add(CreateParagraph(line, 15, FontWeights.Normal));
            }
        }
        return document;
    }

    private static Paragraph CreateParagraph(string text, double size, FontWeight weight)
    {
        var paragraph = new Paragraph { FontSize = size, FontWeight = weight };
        var parts = text.Split("**");
        for (var i = 0; i < parts.Length; i++)
        {
            paragraph.Inlines.Add(new Run(parts[i]) { FontWeight = i % 2 == 1 ? FontWeights.Bold : weight });
        }
        return paragraph;
    }
}
