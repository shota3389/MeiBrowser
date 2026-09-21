# MeiBrowser

**All in one tool for browsing and downloading Hoyoverse games packages —— with a Simplified Chinese interface.**

> 本仓库是 [Escartem/MeiBrowser](https://github.com/Escartem/MeiBrowser) 的中文本地化分支
> ｜ 仓库地址：<https://github.com/shota3389/MeiBrowser>
> ｜ 原项目作者：[@Escartem](https://github.com/Escartem)
> ｜ **本分支的中文本地化工作由 AI 辅助完成**（详见[下文](#关于-ai-的使用)）

---

## 这个仓库是什么

这是 [MeiBrowser](https://github.com/Escartem/MeiBrowser) 的一个分支，在**完整保留原有功能**的前提下，为整个图形界面增加了**中英双语 + 运行时切换**能力。

- 默认语言：**简体中文**
- 可随时切回 **English**，选择会被记住
- 所有改动都是**界面文案层面**的，不涉及下载逻辑、协议、校验算法 —— 与上游行为一致

> 界面语言在标题栏的「语言」下拉框中切换，位置在「主题」左侧。

### 下载

从 [Releases](https://github.com/shota3389/MeiBrowser/releases) 获取最新构建，
或按下文自行编译。

### 能浏览和下载的内容（与上游一致）

- 全部游戏文件（Sophon 与 Legacy 两种模式）
- 完整游戏压缩包
- 更新压缩包
- 未发布游戏（通过其 beta 链接）
- 版本之间新增/变更的文件
- Devkits 与 Betas（使用你的 stoken 文件）

---

## 对上游做了哪些改动

改动范围为 **18 个文件修改 + 1 个文件新增**。

### 核心：新增 `GUI/Localization.cs`

整个本地化能力的中枢，约 490 行，包含以下几部分：

| 成员 | 作用 |
|---|---|
| `enum AppLanguage { English, Chinese }` | 语言枚举。**成员名即持久化值，不要重命名** |
| 静态字典 `English` / `Chinese` | 两张表，各 **134 个 key**，编译期构建 |
| `Localization.T(key)` / `T(key, args)` | 取值。查不到 → 回退英文 → 回退 key 本身，**永不抛异常** |
| `LocExtension` | XAML 标记扩展，用法 `Text="{loc:Loc Setup_Mode}"` |
| `IRelocalizable` | 由支持自我重建的窗口实现，用于运行时切换 |
| `LanguageOption` | 语言下拉框的选项包装（显示名用该语言自身书写） |

**为什么用静态字典而不是 `.resx`**：不依赖构建期代码生成、可整文件 diff、
能在启动时做跨表一致性校验、迁移时不必改动 `.csproj`。

**key 命名规范**：一律用英文写法，格式 `区域_用途`，例如 `Setup_Mode`、
`Download_Confirm_Body`、`Files_ErrRegex_Title`。`_Title` 后缀专给对话框标题，`_Body` 给正文。
用英文写 key 的好处是：万一漏译，界面上显示英文而不是一个裸 key。

### 两种取值方式

```xml
<!-- XAML：控件构建时求值一次 -->
<TextBlock Text="{loc:Loc Setup_Mode}"/>
<Button Content="{loc:Loc Console_CopyAll}"/>
```

```csharp
// C#：在要显示的那一刻取，天然跟随当前语言
HeadingText.Text = Localization.T("Download_HeadingFor", request.SourceTitle);
ThemedDialog.ShowLocalized("Download_Busy_Body", "Download_Busy_Title", ...);
```

### 运行时切换的实现

`LocExtension.ProvideValue` **只在控件构建时求值一次**，所以换语言必须**重建窗口**。

`Localization.Apply(window, language)` 的流程：

```
window.Hide()  →  IRelocalizable.Relocalize()  →  由 Relocalize 自己 Show() 新窗口
```

`Apply` **不做任何 `Show()` 兜底** —— 职责单一，否则会和一个已 `Close()` 的窗口打架。

`MainWindow.Relocalize()` 里额外处理了三件事：

1. **保留已打开的标签页** —— `CaptureOpenTabs()` 记录每个 `PackageSelection`，
   新窗口 `Window_Loaded` 的最后按序重新加载。
2. **保留窗口布局** —— `WindowState`，以及在 `Normal` 状态下抄回 `Left/Top/Width/Height`。
3. **延迟关闭旧窗** —— `Close()` 必须走
   `Dispatcher.BeginInvoke(..., DispatcherPriority.Background)`，
   否则会在 ComboBox 的 `SelectionChanged` 还在调用栈上时拆窗口。

### 逐文件改动清单

| 文件 | 改动 |
|---|---|
| **`GUI/Localization.cs`** | **新增** —— 本地化核心，见上 |
| `GUI/App.xaml.cs` | `OnStartup` 里加 `Localization.Validate()`；`AppSettings.Load()` → `Localization.Initialize(...)`；崩溃提示文案走 `T()` |
| `GUI/AppSettings.cs` | 新增 `SelectedLanguage` 属性；`Load()` 用 `Enum.TryParse` 容错（旧配置缺字段时保留默认）；`Save()` 写入 |
| `GUI/MainWindow.xaml` | 加 `xmlns:loc`；标语与「主题」改用 `{loc:Loc}`；**新增语言下拉框** |
| `GUI/MainWindow.xaml.cs` | 实现 `IRelocalizable`（`Relocalize` / `CaptureOpenTabs` / `CarryOverLayoutTo` / `ReleaseDownload`）；拆出 `Setup_ConfirmedAsync` 以便重放标签页；`#region language` |
| `GUI/ThemedDialog.xaml` / `.xaml.cs` | `BuildButtons` 内 OK/Cancel/Yes/No 四类按钮文案收口到 `T()`；新增 `ShowLocalized(msgKey, titleKey, ...)` 便捷重载 |
| `GUI/Views/SetupView.xaml` / `.xaml.cs` | 全部标签与按钮改用 `{loc:Loc}`；ComboBox 用 `Tag` 存稳定值；`GameEntry` 显式类替代匿名类型 + `dynamic`；`Report()` 改为接收 key |
| `GUI/Views/DownloadView.xaml` / `.xaml.cs` | 标题、网络/磁盘、进度格式、ETA、各结局文案全部走 `T()` |
| `GUI/Views/FileTreeView.xaml` / `.xaml.cs` | 按钮/搜索/正则/筛选文案；`RefreshFilterUi()` 统一筛选按钮状态与 ToolTip；错误对话框改 `ShowLocalized` |
| `GUI/Views/ConsoleView.xaml` | Clear / Copy all / Auto-scroll |
| `GUI/FilterDialog.xaml` / `.xaml.cs` | 标题、下拉项、大小条件、提示、按钮；`TryRead` 改为接收 key |
| `GUI/SearchFilter.cs` | `Describe()` 的全部描述文案 |
| `GUI/FileItem.cs` | `Type`（文件/文件夹）与 `Elements`（元素计数） |
| `GUI/ActiveDownloadRow.cs` | 状态文本：校验中 / 重试中 / 失败 |

### 两个设计取舍

**1. ComboBox 的 `Content` 不能翻译后当判断依据。**

翻译前靠 `Content` 取值（如 `"OS"`），翻译后会变成「国际服」，逻辑立刻失效。
所以把**显示**与**取值**分开，用 `Tag` 存稳定值：

```csharp
new ComboBoxItem() { Content = Localization.T("Region_OS"), Tag = "OS" };
selectedServer = (item)?.Tag as string ?? (item)?.Content?.ToString();
```

游戏下拉框原本用匿名类型 + `dynamic`，同样的问题，改成了显式类
`GameEntry(string Id, string Name, string Icon, string Key)` —— `Id` 永远是稳定标识
（`hk4e` / `hkrpg` / `nap` / `custom`），`Name` 只管显示。

**2. 下载进行中禁止切换语言。**

下载页持有 `CancellationTokenSource` 和进度管线，重建会丢状态。
此时回滚下拉框选择并明确提示用户，而不是让它悄悄坏掉。

### 保持原样的东西

以下**有意未翻译**，因为它们属于协议或格式的一部分：

- 游戏内部的 URL、`package_id`、`/getBuild` 之类的接口路径
- `" (pre-download)"` 这类用于版本字符串比对的标记
- `OpenFileDialog` 的 Filter 描述（`SToken Build|*.bin|...` 形式，管道符号是语法）
- 单位 `KB` / `MB` / `GB`
- `Console.WriteLine` 的调试输出（保留英文，便于和上游对照排查）
- 游戏名在数据层的英文标识

---

## 同步上游更新

本分支尽量不碰上游代码的逻辑，所以同步通常很干净：

```bash
git remote add upstream https://github.com/Escartem/MeiBrowser.git
git fetch upstream
git merge upstream/master        # 上游的默认分支是 master
```

可能出现冲突的只有文案相关的行。**冲突时以「把上游新增的英文字符串也加进两张表」为原则解，
而不是把 `T()` 调用换回硬编码。**

若上游新增了界面字符串，按这个顺序处理：

1. 在 `BuildEnglish()` 里加 key，值写上游的英文原文
2. 在 `BuildChinese()` 里加同名 key，值写中文
3. 在对应 XAML / C# 处把硬编码换成 `{loc:Loc Key}` 或 `T("Key")`

**漏了第 2 步不用怕** —— `T()` 会回退到英文，界面显示英文而不是裸 key，不会崩。
但 `Validate()` 会在下次启动时抛出并指明是哪个 key，帮你补上。

---

## 构建与运行

需要 **.NET SDK 10**（注意：只有 Runtime 是不够的，编译需要 SDK）。

```bash
winget install Microsoft.DotNet.SDK.10
```

```bash
git clone https://github.com/shota3389/MeiBrowser.git
cd MeiBrowser
dotnet build MeiBrowser.sln -c Release
```

产物：

```
Core/bin/Release/net10.0/Core.dll
GUI/bin/Release/net10.0-windows/MeiBrowser.dll
```

直接运行 `GUI/bin/Release/net10.0-windows/MeiBrowser.exe`。

> 编译时会出现约 107 条 `CS8600/CS8601/CS8602/CS8604/CS8619` 可空性警告，
> 集中在 `Core/Sophon.cs`、`Core/Dispatch.cs`、`Core/Meta.cs`。
> **这些是上游原有的，与本分支的本地化改动无关**，不影响生成。
> 本次改动已通过编译器验证：**0 个错误**。

可能需要留意的元数据（在 `GUI/GUI.csproj` 里）：

```xml
<Version>2.0.0</Version>
<FileVersion>2.0.0</FileVersion>
<Authors>Escartem</Authors>
<Copyright>Copyright (c) 2025-2026 Escartem</Copyright>
<AssemblyName>MeiBrowser</AssemblyName>   <!-- 产物名仍是 MeiBrowser.exe，未改 -->
```

**署名保持原作者 Escartem 不变** —— 本分支以最小侵入的方式维护，
不改动 `Authors` / `Copyright`，中文本地化的贡献记录在 README 与 NOTICE 中即可。

> `AssemblyName` 同样**不要改**。改了会让 `%LocalAppData%\MeiBrowser\settings.json`
> 这类路径与上游不一致，也会让用户在两个版本之间切换时丢配置。

### 发布单文件版

仓库里带了发布配置 `GUI/Properties/PublishProfiles/ReleaseWin64.pubxml`，
输出目录是 `..\dist\`（已被 `.gitignore` 忽略）：

```bash
dotnet publish GUI/GUI.csproj -c Release -p:PublishProfile=ReleaseWin64
```

产物会落在 `<仓库根>/dist/MeiBrowser.exe`（单文件、`win-x64`、非自包含，
所以目标机器需要装 .NET 10 Desktop Runtime）。

---

## 如何添加第三种语言

架构上支持任意多种语言，扩展步骤：

1. **`AppLanguage` 枚举**加成员，例如 `Japanese`
2. **照抄 `BuildChinese()`** 为 `BuildJapanese()`，把值翻译过去，**key 保持完全一致**
3. **`Localization` 的静态字段**加 `Japanese = BuildJapanese()`
4. **`T(key)` 里的表选择**改成 switch 或字典映射
5. **`Validate()`** 加进新表的校验（保证所有表的 key 集合一致）
6. **`DisplayName()`** 加一行返回该语言的自身写法，例如 `"日本語"`
7. **`MainWindow.Window_Loaded`** 的语言下拉框 `ItemsSource` 里加一个
   `new LanguageOption(AppLanguage.Japanese)`

`Validate()` 是防止翻译腐烂的关键闸门 —— 它会在启动时检查所有表的 key 集合是否一致，
不一致就直接抛并列出缺哪些 key。加语言时务必把它一起扩上。

---

## 已知限制

- **运行时切换语言会重建主窗口**。已打开的包标签页会自动重新加载，但重新加载需要联网，
  且标签页的滚动位置与展开状态不会保留。这是 `LocExtension` 构建期求值的固有代价。
- **下载进行中无法切换语言**（有意拦截，见上文）。
- 中文文案未经母语者逐条校对，措辞可能还有优化空间，欢迎提 issue。

---

## 授权与来源

本分支基于 [Escartem/MeiBrowser](https://github.com/Escartem/MeiBrowser)，
原始代码版权归原作者 **Escartem** 所有（见 `GUI/GUI.csproj` 中的
`<Copyright>Copyright (c) 2025-2026 Escartem</Copyright>`）。
本项目保留了原作者署名与项目链接，未改动 `Authors` / `Copyright` 字段。

> ⚠️ **许可证状态**：经核查，上游仓库 master 分支**没有 `LICENSE` 文件**
> （根目录仅含 `Core`、`GUI`、`.gitignore`、`MeiBrowser.sln`、`README.md`）。
> 缺少明确许可证意味着默认适用「保留所有权利」，严格来说并不自动构成开源授权。
>
> 因此，使用、分发或再发布本项目前，请注意：
> 1. **请先确认上游的授权状态**，或直接联系原作者征求同意
> 2. 保留本 README 与 `NOTICE.md` 中的来源标注，勿移除原作者署名
> 3. 若上游后续补充了许可证，本项目将遵循同一许可证

详细说明见 [`NOTICE.md`](NOTICE.md)。

---

## 关于 AI 的使用

**本分支的中文本地化工作，是在 AI 辅助下完成的**，特此公开说明，以免造成误解。

### 哪些部分用了 AI

| 工作内容 | 是否 AI 参与 |
|---|---|
| 梳理待翻译的界面文案、确定 key 命名与分层方案 | ✅ 是 |
| `GUI/Localization.cs` 中英文对照表（各 134 条）的初稿与中文措辞 | ✅ 是 |
| 18 个 XAML / C# 文件的改造与接线 | ✅ 是 |
| README、NOTICE 等文档的撰写 | ✅ 是 |
| 语言切换、窗口重建、标签页恢复等代码逻辑的设计 | ✅ 是 |
| 方案决策、取舍判断与最终验收 | 由维护者把关 |

### 哪些部分**没有**用 AI

- **上游的 MeiBrowser 全部原始代码** —— 一行未改，与本分支无关。
- 下载逻辑、Sophon / Dispatch 协议实现、校验算法 —— **完全未改动**。
- 本分支对界面文案以外的行为**没有任何改动**，与上游一致。

### 你应该知道的事

- **AI 生成的代码与译文均经过编译验证**（0 错误）与人工复核，但**不保证绝对无误**。
  若发现翻译错误、错别字或措辞不当，非常欢迎提 issue 或 PR 指正。
- 中文文案**未经母语者逐条校对**，尤其游戏内专有译名请以官方为准。
- 涉及到**授权与署名**的部分（原作者 `Authors` / `Copyright` 字段、来源标注）
  均**保持上游原样**，未做任何改动，详见上文「授权与来源」。

> 换句话说：本分支新增的是**界面的中文本地化层**，以及配套文档；
> 上游原有的一切功能与代码归属，都没有被 AI 触碰。

---

## 仓库与维护

### 仓库地址

本分支发布在 **<https://github.com/shota3389/MeiBrowser>**

上游原项目：<https://github.com/Escartem/MeiBrowser>

### 首次推送（已完成，留档备查）

```bash
cd MeiBrowser
git init
git add .
git commit -m "MeiBrowser with Simplified Chinese localization"
git branch -M main
git remote add origin https://github.com/shota3389/MeiBrowser.git
git push -u origin main
```

### 日常改动

```bash
git add .
git commit -m "描述这次改了什么"
git push
```

### 与上游同步

上游的默认分支是 `master`（本仓库用 `main`，互不影响）：

```bash
git remote add upstream https://github.com/Escartem/MeiBrowser.git   # 只需一次
git fetch upstream
git merge upstream/master
```

### 关于 `.gitignore`

仓库里的 `.gitignore` 已配置好，分两段：

1. **顶部「项目专属规则」**（本分支新增）—— 覆盖这个项目实际会产生、
   但通用 VS 模板没管到的文件：`dist/`、`meibrowser-error.log`、
   `meibrowser-startup.log`、`settings.json`、`*.bak`、根目录的 `_*.txt`
   临时诊断文件、`.vscode/`、`.idea/` 等。
2. **下面是上游标准的 VisualStudio 模板**（340 行）—— 覆盖 `[Bb]in/`、
   `[Oo]bj/`、`.vs/`、`*.user`、`*.log`、`[Dd]ebug/`、`[Rr]elease/` 等。

已实测：把 `bin`/`obj`/`dist`/日志/`settings.json`/`*.bak`/`*_wpftmp.csproj`
等 14 类垃圾文件造出来，`git status` 仍只显示 **58 个**待提交文件，无一条漏网。

> 注意 `.gitignore` 的规则**不能被后面的规则「取消」**（除了用 `!` 前缀显式反向包含）。
> 所以新增忽略项请加在文件顶部，和现有分组放在一起。

### GitHub 仓库设置建议

| 位置 | 建议 |
|---|---|
| 仓库名 | `MeiBrowser`（已确定） |
| Description | `All in one tool for browsing and downloading Hoyoverse games packages, with a Simplified Chinese interface.` |
| Topics | `wpf` `csharp` `dotnet` `localization` `i18n` `hoyoverse` `genshin-impact` |
| Default branch | `main` |
| `GUI/GUI.csproj` 的 `Authors` / `Copyright` | **保持 `Escartem` 原样**（见上文署名说明） |

### 发布 Release（可选）

若想提供开箱即用的二进制，用上面的 `ReleaseWin64` 配置发布后上传：

```bash
dotnet publish GUI/GUI.csproj -c Release -p:PublishProfile=ReleaseWin64
```

产物在 `dist/MeiBrowser.exe`（已被 `.gitignore` 忽略，不会误提交）。
Release 说明里请注明**依赖 .NET 10 Desktop Runtime**（该配置非自包含）。

---

## 这份副本的验收记录

整理时已做过以下验证，供你参考：

| 项目 | 结果 |
|---|---|
| 源码文件数 | 57 个（与上游应纳入版本控制的文件集完全一致） |
| 是否含 `.git` / `bin` / `obj` | 否，均已排除 |
| 独立编译 | `dotnet build MeiBrowser.sln` → **已成功生成，0 个错误** |
| 两语言表 key 一致性 | 各 134 个 key，**零漂移** |
| XAML + C# 引用的 key | 全部已定义，**无裸 key** |
| 运行期启动链路 | 已用探针验证 `OnStartup → Validate → Load → Initialize → 窗口 Loaded` 全流程无异常 |
| 与上游的同步状态 | fork 基线 `e65e3a7` 即上游 master 当前 tip（2026-09-09），无落后 |
| 上游许可证状态 | 已核查 GitHub：**无 LICENSE 文件**（详见 NOTICE） |
| `.gitignore` 有效性 | 造 14 类垃圾文件实测，`git status` 仍只显示 58 个待提交文件 |

---

## 贡献

欢迎通过 [Issues](https://github.com/shota3389/MeiBrowser/issues) 与
[Pull Requests](https://github.com/shota3389/MeiBrowser/pulls) 参与。优先欢迎：

- 修正中文文案的措辞（尤其游戏内的官方译名，请以官方为准）
- 补充缺失的翻译或新增语言（见上文「如何添加第三种语言」）
- 修复界面布局在中文字符下可能出现的截断/挤压
- 跟进上游的更新

如果是**上游本身的功能问题**（下载失败、协议变更等），
建议先到 [原项目](https://github.com/Escartem/MeiBrowser/issues) 反馈 —— 那是问题的根源所在。

---

## 致谢

- 原项目作者 [@Escartem](https://github.com/Escartem) —— 整个 MeiBrowser
- 上游的所有贡献者
- **本分支的中文本地化由 AI 辅助完成**（见[关于 AI 的使用](#关于-ai-的使用)）
