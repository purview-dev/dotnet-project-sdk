using Purview.DotNetProjectSdk.Harness;

namespace Purview.DotNetProjectSdk.Tests;

/// <summary>
/// Verifies SDK-controlled package and packaging behaviour — IsPackable defaults,
/// SourceLink opt-in/opt-out, and telemetry exclusion flags.
/// </summary>
public sealed class PackageBehaviorTests
{
	[Test]
	public async Task Library_IsPackable_False_ByDefault()
	{
		// The SDK sets IsPackable=false by default; projects must opt in explicitly.
		await using var h = ProjectHarness.Create("MyLibrary");
		await Assert.That(await h.GetPropertyAsync("IsPackable")).IsEqualTo("false");
	}

	[Test]
	public async Task Library_IsPackable_True_WhenExplicitlySet()
	{
		await using var h = ProjectHarness.Create("MyLibrary",
			extraProps: "<IsPackable>true</IsPackable>");
		await Assert.That(await h.GetPropertyAsync("IsPackable")).IsEqualTo("true");
	}

	[Test]
	public async Task TestProject_IsPackable_False()
	{
		await using var h = ProjectHarness.Create("MyApp.UnitTests");
		await Assert.That(await h.GetPropertyAsync("IsPackable")).IsEqualTo("false");
	}

	[Test]
	public async Task SharedTestingProject_IsPackable_False()
	{
		await using var h = ProjectHarness.Create("SharedTestingFramework");
		await Assert.That(await h.GetPropertyAsync("IsPackable")).IsEqualTo("false");
	}

	[Test]
	public async Task ExcludePurviewTelemetry_CanBeSetTrue()
	{
		await using var h = ProjectHarness.Create("MyLibrary",
			extraProps: "<ExcludePurviewTelemetry>true</ExcludePurviewTelemetry>");
		await Assert.That(await h.GetPropertyAsync("ExcludePurviewTelemetry")).IsEqualTo("true");
	}

	[Test]
	public async Task ExcludeMSTelemetryExtension_CanBeSetTrue()
	{
		await using var h = ProjectHarness.Create("MyLibrary",
			extraProps: "<ExcludeMSTelemetryExtension>true</ExcludeMSTelemetryExtension>");
		await Assert.That(await h.GetPropertyAsync("ExcludeMSTelemetryExtension")).IsEqualTo("true");
	}
}
