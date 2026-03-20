using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using System.Collections.Concurrent;

namespace PaymentGateway.Benchmarks;

/// <summary>
/// Benchmarks concurrent collection performance improvements from Phase 1.
/// Measures: lock-based Dictionary vs ConcurrentDictionary overhead.
/// Compares: List + lock vs ConcurrentBag for reader management.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net80)]
[MedianColumn, Min95Column, Max95Column, StdDevColumn, AllocationColumn]
[WarmupCount(3), TargetCount(5)]
public class ConcurrentCollectionsBenchmark
{
    private ConcurrentDictionary<string, string> _concurrentDict = null!;
    private Dictionary<string, string> _lockDict = null!;
    private object _lockObj = null!;
    private ConcurrentBag<string> _concurrentBag = null!;
    private List<string> _lockList = null!;

    [GlobalSetup]
    public void Setup()
    {
        _concurrentDict = new ConcurrentDictionary<string, string>();
        _lockDict = new Dictionary<string, string>();
        _lockObj = new object();
        _concurrentBag = new ConcurrentBag<string>();
        _lockList = new List<string>();

        // Populate initial data
        for (int i = 0; i < 1000; i++)
        {
            var key = $"topic_{i:D4}";
            _concurrentDict.TryAdd(key, key);
            _lockDict[key] = key;
            _concurrentBag.Add(key);
            _lockList.Add(key);
        }
    }

    /// <summary>
    /// Baseline: Direct dictionary access (single-threaded).
    /// </summary>
    [Benchmark(Description = "Dictionary direct access")]
    public string DictionaryDirectAccess()
    {
        return _lockDict["topic_0500"];
    }

    /// <summary>
    /// Measure: ConcurrentDictionary single-threaded access.
    /// </summary>
    [Benchmark(Description = "ConcurrentDictionary access")]
    public string ConcurrentDictAccess()
    {
        _concurrentDict.TryGetValue("topic_0500", out var value);
        return value ?? "";
    }

    /// <summary>
    /// Measure: Lock-based dictionary lookup (simulating DdsPublisher.GetOrCreateWriter).
    /// </summary>
    [Benchmark(Description = "Lock-based dictionary lookup")]
    public string LockDictLookup()
    {
        lock (_lockObj)
        {
            return _lockDict["topic_0500"];
        }
    }

    /// <summary>
    /// Measure: ConcurrentDictionary GetOrAdd (atomic operation).
    /// This replaces the old pattern: lock + check + create.
    /// </summary>
    [Benchmark(Description = "ConcurrentDictionary GetOrAdd")]
    public string ConcurrentDictGetOrAdd()
    {
        return _concurrentDict.GetOrAdd("topic_new_1", k => k.Replace(".", "_").ToUpperInvariant());
    }

    /// <summary>
    /// Measure: Lock-based GetOrCreate (old pattern).
    /// </summary>
    [Benchmark(Description = "Lock-based GetOrCreate")]
    public string LockDictGetOrCreate()
    {
        lock (_lockObj)
        {
            var key = "topic_new_2";
            if (!_lockDict.ContainsKey(key))
            {
                _lockDict[key] = key.Replace(".", "_").ToUpperInvariant();
            }
            return _lockDict[key];
        }
    }

    /// <summary>
    /// Measure: Concurrent adds to ConcurrentDictionary.
    /// </summary>
    [Benchmark(Description = "ConcurrentDictionary concurrent adds")]
    public void ConcurrentDictConcurrentAdds()
    {
        var tasks = new Task[10];
        for (int t = 0; t < 10; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    var key = $"topic_t{threadId}_i{i}";
                    _concurrentDict.TryAdd(key, key);
                }
            });
        }
        Task.WaitAll(tasks);
    }

    /// <summary>
    /// Measure: Lock-based concurrent adds (old pattern).
    /// </summary>
    [Benchmark(Description = "Lock-based concurrent adds")]
    public void LockDictConcurrentAdds()
    {
        var tasks = new Task[10];
        for (int t = 0; t < 10; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    lock (_lockObj)
                    {
                        var key = $"topic_t{threadId}_i{i}";
                        _lockDict[key] = key;
                    }
                }
            });
        }
        Task.WaitAll(tasks);
    }

    /// <summary>
    /// Measure: ConcurrentBag add (for reader token management).
    /// </summary>
    [Benchmark(Description = "ConcurrentBag add")]
    public void ConcurrentBagAdd()
    {
        for (int i = 0; i < 100; i++)
        {
            _concurrentBag.Add($"reader_token_{i}");
        }
    }

    /// <summary>
    /// Measure: Lock-based list add (old pattern for reader management).
    /// </summary>
    [Benchmark(Description = "Lock-based list add")]
    public void LockListAdd()
    {
        lock (_lockObj)
        {
            for (int i = 0; i < 100; i++)
            {
                _lockList.Add($"reader_token_{i}");
            }
        }
    }

    /// <summary>
    /// Measure: ConcurrentBag enumeration.
    /// </summary>
    [Benchmark(Description = "ConcurrentBag enumerate")]
    public int ConcurrentBagEnumerate()
    {
        int count = 0;
        foreach (var item in _concurrentBag)
        {
            count++;
        }
        return count;
    }

    /// <summary>
    /// Measure: Lock-based list enumeration.
    /// </summary>
    [Benchmark(Description = "Lock-based list enumerate")]
    public int LockListEnumerate()
    {
        lock (_lockObj)
        {
            return _lockList.Count;
        }
    }

    /// <summary>
    /// Measure: Topic name cache lookup pattern.
    /// Simulates the TopicNameCache optimization for normalizing topic names.
    /// </summary>
    [Benchmark(Description = "Topic name cache pattern")]
    public string TopicNameCacheLookup()
    {
        var cache = new ConcurrentDictionary<string, string>();
        var topics = new[] { "payment.create", "payment.approved", "fraud.check" };

        var results = "";
        foreach (var topic in topics)
        {
            results = cache.GetOrAdd(topic, t => NormalizeTopic(t));
        }
        return results;
    }

    /// <summary>
    /// Measure: Lock-based topic name normalization (old pattern).
    /// </summary>
    [Benchmark(Description = "Lock-based topic normalization")]
    public string LockBasedTopicNormalization()
    {
        var cache = new Dictionary<string, string>();
        var lockObj = new object();
        var topics = new[] { "payment.create", "payment.approved", "fraud.check" };

        var results = "";
        foreach (var topic in topics)
        {
            lock (lockObj)
            {
                if (!cache.ContainsKey(topic))
                {
                    cache[topic] = NormalizeTopic(topic);
                }
                results = cache[topic];
            }
        }
        return results;
    }

    private static string NormalizeTopic(string topic)
    {
        return topic.Replace(".", "_").Replace("-", "_").ToUpperInvariant();
    }

    /// <summary>
    /// Measure: High-contention scenario (many threads accessing same key).
    /// </summary>
    [Benchmark(Description = "ConcurrentDictionary high contention")]
    public void ConcurrentDictHighContention()
    {
        var tasks = new Task[20];
        for (int t = 0; t < 20; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    _concurrentDict.TryGetValue("topic_0500", out _);
                }
            });
        }
        Task.WaitAll(tasks);
    }

    /// <summary>
    /// Measure: Lock-based high contention (many threads accessing same key).
    /// </summary>
    [Benchmark(Description = "Lock-based high contention")]
    public void LockDictHighContention()
    {
        var tasks = new Task[20];
        for (int t = 0; t < 20; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    lock (_lockObj)
                    {
                        _ = _lockDict["topic_0500"];
                    }
                }
            });
        }
        Task.WaitAll(tasks);
    }
}
