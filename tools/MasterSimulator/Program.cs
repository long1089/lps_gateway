using System;
using System.Text;
using System.Threading.Tasks;
using LPSGateway.Lib60870;

namespace MasterSimulator
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("IEC-102 Master Simulator");
            Console.WriteLine("========================\n");

            var host = args.Length > 0 ? args[0] : "127.0.0.1";
            var port = args.Length > 1 ? int.Parse(args[1]) : 2404;

            Console.WriteLine($"Connecting to {host}:{port}...");

            var linkLayer = new TcpLinkLayer();
            
            try
            {
                await linkLayer.ConnectAsync(host, port);
                Console.WriteLine("Connected!");

                // Wait a bit for connection to stabilize
                await Task.Delay(1000);

                // Create a sample E-file content
                var gbk = Encoding.GetEncoding("GBK");
                var efileContent = @"<basic_info>
@station_id	STATION_001
@station_name	Test Power Station
@location	Building A, Floor 3
@install_date	2024-01-15
#001	Meter_A	Active	100.5	50.2
#002	Meter_B	Active	200.3	75.8
<power_data>
@measurement_time	2024-11-04 07:00:00
@unit	kWh
#001	Phase_A	1500.5	-99	Normal
#002	Phase_B	1600.2	100.3	Normal
#003	Phase_C	1550.8	105.1	Warning
";

                var efileBytes = gbk.GetBytes(efileContent);
                Console.WriteLine($"\nE-file content length: {efileBytes.Length} bytes");

                // Split into two frames for testing multi-frame handling
                var midpoint = efileBytes.Length / 2;
                var part1 = new byte[midpoint];
                var part2 = new byte[efileBytes.Length - midpoint];
                Array.Copy(efileBytes, 0, part1, 0, midpoint);
                Array.Copy(efileBytes, midpoint, part2, 0, part2.Length);

                Console.WriteLine($"Split into 2 frames: {part1.Length} + {part2.Length} bytes");

                // Address bytes (example: address 0x0001)
                var address = new byte[] { 0x01, 0x00 };

                // Send first frame (TYPE ID = 0x90, COT = 0x06 - data transfer in progress)
                Console.WriteLine("\nSending frame 1...");
                var asdu1 = AsduManager.BuildAsdu(0x90, 0x06, 0x0001, part1);
                var frame1 = new Iec102Frame(new byte[] { 0x73 }, address, asdu1);
                await linkLayer.SendFrameAsync(frame1);
                Console.WriteLine("Frame 1 sent");

                await Task.Delay(500);

                // Send second frame (TYPE ID = 0x90, COT = 0x07 - end of transfer)
                Console.WriteLine("Sending frame 2 (end of transfer)...");
                var asdu2 = AsduManager.BuildAsdu(0x90, 0x07, 0x0001, part2);
                var frame2 = new Iec102Frame(new byte[] { 0x73 }, address, asdu2);
                await linkLayer.SendFrameAsync(frame2);
                Console.WriteLine("Frame 2 sent");

                Console.WriteLine("\nE-file transfer complete!");
                Console.WriteLine("The server should now process the E-file data.");

                await Task.Delay(2000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
            finally
            {
                await linkLayer.StopAsync();
                Console.WriteLine("\nSimulator stopped.");
            }
        }
    }
}
