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
    }
}
