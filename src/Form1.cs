using System;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Windows.Forms;

namespace LEDCountDown
{
    // 用于线程安全调用的委托
    public delegate void StringArgDelegate(string text);

    public partial class Form1 : Form
    {
        // ═══════════════════════════════════════════
        //  字段定义
        // ═══════════════════════════════════════════

        // ── 控件 ──
        private PictureBox logoBox = null;            // Logo 显示控件（懒加载）

        // ── 时钟区域 ──
        private int clockAreaLeft;                    // 模拟时钟左上角 X 坐标
        private int clockAreaSize;                    // 模拟时钟边长（正方形）

        // ── 滚动字幕 ──
        private float scrollX;                        // 当前文字 X 坐标（用于滚动动画）
        private int lastTick;                         // 上次空闲帧的时间戳（帧平滑用）
        private string marqueeText;                   // 当前字幕文字内容
        private Font marqueeFont = null;               // 字幕字体（按窗体高度动态创建）
        private float marqueeTextWidth;               // 字幕文字像素宽度
        private bool marqueeNeedsScroll;               // 文字宽度是否超过显示区域，需要滚动

        // ── 倒计时 ──
        private enum CountdownState { Idle, Running, Finished }
        private CountdownState _countdownState = CountdownState.Idle;
        private int _countdownTotalSeconds;            // 倒计时总秒数
        private double _countdownRemainingSeconds;     // 剩余秒数（支持毫秒精度）
        private int _countdownEndTick;                 // Environment.TickCount 倒计时结束时刻
        private bool _reminded1Min;                    // 1 分钟提醒提示音是否已触发
        private bool _countdownEndPlayed;              // 倒计时结束提示音是否已播放

        // ── 字体 ──
        private PrivateFontCollection _fontCollection = null;   // LED 数字字体集合
        private FontFamily _countdownFontFamily = null;         // 加载的 LED 字体族

        // ── 时钟样式索引（0~4） ──
        private int _clockFaceIndex;

        // ── 窗体尺寸与配置 ──
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
            SetIcon();
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
                    _fontCollection = new PrivateFontCollection();
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
        //  设置窗体和程序图标
        // ═══════════════════════════════════════════
        private void SetIcon()
        {
            try
            {
                string exeDir = Path.GetDirectoryName(typeof(Config).Assembly.Location);
                string iconPath = Path.Combine(Path.Combine(exeDir, "Resources"), "clock.ico");
                if (File.Exists(iconPath))
                    Icon = new Icon(iconPath);
            }
            catch
            {
                // 图标加载失败则使用默认图标
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
        //  Logo 区（左侧，可选）
        // ═══════════════════════════════════════════
        private void InitLogo()
        {
            // 仅在配置了 Logo 路径时才创建 Logo 控件
            if (!string.IsNullOrEmpty(config.LogoPath))
            {
                int logoSize = Math.Min(formHeight - 20, 200);
                logoBox = new PictureBox
                {
                    Location = new Point(10, (formHeight - logoSize) / 2),
                    Size = new Size(logoSize, logoSize),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    BackColor = Color.Black,
                };
                Controls.Add(logoBox);
                SetLogo(config.LogoPath);
            }
        }
    }

    // 额外的委托定义
    public delegate void CountdownStartDelegate(int seconds);
    public delegate string StringReturnDelegate();
}
