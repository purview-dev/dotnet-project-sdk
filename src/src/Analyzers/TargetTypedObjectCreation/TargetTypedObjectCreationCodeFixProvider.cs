using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.DotNetProjectSdk.Analyzers.TargetTypedObjectCreation;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(TargetTypedObjectCreationCodeFixProvider))]
[Shared]
public sealed class TargetTypedObjectCreationCodeFixProvider : CodeFixProvider
{
  public override ImmutableArray<string> FixableDiagnosticIds =>
    [TargetTypedObjectCreationAnalyzer.DiagnosticId];

  public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

  public override async Task RegisterCodeFixesAsync(CodeFixContext context)
  {
    var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
    if (root is null)
      return;

    var node = root.FindNode(context.Diagnostics[0].Location.SourceSpan);
    var declaration = node.FirstAncestorOrSelf<VariableDeclarationSyntax>();
    if (declaration is null || !declaration.Type.IsVar)
      return;

    if (
      declaration.Variables.Count != 1
      || declaration.Variables[0].Initializer?.Value is not ObjectCreationExpressionSyntax
    )
      return;

    context.RegisterCodeFix(
      CodeAction.Create(
        "Use explicit type and target-typed new",
        cancellationToken => ApplyFixAsync(context.Document, declaration, cancellationToken),
        equivalenceKey: TargetTypedObjectCreationAnalyzer.DiagnosticId
      ),
      context.Diagnostics
    );
  }

  static async Task<Document> ApplyFixAsync(
    Document document,
    VariableDeclarationSyntax declaration,
    CancellationToken cancellationToken
  )
  {
    _ = cancellationToken;
    var objectCreation = declaration.Variables[0].Initializer?.Value
      as ObjectCreationExpressionSyntax;
    if (objectCreation is null)
      return document;

    var explicitType = objectCreation.Type.WithTriviaFrom(declaration.Type);
    var targetTypedCreation = SyntaxFactory
      .ImplicitObjectCreationExpression(
        objectCreation.ArgumentList ?? SyntaxFactory.ArgumentList(),
        objectCreation.Initializer
      )
      .WithTriviaFrom(objectCreation);

    var rewrittenDeclaration = declaration
      .ReplaceNode(objectCreation, targetTypedCreation)
      .WithType(explicitType);
    var root = await document.GetSyntaxRootAsync(cancellationToken);
    return root is null
      ? document
      : document.WithSyntaxRoot(root.ReplaceNode(declaration, rewrittenDeclaration));
  }
}
