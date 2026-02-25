using API;
using API.ServiceFactory;
using API.Session;
using Newtonsoft.Json;
using PacketLibrary.Handler;
using Serilog;
using WatsonWebserver.Core;
using WebAPI.Dto;
using ServerType = API.ServerType;

namespace WebAPI.Routes;

public static class SessionRoutes
{
    public static async Task ListSessions(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        try
        {
            var shared = ServiceFactory.Load<ISharedObjects>(typeof(ISharedObjects));
            if (shared == null)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("{\"error\":\"ISharedObjects not available\"}");
                return;
            }

            var serverTypeFilter = ctx.Request.Url.Parameters["serverType"]?.Trim();
            var list = new List<SessionDto>();
            if (string.IsNullOrEmpty(serverTypeFilter) || serverTypeFilter.Equals("DownloadServer", StringComparison.OrdinalIgnoreCase))
                foreach (var s in shared.DownloadSessions)
                    list.Add(SessionToDto(s, "DownloadServer"));
            if (string.IsNullOrEmpty(serverTypeFilter) || serverTypeFilter.Equals("GatewayServer", StringComparison.OrdinalIgnoreCase))
                foreach (var s in shared.GatewaySessions)
                    list.Add(SessionToDto(s, "GatewayServer"));
            if (string.IsNullOrEmpty(serverTypeFilter) || serverTypeFilter.Equals("AgentServer", StringComparison.OrdinalIgnoreCase))
                foreach (var s in shared.AgentSessions)
                    list.Add(SessionToDto(s, "AgentServer"));

            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(list));
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    public static async Task GetSessionDetail(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        var guidStr = ctx.Request.Url.Parameters["guid"];
        if (string.IsNullOrEmpty(guidStr))
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.Send("{\"error\":\"Missing guid parameter\"}");
            return;
        }
        if (!Guid.TryParse(guidStr, out var guid))
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.Send("{\"error\":\"Invalid guid\"}");
            return;
        }
        try
        {
            var shared = ServiceFactory.Load<ISharedObjects>(typeof(ISharedObjects));
            if (shared == null)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("{\"error\":\"ISharedObjects not available\"}");
                return;
            }
            ISession? session = null;
            string serverType = "";
            foreach (var s in shared.DownloadSessions) { if (s.Guid == guid) { session = s; serverType = "DownloadServer"; break; } }
            if (session == null)
                foreach (var s in shared.GatewaySessions) { if (s.Guid == guid) { session = s; serverType = "GatewayServer"; break; } }
            if (session == null)
                foreach (var s in shared.AgentSessions) { if (s.Guid == guid) { session = s; serverType = "AgentServer"; break; } }
            if (session == null)
            {
                ctx.Response.StatusCode = 404;
                await ctx.Response.Send("{\"error\":\"Session not found\"}");
                return;
            }
            var dto = SessionToDetailDto(session, serverType);
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(dto));
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    public static async Task GetSessionData(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        var guidStr = ctx.Request.Url.Parameters["guid"];
        if (string.IsNullOrEmpty(guidStr))
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.Send("{\"error\":\"Missing guid parameter\"}");
            return;
        }
        if (!Guid.TryParse(guidStr, out var guid))
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.Send("{\"error\":\"Invalid guid\"}");
            return;
        }
        try
        {
            var shared = ServiceFactory.Load<ISharedObjects>(typeof(ISharedObjects));
            if (shared == null)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("{\"error\":\"ISharedObjects not available\"}");
                return;
            }
            ISession? session = null;
            foreach (var s in shared.DownloadSessions) { if (s.Guid == guid) { session = s; break; } }
            if (session == null)
                foreach (var s in shared.GatewaySessions) { if (s.Guid == guid) { session = s; break; } }
            if (session == null)
                foreach (var s in shared.AgentSessions) { if (s.Guid == guid) { session = s; break; } }
            if (session == null)
            {
                ctx.Response.StatusCode = 404;
                await ctx.Response.Send("{\"error\":\"Session not found\"}");
                return;
            }
            var dto = SessionToDataDto(session);
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send(JsonConvert.SerializeObject(dto));
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    public static async Task DisconnectSession(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        var guidStr = ctx.Request.Url.Parameters["guid"];
        if (string.IsNullOrEmpty(guidStr))
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.Send("{\"error\":\"Missing guid parameter\"}");
            return;
        }
        if (!Guid.TryParse(guidStr, out var guid))
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.Send("{\"error\":\"Invalid guid\"}");
            return;
        }
        try
        {
            var shared = ServiceFactory.Load<ISharedObjects>(typeof(ISharedObjects));
            if (shared == null)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("{\"error\":\"ISharedObjects not available\"}");
                return;
            }
            ISession? session = null;
            foreach (var s in shared.DownloadSessions) { if (s.Guid == guid) { session = s; break; } }
            if (session == null) foreach (var s in shared.GatewaySessions) { if (s.Guid == guid) { session = s; break; } }
            if (session == null) foreach (var s in shared.AgentSessions) { if (s.Guid == guid) { session = s; break; } }
            if (session == null)
            {
                ctx.Response.StatusCode = 404;
                await ctx.Response.Send("{\"error\":\"Session not found\"}");
                return;
            }
            await session.Disconnect();
            PacketLogStore.RemoveSession(guid);
            AuditLog.Log("Session.Disconnect", guidStr, WebApiHelpers.GetUsername(ctx));
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send("{\"status\":\"ok\"}");
        }
        catch (Exception ex)
        {
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    public static async Task GetSessionPackets(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        var guidStr = ctx.Request.Url.Parameters["guid"];
        if (string.IsNullOrEmpty(guidStr))
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.Send("{\"error\":\"Missing guid parameter\"}");
            return;
        }

        if (!Guid.TryParse(guidStr, out var guid))
        {
            ctx.Response.StatusCode = 400;
            await ctx.Response.Send("{\"error\":\"Invalid guid\"}");
            return;
        }

        var limitStr = ctx.Request.Url.Parameters["limit"];
        int? limit = null;
        if (!string.IsNullOrEmpty(limitStr) && int.TryParse(limitStr, out var l) && l > 0)
            limit = Math.Min(l, 500);

        DateTime? since = null;
        var sinceStr = ctx.Request.Url.Parameters["since"]?.Trim();
        if (!string.IsNullOrEmpty(sinceStr) && DateTime.TryParse(sinceStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var sinceParsed))
            since = sinceParsed.ToUniversalTime();

        var packets = PacketLogStore.GetPackets(guid, limit, since);
        ctx.Response.StatusCode = 200;
        await ctx.Response.Send(JsonConvert.SerializeObject(packets));
    }

    public static async Task SendPacket(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        try
        {
            var body = ctx.Request.DataAsString;
            var req = JsonConvert.DeserializeObject<SendPacketRequest>(body);
            if (req == null || string.IsNullOrWhiteSpace(req.ClientId))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send("{\"error\":\"Invalid body or missing clientId\"}");
                return;
            }

            if (!Guid.TryParse(req.ClientId, out var clientGuid))
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send("{\"error\":\"Invalid clientId (guid)\"}");
                return;
            }

            var shared = ServiceFactory.Load<ISharedObjects>(typeof(ISharedObjects));
            if (shared == null)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.Send("{\"error\":\"ISharedObjects not available\"}");
                return;
            }

            ISession? session = null;
            foreach (var s in shared.DownloadSessions)
            {
                if (s.Guid == clientGuid) { session = s; break; }
            }
            if (session == null)
            {
                foreach (var s in shared.GatewaySessions)
                {
                    if (s.Guid == clientGuid) { session = s; break; }
                }
            }
            if (session == null)
            {
                foreach (var s in shared.AgentSessions)
                {
                    if (s.Guid == clientGuid) { session = s; break; }
                }
            }

            if (session == null)
            {
                ctx.Response.StatusCode = 404;
                await ctx.Response.Send("{\"error\":\"Session not found\"}");
                return;
            }

            var packet = PacketBuilder.FromRequest(req.MsgId, req.Encrypted, req.Massive, req.Data, req.RawHex);
            packet = await packet.Build();

            if (req.Direction != null && req.Direction.Equals("Module", StringComparison.OrdinalIgnoreCase))
                await session.SendToServer(packet);
            else
                await session.SendToClient(packet);
            
            var finalBytes = packet.GetBytes();
            var payloadLen = finalBytes?.Length ?? 0;
            var hexPreview = payloadLen == 0 ? "" : PacketBuilder.ToHex(packet);
            if (hexPreview.Length > 256) hexPreview = hexPreview.Substring(0, 256) + "...";
            var detail = $"guid={req.ClientId} dir={req.Direction ?? "Client"} msgId=0x{req.MsgId:X} enc={req.Encrypted} massive={req.Massive} payloadBytes={payloadLen} hex={hexPreview}";
            Log.Debug("[SendPacket] Sent successfully guid={ClientId} msgId=0x{MsgId:X} len={Len}", req.ClientId, req.MsgId, payloadLen);
            AuditLog.Log("Packet.Send", detail, WebApiHelpers.GetUsername(ctx));
            
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send("{\"status\":\"ok\"}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[SendPacket] Error: {Message} | Inner: {Inner} | Stack: {Stack}",
                ex.Message, ex.InnerException?.Message ?? "-", ex.StackTrace ?? "");
            ctx.Response.StatusCode = 500;
            var errMsg = ex.Message + (ex.InnerException != null ? " | " + ex.InnerException.Message : "");
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = errMsg }));
        }
    }

    public static async Task BroadcastPacket(HttpContextBase ctx)
    {
        ctx.Response.ContentType = "application/json";
        if (!WebApiHelpers.TryRateLimit(ctx)) { await ctx.Response.Send("{\"error\":\"Too many requests\"}"); return; }
        if (!WebApiHelpers.RequireAdmin(ctx)) { ctx.Response.StatusCode = 403; await ctx.Response.Send("{\"error\":\"Admin required\"}"); return; }
        try
        {
            var body = ctx.Request.DataAsString;
            var req = JsonConvert.DeserializeObject<BroadcastPacketRequest>(body);
            if (req == null)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.Send("{\"error\":\"Invalid body\"}");
                return;
            }
            var serverType = ServerType.AgentServer;
            var st = req.ServerType?.Trim();
            if (!string.IsNullOrEmpty(st))
            {
                if (st.Equals("GatewayServer", StringComparison.OrdinalIgnoreCase)) serverType = ServerType.GatewayServer;
                else if (st.Equals("DownloadServer", StringComparison.OrdinalIgnoreCase)) serverType = ServerType.DownloadServer;
            }
            var packet = PacketBuilder.FromRequest(req.MsgId, req.Encrypted, req.Massive, req.Data, req.RawHex);
            packet = await packet.Build();
            await Helper.BroadcastPacket(packet, serverType);
            AuditLog.Log("Packet.Broadcast", $"msgId=0x{req.MsgId:X} serverType={serverType}", WebApiHelpers.GetUsername(ctx));
            ctx.Response.StatusCode = 200;
            await ctx.Response.Send("{\"status\":\"ok\"}");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[BroadcastPacket] Error");
            ctx.Response.StatusCode = 500;
            await ctx.Response.Send(JsonConvert.SerializeObject(new { error = ex.Message }));
        }
    }

    private static SessionDto SessionToDto(ISession s, string serverType)
    {
        return new SessionDto
        {
            Guid = s.Guid.ToString(),
            ServerType = serverType,
            RemoteEndPoint = s.RemoteEndPoint?.ToString()
        };
    }

    private static SessionDetailDto SessionToDetailDto(ISession s, string serverType)
    {
        var dto = new SessionDetailDto
        {
            Guid = s.Guid.ToString(),
            ServerType = serverType,
            RemoteEndPoint = s.RemoteEndPoint?.ToString()
        };
        s.GetData(Data.CharacterGameReady, out bool gameReady, false);
        dto.CharacterGameReady = gameReady;
        s.GetData(Data.CharInfo, out ICharInfo? charInfo, null);
        if (charInfo != null)
        {
            dto.CharName = charInfo.CharName;
            dto.CharLevel = charInfo.CurLevel;
            dto.JobName = charInfo.JobName;
            try { dto.RegionId = charInfo.CurPosition.Region.Id; } catch { /* ignore */ }
        }
        return dto;
    }

    private static SessionDataDto SessionToDataDto(ISession s)
    {
        var dto = new SessionDataDto();
        s.GetData(Data.CharacterGameReady, out bool gameReady, false);
        dto.CharacterGameReady = gameReady;
        s.GetData(Data.CharId, out int charId, -1);
        dto.CharId = charId;
        s.GetData(Data.CharNameSent, out bool charNameSent, false);
        dto.CharNameSent = charNameSent;
        s.GetData(Data.CharInfo, out ICharInfo? charInfo, null);
        if (charInfo != null)
        {
            dto.CharInfo = new SessionCharInfoDto
            {
                CharName = charInfo.CharName,
                CurLevel = charInfo.CurLevel,
                JobName = charInfo.JobName,
                JobLevel = charInfo.JobLevel,
                UniqueCharId = charInfo.UniqueCharId,
                Jid = charInfo.Jid,
                Hp = charInfo.Hp,
                Mp = charInfo.Mp
            };
            try { dto.CharInfo.RegionId = charInfo.CurPosition.Region.Id; } catch { /* ignore */ }
        }
        try
        {
            var raw = s.GetRawSessionData();
            foreach (var key in raw.Keys.OrderBy(k => k))
            {
                var entry = new SessionDataEntryDto { Key = key };
                object? val = null;
                try
                {
                    if (!raw.TryGetValue(key, out val) || val == null)
                        entry.Value = "null";
                    else if (val is ICharInfo ci)
                    {
                        var ciDto = new SessionCharInfoDto
                        {
                            CharName = ci.CharName,
                            CurLevel = ci.CurLevel,
                            JobName = ci.JobName,
                            JobLevel = ci.JobLevel,
                            UniqueCharId = ci.UniqueCharId,
                            Jid = ci.Jid,
                            Hp = ci.Hp,
                            Mp = ci.Mp
                        };
                        try { ciDto.RegionId = ci.CurPosition.Region.Id; } catch { /* ignore */ }
                        entry.Value = JsonConvert.SerializeObject(ciDto);
                    }
                    else
                        entry.Value = JsonConvert.SerializeObject(val);
                }
                catch
                {
                    entry.Value = "[" + (val?.GetType().Name ?? "null") + "]";
                }
                dto.Data.Add(entry);
            }
        }
        catch { /* ignore */ }
        return dto;
    }
}
