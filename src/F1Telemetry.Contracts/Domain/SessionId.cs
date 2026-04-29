namespace F1Telemetry.Contracts;

public readonly record struct SessionId(string Value)
{
    public override string ToString() => Value;
    public static SessionId From(ulong rawUid) =>
        new(rawUid.ToString("X16", System.Globalization.CultureInfo.InvariantCulture));
    public static SessionId Empty => new(string.Empty);
}
