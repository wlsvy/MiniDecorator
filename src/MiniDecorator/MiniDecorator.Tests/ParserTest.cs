using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace MiniDecorator.Tests;

public sealed class ParserTest
{
   [Fact]
   public void Parse()
   {
      string code = @"
public sealed class TestDecoratorAttribute : DecorateBaseAttribute(template: $$""""""
  public {{DecoratorTemplate.ReturnType}}{{DecoratorTemplate.MethodName}}WithTryCatch({{DecoratorTemplate.ParameterListWithType}})
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
";
      SyntaxTree tree = CSharpSyntaxTree.ParseText(code)!;
      SyntaxNode root = tree.GetRoot()!;
      ClassDeclarationSyntax classDeclarationSyntax = root.DescendantNodes()
          .OfType<ClassDeclarationSyntax>()
          .Single();
      
      Assert.True(MiniDecorator.DecoratorSourceGenerator.TryGetDecoratorAttribute(classDeclarationSyntax, out PrimaryConstructorBaseTypeSyntax? decoratorConstructor));
      string parsedTemplate = MiniDecorator.DecoratorSourceGenerator.ParseTemplate(decoratorConstructor!);

   }
}