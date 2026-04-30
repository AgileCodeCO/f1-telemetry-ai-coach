using System.Globalization;
using F1Telemetry.Contracts;
using F1Telemetry.Storage.Options;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Microsoft.Extensions.Options;

namespace F1Telemetry.Storage.Repositories;

internal sealed class InfluxTelemetryRepository(IInfluxDBClient client, IOptions<InfluxDbOptions> options) : ITelemetryRepository
{
    private const string Measurement = "car_telemetry";

    public async Task WriteFrameAsync(TelemetryFrame frame, CancellationToken ct = default)
    {
        InfluxDbOptions opts = options.Value;
        IWriteApiAsync writeApi = client.GetWriteApiAsync();

        PointData point = PointData.Measurement(Measurement)
            .Tag("session_uid", frame.SessionId.Value)
            .Tag("lap_number", frame.LapNumber.ToString(CultureInfo.InvariantCulture))
            .Field("speed_kmh", frame.SpeedKmh)
            .Field("throttle", frame.Throttle)
            .Field("brake", frame.Brake)
            .Field("gear", frame.Gear)
            .Field("engine_rpm", frame.EngineRpm)
            .Field("drs", frame.Drs)
            .Field("tyre_temp_fl", frame.TyreTempFl)
            .Field("tyre_temp_fr", frame.TyreTempFr)
            .Field("tyre_temp_rl", frame.TyreTempRl)
            .Field("tyre_temp_rr", frame.TyreTempRr)
            .Timestamp((long)(frame.SessionTime * 1_000_000_000L), WritePrecision.Ns);

        await writeApi.WritePointAsync(point, opts.Bucket, opts.Org, ct);
    }

    public async Task<IReadOnlyList<TelemetryFrame>> GetLapTraceAsync(
        SessionId sessionId,
        int lapNumber,
        CancellationToken ct = default)
    {
        InfluxDbOptions opts = options.Value;
        string flux = $"""
            from(bucket: "{opts.Bucket}")
              |> range(start: -30d)
              |> filter(fn: (r) => r._measurement == "{Measurement}")
              |> filter(fn: (r) => r.session_uid == "{sessionId.Value}")
              |> filter(fn: (r) => r.lap_number == "{lapNumber.ToString(CultureInfo.InvariantCulture)}")
              |> pivot(rowKey: ["_time"], columnKey: ["_field"], valueColumn: "_value")
            """;

        List<TelemetryFrame> frames = [];
        var tables = await client.GetQueryApi().QueryAsync(flux, opts.Org, ct);

        foreach (var record in tables.SelectMany(t => t.Records))
        {
            frames.Add(new TelemetryFrame(
                SessionId: sessionId,
                LapNumber: lapNumber,
                SessionTime: (float)Convert.ToDouble(record.GetValueByKey("_time"), CultureInfo.InvariantCulture),
                SpeedKmh: (float)Convert.ToDouble(record.GetValueByKey("speed_kmh"), CultureInfo.InvariantCulture),
                Throttle: (float)Convert.ToDouble(record.GetValueByKey("throttle"), CultureInfo.InvariantCulture),
                Brake: (float)Convert.ToDouble(record.GetValueByKey("brake"), CultureInfo.InvariantCulture),
                Gear: Convert.ToInt32(record.GetValueByKey("gear"), CultureInfo.InvariantCulture),
                EngineRpm: Convert.ToInt32(record.GetValueByKey("engine_rpm"), CultureInfo.InvariantCulture),
                Drs: Convert.ToBoolean(record.GetValueByKey("drs"), CultureInfo.InvariantCulture),
                TyreTempFl: (float)Convert.ToDouble(record.GetValueByKey("tyre_temp_fl"), CultureInfo.InvariantCulture),
                TyreTempFr: (float)Convert.ToDouble(record.GetValueByKey("tyre_temp_fr"), CultureInfo.InvariantCulture),
                TyreTempRl: (float)Convert.ToDouble(record.GetValueByKey("tyre_temp_rl"), CultureInfo.InvariantCulture),
                TyreTempRr: (float)Convert.ToDouble(record.GetValueByKey("tyre_temp_rr"), CultureInfo.InvariantCulture),
                WorldPositionX: 0f,
                WorldPositionY: 0f,
                WorldPositionZ: 0f));
        }

        return frames;
    }
}
