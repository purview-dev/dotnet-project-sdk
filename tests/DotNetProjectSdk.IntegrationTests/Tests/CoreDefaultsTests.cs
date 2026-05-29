using Purview.DotNetProjectSdk.Harness;

namespace Purview.DotNetProjectSdk.Tests;

/// <summary>
/// Verifies the C# compiler defaults injected by Sdk.props — Nullable, ImplicitUsings,
/// LangVersion, Deterministic, RootNamespace derivation, and CI flag passthrough.
/// </summary>
public sealed class CoreDefaultsTests
{
	[Test]
	public async Task NullableEnabled_ByDefault()
	{
		await using var h = ProjectHarness.Create("MyLibrary");
		await Assert.That(await h.GetPropertyAsync("Nullable")).IsEqualTo("enable");
	}

	[Test]
	public async Task ImplicitUsings_Enabled_ByDefault()
	{
		await using var h = ProjectHarness.Create("MyLibrary");
		await Assert.That(await h.GetPropertyAsync("ImplicitUsings")).IsEqualTo("enable");
	}

	[Test]
	public async Task LangVersion_Preview_ByDefault()
	{
		await using var h = ProjectHarness.Create("MyLibrary");
		// The SDK sets LangVersion to "preview" unless explicitly overridden.
		await Assert.That(await h.GetPropertyAsync("LangVersion")).IsEqualTo("preview");
	}

	[Test]
	public async Task Deterministic_True_ByDefault()
	{
		await using var h = ProjectHarness.Create("MyLibrary");
		await Assert.That(await h.GetPropertyAsync("Deterministic")).IsEqualTo("true");
	}

	[Test]
	public async Task ManagePackageVersionsCentrally_True_ByDefault()
	{
		await using var h = ProjectHarness.Create("MyLibrary");
		await Assert.That(await h.GetPropertyAsync("ManagePackageVersionsCentrally")).IsEqualTo("true");
	}

	[Test]
	public async Task RootNamespace_DerivedFromNamespacePrefixAndProjectName()
	{
		// NamespacePrefix=Test, ProjectName=MyLibrary → Test.MyLibrary
		await using var h = ProjectHarness.Create("MyLibrary", namespacePrefix: "Test");
		await Assert.That(await h.GetPropertyAsync("RootNamespace")).IsEqualTo("Test.MyLibrary");
	}

	[Test]
	public async Task RootNamespace_TestSuffixStripped_ForTestProjects()
	{
		// ProjectName=MyApp.UnitTests, NamespacePrefix=Test → Test.MyApp.UnitTests
		// After FixRootNamespaceTarget strips ".UnitTests" → Test.MyApp
		await using var h = ProjectHarness.Create("MyApp.UnitTests", namespacePrefix: "Test");
		await Assert.That(await h.GetPropertyAsync("RootNamespace")).IsEqualTo("Test.MyApp");
	}

	[Test]
	public async Task RootNamespace_IntegrationTestsSuffixStripped()
	{
		await using var h = ProjectHarness.Create("MyApp.IntegrationTests", namespacePrefix: "Test");
		await Assert.That(await h.GetPropertyAsync("RootNamespace")).IsEqualTo("Test.MyApp");
	}

	[Test]
	public async Task Ci_PropertySet_WhenEnvironmentVariablePresent()
	{
		await using var h = ProjectHarness.Create("MyLibrary",
			extraEnv: new Dictionary<string, string> { ["CI"] = "true" });
		await Assert.That(await h.GetPropertyAsync("ContinuousIntegrationBuild")).IsEqualTo("true");
	}

	[Test]
	public async Task Ci_PropertyNotSet_WhenEnvironmentVariableAbsent()
	{
		// Ensure CI env var is not set for this test (it may be set in CI environments, so
		// we override with empty string to simulate a local dev machine).
		await using var h = ProjectHarness.Create("MyLibrary",
			extraEnv: new Dictionary<string, string> { ["CI"] = "" });
		var value = await h.GetPropertyAsync("ContinuousIntegrationBuild");
		// ContinuousIntegrationBuild should be empty (not "true") when CI is not set.
		await Assert.That(value).IsNotEqualTo("true");
	}

	[Test]
	[Arguments("net9.0", "net9.0")]
	[Arguments("net10.0", "net10.0")]
	public async Task TargetFramework_Honoured_WhenExplicitlySet(string tfm, string expected)
	{
		await using var h = ProjectHarness.Create("MyLibrary", targetFramework: tfm);
		await Assert.That(await h.GetPropertyAsync("TargetFramework")).IsEqualTo(expected);
	}
}
