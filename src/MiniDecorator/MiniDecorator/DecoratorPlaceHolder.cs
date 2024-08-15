using System;

namespace MiniDecorator;

public static class DecoratorTemplateExpression
{
    public const string Name = "##Name##";
    public const string ReturnType = "##ReturnType##";
}

public static class ParameterListExpression
{
    public const string _ = "##ParameterList##";
}

public static class ArgumentPlaceHolder
{
    public const string _1 = "##Args:1##";
    public const string _2 = "##Args:2##";
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class DecorateWithAttribute(string template) : System.Attribute;

public partial class Sample
{
    public const string Template =$$"""
         public string Decoreated{{DecoratorTemplateExpression.Name}}({{ParameterListExpression._}}}
         {
         #if DEBUG
             System.Console.WriteLine($"Ok, DebugMode Decorator Call...");
         #endif
             
             {{DecoratorTemplateExpression.Name}}({{ParameterListExpression._}});
             return "Ok Invoked";
         }
         """;
    
    [DecorateWith(Sample.Template)]
    private void DoSomething()
    {
        Console.WriteLine($"Do Something");
    }
}