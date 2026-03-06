using FileConverter.Domain.Interfaces;
using System.Collections.Concurrent;

namespace FileConverter.Infrastructure.Services;

public class RateLimitService : IRateLimitService
{
    private readonly ConcurrentDictionary<string, (int Count, DateTime Date)> _counters = new();

    public bool IsAllowed(string ipAddress, int maxPerDay = 20)
    {
        if (_counters.TryGetValue(ipAddress, out var entry))
        {
            if (entry.Date.Date != DateTime.UtcNow.Date)
                return true; // New day, reset

            return entry.Count < maxPerDay;
        }
        return true;
    }

    public void RecordConversion(string ipAddress)
    {
        _counters.AddOrUpdate(ipAddress,
            _ => (1, DateTime.UtcNow),
            (_, existing) =>
            {
                if (existing.Date.Date != DateTime.UtcNow.Date)
                    return (1, DateTime.UtcNow);
                return (existing.Count + 1, existing.Date);
            });
    }

    public void ResetExpiredCounters()
    {
        var today = DateTime.UtcNow.Date;
        var expired = _counters.Where(kv => kv.Value.Date.Date < today).Select(kv => kv.Key).ToList();
        foreach (var key in expired)
            _counters.TryRemove(key, out _);
    }
}
