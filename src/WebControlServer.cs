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

        /// <summary>钟面样式名称列表</summary>
        private static readonly string[] THEME_NAMES = new string[] {
            "典雅白陶瓷", "深海幽蓝", "勃艮第酒红", "陨石灰", "墨玉黑金"
        };

        private int Port { get { return _config.WebPort; } }

        // ═══════════════════════════════════════════
        //  内嵌 HTML 控制页面
        // ═══════════════════════════════════════════
        private const string HTML_PAGE = @"<!DOCTYPE html>
<html lang=""zh-CN"">
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
<title>LED 显示屏控制</title>
<style>
* { margin:0; padding:0; box-sizing:border-box; }
body {
  font-family: -apple-system, 'Microsoft YaHei', sans-serif;
  background: #0d1117; color: #c9d1d9; min-height: 100vh;
  display: flex; justify-content: center; align-items: center;
}
.container {
  background: #161b22; border-radius: 12px; padding: 40px;
  width: 520px; max-width: 94vw; box-shadow: 0 8px 32px rgba(0,0,0,.5);
  border: 1px solid #30363d;
}
h1 { font-size: 22px; margin-bottom: 28px; color: #58a6ff; text-align:center; }
.form-group { margin-bottom: 18px; }
label { display: block; font-size: 13px; margin-bottom: 6px; color: #8b949e; }
input[type=""text""], textarea {
  width:100%; padding:10px 12px; border:1px solid #30363d; border-radius:6px;
  background:#0d1117; color:#c9d1d9; font-size:14px; outline:none;
  transition: border-color .2s;
}
input[type=""text""]:focus, textarea:focus { border-color:#58a6ff; }
textarea { resize:vertical; min-height:60px; font-family:inherit; }
input[type=""file""] {
  width:100%; padding:8px 0; font-size:13px; color:#8b949e;
}
.btn-row { display:flex; gap:10px; margin-top:6px; }
.btn {
  flex:1; padding:10px; border:none; border-radius:6px; font-size:14px;
  cursor:pointer; font-weight:600; transition: opacity .2s;
}
.btn:hover { opacity:.85; }
.btn-primary { background:#238636; color:#fff; }
.btn-danger { background:#da3633; color:#fff; }
.btn-secondary { background:#21262d; color:#c9d1d9; border:1px solid #30363d; }
.btn-success { background:#238636; color:#fff; border:1px solid #2ea043; }
.status-bar {
  margin-top:20px; padding:10px 14px; border-radius:6px;
  font-size:13px; display:none; text-align:center;
}
.status-bar.success { display:block; background:#0d3a1a; color:#3fb950; border:1px solid #196c2e; }
.status-bar.error { display:block; background:#3a0d0d; color:#f85149; border:1px solid #6c1919; }
.status-bar.info { display:block; background:#0d1f3a; color:#58a6ff; border:1px solid #1c4b8c; }
.section-title {
  font-size:14px; color:#8b949e; margin:24px 0 12px; padding-bottom:6px;
  border-bottom:1px solid #21262d;
}
.grid-2 { display:grid; grid-template-columns:1fr 1fr; gap:10px; }
  .grid-3 { display:grid; grid-template-columns:1fr 1fr 1fr; gap:10px; }
@media(max-width:480px){ .grid-2 { grid-template-columns:1fr; } .grid-3 { grid-template-columns:1fr; } }
.server-info { text-align:center; font-size:12px; color:#484f58; margin-top:24px; }
</style>
</head>
<body>
<div class=""container"">
  <h1>🔴 LED 显示屏控制</h1>

  <div class=""section-title"">字幕文字</div>
  <div class=""form-group"">
    <label for=""marqueeText"">滚动文字</label>
    <textarea id=""marqueeText"" placeholder=""输入显示文字...""></textarea>
    <div class=""btn-row"">
      <button class=""btn btn-primary"" onclick=""updateMarquee()"">更新字幕</button>
    </div>
  </div>

  <div class=""section-title"">Logo 图片</div>
  <div class=""form-group"">
    <label for=""logoFile"">上传 Logo 图片</label>
    <input type=""file"" id=""logoFile"" accept=""image/*"">
    <div class=""btn-row"">
      <button class=""btn btn-primary"" onclick=""uploadLogo()"">上传 Logo</button>
      <button class=""btn btn-danger"" onclick=""clearLogo()"">清除 Logo</button>
    </div>
  </div>

  <div class=""section-title"">窗口尺寸</div>
  <div class=""grid-2"">
    <div class=""form-group"">
      <label for=""width"">宽度</label>
      <input type=""text"" id=""width"" placeholder=""1500"">
    </div>
    <div class=""form-group"">
      <label for=""height"">高度</label>
      <input type=""text"" id=""height"" placeholder=""190"">
    </div>
  </div>
  <div class=""btn-row"">
    <button class=""btn btn-primary"" onclick=""updateConfig()"">保存配置</button>
    <button class=""btn btn-secondary"" onclick=""loadConfig()"">刷新</button>
  </div>

  <div class=""section-title"">钟面样式</div>
  <div class=""form-group"">
    <label for=""clockFace"">选择钟面</label>
    <select id=""clockFace"" onchange=""setClockFace()"">
      <option value=""0"">🤍 典雅白陶瓷</option>
      <option value=""1"">💙 深海幽蓝</option>
      <option value=""2"">❤️ 勃艮第酒红</option>
      <option value=""3"">🩶 陨石灰</option>
      <option value=""4"">🖤 墨玉黑金</option>
    </select>
  </div>

  <div class=""section-title"">倒计时</div>
  <div class=""grid-2"">
    <div class=""form-group"">
      <label for=""cdMin"">分</label>
      <input type=""text"" id=""cdMin"" placeholder=""10"" value=""10"">
    </div>
    <div class=""form-group"">
      <label for=""cdSec"">秒</label>
      <input type=""text"" id=""cdSec"" placeholder=""0"" value=""0"">
    </div>
  </div>
  <div class=""btn-row"">
    <button class=""btn btn-primary"" onclick=""startCountdown()"">开始倒计时</button>
    <button class=""btn btn-danger"" onclick=""resetCountdown()"">重置</button>
  </div>

  <div class=""section-title"">操作</div>
  <div class=""btn-row"">
    <button class=""btn btn-secondary"" onclick=""restartApp()"">重启程序</button>
    <button class=""btn btn-danger"" onclick=""exitApp()"">退出程序</button>
  </div>

  <div class=""section-title"">开机自启</div>
  <div class=""btn-row"">
    <button class=""btn btn-primary"" id=""autoStartBtn"" onclick=""toggleAutoStart()"">加载中...</button>
  </div>

  <div id=""status"" class=""status-bar""></div>
  <div class=""server-info"">LED Web Control v1.0</div>
</div>

<script>
async function api(method, path, body) {
  const opts = { method };
  if (body instanceof FormData) {
    opts.body = body;
  } else if (body !== undefined) {
    opts.headers = { 'Content-Type': 'application/json' };
    opts.body = JSON.stringify(body);
  }
  const r = await fetch(path, opts);
  const text = await r.text();
  try { return JSON.parse(text); } catch(e) { return text; }
}

function showStatus(msg, type) {
  const el = document.getElementById('status');
  el.textContent = msg; el.className = 'status-bar ' + type;
  setTimeout(() => { if (el.className.includes(type)) el.style.display='none'; }, 5000);
}

async function loadConfig() {
  try {
    const [cfg, cfInfo] = await Promise.all([
      api('GET', '/api/config'),
      api('GET', '/api/clockface')
    ]);
    document.getElementById('marqueeText').value = cfg.marqueeText || '';
    document.getElementById('width').value = cfg.width || 1500;
    document.getElementById('height').value = cfg.height || 190;
    document.getElementById('clockFace').value = cfg.clockFace || 0;
    // 更新下拉选项文字
    const sel = document.getElementById('clockFace');
    if (cfInfo && cfInfo.themes) {
      for (let i = 0; i < sel.options.length && i < cfInfo.themes.length; i++) {
        sel.options[i].text = cfInfo.themes[i].name;
      }
    }
  } catch(e) { showStatus('加载配置失败', 'error'); }
}

async function setClockFace() {
  const idx = parseInt(document.getElementById('clockFace').value);
  try {
    const r = await api('POST', '/api/clockface', { index: idx });
    if (r.success) showStatus('钟面已切换: ' + r.name, 'success');
    else showStatus(r.error || '切换失败', 'error');
  } catch(e) { showStatus('请求失败: ' + e.message, 'error'); }
}

async function updateMarquee() {
  const text = document.getElementById('marqueeText').value;
  if (!text) { showStatus('请输入文字', 'error'); return; }
  try {
    const r = await api('POST', '/api/marquee', { text });
    if (r.success) showStatus('字幕已更新', 'success');
    else showStatus(r.error || '更新失败', 'error');
  } catch(e) { showStatus('请求失败: ' + e.message, 'error'); }
}

async function uploadLogo() {
  const fileInput = document.getElementById('logoFile');
  if (!fileInput.files || !fileInput.files[0]) { showStatus('请选择图片', 'error'); return; }
  const fd = new FormData();
  fd.append('logo', fileInput.files[0]);
  try {
    const r = await api('POST', '/api/logo', fd);
    if (r.success) showStatus('Logo 已更新', 'success');
    else showStatus(r.error || '上传失败', 'error');
  } catch(e) { showStatus('请求失败: ' + e.message, 'error'); }
}

async function clearLogo() {
  try {
    const r = await api('POST', '/api/logo/clear');
    if (r.success) showStatus('Logo 已清除', 'success');
    else showStatus(r.error || '清除失败', 'error');
  } catch(e) { showStatus('请求失败: ' + e.message, 'error'); }
}

async function updateConfig() {
  const w = parseInt(document.getElementById('width').value);
  const h = parseInt(document.getElementById('height').value);
  const t = document.getElementById('marqueeText').value;
  if (!w || !h) { showStatus('请输入有效尺寸', 'error'); return; }
  try {
    const r = await api('POST', '/api/config', { width: w, height: h, marqueeText: t });
    if (r.success) showStatus('配置已保存（重启后生效尺寸）', 'success');
    else showStatus(r.error || '保存失败', 'error');
  } catch(e) { showStatus('请求失败: ' + e.message, 'error'); }
}

async function startCountdown() {
  const m = parseInt(document.getElementById('cdMin').value) || 0;
  const s = parseInt(document.getElementById('cdSec').value) || 0;
  const total = m * 60 + s;
  if (total < 1) { showStatus('请输入有效的时间', 'error'); return; }
  try {
    const r = await api('POST', '/api/countdown/start', { seconds: total });
    if (r.success) {
      showStatus('倒计时已开始: ' + String(m).padStart(2,'0') + ':' + String(s).padStart(2,'0'), 'success');
    }
    else showStatus(r.error || '启动失败', 'error');
  } catch(e) { showStatus('请求失败: ' + e.message, 'error'); }
}

async function resetCountdown() {
  try {
    const r = await api('POST', '/api/countdown/reset');
    if (r.success) showStatus('倒计时已重置', 'info');
    else showStatus(r.error || '重置失败', 'error');
  } catch(e) { showStatus('请求失败: ' + e.message, 'error'); }
}

async function restartApp() {
  if (!confirm('确定要重启程序吗？')) return;
  try { await api('POST', '/api/restart'); } catch(e) {}
}

async function toggleAutoStart() {
  const btn = document.getElementById('autoStartBtn');
  const enable = btn.dataset.enabled === 'false';
  try {
    const r = await api('POST', '/api/autostart', { enable });
    if (r.success) { btn.dataset.enabled = enable ? 'true' : 'false'; updateAutoStartBtn(); }
    else showStatus(r.error || '操作失败', 'error');
  } catch(e) { showStatus('请求失败: ' + e.message, 'error'); }
}

async function checkAutoStart() {
  try {
    const r = await api('GET', '/api/autostart');
    const btn = document.getElementById('autoStartBtn');
    btn.dataset.enabled = r.enabled ? 'true' : 'false';
    updateAutoStartBtn();
  } catch(e) {}
}

function updateAutoStartBtn() {
  const btn = document.getElementById('autoStartBtn');
  const enabled = btn.dataset.enabled === 'true';
  btn.textContent = enabled ? '✅ 已开启开机自启（点击关闭）' : '⛔ 未开启开机自启（点击开启）';
  btn.className = enabled ? 'btn btn-success' : 'btn btn-secondary';
}

async function exitApp() {
  if (!confirm('确定要退出程序吗？')) return;
  try { await api('POST', '/api/exit'); } catch(e) {}
}

loadConfig();
checkAutoStart();
</script>
</body>
</html>";

        public WebControlServer(Form1 form, Config config)
        {
            _form = form;
            _config = config;
            _listener = new HttpListener();
        }

        public void Start()
        {
            if (_running) return;

            // 直接使用 localhost 前缀，无需管理员权限
            _listener.Prefixes.Add("http://localhost:" + Port + "/");

            try
            {
                _listener.Start();
                _running = true;
                _serverThread = new Thread(ListenLoop)
                {
                    IsBackground = true
                };
                _serverThread.Start();
                Console.WriteLine("[WebControl] 服务器已启动: http://localhost:" + Port);
            }
            catch (Exception ex)
            {
                Console.WriteLine("[WebControl] 启动失败: " + ex.Message);
            }
        }

        public void Stop()
        {
            _running = false;
            try { _listener.Stop(); } catch { }
        }

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
                    break;
                }
                catch (InvalidOperationException)
                {
                    break;
                }
            }
        }

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
                    ServeHtml(response, HTML_PAGE);
                    return;
                }

                // ── API 路由 ──
                switch (path)
                {
                    case "/api/config":
                        HandleConfigApi(request, response);
                        break;
                    case "/api/marquee":
                        HandleMarqueeApi(request, response);
                        break;
                    case "/api/logo":
                        HandleLogoApi(request, response);
                        break;
                    case "/api/logo/clear":
                        HandleLogoClearApi(response);
                        break;
                    case "/api/countdown/start":
                        HandleCountdownStartApi(request, response);
                        break;
                    case "/api/countdown/reset":
                        HandleCountdownResetApi(response);
                        break;
                    case "/api/clockface":
                        HandleClockFaceApi(request, response);
                        break;
                    case "/api/autostart":
                        HandleAutoStartApi(request, response);
                        break;
                    case "/api/restart":
                        HandleRestartApi(response);
                        break;
                    case "/api/exit":
                        HandleExitApi(response);
                        break;
                    case "/api/status":
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
