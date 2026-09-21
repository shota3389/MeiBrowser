# NOTICE

## 本仓库

- **本仓库地址**：https://github.com/shota3389/MeiBrowser
- **性质**：上游项目的中文本地化分支（fork）

## 本项目来源

本仓库是以下项目的分支（fork）：

- **原名**：MeiBrowser
- **原作者**：Escartem
- **上游地址**：https://github.com/Escartem/MeiBrowser
- **分支时基于的上游提交**：`e65e3a7`（Merge pull request #15 from jokelbaf/master）

## 本分支做了什么

在**不改动任何下载逻辑、协议实现与校验算法**的前提下，
为图形界面增加了**简体中文 / English 双语支持与运行时切换**。

改动范围：**18 个文件修改 + 1 个文件新增**（`GUI/Localization.cs`）。

所有涉及界面文字的字符串都被抽取到 `GUI/Localization.cs` 的两张表中，
通过 XAML 标记扩展 `{loc:Loc Key}` 与 C# 的 `Localization.T("Key")` 取值。

## 关于 AI 的使用

**本分支的中文本地化工作是在 AI 辅助下完成的。**

- AI 参与的范围：界面文案的翻译与整理、`Localization.cs` 对照表、
  18 个 XAML / C# 文件的改造接线，以及 README / NOTICE 文档的撰写。
- AI **未参与**的部分：上游 MeiBrowser 的全部原始代码 —— 一行未改。
  下载逻辑、Sophon / Dispatch 协议、校验算法**完全保持上游原样**。
- 产出均经过编译验证（0 个错误）与维护者复核，但译文**未经母语者逐条校对**，
  不保证绝对无误；发现问题欢迎提 issue 或 PR。

## 版权

- 原始代码版权归 **Escartem** 所有（见 `GUI/GUI.csproj` 中的
  `<Copyright>Copyright (c) 2025-2026 Escartem</Copyright>`）
- 本地化部分的工作由本分支维护者完成（其中译文与代码为 AI 辅助产出，见上文）

## 关于许可证

**上游仓库中没有 `LICENSE` 文件**（已直接核查 GitHub 上
[Escartem/MeiBrowser](https://github.com/Escartem/MeiBrowser) 的 master 分支，
根目录仅含 `Core`、`GUI`、`.gitignore`、`MeiBrowser.sln`、`README.md`）。

这意味着在法律上默认适用「保留所有权利」，严格来说并不自动构成开源授权。

如果你是原作者并希望为本项目添加许可证，请直接在仓库根目录添加 `LICENSE` 文件。

如果你是本分支的使用者，请注意：

1. 在使用、分发或再发布前，请先确认上游的授权状态
2. 保留本 `NOTICE` 文件与 `README.md` 中的来源标注，不要移除原作者署名
3. 若上游后续补充了许可证，本分支应遵循同一许可证

## 第三方依赖

本项目使用以下 NuGet 包（各自的许可证见其项目主页）：

| 包 | 版本 | 用途 |
|---|---|---|
| `DarkNet` | 2.3.0 | Windows 标题栏深色主题 |
| `Dirkster.AvalonDock` | 5.0.0 | 可停靠的标签页布局 |
| `Dirkster.AvalonDock.Themes.VS2013` | 5.0.0 | 上述布局的 VS2013 主题 |
