using System.Threading.Channels;
using F1Telemetry.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace F1Telemetry.Ingestion.Services;

internal sealed partial class SessionManager(
    ChannelReader<RawPacket> channelReader,
    IPacketParser parser,
    IEventBus eventBus,
    ILogger<SessionManager> logger) : BackgroundService, ISessionManager
{
    private SessionId _sessionId = SessionId.Empty;
    private int _currentLapNum;
    private uint _sector1Ms;
    private uint _sector2Ms;

    [LoggerMessage(Level = LogLevel.Information, Message = "Session manager started")]
    private static partial void LogSessionStarted(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Session manager stopped")]
    private static partial void LogSessionStopped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Parse failed: {Error}")]
    private static partial void LogParseFailed(ILogger logger, string? error);

    [LoggerMessage(Level = LogLevel.Trace, Message = "Telemetry — speed {SpeedKmh:F0} km/h throttle {Throttle:F2} brake {Brake:F2}")]
    private static partial void LogTelemetry(ILogger logger, float speedKmh, float throttle, float brake);

    [LoggerMessage(Level = LogLevel.Information, Message = "Lap {LapNumber} completed in {LapTimeMs} ms (session {SessionId})")]
    private static partial void LogLapCompleted(ILogger logger, int lapNumber, uint lapTimeMs, SessionId sessionId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Session data — track {TrackId} type {SessionType}")]
    private static partial void LogSessionData(ILogger logger, sbyte trackId, SessionType sessionType);

    [LoggerMessage(Level = LogLevel.Information, Message = "New session detected: {SessionId}")]
    private static partial void LogNewSession(ILogger logger, SessionId sessionId);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        LogSessionStarted(logger);
        await foreach (var raw in channelReader.ReadAllAsync(ct))
        {
            var result = parser.Parse(raw.Buffer.AsSpan(0, raw.Length));
            if (!result.IsSuccess)
            {
                LogParseFailed(logger, result.Error);
                continue;
            }

            Route(result.Value!);
        }
        LogSessionStopped(logger);
    }

    private void Route(IParsedPacket packet)
    {
        switch (packet)
        {
            case PacketCarTelemetryData t: ProcessCarTelemetry(t); break;
            case PacketLapData l:          ProcessLapData(l); break;
            case PacketMotionData m:       ProcessMotionData(m); break;
            case PacketSessionData s:      ProcessSessionData(s); break;
            case PacketCarStatusData cs:   ProcessCarStatus(cs); break;
            case PacketCarDamageData cd:   ProcessCarDamage(cd); break;
        }
    }

    public void ProcessCarTelemetry(PacketCarTelemetryData data)
    {
        UpdateSession(data.Header.SessionUid);
        LogTelemetry(logger, data.SpeedKmh, data.Throttle, data.Brake);
    }

    public void ProcessLapData(PacketLapData data)
    {
        UpdateSession(data.Header.SessionUid);

        if (data.Sector1TimeMs > 0)
        {
            _sector1Ms = data.Sector1TimeMs;
        }

        if (data.Sector2TimeMs > 0)
        {
            _sector2Ms = data.Sector2TimeMs;
        }

        int newLap = data.CurrentLapNum;

        if (newLap > _currentLapNum && _currentLapNum > 0)
        {
            uint lapMs = data.LastLapTimeMs;
            uint s3Ms = lapMs > _sector1Ms + _sector2Ms
                ? lapMs - _sector1Ms - _sector2Ms
                : 0;

            var completed = new CompletedLap(
                SessionId: _sessionId,
                LapNumber: _currentLapNum,
                LapTime: TimeSpan.FromMilliseconds(lapMs),
                Sector1: TimeSpan.FromMilliseconds(_sector1Ms),
                Sector2: TimeSpan.FromMilliseconds(_sector2Ms),
                Sector3: TimeSpan.FromMilliseconds(s3Ms),
                IsValid: !data.CurrentLapInvalid,
                Frames: []);

            LogLapCompleted(logger, completed.LapNumber, lapMs, _sessionId);

            eventBus.Publish(new LapCompletedEvent(completed));

            _sector1Ms = 0;
            _sector2Ms = 0;
        }

        _currentLapNum = newLap;
    }

    public void ProcessMotionData(PacketMotionData data) =>
        UpdateSession(data.Header.SessionUid);

    public void ProcessSessionData(PacketSessionData data)
    {
        UpdateSession(data.Header.SessionUid);
        LogSessionData(logger, data.TrackId, data.SessionType);
    }

    public void ProcessCarStatus(PacketCarStatusData data) =>
        UpdateSession(data.Header.SessionUid);

    public void ProcessCarDamage(PacketCarDamageData data) =>
        UpdateSession(data.Header.SessionUid);

    private void UpdateSession(ulong rawUid)
    {
        var incoming = SessionId.From(rawUid);
        if (incoming == _sessionId)
        {
            return;
        }

        LogNewSession(logger, incoming);
        _sessionId = incoming;
        _currentLapNum = 0;
        _sector1Ms = 0;
        _sector2Ms = 0;
    }
}
