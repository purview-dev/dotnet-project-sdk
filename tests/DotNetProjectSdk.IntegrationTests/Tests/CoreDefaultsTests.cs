using Purview.DotNetProjectSdk.Harness;

namespace Purview.DotNetProjectSdk.Tests;

/// <summary>
/// Verifies the C# compiler defaults injected by Sdk.props — Nullable, ImplicitUsings,
/// LangVersion, Deterministic, RootNamespace derivation, and CI flag passthrough.
/// </summary>
public sealed class CoreDefaultsTests
{
	[Test]
	public async Task NullableEnabled_ByDefault(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync("MyLibrary", cancellationToken: cancellationToken);
		await Assert.That(await h.GetPropertyAsync("Nullable", cancellationToken)).IsEqualTo("enable");
	}

	[Test]
	public async Task ImplicitUsings_Enabled_ByDefault(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync("MyLibrary", cancellationToken: cancellationToken);
		await Assert.That(await h.GetPropertyAsync("ImplicitUsings", cancellationToken)).IsEqualTo("enable");
	}

	[Test]
	public async Task LangVersion_Preview_ByDefault(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync("MyLibrary", cancellationToken: cancellationToken);
		// The SDK sets LangVersion to "preview" unless explicitly overridden.
		await Assert.That(await h.GetPropertyAsync("LangVersion", cancellationToken)).IsEqualTo("preview");
	}

	[Test]
	public async Task Deterministic_True_ByDefault(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync("MyLibrary", cancellationToken: cancellationToken);
		await Assert.That(await h.GetPropertyAsync("Deterministic", cancellationToken)).IsEqualTo("true");
	}

	[Test]
	public async Task ManagePackageVersionsCentrally_True_ByDefault(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync("MyLibrary", cancellationToken: cancellationToken);
		await Assert
			.That(await h.GetPropertyAsync("ManagePackageVersionsCentrally", cancellationToken))
			.IsEqualTo("true");
	}

	[Test]
	public async Task RootNamespace_DerivedFromNamespacePrefixAndProjectName(CancellationToken cancellationToken)
	{
		// NamespacePrefix=Test, ProjectName=MyLibrary → Test.MyLibrary
		await using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			namespacePrefix: "Test",
			cancellationToken: cancellationToken
		);
		await Assert.That(await h.GetPropertyAsync("RootNamespace", cancellationToken)).IsEqualTo("Test.MyLibrary");
	}

	[Test]
	public async Task RootNamespace_TestSuffixStripped_ForTestProjects(CancellationToken cancellationToken)
	{
		// ProjectName=MyApp.UnitTests, NamespacePrefix=Test → Test.MyApp.UnitTests
		// After FixRootNamespaceTarget strips ".UnitTests" → Test.MyApp
		await using var h = await ProjectHarness.CreateAsync(
			"MyApp.UnitTests",
			namespacePrefix: "Test",
			cancellationToken: cancellationToken
		);
		await Assert.That(await h.GetPropertyAsync("RootNamespace", cancellationToken)).IsEqualTo("Test.MyApp");
	}

	[Test]
	public async Task RootNamespace_IntegrationTestsSuffixStripped(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"MyApp.IntegrationTests",
			namespacePrefix: "Test",
			cancellationToken: cancellationToken
		);
		await Assert.That(await h.GetPropertyAsync("RootNamespace", cancellationToken)).IsEqualTo("Test.MyApp");
	}

	[Test]
	public async Task Ci_PropertySet_WhenEnvironmentVariablePresent(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			extraEnv: new Dictionary<string, string> { ["CI"] = "true" },
			cancellationToken: cancellationToken
		);
		await Assert.That(await h.GetPropertyAsync("ContinuousIntegrationBuild", cancellationToken)).IsEqualTo("true");
	}

	[Test]
	public async Task Ci_PropertyNotSet_WhenEnvironmentVariableAbsent(CancellationToken cancellationToken)
	{
		// Ensure CI env var is not set for this test (it may be set in CI environments, so
		// we override with empty string to simulate a local dev machine).
		await using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			extraEnv: new Dictionary<string, string> { ["CI"] = "" },
			cancellationToken: cancellationToken
		);
		var value = await h.GetPropertyAsync("ContinuousIntegrationBuild", cancellationToken);
		// ContinuousIntegrationBuild should be empty (not "true") when CI is not set.
		await Assert.That(value).IsNotEqualTo("true");
	}

	[Test]
	[Arguments("net9.0", "net9.0")]
	[Arguments("net10.0", "net10.0")]
	public async Task TargetFramework_Honoured_WhenExplicitlySet(
		string tfm,
		string expected,
		CancellationToken cancellationToken
	)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			targetFramework: tfm,
			cancellationToken: cancellationToken
		);
		await Assert.That(await h.GetPropertyAsync("TargetFramework", cancellationToken)).IsEqualTo(expected);
	}
}
