using System;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace ledShow
{
    public partial class Form1
    {
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
            // ════════════════════════════════════════════════
            //  2x 超采样抗锯齿：先渲染到 2x 位图，再缩放到正常尺寸
            // ════════════════════════════════════════════════
            float cx = clockAreaLeft + clockAreaSize / 2f;
            float cy = (formHeight - clockAreaSize) / 2f + clockAreaSize / 2f;
            float r = (clockAreaSize - 6) / 2f;

            using (var bmp = new Bitmap(clockAreaSize * 2, clockAreaSize * 2))
            using (var bg = Graphics.FromImage(bmp))
            {
                bg.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                bg.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                bg.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                bg.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                bg.TextRenderingHint = TextRenderingHint.AntiAlias;

                float cx2 = clockAreaSize;
                float cy2 = clockAreaSize;
                float r2 = r * 2;

                // ── 外圈光晕 ──
                using (var glowPen = new Pen(Color.FromArgb(30, 100, 180, 255), 24))
                {
                    bg.DrawEllipse(glowPen, cx2 - r2 - 8, cy2 - r2 - 8, (r2 + 8) * 2, (r2 + 8) * 2);
                }

                // ── 表盘底色 ──
                using (var faceBrush = new SolidBrush(Color.FromArgb(20, 20, 35)))
                {
                    bg.FillEllipse(faceBrush, cx2 - r2, cy2 - r2, r2 * 2, r2 * 2);
                }

                // ── 外圈边框 ──
                using (var outlinePen = new Pen(Color.FromArgb(80, 140, 220), 5))
                {
                    bg.DrawEllipse(outlinePen, cx2 - r2, cy2 - r2, r2 * 2, r2 * 2);
                }

                // ── 内圈装饰 ──
                using (var innerPen = new Pen(Color.FromArgb(40, 100, 180), 2))
                {
                    bg.DrawEllipse(innerPen, cx2 - r2 + 16, cy2 - r2 + 16, (r2 - 16) * 2, (r2 - 16) * 2);
                }

                // ── 刻度 & 阿拉伯数字 1~12 ──
                for (int i = 0; i < 12; i++)
                {
                    double angle = i * 30 - 90;
                    double rad = angle * Math.PI / 180;

                    float outerX = cx2 + (float)(r2 * Math.Cos(rad));
                    float outerY = cy2 + (float)(r2 * Math.Sin(rad));

                    bool isHour = i % 3 == 0;
                    float innerR = isHour ? r2 - 28 : r2 - 16;
                    float innerX = cx2 + (float)(innerR * Math.Cos(rad));
                    float innerY = cy2 + (float)(innerR * Math.Sin(rad));

                    using (var tickPen = new Pen(isHour ? Color.FromArgb(180, 220, 255) : Color.FromArgb(80, 120, 160),
                                                isHour ? 6 : 3))
                    {
                        bg.DrawLine(tickPen, innerX, innerY, outerX, outerY);
                    }

                    // 阿拉伯数字 1~12
                    float numR = r2 - 44;
                    float numX = cx2 + (float)(numR * Math.Cos(rad));
                    float numY = cy2 + (float)(numR * Math.Sin(rad));
                    string num = (i == 0 ? 12 : i).ToString();
                    using (var numFont = new Font("Arial", 20, FontStyle.Regular))
                    using (var numBrush = new SolidBrush(Color.FromArgb(160, 200, 240)))
                    {
                        var sz = bg.MeasureString(num, numFont);
                        bg.DrawString(num, numFont, numBrush, numX - sz.Width / 2, numY - sz.Height / 2);
                    }
                }

                // ── 实时时间 ──
                DateTime now = DateTime.Now;
                double sec = now.Second + now.Millisecond / 1000.0;
                double min = now.Minute + sec / 60.0;
                double hour = (now.Hour % 12) + min / 60.0;

                // ── 时针 ──
                float hAngle = (float)(hour * 30 - 90);
                DrawHandShape(bg, cx2, cy2, hAngle, r2 * 0.4f, 10, 4, Color.FromArgb(200, 230, 255));

                // ── 分针 ──
                float mAngle = (float)(min * 6 - 90);
                DrawHandShape(bg, cx2, cy2, mAngle, r2 * 0.6f, 7, 3, Color.FromArgb(140, 200, 255));

                // ── 秒针 ──
                float sAngle = (float)(sec * 6 - 90);
                DrawHandShape(bg, cx2, cy2, sAngle, r2 * 0.72f, 5, 1, Color.OrangeRed);

                // ── 秒针尾针 ──
                DrawHandShape(bg, cx2, cy2, sAngle + 180, r2 * 0.18f, 4, 0, Color.FromArgb(120, Color.OrangeRed));

                // ── 中心圆点（双层）──
                using (var centerBrush = new SolidBrush(Color.FromArgb(100, 180, 255)))
                {
                    bg.FillEllipse(centerBrush, cx2 - 10, cy2 - 10, 20, 20);
                }
                using (var centerBrush2 = new SolidBrush(Color.FromArgb(200, 230, 255)))
                {
                    bg.FillEllipse(centerBrush2, cx2 - 5, cy2 - 5, 10, 10);
                }

                // ── 将 2x 位图绘制到屏幕（缩放为原始尺寸）──
                var g = e.Graphics;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                float drawX = clockAreaLeft;
                float drawY = (formHeight - clockAreaSize) / 2f;
                g.DrawImage(bmp, drawX, drawY, clockAreaSize, clockAreaSize);
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
    }
}
