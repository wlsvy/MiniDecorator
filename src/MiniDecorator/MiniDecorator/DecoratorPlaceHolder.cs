using System;
// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
#pragma warning disable CS9113 // Parameter is unread.

namespace MiniDecorator;

public static class DecoratorTemplate
{
    public const string MethodName = $"##{nameof(MethodName)}##";
    public const string ClassName = $"##{nameof(ClassName)}##";
    public const string ReturnType = $"##{nameof(ReturnType)}##";
    public const string ParameterList = $"##{nameof(ParameterList)}##";
    public const string ParameterListWithType = $"##{nameof(ParameterListWithType)}##";
    public const string ParameterList_Skip1 = $"##{nameof(ParameterList_Skip1)}##";
    public const string Argument_1 = $"##{nameof(Argument_1)}##";
    public const string Argument_2 = $"##{nameof(Argument_2)}##";
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
public abstract class DecorateBaseAttribute(string template) : Attribute;