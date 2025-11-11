using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LpsGateway.Lib60870;

namespace MasterSimulatorM3;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== IEC-102 M3 Master Simulator ===");
        Console.WriteLine("This tool demonstrates IEC-102 Master (TCP Client) functionality");
        Console.WriteLine();

        string host = args.Length >= 1 ? args[0] : "localhost";
        int port = args.Length >= 2 && int.TryParse(args[1], out int p) ? p : 3000;
        ushort stationAddr = args.Length >= 3 && ushort.TryParse(args[2], out ushort addr) ? addr : (ushort)0xFFFF;

        // Create logger
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        var logger = loggerFactory.CreateLogger<Iec102Master>();

        // Create master
        var master = new Iec102Master(host, port, stationAddr, logger, 5000, 3);

        // Subscribe to events
        master.FrameReceived += (sender, frame) =>
        {
            Console.WriteLine($"\n>>> 接收到帧: {frame}");
            if (frame.UserData.Length > 0)
            {
                Console.WriteLine($"    TypeId: 0x{frame.UserData[0]:X2}");
                Console.WriteLine($"    Data Length: {frame.UserData.Length} bytes");
            }
        };

        master.ConnectionChanged += (sender, isConnected) =>
        {
            Console.WriteLine($"\n>>> 连接状态变化: {(isConnected ? "已连接" : "已断开")}");
        };

        try
        {
            // Connect to slave
            Console.WriteLine($"\n正在连接到从站 {host}:{port}...");
            var connected = await master.ConnectAsync();
            
            if (!connected)
            {
                Console.WriteLine("连接失败！");
                return;
            }

            Console.WriteLine("连接成功！\n");

            // Interactive menu
            while (true)
            {
                Console.WriteLine("\n=== 主菜单 ===");
                Console.WriteLine("1. 复位远方链路");
                Console.WriteLine("2. 请求链路状态");
                Console.WriteLine("3. 请求1级用户数据");
                Console.WriteLine("4. 请求2级用户数据");
                Console.WriteLine("5. 发送时间同步");
                Console.WriteLine("6. 发送文件点播请求");
                Console.WriteLine("7. 发送文件取消");
                Console.WriteLine("8. 执行完整初始化流程");
                Console.WriteLine("9. 退出");
                Console.Write("请选择 (1-9): ");

                var choice = Console.ReadLine();

                try
                {
                    switch (choice)
                    {
                        case "1":
                            await master.ResetLinkAsync();
                            Console.WriteLine("✓ 已发送复位链路命令");
                            break;

                        case "2":
                            await master.RequestLinkStatusAsync();
                            Console.WriteLine("✓ 已发送请求链路状态");
                            break;

                        case "3":
                            await master.RequestClass1DataAsync();
                            Console.WriteLine("✓ 已发送请求1级数据");
                            break;

                        case "4":
                            await master.RequestClass2DataAsync();
                            Console.WriteLine("✓ 已发送请求2级数据");
                            break;

                        case "5":
                            await master.SendTimeSyncAsync(DateTime.UtcNow);
                            Console.WriteLine($"✓ 已发送时间同步: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
                            break;

                        case "6":
                            Console.Write("报表类型代码 (1-19): ");
                            if (byte.TryParse(Console.ReadLine(), out byte reportType) && reportType >= 1 && reportType <= 19)
                            {
                                Console.Write("模式 (0=最新, 1=时间范围): ");
                                if (byte.TryParse(Console.ReadLine(), out byte mode))
                                {
                                    var refTime = DateTime.UtcNow.AddHours(-1);
                                    DateTime? endTime = mode == 1 ? DateTime.UtcNow : null;
                                    
                                    await master.SendFileRequestAsync(reportType, mode, refTime, endTime);
                                    Console.WriteLine($"✓ 已发送文件点播: ReportType={reportType}, Mode={mode}");
                                }
                            }
                            else
                            {
                                Console.WriteLine("✗ 无效的报表类型代码");
                            }
                            break;

                        case "7":
                            Console.Write("报表类型代码 (1-19): ");
                            if (byte.TryParse(Console.ReadLine(), out byte cancelReportType) && cancelReportType >= 1 && cancelReportType <= 19)
                            {
                                Console.Write("取消范围 (0=全部, 1=未开始, 2=进行中): ");
                                if (byte.TryParse(Console.ReadLine(), out byte cancelScope))
                                {
                                    await master.SendFileCancelAsync(cancelReportType, cancelScope);
                                    Console.WriteLine($"✓ 已发送文件取消: ReportType={cancelReportType}, Scope={cancelScope}");
                                }
                            }
                            else
                            {
                                Console.WriteLine("✗ 无效的报表类型代码");
                            }
                            break;

                        case "8":
                            Console.WriteLine("\n开始完整初始化流程...");
                            
                            Console.WriteLine("1/4 复位链路...");
                            await master.ResetLinkAsync();
                            await Task.Delay(200);
                            
                            Console.WriteLine("2/4 请求链路状态...");
                            await master.RequestLinkStatusAsync();
                            await Task.Delay(200);
                            
                            Console.WriteLine("3/4 发送时间同步...");
                            await master.SendTimeSyncAsync(DateTime.UtcNow);
                            await Task.Delay(200);
                            
                            Console.WriteLine("4/4 请求2级数据...");
                            await master.RequestClass2DataAsync();
                            await Task.Delay(200);
                            
                            Console.WriteLine("✓ 初始化流程完成");
                            break;

                        case "9":
                            Console.WriteLine("\n正在断开连接...");
                            await master.DisconnectAsync();
                            Console.WriteLine("再见！");
                            return;

                        default:
                            Console.WriteLine("✗ 无效的选项");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ 操作失败: {ex.Message}");
                }

                await Task.Delay(100); // Small delay to see responses
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ 错误: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
        finally
        {
            master.Dispose();
        }
    }
}
