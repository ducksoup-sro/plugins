using System.Collections.Concurrent;

namespace WebAPI;

public static class RateLimiter
{
    private static readonly ConcurrentDictionary<string, RateLimitState> ByKey = new();
    private const int DefaultMaxRequests = 2000;
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(1);

    public static bool TryAcquire(string key, int maxRequests = DefaultMaxRequests, TimeSpan? window = null)
    {
        var w = window ?? DefaultWindow;
        var now = DateTime.UtcNow;
        var state = ByKey.AddOrUpdate(key, _ => new RateLimitState(now, 1), (_, s) =>
        {
            if (now - s.WindowStart > w)
                return new RateLimitState(now, 1);
            s.Count++;
            return s;
        });
        if (state.Count > maxRequests)
            return false;
        return true;
    }

    private class RateLimitState
    {
        public DateTime WindowStart;
        public int Count;

        public RateLimitState(DateTime start, int count)
        {
            WindowStart = start;
            Count = count;
        }
    }
}
