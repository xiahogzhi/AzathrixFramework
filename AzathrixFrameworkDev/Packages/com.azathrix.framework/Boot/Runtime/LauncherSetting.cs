using System.Collections.Generic;
using ParaCrossGames.Framework.Settings;
using UnityEngine;

namespace ParaCrossGames.Framework.Boot.Runtime
{
    /// <summary>
    /// 启动器配置
    /// </summary>
    [SettingsPath("LauncherSetting")]
    [ShowSetting("Launcher")]
    public class LauncherSetting : SettingsBase<LauncherSetting>
    {
        [Header("Bootstrap 扫描配置")]
        [Tooltip("是否启用 Bootstrap 系统")]
        public bool enableBootstrap = true;

        [Tooltip("要扫描的程序集前缀（为空则扫描所有）")]
        public List<string> scanAssemblyPrefixes = new();

        [Tooltip("排除的程序集前缀")]
        public List<string> excludeAssemblyPrefixes = new()
        {
            "System",
            "Microsoft",
            "Unity",
            "mscorlib",
            "netstandard",
            "Mono",
            "nunit"
        };

        [Header("启动配置")]
        [Tooltip("是否在启动时显示 Bootstrap 执行日志")]
        public bool showBootstrapLogs = true;
    }
}
