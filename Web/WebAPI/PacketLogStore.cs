using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using API.EventFactory;
using PacketLibrary.Handler;
using SilkroadSecurityAPI.Message;
using WebApiPlugin.Dto;

namespace WebApiPlugin;

/// <summary>
/// Stores the last N packets per session for the "read packets" API.
/// Only subscribes to OnClientReceivePacket (C→P→S, client sent to server) and OnModuleReceivePacket (S→P→C, server sent to client).
/// </summary>
public static class PacketLogStore
{
    /// <summary>Max packets kept per session. Default 200, can be set at startup.</summary>
    public static int MaxPacketsPerSession { get; set; } = 200;
    private static long _nextId;
    private static readonly ConcurrentDictionary<Guid, Queue<PacketLogEntryDto>> BySession = new();

    private static void Enqueue(Guid sessionGuid, string direction, Packet packet)
    {
        var queue = BySession.GetOrAdd(sessionGuid, _ => new Queue<PacketLogEntryDto>());
        var id = System.Threading.Interlocked.Increment(ref _nextId);
        var entry = new PacketLogEntryDto
        {
            Id = id,
            Direction = direction,
            MsgId = packet.MsgId,
            Encrypted = packet.Encrypted,
            Massive = packet.Massive,
            Hex = PacketBuilder.ToHex(packet),
            Timestamp = DateTime.UtcNow.ToString("O")
        };
        lock (queue)
        {
            queue.Enqueue(entry);
            while (queue.Count > MaxPacketsPerSession)
                queue.Dequeue();
        }
    }

    public static void Subscribe()
    {
        // C→P→S: client sent something to server
        EventFactory.Subscribe(EventFactoryNames.OnClientReceivePacket, (DateTime _, object serverType, object session, object packet) =>
        {
            if (session is ISession s && packet is Packet p)
                Enqueue(s.Guid, "ClientToServer", p);
        });
        // S→P→C: server sent something to client
        EventFactory.Subscribe(EventFactoryNames.OnModuleReceivePacket, (DateTime _, object serverType, object session, object packet) =>
        {
            if (session is ISession s && packet is Packet p)
                Enqueue(s.Guid, "ServerToClient", p);
        });
    }

    /// <param name="since">Optional ISO8601 timestamp; only packets with Timestamp > since are returned.</param>
    public static List<PacketLogEntryDto> GetPackets(Guid sessionGuid, int? limit = null, DateTime? since = null)
    {
        if (!BySession.TryGetValue(sessionGuid, out var queue))
            return new List<PacketLogEntryDto>();
        lock (queue)
        {
            var list = queue.AsEnumerable();
            if (since.HasValue)
            {
                var sinceStr = since.Value.ToUniversalTime().ToString("O");
                list = list.Where(e => string.CompareOrdinal(e.Timestamp, sinceStr) > 0);
            }
            var asList = list.ToList();
            if (limit.HasValue && asList.Count > limit.Value)
                asList = asList.Skip(asList.Count - limit.Value).ToList();
            asList.Reverse(); // newest first
            return asList;
        }
    }

    public static void RemoveSession(Guid sessionGuid) => BySession.TryRemove(sessionGuid, out _);
}
