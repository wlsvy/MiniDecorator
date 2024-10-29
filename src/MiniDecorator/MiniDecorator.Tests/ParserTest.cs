using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace MiniDecorator.Tests;

public sealed class ParserTest
{
    private static readonly string decoratorTemplate = """
     public ##ReturnType## ##MethodName##WithTryCatch(##ParameterListWithType##)
     {
         try
         {
             System.Console.WriteLine($"Before Invoke");
             return this.##MethodName##(##ParameterList##);
         }
         catch (Exception e)
         {
             System.Console.WriteLine($"Error: {e}");
             return default;
         }
     }
     """;

    [Fact]
    public void Parse()
    {
        string decoratorAttributeCode = @"
public sealed class TestDecoratorAttribute : [DECO](template: $$""""""
  public {{DecoratorTemplate.ReturnType}} {{DecoratorTemplate.MethodName}}WithTryCatch({{DecoratorTemplate.ParameterListWithType}})
  {
      try
      {
          System.Console.WriteLine($""Before Invoke"");
          return this.{{DecoratorTemplate.MethodName}}({{DecoratorTemplate.ParameterList}});
      }
      catch (Exception e)
      {
          System.Console.WriteLine($""Error: {e}"");
          return default;
      }
  }
  """""";
".Replace("[DECO]", nameof(DecorateBaseAttribute));

        SyntaxTree tree = CSharpSyntaxTree.ParseText(decoratorAttributeCode)!;
        SyntaxNode root = tree.GetRoot()!;
        ClassDeclarationSyntax classDeclarationSyntax = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Single();

        Assert.True(DecoratorSourceGeneratorCore.TryGetDecoratorAttribute(classDeclarationSyntax, out PrimaryConstructorBaseTypeSyntax? decoratorConstructor));
        string parsedTemplate = DecoratorSourceGeneratorCore.ParseTemplate(decoratorConstructor!);
        Assert.Equal(decoratorTemplate, parsedTemplate);
    }

    [Fact]
    public void ClassParse()
    {
        string classCode = $$"""
           using System;

           namespace Test;

           public partial sealed class Foo
           {
               public void DoSomething(int a, int b)
               {
                   Console.WriteLine($"DoSomething {a} {b}");
               }
           }
           """;

        string expectedGeneratedMethod = $$"""
         public void DoSomethingWithTryCatch(int a, int b)
         {
             try
             {
                 System.Console.WriteLine($"Before Invoke");
                 return this.DoSomething(a, b);
             }
             catch (Exception e)
             {
                 System.Console.WriteLine($"Error: {e}");
                 return default;
             }
         }
         """;

        SyntaxTree tree = CSharpSyntaxTree.ParseText(classCode)!;
        SyntaxNode root = tree.GetRoot()!;
        ClassDeclarationSyntax classDeclarationSyntax = root.DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .Single();
        MethodDeclarationSyntax methodDeclarationSyntax = classDeclarationSyntax.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single();

        string generatedMethodCode = DecoratorSourceGeneratorCore.GenerateCodeFromTemplate(methodDeclarationSyntax, decoratorTemplate);
        Assert.Equal(expectedGeneratedMethod, generatedMethodCode);
    }
}