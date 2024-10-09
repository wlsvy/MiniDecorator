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
      
      Assert.True(TryGetDecoratorAttribute(classDeclarationSyntax, out PrimaryConstructorBaseTypeSyntax? decoratorConstructor));
      string parsedTemplate = ParseTemplate(decoratorConstructor!);

   }
   
    public static bool TryGetDecoratorAttribute(ClassDeclarationSyntax classDeclarationSyntax, out PrimaryConstructorBaseTypeSyntax? decoratorConstructor)
    {
        if (classDeclarationSyntax.BaseList is null)
        {
            decoratorConstructor = null;
            return false;
        }
        
        SeparatedSyntaxList<BaseTypeSyntax> baseListTypes = classDeclarationSyntax.BaseList!.Types;
        BaseTypeSyntax? decoratorBaseAttributeClass = baseListTypes.SingleOrDefault(t => t.Type.ToString() == nameof(DecorateBaseAttribute));
        PrimaryConstructorBaseTypeSyntax? primaryConstructorBaseTypeSyntax = decoratorBaseAttributeClass as PrimaryConstructorBaseTypeSyntax;
        if (primaryConstructorBaseTypeSyntax is null)
        {
            decoratorConstructor = null;
            return false;
        }

        decoratorConstructor = primaryConstructorBaseTypeSyntax;
        return true;
    }

    public static string ParseTemplate(PrimaryConstructorBaseTypeSyntax decoratorDeclaration)
    {
        InterpolatedStringExpressionSyntax templateExpression = (InterpolatedStringExpressionSyntax)decoratorDeclaration.ArgumentList.Arguments[0].Expression;
        StringBuilder parsedTemplate = new ();
        foreach (InterpolatedStringContentSyntax content in templateExpression.Contents)
        {
            if (content is InterpolatedStringTextSyntax text)
            {
                // InterpolatedStringText는 그대로 사용
                parsedTemplate.Append(text.TextToken.ValueText);
            }
            else if (content is InterpolationSyntax interpolation)
            {
                // Interpolation 구간은 치환
                parsedTemplate.Append($"##{interpolation.Expression}##");
            }
        }

        return parsedTemplate.ToString();
    } 
}