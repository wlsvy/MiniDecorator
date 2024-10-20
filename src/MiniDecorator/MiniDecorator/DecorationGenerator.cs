#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;
using System.Diagnostics;
using System.Linq;
using Humanizer;
using Microsoft.CodeAnalysis.CSharp;

namespace MiniDecorator;

public abstract class DecoratorBaseAttribute(string template) : Attribute;

[Generator(LanguageNames.CSharp)]
public class DecoratorSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var decoratorAttributeTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsDecoratorAttributeWithPrimaryConstructor(s),
                transform: static (ctx, _) => GetDecoratorTemplate(ctx))
            .Collect();

        var methodsWithDecorator = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsMethodWithAttribute(s),
                transform: static (ctx, _) => GetMethodWithDecorator(ctx))
            .Where(static m => m != default)
            .Collect();

        context.RegisterSourceOutput(
            decoratorAttributeTypes.Combine(methodsWithDecorator),
            (spc, source) => Execute(source.Left, source.Right, spc));
    }

    private static bool IsDecoratorAttributeWithPrimaryConstructor(SyntaxNode node)
    {
        if (node is not ClassDeclarationSyntax { BaseList: not null })
        {
            return false;
        }

        SeparatedSyntaxList<BaseTypeSyntax> baseListTypes = ((ClassDeclarationSyntax)node).BaseList!.Types;
        BaseTypeSyntax? decoratorBaseAttributeClass = baseListTypes.SingleOrDefault(t => t.Type.ToString() == nameof(DecorateBaseAttribute));
        return decoratorBaseAttributeClass is PrimaryConstructorBaseTypeSyntax;
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
                ExpressionSyntax expression = interpolation.Expression;
                MemberAccessExpressionSyntax memberAccessExpressionSyntax = (MemberAccessExpressionSyntax)expression; 
                SimpleNameSyntax name = memberAccessExpressionSyntax.Name;
                parsedTemplate.Append($"##{name.Identifier}##");
            }
        }

        return parsedTemplate.ToString();
    } 
    
    private static (string Name, string Template) GetDecoratorTemplate(GeneratorSyntaxContext context)
    {
        ClassDeclarationSyntax attributeClassDeclaration = (ClassDeclarationSyntax)context.Node;
        string className = attributeClassDeclaration.Identifier.ToString();
        BaseTypeSyntax decoratorBaseAttributeClass = attributeClassDeclaration.BaseList!.Types.Single(t => t.Type.ToString() == nameof(DecorateBaseAttribute));
        PrimaryConstructorBaseTypeSyntax primaryConstructorBaseTypeSyntax = (PrimaryConstructorBaseTypeSyntax)decoratorBaseAttributeClass;
        return (className, ParseTemplate(primaryConstructorBaseTypeSyntax));
    }

    // Helper method to check if the syntax node is a method with at least one attribute
    private static bool IsMethodWithAttribute(SyntaxNode node)
    {
        return node is MethodDeclarationSyntax mds && mds.AttributeLists.Count > 0;
    }

    public static void GetMethodWithAttr(ClassDeclarationSyntax classDeclarationSyntax)
    {
        MemberDeclarationSyntax[] memberDeclarationSyntaxes = classDeclarationSyntax.DescendantNodes().OfType<MemberDeclarationSyntax>().ToArray();
        
        string className = classDeclarationSyntax.Identifier.Text;
    }

    public static string GetMemberTT(ClassDeclarationSyntax classDeclarationSyntax, MemberDeclarationSyntax memberDeclarationSyntax, string template)
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
            PropertyDeclarationSyntax property => string.Empty,
            _ => throw new NotSupportedException(),
        };
        string parameterList = memberDeclarationSyntax switch
        {
            MethodDeclarationSyntax method => string.Join(", ", method.ParameterList.Parameters.Select(p => p.Identifier.ToString())),
            PropertyDeclarationSyntax property => string.Empty,
            _ => throw new NotSupportedException(),
        };

        return new StringBuilder(template)
            .Replace(DecoratorTemplate.MethodName, memberName)
            .Replace(DecoratorTemplate.ReturnType, typeName)
            .Replace(DecoratorTemplate.ParameterListWithType, parameterListWithType)
            .Replace(DecoratorTemplate.ParameterList, parameterList)
            .ToString();
    }

    private static (IMethodSymbol MethodSymbol, INamedTypeSymbol AttributeSymbol) GetMethodWithDecorator(GeneratorSyntaxContext context)
    {
        var methodDeclaration = (MethodDeclarationSyntax)context.Node;
        var methodSymbol = context.SemanticModel.GetDeclaredSymbol(methodDeclaration) as IMethodSymbol;

        if (methodSymbol == null)
            return (null, null);

        foreach (var attributeData in methodSymbol.GetAttributes())
        {
            var attrClass = attributeData.AttributeClass;
            if (attrClass == null)
                continue;

            // Check if the attribute inherits from DecorateBaseAttribute
            var baseType = attrClass.BaseType;
            while (baseType != null)
            {
                if (baseType.Name == "DecorateBaseAttribute")
                    return (methodSymbol, attrClass);
                baseType = baseType.BaseType;
            }
        }

        return (null, null);
    }
    
    private void Execute(
        ImmutableArray<(string Name, string Template)> decoratorAttributes,
        ImmutableArray<(IMethodSymbol MethodSymbol, INamedTypeSymbol AttributeSymbol)> methods,
        SourceProductionContext context)
    {
        if (decoratorAttributes.IsDefaultOrEmpty)
        {
            throw new Exception("Attr is Empty");
        }

        if (methods.IsDefaultOrEmpty)
        {
            throw new Exception("method is Empty");
        }

        // Map decorator attribute names to their template strings
        Dictionary<string, string> decoratorTemplates = new ();

        // StringBuilder to accumulate the generated methods
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using System;");
        sb.AppendLine();
        sb.AppendLine("namespace GeneratedDecorators");
        sb.AppendLine("{");

        foreach (var (methodSymbol, attributeSymbol) in methods)
        {
            if (!decoratorTemplates.TryGetValue(attributeSymbol.Name, out var template))
                continue;

            // Extract method details
            var methodName = methodSymbol.Name;
            var returnType = methodSymbol.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            var parametersWithType = string.Join(", ", methodSymbol.Parameters.Select(p => $"{p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {p.Name}"));
            var parameterList = string.Join(", ", methodSymbol.Parameters.Select(p => p.Name));

            // Replace placeholders in the template
            var processedMethod = template
                .Replace("{{DecoratorTemplate.MethodName}}", methodName)
                .Replace("{{DecoratorTemplate.ReturnType}}", returnType)
                .Replace("{{DecoratorTemplate.ParameterListWithType}}", parametersWithType)
                .Replace("{{DecoratorTemplate.ParameterList}}", parameterList);

            sb.AppendLine(processedMethod);
            sb.AppendLine();
        }

        sb.AppendLine("}"); // End of namespace

        throw new Exception($"""
                             rr
                             {sb}
                              
                             """);
        // Add the generated source
        context.AddSource("GeneratedDecorators.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }
}


public record struct TypeOfExpr(
    string TypeName,
    string[] ElementTypeNames,
    SyntaxKind SyntaxType);


public static class Util
{
    /// <summary>
    /// 애트리뷰트 타입 이름에 대해서는 'Attribute' 접미사를 붙인 버전과 뗀 버전에 대해서 각각 비교할 것
    /// </summary>
    /// <param name="attributeSyntax"> 분석한 코드의 Attribute syntax. 이때 'Attribute' 접미사를 떼고 지정했을 수 있다.</param>
    /// <param name="attributeTypeName">비교 대상 AttributeName</param>
    private static bool IsAttributeNameEqual(this AttributeSyntax attributeSyntax, ReadOnlySpan<char> attributeTypeName)
    {
        var declaredName = attributeSyntax.Name.ToString().AsSpan();
        if (declaredName.EndsWith("Attribute".AsSpan()) &&
            declaredName.SequenceEqual(attributeTypeName))
        {
            return true;
        }

        int postfixLength = attributeTypeName.Length - "Attribute".Length;
        if (declaredName.SequenceEqual(attributeTypeName.Slice(start: 0, length: postfixLength)))
        {
            return true;
        }

        return false;
    }

    private static string GetNameOfExpressionOrThrow(this AttributeArgumentSyntax attributeArgumentSyntax, string filePath)
    {
        if (attributeArgumentSyntax.Expression is InvocationExpressionSyntax invocation &&
            invocation.Expression is IdentifierNameSyntax identifier &&
            identifier.Identifier.ValueText == "nameof" &&          //nameof 연산자를 반드시 적용해야 함
            invocation.ArgumentList.Arguments.Count == 1)
        {
            return invocation.ArgumentList.Arguments[0].Expression.GetText().ToString();
        }

        throw new NotSupportedException($$"""
                                             배열 형식을 허용하지 않고, Namespace 이름을 작성할 때 반드시 nameof(...) 안에 작성해주세요.
                                             
                                              file: [{{filePath}}]
                                              argumentText: [{{attributeArgumentSyntax.ToFullString()}}]
                                          """);
    }

    private static TypeOfExpr GetTypeOfExpressionOrThrow(this AttributeArgumentSyntax attributeArgumentSyntax, string filePath)
    {
        if (attributeArgumentSyntax.Expression is TypeOfExpressionSyntax typeOfExpr)
        {
            string typeName = typeOfExpr.Type.GetText().ToString();

            switch (typeOfExpr.Type)
            {
                case PredefinedTypeSyntax:
                    return new(
                        TypeName: typeName,
                        ElementTypeNames: [],
                        SyntaxType: SyntaxKind.PredefinedType);

                case IdentifierNameSyntax:
                    return new(
                        TypeName: typeName,
                        ElementTypeNames: [],
                        SyntaxType: SyntaxKind.IdentifierName);

                case NullableTypeSyntax nullable:
                    return new(
                        TypeName: typeName,
                        ElementTypeNames: [nullable.ElementType.GetText().ToString()],
                        SyntaxType: SyntaxKind.NullableType);

                case GenericNameSyntax generic: //내부 제네릭 파라미터를 볼 때, nested Generic 까지 전부 펼쳐보진 않는다.
                    return new(
                        TypeName: typeName,
                        ElementTypeNames: generic.TypeArgumentList.Arguments.Select(x => x.GetText().ToString()).ToArray(),
                        SyntaxType: SyntaxKind.GenericName);

                case TupleTypeSyntax tuple:
                    return new(
                        TypeName: typeName,
                        ElementTypeNames: tuple.Elements.Select(x => x.Type.GetText().ToString()).ToArray(),
                        SyntaxType: SyntaxKind.TupleType);

                case ArrayTypeSyntax array:
                    return new(
                        TypeName: typeName,
                        ElementTypeNames: [array.ElementType.GetText().ToString()],
                        SyntaxType: SyntaxKind.ArrayType);
            }
        }

        throw new NotSupportedException($$"""
                                          {{nameof(GetTypeOfExpressionOrThrow)}}: 지원하지 않는 표현식입니다. 
                                             
                                              file: [{{filePath}}]
                                              argumentText: [{{attributeArgumentSyntax.ToFullString()}}]
                                          """);
    }


}
