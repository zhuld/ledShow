# LED 显示程序

一个 Windows 下的无边框 LED 显示程序，适用于信息大屏、倒计时、会议提醒等场景。

## 功能

- **无边框窗口**（1500×190），黑色背景，置顶显示
- **左侧 Logo**：支持图片显示（可配置路径）
- **中间滚动字幕**：横向循环滚动 / 文字较短时自动居中
- **右侧模拟时钟**：指针式时钟，阿拉伯数字 1~12，秒针连续平滑转动
- **倒计时**：支持时分秒设置（网页默认 10 分钟），颜色随剩余时间变化，结束后立即恢复字幕
- **网页控制**：内置 HTTP 服务器，通过浏览器实时控制

## 项目结构

```
ledShow/
├── .gitignore              # Git 忽略规则
├── LICENSE                 # MIT 许可证
├── README.md               # 本文件
├── .vscode/                # VS Code 配置（任务）
│   └── tasks.json
├── src/                    # 源代码目录
│   ├── ledShow.csproj      # 项目文件
│   ├── Program.cs          # 入口点
│   ├── Config.cs           # 配置文件读写
│   ├── Form1.cs            # 核心代码：字段、构造函数、窗体/LED 字体/Logo 初始化
│   ├── Form1.Clock.cs      # 模拟时钟绘制（表盘、指针）
│   ├── Form1.Scrolling.cs  # 滚动字幕 + 倒计时逻辑 + 主渲染方法
│   ├── Form1.Public.cs     # 公开 API（字幕更新、Logo、倒计时控制等）
│   ├── Form1.Designer.cs   # 窗体设计器代码
│   ├── WebControlServer.cs # 内置 HTTP 网页控制服务器
│   └── Resources/          # 资源文件
│       └── DSEG14Classic-Regular.ttf  # LED 等宽字体
├── bin/                    # 构建输出（已 gitignore）
└── obj/                    # 临时编译文件（已 gitignore）
```

## 网页控制

启动程序后，在浏览器打开 **http://localhost:8000** 即可访问控制面板。

### 控制功能

| 功能 | 说明 |
|------|------|
| **更新字幕** | 实时修改滚动文字 |
| **上传 Logo** | 上传图片替换 Logo |
| **清除 Logo** | 恢复无 Logo 状态 |
| **倒计时** | 设置时分秒启动倒计时（默认 00:10:00），支持重置 |
| **调整尺寸** | 修改窗口宽高（重启后生效） |
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
| POST | `/api/countdown/start` | `{"seconds":N}` 启动倒计时（分秒） |
| POST | `/api/countdown/reset` | 重置倒计时 |
| POST | `/api/clockface` | `{"index":N}` 切换钟面样式 (0-4) |
| POST | `/api/restart` | 重启程序 |
| POST | `/api/exit` | 退出程序 |
| GET | `/api/status` | 获取运行状态 |
| GET | `/api/autostart` | 查询开机自启状态 |
| POST | `/api/autostart` | `{"enable":true/false}` 设置/取消开机自启 |

### 端口配置

在 `config.json` 中设置 `webPort` 修改端口（默认 8000）：

```json
{
  "webPort": 8080
}
```

## 钟面样式

网页控制面板可选择 5 种钟面配色：

| 索引 | 名称 | 风格 |
|------|------|------|
| 0 | 🔵 经典蓝 | 蓝光科技风（默认） |
| 1 | ⚪ 极简白 | 简洁白灰 |
| 2 | 🟠 复古琥珀 | 琥珀/磷光单色 |
| 3 | 🟢 霓虹青 | 青蓝霓虹赛博风 |
| 4 | 🟡 暗金 | 奢华金棕 |

## 倒计时

- **显示格式**：`MM:SS`，使用 DSEG14 LED 等宽字体
- **颜色变化**：> 5 分钟绿色 → 1~5 分钟橙色 → ≤ 1 分钟红色
- **1 分钟提醒**：到达 1 分钟时系统提示音 + 金色文字闪烁 2 秒
- **结束后**：立即恢复滚动字幕

## 使用方法

### 构建

```bash
dotnet build src\ledShow.csproj
```

### 运行

```bash
dotnet run --project src\ledShow.csproj
```

或使用 VS Code 任务：`Ctrl+Shift+B` → **Build LED Display** / **Run LED Display**

### 配置文件

程序会在 exe 同目录自动创建 `config.json`，编辑即可生效：

```json
{
  "width": 1500,
  "height": 190,
  "webPort": 8000,
  "marqueeText": "热烈欢迎，这里是LED显示程序",
  "logoPath": ""
}
```

| 属性 | 说明 |
|------|------|
| `width` / `height` | 窗口尺寸 |
| `webPort` | 网页控制端口（默认 8000） |
| `marqueeText` | 滚动字幕内容（较长时自动滚动，较短时居中） |
| `logoPath` | Logo 图片路径（留空显示占位文字） |

### 更换 Logo

在 `config.json` 中设置 `logoPath`，或通过代码调用：

```csharp
form.SetLogo(@"C:\path\to\logo.png");
form.SetLogo(Image.FromFile("logo.png"));
```

## 技术栈

- .NET Framework 2.0 + Windows Forms
- GDI+ 自定义绘制（时钟表盘、滚动字幕、锥形指针）
- `Application.Idle` 驱动高帧率动画
- `HttpListener` 内置 HTTP 服务器
- DSEG14 LED 等宽字体（SIL Open Font License）
- JSON 配置文件
