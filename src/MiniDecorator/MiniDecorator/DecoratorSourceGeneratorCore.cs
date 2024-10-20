using System;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace MiniDecorator;

public static class DecoratorSourceGeneratorCore
{
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
                ExpressionSyntax expression = interpolation.Expression;
                MemberAccessExpressionSyntax memberAccessExpressionSyntax = (MemberAccessExpressionSyntax)expression; 
                SimpleNameSyntax name = memberAccessExpressionSyntax.Name;
                parsedTemplate.Append($"##{name.Identifier}##");
            }
        }

        return parsedTemplate.ToString();
    } 

    public static string GenerateCodeFromTemplate(ClassDeclarationSyntax classDeclarationSyntax, MemberDeclarationSyntax memberDeclarationSyntax, string template)
    {
        string className = classDeclarationSyntax.Identifier.Text;
        string memberName = memberDeclarationSyntax switch
        {
            MethodDeclarationSyntax method => method.Identifier.ToString(),
            PropertyDeclarationSyntax property => property.Identifier.ToString(),
            _ => throw new NotSupportedException(),
        };
        string typeName = memberDeclarationSyntax switch
        {
            MethodDeclarationSyntax method => method.ReturnType.ToString(),
            PropertyDeclarationSyntax property => property.Type.ToString(),
            _ => throw new NotSupportedException(),
        };
        string parameterListWithType = memberDeclarationSyntax switch
        {
            MethodDeclarationSyntax method => method.ParameterList.Parameters.ToString(),
            PropertyDeclarationSyntax _ => string.Empty,
            _ => throw new NotSupportedException(),
        };
        string parameterList = memberDeclarationSyntax switch
        {
            MethodDeclarationSyntax method => string.Join(", ", method.ParameterList.Parameters.Select(p => p.Identifier.ToString())),
            PropertyDeclarationSyntax _ => string.Empty,
            _ => throw new NotSupportedException(),
        };

        return new StringBuilder(template)
            .Replace(DecoratorTemplate.MethodName, memberName)
            .Replace(DecoratorTemplate.ReturnType, typeName)
            .Replace(DecoratorTemplate.ParameterListWithType, parameterListWithType)
            .Replace(DecoratorTemplate.ParameterList, parameterList)
            .ToString();
    } 
}