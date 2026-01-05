namespace Azathrix.MiniPanda.Core
{
    /// <summary>
    /// 环境变量提供者接口，用于动态获取变量
    /// </summary>
    public interface IEnvironmentProvider
    {
        /// <summary>获取变量值</summary>
        Value Get(string name);

        /// <summary>检查变量是否存在</summary>
        bool Contains(string name);
    }
}
