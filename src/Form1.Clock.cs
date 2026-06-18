using System;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace ledShow
{
    public partial class Form1
    {
        // ═══════════════════════════════════════════
        //  5 种钟面配色方案
        // ═══════════════════════════════════════════
        private struct ClockColors
        {
            public Color Face, Outline;
            public Color TickMajor, TickMinor;
            public Color NumColor;
            public Color HourHand, MinHand, SecHand;
            public Color Glow, GlowCenter;
        }

        private static readonly ClockColors[] CLOCK_THEMES = new ClockColors[5]
        {
            // 0 - Blue Classic
            new ClockColors {
                Face = Color.FromArgb(20, 20, 35),
                Outline = Color.FromArgb(80, 140, 220),
                TickMajor = Color.FromArgb(180, 220, 255),
                TickMinor = Color.FromArgb(80, 120, 160),
                NumColor = Color.FromArgb(160, 200, 240),
                HourHand = Color.FromArgb(200, 230, 255),
                MinHand = Color.FromArgb(140, 200, 255),
                SecHand = Color.OrangeRed,
                Glow = Color.FromArgb(30, 100, 180, 255),
                GlowCenter = Color.FromArgb(100, 180, 255)
            },
            // 1 - Minimal White
            new ClockColors {
                Face = Color.FromArgb(10, 10, 12),
                Outline = Color.FromArgb(60, 60, 70),
                TickMajor = Color.FromArgb(200, 200, 210),
                TickMinor = Color.FromArgb(100, 100, 110),
                NumColor = Color.FromArgb(180, 180, 190),
                HourHand = Color.FromArgb(220, 220, 230),
                MinHand = Color.FromArgb(160, 160, 170),
                SecHand = Color.FromArgb(255, 80, 80),
                Glow = Color.FromArgb(15, 200, 200, 210),
                GlowCenter = Color.FromArgb(120, 120, 130)
            },
            // 2 - Retro Amber（暖色 CRT 磷光质感）
            new ClockColors {
                Face = Color.FromArgb(12, 8, 0),
                Outline = Color.FromArgb(180, 110, 20),
                TickMajor = Color.FromArgb(255, 200, 50),
                TickMinor = Color.FromArgb(140, 80, 10),
                NumColor = Color.FromArgb(255, 200, 60),
                HourHand = Color.FromArgb(255, 210, 70),
                MinHand = Color.FromArgb(220, 160, 30),
                SecHand = Color.FromArgb(255, 130, 0),
                Glow = Color.FromArgb(30, 255, 180, 20),
                GlowCenter = Color.FromArgb(255, 200, 60)
            },
            // 3 - Neon Cyan（赛博朋克霓虹）
            new ClockColors {
                Face = Color.FromArgb(5, 5, 15),
                Outline = Color.FromArgb(0, 220, 200),
                TickMajor = Color.FromArgb(0, 255, 220),
                TickMinor = Color.FromArgb(0, 140, 160),
                NumColor = Color.FromArgb(140, 240, 230),
                HourHand = Color.FromArgb(0, 255, 220),
                MinHand = Color.FromArgb(0, 200, 200),
                SecHand = Color.FromArgb(255, 40, 130),
                Glow = Color.FromArgb(40, 0, 255, 200),
                GlowCenter = Color.FromArgb(0, 230, 220)
            },
            // 4 - Dark Gold（奢华金棕表盘）
            new ClockColors {
                Face = Color.FromArgb(18, 12, 5),
                Outline = Color.FromArgb(200, 165, 55),
                TickMajor = Color.FromArgb(230, 200, 80),
                TickMinor = Color.FromArgb(150, 120, 30),
                NumColor = Color.FromArgb(210, 180, 70),
                HourHand = Color.FromArgb(230, 210, 100),
                MinHand = Color.FromArgb(190, 160, 50),
                SecHand = Color.FromArgb(255, 120, 60),
                Glow = Color.FromArgb(25, 200, 165, 55),
                GlowCenter = Color.FromArgb(210, 185, 80)
            }
        };

        // ═══════════════════════════════════════════
        //  模拟时钟（右侧）
        // ═══════════════════════════════════════════
        private void InitClock()
        {
            clockAreaSize = Math.Min(formHeight - 20, 200);
            clockAreaLeft = formWidth - clockAreaSize - 10;
            Paint += Form_ClockPaint;
            Application.Idle += OnClockRefresh;
        }

        private void OnClockRefresh(object sender, EventArgs e)
        {
            Invalidate();
        }

        private void Form_ClockPaint(object sender, PaintEventArgs e)
        {
            var colors = CLOCK_THEMES[_clockFaceIndex];

            float cx = clockAreaLeft + clockAreaSize / 2f;
            float cy = (formHeight - clockAreaSize) / 2f + clockAreaSize / 2f;
            float r = (clockAreaSize - 6) / 2f;

            // 2x 超采样抗锯齿
            using (var bmp = new Bitmap(clockAreaSize * 2, clockAreaSize * 2))
            using (var bg = Graphics.FromImage(bmp))
            {
                bg.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                bg.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                bg.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                bg.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                bg.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                float cx2 = clockAreaSize;
                float cy2 = clockAreaSize;
                float r2 = r * 2;

                // 外圈光晕
                using (var glowPen = new Pen(colors.Glow, 24))
                    bg.DrawEllipse(glowPen, cx2 - r2 - 8, cy2 - r2 - 8, (r2 + 8) * 2, (r2 + 8) * 2);

                // 表盘底色
                using (var faceBrush = new SolidBrush(colors.Face))
                    bg.FillEllipse(faceBrush, cx2 - r2, cy2 - r2, r2 * 2, r2 * 2);

                // 外圈边框
                using (var outlinePen = new Pen(colors.Outline, 5))
                    bg.DrawEllipse(outlinePen, cx2 - r2, cy2 - r2, r2 * 2, r2 * 2);

                // 内圈装饰
                using (var innerPen = new Pen(colors.Outline, 2))
                    bg.DrawEllipse(innerPen, cx2 - r2 + 16, cy2 - r2 + 16, (r2 - 16) * 2, (r2 - 16) * 2);

                // 刻度 & 数字
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

                    using (var tickPen = new Pen(isHour ? colors.TickMajor : colors.TickMinor,
                                                isHour ? 6 : 3))
                        bg.DrawLine(tickPen, innerX, innerY, outerX, outerY);

                    // 数字
                    float numR = r2 - 44;
                    float numX = cx2 + (float)(numR * Math.Cos(rad));
                    float numY = cy2 + (float)(numR * Math.Sin(rad));
                    string num = (i == 0 ? 12 : i).ToString();
                    using (var numFont = new Font("Arial", 20, FontStyle.Regular))
                    using (var numBrush = new SolidBrush(colors.NumColor))
                    {
                        var sz = bg.MeasureString(num, numFont);
                        bg.DrawString(num, numFont, numBrush, numX - sz.Width / 2, numY - sz.Height / 2);
                    }
                }

                // 实时时间
                DateTime now = DateTime.Now;
                double sec = now.Second + now.Millisecond / 1000.0;
                double min = now.Minute + sec / 60.0;
                double hour = (now.Hour % 12) + min / 60.0;

                DrawHandShape(bg, cx2, cy2, (float)(hour * 30 - 90), r2 * 0.4f, 10, 4, colors.HourHand);
                DrawHandShape(bg, cx2, cy2, (float)(min * 6 - 90), r2 * 0.6f, 7, 3, colors.MinHand);
                DrawHandShape(bg, cx2, cy2, (float)(sec * 6 - 90), r2 * 0.72f, 5, 1, colors.SecHand);
                DrawHandShape(bg, cx2, cy2, (float)(sec * 6 - 90 + 180), r2 * 0.18f, 4, 0,
                    Color.FromArgb(120, colors.SecHand));

                // 中心圆点
                using (var cb = new SolidBrush(colors.GlowCenter))
                    bg.FillEllipse(cb, cx2 - 10, cy2 - 10, 20, 20);
                using (var cb2 = new SolidBrush(Color.FromArgb(255, 255, 255)))
                    bg.FillEllipse(cb2, cx2 - 5, cy2 - 5, 10, 10);

                // 绘制到屏幕
                var g = e.Graphics;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                float drawX = clockAreaLeft;
                float drawY = (formHeight - clockAreaSize) / 2f;
                g.DrawImage(bmp, drawX, drawY, clockAreaSize, clockAreaSize);
            }
        }

        private static void DrawHandShape(Graphics g, float cx, float cy, float angleDeg,
                                           float length, float baseWidth, float tipWidth, Color color)
        {
            double rad = angleDeg * Math.PI / 180;
            float perpX = (float)Math.Cos(rad + Math.PI / 2);
            float perpY = (float)Math.Sin(rad + Math.PI / 2);
            float dirX = (float)Math.Cos(rad);
            float dirY = (float)Math.Sin(rad);

            var pts = new PointF[4];
            pts[0] = new PointF(cx + perpX * baseWidth / 2, cy + perpY * baseWidth / 2);
            pts[1] = new PointF(cx - perpX * baseWidth / 2, cy - perpY * baseWidth / 2);
            pts[2] = new PointF(cx + dirX * length - perpX * tipWidth / 2,
                                cy + dirY * length - perpY * tipWidth / 2);
            pts[3] = new PointF(cx + dirX * length + perpX * tipWidth / 2,
                                cy + dirY * length + perpY * tipWidth / 2);

            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path.AddPolygon(pts);
                using (var brush = new SolidBrush(color))
                    g.FillPath(brush, path);
            }
        }
    }
}
