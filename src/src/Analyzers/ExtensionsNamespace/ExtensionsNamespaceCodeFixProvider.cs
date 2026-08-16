using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Purview.DotNetProjectSdk.Analyzers.ExtensionsNamespace;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ExtensionsNamespaceCodeFixProvider))]
[Shared]
public sealed class ExtensionsNamespaceCodeFixProvider : CodeFixProvider
{
	const string ProjectDirPropertyKey = "build_property.ProjectDir";

	public override ImmutableArray<string> FixableDiagnosticIds => [ExtensionsNamespaceAnalyzer.DiagnosticId];

	public override FixAllProvider GetFixAllProvider()
	{
		return WellKnownFixAllProviders.BatchFixer;
	}

	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
		if (root is null)
		{
			return;
		}

		var diagnostic = context.Diagnostics[0];
		var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
		var namespaceDeclaration = node.FirstAncestorOrSelf<BaseNamespaceDeclarationSyntax>();
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
		var root = await document.GetSyntaxRootAsync(cancellationToken);
		if (root is null)
		{
			return document;
		}

		if (!TryGetProjectDir(document, root.SyntaxTree, out var projectDir))
		{
			return document;
		}

		var expectedNamespace = ExtensionsNamespaceHelper.ComputeExpectedNamespace(
			projectDir,
			root.SyntaxTree.FilePath
		);
		if (expectedNamespace is null)
		{
			return document;
		}

		if (expectedNamespace.Length == 0)
		{
			var globalNamespaceDocument = RemoveNamespaceDeclaration(root, namespaceDeclaration);
			return document.WithSyntaxRoot(globalNamespaceDocument);
		}

		var expectedNamespaceName = SyntaxFactory
			.ParseName(expectedNamespace)
			.WithTriviaFrom(namespaceDeclaration.Name);

		var rewrittenNamespace = namespaceDeclaration switch
		{
			FileScopedNamespaceDeclarationSyntax fileScoped => fileScoped.WithName(expectedNamespaceName),
			NamespaceDeclarationSyntax blockScoped => blockScoped.WithName(expectedNamespaceName),
			_ => namespaceDeclaration,
		};

		var newRoot = root.ReplaceNode(namespaceDeclaration, rewrittenNamespace);
		return document.WithSyntaxRoot(newRoot);
	}

	static bool TryGetProjectDir(Document document, SyntaxTree syntaxTree, out string projectDir)
	{
		var options = document.Project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GetOptions(syntaxTree);

		if (
			options.TryGetValue(ProjectDirPropertyKey, out var configuredValue)
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
			var index = compilationUnit.Members.IndexOf(namespaceDeclaration);
			if (index >= 0)
			{
				var members = compilationUnit.Members.RemoveAt(index).InsertRange(index, namespaceDeclaration.Members);
				return compilationUnit.WithMembers(members);
			}
		}

		if (namespaceDeclaration.Parent is NamespaceDeclarationSyntax parentNamespace)
		{
			var index = parentNamespace.Members.IndexOf(namespaceDeclaration);
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
