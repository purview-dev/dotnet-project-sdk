using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Purview.DotNetProjectSdk.Analyzers.ExtensionsNamespace;

namespace Purview.DotNetProjectSdk.Analyzers.IntegrationTests.Extensions;

/// <summary>
/// Integration tests for <see cref="ExtensionsNamespaceCodeFixProvider"/>.
/// </summary>
[Category("Integration")]
public sealed class ExtensionsNamespaceCodeFixTests
{
	static async Task<string> ApplyCodeFixAsync(string fileName, string source, CancellationToken cancellationToken)
	{
		using var workspace = new AdhocWorkspace();
		ProjectId projectId = ProjectId.CreateNewId();
		DocumentId documentId = DocumentId.CreateNewId(projectId);

		var projectInfo = ProjectInfo
			.Create(
				projectId,
				VersionStamp.Create(),
				"TestProject",
				"TestProject",
				LanguageNames.CSharp,
				parseOptions: CSharpParseOptions.Default,
				compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
			)
			.WithMetadataReferences(AnalyzerTestInfrastructure.BuildBclReferences());

		Solution solution = workspace
			.CurrentSolution.AddProject(projectInfo)
			.AddDocument(documentId, Path.GetFileName(fileName), SourceText.From(source), filePath: fileName);

		string analyzerConfig = """
			is_root = true
			[*.cs]
			build_property.ProjectDir = C:\FakeProject\
			build_property.RootNamespace = An.Example.Project
			""";
		solution = solution.AddAnalyzerConfigDocument(
			DocumentId.CreateNewId(projectId),
			".editorconfig",
			SourceText.From(analyzerConfig),
			filePath: @"C:\FakeProject\.editorconfig"
		);

		Project project = solution.GetProject(projectId)!;
		Document document = project.GetDocument(documentId)!;
		Compilation compilation = (await project.GetCompilationAsync(cancellationToken))!;

		var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new ExtensionsNamespaceAnalyzer());
		ImmutableArray<Diagnostic> diagnostics = await compilation
			.WithAnalyzers(analyzers, project.AnalyzerOptions)
			.GetAnalyzerDiagnosticsAsync(cancellationToken);

		Diagnostic diagnostic = diagnostics.Single(d => d.Id == ExtensionsNamespaceAnalyzer.DiagnosticId);
		var provider = new ExtensionsNamespaceCodeFixProvider();
		var actions = new List<CodeAction>();

		var context = new CodeFixContext(
			document,
			diagnostic,
			(action, _) => actions.Add(action),
			CancellationToken.None
		);

		await provider.RegisterCodeFixesAsync(context);
		CodeAction actionToApply = actions.Single();
		ImmutableArray<CodeActionOperation> operations = await actionToApply.GetOperationsAsync(CancellationToken.None);

		ApplyChangesOperation applyChanges = operations.OfType<ApplyChangesOperation>().Single();
		Document fixedDocument = applyChanges.ChangedSolution.GetDocument(documentId)!;
		SourceText fixedText = (await fixedDocument.GetTextAsync(cancellationToken))!;
		return fixedText.ToString();
	}

	[Test]
	[Arguments(
		@"C:\FakeProject\Extensions\System\StringExtensions.cs",
		"An.Example.Project.Extensions.System",
		"System",
		DisplayName = "Block namespace → corrected to System"
	)]
	[Arguments(
		@"C:\FakeProject\Extensions\Microsoft\Extensions\Configuration\ConfigExt.cs",
		"An.Example.Project.Microsoft.Extensions.Configuration",
		"Microsoft.Extensions.Configuration",
		DisplayName = "Deeply nested → corrected to Microsoft.Extensions.Configuration"
	)]
	public async Task CodeFix_ReplacesNamespace_WithFolderDerivedNamespace(
		string fileName,
		string wrongNamespace,
		string correctNamespace,
		CancellationToken cancellationToken
	)
	{
		string before = $$"""
			namespace {{wrongNamespace}}
			{
				public static class Ext { }
			}
			""";

		string after = $$"""
			namespace {{correctNamespace}}
			{
				public static class Ext { }
			}
			""";

		string fixedSource = await ApplyCodeFixAsync(fileName, before, cancellationToken);
		await Assert.That(fixedSource).IsEqualTo(after);
	}

	[Test]
	public async Task CodeFix_CorrectlyHandles_FileScopedNamespace(CancellationToken cancellationToken)
	{
		const string before = """
			namespace An.Example.Project.Extensions.System;

			public static class StringExtensions { }
			""";

		const string after = """
			namespace System;

			public static class StringExtensions { }
			""";

		string fixedSource = await ApplyCodeFixAsync(
			@"C:\FakeProject\Extensions\System\StringExtensions.cs",
			before,
			cancellationToken
		);
		await Assert.That(fixedSource).IsEqualTo(after);
	}
}
