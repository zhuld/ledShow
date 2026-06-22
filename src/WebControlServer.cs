using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace LEDCountDown
{
    /// <summary>
    /// 简易 HTTP 服务器，提供 LED 显示的网页控制功能
    /// </summary>
    public class WebControlServer : IDisposable
    {
        private readonly HttpListener _listener;
        private readonly Form1 _form;
        private readonly Config _config;
        private Thread _serverThread;
        private bool _running;

        /// <summary>钟面样式名称列表（索引对应 Form1.Clock.cs 中的 CLOCK_THEMES）</summary>
        private static readonly string[] THEME_NAMES = new string[] {
            "典雅白陶瓷", "深海幽蓝", "勃艮第酒红", "陨石灰", "墨玉黑金"
        };

        /// <summary>获取监听的端口号（从配置中读取）</summary>
        private int Port { get { return _config.WebPort; } }

        // ═══════════════════════════════════════════
        //  HTML 控制页面
        // ═══════════════════════════════════════════

        /// <summary>获取 web/index.html 的完整路径</summary>
        private static string HtmlPagePath
        {
            get
            {
                string exeDir = Path.GetDirectoryName(typeof(WebControlServer).Assembly.Location);
                return Path.Combine(Path.Combine(exeDir, "web"), "index.html");
            }
        }

        /// <summary>从磁盘加载 HTML 控制页面，文件不存在时返回错误提示</summary>
        private static string LoadHtmlPage()
        {
            string path = HtmlPagePath;
            if (File.Exists(path))
                return File.ReadAllText(path, Encoding.UTF8);
            return "<html><body><h1>页面文件丢失</h1><p>找不到 " + path + "</p></body></html>";
        }

        /// <summary>
        /// 初始化 Web 控制服务器。
        /// 注意：此时尚未启动监听，需调用 Start() 方法。
        /// </summary>
        public WebControlServer(Form1 form, Config config)
        {
            _form = form;
            _config = config;
            _listener = new HttpListener();
        }

        /// <summary>
        /// 启动 HTTP 服务。
        /// 优先绑定到 http://+:port （允许局域网远程访问），
        /// 若失败则回退到 http://localhost:port （仅本机访问）。
        /// 启动前会自动尝试添加 Windows 防火墙入站规则。
        /// </summary>
        public void Start()
        {
            if (_running) return;

            string portStr = Port.ToString();

            // 使用 "+" 前缀绑定到所有网络接口，允许局域网内的设备访问
            _listener.Prefixes.Add("http://+:" + portStr + "/");

            try
            {
                AddFirewallRule(portStr);

                _listener.Start();
                _running = true;
                _serverThread = new Thread(ListenLoop) { IsBackground = true };
                _serverThread.Start();
                Console.WriteLine("[WebControl] 已启动: http://+:" + portStr);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WebControl] 使用 + 前缀失败 (" + ex.Message + ")，尝试 localhost...");

                // 回退到 localhost 绑定（通常因权限不足，需要管理员才能绑定 +）
                try { _listener.Stop(); } catch { }
                _listener.Prefixes.Clear();
                _listener.Prefixes.Add("http://localhost:" + portStr + "/");

                try
                {
                    _listener.Start();
                    _running = true;
                    _serverThread = new Thread(ListenLoop) { IsBackground = true };
                    _serverThread.Start();
                    Console.WriteLine("[WebControl] 已启动: http://localhost:" + portStr + "（仅本机访问）");
                }
                catch (Exception ex2)
                {
                    Console.WriteLine("[WebControl] 启动失败: " + ex2.Message);
                }
            }
        }

        /// <summary>
        /// 添加 Windows 防火墙入站规则，允许外部设备通过 TCP 访问 Web 控制端口。
        /// 如果规则已存在则跳过；非管理员运行时静默失败。
        /// </summary>
        private static void AddFirewallRule(string port)
        {
            try
            {
                // 先检查规则是否已存在，避免重复添加
                var checkPsi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "advfirewall firewall show rule name=\"LED Web Control\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var checkP = System.Diagnostics.Process.Start(checkPsi))
                {
                    checkP.WaitForExit(2000);
                    if (checkP.ExitCode == 0)
                        return; // 规则已存在，跳过
                }

                // 规则不存在，添加允许 TCP 入站的新规则
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "advfirewall firewall add rule name=\"LED Web Control\" dir=in action=allow protocol=TCP localport=" + port,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    p.WaitForExit(3000);
                }
            }
            catch
            {
                // 非管理员运行时无法操作防火墙，静默跳过
            }
        }

        /// <summary>停止 HTTP 监听服务</summary>
        public void Stop()
        {
            _running = false;
            try { _listener.Stop(); } catch { }
        }

        /// <summary>
        /// HTTP 请求监听循环（在独立后台线程中运行）。
        /// 每收到一个请求就将其放入 ThreadPool 异步处理，避免阻塞下个请求。
        /// </summary>
        private void ListenLoop()
        {
            while (_running)
            {
                try
                {
                    var context = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
                }
                catch (HttpListenerException)
                {
                    break;  // 监听器被停止时退出循环
                }
                catch (InvalidOperationException)
                {
                    break;  // 监听器已关闭或未启动
                }
            }
        }

        /// <summary>
        /// 路由处理：根据 URL 路径分发到对应的处理方法。
        /// 根路径 / 或 /index.html 返回静态控制页面；
        /// /api/* 路径由对应 API 处理器处理。
        /// </summary>
        private void HandleRequest(HttpListenerContext ctx)
        {
            try
            {
                var request = ctx.Request;
                var response = ctx.Response;
                string path = request.Url.AbsolutePath.ToLowerInvariant();

                // ── 静态页面 ──
                if (path == "/" || path == "/index.html")
                {
                    ServeHtml(response, LoadHtmlPage());
                    return;
                }

                // ── API 路由分发 ──
                switch (path)
                {
                    case "/api/config":          // GET 获取配置 / POST 修改配置
                        HandleConfigApi(request, response);
                        break;
                    case "/api/marquee":          // POST 更新滚动字幕文字
                        HandleMarqueeApi(request, response);
                        break;
                    case "/api/logo":             // POST 上传 Logo 图片
                        HandleLogoApi(request, response);
                        break;
                    case "/api/logo/clear":       // POST 清除 Logo
                        HandleLogoClearApi(response);
                        break;
                    case "/api/countdown/start":  // POST 开始倒计时
                        HandleCountdownStartApi(request, response);
                        break;
                    case "/api/countdown/reset":  // POST 重置倒计时
                        HandleCountdownResetApi(response);
                        break;
                    case "/api/clockface":        // GET 获取钟面列表 / POST 切换钟面
                        HandleClockFaceApi(request, response);
                        break;
                    case "/api/autostart":        // GET 查询自启 / POST 设置自启
                        HandleAutoStartApi(request, response);
                        break;
                    case "/api/restart":          // POST 重启程序
                        HandleRestartApi(response);
                        break;
                    case "/api/exit":             // POST 退出程序
                        HandleExitApi(response);
                        break;
                    case "/api/status":           // GET 获取运行状态
                        HandleStatusApi(response);
                        break;
                    default:
                        Serve404(response);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WebControl] 请求处理错误: " + ex.Message);
            }
        }

        // ═══════════════════════════════════════════
        //  API 处理
        // ═══════════════════════════════════════════

        private void HandleConfigApi(HttpListenerRequest req, HttpListenerResponse resp)
        {
            if (req.HttpMethod == "GET")
            {
                string json = _config.ToJson();
                ServeJson(resp, json);
            }
            else if (req.HttpMethod == "POST")
            {
                string body = ReadBody(req);
                try
                {
                    // 解析 JSON
                    int w = ParseJsonInt(body, "width");
                    int h = ParseJsonInt(body, "height");
                    string t = ParseJsonString(body, "marqueeText");
                    string cfStr = ExtractValue(body, "clockFace");

                    if (w > 0) _config.Width = w;
                    if (h > 0) _config.Height = h;
                    if (t != null) _config.MarqueeText = t;
                    if (cfStr != null) { int cf = int.Parse(cfStr); if (cf >= 0 && cf < THEME_NAMES.Length) { _config.ClockFace = cf; _form.SetClockFace(cf); } }

                    _config.Save();

                    // 立即更新字幕
                    if (t != null)
                        _form.UpdateMarqueeText(t);

                    ServeJson(resp, "{\"success\":true}");
                }
                catch
                {
                    ServeJson(resp, "{\"success\":false,\"error\":\"解析失败\"}");
                }
            }
        }

        private void HandleMarqueeApi(HttpListenerRequest req, HttpListenerResponse resp)
        {
            string body = ReadBody(req);
            string text = ParseJsonString(body, "text");
            if (text == null)
            {
                ServeJson(resp, "{\"success\":false,\"error\":\"缺少 text 字段\"}");
                return;
            }

            _config.MarqueeText = text;
            _config.Save();
            _form.UpdateMarqueeText(text);

            ServeJson(resp, "{\"success\":true}");
        }

        private void HandleLogoApi(HttpListenerRequest req, HttpListenerResponse resp)
        {
            if (req.ContentType != null && req.ContentType.StartsWith("multipart/form-data"))
            {
                try
                {
                    string logoDir = Path.Combine(
                        Path.GetDirectoryName(typeof(Config).Assembly.Location), "logos");
                    if (!Directory.Exists(logoDir))
                        Directory.CreateDirectory(logoDir);

                    // 简单边界解析
                    string boundary = GetBoundary(req.ContentType);
                    byte[] bodyBytes = ReadBodyBytes(req);
                    var fileData = ExtractFileFromMultipart(bodyBytes, boundary);

                    if (fileData != null)
                    {
                        string fileName = "logo_" + DateTime.Now.Ticks + fileData.Extension;
                        string filePath = Path.Combine(logoDir, fileName);
                        File.WriteAllBytes(filePath, fileData.Data);

                        _config.LogoPath = filePath;
                        _config.Save();
                        _form.SetLogo(filePath);

                        ServeJson(resp, "{\"success\":true}");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    ServeJson(resp, "{\"success\":false,\"error\":\"" + EscapeJson(ex.Message) + "\"}");
                    return;
                }
            }

            ServeJson(resp, "{\"success\":false,\"error\":\"不支持的请求格式\"}");
        }

        private void HandleLogoClearApi(HttpListenerResponse resp)
        {
            _config.LogoPath = "";
            _config.Save();
            _form.ClearLogo();
            ServeJson(resp, "{\"success\":true}");
        }

        private void HandleRestartApi(HttpListenerResponse resp)
        {
            ServeJson(resp, "{\"success\":true}");
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(500);
                Stop();
                System.Diagnostics.Process.Start(
                    typeof(Form1).Assembly.Location,
                    "--width " + _config.Width + " --height " + _config.Height);
                Environment.Exit(0);
            });
        }

        private void HandleExitApi(HttpListenerResponse resp)
        {
            ServeJson(resp, "{\"success\":true}");
            ThreadPool.QueueUserWorkItem(_ =>
            {
                Thread.Sleep(500);
                Stop();
                Environment.Exit(0);
            });
        }

        private void HandleCountdownStartApi(HttpListenerRequest req, HttpListenerResponse resp)
        {
            string body = ReadBody(req);
            int seconds = ParseJsonInt(body, "seconds");

            if (seconds <= 0)
            {
                ServeJson(resp, "{\"success\":false,\"error\":\"seconds 必须大于 0\"}");
                return;
            }

            _form.StartCountdown(seconds);
            ServeJson(resp, "{\"success\":true}");
        }

        private void HandleCountdownResetApi(HttpListenerResponse resp)
        {
            _form.ResetCountdown();
            ServeJson(resp, "{\"success\":true}");
        }

        private void HandleClockFaceApi(HttpListenerRequest req, HttpListenerResponse resp)
        {
            if (req.HttpMethod == "GET")
            {
                int idx = _config.ClockFace;
                string json = "{\"index\":" + idx + ",\"name\":\"" + THEME_NAMES[idx] +
                    "\",\"themes\":[";
                for (int i = 0; i < THEME_NAMES.Length; i++)
                {
                    if (i > 0) json += ",";
                    json += "{\"index\":" + i + ",\"name\":\"" + THEME_NAMES[i] + "\"}";
                }
                json += "]}";
                ServeJson(resp, json);
                return;
            }

            string body = ReadBody(req);
            int index = ParseJsonInt(body, "index");
            if (index < 0 || index >= THEME_NAMES.Length)
            {
                ServeJson(resp, "{\"success\":false,\"error\":\"无效的钟面索引 (0-" +
                    (THEME_NAMES.Length - 1) + ")\"}");
                return;
            }
            _config.ClockFace = index;
            _config.Save();
            _form.SetClockFace(index);
            ServeJson(resp, "{\"success\":true,\"name\":\"" + THEME_NAMES[index] + "\"}");
        }
        private void HandleAutoStartApi(HttpListenerRequest req, HttpListenerResponse resp)
        {
            if (req.HttpMethod == "GET")
            {
                bool enabled = Form1.GetAutoStart();
                ServeJson(resp, "{\"enabled\":" + (enabled ? "true" : "false") + "}");
            }
            else if (req.HttpMethod == "POST")
            {
                string body = ReadBody(req);
                bool enable = ParseJsonBool(body, "enable");
                Form1.SetAutoStart(enable);
                ServeJson(resp, "{\"success\":true}");
            }
        }
        private void HandleStatusApi(HttpListenerResponse resp)
        {
            int cf = _config.ClockFace;
            string json = "{\"running\":true,\"marqueeText\":" +
                EscapeJson(_config.MarqueeText) + ",\"width\":" +
                _config.Width + ",\"height\":" + _config.Height +
                ",\"clockFace\":" + cf + ",\"clockFaceName\":\"" +
                THEME_NAMES[cf] + "\",\"autoStart\":" +
                (Form1.GetAutoStart() ? "true" : "false") +
                ",\"countdown\":" + _form.GetCountdownStatus() + "}";
            ServeJson(resp, json);
        }

        // ═══════════════════════════════════════════
        //  辅助方法
        // ═══════════════════════════════════════════

        private static void ServeHtml(HttpListenerResponse resp, string html)
        {
            byte[] data = Encoding.UTF8.GetBytes(html);
            resp.ContentType = "text/html; charset=utf-8";
            resp.ContentLength64 = data.Length;
            resp.OutputStream.Write(data, 0, data.Length);
            resp.OutputStream.Close();
        }

        private static void ServeJson(HttpListenerResponse resp, string json)
        {
            byte[] data = Encoding.UTF8.GetBytes(json);
            resp.ContentType = "application/json; charset=utf-8";
            resp.ContentLength64 = data.Length;
            resp.Headers.Add("Access-Control-Allow-Origin", "*");
            resp.OutputStream.Write(data, 0, data.Length);
            resp.OutputStream.Close();
        }

        private static void Serve404(HttpListenerResponse resp)
        {
            resp.StatusCode = 404;
            byte[] data = Encoding.UTF8.GetBytes("404 Not Found");
            resp.ContentLength64 = data.Length;
            resp.OutputStream.Write(data, 0, data.Length);
            resp.OutputStream.Close();
        }

        private static string ReadBody(HttpListenerRequest req)
        {
            using (var reader = new StreamReader(req.InputStream, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        private static byte[] ReadBodyBytes(HttpListenerRequest req)
        {
            using (var ms = new MemoryStream())
            {
                byte[] buffer = new byte[4096];
                int bytesRead;
                while ((bytesRead = req.InputStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, bytesRead);
                }
                return ms.ToArray();
            }
        }

        private static string GetBoundary(string contentType)
        {
            foreach (string part in contentType.Split(';'))
            {
                string trimmed = part.Trim();
                if (trimmed.StartsWith("boundary="))
                    return trimmed.Substring("boundary=".Length).Trim('"');
            }
            return "----WebKitFormBoundary";
        }

        private class FileData
        {
            public string Extension;
            public byte[] Data;
        }

        private static FileData ExtractFileFromMultipart(byte[] body, string boundary)
        {
            string boundaryMarker = "--" + boundary;
            byte[] markerBytes = Encoding.ASCII.GetBytes(boundaryMarker);

            // 找到第一个 boundary
            int start = IndexOfBytes(body, markerBytes, 0);
            if (start < 0) return null;

            // 跳到第一个 boundary 后的内容
            int pos = start + markerBytes.Length;
            // 跳过 \r\n
            while (pos < body.Length && (body[pos] == '\r' || body[pos] == '\n')) pos++;

            // 找第二个 boundary（数据结束）
            int end = IndexOfBytes(body, markerBytes, pos);
            if (end < 0) return null;

            // 提取头部和数据之间的部分
            int headerEnd = IndexOfBytes(body, Encoding.ASCII.GetBytes("\r\n\r\n"), pos);
            if (headerEnd < 0 || headerEnd > end) return null;

            headerEnd += 4; // 跳过 \r\n\r\n

            // 获取文件名扩展名
            string header = Encoding.ASCII.GetString(body, pos, headerEnd - pos);
            string ext = ".png";
            if (header.Contains("image/jpeg") || header.Contains("image/jpg"))
                ext = ".jpg";
            else if (header.Contains("image/gif"))
                ext = ".gif";
            else if (header.Contains("image/bmp"))
                ext = ".bmp";

            // 提取文件数据
            int dataLen = end - headerEnd;
            // 去掉末尾的 \r\n
            while (dataLen > 0 && (body[headerEnd + dataLen - 1] == '\r' || body[headerEnd + dataLen - 1] == '\n'))
                dataLen--;

            byte[] data = new byte[dataLen];
            Array.Copy(body, headerEnd, data, 0, dataLen);

            return new FileData { Extension = ext, Data = data };
        }

        private static int IndexOfBytes(byte[] haystack, byte[] needle, int startIndex)
        {
            for (int i = startIndex; i <= haystack.Length - needle.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return i;
            }
            return -1;
        }

        private static int ParseJsonInt(string json, string key)
        {
            string val = ExtractValue(json, key);
            if (val == null) return 0;
            int result;
            int.TryParse(val, out result);
            return result;
        }

        private static bool ParseJsonBool(string json, string key)
        {
            string val = ExtractValue(json, key);
            return val != null && val.Trim().ToLower() == "true";
        }

        private static string ParseJsonString(string json, string key)
        {
            return ExtractValue(json, key);
        }

        private static string ExtractValue(string json, string key)
        {
            string search = "\"" + key + "\"";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return null;
            idx += search.Length;

            while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\t' || json[idx] == '\r' || json[idx] == '\n'))
                idx++;
            if (idx < json.Length && json[idx] == ':') idx++;
            while (idx < json.Length && (json[idx] == ' ' || json[idx] == '\t' || json[idx] == '\r' || json[idx] == '\n'))
                idx++;

            if (idx >= json.Length) return null;

            if (json[idx] == '"')
            {
                idx++;
                int end = idx;
                while (end < json.Length && json[end] != '"')
                {
                    if (json[end] == '\\') end++;
                    end++;
                }
                return json.Substring(idx, end - idx).Replace("\\\"", "\"").Replace("\\\\", "\\");
            }

            int start = idx;
            while (idx < json.Length && json[idx] != ',' && json[idx] != '}' && json[idx] != '\r' && json[idx] != '\n')
                idx++;
            return json.Substring(start, idx - start).Trim();
        }

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        public void Dispose()
        {
            Stop();
            try { ((IDisposable)_listener).Dispose(); } catch { }
        }
    }
}
