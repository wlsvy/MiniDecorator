#nullable enable
using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Text;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;

namespace MiniDecorator;

internal record struct DecoratorTemplateIngredient(
    string DecoratorAttributeClassName,
    string Template);

internal record struct DecoratorTargetIngredient(
    BaseNamespaceDeclarationSyntax NamespaceSyntax,
    TypeDeclarationSyntax TypeDeclaration,
    MemberDeclarationSyntax MemberDeclaration,
    SyntaxList<UsingDirectiveSyntax> UsingDirectiveSyntaxes,
    string DecoratorAttributeClassName);

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
                predicate: static (s, _) => IsMethodOrPropertyWithAttribute(s),
                transform: static (ctx, _) => GetMethodWithDecorator(ctx))
            .Where(static m => m != null)
            .Select(static (nullable, _) => nullable!.Value)
            .Collect();

        context.RegisterSourceOutput(
            source: decoratorAttributeTypes.Combine(methodsWithDecorator),
            action: (spc, source) => Execute(source.Left, source.Right, spc));
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

    private static DecoratorTemplateIngredient GetDecoratorTemplate(GeneratorSyntaxContext context)
    {
        ClassDeclarationSyntax attributeClassDeclaration = (ClassDeclarationSyntax)context.Node;
        string className = attributeClassDeclaration.Identifier.ToString();
        BaseTypeSyntax decoratorBaseAttributeClass = attributeClassDeclaration.BaseList!.Types.Single(t => t.Type.ToString() == nameof(DecorateBaseAttribute));
        PrimaryConstructorBaseTypeSyntax primaryConstructorBaseTypeSyntax = (PrimaryConstructorBaseTypeSyntax)decoratorBaseAttributeClass;
        return new DecoratorTemplateIngredient(className, DecoratorSourceGeneratorCore.ParseTemplate(primaryConstructorBaseTypeSyntax));
    }

    /// <summary>
    /// 우선 메서드와 프로퍼티 대상으로 체크
    /// </summary>
    private static bool IsMethodOrPropertyWithAttribute(SyntaxNode node)
    {
        bool result = node is MethodDeclarationSyntax { AttributeLists.Count: > 0 };
        result |= node is PropertyDeclarationSyntax { AttributeLists.Count: > 0 };
        return result;
    }

    private static DecoratorTargetIngredient? GetMethodWithDecorator(GeneratorSyntaxContext context)
    {
        MemberDeclarationSyntax memberDeclaration = (MemberDeclarationSyntax)context.Node;
        ISymbol memberSymbol = context.SemanticModel.GetDeclaredSymbol(memberDeclaration)!;

        foreach (AttributeData? attr in memberSymbol.GetAttributes())
        {
            INamedTypeSymbol? attrBaseType = attr?.AttributeClass?.BaseType;
            if (Util.IsAttributeNameEqual(
                   targetTypeName: attrBaseType?.Name ?? string.Empty,
                   nameof(DecorateBaseAttribute).AsSpan()))
            {
                BaseNamespaceDeclarationSyntax namespaceSyntax = Util.GetNamespace(memberDeclaration);
                TypeDeclarationSyntax typeDeclaration = Util.GetParentTypeOfMember(memberDeclaration);
                SyntaxList<UsingDirectiveSyntax> usingDirectiveSyntaxes = memberDeclaration.SyntaxTree.GetCompilationUnitRoot().Usings;
                string decoratorAttributeClassName = attrBaseType!.Name;

                return new DecoratorTargetIngredient(namespaceSyntax, typeDeclaration, memberDeclaration, usingDirectiveSyntaxes, decoratorAttributeClassName);
            }
        }

        return default;
    }

    private void Execute(
        ImmutableArray<DecoratorTemplateIngredient> templates,
        ImmutableArray<DecoratorTargetIngredient> targets,
        SourceProductionContext context)
    {
        if (templates.IsDefaultOrEmpty)
        {
            throw new Exception("Attr is Empty");
        }

        if (targets.IsDefaultOrEmpty)
        {
            throw new Exception("method is Empty");
        }

        // Map decorator attribute names to their template strings
        Dictionary<string, DecoratorTemplateIngredient> decoratorTemplates = new();

        // StringBuilder to accumulate the generated methods
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("using System;");
        sb.AppendLine();
        sb.AppendLine("namespace GeneratedDecorators");
        sb.AppendLine("{");

        foreach (var target in targets)
        {
            if (!decoratorTemplates.TryGetValue(target.DecoratorAttributeClassName, out var template))
            {
                //TODO : error
                continue;
            }

            string generatedMethod = DecoratorSourceGeneratorCore.GenerateCodeFromTemplate(target.MemberDeclaration, template.Template);
            sb.AppendLine(generatedMethod);
            sb.AppendLine();
        }

        sb.AppendLine("}"); // End of namespace

        // Add the generated source
        context.AddSource("GeneratedDecorators.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }
}

