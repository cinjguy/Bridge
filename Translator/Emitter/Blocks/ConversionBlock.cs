﻿using Bridge.Contract;
using ICSharpCode.NRefactory.CSharp;
using ICSharpCode.NRefactory.Semantics;
using System.Collections.Generic;
using System.Linq;
using ICSharpCode.NRefactory.TypeSystem;

namespace Bridge.Translator
{
    public abstract class ConversionBlock : AbstractEmitterBlock
    {
        public ConversionBlock(IEmitter emitter, AstNode node) : base(emitter, node)
        {

        }

        protected sealed override void DoEmit()
        {
            var expression = this.GetExpression();

            if (expressionInWork.Contains(expression))
            {
                this.EmitConversionExpression();
                return;
            }

            expressionInWork.Add(expression);

            var isConversion = false;
            bool check = expression != null && !expression.IsNull && expression.Parent != null;

            if (check)
            {
                isConversion = this.CheckConversion(expression);
            }

            if (this.DisableEmitConversionExpression)
            {
                expressionInWork.Remove(expression);
                return;
            }

            this.EmitConversionExpression();
            expressionInWork.Remove(expression);

            if (isConversion)
            {
                this.WriteCloseParentheses();
            }
        }

        private static List<Expression> expressionInWork = new List<Expression>();

        protected virtual bool DisableEmitConversionExpression
        {
            get;
            set;
        }

        protected virtual bool CheckConversion(Expression expression)
        {
            return ConversionBlock.CheckConversion(this, expression);
        }

        public static bool IsUserDefinedConversion(AbstractEmitterBlock block, Expression expression)
        {
            Conversion conversion = null;

            try
            {
                var rr = block.Emitter.Resolver.ResolveNode(expression, null);
                conversion = block.Emitter.Resolver.Resolver.GetConversion(expression);

                if (conversion == null)
                {
                    return false;
                }

                return conversion.IsUserDefined;
            }
            catch
            {
            }

            return false;
        }

        public static bool CheckConversion(ConversionBlock block, Expression expression)
        {
            Conversion conversion = null;
            try
            {
                var rr = block.Emitter.Resolver.ResolveNode(expression, block.Emitter);
                conversion = block.Emitter.Resolver.Resolver.GetConversion(expression);
                var expectedType = block.Emitter.Resolver.Resolver.GetExpectedType(expression);

                var invocationExpression = expression.Parent as InvocationExpression;
                if (invocationExpression != null && invocationExpression.Arguments.Any(a => a == expression))
                {
                    var index = invocationExpression.Arguments.ToList().IndexOf(expression);
                    var methodResolveResult = block.Emitter.Resolver.ResolveNode(invocationExpression, block.Emitter) as MemberResolveResult;

                    var m = methodResolveResult.Member as IMethod;
                    var arg = m.Parameters[index < m.Parameters.Count ? index : (m.Parameters.Count - 1)];

                    if (Helpers.IsDecimalType(arg.Type, block.Emitter.Resolver) && !Helpers.IsDecimalType(rr.Type, block.Emitter.Resolver))
                    {
                        block.Write("Bridge.Decimal");
                        if (NullableType.IsNullable(arg.Type))
                        {
                            block.Write(".lift");
                        }
                        block.WriteOpenParentheses();
                        return true;
                    }
                }

                var namedArgExpression = expression.Parent as NamedArgumentExpression;
                if (namedArgExpression != null)
                {
                    var namedArgResolveResult = block.Emitter.Resolver.ResolveNode(namedArgExpression, block.Emitter) as NamedArgumentResolveResult;

                    if (Helpers.IsDecimalType(namedArgResolveResult.Type, block.Emitter.Resolver) && !Helpers.IsDecimalType(rr.Type, block.Emitter.Resolver))
                    {
                        block.Write("Bridge.Decimal");
                        if (NullableType.IsNullable(namedArgResolveResult.Type))
                        {
                            block.Write(".lift");
                        }
                        block.WriteOpenParentheses();
                        return true;
                    }
                }

                var namedExpression = expression.Parent as NamedExpression;
                if (namedExpression != null)
                {
                    var namedResolveResult = block.Emitter.Resolver.ResolveNode(namedExpression, block.Emitter);

                    if (Helpers.IsDecimalType(namedResolveResult.Type, block.Emitter.Resolver) && !Helpers.IsDecimalType(rr.Type, block.Emitter.Resolver))
                    {
                        block.Write("Bridge.Decimal");
                        if (NullableType.IsNullable(namedResolveResult.Type))
                        {
                            block.Write(".lift");
                        }
                        block.WriteOpenParentheses();
                        return true;
                    }
                }

                var arrayInit = expression.Parent as ArrayInitializerExpression;
                if (arrayInit != null)
                {
                    while (arrayInit.Parent is ArrayInitializerExpression)
                    {
                        arrayInit = (ArrayInitializerExpression)arrayInit.Parent;
                    }

                    IType elementType = null;
                    var arrayCreate = arrayInit.Parent as ArrayCreateExpression;
                    if (arrayCreate != null)
                    {
                        var rrArrayType = block.Emitter.Resolver.ResolveNode(arrayCreate.Type, block.Emitter);
                        elementType = rrArrayType.Type;
                    }
                    else
                    {
                        var rrElemenet = block.Emitter.Resolver.ResolveNode(arrayInit.Parent, block.Emitter);
                        var pt = rrElemenet.Type as ParameterizedType;
                        if (pt != null && pt.TypeArguments.Count > 0)
                        {
                            elementType = pt.TypeArguments.First();
                        }
                    }

                    if (Helpers.IsDecimalType(elementType, block.Emitter.Resolver) && !Helpers.IsDecimalType(rr.Type, block.Emitter.Resolver))
                    {
                        block.Write("Bridge.Decimal");
                        if (NullableType.IsNullable(elementType))
                        {
                            block.Write(".lift");
                        }
                        block.WriteOpenParentheses();
                        return true;
                    }
                }

                if (Helpers.IsDecimalType(expectedType, block.Emitter.Resolver) && !Helpers.IsDecimalType(rr.Type, block.Emitter.Resolver))
                {
                    block.Write("Bridge.Decimal");
                    if (NullableType.IsNullable(expectedType))
                    {
                        block.Write(".lift");
                    }
                    block.WriteOpenParentheses();
                    return true;
                }

                if (Helpers.IsDecimalType(expectedType, block.Emitter.Resolver))
                {
                    return false;
                }

                if (conversion == null)
                {
                    return false;
                }

                if (conversion.IsIdentityConversion)
                {
                    return false;
                }

                var isNumLifted = conversion.IsImplicit && conversion.IsLifted && conversion.IsNumericConversion && !(expression is BinaryOperatorExpression);
                if (isNumLifted && !conversion.IsUserDefined)
                {
                    return false;
                }
                bool isLifted = conversion.IsLifted && !isNumLifted && !(block is CastBlock);
                if (isLifted)
                {
                    block.Write("Bridge.Nullable.lift(");
                }

                if (conversion.IsUserDefined)
                {
                    var method = conversion.Method;

                    string inline = block.Emitter.GetInline(method);

                    if (conversion.IsExplicit && !string.IsNullOrWhiteSpace(inline))
                    {
                        // Still returns true if Nullable.lift( was written.
                        return isLifted;
                    }

                    if (!string.IsNullOrWhiteSpace(inline))
                    {
                        if (expression is InvocationExpression)
                        {
                            new InlineArgumentsBlock(block.Emitter, new ArgumentsInfo(block.Emitter, (InvocationExpression)expression), inline).Emit();
                        }
                        else if (expression is ObjectCreateExpression)
                        {
                            new InlineArgumentsBlock(block.Emitter, new ArgumentsInfo(block.Emitter, (InvocationExpression)expression), inline).Emit();
                        }
                        else if (expression is UnaryOperatorExpression)
                        {
                            var unaryExpression = (UnaryOperatorExpression)expression;
                            var resolveOperator = block.Emitter.Resolver.ResolveNode(unaryExpression, block.Emitter);
                            OperatorResolveResult orr = resolveOperator as OperatorResolveResult;
                            new InlineArgumentsBlock(block.Emitter, new ArgumentsInfo(block.Emitter, unaryExpression, orr, method), inline).Emit();
                        }
                        else if (expression is BinaryOperatorExpression)
                        {
                            var binaryExpression = (BinaryOperatorExpression)expression;
                            var resolveOperator = block.Emitter.Resolver.ResolveNode(binaryExpression, block.Emitter);
                            OperatorResolveResult orr = resolveOperator as OperatorResolveResult;
                            new InlineArgumentsBlock(block.Emitter, new ArgumentsInfo(block.Emitter, binaryExpression, orr, method), inline).Emit();
                        }
                        else
                        {
                            new InlineArgumentsBlock(block.Emitter, new ArgumentsInfo(block.Emitter, expression), inline).Emit();
                        }

                        block.DisableEmitConversionExpression = true;

                        // Still returns true if Nullable.lift( was written.
                        return isLifted;
                    }
                    else
                    {
                        if (method.DeclaringTypeDefinition != null && block.Emitter.Validator.IsIgnoreType(method.DeclaringTypeDefinition))
                        {
                            // Still returns true if Nullable.lift( was written.
                            return isLifted;
                        }

                        block.Write(BridgeTypes.ToJsName(method.DeclaringType, block.Emitter));
                        block.WriteDot();

                        block.Write(OverloadsCollection.Create(block.Emitter, method).GetOverloadName());
                    }

                    if (isLifted)
                    {
                        block.WriteComma();
                    }
                    else
                    {
                        block.WriteOpenParentheses();
                    }

                    return true;
                }
                // Still returns true if Nullable.lift( was written.
                return isLifted;
            }
            catch
            {
            }

            return false;
        }

        protected abstract void EmitConversionExpression();
        protected abstract Expression GetExpression();
    }
}
