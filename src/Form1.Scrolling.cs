using System;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace ledShow
{
    public partial class Form1
    {
        // ═══════════════════════════════════════════
        //  横向滚动字幕（中间）
        // ═══════════════════════════════════════════
        private void InitMarquee()
        {
            int fontSize = (int)(formHeight * 0.8f);
            marqueeFont = new Font("微软雅黑", fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
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

                    // ── 到达时间点提醒（预留）──
                    int secs = (int)Math.Ceiling(remaining);
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
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

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
                        using (var font = new Font("微软雅黑", fontSize, FontStyle.Regular, GraphicsUnit.Pixel))
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
                    int m = totalSecs / 60;
                    int s = totalSecs % 60;
                    displayText = string.Format("{0:D2}:{1:D2}", m, s);
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
                    ? new Font(_countdownFontFamily, fontSize2, FontStyle.Regular, GraphicsUnit.Pixel)
                    : new Font("Consolas", fontSize2, FontStyle.Regular, GraphicsUnit.Pixel))
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

            // 用渐变颜色绘制文字（上下渐变，下面深红）
            using (var brush = new System.Drawing.Drawing2D.LinearGradientBrush(
                new PointF(0, 0),
                new PointF(0, Height),
                Color.Red,
                Color.DarkRed))
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
    }
}
