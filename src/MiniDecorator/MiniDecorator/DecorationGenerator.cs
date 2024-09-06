using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiniDecorator;

[Generator]
public sealed class DecorationGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        
    }

    public void Execute(GeneratorExecutionContext context)
    {
        throw new NotImplementedException();
    }
}



[Generator]
public class AutoNotifyGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        // 초기화 단계에서는 별도 작업이 필요하지 않습니다.
    }

    public void Execute(GeneratorExecutionContext context)
    {
        // 모든 SyntaxTree를 가져옵니다.
        SyntaxTree[] syntaxTrees = context.Compilation.SyntaxTrees.ToArray();

        // 병렬 처리 결과를 저장할 ConcurrentBag
        ConcurrentBag<(string fileName, string code)> generatedCodes = new();

        // 병렬 처리로 각 SyntaxTree를 분석
        Parallel.ForEach(syntaxTrees, syntaxTree =>
        {
            SyntaxNode root = syntaxTree.GetRoot();
            // 클래스 선언을 가져오고 그 안에 모든 멤버를 조회
            var classDeclarations = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .SelectMany(classDecl => classDecl.Members
                    .Select(member => (classDecl, member)));

            foreach (var (classDeclaration, member) in classDeclarations)
            {
                if (member is FieldDeclarationSyntax fieldDeclaration)
                {
                    foreach (VariableDeclaratorSyntax variable in fieldDeclaration.Declaration.Variables)
                    {
                        // 필드에 [AutoNotify] 속성이 있는지 확인
                        if (fieldDeclaration.AttributeLists.Any(attrList => attrList.Attributes.Any(attr => attr.ToString().Contains("AutoNotify"))))
                        {
                            // 클래스 및 필드 이름 추출
                            string className = classDeclaration.Identifier.Text;
                            string fieldName = variable.Identifier.Text;
                            string propertyName = char.ToUpper(fieldName[0]) + fieldName.Substring(1);

                            // Raw string literal을 사용하여 코드 생성
                            string generatedCode = $$"""
                                namespace {{context.Compilation.AssemblyName}}
                                {
                                    public partial class {{className}}
                                    {
                                        public {{fieldDeclaration.Declaration.Type}} {{propertyName}}
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
                                }
                                """;

                            // 결과를 ConcurrentBag에 저장
                            generatedCodes.Add(($"{className}_{fieldName}_AutoNotify.cs", generatedCode));
                        }
                    }
                }
            }
        });

        // 병렬 처리 후 context에 코드 추가
        foreach (var (fileName, code) in generatedCodes)
        {
            context.AddSource(fileName, SourceText.From(code, Encoding.UTF8));
        }
    }
}