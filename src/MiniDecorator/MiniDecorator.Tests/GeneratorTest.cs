
using System;

namespace MiniDecorator.Tests;

public static class DecoratorTemplate
{
    public const string MethodName = "##MethodOrClassName##";
    public const string ClassName = "##MethodOrClassName##";
    public const string ReturnType = "##ReturnType##";
    public const string ParameterListWithType = "##ParameterListWithType##";
    public const string ParameterList = "##ParameterList##";
    public const string ParameterList_Skip1 = "##ParameterList_Skip1##";
    public const string Argument_1 = "##Args_11##";
    public const string Argument_2 = "##Args_2##";
}

public abstract class DecorateBaseAttribute(string template) : Attribute;

public sealed class DecorateWithTryCatchAttribute() :
    DecorateBaseAttribute(template: $$"""
                  public {{DecoratorTemplate.ReturnType}} {{DecoratorTemplate.MethodName}}WithTryCatch({{DecoratorTemplate.ParameterListWithType}})
                  {
                      try
                      {
                          System.Console.WriteLine($"Before Invoke");
                          return this.{{DecoratorTemplate.MethodName}}({{DecoratorTemplate.ParameterList}});
                      }
                      catch (Exception e)
                      {
                          System.Console.WriteLine($"Error: {e}");
                           return default; 
                      }
                  }                    
                  """);

public partial class GeneratorTest
{
    [DecorateWithTryCatch]
    public int DoSomething()
    {
        return 1 + 1;
    }
}
