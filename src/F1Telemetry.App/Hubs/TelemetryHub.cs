using F1Telemetry.App.Services;
using Microsoft.AspNetCore.SignalR;

namespace F1Telemetry.App.Hubs;

public sealed class TelemetryHub(TelemetryState telemetryState) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var snapshot = telemetryState.Frames;
        if (snapshot.Count > 0)
        {
            await Clients.Caller.SendAsync("TelemetrySnapshot", snapshot);
        }

        await base.OnConnectedAsync();
    }
}
