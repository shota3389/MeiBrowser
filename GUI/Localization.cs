using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Markup;

namespace GUI
{
    /// <summary>
    /// The languages the interface can be shown in.
    /// The enum member names are the persisted values, so do not rename them.
    /// </summary>
    public enum AppLanguage
    {
        English,
        Chinese
    }

    /// <summary>
    /// Runtime language switching for the whole interface.
    ///
    /// Two ways to get a translated string:
    ///   1. From XAML, once at load:  Text="{loc:Loc Login_Title}"
    ///      These are captured when the control is built, so a change of language
    ///      only reaches them after the window is rebuilt. <see cref="Apply"/> does
    ///      exactly that for the whole window, keeping the current tab layout.
    ///   2. Anything refreshed while running (dialog boxes, status text, tab titles)
    ///      should call <see cref="T"/> at the moment it is displayed. Those always
    ///      follow the current language without a restart.
    /// </summary>
    public static class Localization
    {
        private static readonly Dictionary<string, string> English = BuildEnglish();
        private static readonly Dictionary<string, string> Chinese = BuildChinese();

        private static AppLanguage current = AppLanguage.English;

        /// <summary>The language everything is displayed in.</summary>
        public static AppLanguage Current => current;

        public static bool IsChinese => current == AppLanguage.Chinese;

        /// <summary>Captions for the language picker, shown in their own language.</summary>
        public static string DisplayName(AppLanguage language) => language switch
        {
            AppLanguage.Chinese => "简体中文",
            _ => "English"
        };

        /// <summary>
        /// Looks up <paramref name="key"/> in the active language. Keys are always written
        /// in English, so a missing translation falls back to the English text and shows
        /// up as English in the interface instead of throwing.
        /// </summary>
        public static string T(string key)
        {
            var table = current == AppLanguage.Chinese ? Chinese : English;
            if (table.TryGetValue(key, out var text)) return text;
            if (English.TryGetValue(key, out var fallback)) return fallback;
            return key;
        }

        /// <summary>Same as <see cref="T"/> but with {0}, {1} ... placeholders filled in.</summary>
        public static string T(string key, params object?[] args)
        {
            string format = T(key);
            try { return string.Format(CultureInfo.CurrentCulture, format, args); }
            catch (FormatException) { return format; }
        }

        /// <summary>
        /// Sets the language before any window exists. Use <see cref="Apply"/> once the
        /// interface is up, which also rebuilds it.
        /// </summary>
        public static void Initialize(AppLanguage language)
        {
            current = language;
            ApplyToCulture();
        }

        /// <summary>
        /// Switches to <paramref name="language"/> and rebuilds <paramref name="window"/> so the
        /// captions captured while its XAML was parsed pick up the change. The window is hidden
        /// for the swap; the rebuild is expected to put a replacement on screen.
        /// </summary>
        public static void Apply(Window? window, AppLanguage language)
        {
            if (window is not IRelocalizable relocalizable) return;
            if (!window.IsLoaded) return;
            if (language == current) return;

            current = language;
            ApplyToCulture();

            window.Hide();
            relocalizable.Relocalize();
        }

        private static void ApplyToCulture()
        {
            // Keeps date and number parsing consistent with the interface language
            var culture = current == AppLanguage.Chinese
                ? CultureInfo.GetCultureInfo("zh-CN")
                : CultureInfo.GetCultureInfo("en-US");

            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        /// <summary>
        /// Fails the build when a key is missing from a translation table or the two
        /// tables drift apart, which is the usual way translations rot.
        /// </summary>
        internal static void Validate()
        {
            var missing = new List<string>();

            foreach (var key in English.Keys.Where(k => !Chinese.ContainsKey(k)))
                missing.Add($"Chinese is missing the key '{key}'");

            foreach (var key in Chinese.Keys.Where(k => !English.ContainsKey(k)))
                missing.Add($"Chinese has an unknown key '{key}' (no English original)");

            if (missing.Count > 0)
                throw new InvalidOperationException("Localisation tables are inconsistent:\n" + string.Join("\n", missing));
        }

        private static Dictionary<string, string> BuildEnglish()
        {
            var t = new Dictionary<string, string>(StringComparer.Ordinal);

            t["Language"] = "Language";
            t["Language_Busy_Title"] = "Download in progress";
            t["Language_Busy_Body"] =
                "The interface language cannot be changed while a download is running, because the download tab " +
                "would have to be rebuilt. Let it finish or cancel it first.";
            t["Theme"] = "Theme";

            t["App_Tagline"] = "MeiBrowser - All in one tool for browsing and downloading Hoyoverse games packages";
            t["App_UnhandledError"] = "Something went wrong.\n\n{0}\n\nDetails were written to:\n{1}";
            t["App_UnhandledError_Title"] = "Unexpected error";
            t["Tab_Setup"] = "Setup";
            t["Tab_Console"] = "Console";
            t["Tab_Download"] = "Download";
            t["Tab_DownloadDone"] = "Download - done";
            t["Tab_DownloadErrors"] = "Download - errors";

            t["Common_Loading"] = "Loading...";
            t["Common_Searching"] = "Searching...";
            t["Common_Error"] = "Error";
            t["Common_Done"] = "Done";
            t["Common_Cancelled"] = "Cancelled";
            t["Common_Idle"] = "idle";
            t["Common_Cancel"] = "Cancel";
            t["Common_Close"] = "Close";

            t["Setup_Mode"] = "Mode:";
            t["Setup_Infos"] = "Infos";
            t["Setup_Game"] = "Game:";
            t["Setup_Region"] = "Region:";
            t["Setup_Url"] = "URL:";
            t["Setup_Check"] = "Check";
            t["Setup_Version"] = "Version:";
            t["Setup_Package"] = "Package:";
            t["Setup_DiffMode"] = "Diff Mode:";
            t["Setup_DiffModeContent"] = "Only show new/changed files from previous version";
            t["Setup_CustomBuild"] = "Custom Build";
            t["Setup_Confirm"] = "Confirm";

            t["Mode_Sophon"] = "Sophon";
            t["Mode_ScatteredFiles"] = "Scattered Files";
            t["Game_Genshin"] = "Genshin Impact";
            t["Game_StarRail"] = "Honkai: Star Rail";
            t["Game_ZZZ"] = "Zenless Zone Zero";
            t["Game_CustomSophon"] = "Custom Sophon URL";
            t["Region_OS"] = "OS";
            t["Region_CN"] = "CN";

            t["Setup_ModeInfo_Title"] = "Mode Information";
            t["Setup_ModeInfo_Body"] =
                "Sophon mode is the new method to download files, it is better & faster.\n\n" +
                "Scattered files is the old method, while older it provides content such as full game zip, " +
                "update zip, and files from versions earlier than when sophon was available, consider it the legacy mode.";

            t["Setup_ErrVersionsForGame"] = "Could not load the version list for this game.";
            t["Setup_ErrVersionsForServer"] = "Could not load the versions for this server.";
            t["Setup_ErrPackagesForVersion"] = "Could not load the packages for this version.";
            t["Setup_ErrSophonBuild"] = "Failed to fetch sophon build from the provided URL. Make sure it is a /getBuild URL and try again.";

            t["Download_Heading"] = "Downloading your files";
            t["Download_HeadingFor"] = "Downloading {0}";
            t["Download_Network"] = "Network";
            t["Download_Disk"] = "Disk";
            t["Download_Preparing"] = "Preparing...";
            t["Download_Cancel"] = "Cancel";
            t["Download_Cancelling"] = "Cancelling...";
            t["Download_Starting"] = "Starting...";
            t["Download_Progress"] = "{0}%  ({1} / {2})  {3}  ETA {4}";
            t["Download_FileCount"] = "{0}/{1} files";
            t["Download_FileCountFailed"] = "  -  {0} failed";
            t["Download_Failed"] = "Download failed";
            t["Download_FailedBody"] = "The download could not be started.\n\n{0}";
            t["Download_Complete"] = "Download complete";
            t["Download_AllVerified"] = "All {0} file(s) downloaded and verified.";
            t["Download_AllVerifiedWithSkipped"] = "All {0} file(s) downloaded and verified.\n{1} were already present.";
            t["Download_WithErrors"] = "Finished with errors";
            t["Download_WithErrorsSummary"] = "{0} completed, {1} failed. See the console tab.";
            t["Download_WithErrorsBody"] =
                "{0} file(s) completed, {1} failed:\n\n{2}\n\n" +
                "Run the download again with the same folder to retry only what is missing.";
            t["Download_WithErrorsMore"] = "\n...and {0} more (see the console tab).";
            t["Download_CancelledBody"] = "Download cancelled. Finished files were kept, so restarting will resume where it stopped.";
            t["Download_ConfirmCancel_Title"] = "Cancel download";
            t["Download_ConfirmCancel_Body"] =
                "Stop the download?\n\nFiles that already finished are kept, so downloading to the same folder " +
                "later picks up where this left off.";
            t["Download_Quit_Title"] = "Download in progress";
            t["Download_Quit_Body"] = "A download is still running. Stop it and quit?";
            t["Download_Busy_Title"] = "One at a time";
            t["Download_Busy_Body"] = "A download is already running. Wait for it to finish, or cancel it from the Download tab.";
            t["Download_Confirm_Title"] = "Continue?";
            t["Download_Confirm_Body"] = "You are about to download {0} file(s), {1}, continue ?";

            t["Files_DownloadSelected"] = "Download selected";
            t["Files_SearchHint"] = "Search files, then press Enter or Search...";
            t["Files_Regex"] = "Regex";
            t["Files_RegexTooltip"] = "Treat the search text as a .NET regular expression";
            t["Files_Filter"] = "Filter";
            t["Files_FilterActive"] = "Filter *";
            t["Files_Search"] = "Search";
            t["Files_Summary"] = "{0} - {1}  -  {2}  -  version {3}";
            t["Files_SummaryChanges"] = "  -  changes since {0}";
            t["Files_SummaryVersionSophon"] = "{0}.0";
            t["Files_ErrNoFiles"] = "No files found in this package.";
            t["Files_ErrLoad"] = "Could not load this package.\n\n{0}";
            t["Files_ErrRegex"] = "That is not a valid regular expression.\n\n{0}";
            t["Files_ErrRegex_Title"] = "Invalid pattern";
            t["Files_ErrSearch"] = "The search could not be completed.\n\n{0}";
            t["Files_ErrSearch_Title"] = "Search failed";
            t["Files_ErrNothingSelected"] = "Select at least one file or folder first.";
            t["Files_ErrNothingSelected_Title"] = "Nothing selected";
            t["Files_ErrNothingToDownload"] = "The selection contains no downloadable files.";
            t["Files_ErrNothingToDownload_Title"] = "Nothing to download";
            t["Files_File"] = "File";
            t["Files_Folder"] = "Folder";
            t["Files_ElementCount"] = "{0:# ##0} files";

            t["Filter_Title"] = "Search filter";
            t["Filter_ShowOnly"] = "Show only";
            t["Filter_All"] = "Files and folders";
            t["Filter_FilesOnly"] = "Files only";
            t["Filter_FoldersOnly"] = "Folders only";
            t["Filter_Size"] = "Size";
            t["Filter_AtLeast"] = "at least";
            t["Filter_AtMost"] = "at most";
            t["Filter_Hint"] = "Leave a size box empty for no limit. Folder sizes count everything inside them.";
            t["Filter_Clear"] = "Clear";
            t["Filter_Ok"] = "OK";
            t["Filter_ErrNotANumber"] = "'{0}' is not a number, so the '{1}' size cannot be used.";
            t["Filter_ErrNegative"] = "The '{1}' size cannot be negative.";
            t["Filter_ErrRange"] = "The 'at least' size is bigger than the 'at most' size, so nothing could match.";
            t["Filter_CheckSizes"] = "Check the sizes";
            t["Filter_DescribeNone"] = "No filter set";
            t["Filter_DescribeFilesOnly"] = "files only";
            t["Filter_DescribeFoldersOnly"] = "folders only";
            t["Filter_DescribeBetween"] = "between {0} and {1}";
            t["Filter_DescribeMin"] = "at least {0}";
            t["Filter_DescribeMax"] = "at most {0}";

            t["Row_Checking"] = "checking";
            t["Row_Retrying"] = "retrying";
            t["Row_RetryingAttempt"] = "  (try {0}/{1})";
            t["Row_Failed"] = "failed";

            t["Console_Clear"] = "Clear";
            t["Console_CopyAll"] = "Copy all";
            t["Console_AutoScroll"] = "Auto-scroll";

            t["Dialog_Title"] = "Message";
            t["Dialog_Ok"] = "OK";
            t["Dialog_Cancel"] = "Cancel";
            t["Dialog_Yes"] = "Yes";
            t["Dialog_No"] = "No";

            t["Time_HoursMinutes"] = "{0}h {1:D2}m";
            t["Time_MinutesSeconds"] = "{0}m {1:D2}s";
            t["Time_Seconds"] = "{0}s";

            return t;
        }

        private static Dictionary<string, string> BuildChinese()
        {
            var t = new Dictionary<string, string>(StringComparer.Ordinal);

            t["Language"] = "语言";
            t["Language_Busy_Title"] = "下载进行中";
            t["Language_Busy_Body"] = "下载运行期间无法切换界面语言，因为「下载」标签页需要被重建。请等下载结束或先取消它。";
            t["Theme"] = "主题";

            t["App_Tagline"] = "MeiBrowser - 浏览并下载米哈游游戏资源包的一站式工具";
            t["App_UnhandledError"] = "出了点问题。\n\n{0}\n\n详细信息已写入：\n{1}";
            t["App_UnhandledError_Title"] = "意外错误";
            t["Tab_Setup"] = "设置";
            t["Tab_Console"] = "控制台";
            t["Tab_Download"] = "下载";
            t["Tab_DownloadDone"] = "下载 - 已完成";
            t["Tab_DownloadErrors"] = "下载 - 有错误";

            t["Common_Loading"] = "加载中...";
            t["Common_Searching"] = "搜索中...";
            t["Common_Error"] = "错误";
            t["Common_Done"] = "完成";
            t["Common_Cancelled"] = "已取消";
            t["Common_Idle"] = "空闲";
            t["Common_Cancel"] = "取消";
            t["Common_Close"] = "关闭";

            t["Setup_Mode"] = "模式：";
            t["Setup_Infos"] = "说明";
            t["Setup_Game"] = "游戏：";
            t["Setup_Region"] = "区服：";
            t["Setup_Url"] = "链接：";
            t["Setup_Check"] = "检测";
            t["Setup_Version"] = "版本：";
            t["Setup_Package"] = "资源包：";
            t["Setup_DiffMode"] = "差异模式：";
            t["Setup_DiffModeContent"] = "只显示相比上一版本新增或变更的文件";
            t["Setup_CustomBuild"] = "自定义构建";
            t["Setup_Confirm"] = "确定";

            t["Mode_Sophon"] = "Sophon";
            t["Mode_ScatteredFiles"] = "散文件";
            t["Game_Genshin"] = "原神";
            t["Game_StarRail"] = "崩坏：星穹铁道";
            t["Game_ZZZ"] = "绝区零";
            t["Game_CustomSophon"] = "自定义 Sophon 链接";
            t["Region_OS"] = "国际服";
            t["Region_CN"] = "国服";

            t["Setup_ModeInfo_Title"] = "模式说明";
            t["Setup_ModeInfo_Body"] =
                "Sophon 是新的下载方式，效果更好、速度更快。\n\n" +
                "散文件是旧方式，虽然比较老旧，但能提供完整游戏压缩包、更新压缩包，以及 Sophon 出现之前" +
                "各版本的文件，可以把它当作兼容模式。";

            t["Setup_ErrVersionsForGame"] = "无法加载该游戏的版本列表。";
            t["Setup_ErrVersionsForServer"] = "无法加载该区服的版本列表。";
            t["Setup_ErrPackagesForVersion"] = "无法加载该版本的资源包列表。";
            t["Setup_ErrSophonBuild"] = "无法从所提供的链接获取 Sophon 构建信息。请确认这是一个 /getBuild 链接后重试。";

            t["Download_Heading"] = "正在下载你的文件";
            t["Download_HeadingFor"] = "正在下载 {0}";
            t["Download_Network"] = "网络";
            t["Download_Disk"] = "磁盘";
            t["Download_Preparing"] = "准备中...";
            t["Download_Cancel"] = "取消";
            t["Download_Cancelling"] = "正在取消...";
            t["Download_Starting"] = "正在启动...";
            t["Download_Progress"] = "{0}%  （{1} / {2}）  {3}  剩余 {4}";
            t["Download_FileCount"] = "{0}/{1} 个文件";
            t["Download_FileCountFailed"] = "  -  {0} 个失败";
            t["Download_Failed"] = "下载失败";
            t["Download_FailedBody"] = "无法启动下载。\n\n{0}";
            t["Download_Complete"] = "下载完成";
            t["Download_AllVerified"] = "{0} 个文件已全部下载并校验通过。";
            t["Download_AllVerifiedWithSkipped"] = "{0} 个文件已全部下载并校验通过。\n其中 {1} 个本地已存在。";
            t["Download_WithErrors"] = "完成，但有错误";
            t["Download_WithErrorsSummary"] = "{0} 个完成，{1} 个失败。详情见控制台标签页。";
            t["Download_WithErrorsBody"] =
                "{0} 个文件完成，{1} 个失败：\n\n{2}\n\n" +
                "用同一个文件夹再下载一次，即可只重试缺失的部分。";
            t["Download_WithErrorsMore"] = "\n...还有 {0} 个（详情见控制台标签页）。";
            t["Download_CancelledBody"] = "下载已取消。已完成的文件会保留，下次重新开始会从断点继续。";
            t["Download_ConfirmCancel_Title"] = "取消下载";
            t["Download_ConfirmCancel_Body"] =
                "要停止下载吗？\n\n已经下载完成的文件会保留，之后下载到同一个文件夹时会从中断处继续。";
            t["Download_Quit_Title"] = "下载进行中";
            t["Download_Quit_Body"] = "仍有下载正在进行，要停止并退出吗？";
            t["Download_Busy_Title"] = "只能同时进行一个";
            t["Download_Busy_Body"] = "已经有一个下载在运行了。请等它结束，或到「下载」标签页取消它。";
            t["Download_Confirm_Title"] = "继续？";
            t["Download_Confirm_Body"] = "即将下载 {0} 个文件，共 {1}，是否继续？";

            t["Files_DownloadSelected"] = "下载所选";
            t["Files_SearchHint"] = "搜索文件，然后按回车或点「搜索」...";
            t["Files_Regex"] = "正则";
            t["Files_RegexTooltip"] = "把搜索内容当作 .NET 正则表达式处理";
            t["Files_Filter"] = "筛选";
            t["Files_FilterActive"] = "筛选 *";
            t["Files_Search"] = "搜索";
            t["Files_Summary"] = "{0} - {1}  -  {2}  -  版本 {3}";
            t["Files_SummaryChanges"] = "  -  相比 {0} 的变更";
            t["Files_SummaryVersionSophon"] = "{0}.0";
            t["Files_ErrNoFiles"] = "该资源包中没有找到任何文件。";
            t["Files_ErrLoad"] = "无法加载该资源包。\n\n{0}";
            t["Files_ErrRegex"] = "这不是一个有效的正则表达式。\n\n{0}";
            t["Files_ErrRegex_Title"] = "表达式无效";
            t["Files_ErrSearch"] = "无法完成搜索。\n\n{0}";
            t["Files_ErrSearch_Title"] = "搜索失败";
            t["Files_ErrNothingSelected"] = "请先选择至少一个文件或文件夹。";
            t["Files_ErrNothingSelected_Title"] = "未选择任何内容";
            t["Files_ErrNothingToDownload"] = "所选内容中没有可下载的文件。";
            t["Files_ErrNothingToDownload_Title"] = "无可下载内容";
            t["Files_File"] = "文件";
            t["Files_Folder"] = "文件夹";
            t["Files_ElementCount"] = "{0:# ##0} 个文件";

            t["Filter_Title"] = "搜索筛选";
            t["Filter_ShowOnly"] = "只显示";
            t["Filter_All"] = "文件和文件夹";
            t["Filter_FilesOnly"] = "仅文件";
            t["Filter_FoldersOnly"] = "仅文件夹";
            t["Filter_Size"] = "大小";
            t["Filter_AtLeast"] = "不小于";
            t["Filter_AtMost"] = "不大于";
            t["Filter_Hint"] = "大小留空表示不限。文件夹的大小会统计其内部所有内容。";
            t["Filter_Clear"] = "清空";
            t["Filter_Ok"] = "确定";
            t["Filter_ErrNotANumber"] = "「{0}」不是数字，因此无法使用「{1}」这一大小条件。";
            t["Filter_ErrNegative"] = "「{1}」的大小不能为负数。";
            t["Filter_ErrRange"] = "「不小于」的大小比「不大于」还大，这样匹配不到任何内容。";
            t["Filter_CheckSizes"] = "请检查大小";
            t["Filter_DescribeNone"] = "未设置筛选";
            t["Filter_DescribeFilesOnly"] = "仅文件";
            t["Filter_DescribeFoldersOnly"] = "仅文件夹";
            t["Filter_DescribeBetween"] = "{0} 到 {1} 之间";
            t["Filter_DescribeMin"] = "不小于 {0}";
            t["Filter_DescribeMax"] = "不大于 {0}";

            t["Row_Checking"] = "校验中";
            t["Row_Retrying"] = "重试中";
            t["Row_RetryingAttempt"] = "  （第 {0}/{1} 次）";
            t["Row_Failed"] = "失败";

            t["Console_Clear"] = "清空";
            t["Console_CopyAll"] = "复制全部";
            t["Console_AutoScroll"] = "自动滚动";

            t["Dialog_Title"] = "提示";
            t["Dialog_Ok"] = "确定";
            t["Dialog_Cancel"] = "取消";
            t["Dialog_Yes"] = "是";
            t["Dialog_No"] = "否";

            t["Time_HoursMinutes"] = "{0} 小时 {1:D2} 分";
            t["Time_MinutesSeconds"] = "{0} 分 {1:D2} 秒";
            t["Time_Seconds"] = "{0} 秒";

            return t;
        }
    }

    /// <summary>
    /// Implemented by windows that can rebuild themselves in place when the language
    /// changes, instead of being thrown away and recreated.
    /// </summary>
    public interface IRelocalizable
    {
        void Relocalize();
    }

    /// <summary>
    /// XAML markup extension: Text="{loc:Loc Key=Setup_Mode}".
    /// The value is resolved once, while the element is being built.
    /// </summary>
    [MarkupExtensionReturnType(typeof(string))]
    public sealed class LocExtension : MarkupExtension
    {
        public LocExtension() { }

        public LocExtension(string key) => Key = key;

        [ConstructorArgument("key")]
        public string Key { get; set; } = "";

        public override object ProvideValue(IServiceProvider serviceProvider) => Localization.T(Key);
    }

    /// <summary>A language paired with the caption to show for it, for the picker in the toolbar.</summary>
    public sealed class LanguageOption
    {
        public LanguageOption(AppLanguage language)
        {
            Language = language;
            Name = Localization.DisplayName(language);
        }

        public AppLanguage Language { get; }

        /// <summary>The language written in itself, so it reads correctly whatever is selected.</summary>
        public string Name { get; }
    }
}
