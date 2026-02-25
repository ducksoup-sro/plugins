using System.Text;
using SilkroadSecurityAPI.Message;

namespace WebAPI;

/// <summary>
/// Builds a Packet from API request data format: "type:;:value;:;type2:;:value2"
/// Separator between fields: ;:;
/// Separator between type and value: :;:
/// Array types: value is comma-separated, e.g. "uint8array:;:1,2,3"
/// </summary>
public static class PacketBuilder
{
    public static Packet FromRequest(ushort msgId, bool encrypted, bool massive, string? data, string? rawHex = null)
    {
        if (!string.IsNullOrWhiteSpace(rawHex))
            return FromRawHex(msgId, encrypted, massive, rawHex!);
        var packet = new Packet(msgId, encrypted, massive);
        if (string.IsNullOrWhiteSpace(data))
            return packet;
        ApplyData(packet, data!);
        return packet;
    }

    public static Packet FromRawHex(ushort msgId, bool encrypted, bool massive, string hex)
    {
        hex = hex.Replace(" ", "").Replace("-", "").Trim();
        if (hex.Length % 2 != 0) hex = "0" + hex;
        var bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        return new Packet(msgId, encrypted, massive, bytes, 0, bytes.Length);
    }

    /// <summary>Type-value separator: ":;:" (colon-semicolon-colon). Alternative for single field: ";:;" also accepted.</summary>
    private const string TypeValueSep = ":;:";
    private const string FieldSep = ";:;";

    private static void ApplyData(Packet packet, string data)
    {
        var parts = data.Split(new[] { FieldSep }, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var sep = part.IndexOf(TypeValueSep, StringComparison.Ordinal);
            if (sep < 0)
                sep = part.IndexOf(FieldSep, StringComparison.Ordinal);
            string type;
            string value;
            if (sep >= 0)
            {
                type = part.Substring(0, sep).Trim().ToLowerInvariant();
                value = part.Substring(sep + 3).Trim();
            }
            else if (i + 1 < parts.Length)
            {
                type = part.Trim().ToLowerInvariant();
                value = parts[i + 1].Trim();
                i++;
            }
            else
                continue;

            switch (type)
            {
                case "uint8":
                    packet.TryWrite(Convert.ToByte(value));
                    break;
                case "int8":
                    packet.TryWrite(Convert.ToSByte(value));
                    break;
                case "uint16":
                    packet.TryWrite(Convert.ToUInt16(value));
                    break;
                case "int16":
                    packet.TryWrite(Convert.ToInt16(value));
                    break;
                case "uint32":
                    packet.TryWrite(Convert.ToUInt32(value));
                    break;
                case "int32":
                    packet.TryWrite(Convert.ToInt32(value));
                    break;
                case "uint64":
                    packet.TryWrite(Convert.ToUInt64(value));
                    break;
                case "int64":
                    packet.TryWrite(Convert.ToInt64(value));
                    break;
                case "single":
                case "float":
                    packet.TryWrite(Convert.ToSingle(value));
                    break;
                case "double":
                    packet.TryWrite(Convert.ToDouble(value));
                    break;
                case "bool":
                    packet.TryWrite(Convert.ToBoolean(value));
                    break;
                case "byte":
                    packet.TryWrite(Convert.ToByte(value));
                    break;
                case "ascii":
                    packet.TryWriteString(value);
                    break;
                case "unicode":
                    packet.TryWriteUnicode(value);
                    break;
                case "uint8array":
                    foreach (var v in value.Split(',').Select(x => Convert.ToByte(x.Trim()))) packet.TryWrite(v);
                    break;
                case "int8array":
                    foreach (var v in value.Split(',').Select(x => Convert.ToSByte(x.Trim()))) packet.TryWrite(v);
                    break;
                case "uint16array":
                    foreach (var v in value.Split(',').Select(x => Convert.ToUInt16(x.Trim()))) packet.TryWrite(v);
                    break;
                case "int16array":
                    foreach (var v in value.Split(',').Select(x => Convert.ToInt16(x.Trim()))) packet.TryWrite(v);
                    break;
                case "uint32array":
                    foreach (var v in value.Split(',').Select(x => Convert.ToUInt32(x.Trim()))) packet.TryWrite(v);
                    break;
                case "int32array":
                    foreach (var v in value.Split(',').Select(x => Convert.ToInt32(x.Trim()))) packet.TryWrite(v);
                    break;
                case "uint64array":
                    foreach (var v in value.Split(',').Select(x => Convert.ToUInt64(x.Trim()))) packet.TryWrite(v);
                    break;
                case "int64array":
                    foreach (var v in value.Split(',').Select(x => Convert.ToInt64(x.Trim()))) packet.TryWrite(v);
                    break;
                case "singlearray":
                case "floatarray":
                    foreach (var v in value.Split(',').Select(x => Convert.ToSingle(x.Trim()))) packet.TryWrite(v);
                    break;
                case "doublearray":
                    foreach (var v in value.Split(',').Select(x => Convert.ToDouble(x.Trim()))) packet.TryWrite(v);
                    break;
                case "asciiarray":
                    foreach (var v in value.Split(',').Select(x => x.Trim())) packet.TryWriteString(v);
                    break;
                case "unicodearray":
                    foreach (var v in value.Split(',').Select(x => x.Trim())) packet.TryWriteUnicode(v);
                    break;
                default:
                    break;
            }
        }
    }

    public static string ToHex(Packet packet)
    {
        var bytes = packet.GetBytes();
        if (bytes == null || bytes.Length == 0) return "";
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            sb.Append(b.ToString("X2"));
        return sb.ToString();
    }
}
