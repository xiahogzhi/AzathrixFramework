using System;

namespace Azathrix.Framework.Core.Pipelines
{
    /// <summary>
    /// 阶段执行顺序特性
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class PhaseOrderAttribute : Attribute
    {
        public int Order { get; }
        public PhaseOrderAttribute(int order) => Order = order;
    }
}
