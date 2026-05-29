namespace Purview.DotNetProjectSdk.Tests;

/// <summary>
/// Verifies that Sdk.props correctly classifies project types from file names, Sdk attributes,
/// and the presence of a Dockerfile — all without triggering a build.
/// </summary>
public sealed class ProjectClassificationTests
{
	[Test]
	public async Task CSharpProject_IsCSharpProject_True()
	{
		await using var h = ProjectHarness.Create("MyLibrary");
		await Assert.That(await h.GetPropertyAsync("IsCSharpProject")).IsEqualTo("true");
	}

	[Test]
	[Arguments("MyApp.UnitTests", "Unit", true)]
	[Arguments("MyApp.IntegrationTests", "Integration", true)]
	[Arguments("MyApp.AcceptanceTests", "Acceptance", true)]
	[Arguments("MyApp.SmokeTests", "Smoke", true)]
	[Arguments("MyApp.PerformanceTests", "Performance", true)]
	[Arguments("MyApp.FunctionalTest", "Functional", true)]    // singular "Test"
	[Arguments("MyApp", "", false)]
	[Arguments("MyApp.Core", "", false)]
	public async Task TestProject_Detection_ByNamingConvention(
		string projectName, string expectedTestingType, bool expectedIsTest)
	{
		await using var h = ProjectHarness.Create(projectName);
		var props = await h.GetPropertiesAsync("IsTestProject", "TestingType");

		await Assert.That(props["IsTestProject"]).IsEqualTo(expectedIsTest ? "true" : "false");
		await Assert.That(props["TestingType"]).IsEqualTo(expectedTestingType);
	}

	[Test]
	[Arguments("SharedTestingFramework", true)]
	[Arguments("SharedTestingInfrastructure", true)]
	[Arguments("SharedTestingInfra", true)]
	[Arguments("SharedTestingUtilities", true)]
	[Arguments("SharedTestingLibrary", true)]
	[Arguments("SharedTestingLib", true)]
	[Arguments("SharedTestingHelpers", true)]
	[Arguments("MyRegularLib", false)]
	public async Task SharedTestingProject_Detection_ByWellKnownNames(
		string projectName, bool expectedIsShared)
	{
		await using var h = ProjectHarness.Create(projectName);
		await Assert.That(await h.GetPropertyAsync("IsSharedTestingProject"))
			.IsEqualTo(expectedIsShared ? "true" : "false");
	}

	[Test]
	public async Task ContainerProject_DetectedByDockerfile()
	{
		await using var h = ProjectHarness.Create("MyService", withDockerfile: true);
		await Assert.That(await h.GetPropertyAsync("IsContainerProject")).IsEqualTo("true");
	}

	[Test]
	public async Task ContainerProject_NotDetected_WithoutDockerfile()
	{
		await using var h = ProjectHarness.Create("MyService");
		await Assert.That(await h.GetPropertyAsync("IsContainerProject")).IsEqualTo("false");
	}

	[Test]
	public async Task WebSdkProject_DetectedBySdkAttribute()
	{
		await using var h = ProjectHarness.Create("MyWebApp", sdk: "Microsoft.NET.Sdk.Web");
		var props = await h.GetPropertiesAsync("IsSdkProject", "IsWebSdkProject", "IsWorkerSdkProject");

		await Assert.That(props["IsSdkProject"]).IsEqualTo("true");
		await Assert.That(props["IsWebSdkProject"]).IsEqualTo("true");
		await Assert.That(props["IsWorkerSdkProject"]).IsEqualTo("false");
	}

	[Test]
	public async Task WorkerSdkProject_DetectedBySdkAttribute()
	{
		await using var h = ProjectHarness.Create("MyWorker", sdk: "Microsoft.NET.Sdk.Worker");
		var props = await h.GetPropertiesAsync("IsSdkProject", "IsWebSdkProject", "IsWorkerSdkProject");

		await Assert.That(props["IsSdkProject"]).IsEqualTo("true");
		await Assert.That(props["IsWebSdkProject"]).IsEqualTo("false");
		await Assert.That(props["IsWorkerSdkProject"]).IsEqualTo("true");
	}

	/// <summary>
	/// The SDK reads the raw .csproj file content with a regex to detect the Sdk attribute.
	/// For an Aspire host project that uses a bare &lt;Project&gt; (no Sdk attribute, so that
	/// the Aspire SDK is not accidentally loaded in tests), we can detect IsAspireHostProject
	/// by embedding the marker in a comment with two whitespace chars before &lt;Project.
	/// The project must also explicitly import Sdk.props since bare projects don't get
	/// Directory.Build.props auto-imported by MSBuild.
	/// </summary>
	[Test]
	public async Task AspireHostProject_DetectedByFileContentMarker()
	{
		var content = $"""
			<Project>
				<Import Project="{SdkPaths.SdkDirectory}/Sdk.props" />
				<PropertyGroup>
					<NamespacePrefix>Test</NamespacePrefix>
					<DisableNamespacePrefixCheck>true</DisableNamespacePrefixCheck>
					<TargetFramework>net10.0</TargetFramework>
				</PropertyGroup>
				<!--
				  <Project Sdk="Aspire.Sdk.Host" />
				-->
			</Project>
			""";

		await using var h = ProjectHarness.CreateWithContent("Acme.AppHost", content);
		await Assert.That(await h.GetPropertyAsync("IsAspireHostProject")).IsEqualTo("true");
	}

	[Test]
	public async Task RegularProject_IsNotAspireHostProject()
	{
		await using var h = ProjectHarness.Create("Acme.AppHost");
		await Assert.That(await h.GetPropertyAsync("IsAspireHostProject")).IsEqualTo("false");
	}
}
