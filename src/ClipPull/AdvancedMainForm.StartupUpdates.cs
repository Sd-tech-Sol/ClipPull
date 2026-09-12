namespace ClipPull;

internal sealed partial class AdvancedMainForm
{
    private CancellationTokenSource? _startupDependencyUpdate;

    internal void EnableStartupDependencyUpdates()
    {
        Shown += async (_, _) => await RunStartupDependencyUpdatesAsync();
        FormClosing += (_, _) => _startupDependencyUpdate?.Cancel();
    }

    private async Task RunStartupDependencyUpdatesAsync()
    {
        if (_startupDependencyUpdate is not null || _activeOperation is not null)
            return;

        _startupDependencyUpdate = new CancellationTokenSource();
        var token = _startupDependencyUpdate.Token;
        SetBusy(true, allowCancel: false);
        SetProgress(0);

        try
        {
            SetStatusSafe("Démarrage • Vérification automatique des composants...");
            await _engineManager.EnsureAsync(message => SetStatusSafe($"Démarrage • {message.Resolve()}"), token);

            string ffmpegState;
            if (_ffmpegManager.IsInstalled)
            {
                var ffmpegProgress = new Progress<double>(SetProgress);
                ffmpegState = await _ffmpegManager.UpdateInstalledAsync(
                    message => SetStatusSafe($"Démarrage • {message.Resolve()}"),
                    ffmpegProgress,
                    token);
            }
            else
            {
                ffmpegState = "FFmpeg non installé";
            }

            SetProgress(0);
            SetStatusSafe($"Composants vérifiés • yt-dlp à jour • {ffmpegState}.");
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            SetProgress(0);
            SetStatusSafe($"Mise à jour non bloquante : {GetUsefulError(ex.Message)} ClipPull reste utilisable.");
        }
        finally
        {
            _startupDependencyUpdate.Dispose();
            _startupDependencyUpdate = null;
            SetBusy(false);
        }
    }
}
