using Purview.DotNetProjectSdk.Harness;

namespace Purview.DotNetProjectSdk.Tests;

/// <summary>
/// Verifies that the SDK wires the correct test framework packages and output type
/// for projects that match the test-project naming convention.
/// </summary>
public sealed class TestWiringTests
{
	[Test]
	public async Task TestProject_OutputType_IsExe(CancellationToken cancellationToken)
	{
		// Test projects using Microsoft.Testing.Platform must be executables.
		await using var h = await ProjectHarness.CreateAsync("MyApp.UnitTests", cancellationToken: cancellationToken);
		await Assert.That(await h.GetPropertyAsync("OutputType", cancellationToken)).IsEqualTo("Exe");
	}

	[Test]
	public async Task TestProject_DefaultTestingFramework_IsTUnit(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync("MyApp.UnitTests", cancellationToken: cancellationToken);
		await Assert.That(await h.GetPropertyAsync("TestingFramework", cancellationToken)).IsEqualTo("TUnit");
	}

	[Test]
	public async Task TestProject_DefaultSubstituteFramework_IsTUnitMocks(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync("MyApp.UnitTests", cancellationToken: cancellationToken);
		await Assert.That(await h.GetPropertyAsync("SubstituteFramework", cancellationToken)).IsEqualTo("TUnitMocks");
	}

	[Test]
	public async Task TestProject_DefaultTestDataFramework_IsBogus(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync("MyApp.UnitTests", cancellationToken: cancellationToken);
		await Assert.That(await h.GetPropertyAsync("TestDataFramework", cancellationToken)).IsEqualTo("Bogus");
	}

	[Test]
	public async Task TestProject_Xunit_WhenOptedIn(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"MyApp.UnitTests",
			preImportProps: "<TestingFramework>Xunit</TestingFramework>",
			cancellationToken: cancellationToken
		);
		await Assert.That(await h.GetPropertyAsync("TestingFramework", cancellationToken)).IsEqualTo("Xunit");
	}

	[Test]
	public async Task TestProject_NoneFramework_DisablesRunnerOutputType(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"MyApp.UnitTests",
			preImportProps: "<TestingFramework>None</TestingFramework>",
			cancellationToken: cancellationToken
		);
		await Assert.That(await h.GetPropertyAsync("OutputType", cancellationToken)).IsEqualTo("Library");
	}

	[Test]
	public async Task TestProject_DefaultFrameworkPackages_AreTUnitTUnitMocksAndBogus(
		CancellationToken cancellationToken
	)
	{
		await using var h = await ProjectHarness.CreateAsync("MyApp.UnitTests", cancellationToken: cancellationToken);
		var packageReferences = await h.GetItemIdentitiesAsync("PackageReference", cancellationToken);
		await Assert.That(packageReferences).Contains("TUnit");
		await Assert.That(packageReferences).Contains("TUnit.Mocks");
		await Assert.That(packageReferences).Contains("Bogus");
		await Assert.That(packageReferences).DoesNotContain("NSubstitute");
	}

	[Test]
	public async Task TestProject_NSubstitute_OptIn_SwitchesMockProvider(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"MyApp.UnitTests",
			preImportProps: "<SubstituteFramework>NSubstitute</SubstituteFramework>",
			cancellationToken: cancellationToken
		);
		var packageReferences = await h.GetItemIdentitiesAsync("PackageReference", cancellationToken);
		await Assert.That(packageReferences).Contains("NSubstitute");
		await Assert.That(packageReferences).Contains("NSubstitute.Analyzers.CSharp");
		await Assert.That(packageReferences).DoesNotContain("TUnit.Mocks");
	}

	[Test]
	public async Task TestProject_NoneMockingAndTestData_DisablesBothPackageSets(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"MyApp.UnitTests",
			preImportProps: "<SubstituteFramework>None</SubstituteFramework><TestDataFramework>None</TestDataFramework>",
			cancellationToken: cancellationToken
		);
		var packageReferences = await h.GetItemIdentitiesAsync("PackageReference", cancellationToken);
		await Assert.That(packageReferences).DoesNotContain("TUnit.Mocks");
		await Assert.That(packageReferences).DoesNotContain("NSubstitute");
		await Assert.That(packageReferences).DoesNotContain("Bogus");
	}

	[Test]
	public async Task SharedTestingProject_IsNotATestProject(CancellationToken cancellationToken)
	{
		// Shared testing projects provide helpers but are not runnable test projects.
		await using var h = await ProjectHarness.CreateAsync(
			"SharedTestingFramework",
			cancellationToken: cancellationToken
		);
		var props = await h.GetPropertiesAsync(cancellationToken, "IsTestProject", "IsSharedTestingProject");
		await Assert.That(props["IsSharedTestingProject"]).IsEqualTo("true");
		await Assert.That(props["IsTestProject"]).IsEqualTo("false");
	}
}
