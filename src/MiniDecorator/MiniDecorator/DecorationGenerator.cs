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

namespace MiniDecorator;

public abstract class DecoratorBaseAttribute(string template) : Attribute;

[Generator(LanguageNames.CSharp)]
public class DecoratorSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Step 1: Find all classes that inherit from DecorateBaseAttribute
        var decoratorAttributeTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsDecoratorAttributeWithPrimaryConstructor(s),
                transform: static (ctx, _) => GetDecoratorTemplate(ctx))
            .Where(static m => m != null)
            .Collect();

        // Step 2: Find all methods decorated with any of the decorator attributes
        var methodsWithDecorator = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsMethodWithAttribute(s),
                transform: static (ctx, _) => GetMethodWithDecorator(ctx))
            .Where(static m => m != default)
            .Collect();

        // Combine the collected decorator attributes and methods
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
    
    private static INamedTypeSymbol GetDecoratorTemplate(GeneratorSyntaxContext context)
    {
        ClassDeclarationSyntax attributeClassDeclaration = (ClassDeclarationSyntax)context.Node;
        string attributeClassName = attributeClassDeclaration.Identifier.ToString();
        
        BaseTypeSyntax decoratorBaseAttributeClass = attributeClassDeclaration.BaseList!.Types.Single(t => t.Type.ToString() == nameof(DecorateBaseAttribute));
        PrimaryConstructorBaseTypeSyntax primaryConstructorBaseTypeSyntax = (PrimaryConstructorBaseTypeSyntax)decoratorBaseAttributeClass;
        InterpolatedStringExpressionSyntax templateExpression = (InterpolatedStringExpressionSyntax)primaryConstructorBaseTypeSyntax.ArgumentList.Arguments[0].Expression;
        
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

        throw new Exception($"""
                             Complete?? : {attributeClassName} 
                             {parsedTemplate}
                             ---
                             {templateExpression.Contents.ToString()}
                             """);
        
        // Look for classes that inherit from DecorateBaseAttribute
        foreach (BaseTypeSyntax baseType in attributeClassDeclaration.BaseList!.Types)
        {
            if (baseType.Type.ToString() == nameof(DecorateBaseAttribute))
            {
                throw new Exception($"Find deco {attributeClassDeclaration.Identifier.ToString()}");
            }
            INamedTypeSymbol? typeSymbol = context.SemanticModel.GetTypeInfo(baseType.Type).Type as INamedTypeSymbol;
            if (typeSymbol == null)
            {
                throw new ArgumentException("Test");
            }

            if (typeSymbol.ToDisplayString() == nameof(DecorateBaseAttribute))
            {
                return context.SemanticModel.GetDeclaredSymbol(attributeClassDeclaration) as INamedTypeSymbol;
            }
            else
            {
                // throw new Exception(
                //     $"{nameof(GetSemanticTargetForDecoratorAttribute)} {typeSymbol.ToDisplayString()} is not deco {classDeclaration.Identifier.ToString()}");
            }
        }

        //throw new Exception( $"{nameof(GetSemanticTargetForDecoratorAttribute)} deco attr not found {classDeclaration.Identifier.ToString()}");
        return null;
    }

    // Helper method to check if the syntax node is a method with at least one attribute
    private static bool IsMethodWithAttribute(SyntaxNode node)
    {
        return node is MethodDeclarationSyntax mds && mds.AttributeLists.Count > 0;
    }

    // Transform method to get the method symbol and its decorator attribute symbol
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
        ImmutableArray<INamedTypeSymbol> decoratorAttributes,
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
        var decoratorTemplates = new Dictionary<string, string>();

        foreach (INamedTypeSymbol? decorator in decoratorAttributes)
        {
            // Get the 'template' constructor argument
            var template = decorator
                .GetAttributes()
                .FirstOrDefault(attr => attr.AttributeClass.Name == "DecorateBaseAttribute")?
                .ConstructorArguments
                .FirstOrDefault().Value as string;

            if (!string.IsNullOrEmpty(template))
            {
                decoratorTemplates[decorator.Name] = template;
            }
        }

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
