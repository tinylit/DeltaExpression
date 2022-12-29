﻿using Delta.Expressions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Delta.Emitters
{
    /// <summary>
    /// 构造函数。
    /// </summary>
    public class ConstructorEmitter : BlockExpression
    {
        private int parameterIndex = 0;
        private readonly ReturnExpression returnAst;
        private readonly List<ParameterEmitter> parameters = new List<ParameterEmitter>();
        private readonly AbstractTypeEmitter typeEmitter;

        private class ConstructorExpression : Expression
        {
            private readonly ConstructorInfo constructor;
            private readonly Expression[] parameters;

            public ConstructorExpression(ConstructorInfo constructor) : base(constructor.DeclaringType)
            {
                this.constructor = constructor ?? throw new ArgumentNullException(nameof(constructor));
            }
            public ConstructorExpression(ConstructorInfo constructor, Expression[] parameters) : this(constructor)
            {
                ArgumentsCheck(constructor, parameters);

                this.parameters = parameters;
            }

            private static void ArgumentsCheck(ConstructorInfo constructorInfo, Expression[] arguments)
            {
                var parameterInfos = constructorInfo.GetParameters();

                if (arguments?.Length != parameterInfos.Length)
                {
                    throw new AstException("指定参数和构造函数参数个数不匹配!");
                }

                if (!parameterInfos.Zip(arguments, (x, y) =>
                {
                    return x.ParameterType == y.RuntimeType || x.ParameterType.IsAssignableFrom(y.RuntimeType);

                }).All(x => x))
                {
                    throw new AstException("指定参数和构造函数参数类型不匹配!");
                }
            }

            public override void Load(ILGenerator ilg)
            {
                ilg.Emit(OpCodes.Ldarg_0);

                if (parameters?.Length > 0)
                {
                    foreach (var expression in parameters)
                    {
                        expression.Load(ilg);
                    }
                }

                ilg.Emit(OpCodes.Call, constructor);
            }
        }

        /// <summary>
        /// 构造函数。
        /// </summary>
        /// <param name="typeEmitter">父类型。</param>
        /// <param name="attributes">属性。</param>
        public ConstructorEmitter(AbstractTypeEmitter typeEmitter, MethodAttributes attributes) : this(typeEmitter, attributes, CallingConventions.Standard)
        {
        }

        /// <summary>
        /// 构造函数。
        /// </summary>
        /// <param name="typeEmitter">父类型。</param>
        /// <param name="attributes">属性。</param>
        /// <param name="conventions">调用约定。</param>
        public ConstructorEmitter(AbstractTypeEmitter typeEmitter, MethodAttributes attributes, CallingConventions conventions) : base(typeof(void))
        {
            this.typeEmitter = typeEmitter;
            Attributes = attributes;
            Conventions = conventions;

            returnAst = new ReturnExpression(typeof(void));
        }

        /// <summary>
        /// 方法的名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 方法的属性。
        /// </summary>
        public MethodAttributes Attributes { get; }

        /// <summary>
        /// 调用约定。
        /// </summary>
        public CallingConventions Conventions { get; }

        /// <summary>
        /// 参数。
        /// </summary>
        public ParameterEmitter[] Parameters => parameters.ToArray();

        /// <summary>
        /// 声明参数。
        /// </summary>
        /// <param name="parameterInfo">参数。</param>
        /// <returns></returns>
        public ParameterEmitter DefineParameter(ParameterInfo parameterInfo)
        {
            var parameter = DefineParameter(parameterInfo.ParameterType, parameterInfo.Attributes, parameterInfo.Name);

            if (parameterInfo.HasDefaultValue)
            {
                parameter.SetConstant(parameterInfo.DefaultValue);
            }

            foreach (var customAttribute in parameterInfo.CustomAttributes)
            {
                parameter.SetCustomAttribute(customAttribute);
            }

            return parameter;
        }

        /// <summary>
        /// 声明参数。
        /// </summary>
        /// <param name="parameterType">参数类型。</param>
        /// <param name="attributes">属性。</param>
        /// <param name="strParamName">名称。</param>
        /// <returns></returns>
        public ParameterEmitter DefineParameter(Type parameterType, ParameterAttributes attributes, string strParamName)
        {
            var parameter = new ParameterEmitter(parameterType, ++parameterIndex, attributes, strParamName);

            parameters.Add(parameter);

            return parameter;
        }

        /// <summary>
        /// 跳转到当前代码块末尾。
        /// </summary>
        public ReturnExpression Return => returnAst;

        /// <summary>
        /// 调用父类构造函数。
        /// </summary>
        public void InvokeBaseConstructor()
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            var type = typeEmitter.BaseType;

            if (type.IsGenericParameter)
            {
                type = type.GetGenericTypeDefinition();
            }

            InvokeBaseConstructor(type.GetConstructor(flags, null, Type.EmptyTypes, null));
        }

        /// <summary>
        /// 调用父类构造函数。
        /// </summary>
        /// <param name="constructor">构造函数。</param>
        public void InvokeBaseConstructor(ConstructorInfo constructor) => Append(new ConstructorExpression(constructor));

        /// <summary>
        /// 调用父类构造函数。
        /// </summary>
        /// <param name="constructor">构造函数。</param>
        /// <param name="parameters">参数。</param>
        public void InvokeBaseConstructor(ConstructorInfo constructor, params Expression[] parameters) => Append(new ConstructorExpression(constructor, parameters));

        /// <summary>
        /// 发行。
        /// </summary>
        public void Emit(ConstructorBuilder builder)
        {
            var attributes = builder.MethodImplementationFlags;

            if ((attributes & MethodImplAttributes.Runtime) != MethodImplAttributes.IL)
            {
                return;
            }

            if (IsEmpty)
            {
                InvokeBaseConstructor();
            }

            foreach (var parameter in parameters)
            {
                parameter.Emit(builder.DefineParameter(parameter.Position, parameter.Attributes, parameter.ParameterName));
            }

            var ilg = builder.GetILGenerator();

            base.Load(ilg);

            ilg.Emit(OpCodes.Ret);
        }

        /// <summary>
        /// 发行。
        /// </summary>
        /// <param name="ilg">指令。</param>
        public override void Load(ILGenerator ilg)
        {
            var label = ilg.DefineLabel();

            returnAst.Emit(label);

            base.Load(ilg);

            ilg.MarkLabel(label);

            ilg.Emit(OpCodes.Nop);
        }
    }
}
