# LED 显示程序

一个 Windows 下的无边框 LED 显示程序，适用于信息大屏、广告展示等场景。

## 功能

- **无边框窗口**，黑色背景，置顶显示
- **左侧 Logo**：支持图片显示（可配置路径）
- **中间滚动字幕**：横向循环滚动 / 文字较短时自动居中
- **右侧模拟时钟**：指针式时钟，秒针连续平滑转动
- **配置文件**：所有属性通过 `config.json` 调整，无需改代码

## 使用方法

### 构建

```bash
dotnet build ledShow
```

### 运行

直接运行生成的 exe：

```bash
.\ledShow\bin\Debug\ledShow.exe
```

### 配置文件

程序会在 exe 同目录自动创建 `config.json`，编辑即可生效：

```json
{
  "width": 1500,
  "height": 190,
  "marqueeText": "热烈欢迎，这里是LED显示程序",
  "logoPath": ""
}
```

| 属性 | 说明 |
|------|------|
| `width` / `height` | 窗口尺寸 |
| `marqueeText` | 滚动字幕内容（较长时自动滚动，较短时居中） |
| `logoPath` | Logo 图片路径（留空显示占位文字） |

### 更换 Logo

在 `config.json` 中设置 `logoPath`，或通过代码调用：

```csharp
form.SetLogo(@"C:\path\to\logo.png");
form.SetLogo(Image.FromFile("logo.png"));
```

### 拖动窗口

按住窗口任意位置拖动即可移动。

## 技术栈

- .NET Framework 2.0 + Windows Forms
- GDI+ 自定义绘制（时钟表盘、滚动字幕、锥形指针）
- `Application.Idle` 驱动高帧率动画
- JSON 配置文件
