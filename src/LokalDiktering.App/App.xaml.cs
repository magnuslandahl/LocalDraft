using System.Globalization;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Markup;
using LokalDiktering.Core;
using LokalDiktering.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace LokalDiktering.App;

public partial class App : Application
{
    private ServiceProvider? services;

    static App()
    {
        var root = AppContext.BaseDirectory;
        AppPathService.ConfigureProcessEnvironment(root);
        var swedish = CultureInfo.GetCultureInfo("sv-SE");
        CultureInfo.DefaultThreadCurrentCulture = swedish;
        CultureInfo.DefaultThreadCurrentUICulture = swedish;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(swedish.IetfLanguageTag)));
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        try
        {
            AppPathService.VerifyWritable(AppContext.BaseDirectory);
        }
        catch (AppRootNotWritableException)
        {
            MessageBox.Show(
                "Appen kan inte spara i den här mappen. Flytta hela appmappen till en lokal mapp där du har skrivbehörighet. Appen sparar aldrig information i AppData som reserv.",
                "Lokal Diktering",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(2);
            return;
        }

        var collection = new ServiceCollection();
        collection.AddSingleton<IAppPathService, AppPathService>();
        collection.AddSingleton<IDocumentRepository, DocumentRepository>();
        collection.AddSingleton<IVersionService, VersionService>();
        collection.AddSingleton<IRecordingRepository, RecordingRepository>();
        collection.AddSingleton<ISettingsService, SettingsService>();
        collection.AddSingleton<IAudioDeviceService, AudioDeviceService>();
        collection.AddTransient<IAudioRecorder, WasapiAudioRecorder>();
        collection.AddTransient<IAudioPlaybackService, AudioPlaybackService>();
        collection.AddSingleton<IPartialRecordingRecovery, PartialRecordingRecovery>();
        collection.AddSingleton<ITranscriptionService, WhisperCliTranscriptionService>();
        collection.AddSingleton<ITextAssistantService, LlamaTextAssistantService>();
        collection.AddSingleton<IAssistantHistoryService, AssistantHistoryService>();
        collection.AddSingleton<IModelManifestValidator, ModelManifestValidator>();
        collection.AddSingleton<ILocalLog, LocalLog>();
        collection.AddSingleton<MainWindowViewModel>();
        collection.AddSingleton<MainWindow>();
        services = collection.BuildServiceProvider();

        var paths = services.GetRequiredService<IAppPathService>();
        if (paths.IsSynchronizedLocation)
        {
            MessageBox.Show(
                "Appmappen verkar ligga i en synkroniserad plats. En sådan mapp kan kopiera känsliga dokument och inspelningar till en molntjänst. Flytta gärna hela appmappen till en vanlig lokal mapp. Kontrollen kan inte upptäcka alla synkroniseringstjänster.",
                "Kontrollera lagringsplatsen",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        await HandlePartialRecordingsAsync(services.GetRequiredService<IPartialRecordingRecovery>());

        var settings = await services.GetRequiredService<ISettingsService>().LoadAsync();
        if (!settings.FirstRunCompleted)
        {
            var welcome = new SettingsWindow(
                services.GetRequiredService<IAudioDeviceService>(),
                services.GetRequiredService<ISettingsService>(),
                services.GetRequiredService<IAppPathService>(),
                services.GetRequiredService<IAudioRecorder>(),
                true);
            if (welcome.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
        }

        MainWindow = services.GetRequiredService<MainWindow>();
        MainWindow.Show();
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        _ = ValidateModelsAsync(services.GetRequiredService<IModelManifestValidator>());
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (services is not null)
        {
            await services.DisposeAsync();
        }

        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        services?.GetService<ILocalLog>()?.Error("app.unhandled", e.Exception);
        MessageBox.Show(
            "Något gick fel. Dina senast sparade ändringar finns kvar. Starta om appen och försök igen.",
            "Lokal Diktering",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static async Task HandlePartialRecordingsAsync(IPartialRecordingRecovery recovery)
    {
        foreach (var partial in recovery.Find())
        {
            var answer = MessageBox.Show(
                "En ofullständig inspelning hittades efter att appen inte avslutades normalt. Vill du försöka återställa ljudet?",
                "Återställ inspelning",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (answer == MessageBoxResult.Yes)
            {
                try
                {
                    await recovery.RecoverAsync(partial);
                    continue;
                }
                catch (Exception)
                {
                    MessageBox.Show(
                        "Inspelningen kunde inte återställas. Du kan ta bort den ofullständiga filen nu.",
                        "Återställningen misslyckades",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

            }

            if (MessageBox.Show(
                    "Vill du ta bort den ofullständiga inspelningen permanent?",
                    "Ta bort ofullständig inspelning",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                await recovery.DeleteAsync(partial);
            }
        }
    }

    private static async Task ValidateModelsAsync(IModelManifestValidator validator)
        {
            IReadOnlyList<string> modelErrors;
            try
            {
                modelErrors = await validator.ValidateAsync();
            }
            catch (Exception)
            {
                modelErrors = ["Modellfilerna kunde inte kontrolleras."];
            }
            if (modelErrors.Count == 0)
            {
                return;
            }

            MessageBox.Show(
                string.Join(Environment.NewLine, modelErrors) +
                Environment.NewLine + Environment.NewLine +
                "Diktering och textassistent är inte tillgängliga förrän den kompletta appmappen har ersatts.",
                "Lokala modeller saknas eller är skadade",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
    }
}
