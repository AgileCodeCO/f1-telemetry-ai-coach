namespace F1Telemetry.Contracts;

// Non-generic factory — avoids CA1000 (no static members on generic types)
public static class ParseResult
{
    public static ParseResult<T> Ok<T>(T value) => new(value, null);
    public static ParseResult<T> Fail<T>(string error) => new(default, error);
}

public record struct ParseResult<T>(T? Value, string? Error)
{
    public bool IsSuccess => Error is null;
}
