using System.Net.Sockets;
using System.Text;

Console.WriteLine("=== IEC-102 Master Simulator ===");
Console.WriteLine("This tool simulates a master station sending ASDU data to the gateway");
Console.WriteLine();

string host = "localhost";
int port = 2404;

if (args.Length >= 1) host = args[0];
if (args.Length >= 2) int.TryParse(args[1], out port);

Console.WriteLine($"Connecting to {host}:{port}...");

try
{
    using var client = new TcpClient();
    await client.ConnectAsync(host, port);
    Console.WriteLine("Connected successfully!");

    using var stream = client.GetStream();

    while (true)
    {
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("1. Send sample E file data (single frame)");
        Console.WriteLine("2. Send sample E file data (multi-frame)");
        Console.WriteLine("3. Send custom ASDU");
        Console.WriteLine("4. Exit");
        Console.Write("Select option: ");

        var choice = Console.ReadLine();

        switch (choice)
        {
            case "1":
                await SendSingleFrameEFile(stream);
                break;
            case "2":
                await SendMultiFrameEFile(stream);
                break;
            case "3":
                await SendCustomAsdu(stream);
                break;
            case "4":
                Console.WriteLine("Exiting...");
                return;
            default:
                Console.WriteLine("Invalid option");
                break;
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    return;
}

static async Task SendSingleFrameEFile(NetworkStream stream)
{
    Console.WriteLine("Sending single frame E file...");

    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    var gbk = Encoding.GetEncoding("GBK");

    var fileContent = @"<table> STATION_INFO
@ID	Name	Capacity
#S001	测试站点	100.5
<table> DEVICE_INFO
@ID	DeviceType	Status
#D001	Transformer	Active";

    var payload = gbk.GetBytes(fileContent);

    // Build ASDU: TypeId=0x90, COT=0x07 (last frame), CommonAddr=1001
    byte typeId = 0x90;
    byte cot = 0x07; // Last frame
    ushort commonAddr = 1001;

    var asdu = BuildAsdu(typeId, cot, commonAddr, payload);

    await stream.WriteAsync(asdu);
    await stream.FlushAsync();

    Console.WriteLine($"Sent {asdu.Length} bytes (TypeId: 0x{typeId:X2}, COT: 0x{cot:X2}, CommonAddr: {commonAddr})");
}

static async Task SendMultiFrameEFile(NetworkStream stream)
{
    Console.WriteLine("Sending multi-frame E file...");

    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    var gbk = Encoding.GetEncoding("GBK");

    var fileContent = @"<table> STATION_INFO
@ID	Name	Location	Capacity
#S001	测试站点1	北京	150.5
#S002	测试站点2	上海	200.0
#S003	测试站点3	广州	180.0
<table> ENERGY_DATA
@StationId	ActivePower	ReactivePower
#S001	145.2	30.5
#S002	195.8	40.2
#S003	175.5	35.0";

    var fullPayload = gbk.GetBytes(fileContent);

    // Split into chunks
    int chunkSize = 200;
    int chunks = (fullPayload.Length + chunkSize - 1) / chunkSize;

    byte typeId = 0x91;
    ushort commonAddr = 1002;

    for (int i = 0; i < chunks; i++)
    {
        int offset = i * chunkSize;
        int length = Math.Min(chunkSize, fullPayload.Length - offset);
        var chunk = new byte[length];
        Array.Copy(fullPayload, offset, chunk, 0, length);

        byte cot = (byte)(i == chunks - 1 ? 0x07 : 0x06); // 0x07 for last, 0x06 for intermediate

        var asdu = BuildAsdu(typeId, cot, commonAddr, chunk);
        await stream.WriteAsync(asdu);
        await stream.FlushAsync();

        Console.WriteLine($"Sent frame {i + 1}/{chunks} ({length} bytes, COT: 0x{cot:X2})");
        await Task.Delay(100); // Small delay between frames
    }

    Console.WriteLine("Multi-frame transmission complete");
}

static async Task SendCustomAsdu(NetworkStream stream)
{
    Console.Write("Enter TypeId (hex, e.g., 90): ");
    var typeIdStr = Console.ReadLine();
    if (!byte.TryParse(typeIdStr, System.Globalization.NumberStyles.HexNumber, null, out byte typeId))
    {
        Console.WriteLine("Invalid TypeId");
        return;
    }

    Console.Write("Enter COT (hex, e.g., 07): ");
    var cotStr = Console.ReadLine();
    if (!byte.TryParse(cotStr, System.Globalization.NumberStyles.HexNumber, null, out byte cot))
    {
        Console.WriteLine("Invalid COT");
        return;
    }

    Console.Write("Enter CommonAddr (decimal, e.g., 1001): ");
    var addrStr = Console.ReadLine();
    if (!ushort.TryParse(addrStr, out ushort commonAddr))
    {
        Console.WriteLine("Invalid CommonAddr");
        return;
    }

    Console.Write("Enter payload (text): ");
    var payloadText = Console.ReadLine() ?? "";
    var payload = Encoding.UTF8.GetBytes(payloadText);

    var asdu = BuildAsdu(typeId, cot, commonAddr, payload);
    await stream.WriteAsync(asdu);
    await stream.FlushAsync();

    Console.WriteLine($"Sent {asdu.Length} bytes");
}

static byte[] BuildAsdu(byte typeId, byte cot, ushort commonAddr, byte[] payload)
{
    var asdu = new byte[5 + payload.Length];
    asdu[0] = typeId;
    asdu[1] = (byte)(payload.Length + 2);
    asdu[2] = cot;
    BitConverter.GetBytes(commonAddr).CopyTo(asdu, 3);
    payload.CopyTo(asdu, 5);
    return asdu;
}
