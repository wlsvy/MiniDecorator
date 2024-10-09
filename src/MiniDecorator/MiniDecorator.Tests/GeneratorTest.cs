namespace MiniDecorator.Tests;

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
