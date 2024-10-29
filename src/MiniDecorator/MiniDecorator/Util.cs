#nullable enable
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace MiniDecorator;

public record struct TypeOfExpr(
    string TypeName,
    string[] ElementTypeNames,
    SyntaxKind SyntaxType);

public static class Util
{
    public static bool IsAttributeNameEqual(this AttributeSyntax attributeSyntax, ReadOnlySpan<char> expectedAttributeTypeName)
        => IsAttributeNameEqual(attributeSyntax.Name.ToString(), expectedAttributeTypeName);

    /// <summary>
    /// 애트리뷰트 타입 이름에 대해서는 'Attribute' 접미사를 붙인 버전과 뗀 버전에 대해서 각각 비교할 것
    /// </summary>
    /// <param name="targetTypeName"> 분석한 코드의 Attribute syntax. 이때 'Attribute' 접미사를 떼고 지정했을 수 있다.</param>
    /// <param name="expectedAttributeTypeName">비교 대상 AttributeName</param>
    public static bool IsAttributeNameEqual(string targetTypeName, ReadOnlySpan<char> expectedAttributeTypeName)
    {
        ReadOnlySpan<char> span = targetTypeName.AsSpan();
        if (span.EndsWith("Attribute".AsSpan()) &&
            span.SequenceEqual(expectedAttributeTypeName))
        {
            return true;
        }

        int postfixLength = expectedAttributeTypeName.Length - "Attribute".Length;
        if (span.SequenceEqual(expectedAttributeTypeName.Slice(start: 0, length: postfixLength)))
        {
            return true;
        }

        return false;
    }

    public static TypeDeclarationSyntax GetParentTypeOfMember(MemberDeclarationSyntax memberDeclarationSyntax)
    {
        SyntaxNode? current = memberDeclarationSyntax.Parent;
        while (current != null)
        {
            if (current is TypeDeclarationSyntax typeDeclarationSyntax)
            {
                return typeDeclarationSyntax;
            }
            current = current.Parent;
        }

        throw new NotImplementedException();
    }

    public static BaseNamespaceDeclarationSyntax GetNamespace(SyntaxNode node)
    {
        SyntaxNode? current = node;
        while (current != null)
        {
            if (current is NamespaceDeclarationSyntax namespaceDeclaration)
            {
                return namespaceDeclaration;
            }
            if (current is FileScopedNamespaceDeclarationSyntax fileScopedNamespace)
            {
                return fileScopedNamespace;
            }
            current = current.Parent;
        }

        throw new NotImplementedException();
    }
    public static string GetNameOfExpressionOrThrow(this AttributeArgumentSyntax attributeArgumentSyntax, string filePath)
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

    public static TypeOfExpr GetTypeOfExpressionOrThrow(this AttributeArgumentSyntax attributeArgumentSyntax, string filePath)
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

