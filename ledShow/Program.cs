using System;
using System.Windows.Forms;

namespace ledShow
{
    static class Program
    {
        private static WebControlServer _webServer;

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
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

            // 启动网页控制服务器
            _webServer = new WebControlServer(form, config);
            _webServer.Start();

            Application.Run(form);
        }
    }
}