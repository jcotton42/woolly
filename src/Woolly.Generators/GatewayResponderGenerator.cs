using System;
using System.Threading;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Woolly.Generators;

[Generator(LanguageNames.CSharp)]
public class GatewayResponderGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.SyntaxProvider.ForAttributeWithMetadataName(
            "Woolly.Infrastructure.GatewayResponderAttribute",
            (node, _) => node is TypeDeclarationSyntax,
            Transform);
    }

    private static object Transform(GeneratorAttributeSyntaxContext context, CancellationToken token)
    {

    }

}
