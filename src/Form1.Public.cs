using System;
using System.Drawing;
using System.IO;
using System.Media;
using System.Windows.Forms;

namespace ledShow
{
    public partial class Form1
    {
        private const string AUTOSTART_KEY = "LED显示屏";

        // ═══════════════════════════════════════════
        //  公开方法：开机自启
        // ═══════════════════════════════════════════

        /// <summary>设置或取消开机自启</summary>
        public static void SetAutoStart(bool enable)
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true))
            {
                if (enable)
                    key.SetValue(AUTOSTART_KEY, Application.ExecutablePath);
                else
                    key.DeleteValue(AUTOSTART_KEY, false);
            }
        }

        /// <summary>查询是否已设置开机自启</summary>
        public static bool GetAutoStart()
        {
            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run"))
            {
                return key != null && key.GetValue(AUTOSTART_KEY) != null;
            }
        }
        // ═══════════════════════════════════════════
        //  公开方法：钟面样式
        // ═══════════════════════════════════════════

        /// <summary>切换钟面样式（线程安全）</summary>
        public void SetClockFace(int index)
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => SetClockFace(index)));
                return;
            }

            if (index < 0 || index > 4) return;
            _clockFaceIndex = index;
            Invalidate();
        }

        /// <summary>获取当前钟面样式索引</summary>
        public int GetClockFace() { return _clockFaceIndex; }

        // ═══════════════════════════════════════════
        //  公开方法：更换 Logo
        // ═══════════════════════════════════════════
        private void EnsureLogoBox()
        {
            if (logoBox != null) return;
            int logoSize = Math.Min(formHeight - 20, 200);
            logoBox = new PictureBox
            {
                Location = new Point(10, (formHeight - logoSize) / 2),
                Size = new Size(logoSize, logoSize),
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.Black,
            };
            Controls.Add(logoBox);
        }

        public void SetLogo(string imagePath)
        {
            if (File.Exists(imagePath))
            {
                EnsureLogoBox();
                logoBox.Image = Image.FromFile(imagePath);
                logoBox.Invalidate();
            }
        }

        public void SetLogo(Image image)
        {
            EnsureLogoBox();
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
            marqueeFont = new Font("微软雅黑", fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
            using (var tmpG = CreateGraphics())
            {
                marqueeTextWidth = tmpG.MeasureString(marqueeText, marqueeFont).Width;
            }

            float marqueeLeft = GetMarqueeLeft();
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

            if (logoBox == null) return;

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
            _countdownEndPlayed = false;
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
