using System;
using System.Reflection;

namespace Delta.AOP
{
    /// <summary>
    /// 拦截上下文。
    /// </summary>
    public class InterceptContext
    {
        /// <summary>
        /// 构造函数。
        /// </summary>
        /// <param name="main">调用函数。</param>
        /// <param name="inputs">函数参数。</param>
        public InterceptContext(MethodInfo main, object[] inputs)
        {
            Main = main ?? throw new ArgumentNullException(nameof(main));
            Inputs = inputs ?? throw new ArgumentNullException(nameof(inputs));
        }

        /// <summary>
        /// 方法主体。
        /// </summary>
        public MethodInfo Main { get; }

        /// <summary>
        /// 输入参数。
        /// </summary>
        public object[] Inputs { get; }
    }
}
