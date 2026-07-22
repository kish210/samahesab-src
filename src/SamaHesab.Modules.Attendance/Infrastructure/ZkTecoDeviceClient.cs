using System.Net.Sockets;
using System.Text;

namespace SamaHesab.Modules.Attendance.Infrastructure;

/// <summary>یک ضربهٔ خامِ خوانده‌شده از حافظهٔ دستگاه (کدِ کارمندِ ثبت‌شده رویِ دستگاه + زمان).</summary>
public sealed record ZkPunch(string EmployeeCode, DateTime Timestamp);

/// <summary>
/// U-ATT-ZK — کلاینتِ پروتکلِ TCP/IPِ دستگاه‌هایِ زدکتکو (خانوادهٔ ZKSoftware/ZKTeco، پورتِ ۴۳۷۰).
/// پروتکل رسمی/عمومی نیست؛ پیاده‌سازی بر اساسِ مهندسیِ معکوسِ کتابخانهٔ متن‌بازِ
/// <c>fananimi/pyzk</c> (https://github.com/fananimi/pyzk، فایل‌هایِ zk/base.py و zk/const.py)
/// انجام شده — همان الگویی که کتابخانه‌هایِ منبع‌بازِ رایج (python/php/js) برایِ این خانواده استفاده می‌کنند.
/// ⚠️ محدودیتِ صادقانه: بدونِ دستگاهِ فیزیکی تستِ زنده نشده؛ اگر مدلِ خاصی پاسخِ متفاوت داد،
/// لاگِ خطا را با کدِ پاسخِ دستگاه بررسی کنید.
/// </summary>
public sealed class ZkTecoDeviceClient : IDisposable
{
    private const ushort CmdConnect = 1000;
    private const ushort CmdExit = 1001;
    private const ushort CmdAuth = 1102;
    private const ushort CmdAckOk = 2000;
    private const ushort CmdAckUnauth = 2005;
    private const ushort CmdPrepareData = 1500;
    private const ushort CmdData = 1501;
    private const ushort CmdFreeData = 1502;
    private const ushort CmdAttLogRrq = 13;
    private const ushort CmdPrepareBuffer = 1503;
    private const ushort CmdReadBuffer = 1504;
    private const int UshrtMax = 65535;
    private const ushort TcpMagic1 = 0x5050;
    private const ushort TcpMagic2 = 0x7d82;
    private const int MaxChunk = 0xffc0;

    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private ushort _sessionId;
    private ushort _replyId;

    public bool IsConnected { get; private set; }

    public void Connect(string host, int port = 4370, string? password = null, int timeoutMs = 5000)
    {
        _tcp = new TcpClient();
        if (!_tcp.ConnectAsync(host, port).Wait(timeoutMs))
            throw new TimeoutException($"اتصال به دستگاهِ {host}:{port} با تایم‌اوت مواجه شد.");
        _tcp.ReceiveTimeout = timeoutMs;
        _tcp.SendTimeout = timeoutMs;
        _stream = _tcp.GetStream();
        _sessionId = 0;
        _replyId = (ushort)(UshrtMax - 1);

        var resp = SendRaw(CmdConnect, Array.Empty<byte>());
        _sessionId = resp.SessionId;
        if (resp.Command == CmdAckUnauth)
        {
            var key = MakeCommKey(ParsePassword(password), _sessionId);
            resp = SendRaw(CmdAuth, key);
        }
        if (resp.Command != CmdAckOk)
            throw new InvalidOperationException($"اتصال به دستگاه رد شد (کدِ پاسخ {resp.Command}). رمزِ ارتباطیِ دستگاه (CommKey) را بررسی کنید.");
        IsConnected = true;
    }

    /// <summary>خواندنِ کاملِ ترددِ خامِ ذخیره‌شده در حافظهٔ دستگاه (بدونِ پاک‌کردنِ حافظه).</summary>
    public List<ZkPunch> GetAttendanceLogs()
    {
        if (!IsConnected) throw new InvalidOperationException("ابتدا باید به دستگاه متصل شوید.");

        var prepare = BuildPrepareBufferPayload(CmdAttLogRrq);
        var resp = SendRaw(CmdPrepareBuffer, prepare);

        byte[] data;
        if (resp.Command == CmdData)
        {
            data = resp.Payload;
        }
        else if (resp.Command == CmdPrepareData)
        {
            if (resp.Payload.Length < 5)
                throw new InvalidOperationException("پاسخِ نامعتبرِ آماده‌سازیِ بافر از دستگاه.");
            uint size = BitConverter.ToUInt32(resp.Payload, 1);
            var buffer = new List<byte>((int)size);
            int start = 0;
            while (start < size)
            {
                int chunkSize = (int)Math.Min(MaxChunk, size - start);
                var readPayload = BuildReadBufferPayload(start, chunkSize);
                var chunkResp = SendRaw(CmdReadBuffer, readPayload);
                if (chunkResp.Command != CmdData)
                    throw new InvalidOperationException($"پاسخِ نامعتبر هنگامِ خواندنِ بخشِ داده از دستگاه (کد {chunkResp.Command}).");
                buffer.AddRange(chunkResp.Payload);
                start += chunkSize;
            }
            try { SendRaw(CmdFreeData, Array.Empty<byte>()); } catch { /* پاک‌سازیِ بافرِ دستگاه بهترین‌تلاش است */ }
            data = buffer.ToArray();
        }
        else
        {
            throw new InvalidOperationException($"دستگاه پاسخِ نامعتبر داد (کدِ {resp.Command}).");
        }

        return ParseAttendanceRecords(data);
    }

    public void Disconnect()
    {
        try { if (IsConnected) SendRaw(CmdExit, Array.Empty<byte>()); } catch { /* اتصال ممکن است قبلاً قطع شده باشد */ }
        _stream?.Dispose();
        _tcp?.Dispose();
        IsConnected = false;
    }

    public void Dispose() => Disconnect();

    // ── فریم‌بندیِ پروتکل (TCP top: ۲ کلمهٔ جادویی + طول، سپس هدرِ ۸بایتیِ ZK: دستور/چک‌سام/سشن/ریپلای) ──

    private readonly record struct RawResponse(ushort Command, ushort SessionId, ushort ReplyId, byte[] Payload);

    private RawResponse SendRaw(ushort command, byte[] commandData)
    {
        var packet = CreateHeader(command, commandData, _sessionId, _replyId);
        var top = CreateTcpTop(packet);
        _stream!.Write(top, 0, top.Length);
        _stream.Flush();

        var topHeader = ReadExact(8);
        ushort magic1 = BitConverter.ToUInt16(topHeader, 0);
        ushort magic2 = BitConverter.ToUInt16(topHeader, 2);
        uint length = BitConverter.ToUInt32(topHeader, 4);
        if (magic1 != TcpMagic1 || magic2 != TcpMagic2 || length < 8)
            throw new InvalidOperationException("پاسخِ نامعتبر از دستگاه (فریمِ TCP نامعتبر).");

        var zkPacket = ReadExact((int)length);
        ushort respCommand = BitConverter.ToUInt16(zkPacket, 0);
        ushort respSession = BitConverter.ToUInt16(zkPacket, 4);
        ushort respReply = BitConverter.ToUInt16(zkPacket, 6);
        var payload = zkPacket[8..];

        _replyId = respReply;
        return new RawResponse(respCommand, respSession, respReply, payload);
    }

    private byte[] ReadExact(int count)
    {
        var buf = new byte[count];
        int offset = 0;
        while (offset < count)
        {
            int read = _stream!.Read(buf, offset, count - offset);
            if (read == 0) throw new IOException("ارتباط با دستگاه به‌طورِ غیرمنتظره قطع شد.");
            offset += read;
        }
        return buf;
    }

    private static byte[] CreateTcpTop(byte[] packet)
    {
        var top = new byte[8 + packet.Length];
        BitConverter.GetBytes(TcpMagic1).CopyTo(top, 0);
        BitConverter.GetBytes(TcpMagic2).CopyTo(top, 2);
        BitConverter.GetBytes((uint)packet.Length).CopyTo(top, 4);
        packet.CopyTo(top, 8);
        return top;
    }

    /// <summary>
    /// طبقِ pyzk: چک‌سام رویِ هدرِ حاویِ reply_id قدیم محاسبه می‌شود، سپس reply_id در بستهٔ نهایی
    /// افزایش می‌یابد (بدونِ تغییرِ چک‌سام) — همین رفتار عیناً پیاده‌سازی شده تا با دستگاهِ واقعی سازگار بماند.
    /// </summary>
    private static byte[] CreateHeader(ushort command, byte[] commandData, ushort sessionId, ushort replyId)
    {
        var buf = new byte[8 + commandData.Length];
        BitConverter.GetBytes(command).CopyTo(buf, 0);
        BitConverter.GetBytes(sessionId).CopyTo(buf, 4);
        BitConverter.GetBytes(replyId).CopyTo(buf, 6);
        commandData.CopyTo(buf, 8);

        ushort checksum = ComputeChecksum(buf);
        ushort nextReplyId = (ushort)((replyId + 1) >= UshrtMax ? (replyId + 1 - UshrtMax) : (replyId + 1));

        var final = new byte[buf.Length];
        BitConverter.GetBytes(command).CopyTo(final, 0);
        BitConverter.GetBytes(checksum).CopyTo(final, 2);
        BitConverter.GetBytes(sessionId).CopyTo(final, 4);
        BitConverter.GetBytes(nextReplyId).CopyTo(final, 6);
        commandData.CopyTo(final, 8);
        return final;
    }

    /// <summary>الگوریتمِ چک‌سامِ zkemsdk.c (کپی‌شده در pyzk): جمعِ کلمه‌هایِ ۱۶بیتی + یکمکمل.</summary>
    private static ushort ComputeChecksum(byte[] p)
    {
        int checksum = 0;
        int i = 0;
        int remaining = p.Length;
        while (remaining > 1)
        {
            checksum += p[i] | (p[i + 1] << 8);
            if (checksum > UshrtMax) checksum -= UshrtMax;
            i += 2;
            remaining -= 2;
        }
        if (remaining == 1) checksum += p[^1];
        while (checksum > UshrtMax) checksum -= UshrtMax;
        checksum = ~checksum;
        while (checksum < 0) checksum += UshrtMax;
        return (ushort)checksum;
    }

    /// <summary>طبقِ commpro.c MakeKey (پورت‌شده از pyzk.make_commkey).</summary>
    private static byte[] MakeCommKey(int key, ushort sessionId, byte ticks = 50)
    {
        uint k = 0;
        for (int i = 0; i < 32; i++)
            k = (key & (1 << i)) != 0 ? (k << 1) | 1u : k << 1;
        k += sessionId;

        var bytes = BitConverter.GetBytes(k);
        bytes[0] ^= (byte)'Z';
        bytes[1] ^= (byte)'K';
        bytes[2] ^= (byte)'S';
        bytes[3] ^= (byte)'O';

        ushort h0 = BitConverter.ToUInt16(bytes, 0);
        ushort h1 = BitConverter.ToUInt16(bytes, 2);
        var swapped = new byte[4];
        BitConverter.GetBytes(h1).CopyTo(swapped, 0);
        BitConverter.GetBytes(h0).CopyTo(swapped, 2);

        swapped[0] ^= ticks;
        swapped[1] ^= ticks;
        swapped[2] = ticks;
        swapped[3] ^= ticks;
        return swapped;
    }

    private static int ParsePassword(string? password) => int.TryParse(password, out var p) ? p : 0;

    private static byte[] BuildPrepareBufferPayload(ushort command, int fct = 0, int ext = 0)
    {
        var buf = new byte[11];
        buf[0] = 1;
        BitConverter.GetBytes((short)command).CopyTo(buf, 1);
        BitConverter.GetBytes(fct).CopyTo(buf, 3);
        BitConverter.GetBytes(ext).CopyTo(buf, 7);
        return buf;
    }

    private static byte[] BuildReadBufferPayload(int start, int size)
    {
        var buf = new byte[8];
        BitConverter.GetBytes(start).CopyTo(buf, 0);
        BitConverter.GetBytes(size).CopyTo(buf, 4);
        return buf;
    }

    // ── تجزیهٔ رکوردهایِ تردد (سه فرمتِ رایجِ سخت‌افزار: ۴۰/۱۶/۸ بایت) ──

    private static List<ZkPunch> ParseAttendanceRecords(byte[] data)
    {
        var list = new List<ZkPunch>();
        if (data.Length == 0) return list;

        int recordSize = data.Length % 40 == 0 ? 40 : data.Length % 16 == 0 ? 16 : 8;
        for (int offset = 0; offset + recordSize <= data.Length; offset += recordSize)
        {
            string code;
            uint packedTime;
            if (recordSize == 40)
            {
                ushort uid = BitConverter.ToUInt16(data, offset);
                var userId = TrimNulls(Encoding.ASCII.GetString(data, offset + 2, 24));
                packedTime = BitConverter.ToUInt32(data, offset + 27);
                code = string.IsNullOrWhiteSpace(userId) ? uid.ToString() : userId;
            }
            else if (recordSize == 16)
            {
                uint userId = BitConverter.ToUInt32(data, offset);
                packedTime = BitConverter.ToUInt32(data, offset + 4);
                code = userId.ToString();
            }
            else
            {
                ushort uid = BitConverter.ToUInt16(data, offset);
                packedTime = BitConverter.ToUInt32(data, offset + 3);
                code = uid.ToString();
            }
            list.Add(new ZkPunch(code, DecodeTime(packedTime)));
        }
        return list;
    }

    private static string TrimNulls(string s) => s.Split('\0')[0].Trim();

    /// <summary>فرمتِ فشردهٔ زمانِ زدکتکو: ثانیه/دقیقه/ساعت/روز/ماه به‌ترتیب mod می‌شوند، سال از ۲۰۰۰ به بعد.</summary>
    private static DateTime DecodeTime(uint t)
    {
        int second = (int)(t % 60); t /= 60;
        int minute = (int)(t % 60); t /= 60;
        int hour = (int)(t % 24); t /= 24;
        int day = (int)(t % 31) + 1; t /= 31;
        int month = (int)(t % 12) + 1; t /= 12;
        int year = (int)(t + 2000);
        try { return new DateTime(year, month, day, hour, minute, second); }
        catch { return DateTime.Now; }
    }
}
