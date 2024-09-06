using System;
// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming
#pragma warning disable CS9113 // Parameter is unread.

namespace MiniDecorator;

public static class DecoratorTemplate
{
    public const string MethodName = "##MethodOrClassName##";
    public const string ClassName = "##MethodOrClassName##";
    public const string ReturnType = "##ReturnType##";
    public const string ParameterList = "##ParameterList##";
    public const string ParameterList_Skip1 = "##ParameterList_Skip1##";
    public const string Argument_1 = "##Args_11##";
    public const string Argument_2 = "##Args_2##";
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class DecorateWithAttribute(string template) : Attribute;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Method)]
public sealed class AutoNotifyAttribute : Attribute;

public partial class Sample
{
    public const string Template =$$"""
         public string Decoreated{{DecoratorTemplate.MethodName}}({{DecoratorTemplate.ParameterList}}}
         {
         #if DEBUG
             System.Console.WriteLine($"Ok, DebugMode Decorator Call...");
         #endif
             try
             {
                 System.Console.WriteLine($"Before Call");
                 {{DecoratorTemplate.MethodName}}({{DecoratorTemplate.ParameterList}});
                 return "Ok, Decoration Completed"
             }
             catch (Exception e)
             {
                  return $"Exception Occured: {e}";
             }
         }
         """;
    
    [DecorateWith(Sample.Template)]
    private void DoSomething()
    {
        Console.WriteLine($"Do Something");
    }
}