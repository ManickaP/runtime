// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace System.Net.Http;

internal class DnsCache : IDisposable
{
    /// <summary>
    /// 60 minutes, in milliseconds.
    /// </summary>
    private const int DefaultTtl = 3_600 * 1_000;
    /// <summary>
    /// 5 seconds, in milliseconds.
    /// </summary>
    private const int MinTimeout = 5_000;

    /// <summary>
    /// In milliseconds.
    /// </summary>
    private readonly int _defaultTtl;

    private readonly ConcurrentDictionary<string, DnsCacheRecord> _cache = new();

    private SemaphoreSlim _refreshSemaphore = new SemaphoreSlim(0);

    private volatile bool _disposed;

    public DnsCache()
        : this (DefaultTtl)
    { }

    public DnsCache(TimeSpan defaultTtl)
        : this (((int)defaultTtl.TotalSeconds) * 1_000)
    { }

    private DnsCache(int defaultTtl)
    {
        if (defaultTtl < MinTimeout && defaultTtl != Timeout.Infinite)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultTtl));
        }

        _defaultTtl = defaultTtl;

        RefreshRecords();
    }

    public void Add(string hostName)
        => Add(hostName, _defaultTtl);

    public void Add(string hostName, TimeSpan idleTimeout)
        => Add(hostName, ((int)idleTimeout.TotalSeconds) * 1000);

    private DnsCacheRecord Add(string hostName, int idleTimeout)
    {
        if (idleTimeout < MinTimeout && idleTimeout != Timeout.Infinite)
        {
            throw new ArgumentOutOfRangeException(nameof(idleTimeout));
        }

        long now = Environment.TickCount64;
        DnsCacheRecord record = _cache.AddOrUpdate(hostName,
            (hostName) => new DnsCacheRecord(hostName, idleTimeout, _defaultTtl, now),
            (hostName, record) =>
            {
                record.IdleTimeout = idleTimeout;
                return record;
            });

        _refreshSemaphore.Release();
        return record;
    }

    public IPAddress[] GetIPAddresses(string hostName)
    {
        if (!_cache.TryGetValue(hostName, out DnsCacheRecord? record))
        {
            record = Add(hostName, _defaultTtl);
        }
        return record.Addresses;
    }

    private async void RefreshRecords()
    {
        int refreshIn = Timeout.Infinite;

        while (!_disposed)
        {
            await _refreshSemaphore.WaitAsync(refreshIn <= MinTimeout ? MinTimeout : refreshIn).ConfigureAwait(false);
            long now = Environment.TickCount64;

            foreach (var pair in _cache)
            {
                DnsCacheRecord record = pair.Value;
                if (record.IdlesOn >= now)
                {
                    _cache.TryRemove(pair.Key, out _);
                    continue;
                }
                if (record.ExpiresOn + MinTimeout <= now)
                {
                    try
                    {
                        record.Addresses = await Dns.GetHostAddressesAsync(record.HostName).ConfigureAwait(false);
                        record.RefreshedTicks = now;
                    }
                    catch (Exception)
                    {
                        // TODO: log the exception
                        _cache.TryRemove(pair.Key, out _);
                        continue;
                    }
                }
                if (refreshIn == Timeout.Infinite || refreshIn > record.Ttl)
                {
                    refreshIn = record.Ttl;
                }
            }
        }
    }

    public void Dispose()
    {
        _disposed = true;
    }

    private class DnsCacheRecord
    {
        public string HostName { get; }

        public int IdleTimeout { get; set; }

        public IPAddress[] Addresses { get; set; }
        public int Ttl { get; set; }

        public long LastUsedTicks { get; set; }
        public long RefreshedTicks { get; set; }

        public DnsCacheRecord(string hostName, int idleTimeout, int defaultTtl, long now)
        {
            HostName = hostName;
            IdleTimeout = idleTimeout;

            Addresses = Dns.GetHostAddresses(hostName);
            Ttl = defaultTtl; // TODO: when we actually have TTL, put here the minimum from all returned IP address records

            LastUsedTicks = now;
            RefreshedTicks = now;
        }

        public long IdlesOn => IdleTimeout == Timeout.Infinite ? long.MaxValue : LastUsedTicks + IdleTimeout;
        public long ExpiresOn => Ttl == Timeout.Infinite ? long.MaxValue : RefreshedTicks + Ttl;
    }
}
