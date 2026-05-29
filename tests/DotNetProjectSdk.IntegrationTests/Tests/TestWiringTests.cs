using Purview.DotNetProjectSdk.Harness;

namespace Purview.DotNetProjectSdk.Tests;

/// <summary>
/// Verifies that the SDK wires the correct test framework packages and output type
/// for projects that match the test-project naming convention.
/// </summary>
public sealed class TestWiringTests
{
	[Test]
	public async Task TestProject_OutputType_IsExe()
	{
		// Test projects using Microsoft.Testing.Platform must be executables.
		await using var h = ProjectHarness.Create("MyApp.UnitTests");
		await Assert.That(await h.GetPropertyAsync("OutputType")).IsEqualTo("Exe");
	}

	[Test]
	public async Task TestProject_DefaultTestingFramework_IsTUnit()
	{
		await using var h = ProjectHarness.Create("MyApp.UnitTests");
		await Assert.That(await h.GetPropertyAsync("ProjectSdkTestFramework")).IsEqualTo("TUnit");
	}

	[Test]
	public async Task TestProject_XUnit_WhenOptedIn()
	{
		await using var h = ProjectHarness.Create("MyApp.UnitTests",
			extraProps: "<ProjectSdkTestFramework>XUnit</ProjectSdkTestFramework>");
		await Assert.That(await h.GetPropertyAsync("ProjectSdkTestFramework")).IsEqualTo("XUnit");
	}

	[Test]
	public async Task SharedTestingProject_IsNotATestProject()
	{
		// Shared testing projects provide helpers but are not runnable test projects.
		await using var h = ProjectHarness.Create("SharedTestingFramework");
		var props = await h.GetPropertiesAsync("IsTestProject", "IsSharedTestingProject");
		await Assert.That(props["IsSharedTestingProject"]).IsEqualTo("true");
		await Assert.That(props["IsTestProject"]).IsEqualTo("false");
	}
}
