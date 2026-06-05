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
		await Assert.That(await h.GetPropertyAsync("ProjectSdkTestFramework", cancellationToken)).IsEqualTo("TUnit");
	}

	[Test]
	public async Task TestProject_XUnit_WhenOptedIn(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"MyApp.UnitTests",
			extraProps: "<ProjectSdkTestFramework>XUnit</ProjectSdkTestFramework>",
			cancellationToken: cancellationToken
		);
		await Assert.That(await h.GetPropertyAsync("ProjectSdkTestFramework", cancellationToken)).IsEqualTo("XUnit");
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
