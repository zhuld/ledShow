using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace LEDCountDown
{
    public partial class Form1
    {
        private const string AUTOSTART_KEY = "LED显示屏";

        // ═══════════════════════════════════════════
        //  公开方法：开机自启
        // ═══════════════════════════════════════════

        /// <summary>
        /// 设置或取消开机自启。
        /// 通过修改注册表 HKCU\Software\Microsoft\Windows\CurrentVersion\Run 实现，
        /// 仅对当前用户生效，无需管理员权限。
        /// </summary>
        /// <param name="enable">true 启用开机自启，false 取消</param>
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
        /// <returns>true 表示已启用开机自启</returns>
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

        /// <summary>切换钟面样式（线程安全，可在后台线程直接调用）</summary>
        /// <param name="index">钟面索引（0~4，对应 5 种配色方案）</param>
        public void SetClockFace(int index)
        {
            // 若在非 UI 线程调用，通过 Invoke 封送到 UI 线程执行
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(() => SetClockFace(index)));
                return;
            }

            if (index < 0 || index > 4) return;
            _clockFaceIndex = index;
            Invalidate();   // 触发重绘，应用新钟面
        }

        // ═══════════════════════════════════════════
        //  公开方法：更换 Logo
        // ═══════════════════════════════════════════
        /// <summary>确保 Logo 控件已创建（懒加载，仅在需要时创建）</summary>
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

        /// <summary>设置 Logo 图片（线程安全，通过文件路径加载）</summary>
        /// <param name="imagePath">图片文件路径，不存在则忽略</param>
        public void SetLogo(string imagePath)
        {
            if (File.Exists(imagePath))
            {
                EnsureLogoBox();
                logoBox.Image = Image.FromFile(imagePath);
                logoBox.Invalidate();
            }
        }

        // ═══════════════════════════════════════════
        //  公开方法：网页控制接口
        // ═══════════════════════════════════════════

        /// <summary>
        /// 更新滚动字幕文字（线程安全，可在后台线程直接调用）。
        /// 会自动重新计算字体大小和是否需要滚动，若需要滚动则将位置重置到右侧起点。
        /// </summary>
        /// <param name="text">新的字幕文字</param>
        public void UpdateMarqueeText(string text)
        {
            if (InvokeRequired)
            {
                Invoke(new StringArgDelegate(UpdateMarqueeText), text);
                return;
            }

            marqueeText = text;
            // 释放旧字体，重新创建以适应新文字
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

            // 判断文字宽度是否超过可用区域，超过则需要滚动
            float marqueeLeft = GetMarqueeLeft();
            float marqueeRight = clockAreaLeft - 15;
            float availableWidth = marqueeRight - marqueeLeft;
            marqueeNeedsScroll = marqueeTextWidth > availableWidth;

            if (marqueeNeedsScroll)
            {
                scrollX = formWidth;    // 从右侧外开始，准备向左滚动
            }

            Invalidate();
        }

        /// <summary>
        /// 清除 Logo 图片并释放资源（线程安全）。
        /// 如果从未设置过 Logo（logoBox 为 null）则直接返回。
        /// </summary>
        public void ClearLogo()
        {
            if (InvokeRequired)
            {
                Invoke(new MethodInvoker(ClearLogo));
                return;
            }

            if (logoBox == null) return;

            // 释放 Image 资源防止内存泄漏
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

        /// <summary>
        /// 开始倒计时（线程安全）。
        /// 设置总秒数、剩余秒数和结束时刻（基于 Environment.TickCount），
        /// 然后将状态切换为 Running 并触发重绘。
        /// </summary>
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

        /// <summary>
        /// 重置/取消倒计时（线程安全）。
        /// 将状态恢复为 Idle，清除所有倒计时相关标记，界面恢复显示滚动字幕。
        /// </summary>
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
            Invalidate();
        }

        /// <summary>
        /// 获取倒计时状态 JSON（线程安全，供网页控制 API 调用）。
        /// 返回格式：{"state":"idle|running|finished","remaining":N,"total":N}
        /// </summary>
        public string GetCountdownStatus()
        {
            if (InvokeRequired)
            {
                return (string)Invoke(new StringReturnDelegate(GetCountdownStatus));
            }

            // 将枚举状态映射为字符串，供前端解析
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
