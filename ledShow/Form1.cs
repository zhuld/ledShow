using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ledShow
{
    // 用于线程安全调用的委托
    public delegate void StringArgDelegate(string text);

    public partial class Form1 : Form
    {
        // ── 无边框窗口拖动 ──
        // public const int WM_NCLBUTTONDOWN = 0xA1;
        // public const int HT_CAPTION = 0x2;

        // [DllImport("user32.dll")]
        // public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        // [DllImport("user32.dll")]
        // public static extern bool ReleaseCapture();

        // ── 控件 ──
        private PictureBox logoBox = null;

        // ── 时钟区域 ──
        private int clockAreaLeft;
        private int clockAreaSize;

        // ── 滚动字幕 ──
        private float scrollX;                // 当前文字 X 坐标
        private int lastTick;                 // 上次刷新时间戳（帧平滑）
        private string marqueeText;
        private Font marqueeFont = null;
        private float marqueeTextWidth;
        private bool marqueeNeedsScroll;      // 文字是否需要滚动

        // ── 倒计时 ──
        private enum CountdownState { Idle, Running, Finished }
        private CountdownState _countdownState = CountdownState.Idle;
        private int _countdownTotalSeconds;
        private double _countdownRemainingSeconds;
        private int _countdownEndTick;          // Environment.TickCount 倒计时结束时刻
        private bool _reminded1Min;             // 1 分钟提醒是否已触发
        private string _notificationMsg = "";   // 当前提醒文字
        private int _notificationEndTick;       // 提醒显示截止时刻

        // ── 字体 ──
        private System.Drawing.Text.PrivateFontCollection _fontCollection = null;
        private FontFamily _countdownFontFamily = null;

        // ── 时钟 ──

        private readonly int formWidth;
        private readonly int formHeight;
        private readonly Config config;

        public Form1(Config config)
        {
            this.config = config;
            formWidth = config.Width;
            formHeight = config.Height;
            marqueeText = config.MarqueeText;
            InitializeComponent();
            LoadFont();
            InitForm();
            InitLogo();
            InitClock();
            InitMarquee();
        }

    // ═══════════════════════════════════════════
    //  加载 LED 字体
    // ═══════════════════════════════════════════
    private void LoadFont()
    {
        try
        {
            string exeDir = Path.GetDirectoryName(typeof(Config).Assembly.Location);
            string fontPath = Path.Combine(exeDir, "DSEG14Classic-Regular.ttf");
            if (File.Exists(fontPath))
            {
                _fontCollection = new System.Drawing.Text.PrivateFontCollection();
                _fontCollection.AddFontFile(fontPath);
                if (_fontCollection.Families.Length > 0)
                    _countdownFontFamily = _fontCollection.Families[0];
            }
        }
        catch
        {
            // 字体加载失败则使用系统默认字体
        }
    }

    // ═══════════════════════════════════════════
    //  窗体初始化
    // ═══════════════════════════════════════════
    private void InitForm()
    {
        Width = formWidth;
        Height = formHeight;
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.Black;
        Location = new Point(0, 0);
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        DoubleBuffered = true;

        // 允许拖动窗口
        // MouseDown += (s, e) =>
        // {
        //     if (e.Button == MouseButtons.Left)
        //     {
        //         ReleaseCapture();
        //         SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
        //     }
        // };
    }

    // ═══════════════════════════════════════════
    //  Logo 区（左侧，可更换）
    // ═══════════════════════════════════════════
    private void InitLogo()
    {
        int logoSize = Math.Min(formHeight - 20, 200);
        logoBox = new PictureBox
        {
            Location = new Point(10, (formHeight - logoSize) / 2),
            Size = new Size(logoSize, logoSize),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Black,
        };
        logoBox.Paint += LogoPlaceholderPaint;
        Controls.Add(logoBox);

        // 从配置加载 Logo
        if (!string.IsNullOrEmpty(config.LogoPath))
        {
            SetLogo(config.LogoPath);
        }
    }

    private void LogoPlaceholderPaint(object sender, PaintEventArgs e)
    {
        // 未设置图片时显示 "LOGO" 占位文字
        var pb = sender as PictureBox;
        if (pb == null || pb.Image != null) return;

        e.Graphics.TextRenderingHint = TextRenderingHint.AntiAlias;
        using (var font = new Font("Arial", 20, FontStyle.Bold))
        using (var brush = new SolidBrush(Color.DimGray))
        {
            var sz = e.Graphics.MeasureString("LOGO", font);
            var pt = new PointF((pb.Width - sz.Width) / 2, (pb.Height - sz.Height) / 2);
            e.Graphics.DrawString("LOGO", font, brush, pt);
        }
    }

    // ═══════════════════════════════════════════
    //  模拟时钟（右侧）
    // ═══════════════════════════════════════════
    private void InitClock()
    {
        clockAreaSize = Math.Min(formHeight - 20, 200);
        clockAreaLeft = formWidth - clockAreaSize - 10;
        // 直接在双缓冲窗体上绘制时钟（消除闪烁）
        Paint += Form_ClockPaint;
        // 无论字幕是否滚动，时钟都需要持续刷新
        Application.Idle += OnClockRefresh;
    }

    private void OnClockRefresh(object sender, EventArgs e)
    {
        Invalidate();
    }

    private void Form_ClockPaint(object sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;

        float cx = clockAreaLeft + clockAreaSize / 2f;
        float cy = (formHeight - clockAreaSize) / 2f + clockAreaSize / 2f;
        float r = (clockAreaSize - 6) / 2f;

        // ── 外圈光晕 ──
        using (var glowPen = new Pen(Color.FromArgb(30, 100, 180, 255), 12))
        {
            g.DrawEllipse(glowPen, cx - r - 4, cy - r - 4, (r + 4) * 2, (r + 4) * 2);
        }

        // ── 表盘底色 ──
        using (var faceBrush = new SolidBrush(Color.FromArgb(20, 20, 35)))
        {
            g.FillEllipse(faceBrush, cx - r, cy - r, r * 2, r * 2);
        }

        // ── 外圈边框 ──
        using (var outlinePen = new Pen(Color.FromArgb(80, 140, 220), 2.5f))
        {
            g.DrawEllipse(outlinePen, cx - r, cy - r, r * 2, r * 2);
        }

        // ── 内圈装饰 ──
        using (var innerPen = new Pen(Color.FromArgb(40, 100, 180), 1))
        {
            g.DrawEllipse(innerPen, cx - r + 8, cy - r + 8, (r - 8) * 2, (r - 8) * 2);
        }

        // ── 刻度 & 阿拉伯数字 1~12 ──
        for (int i = 0; i < 12; i++)
        {
            double angle = i * 30 - 90;
            double rad = angle * Math.PI / 180;

            float outerX = cx + (float)(r * Math.Cos(rad));
            float outerY = cy + (float)(r * Math.Sin(rad));

            bool isHour = i % 3 == 0;
            float innerR = isHour ? r - 14 : r - 8;
            float innerX = cx + (float)(innerR * Math.Cos(rad));
            float innerY = cy + (float)(innerR * Math.Sin(rad));

            using (var tickPen = new Pen(isHour ? Color.FromArgb(180, 220, 255) : Color.FromArgb(80, 120, 160),
                                        isHour ? 3 : 1.5f))
            {
                g.DrawLine(tickPen, innerX, innerY, outerX, outerY);
            }

            // 阿拉伯数字 1~12
            float numR = r - 22;
            float numX = cx + (float)(numR * Math.Cos(rad));
            float numY = cy + (float)(numR * Math.Sin(rad));
            string num = (i == 0 ? 12 : i).ToString();
            using (var numFont = new Font("Arial", 10, FontStyle.Bold))
            using (var numBrush = new SolidBrush(Color.FromArgb(160, 200, 240)))
            {
                var sz = g.MeasureString(num, numFont);
                g.DrawString(num, numFont, numBrush, numX - sz.Width / 2, numY - sz.Height / 2);
            }
        }

        // ── 实时时间 ──
        DateTime now = DateTime.Now;
        double sec = now.Second + now.Millisecond / 1000.0;
        double min = now.Minute + sec / 60.0;
        double hour = (now.Hour % 12) + min / 60.0;

        // ── 时针（多边形形状）──
        float hAngle = (float)(hour * 30 - 90);
        DrawHandShape(g, cx, cy, hAngle, r * 0.4f, 5, 2, Color.FromArgb(200, 230, 255));

        // ── 分针 ──
        float mAngle = (float)(min * 6 - 90);
        DrawHandShape(g, cx, cy, mAngle, r * 0.6f, 3.5f, 1.5f, Color.FromArgb(140, 200, 255));

        // ── 秒针（带反向尾针）──
        float sAngle = (float)(sec * 6 - 90);
        double sRad = sAngle * Math.PI / 180;
        float sEndX = cx + (float)(r * 0.72f * Math.Cos(sRad));
        float sEndY = cy + (float)(r * 0.72f * Math.Sin(sRad));
        float sTailX = cx - (float)(r * 0.18f * Math.Cos(sRad));
        float sTailY = cy - (float)(r * 0.18f * Math.Sin(sRad));
        using (var secPen = new Pen(Color.OrangeRed, 1.5f) { EndCap = System.Drawing.Drawing2D.LineCap.Round })
        {
            g.DrawLine(secPen, sTailX, sTailY, sEndX, sEndY);
        }

        // ── 中心圆点（双层）──
        using (var centerBrush = new SolidBrush(Color.FromArgb(100, 180, 255)))
        {
            g.FillEllipse(centerBrush, cx - 5, cy - 5, 10, 10);
        }
        using (var centerBrush2 = new SolidBrush(Color.FromArgb(200, 230, 255)))
        {
            g.FillEllipse(centerBrush2, cx - 2.5f, cy - 2.5f, 5, 5);
        }
    }

    /// <summary>
    /// 绘制锥形指针
    /// </summary>
    private static void DrawHandShape(Graphics g, float cx, float cy, float angleDeg,
                                       float length, float baseWidth, float tipWidth, Color color)
    {
        double rad = angleDeg * Math.PI / 180;
        float perpX = (float)Math.Cos(rad + Math.PI / 2);
        float perpY = (float)Math.Sin(rad + Math.PI / 2);
        float dirX = (float)Math.Cos(rad);
        float dirY = (float)Math.Sin(rad);

        var pts = new PointF[4];
        // 根部（靠近中心）
        pts[0] = new PointF(cx + perpX * baseWidth / 2, cy + perpY * baseWidth / 2);
        pts[1] = new PointF(cx - perpX * baseWidth / 2, cy - perpY * baseWidth / 2);
        // 尖端
        pts[2] = new PointF(cx + dirX * length - perpX * tipWidth / 2,
                            cy + dirY * length - perpY * tipWidth / 2);
        pts[3] = new PointF(cx + dirX * length + perpX * tipWidth / 2,
                            cy + dirY * length + perpY * tipWidth / 2);

        using (var path = new System.Drawing.Drawing2D.GraphicsPath())
        {
            path.AddPolygon(pts);
            using (var brush = new SolidBrush(color))
            {
                g.FillPath(brush, path);
            }
        }
    }

    // ═══════════════════════════════════════════
    //  横向滚动字幕（中间）
    // ═══════════════════════════════════════════
    private void InitMarquee()
    {
        int fontSize = (int)(formHeight * 0.8f);
        marqueeFont = new Font("微软雅黑", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using (var tmpG = CreateGraphics())
        {
            marqueeTextWidth = tmpG.MeasureString(marqueeText, marqueeFont).Width;
        }

        // 计算可显示宽度
        float marqueeLeft = Math.Max(logoBox.Right + 15, 10);
        float marqueeRight = clockAreaLeft - 15;
        float availableWidth = marqueeRight - marqueeLeft;

        // 文字宽度 > 显示区域才需要滚动
        marqueeNeedsScroll = marqueeTextWidth > availableWidth;

        if (marqueeNeedsScroll)
        {
            // 从右侧外开始
            scrollX = formWidth;
            lastTick = Environment.TickCount;
            Application.Idle += OnMarqueeIdle;
        }

        // 重写 Paint 事件绘制字幕
        Paint += FormPaint;
    }

    private void OnMarqueeIdle(object sender, EventArgs e)
    {
        int now = Environment.TickCount;
        int elapsed = now - lastTick;
        lastTick = now;

        // ── 倒计时逻辑 ──
        if (_countdownState == CountdownState.Running)
        {
            double remaining = (_countdownEndTick - now) / 1000.0;
            if (remaining <= 0)
            {
                _countdownRemainingSeconds = 0;
                _countdownState = CountdownState.Idle;  // 倒计时结束，立即恢复滚动字符
            }
            else
            {
                _countdownRemainingSeconds = remaining;

                // ── 到达时间点提醒 ──
                int secs = (int)Math.Ceiling(remaining);
                if (!_reminded1Min && secs <= 60 && _countdownTotalSeconds > 60)
                {
                    _reminded1Min = true;
                    ShowNotification("还剩1分钟!");
                }
            }
        }


        // ── 字幕滚动（仅在倒计时空闲时滚动）──
        if (_countdownState == CountdownState.Idle && marqueeNeedsScroll)
        {
            // 防止卡顿时瞬间大跳
            if (elapsed > 0 && elapsed < 100)
                scrollX -= 0.15f * elapsed;

            // 完全移出左侧后，将滚动位置向前回绕一整段，实现无缝衔接
            if (scrollX + marqueeTextWidth < 0)
                scrollX += marqueeTextWidth + 30;
        }

        Invalidate();           // 刷新字幕+时钟（窗体双缓冲，无闪烁）
    }

    private void FormPaint(object sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.TextRenderingHint = TextRenderingHint.AntiAlias;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // 字幕区域：logo 右侧 ~ logoBox.Right+15  到时钟区域左侧-15
        float marqueeLeft = logoBox.Right + 15;
        float marqueeRight = clockAreaLeft - 15;
        float marqueeWidth = marqueeRight - marqueeLeft;
        float centerY = Height / 2f;

        // ── 倒计时显示（优先级高于字幕）──
        if (_countdownState != CountdownState.Idle)
        {
            string displayText;
            Color textColor;

            if (_countdownState == CountdownState.Running)
            {
                // ── 有活跃提醒时显示提醒文字（持续 2 秒）──
                if (!string.IsNullOrEmpty(_notificationMsg) && Environment.TickCount < _notificationEndTick)
                {
                    int fontSize = (int)(formHeight * 0.7f);
                    using (var font = new Font("微软雅黑", fontSize, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var brush = new SolidBrush(Color.Gold))
                    {
                        g.SetClip(new RectangleF(marqueeLeft, 0, marqueeWidth, Height));
                        var sz = g.MeasureString(_notificationMsg, font);
                        float cx = marqueeLeft + (marqueeWidth - sz.Width) / 2f;
                        float cy = centerY - sz.Height / 2f;
                        g.DrawString(_notificationMsg, font, brush, cx, cy);
                        g.ResetClip();
                    }
                    return;
                }
                else
                {
                    _notificationMsg = "";  // 过期后清除
                }

                int totalSecs = (int)Math.Ceiling(_countdownRemainingSeconds);
                int h = totalSecs / 3600;
                int m = (totalSecs % 3600) / 60;
                int s = totalSecs % 60;
                displayText = string.Format("{0:D2}:{1:D2}:{2:D2}", h, m, s);
                if (totalSecs <= 60)
                    textColor = Color.Red;
                else if (totalSecs <= 300)
                    textColor = Color.Orange;
                else
                    textColor = Color.Lime;
            }
            else
            {
                displayText = "时间到!";
                textColor = Color.Gold;
            }

            int fontSize2 = (int)(formHeight * 0.63f);
            using (var font = _countdownFontFamily != null
                ? new Font(_countdownFontFamily, fontSize2, FontStyle.Bold, GraphicsUnit.Pixel)
                : new Font("Consolas", fontSize2, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(textColor))
            {
                g.SetClip(new RectangleF(marqueeLeft, 0, marqueeWidth, Height));
                var sz = g.MeasureString(displayText, font);
                float cx = marqueeLeft + (marqueeWidth - sz.Width) / 2f;
                float cy = centerY - sz.Height / 2f;
                g.DrawString(displayText, font, brush, cx, cy);
                g.ResetClip();
            }
            return;
        }

        // ── 滚动字幕 ──
        // 设置裁剪区域，使文字仅在 Logo 和时钟之间的区域可见
        g.SetClip(new RectangleF(marqueeLeft, 0, marqueeWidth, Height));

        // 用渐变颜色绘制文字
        using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
            new PointF(marqueeLeft, 0),
            new PointF(marqueeRight, 0),
            Color.Lime,
            Color.Cyan))
        {
            if (marqueeNeedsScroll)
            {
                // 滚动模式
                g.DrawString(marqueeText, marqueeFont, brush, scrollX, centerY - marqueeFont.Height / 2f);

                if (scrollX + marqueeTextWidth < marqueeRight)
                {
                    g.DrawString(marqueeText, marqueeFont, brush,
                        scrollX + marqueeTextWidth + 30, centerY - marqueeFont.Height / 2f);
                }
            }
            else
            {
                // 居中显示
                float centerX = marqueeLeft + (marqueeWidth - marqueeTextWidth) / 2f;
                g.DrawString(marqueeText, marqueeFont, brush, centerX, centerY - marqueeFont.Height / 2f);
            }
        }

        // 恢复裁剪区域
        g.ResetClip();
    }

    // ═══════════════════════════════════════════
    //  公开方法：更换 Logo
    // ═══════════════════════════════════════════
    public void SetLogo(string imagePath)
    {
        if (File.Exists(imagePath))
        {
            logoBox.Image = Image.FromFile(imagePath);
            logoBox.Invalidate();
        }
    }

    public void SetLogo(Image image)
    {
        logoBox.Image = image;
        logoBox.Invalidate();
    }

    // ═══════════════════════════════════════════
    //  公开方法：网页控制接口
    // ═══════════════════════════════════════════

    /// <summary>更新滚动字幕文字（线程安全）</summary>
    public void UpdateMarqueeText(string text)
    {
        if (InvokeRequired)
        {
            Invoke(new StringArgDelegate(UpdateMarqueeText), text);
            return;
        }

        marqueeText = text;
        // 重新计算字体和宽度
        if (marqueeFont != null)
        {
            marqueeFont.Dispose();
        }
        int fontSize = (int)(formHeight * 0.8f);
        marqueeFont = new Font("微软雅黑", fontSize, FontStyle.Bold, GraphicsUnit.Pixel);
        using (var tmpG = CreateGraphics())
        {
            marqueeTextWidth = tmpG.MeasureString(marqueeText, marqueeFont).Width;
        }

        float marqueeLeft = Math.Max(logoBox.Right + 15, 10);
        float marqueeRight = clockAreaLeft - 15;
        float availableWidth = marqueeRight - marqueeLeft;
        marqueeNeedsScroll = marqueeTextWidth > availableWidth;

        if (marqueeNeedsScroll)
        {
            scrollX = formWidth;
        }

        Invalidate();
    }

    /// <summary>清除 Logo（线程安全）</summary>
    public void ClearLogo()
    {
        if (InvokeRequired)
        {
            Invoke(new MethodInvoker(ClearLogo));
            return;
        }

        if (logoBox.Image != null)
        {
            logoBox.Image.Dispose();
            logoBox.Image = null;
        }
        logoBox.Invalidate();
    }

    // ═══════════════════════════════════════════
    //  公开方法：倒计时控制
    // ═══════════════════════════════════════════

    /// <summary>开始倒计时（线程安全）</summary>
    /// <param name="seconds">倒计时秒数</param>
    public void StartCountdown(int seconds)
    {
        if (InvokeRequired)
        {
            Invoke(new CountdownStartDelegate(StartCountdown), seconds);
            return;
        }

        _countdownTotalSeconds = seconds;
        _countdownRemainingSeconds = seconds;
        _countdownState = CountdownState.Running;
        _countdownEndTick = Environment.TickCount + seconds * 1000;
        lastTick = Environment.TickCount;
        Invalidate();
    }

    /// <summary>重置/取消倒计时（线程安全）</summary>
    public void ResetCountdown()
    {
        if (InvokeRequired)
        {
            Invoke(new MethodInvoker(ResetCountdown));
            return;
        }

        _countdownState = CountdownState.Idle;
        _countdownRemainingSeconds = 0;
        _reminded1Min = false;
        _notificationMsg = "";
        Invalidate();
    }

    /// <summary>显示提醒通知（2 秒后自动消失）</summary>
    private void ShowNotification(string msg)
    {
        _notificationMsg = msg;
        _notificationEndTick = Environment.TickCount + 2000;
        SystemSounds.Beep.Play();
    }

    /// <summary>获取倒计时状态（线程安全，供 API 调用）</summary>
    public string GetCountdownStatus()
    {
        if (InvokeRequired)
        {
            return (string)Invoke(new StringReturnDelegate(GetCountdownStatus));
        }

        string stateStr;
        switch (_countdownState)
        {
            case CountdownState.Running:
                stateStr = "running";
                break;
            case CountdownState.Finished:
                stateStr = "finished";
                break;
            default:
                stateStr = "idle";
                break;
        }

        return "{\"state\":\"" + stateStr + "\",\"remaining\":" +
            (int)Math.Ceiling(_countdownRemainingSeconds) + ",\"total\":" +
            _countdownTotalSeconds + "}";
    }
}
}

// 额外的委托定义
public delegate void CountdownStartDelegate(int seconds);
public delegate string StringReturnDelegate();
