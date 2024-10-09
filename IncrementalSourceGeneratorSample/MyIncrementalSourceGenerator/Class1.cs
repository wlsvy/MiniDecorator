using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Text;

[Generator]
public class HelloWorldGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 소스 코드를 생성하는 로직을 추가합니다.
        context.RegisterSourceOutput(context.CompilationProvider, (spc, compilation) =>
        {
            // 간단한 "Hello, World!" 메서드를 생성합니다.
            var source = @"
            namespace GeneratedCode
            {
                public static class HelloWorld
                {
                    public static string SayHello() => ""Hello, World!"";
                }
            }";

            // 소스를 추가합니다.
            spc.AddSource("HelloWorldGenerator", SourceText.From(source, Encoding.UTF8));
        });
    }
}