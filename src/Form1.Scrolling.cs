using System;
using System.Drawing;
using System.Drawing.Text;
using System.Media;
using System.Windows.Forms;

namespace LEDCountDown
{
    public partial class Form1
    {
        // ═══════════════════════════════════════════
        //  横向滚动字幕（中间）
        // ═══════════════════════════════════════════
        /// <summary>获取滚动字幕的起始 X 坐标（Logo 右侧或左侧 10px）</summary>
        private float GetMarqueeLeft()
        {
            return logoBox != null ? logoBox.Right + 15 : 10;
        }

        /// <summary>初始化滚动字幕：创建字体、判断是否需要滚动、挂载绘制事件</summary>
        private void InitMarquee()
        {
            int fontSize = (int)(formHeight * 0.8f);
            marqueeFont = new Font("微软雅黑", fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
            using (var tmpG = CreateGraphics())
            {
                marqueeTextWidth = tmpG.MeasureString(marqueeText, marqueeFont).Width;
            }

            // 计算可显示宽度
            float marqueeLeft = GetMarqueeLeft();
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

        /// <summary>
        /// 应用程序空闲时触发：更新倒计时剩余时间 + 滚动字幕位置。
        /// 使用帧平滑（elapsed 增量）确保滚动速度不受帧率波动影响。
        /// </summary>
        private void OnMarqueeIdle(object sender, EventArgs e)
        {
            int now = Environment.TickCount;
            int elapsed = now - lastTick;
            lastTick = now;

            // ── 倒计时逻辑 ──
            if (_countdownState == CountdownState.Running)
            {
                // 根据 _countdownEndTick 计算精确剩余秒数（支持毫秒级精度）
                double remaining = (_countdownEndTick - now) / 1000.0;
                if (remaining <= 0)
                {
                    // 倒计时归零：播放结束提示音（仅一次），恢复空闲状态以显示滚动字幕
                    if (!_countdownEndPlayed)
                    {
                        _countdownEndPlayed = true;
                        SystemSounds.Exclamation.Play();
                    }
                    _countdownRemainingSeconds = 0;
                    _countdownState = CountdownState.Idle;
                }
                else
                {
                    _countdownRemainingSeconds = remaining;

                    // ── 到达 1 分钟时播放提示音（仅对总时长 > 60s 的倒计时生效）──
                    int secs = (int)Math.Ceiling(remaining);
                    if (!_reminded1Min && secs <= 60 && _countdownTotalSeconds > 60)
                    {
                        _reminded1Min = true;
                        SystemSounds.Beep.Play();
                    }
                }
            }

            // ── 字幕滚动（仅在倒计时空闲时滚动）──
            if (_countdownState == CountdownState.Idle && marqueeNeedsScroll)
            {
                // 防止卡顿时瞬间大跳：仅当帧间隔 < 100ms 时才执行滚动，
                // 避免长时间挂起后（如调试断点）文字瞬间飞走
                if (elapsed > 0 && elapsed < 100)
                    scrollX -= 0.15f * elapsed;

                // 完全移出左侧后，将滚动位置向前回绕一整段，实现无缝衔接循环
                if (scrollX + marqueeTextWidth < 0)
                    scrollX += marqueeTextWidth + 30;
            }

            Invalidate();   // 触发重绘，刷新字幕 + 时钟（窗体已开启 DoubleBuffered，无闪烁）
        }

        /// <summary>
        /// 主绘制方法：绘制滚动字幕或倒计时数字。
        /// 倒计时时全屏居中显示大号 LED 数字；空闲时显示滚动字幕 + 边缘淡入淡出遮罩。
        /// </summary>
        private void FormPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            // 开启高质量渲染模式，消除锯齿
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = TextRenderingHint.AntiAlias;

            // 字幕区域：logo 右侧（无 logo 则从左侧 10px 起）到时钟区域左侧-15
            float marqueeLeft = GetMarqueeLeft();
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
                    // 计算 MM:SS 格式，冒号每 500ms 闪烁一次（奇偶交替空格/冒号）
                    int totalSecs = (int)Math.Ceiling(_countdownRemainingSeconds);
                    int m = totalSecs / 60;
                    int s = totalSecs % 60;
                    string colon = Environment.TickCount / 500 % 2 == 0 ? " " : ":";
                    displayText = string.Format("{0,2}" + colon + "{1:D2}", m, s);
                    // 不同剩余时间显示不同警示色：<=60s 红, <=300s 橙, >300s 绿
                    if (totalSecs <= 60)
                        textColor = Color.Red;
                    else if (totalSecs <= 300)
                        textColor = Color.Orange;
                    else
                        textColor = Color.Lime;
                }
                else
                {
                    // CountdownState.Finished：倒计时结束，显示金色提示
                    displayText = "时间到!";
                    textColor = Color.Gold;
                }

                int fontSize2 = (int)(formHeight * 0.63f);
                using (var font = _countdownFontFamily != null
                    ? new Font(_countdownFontFamily, fontSize2, FontStyle.Regular, GraphicsUnit.Pixel)
                    : new Font("Consolas", fontSize2, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var brush = new SolidBrush(textColor))
                {
                    var sz = g.MeasureString(displayText, font);
                    float cx = marqueeLeft + (marqueeWidth - sz.Width) / 2f;
                    float cy = centerY - sz.Height / 2f;
                    // 底层：半透明 88:88 占位符（冒号同步闪烁）
                    using (var dimBrush = new SolidBrush(Color.FromArgb(5, textColor)))
                        g.DrawString("88:88", font, dimBrush, cx, cy);
                    // 模糊边缘层：在数字周围绘制多重半透明偏移副本，营造柔光模糊效果
                    DrawBlurredEdge(g, displayText, font, textColor, cx, cy, blurRadius: 2);
                    // 顶层：实际倒计时数字（清晰层）
                    g.DrawString(displayText, font, brush, cx, cy);
                }
                return;
            }

            // ── 滚动字幕 ──
            // 设置裁剪区域，使文字仅在 Logo 和时钟之间的区域可见
            g.SetClip(new RectangleF(marqueeLeft, 0, marqueeWidth, Height));

            // 文字颜色：根据当前钟面主题选取配色
            Color textMain, textGlow;
            switch (_clockFaceIndex)
            {
                case 0: textMain = Color.FromArgb(200, 180, 160); textGlow = Color.FromArgb(40, 200, 180, 160); break;
                case 1: textMain = Color.FromArgb(160, 200, 240); textGlow = Color.FromArgb(40, 160, 200, 240); break;
                case 2: textMain = Color.FromArgb(220, 180, 140); textGlow = Color.FromArgb(40, 220, 180, 140); break;
                case 3: textMain = Color.FromArgb(200, 200, 215); textGlow = Color.FromArgb(40, 200, 200, 215); break;
                case 4: textMain = Color.FromArgb(220, 195, 80); textGlow = Color.FromArgb(40, 220, 195, 80); break;
                default: textMain = Color.FromArgb(200, 80, 80); textGlow = Color.FromArgb(40, 200, 80, 80); break;
            }

            using (var brush = new SolidBrush(textMain))
            using (var glowBrush = new SolidBrush(textGlow))
            {
                if (marqueeNeedsScroll)
                {
                    DrawGlowText(g, marqueeText, marqueeFont, glowBrush, brush,
                        scrollX, centerY - marqueeFont.Height / 2f);

                    if (scrollX + marqueeTextWidth < marqueeRight)
                    {
                        DrawGlowText(g, marqueeText, marqueeFont, glowBrush, brush,
                            scrollX + marqueeTextWidth + 30,
                            centerY - marqueeFont.Height / 2f);
                    }
                }
                else
                {
                    float centerX = marqueeLeft + (marqueeWidth - marqueeTextWidth) / 2f;
                    DrawGlowText(g, marqueeText, marqueeFont, glowBrush, brush,
                        centerX, centerY - marqueeFont.Height / 2f);
                }
            }

            // ── 边缘淡入淡出遮罩 ──
            float fadeWidth = 40;
            // 左边缘
            using (var fadeLeft = new System.Drawing.Drawing2D.LinearGradientBrush(
                new PointF(marqueeLeft, 0), new PointF(marqueeLeft + fadeWidth, 0),
                Color.Black, Color.FromArgb(0, Color.Black)))
            {
                fadeLeft.WrapMode = System.Drawing.Drawing2D.WrapMode.TileFlipX;
                g.FillRectangle(fadeLeft, marqueeLeft, 0, fadeWidth, Height);
            }
            // 右边缘
            using (var fadeRight = new System.Drawing.Drawing2D.LinearGradientBrush(
                new PointF(marqueeRight - fadeWidth, 0), new PointF(marqueeRight, 0),
                Color.FromArgb(0, Color.Black), Color.Black))
            {
                fadeRight.WrapMode = System.Drawing.Drawing2D.WrapMode.TileFlipX;
                g.FillRectangle(fadeRight, marqueeRight - fadeWidth, 0, fadeWidth, Height);
            }

            // 恢复裁剪区域
            g.ResetClip();
        }

        /// <summary>绘制带发光辉光的文字（上下左右各偏移 1px）</summary>
        private static void DrawGlowText(Graphics g, string text, Font font,
            Brush glowBrush, Brush textBrush, float x, float y)
        {
            // 发光层（4 方向偏移）
            g.DrawString(text, font, glowBrush, x - 1, y);
            g.DrawString(text, font, glowBrush, x + 1, y);
            g.DrawString(text, font, glowBrush, x, y - 1);
            g.DrawString(text, font, glowBrush, x, y + 1);
            // 主体层
            g.DrawString(text, font, textBrush, x, y);
        }

        /// <summary>
        /// 绘制文字的模糊边缘效果（柔光辉光）。
        /// 在文字周围以圆形分布绘制多重半透明偏移副本，模拟模糊边缘。
        /// </summary>
        /// <param name="g">Graphics 对象</param>
        /// <param name="text">要绘制的文字</param>
        /// <param name="font">字体</param>
        /// <param name="color">文字颜色</param>
        /// <param name="x">左上角 X</param>
        /// <param name="y">左上角 Y</param>
        /// <param name="blurRadius">模糊半径（像素），越大边缘越模糊</param>
        private static void DrawBlurredEdge(Graphics g, string text, Font font,
            Color color, float x, float y, int blurRadius = 3)
        {
            // 在圆形区域内绘制多层半透明副本，距离中心越远透明度越低
            for (int dy = -blurRadius; dy <= blurRadius; dy++)
            {
                for (int dx = -blurRadius; dx <= blurRadius; dx++)
                {
                    int distSq = dx * dx + dy * dy;
                    if (distSq > blurRadius * blurRadius)
                        continue;

                    // 根据距离计算 alpha：边缘约 20，中心附近约 4
                    float dist = (float)Math.Sqrt(distSq);
                    float ratio = dist / blurRadius; // 0~1
                    int alpha = (int)(18 * (1 - ratio * ratio));
                    if (alpha <= 0) continue;

                    using (var blurBrush = new SolidBrush(Color.FromArgb(alpha, color)))
                        g.DrawString(text, font, blurBrush, x + dx, y + dy);
                }
            }
        }
    }
}
