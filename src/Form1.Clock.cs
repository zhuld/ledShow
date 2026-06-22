using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace LEDCountDown
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
            // 0 - 典雅白陶瓷（暖白盘面 + 玫瑰金）
            new ClockColors {
                Face = Color.FromArgb(235, 228, 218),
                Outline = Color.FromArgb(200, 155, 110),
                TickMajor = Color.FromArgb(80, 70, 60),
                TickMinor = Color.FromArgb(160, 145, 130),
                NumColor = Color.FromArgb(40, 35, 30),
                HourHand = Color.FromArgb(50, 45, 40),
                MinHand = Color.FromArgb(100, 85, 70),
                SecHand = Color.FromArgb(200, 120, 75),
                Glow = Color.FromArgb(30, 200, 155, 110),
                GlowCenter = Color.FromArgb(185, 140, 100)
            },
            // 1 - 深海幽蓝（深海蓝 + 精钢银）
            new ClockColors {
                Face = Color.FromArgb(10, 12, 28),
                Outline = Color.FromArgb(160, 175, 195),
                TickMajor = Color.FromArgb(200, 220, 245),
                TickMinor = Color.FromArgb(100, 120, 150),
                NumColor = Color.FromArgb(180, 205, 235),
                HourHand = Color.FromArgb(220, 230, 245),
                MinHand = Color.FromArgb(140, 165, 200),
                SecHand = Color.FromArgb(255, 90, 70),
                Glow = Color.FromArgb(25, 60, 120, 200),
                GlowCenter = Color.FromArgb(150, 190, 230)
            },
            // 2 - 勃艮第酒红（勃艮第红 + 香槟金）
            new ClockColors {
                Face = Color.FromArgb(22, 8, 10),
                Outline = Color.FromArgb(195, 145, 105),
                TickMajor = Color.FromArgb(225, 190, 140),
                TickMinor = Color.FromArgb(140, 95, 80),
                NumColor = Color.FromArgb(200, 170, 125),
                HourHand = Color.FromArgb(210, 170, 120),
                MinHand = Color.FromArgb(170, 135, 90),
                SecHand = Color.FromArgb(220, 60, 50),
                Glow = Color.FromArgb(25, 195, 145, 105),
                GlowCenter = Color.FromArgb(180, 140, 100)
            },
            // 3 - 陨石灰（炭灰 + 铂金白）
            new ClockColors {
                Face = Color.FromArgb(14, 14, 16),
                Outline = Color.FromArgb(185, 185, 195),
                TickMajor = Color.FromArgb(230, 230, 240),
                TickMinor = Color.FromArgb(130, 130, 140),
                NumColor = Color.FromArgb(200, 200, 210),
                HourHand = Color.FromArgb(225, 225, 235),
                MinHand = Color.FromArgb(160, 160, 170),
                SecHand = Color.FromArgb(60, 140, 255),
                Glow = Color.FromArgb(20, 185, 185, 195),
                GlowCenter = Color.FromArgb(180, 180, 190)
            },
            // 4 - 墨玉黑金（墨玉 + 黄金）
            new ClockColors {
                Face = Color.FromArgb(8, 12, 8),
                Outline = Color.FromArgb(215, 175, 55),
                TickMajor = Color.FromArgb(240, 210, 80),
                TickMinor = Color.FromArgb(120, 95, 30),
                NumColor = Color.FromArgb(215, 185, 65),
                HourHand = Color.FromArgb(230, 200, 75),
                MinHand = Color.FromArgb(185, 155, 45),
                SecHand = Color.FromArgb(255, 140, 20),
                Glow = Color.FromArgb(30, 215, 175, 55),
                GlowCenter = Color.FromArgb(220, 195, 70)
            }
        };

        // ═══════════════════════════════════════════
        //  颜色辅助方法
        // ═══════════════════════════════════════════
        private static Color Lighten(Color c, float factor)
        {
            return Color.FromArgb(c.A,
                Math.Min(255, (int)(c.R + (255 - c.R) * factor)),
                Math.Min(255, (int)(c.G + (255 - c.G) * factor)),
                Math.Min(255, (int)(c.B + (255 - c.B) * factor)));
        }

        private static Color Darken(Color c, float factor)
        {
            return Color.FromArgb(c.A,
                Math.Max(0, (int)(c.R * (1 - factor))),
                Math.Max(0, (int)(c.G * (1 - factor))),
                Math.Max(0, (int)(c.B * (1 - factor))));
        }

        private static Color ToOpaque(Color c)
        {
            return Color.FromArgb(255, c.R, c.G, c.B);
        }

        // ═══════════════════════════════════════════
        //  模拟时钟（右侧）— 拟物化表盘
        // ═══════════════════════════════════════════
        private void InitClock()
        {
            clockAreaSize = formHeight - 10;
            clockAreaLeft = formWidth - clockAreaSize - 4;
            Paint += Form_ClockPaint;
        }

        private void Form_ClockPaint(object sender, PaintEventArgs e)
        {
            var colors = CLOCK_THEMES[_clockFaceIndex];

            float cx = clockAreaLeft + clockAreaSize / 2f;
            float cy = (formHeight - clockAreaSize) / 2f + clockAreaSize / 2f;
            float r = (clockAreaSize - 6) / 2f;

            // 外圈厚度
            float bezelWidth = 6;
            float extraPad = bezelWidth + 2;
            float renderSize = clockAreaSize + extraPad * 2;

            // 2x 超采样
            int bmpSize = (int)(renderSize * 2);
            using (var bmp = new Bitmap(bmpSize, bmpSize))
            using (var bg = Graphics.FromImage(bmp))
            {
                bg.SmoothingMode = SmoothingMode.HighQuality;
                bg.CompositingQuality = CompositingQuality.HighQuality;
                bg.PixelOffsetMode = PixelOffsetMode.HighQuality;
                bg.InterpolationMode = InterpolationMode.HighQualityBicubic;
                bg.TextRenderingHint = TextRenderingHint.AntiAlias;

                float cx2 = bmpSize / 2f;
                float cy2 = bmpSize / 2f;
                float r2 = r * 2;
                float faceR = r2 - bezelWidth;       // 内盘半径

                // ── 底部光晕 ──
                using (var glowPen = new Pen(colors.Glow, 20))
                    bg.DrawEllipse(glowPen,
                        cx2 - faceR - 6, cy2 - faceR - 6,
                        (faceR + 6) * 2, (faceR + 6) * 2);

                // ── 外圈（斜面金属环） ──
                Color outlineOpaque = ToOpaque(colors.Outline);
                Color bezelDark = Darken(outlineOpaque, 0.4f);
                Color bezelLight = Lighten(outlineOpaque, 0.3f);

                //  外环阴影
                using (var bDark = new SolidBrush(Darken(bezelDark, 0.3f)))
                    bg.FillEllipse(bDark,
                        cx2 - r2 - 2, cy2 - r2 - 2,
                        (r2 + 2) * 2, (r2 + 2) * 2);

                //  外环主体（环绕渐变）
                using (var ringPath = new GraphicsPath())
                {
                    ringPath.AddEllipse(cx2 - r2, cy2 - r2, r2 * 2, r2 * 2);
                    using (var ringBrush = new PathGradientBrush(ringPath))
                    {
                        ringBrush.CenterColor = bezelLight;
                        ringBrush.SurroundColors = new Color[] { bezelDark };
                        bg.FillEllipse(ringBrush,
                            cx2 - r2, cy2 - r2, r2 * 2, r2 * 2);
                    }
                }

                //  高光切边（左上）
                using (var hlPath = new GraphicsPath())
                {
                    hlPath.AddArc(cx2 - r2, cy2 - r2, r2 * 2, r2 * 2, 210, 120);
                    using (var hlPen = new Pen(Color.FromArgb(120, Lighten(bezelLight, 0.4f)), 3))
                        bg.DrawPath(hlPen, hlPath);
                }

                //  阴影切边（右下）
                using (var shPath = new GraphicsPath())
                {
                    shPath.AddArc(cx2 - r2, cy2 - r2, r2 * 2, r2 * 2, 30, 120);
                    using (var shPen = new Pen(Color.FromArgb(100, Color.Black), 2.5f))
                        bg.DrawPath(shPen, shPath);
                }

                // ── 表盘（柔和径向渐变） ──
                using (var facePath = new GraphicsPath())
                {
                    facePath.AddEllipse(cx2 - faceR, cy2 - faceR, faceR * 2, faceR * 2);
                    Color faceCenter = Lighten(colors.Face, 0.20f);
                    Color faceEdge = Darken(colors.Face, 0.10f);
                    using (var faceBrush = new PathGradientBrush(facePath))
                    {
                        faceBrush.CenterColor = faceCenter;
                        faceBrush.SurroundColors = new Color[] { faceEdge };
                        faceBrush.FocusScales = new PointF(0.3f, 0.3f);
                        bg.FillPath(faceBrush, facePath);
                    }
                }

                // ── 表盘内缘阴影 ──
                using (var gp = new GraphicsPath())
                {
                    gp.AddEllipse(cx2 - faceR + 2, cy2 - faceR + 2,
                                  (faceR - 2) * 2, (faceR - 2) * 2);
                    using (var gpPen = new Pen(Color.FromArgb(40, Color.Black), 3))
                        bg.DrawPath(gpPen, gp);
                }

                // ── 60 个刻度 ──
                for (int i = 0; i < 60; i++)
                {
                    double a = i * 6 - 90;
                    double rad = a * Math.PI / 180;

                    float tickLen, tickWid;
                    Color tickColor;

                    if (i % 15 == 0)          // 12/3/6/9
                    {
                        tickLen = faceR * 0.135f;
                        tickWid = 5f;
                        tickColor = colors.TickMajor;
                    }
                    else if (i % 5 == 0)      // 其他整点
                    {
                        tickLen = faceR * 0.10f;
                        tickWid = 3.5f;
                        tickColor = colors.TickMajor;
                    }
                    else                      // 分钟
                    {
                        tickLen = faceR * 0.06f;
                        tickWid = 2f;
                        tickColor = colors.TickMinor;
                    }

                    float outerX = cx2 + (float)(faceR * Math.Cos(rad));
                    float outerY = cy2 + (float)(faceR * Math.Sin(rad));
                    float innerX = cx2 + (float)((faceR - tickLen) * Math.Cos(rad));
                    float innerY = cy2 + (float)((faceR - tickLen) * Math.Sin(rad));

                    // 主刻度阴影
                    if (i % 15 == 0)
                    {
                        using (var shPen = new Pen(Color.FromArgb(50, 0, 0, 0), tickWid + 1))
                            bg.DrawLine(shPen,
                                innerX + 1.5f, innerY + 1.5f,
                                outerX + 1.5f, outerY + 1.5f);
                    }

                    using (var pen = new Pen(tickColor, tickWid))
                        bg.DrawLine(pen, innerX, innerY, outerX, outerY);

                    // 主刻度微高光
                    if (i % 15 == 0)
                    {
                        using (var hlPen = new Pen(
                            Color.FromArgb(90, Lighten(colors.TickMajor, 0.5f)), 2))
                            bg.DrawLine(hlPen,
                                innerX - 0.8f, innerY - 0.8f,
                                outerX - 0.8f, outerY - 0.8f);
                    }
                }

                // ── 12 个数字 ──
                using (var numFont = new Font("Arial", 16, FontStyle.Regular))
                {
                    for (int i = 0; i < 12; i++)
                    {
                        double a = i * 30 - 90;
                        double rad = a * Math.PI / 180;
                        float numR = faceR * 0.72f;
                        float numX = cx2 + (float)(numR * Math.Cos(rad));
                        float numY = cy2 + (float)(numR * Math.Sin(rad));
                        string num = i == 0 ? "12" : i.ToString();

                        var sz = bg.MeasureString(num, numFont);
                        // 阴影
                        using (var shBrush = new SolidBrush(Color.FromArgb(50, 0, 0, 0)))
                            bg.DrawString(num, numFont, shBrush,
                                numX - sz.Width / 2 + 1.2f,
                                numY - sz.Height / 2 + 1.2f);
                        // 主体
                        using (var brush = new SolidBrush(colors.NumColor))
                            bg.DrawString(num, numFont, brush,
                                numX - sz.Width / 2,
                                numY - sz.Height / 2);
                    }
                }

                // ── 实时时间 ──
                DateTime now = DateTime.Now;
                double sec = now.Second + now.Millisecond / 1000.0;
                double min = now.Minute + sec / 60.0;
                double hour = (now.Hour % 12) + min / 60.0;

                // 时针阴影 + 主体
                Color shadowColor = Color.FromArgb(70, 0, 0, 0);
                DrawTaperedHand(bg, cx2 + 2.2f, cy2 + 2.2f,
                    (float)(hour * 30 - 90), faceR * 0.38f,
                    faceR * 0.09f, 2, shadowColor);
                DrawTaperedHand(bg, cx2, cy2,
                    (float)(hour * 30 - 90), faceR * 0.38f,
                    faceR * 0.09f, 2, colors.HourHand);

                // 分针阴影 + 主体
                DrawTaperedHand(bg, cx2 + 2.2f, cy2 + 2.2f,
                    (float)(min * 6 - 90), faceR * 0.58f,
                    faceR * 0.05f, 1.5f, shadowColor);
                DrawTaperedHand(bg, cx2, cy2,
                    (float)(min * 6 - 90), faceR * 0.58f,
                    faceR * 0.05f, 1.5f, colors.MinHand);

                // 秒针（细线 + 配重圆）
                DrawSecondHand(bg, cx2, cy2,
                    (float)(sec * 6 - 90), faceR * 0.70f, colors.SecHand, false);

                // ── 中心轴 ──
                // 外圈阴影
                using (var shBrush = new SolidBrush(Color.FromArgb(50, 0, 0, 0)))
                    bg.FillEllipse(shBrush, cx2 - 9, cy2 - 9, 18, 18);
                // 金属环
                using (var cp = new GraphicsPath())
                {
                    cp.AddEllipse(cx2 - 8, cy2 - 8, 16, 16);
                    using (var cb = new PathGradientBrush(cp))
                    {
                        cb.CenterColor = Lighten(colors.GlowCenter, 0.3f);
                        cb.SurroundColors = new Color[] { Darken(colors.GlowCenter, 0.25f) };
                        bg.FillPath(cb, cp);
                    }
                }
                // 内圈主体
                using (var cb = new SolidBrush(colors.GlowCenter))
                    bg.FillEllipse(cb, cx2 - 5.5f, cy2 - 5.5f, 11, 11);
                // 高光点
                using (var hl = new SolidBrush(Color.FromArgb(180, 255, 255, 255)))
                    bg.FillEllipse(hl, cx2 - 2.5f, cy2 - 2.5f, 5, 5);
                using (var dot = new SolidBrush(Color.FromArgb(255, 255, 255)))
                    bg.FillEllipse(dot, cx2 - 1.5f, cy2 - 1.5f, 3, 3);

                // ── AM / PM ──
                string ampm = now.Hour < 12 ? "AM" : "PM";
                using (var ampmFont = new Font("Arial", 13, FontStyle.Regular))
                {
                    var sz = bg.MeasureString(ampm, ampmFont);
                    using (var shBrush = new SolidBrush(Color.FromArgb(40, 0, 0, 0)))
                        bg.DrawString(ampm, ampmFont, shBrush,
                            cx2 - sz.Width / 2 + 1, cy2 + faceR * 0.38f + 1);
                    using (var brush = new SolidBrush(colors.NumColor))
                        bg.DrawString(ampm, ampmFont, brush,
                            cx2 - sz.Width / 2, cy2 + faceR * 0.38f);
                }

                // ── 玻璃反光（单弧面） ──
                using (var refPath = new GraphicsPath())
                {
                    float rw = faceR * 1.4f;
                    float rh = faceR * 0.40f;
                    refPath.AddEllipse(cx2 - rw / 2, cy2 - faceR * 0.82f, rw, rh);
                    using (var refBrush = new PathGradientBrush(refPath))
                    {
                        refBrush.CenterColor = Color.FromArgb(30, 255, 255, 255);
                        refBrush.SurroundColors = new Color[] { Color.FromArgb(0, 255, 255, 255) };
                        refBrush.FocusScales = new PointF(0.1f, 0.1f);
                        bg.FillPath(refBrush, refPath);
                    }
                }

                // ── 绘制到屏幕 ──
                var g = e.Graphics;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(bmp, cx - renderSize / 2, cy - renderSize / 2,
                            renderSize, renderSize);
            }
        }

        /// <summary>锥形指针（宽底→窄尖）</summary>
        private static void DrawTaperedHand(Graphics g, float cx, float cy,
            float angleDeg, float length, float baseWidth, float tipWidth, Color color)
        {
            double rad = angleDeg * Math.PI / 180;
            float px = (float)Math.Cos(rad + Math.PI / 2);
            float py = (float)Math.Sin(rad + Math.PI / 2);
            float dx = (float)Math.Cos(rad);
            float dy = (float)Math.Sin(rad);

            var pts = new PointF[4];
            pts[0] = new PointF(cx + px * baseWidth / 2, cy + py * baseWidth / 2);
            pts[1] = new PointF(cx - px * baseWidth / 2, cy - py * baseWidth / 2);
            pts[2] = new PointF(cx + dx * length - px * tipWidth / 2,
                                cy + dy * length - py * tipWidth / 2);
            pts[3] = new PointF(cx + dx * length + px * tipWidth / 2,
                                cy + dy * length + py * tipWidth / 2);

            using (var path = new GraphicsPath())
            {
                path.AddPolygon(pts);
                using (var brush = new SolidBrush(color))
                    g.FillPath(brush, path);
            }
        }

        /// <summary>秒针（细线 + 尾部配重圆 + 中心小圆）</summary>
        private static void DrawSecondHand(Graphics g, float cx, float cy,
            float angleDeg, float length, Color color, bool hasShadow)
        {
            double rad = angleDeg * Math.PI / 180;
            float dx = (float)Math.Cos(rad);
            float dy = (float)Math.Sin(rad);
            float px = (float)Math.Cos(rad + Math.PI / 2);
            float py = (float)Math.Sin(rad + Math.PI / 2);

            float tipX = cx + dx * length;
            float tipY = cy + dy * length;
            float tailLen = length * 0.22f;
            float tailX = cx - dx * tailLen;
            float tailY = cy - dy * tailLen;

            if (hasShadow)
            {
                using (var shPen = new Pen(Color.FromArgb(40, 0, 0, 0), 3))
                    g.DrawLine(shPen, tailX + 1.5f, tailY + 1.5f, tipX + 1.5f, tipY + 1.5f);
            }

            // 针体
            using (var pen = new Pen(color, 2.5f))
                g.DrawLine(pen, tailX, tailY, tipX, tipY);

            // 尾部配重圆
            float cr = length * 0.04f;
            using (var cb = new SolidBrush(color))
                g.FillEllipse(cb, tailX - cr, tailY - cr, cr * 2, cr * 2);

            // 中心小圆
            float dotR = length * 0.03f;
            using (var dot = new SolidBrush(color))
                g.FillEllipse(dot, cx - dotR, cy - dotR, dotR * 2, dotR * 2);
        }
    }
}
