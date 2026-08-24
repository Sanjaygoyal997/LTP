using CuringMonitor.Api.Domain;

namespace CuringMonitor.Api.Services;

/// <summary>
/// Source of process values. Implementations hide whatever the plant actually runs —
/// an OPC server, the SCADA log files, or the simulator used for development.
/// </summary>
public interface IPressDataProvider
{
    /// <summary>True while the underlying source is connected and delivering values.</summary>
    bool IsConnected { get; }

    /// <summary>
    /// Reads the given tags. Implementations must return an entry for every requested tag —
    /// an unreadable tag comes back as <see cref="TagValue.Bad"/> rather than being omitted,
    /// so callers can tell "not configured" from "not answering".
    /// </summary>
    Task<IReadOnlyDictionary<string, TagValue>> ReadAsync(
        IReadOnlyList<string> tags,
        CancellationToken cancellationToken);
}
