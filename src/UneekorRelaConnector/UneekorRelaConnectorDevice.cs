using System.IO;
using System.Windows;
using relaDevicePlugin;

namespace UneekorRelaConnector;

/// <summary>
/// rēlā / PurePlay "Other" launch-monitor plugin.
/// Full swing: Uneekor VIEW ShotData. Putting: ExPutt via file drop or
/// GSPro Open Connect (piggyback on springbok's ExPutt OCR client).
/// Deploy: copy UneekorRelaConnector.dll next to rela.exe, Device Type = Other, Search.
/// </summary>
public sealed class UneekorRelaConnectorDevice : ILMDevice, IDeviceSettingsProvider
{
    private UneekorConnectorSettings _settings = new();
    private UneekorShotDataWatcher? _watcher;
    private ExputtPuttFileWatcher? _puttWatcher;
    private GsproOpenConnectServer? _openConnect;
    private string _mode = "NORMAL";
    private string _handed = "RH";
    private bool _connected;
    private string? _lastClub;

    public bool SessionConnected => _connected;
    public bool OtherIsReady { get; private set; }
    public bool OtherBallPresent { get; private set; }
    public bool OtherIsArmed { get; private set; }
    public bool UsesTelemetryFinalShotPath => true;
    public string SupportedConnectionTypes => "Direct,Network";
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

    private bool IsPuttingActive =>
        _mode == "PUTTING" || (_settings.AutoPuttingOnPutterClub && ExputtPuttParser.IsPutterClub(_lastClub));

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

    public string GetDeviceName()
        => _settings.PuttingEnabled ? "Uneekor VIEW + ExPutt" : "Uneekor VIEW (ShotData)";

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
            PublishPlayerInfo();

            if (_connected)
            {
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
            var shotDataOk = !string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir);
            if (!shotDataOk && !_settings.PuttingEnabled)
            {
                var reason = string.IsNullOrWhiteSpace(dir)
                    ? "ShotData path missing."
                    : "ShotData folder not found: " + dir;
                OnError.Invoke("[Uneekor] " + reason);
                OnNotification.Invoke("[Other] Disconnected: " + reason);
                return false;
            }

            StopSources();

            if (shotDataOk)
            {
                _watcher = new UneekorShotDataWatcher(dir!);
                _watcher.Log += msg => OnNotification.Invoke("[Uneekor] " + msg);
                _watcher.ShotDetected += OnUneekorShot;
                _watcher.Start(ignoreExisting: true);
            }
            else
            {
                OnNotification.Invoke("[Uneekor] ShotData folder not found — putting-only session.");
            }

            if (_settings.FilePuttingEnabled)
            {
                var puttDir = _settings.ResolvePuttingDirectory();
                _puttWatcher = new ExputtPuttFileWatcher(puttDir);
                _puttWatcher.Log += msg => OnNotification.Invoke("[ExPutt] " + msg);
                _puttWatcher.PuttDetected += OnExputtPutt;
                _puttWatcher.Start(ignoreExisting: true);
            }

            if (_settings.OpenConnectPuttingEnabled)
            {
                _openConnect = new GsproOpenConnectServer(_settings.OpenConnectBind, _settings.OpenConnectPort);
                _openConnect.Log += msg => OnNotification.Invoke("[ExPutt] " + msg);
                _openConnect.PuttDetected += OnExputtPutt;
                _openConnect.Start();
            }

            _connected = true;
            OtherIsReady = true;
            OtherIsArmed = true;
            OtherBallPresent = false;
            TouchActivity();

            OnNote.Invoke("[Other] BallStatus: ready=true ball=false");
            OnNotification.Invoke("[Other] Connected: " + DescribeSession(dir, shotDataOk));
            OnModeChange.Invoke(_mode);
            OnHandedChange.Invoke(_handed);
            PublishPlayerInfo();
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
        StopSources();
        _connected = false;
        OtherIsReady = false;
        OtherBallPresent = false;
        OtherIsArmed = false;
        TouchActivity();
        OnNote.Invoke("[Other] BallStatus: ready=false ball=false");
        OnNotification.Invoke("[Other] Disconnected: Uneekor VIEW / ExPutt.");
        return true;
    }

    public bool SetRightHanded()
    {
        _handed = "RH";
        SaveCurrentSettings();
        OnHandedChange.Invoke("RH");
        PublishPlayerInfo();
        return true;
    }

    public bool SetLeftHanded()
    {
        _handed = "LH";
        SaveCurrentSettings();
        OnHandedChange.Invoke("LH");
        PublishPlayerInfo();
        return true;
    }

    public bool SetPuttingMode()
    {
        EnterMode("PUTTING");
        if (string.IsNullOrWhiteSpace(_lastClub) || !ExputtPuttParser.IsPutterClub(_lastClub))
            _lastClub = "PT";
        PublishPlayerInfo();
        return true;
    }

    public bool SetChippingMode()
    {
        EnterMode("CHIPPING");
        PublishPlayerInfo();
        return true;
    }

    public bool SetNormalMode()
    {
        EnterMode("NORMAL");
        if (ExputtPuttParser.IsPutterClub(_lastClub))
            _lastClub = "DR";
        PublishPlayerInfo();
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

        if (_settings.AutoPuttingOnPutterClub)
        {
            if (ExputtPuttParser.IsPutterClub(club) && _mode != "PUTTING")
                EnterMode("PUTTING");
            else if (!ExputtPuttParser.IsPutterClub(club) && _mode == "PUTTING")
                EnterMode("NORMAL");
        }

        PublishPlayerInfo();
        return true;
    }

    private void OnUneekorShot(ParsedUneekorShot parsed)
    {
        try
        {
            if (IsPuttingActive && _settings.IgnoreUneekorWhilePutting && _settings.PuttingEnabled)
            {
                OnNotification.Invoke("[Uneekor] Ignored ShotData while putting (ExPutt is the putting source).");
                return;
            }

            var speed = parsed.Speed * _settings.SpeedScale;
            var hla = _settings.InvertHla ? -parsed.Hla : parsed.Hla;
            var notes = new List<string> { "uneekor-shotdata:" + parsed.FolderName };
            if (!string.IsNullOrWhiteSpace(parsed.ClubName))
                notes.Add("club=" + parsed.ClubName);
            if (!string.IsNullOrWhiteSpace(_lastClub))
                notes.Add("simClub=" + _lastClub);

            if (_mode == "PUTTING" || speed < 15m)
                notes.Add("OTHER_PUTT_LIKE=1");

            EmitFinalShot(
                speed: speed,
                hla: hla,
                vla: parsed.Vla,
                backSpin: parsed.BackSpin,
                sideSpin: parsed.SideSpin,
                spinAxis: parsed.SpinAxis,
                totalSpin: parsed.TotalSpin,
                path: 0m,
                faceToTarget: 0m,
                notes: notes,
                log: $"[Uneekor] Shot #{parsed.FolderName}: {Round(speed, 2)} mph, VLA {Round(parsed.Vla, 2)}, HLA {Round(hla, 2)}, spin {Round(parsed.TotalSpin, 1)}");
        }
        catch (Exception ex)
        {
            OnError.Invoke("[Uneekor] Shot emit failed: " + ex.Message);
        }
    }

    private void OnExputtPutt(ParsedPutt parsed)
    {
        try
        {
            if (!IsPuttingActive)
            {
                OnNotification.Invoke("[ExPutt] Ignored putt — not in putting mode (select PT / SetPuttingMode).");
                return;
            }

            var speed = parsed.Speed * _settings.PuttSpeedScale;
            var hla = _settings.InvertPuttHla ? -parsed.Hla : parsed.Hla;
            var notes = new List<string>
            {
                "exputt:" + parsed.Source,
                "OTHER_PUTT_LIKE=1"
            };
            if (!string.IsNullOrWhiteSpace(_lastClub))
                notes.Add("simClub=" + _lastClub);

            EmitFinalShot(
                speed: speed,
                hla: hla,
                vla: parsed.Vla,
                backSpin: 0m,
                sideSpin: 0m,
                spinAxis: 0m,
                totalSpin: 0m,
                path: parsed.Path,
                faceToTarget: parsed.FaceToTarget,
                notes: notes,
                log: $"[ExPutt] Putt: {Round(speed, 2)} mph, HLA {Round(hla, 2)}, path {Round(parsed.Path, 2)}");
        }
        catch (Exception ex)
        {
            OnError.Invoke("[ExPutt] Putt emit failed: " + ex.Message);
        }
    }

    private void EmitFinalShot(
        decimal speed,
        decimal hla,
        decimal vla,
        decimal backSpin,
        decimal sideSpin,
        decimal spinAxis,
        decimal totalSpin,
        decimal path,
        decimal faceToTarget,
        List<string> notes,
        string log)
    {
        var shot = new DeviceShotData
        {
            Speed = Round(speed, 2),
            HLA = Round(hla, 2),
            VLA = Round(vla, 2),
            BackSpin = Round(backSpin, 1),
            SideSpin = Round(sideSpin, 1),
            SpinAxis = Round(spinAxis, 2),
            TotalSpin = Round(totalSpin, 1),
            CarryDistance = 0m,
            ClubPathHorizontalDeg = Round(path, 2),
            FaceAngleDeg = Round(faceToTarget, 2),
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

        OnNotification.Invoke(log);

        OtherBallPresent = false;
        OtherIsReady = true;
        OtherIsArmed = true;
        OnNote.Invoke("[Other] BallStatus: ready=true ball=false");
    }

    private void EnterMode(string mode)
    {
        _mode = mode;
        SaveCurrentSettings();
        OnModeChange.Invoke(_mode);
        OnNotification.Invoke("[Uneekor] Mode: " + _mode + (IsPuttingActive ? " (ExPutt armed)" : " (Uneekor armed)"));
    }

    private void PublishPlayerInfo()
    {
        var club = IsPuttingActive
            ? (ExputtPuttParser.IsPutterClub(_lastClub) ? _lastClub! : "PT")
            : (string.IsNullOrWhiteSpace(_lastClub) || ExputtPuttParser.IsPutterClub(_lastClub) ? "DR" : _lastClub);
        try { _openConnect?.SetPlayer(_handed, club); } catch { /* ignore */ }
    }

    private string DescribeSession(string? dir, bool shotDataOk)
    {
        var parts = new List<string>();
        if (shotDataOk)
            parts.Add("Uneekor VIEW ShotData @ " + dir);
        if (_settings.FilePuttingEnabled)
            parts.Add("ExPutt file @ " + _settings.ResolvePuttingDirectory());
        if (_settings.OpenConnectPuttingEnabled)
            parts.Add("ExPutt Open Connect " + _settings.OpenConnectBind + ":" + _settings.OpenConnectPort);
        return parts.Count == 0 ? "no sources" : string.Join(" + ", parts);
    }

    private string? ResolveShotDataDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_settings.ShotDataDirectory))
            return _settings.ShotDataDirectory;
        return UneekorShotDataWatcher.DefaultShotDataDirectory();
    }

    private void StopSources()
    {
        try { _watcher?.Dispose(); } catch { /* ignore */ }
        _watcher = null;
        try { _puttWatcher?.Dispose(); } catch { /* ignore */ }
        _puttWatcher = null;
        try { _openConnect?.Dispose(); } catch { /* ignore */ }
        _openConnect = null;
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
