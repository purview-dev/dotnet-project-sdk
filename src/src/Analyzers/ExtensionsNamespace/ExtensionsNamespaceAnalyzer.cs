using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Purview.DotNetProjectSdk.Analyzers.ExtensionsNamespace;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExtensionsNamespaceAnalyzer : DiagnosticAnalyzer
{
	internal const string DiagnosticId = "PDS0002";

	const string ProjectDirPropertyKey = "build_property.ProjectDir";
	const string RootNamespacePropertyKey = "build_property.RootNamespace";

	static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Extensions root folder resets namespace",
		"Namespace '{0}' does not match expected Extensions namespace '{1}'",
		"Naming",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true
	);

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

	public override void Initialize(AnalysisContext context)
	{
		if (context is null)
			throw new ArgumentNullException(nameof(context));

		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		context.RegisterSyntaxNodeAction(
			AnalyzeNamespaceDeclaration,
			SyntaxKind.NamespaceDeclaration,
			SyntaxKind.FileScopedNamespaceDeclaration
		);
	}

	static void AnalyzeNamespaceDeclaration(SyntaxNodeAnalysisContext context)
	{
		if (context.Node is not BaseNamespaceDeclarationSyntax namespaceDeclaration)
		{
			return;
		}

		string? filePath = context.Node.SyntaxTree.FilePath;
		if (string.IsNullOrWhiteSpace(filePath))
		{
			return;
		}

		if (!TryGetBuildProperty(context, ProjectDirPropertyKey, out string projectDir))
		{
			return;
		}

		// The RootNamespace build property is intentionally read from AnalyzerConfigOptions
		// as the authoritative source for non-Extensions files, even though this analyzer
		// only reports diagnostics for files scoped to the root Extensions directory.
		_ = TryGetBuildProperty(context, RootNamespacePropertyKey, out _);

		string? expectedNamespace = ExtensionsNamespaceHelper.ComputeExpectedNamespace(projectDir, filePath);
		if (expectedNamespace is null)
		{
			return;
		}

		string actualNamespace = namespaceDeclaration.Name.ToString();
		if (string.Equals(actualNamespace, expectedNamespace, StringComparison.Ordinal))
		{
			return;
		}

		var diagnostic = Diagnostic.Create(
			Rule,
			namespaceDeclaration.Name.GetLocation(),
			actualNamespace,
			expectedNamespace
		);

		context.ReportDiagnostic(diagnostic);
	}

	static bool TryGetBuildProperty(SyntaxNodeAnalysisContext context, string key, out string value)
	{
		AnalyzerConfigOptions options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(
			context.Node.SyntaxTree
		);

		if (options.TryGetValue(key, out string? configuredValue) && !string.IsNullOrWhiteSpace(configuredValue))
		{
			value = configuredValue;
			return true;
		}

		value = string.Empty;
		return false;
	}
}
