using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace LEDCountDown
{
    static class Program
    {
        // ── 电源管理常量 ──
        // ES_CONTINUOUS = 持续生效, ES_SYSTEM_REQUIRED = 阻止系统睡眠, ES_DISPLAY_REQUIRED = 阻止显示器关闭
        private const uint ES_CONTINUOUS = 0x80000000;
        private const uint ES_SYSTEM_REQUIRED = 0x00000001;
        private const uint ES_DISPLAY_REQUIRED = 0x00000002;

        /// <summary>阻止系统进入睡眠或关闭显示器（用于 LED 展示场景）</summary>
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SetThreadExecutionState(uint esFlags);

        /// <summary>网页控制服务器实例，用于在退出时关闭</summary>
        private static WebControlServer _webServer;

        /// <summary>应用程序主入口点</summary>
        [STAThread]
        static void Main(string[] args)
        {
            // 禁止系统息屏和睡眠，确保 LED 显示持续可见
            SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);

            // 加载配置文件（文件不存在则自动创建默认配置）
            Config config = Config.Load();

            // 解析命令行参数，支持 --width 和 --height 覆盖配置文件中的窗口尺寸
            for (int i = 0; i < args.Length; i++)
            {
                int val;
                if (args[i] == "--width" && i + 1 < args.Length && int.TryParse(args[i + 1], out val))
                    config.Width = val;
                else if (args[i] == "--height" && i + 1 < args.Length && int.TryParse(args[i + 1], out val))
                    config.Height = val;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Form1 form = new Form1(config);

            // 注册应用程序退出事件：先停止 Web 服务器，再恢复系统默认电源策略
            Application.ApplicationExit += (sender, e) =>
            {
                _webServer.Stop();
                SetThreadExecutionState(ES_CONTINUOUS);
            };

            // 启动 HTTP 网页控制服务器，允许局域网内的设备通过浏览器控制 LED 显示
            _webServer = new WebControlServer(form, config);
            _webServer.Start();

            // 在控制台输出本机 IP 和局域网访问地址
            PrintLocalAddresses(config.WebPort);

            Application.Run(form);
        }

        /// <summary>输出本机 IP 地址，方便局域网访问</summary>
        private static void PrintLocalAddresses(int port)
        {
            try
            {
                Console.WriteLine("");
                Console.WriteLine("═══════════════════════════════════════");
                Console.WriteLine("  LED 网页控制面板访问地址");
                Console.WriteLine("───────────────────────────────────────");
                Console.WriteLine("  本机:    http://localhost:" + port + "/");
                Console.WriteLine("  本机:    http://127.0.0.1:" + port + "/");

                // 获取局域网 IP
                string hostName = Dns.GetHostName();
                IPAddress[] addresses = Dns.GetHostEntry(hostName).AddressList;
                foreach (IPAddress addr in addresses)
                {
                    if (addr.AddressFamily == AddressFamily.InterNetwork)
                    {
                        Console.WriteLine("  局域网:  http://" + addr + ":" + port + "/");
                    }
                }

                Console.WriteLine("═══════════════════════════════════════");
                Console.WriteLine("");
            }
            catch
            {
                // 静默忽略
            }
        }
    }
}