using System;

namespace Azathrix.Framework.Settings
{
    /// <summary>
    /// 标记设置类在 Settings 窗口中显示
    /// 使用此特性的 SettingsBase 子类将自动出现在 ParaCross Games/Preference 菜单中
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ShowSettingAttribute : Attribute
    {
        /// <summary>
        /// 在设置窗口中显示的名称
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 创建 Setting 特性
        /// </summary>
        /// <param name="displayName">显示名称，为空则使用类名</param>
        public ShowSettingAttribute(string displayName = null)
        {
            DisplayName = displayName;
        }
    }
}
