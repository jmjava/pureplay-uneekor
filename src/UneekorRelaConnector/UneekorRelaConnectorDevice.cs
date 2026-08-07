using System.IO;
using System.Windows;
using relaDevicePlugin;

namespace UneekorRelaConnector;

/// <summary>
/// rēlā / PurePlay "Other" launch-monitor plugin that feeds Uneekor VIEW ShotData into the host.
/// Deploy: copy UneekorRelaConnector.dll next to rela.exe, Device Type = Other, Search.
/// </summary>
public sealed class UneekorRelaConnectorDevice : ILMDevice, IDeviceSettingsProvider
{
    private UneekorConnectorSettings _settings = new();
    private UneekorShotDataWatcher? _watcher;
    private string _mode = "NORMAL";
    private string _handed = "RH";
    private bool _connected;
    private string? _lastClub;

    public bool SessionConnected => _connected;
    public bool OtherIsReady { get; private set; }
    public bool OtherBallPresent { get; private set; }
    public bool OtherIsArmed { get; private set; }
    public bool UsesTelemetryFinalShotPath => true;
    public string SupportedConnectionTypes => "Direct";
    public long LastStatusUtcTicks { get; private set; } = DateTime.UtcNow.Ticks;

    public event Action<DeviceShotData> OnBallData = delegate { };
    public event Action<DeviceShotData> OnShotEnded = delegate { };
    public event Action<DeviceShotData> OnShot = delegate { };
    public event Action<DeviceRawShot> OnRawShot = delegate { };
    public event Action<string> OnNotification = delegate { };
    public event Action<string> OnHandedChange = delegate { };
    public event Action<string> OnModeChange = delegate { };
    public event Action<string> OnError = delegate { };
    public event Action<string> OnNote = delegate { };

    public void Init()
    {
        try
        {
            _settings = UneekorConnectorSettings.Load();
            _mode = _settings.Mode;
            _handed = _settings.Handedness;
        }
        catch (Exception ex)
        {
            OnError.Invoke("[Uneekor] Settings load failed: " + ex.Message);
        }

        OtherIsReady = false;
        OtherBallPresent = false;
        OtherIsArmed = false;
        _connected = false;
        TouchActivity();
        OnNotification.Invoke("[Uneekor] Initialized.");
    }

    public string GetDeviceName() => "Uneekor VIEW (ShotData)";

    public void ShowDeviceSettings()
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(ShowDeviceSettings);
            return;
        }

        try
        {
            var candidate = _settings.Copy();
            if (!UneekorConnectorSettingsDialog.TryCollect(candidate))
                return;

            candidate.Save();
            _settings = candidate;
            _mode = candidate.Mode;
            _handed = candidate.Handedness;
            OnModeChange.Invoke(_mode);
            OnHandedChange.Invoke(_handed);
            OnNotification.Invoke("[Uneekor] Device settings saved.");

            if (_connected)
            {
                // Restart watcher if path/options changed.
                Disconnect();
                Connect();
            }
        }
        catch (Exception ex)
        {
            OnError.Invoke("[Uneekor] Settings failed: " + ex.Message);
        }
    }

    public bool Discover()
    {
        // Host Search does not call Connect() afterward — Discover must start the live session.
        return Connect();
    }

    public bool Connect()
    {
        try
        {
            var dir = ResolveShotDataDirectory();
            if (string.IsNullOrWhiteSpace(dir))
            {
                OnError.Invoke("[Uneekor] ShotData directory not configured.");
                OnNotification.Invoke("[Other] Disconnected: ShotData path missing.");
                return false;
            }

            if (!Directory.Exists(dir))
            {
                OnError.Invoke("[Uneekor] ShotData folder not found: " + dir);
                OnNotification.Invoke("[Other] Disconnected: ShotData folder not found.");
                return false;
            }

            StopWatcher();
            _watcher = new UneekorShotDataWatcher(dir);
            _watcher.Log += msg => OnNotification.Invoke("[Uneekor] " + msg);
            _watcher.ShotDetected += OnUneekorShot;
            _watcher.Start(ignoreExisting: true);

            _connected = true;
            OtherIsReady = true;
            OtherIsArmed = true;
            OtherBallPresent = false;
            TouchActivity();

            OnNote.Invoke("[Other] BallStatus: ready=true ball=false");
            OnNotification.Invoke("[Other] Connected: Uneekor VIEW ShotData @ " + dir);
            OnModeChange.Invoke(_mode);
            OnHandedChange.Invoke(_handed);
            return true;
        }
        catch (Exception ex)
        {
            OnError.Invoke("[Uneekor] Connect failed: " + ex.Message);
            OnNotification.Invoke("[Other] Disconnected: " + ex.Message);
            return false;
        }
    }

    public bool Reconnect()
    {
        Disconnect();
        return Connect();
    }

    public bool Disconnect()
    {
        StopWatcher();
        _connected = false;
        OtherIsReady = false;
        OtherBallPresent = false;
        OtherIsArmed = false;
        TouchActivity();
        OnNote.Invoke("[Other] BallStatus: ready=false ball=false");
        OnNotification.Invoke("[Other] Disconnected: Uneekor VIEW ShotData.");
        return true;
    }

    public bool SetRightHanded()
    {
        _handed = "RH";
        SaveCurrentSettings();
        OnHandedChange.Invoke("RH");
        return true;
    }

    public bool SetLeftHanded()
    {
        _handed = "LH";
        SaveCurrentSettings();
        OnHandedChange.Invoke("LH");
        return true;
    }

    public bool SetPuttingMode()
    {
        _mode = "PUTTING";
        SaveCurrentSettings();
        OnModeChange.Invoke(_mode);
        return true;
    }

    public bool SetChippingMode()
    {
        _mode = "CHIPPING";
        SaveCurrentSettings();
        OnModeChange.Invoke(_mode);
        return true;
    }

    public bool SetNormalMode()
    {
        _mode = "NORMAL";
        SaveCurrentSettings();
        OnModeChange.Invoke(_mode);
        return true;
    }

    public bool ResetReady()
    {
        OtherBallPresent = false;
        OtherIsReady = true;
        OtherIsArmed = true;
        TouchActivity();
        OnNote.Invoke("[Other] BallStatus: ready=true ball=false");
        OnNotification.Invoke("[Uneekor] Ready reset.");
        return true;
    }

    public bool ArmOnly() => ResetReady();

    public bool SetClub(string club)
    {
        _lastClub = club;
        OnNotification.Invoke("[Uneekor] Simulator club: " + club);
        return true;
    }

    private void OnUneekorShot(ParsedUneekorShot parsed)
    {
        try
        {
            var speed = parsed.Speed * _settings.SpeedScale;
            var hla = _settings.InvertHla ? -parsed.Hla : parsed.Hla;
            var notes = new List<string> { "uneekor-shotdata:" + parsed.FolderName };
            if (!string.IsNullOrWhiteSpace(parsed.ClubName))
                notes.Add("club=" + parsed.ClubName);
            if (!string.IsNullOrWhiteSpace(_lastClub))
                notes.Add("simClub=" + _lastClub);

            // Heuristic: very low ball speed in putting mode or under ~15 mph.
            if (_mode == "PUTTING" || speed < 15m)
                notes.Add("OTHER_PUTT_LIKE=1");

            var shot = new DeviceShotData
            {
                Speed = Round(speed, 2),
                HLA = Round(hla, 2),
                VLA = Round(parsed.Vla, 2),
                BackSpin = Round(parsed.BackSpin, 1),
                SideSpin = Round(parsed.SideSpin, 1),
                SpinAxis = Round(parsed.SpinAxis, 2),
                TotalSpin = Round(parsed.TotalSpin, 1),
                CarryDistance = 0m,
                IsShotValid = true,
                Notes = notes
            };

            OtherBallPresent = true;
            TouchActivity();
            OnNote.Invoke("[Other] BallStatus: ready=true ball=true");

            // Final-only path: do not also emit OnBallData with the same payload.
            OnShotEnded(shot);

            OnRawShot(new DeviceRawShot
            {
                InsertedAt = DateTime.UtcNow,
                TotalSpeedMPH = shot.Speed,
                TotalSpin = shot.TotalSpin,
                Carry = shot.CarryDistance
            });

            OnNotification.Invoke(
                $"[Uneekor] Shot #{parsed.FolderName}: {shot.Speed} mph, VLA {shot.VLA}, HLA {shot.HLA}, spin {shot.TotalSpin}");

            // Clear ball-present shortly so UI can re-arm for next swing.
            OtherBallPresent = false;
            OtherIsReady = true;
            OtherIsArmed = true;
            OnNote.Invoke("[Other] BallStatus: ready=true ball=false");
        }
        catch (Exception ex)
        {
            OnError.Invoke("[Uneekor] Shot emit failed: " + ex.Message);
        }
    }

    private string? ResolveShotDataDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_settings.ShotDataDirectory))
            return _settings.ShotDataDirectory;
        return UneekorShotDataWatcher.DefaultShotDataDirectory();
    }

    private void StopWatcher()
    {
        try { _watcher?.Dispose(); } catch { /* ignore */ }
        _watcher = null;
    }

    private void SaveCurrentSettings()
    {
        try
        {
            _settings.Mode = _mode;
            _settings.Handedness = _handed;
            _settings.Save();
        }
        catch (Exception ex)
        {
            OnError.Invoke("[Uneekor] Settings save failed: " + ex.Message);
        }
    }

    private void TouchActivity() => LastStatusUtcTicks = DateTime.UtcNow.Ticks;

    private static decimal Round(decimal n, int places) => Math.Round(n, places);
}
