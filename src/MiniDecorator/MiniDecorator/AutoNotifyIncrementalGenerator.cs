using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace MiniDecorator;

[Generator(LanguageNames.CSharp)]
public sealed class AutoNotifyIncrementalGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        System.Diagnostics.Debugger.Launch();
        
        // 단계 1: [AutoNotify] 속성을 가진 필드 선언을 찾습니다.
        var fieldDeclarations = context.SyntaxProvider
            .CreateSyntaxProvider(
                predicate: static (s, _) => IsFieldWithAttribute(s),
                transform: static (ctx, _) => GetFieldInfo(ctx))
            .Where(static field => field is not null)
            .Select(static (field, _) => field!)
            .Collect();
        
        // 단계 2: 코드를 생성하고 출력에 등록합니다.
        context.RegisterSourceOutput(fieldDeclarations, 
            static (spc, fields) => Execute(spc, fields));
    }

    private static bool IsFieldWithAttribute(SyntaxNode node)
    {
        return node is PropertyDeclarationSyntax propertyDeclarationSyntax &&
               propertyDeclarationSyntax.AttributeLists
                   .Any(attrList => attrList.Attributes
                       .Any(attr => attr.Name.ToString().Contains("AutoNotify")));
    }

    private static (ClassDeclarationSyntax classDeclaration, PropertyDeclarationSyntax fieldDeclaration)? GetFieldInfo(GeneratorSyntaxContext context)
    {
        var propertyDeclarationSyntax = (PropertyDeclarationSyntax)context.Node;
        var classDeclaration = propertyDeclarationSyntax.Parent as ClassDeclarationSyntax;
        if (classDeclaration == null)
        {
            throw new Exception($"Class Can't Found. class:{propertyDeclarationSyntax.Identifier.ToString()}");
        }

        BaseNamespaceDeclarationSyntax? namespaceDeclarationSyntax = classDeclaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().SingleOrDefault();
        if (namespaceDeclarationSyntax is null)
        {
            throw new Exception($"Namespace Can't Found. class:{classDeclaration.Identifier.ToString()}");
        }
        
        return (classDeclaration, propertyDeclarationSyntax);
    }

    private static void Execute(
        SourceProductionContext context,
        ImmutableArray<(ClassDeclarationSyntax classDeclaration, PropertyDeclarationSyntax fieldDeclaration)?> fields)
    {
        foreach (var (classDeclaration, propertyDeclarationSyntax) in
                 fields.Where(x => x is not null)
                     .Select(x => x!.Value))
        {
            string className = classDeclaration.Identifier.Text;
            string fieldName = propertyDeclarationSyntax.Identifier.Text;
            string propertyName = $"Generated_{fieldName}";
            string fieldType = propertyDeclarationSyntax.Type.ToString();

            string generatedCode = $$"""
                namespace {{GetNamespace(classDeclaration)}};
                public partial class {{className}}
                {
                    public {{fieldType}} {{propertyName}}
                    {
                        get => {{fieldName}};
                        set
                        {
                            if (!Equals({{fieldName}}, value))
                            {
                                {{fieldName}} = value;
                                OnPropertyChanged(nameof({{propertyName}}));
                            }
                        }
                    }
            
                    private void OnPropertyChanged(string propertyName)
                    {
                        // PropertyChanged 이벤트를 호출하는 코드가 여기에 위치할 수 있습니다.
                    }
                }
                """;

            context.AddSource($"{className}_{fieldName}.g.cs", SourceText.From(generatedCode, Encoding.UTF8));
        }
    }

    private static string GetNamespace(SyntaxNode classDeclaration)
    {
        // 클래스의 네임스페이스를 가져옵니다.
        var namespaceDeclaration = classDeclaration.Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault();

        return namespaceDeclaration?.Name.ToString() ?? "GlobalNamespace";
    }
}
