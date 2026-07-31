using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Purview.DotNetProjectSdk.Analyzers.ExtensionsNamespace;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExtensionsNamespaceSuppressor : DiagnosticSuppressor
{
	const string ProjectDirPropertyKey = "build_property.ProjectDir";

	static readonly SuppressionDescriptor SuppressIde0130ForExtensionsNamespaceRule = new(
		"PDS0003",
		"IDE0130",
		"Files rooted under 'Extensions' intentionally derive namespace from the Extensions subtree and ignore RootNamespace."
	);

	public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions =>
		[SuppressIde0130ForExtensionsNamespaceRule];

	public override void ReportSuppressions(SuppressionAnalysisContext context)
	{
		foreach (Diagnostic diagnostic in context.ReportedDiagnostics)
		{
			if (!string.Equals(diagnostic.Id, "IDE0130", StringComparison.Ordinal))
			{
				continue;
			}

			Location location = diagnostic.Location;
			if (!location.IsInSource || location.SourceTree is null)
			{
				continue;
			}

			AnalyzerConfigOptions options = context.Options.AnalyzerConfigOptionsProvider.GetOptions(
				location.SourceTree
			);
			if (
				!options.TryGetValue(ProjectDirPropertyKey, out string? projectDir)
				|| string.IsNullOrWhiteSpace(projectDir)
			)
			{
				continue;
			}

			if (ExtensionsNamespaceHelper.IsInExtensionsRootScope(projectDir, location.SourceTree.FilePath))
			{
				context.ReportSuppression(Suppression.Create(SuppressIde0130ForExtensionsNamespaceRule, diagnostic));
			}
		}
	}
}
