using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace LEDCountDown
{
    static class Program
    {
        // 防止系统进入睡眠或关闭显示器
        // ES_CONTINUOUS = 0x80000000, ES_SYSTEM_REQUIRED = 0x00000001, ES_DISPLAY_REQUIRED = 0x00000002
        private const uint ES_CONTINUOUS = 0x80000000;
        private const uint ES_SYSTEM_REQUIRED = 0x00000001;
        private const uint ES_DISPLAY_REQUIRED = 0x00000002;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SetThreadExecutionState(uint esFlags);

        private static WebControlServer _webServer;

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // 禁止系统息屏和睡眠
            SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED);

            // 加载配置（文件不存在则自动创建）
            Config config = Config.Load();

            // 命令行参数可覆盖配置
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

            // 应用程序退出时恢复系统默认电源策略
            Application.ApplicationExit += (sender, e) =>
            {
                _webServer.Stop();
                SetThreadExecutionState(ES_CONTINUOUS);
            };

            // 启动网页控制服务器
            _webServer = new WebControlServer(form, config);
            _webServer.Start();

            // 输出局域网访问地址
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