using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace ledShow
{
    // 用于线程安全调用的委托
    public delegate void StringArgDelegate(string text);

    public partial class Form1 : Form
    {
    
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
        private string _notificationMsg = "";   // 当前提醒文字
        private int _notificationEndTick;       // 提醒显示截止时刻

        // ── 字体 ──
        private System.Drawing.Text.PrivateFontCollection _fontCollection = null;
        private FontFamily _countdownFontFamily = null;

        // ── 时钟样式 ──
        private int _clockFaceIndex;

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
            _clockFaceIndex = config.ClockFace;
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
            string fontPath = Path.Combine(Path.Combine(exeDir, "Resources"), "DigitalNumbers-Regular.ttf");
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
        using (var font = new Font("Arial", 20, FontStyle.Regular))
        using (var brush = new SolidBrush(Color.DimGray))
        {
            var sz = e.Graphics.MeasureString("LOGO", font);
            var pt = new PointF((pb.Width - sz.Width) / 2, (pb.Height - sz.Height) / 2);
            e.Graphics.DrawString("LOGO", font, brush, pt);
        }
    }
}

// 额外的委托定义
public delegate void CountdownStartDelegate(int seconds);
public delegate string StringReturnDelegate();
}

// 额外的委托定义
public delegate void CountdownStartDelegate(int seconds);
public delegate string StringReturnDelegate();
