using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace AccardND.LoadTest;

/// <summary>
/// Raccolta delle latenze per operazione. Tutti i campioni restano in memoria: a
/// qualche centinaio di bot per qualche minuto sono poche centinaia di migliaia di
/// double, e i percentili esatti valgono piu' della memoria risparmiata.
/// </summary>
public sealed class Metrics
{
    private readonly ConcurrentDictionary<string, OpStat> operations = new();
    private long connectionsOpen;
    private long connectionsFailed;
    private long connectionsDropped;
    private long botErrors;
    private readonly ConcurrentDictionary<string, int> botErrorKinds = new();

    public long ConnectionsOpen => Interlocked.Read(ref connectionsOpen);
    public long ConnectionsFailed => Interlocked.Read(ref connectionsFailed);
    public long ConnectionsDropped => Interlocked.Read(ref connectionsDropped);

    public void ConnectionOpened() => Interlocked.Increment(ref connectionsOpen);
    public void ConnectionClosed() => Interlocked.Decrement(ref connectionsOpen);
    public void ConnectionFailed() => Interlocked.Increment(ref connectionsFailed);
    public void ConnectionDropped() => Interlocked.Increment(ref connectionsDropped);

    public void BotError(string kind)
    {
        Interlocked.Increment(ref botErrors);
        botErrorKinds.AddOrUpdate(kind, 1, (_, count) => count + 1);
    }

    public void Record(string operation, double milliseconds, string errorCode = null) =>
        operations.GetOrAdd(operation, _ => new OpStat()).Add(milliseconds, errorCode);

    public Snapshot Take()
    {
        var rows = new List<OpSnapshot>();
        foreach (KeyValuePair<string, OpStat> entry in operations)
            rows.Add(entry.Value.Snapshot(entry.Key));
        rows.Sort((left, right) => right.Count.CompareTo(left.Count));
        return new Snapshot(
            rows,
            ConnectionsOpen,
            ConnectionsFailed,
            ConnectionsDropped,
            Interlocked.Read(ref botErrors),
            new Dictionary<string, int>(botErrorKinds));
    }

    private sealed class OpStat
    {
        private readonly object gate = new();
        private readonly List<double> samples = new();
        private readonly Dictionary<string, int> errorCodes = new();
        private long errors;

        public void Add(double milliseconds, string errorCode)
        {
            lock (gate)
            {
                samples.Add(milliseconds);
                if (errorCode == null)
                    return;
                errors++;
                errorCodes.TryGetValue(errorCode, out int count);
                errorCodes[errorCode] = count + 1;
            }
        }

        public OpSnapshot Snapshot(string name)
        {
            double[] copy;
            long errorCount;
            Dictionary<string, int> codes;
            lock (gate)
            {
                copy = samples.ToArray();
                errorCount = errors;
                codes = new Dictionary<string, int>(errorCodes);
            }

            Array.Sort(copy);
            return new OpSnapshot(
                name,
                copy.Length,
                errorCount,
                Percentile(copy, 0.50),
                Percentile(copy, 0.90),
                Percentile(copy, 0.95),
                Percentile(copy, 0.99),
                copy.Length > 0 ? copy[^1] : 0,
                codes);
        }

        private static double Percentile(double[] sorted, double fraction)
        {
            if (sorted.Length == 0)
                return 0;
            int index = (int)Math.Ceiling(fraction * sorted.Length) - 1;
            return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
        }
    }

    public sealed record OpSnapshot(
        string Name,
        int Count,
        long Errors,
        double P50,
        double P90,
        double P95,
        double P99,
        double Max,
        Dictionary<string, int> ErrorCodes);

    public sealed record Snapshot(
        List<OpSnapshot> Operations,
        long ConnectionsOpen,
        long ConnectionsFailed,
        long ConnectionsDropped,
        long BotErrors,
        Dictionary<string, int> BotErrorKinds)
    {
        public long TotalRequests
        {
            get
            {
                long total = 0;
                foreach (OpSnapshot row in Operations)
                    total += row.Count;
                return total;
            }
        }

        public long TotalErrors
        {
            get
            {
                long total = 0;
                foreach (OpSnapshot row in Operations)
                    total += row.Errors;
                return total;
            }
        }

        /// <summary>p95 di tutte le operazioni messe insieme, pesato sul numero di campioni.</summary>
        public double WeightedP95
        {
            get
            {
                long count = 0;
                double sum = 0;
                foreach (OpSnapshot row in Operations)
                {
                    count += row.Count;
                    sum += row.P95 * row.Count;
                }
                return count == 0 ? 0 : sum / count;
            }
        }

        public string ToTable()
        {
            var text = new StringBuilder();
            text.AppendLine(
                $"{"operazione",-34}{"n",8}{"err",7}{"p50",9}{"p90",9}{"p95",9}{"p99",9}{"max",10}");
            text.AppendLine(new string('-', 95));
            foreach (OpSnapshot row in Operations)
            {
                text.AppendLine(
                    $"{Trim(row.Name, 33),-34}{row.Count,8}{row.Errors,7}" +
                    $"{row.P50,9:F0}{row.P90,9:F0}{row.P95,9:F0}{row.P99,9:F0}{row.Max,10:F0}");
            }

            var codes = new Dictionary<string, int>();
            foreach (OpSnapshot row in Operations)
            {
                foreach (KeyValuePair<string, int> entry in row.ErrorCodes)
                {
                    codes.TryGetValue(entry.Key, out int count);
                    codes[entry.Key] = count + entry.Value;
                }
            }

            if (codes.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("Errori applicativi per codice:");
                foreach (KeyValuePair<string, int> entry in codes.OrderByDescending(entry => entry.Value))
                    text.AppendLine($"  {entry.Key,-40}{entry.Value}");
            }

            if (BotErrorKinds.Count > 0)
            {
                text.AppendLine();
                text.AppendLine("Errori di trasporto/bot:");
                foreach (KeyValuePair<string, int> entry in BotErrorKinds.OrderByDescending(entry => entry.Value))
                    text.AppendLine($"  {entry.Key,-40}{entry.Value}");
            }

            return text.ToString();
        }

        public string ToJson(object header) => JsonSerializer.Serialize(new
        {
            run = header,
            connectionsOpen = ConnectionsOpen,
            connectionsFailed = ConnectionsFailed,
            connectionsDropped = ConnectionsDropped,
            totalRequests = TotalRequests,
            totalErrors = TotalErrors,
            weightedP95Ms = WeightedP95,
            botErrors = BotErrors,
            botErrorKinds = BotErrorKinds,
            operations = Operations
        }, new JsonSerializerOptions { WriteIndented = true });

        private static string Trim(string value, int length) =>
            value.Length <= length ? value : value[..length];
    }
}
