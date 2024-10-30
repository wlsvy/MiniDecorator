using System;
using Xunit;

namespace MiniDecorator.Tests;

public sealed class DecorateWithTryCatchAttribute() : DecorateBaseAttribute(template: $$"""
                  public {{DecoratorTemplate.ReturnType}} {{DecoratorTemplate.MethodName}}WithTryCatch({{DecoratorTemplate.ParameterListWithType}})
                  {
                      try
                      {
                          System.Console.WriteLine($"Before Invoke");
                          return this.{{DecoratorTemplate.MethodName}}({{DecoratorTemplate.ParameterList}});
                      }
                      catch (System.Exception e)
                      {
                          System.Console.WriteLine($"Error: {e}");
                           return default; 
                      }
                  }                    
                  """);

public sealed class MultiplyTwoAttribute() : DecorateBaseAttribute(template: $$"""
                  public {{DecoratorTemplate.ReturnType}} {{DecoratorTemplate.MethodName}}AndMultiplyByTwo({{DecoratorTemplate.ParameterListWithType}})
                  {
                      return this.{{DecoratorTemplate.MethodName}}({{DecoratorTemplate.ParameterList}}) * 2;
                  }                    
                  """);

public partial class SimpleGeneratorTest
{
    [Fact]
    public void DecoratorCompileTest()
    {
        this.DoSomethingWithTryCatch();
    }

    [Fact]
    public void DecoratorCompileTest2()
    {
        long baseResult = this.DoSomething(1, 999, 'a');
        long decoratedMethodResult = this.DoSomethingAndMultiplyByTwo(1, 999, 'a');
        Assert.Equal(baseResult * 2, decoratedMethodResult);
    }

    [DecorateWithTryCatch]
    public int DoSomething()
    {
        return 1 + 1;
    }

    [MultiplyTwo]
    public long DoSomething(int value1, long value2, char value3)
    {
        return value1 + value2 + (int)value3;
    }
}
