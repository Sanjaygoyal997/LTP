namespace CuringMonitor.Api.Domain;

/// <summary>
/// One reading from the process data source. <paramref name="IsGood"/> carries OPC quality:
/// a tag can hold a stale value and still be delivered, so quality and timestamp are kept
/// alongside the value rather than folded into it.
/// </summary>
/// <param name="Value">Raw value; null when the tag could not be read at all.</param>
/// <param name="IsGood">True when the source reported good quality.</param>
/// <param name="Timestamp">Source timestamp of the reading.</param>
public readonly record struct TagValue(object? Value, bool IsGood, DateTimeOffset Timestamp)
{
    public static TagValue Bad(DateTimeOffset at) => new(null, false, at);

    public bool TryGetBoolean(out bool result)
    {
        result = false;
        if (!IsGood || Value is null)
        {
            return false;
        }

        try
        {
            result = Convert.ToBoolean(Value, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return false;
        }
    }

    public bool TryGetDouble(out double result)
    {
        result = 0;
        if (!IsGood || Value is null)
        {
            return false;
        }

        try
        {
            result = Convert.ToDouble(Value, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return false;
        }
    }

    public bool TryGetInt32(out int result)
    {
        result = 0;
        if (!TryGetDouble(out var value))
        {
            return false;
        }

        result = (int)Math.Round(value);
        return true;
    }

    public string? AsString() => IsGood ? Value?.ToString() : null;
}
