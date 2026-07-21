using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Purview.DotNetProjectSdk.Analyzers.ExtensionsNamespace;

sealed class ExtensionsNamespaceCodeFixProvider : CodeFixProvider
{
	const string ProjectDirPropertyKey = "build_property.ProjectDir";

	public override ImmutableArray<string> FixableDiagnosticIds => [ExtensionsNamespaceAnalyzer.DiagnosticId];

	public override FixAllProvider GetFixAllProvider()
	{
		return WellKnownFixAllProviders.BatchFixer;
	}

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
		if (root is null)
		{
			return;
		}

		Diagnostic diagnostic = context.Diagnostics[0];
		SyntaxNode? node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
		BaseNamespaceDeclarationSyntax? namespaceDeclaration =
			node.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>();
		if (namespaceDeclaration is null)
		{
			return;
		}

		context.RegisterCodeFix(
			CodeAction.Create(
				"Sync namespace to Extensions folder structure",
				ct => ApplyFixAsync(context.Document, namespaceDeclaration, ct),
				equivalenceKey: ExtensionsNamespaceAnalyzer.DiagnosticId
			),
			diagnostic
		);
	}

	static async Task<Document> ApplyFixAsync(
		Document document,
		BaseNamespaceDeclarationSyntax namespaceDeclaration,
		CancellationToken cancellationToken
	)
	{
		SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken);
		if (root is null)
		{
			return document;
		}

		if (!TryGetProjectDir(document, root.SyntaxTree, out string projectDir))
		{
			return document;
		}

		string? expectedNamespace = ExtensionsNamespaceHelper.ComputeExpectedNamespace(
			projectDir,
			root.SyntaxTree.FilePath
		);
		if (expectedNamespace is null)
		{
			return document;
		}

		if (expectedNamespace.Length == 0)
		{
			SyntaxNode globalNamespaceDocument = RemoveNamespaceDeclaration(root, namespaceDeclaration);
			return document.WithSyntaxRoot(globalNamespaceDocument);
		}

		var expectedNamespaceName = SyntaxFactory
			.ParseName(expectedNamespace)
			.WithTriviaFrom(namespaceDeclaration.Name);

		BaseNamespaceDeclarationSyntax rewrittenNamespace = namespaceDeclaration switch
		{
			FileScopedNamespaceDeclarationSyntax fileScoped => fileScoped.WithName(expectedNamespaceName),
			NamespaceDeclarationSyntax blockScoped => blockScoped.WithName(expectedNamespaceName),
			_ => namespaceDeclaration,
		};

		SyntaxNode newRoot = root.ReplaceNode(namespaceDeclaration, rewrittenNamespace);
		return document.WithSyntaxRoot(newRoot);
	}

	static bool TryGetProjectDir(Document document, SyntaxTree syntaxTree, out string projectDir)
	{
		AnalyzerConfigOptions options = document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(
			syntaxTree
		);

		if (
			options.TryGetValue(ProjectDirPropertyKey, out string? configuredValue)
			&& !string.IsNullOrWhiteSpace(configuredValue)
		)
		{
			projectDir = configuredValue;
			return true;
		}

		projectDir = string.Empty;
		return false;
	}

	static SyntaxNode RemoveNamespaceDeclaration(SyntaxNode root, BaseNamespaceDeclarationSyntax namespaceDeclaration)
	{
		if (namespaceDeclaration.Parent is CompilationUnitSyntax compilationUnit)
		{
			int index = compilationUnit.Members.IndexOf(namespaceDeclaration);
			if (index >= 0)
			{
				var members = compilationUnit.Members.RemoveAt(index).InsertRange(index, namespaceDeclaration.Members);
				return compilationUnit.WithMembers(members);
			}
		}

		if (namespaceDeclaration.Parent is NamespaceDeclarationSyntax parentNamespace)
		{
			int index = parentNamespace.Members.IndexOf(namespaceDeclaration);
			if (index >= 0)
			{
				var members = parentNamespace.Members.RemoveAt(index).InsertRange(index, namespaceDeclaration.Members);
				var rewrittenParent = parentNamespace.WithMembers(members);
				return root.ReplaceNode(parentNamespace, rewrittenParent);
			}
		}

		return root;
	}
}
