# LEDCountDown

一个 Windows 下的无边框 LED 倒计时显示程序，适用于信息大屏、倒计时、会议提醒等场景。

## 功能

- **无边框窗口**（默认 1500×190），黑色背景，置顶显示
- **左侧 Logo**：支持图片显示（可配置路径 / 网页上传）
- **中间滚动字幕**：横向循环滚动，发光辉光 + 边缘淡入淡出，颜色随钟面主题联动
- **右侧拟物时钟**：60 个精密刻度，锥形指针 + 配重秒针，5 种高级配色主题，2x 超采样抗锯齿
- **倒计时**：支持时分秒设置，使用 DSEG14 LED 等宽字体，颜色随剩余时间变化，结束后立即恢复字幕
- **网页控制**：内置 HTTP 服务器，支持局域网/本机浏览器实时控制
- **管理员权限**：自动请求管理员权限，自动配置防火墙和 URL ACL
- **防息屏**：程序运行时阻止系统进入睡眠和关闭显示器，适合长时间信息展示
- **命令行参数**：支持 `--width` 和 `--height` 参数覆盖窗口尺寸
- **开机自启**：支持通过网页控制面板设置/取消开机自启

## 项目结构

```
LEDCountDown/
├── .gitignore              # Git 忽略规则
├── LICENSE                 # MIT 许可证
├── README.md               # 本文件
├── .vscode/                # VS Code 配置（任务）
│   └── tasks.json
├── src/                    # 源代码目录
│   ├── app.manifest        # 应用程序清单（请求管理员权限）
│   ├── LEDCountDown.csproj # 项目文件（.NET Framework 2.0）
│   ├── Program.cs          # 入口点
│   ├── Config.cs           # 配置文件读写
│   ├── Form1.cs            # 核心字段、构造函数、初始化
│   ├── Form1.Clock.cs      # 拟物模拟时钟绘制（表盘、刻度、指针）
│   ├── Form1.Scrolling.cs  # 滚动字幕 + 倒计时逻辑 + 主渲染
│   ├── Form1.Public.cs     # 公开 API（字幕、Logo、倒计时控制等）
│   ├── Form1.Designer.cs   # 窗体设计器代码
│   ├── WebControlServer.cs # 内置 HTTP 网页控制服务器
│   ├── web/
│   │   └── index.html      # 独立网页控制面板（可直接编辑）
│   └── Resources/
│       ├── clock.ico                      # 应用图标（256×256，新拟物表盘）
│       └── DigitalNumbers-Regular.ttf     # LED 等宽字体
├── bin/                    # 构建输出（已 gitignore）
│   └── Debug/
│       ├── web/
│       │   └── index.html  # 复制的控制面板页面
│       ├── logos/          # 网页上传的 Logo 图片（运行时创建）
│       └── Resources/      # 资源文件
└── obj/                    # 临时编译文件（已 gitignore）
```

## 网页控制

启动程序后，在浏览器打开以下地址即可访问控制面板：

```
本机:   http://localhost:8000/
局域网:  http://192.168.x.x:8000/    （以实际 IP 为准）
```

程序启动时会自动输出所有可用的访问地址。

### 网络访问说明

程序以 **管理员权限** 运行时会自动执行以下操作：

1. **绑定全局端口** — 通过 `http://+:8000/` 前缀监听所有网络接口
2. **添加防火墙规则** — 自动开放 Windows 防火墙 TCP 端口（规则名 `LED Web Control`）
3. **输出访问地址** — 控制台窗口中显示本机和局域网 IP 地址

> 如果 `+` 前缀绑定失败（如非管理员运行），会自动回退到 `http://localhost:{port}/` 仅本机访问。

### 控制功能

| 功能 | 说明 |
|------|------|
| **更新字幕** | 实时修改滚动文字 |
| **上传 Logo** | 上传图片替换 Logo |
| **清除 Logo** | 恢复无 Logo 状态 |
| **倒计时** | 设置时分秒启动倒计时（默认 00:10:00），支持重置 |
| **钟面样式** | 切换 5 种高级配色主题 |
| **调整尺寸** | 修改窗口宽高（重启后生效） |
| **开机自启** | 一键设置/取消开机自启 |
| **重启程序** | 使用新配置重启 |
| **退出程序** | 关闭 LED 显示 |

### API 接口

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/` | 控制页面 |
| GET | `/api/config` | 获取当前配置 |
| POST | `/api/config` | 更新配置 |
| POST | `/api/marquee` | `{"text":"..."}` 更新字幕 |
| POST | `/api/logo` | 上传 Logo 图片 |
| POST | `/api/logo/clear` | 清除 Logo |
| POST | `/api/countdown/start` | `{"seconds":N}` 启动倒计时 |
| POST | `/api/countdown/reset` | 重置倒计时 |
| GET | `/api/clockface` | 获取当前钟面及所有可选主题列表 |
| POST | `/api/clockface` | `{"index":N}` 切换钟面样式 (0-4) |
| GET/POST | `/api/autostart` | 查询/设置开机自启 |
| GET | `/api/status` | 获取运行状态（含倒计时和开机自启信息） |
| POST | `/api/restart` | 重启程序 |
| POST | `/api/exit` | 退出程序 |

### 端口配置

在 `config.json` 中设置 `webPort` 修改端口（默认 8000）：

```json
{
  "webPort": 8080
}
```

> 修改端口后需要重启程序生效。首次以管理员运行时会自动注册 URL ACL 和防火墙规则。

## 钟面样式

网页控制面板可选择 5 种高级拟物配色主题：

| 索引 | 名称 | 风格 | 盘面 | 外圈 |
|------|------|------|------|------|
| 0 | 🤍 典雅白陶瓷 | 暖白陶瓷 + 玫瑰金 | 米白渐变 | 玫瑰金金属环 |
| 1 | 💙 深海幽蓝 | 深海蓝 + 精钢银 | 深海军蓝 | 精钢银环 |
| 2 | ❤️ 勃艮第酒红 | 勃艮第红 + 香槟金 | 暗酒红 | 玫瑰金环 |
| 3 | 🩶 陨石灰 | 炭灰 + 铂金白 | 深炭灰 | 铂金银环 |
| 4 | 🖤 墨玉黑金 | 墨玉 + 黄金 | 墨绿黑 | 黄金环 |

每个主题包含：
- 径向渐变表盘（中心亮 → 边缘暗，模拟凹面透镜）
- 斜面金属外圈（环绕渐变 + 高光切边）
- 60 个精密刻度（3 级粗细，主刻度带投影和高光）
- 锥形指针（宽底窄尖，带右下投影）
- 配重秒针（细线 + 尾部圆 + 中心小圆）
- 精钢中心轴
- 玻璃弧面反光

## 滚动字幕

- **字体**：微软雅黑，字号为窗体高度的 80%
- **发光效果**：主体文字四周半透明辉光层
- **颜色联动**：字幕颜色随钟面主题自动匹配
- **边缘淡入淡出**：左右各 40px 渐变遮罩，文字进入/离开时平滑过渡
- **滚动逻辑**：文字超出显示区域时从右侧外开始循环左移；未超出时居中显示
- **帧率控制**：`Application.Idle` 驱动，每帧限制最大位移防止卡顿跳变

## 模拟时钟

- **渲染引擎**：2x 超采样位图 + `HighQualityBicubic` 缩放，消除锯齿
- **表盘**：`PathGradientBrush` 径向渐变，中心亮度略高于边缘
- **外圈**：环绕渐变金属环 + 左上高光切边 + 右下阴影切边，立体斜面感
- **刻度**：60 个刻度按层级分为 12/3/6/9（最长）、其他整点（中等）、分钟（细短）
- **数字**：12 个阿拉伯数字，带微阴影
- **时针**：6 顶点锥形（宽底→收腰→尖头），短粗优雅
- **分针**：6 顶点锥形，修长
- **秒针**：细线针体 + 尾部配重圆 + 中心小圆
- **中心轴**：阴影外圈 → 金属渐变环 → 内圈主体 → 高光点
- **玻璃反光**：顶部弧面渐变，模拟曲面玻璃
- **AM/PM**：中心轴下方显示

## 倒计时

- **显示格式**：`MM:SS`，使用 DSEG14 LED 等宽字体，冒号每 500ms 闪烁
- **占位效果**：倒计时数字下层有半透明 `88:88` 占位符，保证布局稳定
- **颜色变化**：> 5 分钟绿色 → 1~5 分钟橙色 → ≤ 1 分钟红色
- **1 分钟提醒**：剩余 1 分钟时系统提示音 + 金色文字叠加显示 2 秒
- **结束后**：播放提示音，立即恢复滚动字幕

## 使用方法

### 构建

```bash
dotnet build src\LEDCountDown.csproj
```

### 运行

```bash
dotnet run --project src\LEDCountDown.csproj
```

> 首次运行会弹出 **UAC 管理员提权提示**，请点击"是"以允许程序绑定网络端口和配置防火墙。

支持命令行参数覆盖窗口尺寸：

```bash
dotnet run --project src\LEDCountDown.csproj -- --width 1920 --height 240
```

或使用 VS Code 任务：`Ctrl+Shift+B` → **Build LEDCountDown** / **Run LEDCountDown**

### 配置文件

程序会在 exe 同目录自动创建 `config.json`，编辑即可生效：

```json
{
  "width": 1500,
  "height": 190,
  "webPort": 8000,
  "marqueeText": "热烈欢迎，这里是LED显示程序",
  "logoPath": "",
  "clockFace": 0
}
```

| 属性 | 说明 |
|------|------|
| `width` / `height` | 窗口尺寸 |
| `webPort` | 网页控制端口（默认 8000） |
| `marqueeText` | 滚动字幕内容（较长时自动滚动，较短时居中） |
| `logoPath` | Logo 图片路径（留空不显示） |
| `clockFace` | 钟面样式索引 (0-4，默认 0) |

### 自定义网页界面

网页控制面板位于 `src/web/index.html`，可直接编辑 HTML/CSS/JavaScript 修改界面样式，**无需重新编译**程序即可生效。

### 更换 Logo

在 `config.json` 中设置 `logoPath`，或通过代码调用：

```csharp
form.SetLogo(@"C:\path\to\logo.png");
form.SetLogo(Image.FromFile("logo.png"));
```

## 技术栈

- .NET Framework 2.0 + Windows Forms
- GDI+ 自定义绘制：`PathGradientBrush`、`LinearGradientBrush`、`GraphicsPath`
- 2x 超采样抗锯齿 + `HighQualityBicubic` 缩放
- `Application.Idle` 驱动高帧率动画
- `SetThreadExecutionState` 阻止系统息屏/睡眠
- `HttpListener` 内置 HTTP 服务器（支持局域网访问）
- Windows 防火墙 API（`netsh advfirewall`）
- DSEG14 LED 等宽字体（SIL Open Font License）
- JSON 配置文件
