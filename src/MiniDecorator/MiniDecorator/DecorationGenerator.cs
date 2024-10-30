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

internal record struct DecoratorSourceIngredient(
    BaseNamespaceDeclarationSyntax Namespace,
    TypeDeclarationSyntax TypeDeclaration,
    MemberDeclarationSyntax MemberDeclaration,
    SyntaxList<UsingDirectiveSyntax> UsingDirectiveSyntaxList,
    string DecoratorAttributeClassName);

[Generator(LanguageNames.CSharp)]
public class DecoratorSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Note: ForDebug
        //System.Diagnostics.Debugger.Launch();

        var decoratorAttributeTypes = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsDecoratorAttributeWithPrimaryConstructor(s),
                transform: static (ctx, _) => GetDecoratorTemplate(ctx))
            .Collect();

        var methodsWithDecorator = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsMethodOrPropertyWithAttribute(s),
                transform: static (ctx, _) => GetDecorationRequestedSource(ctx))
            .Where(static m => m != null)
            .Select(static (nullable, _) => nullable!.Value)
            .Collect();

        context.RegisterSourceOutput(
            source: decoratorAttributeTypes.Combine(methodsWithDecorator),
            action: (spc, source) => Execute(source.Left, source.Right, spc));
    }

    private static bool IsDecoratorAttributeWithPrimaryConstructor(SyntaxNode node)
    {
        return DecoratorSourceGeneratorCore.TryGetDecoratorAttribute(node, out _);
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

    private static DecoratorSourceIngredient? GetDecorationRequestedSource(GeneratorSyntaxContext context)
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
                SyntaxList<UsingDirectiveSyntax> usingDirectiveSyntaxList = memberDeclaration.SyntaxTree.GetCompilationUnitRoot().Usings;
                string decoratorAttributeClassName = attr!.AttributeClass!.Name;

                return new DecoratorSourceIngredient(namespaceSyntax, typeDeclaration, memberDeclaration, usingDirectiveSyntaxList, decoratorAttributeClassName);
            }
        }

        return default;
    }

    private void Execute(
        ImmutableArray<DecoratorTemplateIngredient> templates,
        ImmutableArray<DecoratorSourceIngredient> sources,
        SourceProductionContext context)
    {
        Dictionary<string, DecoratorTemplateIngredient> templateDic = templates.ToDictionary(
            keySelector: x => x.DecoratorAttributeClassName,
            elementSelector: x => x);

        var sourcesByType = sources
            .Where(s => templateDic.ContainsKey(s.DecoratorAttributeClassName))
            .GroupBy(s => $"{s.Namespace.Name}_{s.TypeDeclaration.Identifier}");

        foreach (var sourcesInSingleType in sourcesByType)
        {
            List<string> generatedCodeInSingleType = [];
            DecoratorSourceIngredient? firstSource = null;
            foreach (DecoratorSourceIngredient source in sourcesInSingleType)
            {
                firstSource ??= source;
                DecoratorTemplateIngredient template = templateDic[source.DecoratorAttributeClassName];
                string generatedCode = DecoratorSourceGeneratorCore.GenerateCodeFromTemplate(source.MemberDeclaration, template.Template);
                generatedCodeInSingleType.Add(generatedCode);
            }

            NameSyntax namespaceDeclaration = firstSource!.Value.Namespace.Name;
            TypeDeclarationSyntax typeDecl = firstSource!.Value.TypeDeclaration;
            IEnumerable<string> usingDeclarations = firstSource!.Value.UsingDirectiveSyntaxList.Select(x => $"using {x.Name};");

            string generatedFileCode =
                $$"""
                  {{usingDeclarations.JoinLines(tab: 0)}}

                  // <auto-generated />

                  namespace {{namespaceDeclaration}};

                  partial {{typeDecl.Keyword}} {{typeDecl.Identifier}}
                  {
                      {{generatedCodeInSingleType.JoinLines(tab: 1)}}
                  }
                  """;

            context.AddSource(
                hintName: $"{typeDecl.Identifier}.g.cs", 
                sourceText: SourceText.From(
                    encoding: Encoding.UTF8,
                    text: generatedFileCode));
        }
    }
}

