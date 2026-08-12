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
		using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			cancellationToken: cancellationToken
		);
		await Assert
			.That(await h.GetPropertyAsync("Nullable", cancellationToken))
			.IsEqualTo("enable");
	}

	[Test]
	public async Task ImplicitUsings_Enabled_ByDefault(CancellationToken cancellationToken)
	{
		using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			cancellationToken: cancellationToken
		);
		await Assert
			.That(await h.GetPropertyAsync("ImplicitUsings", cancellationToken))
			.IsEqualTo("enable");
	}

	[Test]
	public async Task LangVersion_Preview_ByDefault(CancellationToken cancellationToken)
	{
		using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			cancellationToken: cancellationToken
		);
		// The SDK sets LangVersion to "preview" unless explicitly overridden.
		await Assert
			.That(await h.GetPropertyAsync("LangVersion", cancellationToken))
			.IsEqualTo("preview");
	}

	[Test]
	public async Task WarnOnPackingNonPackableProject_WhenPackableIsFalse_DefaultsToFalse(
		CancellationToken cancellationToken
	)
	{
		using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			extraProps: "<IsPackable>false</IsPackable>",
			cancellationToken: cancellationToken
		);
		// The SDK sets LangVersion to "preview" unless explicitly overridden.
		await Assert
			.That(await h.GetPropertyAsync("WarnOnPackingNonPackableProject", cancellationToken))
			.IsEqualTo("false");
	}

	[Test]
	public async Task Deterministic_True_ByDefault(CancellationToken cancellationToken)
	{
		using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			cancellationToken: cancellationToken
		);
		await Assert
			.That(await h.GetPropertyAsync("Deterministic", cancellationToken))
			.IsEqualTo("true");
	}

	[Test]
	public async Task ManagePackageVersionsCentrally_True_ByDefault(
		CancellationToken cancellationToken
	)
	{
		using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			cancellationToken: cancellationToken
		);
		await Assert
			.That(await h.GetPropertyAsync("ManagePackageVersionsCentrally", cancellationToken))
			.IsEqualTo("true");
	}

	[Test]
	public async Task RootNamespace_DerivedFromNamespacePrefixAndProjectName(
		CancellationToken cancellationToken
	)
	{
		// NamespacePrefix=Test, ProjectName=MyLibrary → Test.MyLibrary
		using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			namespacePrefix: "Test",
			cancellationToken: cancellationToken
		);
		await Assert
			.That(await h.GetPropertyAsync("RootNamespace", cancellationToken))
			.IsEqualTo("Test.MyLibrary");
	}

	[Test]
	public async Task Ci_PropertySet_WhenEnvironmentVariablePresent(
		CancellationToken cancellationToken
	)
	{
		using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			extraEnv: new Dictionary<string, string> { ["CI"] = "true" },
			cancellationToken: cancellationToken
		);
		await Assert
			.That(await h.GetPropertyAsync("ContinuousIntegrationBuild", cancellationToken))
			.IsEqualTo("true");
	}

	[Test]
	public async Task Ci_PropertyNotSet_WhenEnvironmentVariableAbsent(
		CancellationToken cancellationToken
	)
	{
		// Ensure CI env var is not set for this test (it may be set in CI environments, so
		// we override with empty string to simulate a local dev machine).
		using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			extraEnv: new Dictionary<string, string> { ["CI"] = "" },
			cancellationToken: cancellationToken
		);
		var value = await h.GetPropertyAsync("ContinuousIntegrationBuild", cancellationToken);
		// ContinuousIntegrationBuild should be empty (not "true") when CI is not set.
		await Assert.That(value).IsNotEqualTo("true");
	}

	[Test]
	public async Task EditorConfigFilePath_PointsToExistingSdkEditorConfig(
		CancellationToken cancellationToken
	)
	{
		using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			cancellationToken: cancellationToken
		);
		var editorConfigPath = await h.GetPropertyAsync("EditorConfigFilePath", cancellationToken);

		await Assert.That(string.IsNullOrWhiteSpace(editorConfigPath)).IsFalse();
		await Assert.That(File.Exists(editorConfigPath)).IsTrue();
	}

	[Test]
	public async Task EditorConfigFiles_Contains_SdkEditorConfig(
		CancellationToken cancellationToken
	)
	{
		using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			cancellationToken: cancellationToken
		);
		var editorConfigPath = await h.GetPropertyAsync("EditorConfigFilePath", cancellationToken);
		var editorConfigFiles = await h.GetItemIdentitiesAsync(
			"EditorConfigFiles",
			cancellationToken
		);

		var normalizedEditorConfigPath = Path.GetFullPath(editorConfigPath).TrimEnd('\\', '/');
		var hasSdkEditorConfig = editorConfigFiles.Any(path =>
			Path.GetFullPath(path).TrimEnd('\\', '/') == normalizedEditorConfigPath
		);

		await Assert.That(hasSdkEditorConfig).IsTrue();
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
		using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			targetFramework: tfm,
			cancellationToken: cancellationToken
		);
		await Assert
			.That(await h.GetPropertyAsync("TargetFramework", cancellationToken))
			.IsEqualTo(expected);
	}

	[Test]
	public async Task PublishRepositoryUrl_True_ByDefault(CancellationToken cancellationToken)
	{
		using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			cancellationToken: cancellationToken
		);
		await Assert
			.That(await h.GetPropertyAsync("PublishRepositoryUrl", cancellationToken))
			.IsEqualTo("true");
	}

	[Test]
	public async Task IncludeSymbols_True_ByDefault(CancellationToken cancellationToken)
	{
		using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			cancellationToken: cancellationToken
		);
		await Assert
			.That(await h.GetPropertyAsync("IncludeSymbols", cancellationToken))
			.IsEqualTo("true");
	}

	[Test]
	public async Task SymbolPackageFormat_Snupkg_ByDefault(CancellationToken cancellationToken)
	{
		using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			cancellationToken: cancellationToken
		);
		await Assert
			.That(await h.GetPropertyAsync("SymbolPackageFormat", cancellationToken))
			.IsEqualTo("snupkg");
	}

	[Test]
	public async Task AnalysisLevel_Latest_ByDefault(CancellationToken cancellationToken)
	{
		using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			cancellationToken: cancellationToken
		);
		await Assert
			.That(await h.GetPropertyAsync("AnalysisLevel", cancellationToken))
			.IsEqualTo("latest");
	}

	[Test]
	public async Task AnalysisMode_All_ByDefault(CancellationToken cancellationToken)
	{
		using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			cancellationToken: cancellationToken
		);
		await Assert
			.That(await h.GetPropertyAsync("AnalysisMode", cancellationToken))
			.IsEqualTo("All");
	}

	[Test]
	public async Task EnableNetAnalyzers_True_ByDefault(CancellationToken cancellationToken)
	{
		using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			cancellationToken: cancellationToken
		);
		await Assert
			.That(await h.GetPropertyAsync("EnableNETAnalyzers", cancellationToken))
			.IsEqualTo("true");
	}

	[Test]
	public async Task EnforceCodeStyleInBuild_True_ByDefault(CancellationToken cancellationToken)
	{
		using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			cancellationToken: cancellationToken
		);
		await Assert
			.That(await h.GetPropertyAsync("EnforceCodeStyleInBuild", cancellationToken))
			.IsEqualTo("true");
	}

	[Test]
	public async Task Nullable_CanBeOverriddenInProjectFile(CancellationToken cancellationToken)
	{
		using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			extraProps: "<Nullable>disable</Nullable>",
			cancellationToken: cancellationToken
		);
		await Assert
			.That(await h.GetPropertyAsync("Nullable", cancellationToken))
			.IsEqualTo("disable");
	}

	[Test]
	public async Task Nullable_CanBeOverriddenInDirectoryBuildProps(
		CancellationToken cancellationToken
	)
	{
		using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			preImportProps: "<Nullable>disable</Nullable>",
			cancellationToken: cancellationToken
		);
		await Assert
			.That(await h.GetPropertyAsync("Nullable", cancellationToken))
			.IsEqualTo("disable");
	}
}
