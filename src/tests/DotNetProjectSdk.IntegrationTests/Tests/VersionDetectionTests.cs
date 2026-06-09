using Purview.DotNetProjectSdk.Harness;

namespace Purview.DotNetProjectSdk.Tests;

/// <summary>
/// Verifies SDK version detection from package.json: property defaults, explicit and
/// auto-discovered package.json paths, opt-out behaviour, and build-time validation errors.
/// </summary>
public sealed class VersionDetectionTests
{
	// ---------- property defaults ----------

	[Test]
	public async Task UsePackageJsonVersion_DefaultsToTrue(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync("MyLibrary", cancellationToken: cancellationToken);
		await Assert.That(await h.GetPropertyAsync("UsePackageJsonVersion", cancellationToken)).IsEqualTo("true");
	}

	[Test]
	public async Task UsePackageJsonVersion_CanBeSetFalse(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			extraProps: "<UsePackageJsonVersion>false</UsePackageJsonVersion>",
			cancellationToken: cancellationToken
		);
		await Assert.That(await h.GetPropertyAsync("UsePackageJsonVersion", cancellationToken)).IsEqualTo("false");
	}

	[Test]
	public async Task RootPackageJsonWasSpecified_FalseByDefault(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync("MyLibrary", cancellationToken: cancellationToken);
		await Assert
			.That(await h.GetPropertyAsync("_RootPackageJsonWasSpecified", cancellationToken))
			.IsEqualTo("false");
	}

	[Test]
	public async Task RootPackageJsonWasSpecified_TrueWhenExplicitlySet(CancellationToken cancellationToken)
	{
		// RootPackageJson must be set before Sdk.props is imported (pre-import props),
		// otherwise _RootPackageJsonWasSpecified is evaluated before the project file's
		// own PropertyGroups are processed.
		await using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			preImportProps: "<RootPackageJson>package.json</RootPackageJson>",
			cancellationToken: cancellationToken
		);
		await Assert
			.That(await h.GetPropertyAsync("_RootPackageJsonWasSpecified", cancellationToken))
			.IsEqualTo("true");
	}

	// ---------- explicit RootPackageJson ----------

	[Test]
	public async Task Version_SetFromExplicitPackageJson(CancellationToken cancellationToken)
	{
		// RootPackageJson must be set before Sdk.props import so VersionDetection.props
		// sees the value during its evaluation phase.
		await using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			preImportProps: "<RootPackageJson>package.json</RootPackageJson>",
			cancellationToken: cancellationToken
		);
		await File.WriteAllTextAsync(
			Path.Combine(h.ProjectDirectory, "package.json"),
			"""{"name": "my-lib", "version": "1.2.3"}""",
			cancellationToken
		);

		await Assert.That(await h.GetPropertyAsync("Version", cancellationToken)).IsEqualTo("1.2.3");
	}

	[Test]
	public async Task PackageVersion_SetFromExplicitPackageJson(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			preImportProps: "<RootPackageJson>package.json</RootPackageJson>",
			cancellationToken: cancellationToken
		);
		await File.WriteAllTextAsync(
			Path.Combine(h.ProjectDirectory, "package.json"),
			"""{"name": "my-lib", "version": "4.5.6"}""",
			cancellationToken
		);

		await Assert.That(await h.GetPropertyAsync("PackageVersion", cancellationToken)).IsEqualTo("4.5.6");
	}

	// ---------- auto-discovery via .git marker ----------

	[Test]
	public async Task Version_SetFromAutoDiscoveredPackageJson(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync("MyLibrary", cancellationToken: cancellationToken);

		// Place a .git marker in the project directory so GetDirectoryNameOfFileAbove
		// resolves RepoRoot to the project directory, then put package.json alongside it.
		await File.WriteAllTextAsync(Path.Combine(h.ProjectDirectory, ".git"), "", cancellationToken);
		await File.WriteAllTextAsync(
			Path.Combine(h.ProjectDirectory, "package.json"),
			"""{"name": "my-lib", "version": "7.8.9"}""",
			cancellationToken
		);

		await Assert.That(await h.GetPropertyAsync("Version", cancellationToken)).IsEqualTo("7.8.9");
	}

	[Test]
	public async Task PackageVersion_SetFromAutoDiscoveredPackageJson(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync("MyLibrary", cancellationToken: cancellationToken);

		await File.WriteAllTextAsync(Path.Combine(h.ProjectDirectory, ".git"), "", cancellationToken);
		await File.WriteAllTextAsync(
			Path.Combine(h.ProjectDirectory, "package.json"),
			"""{"name": "my-lib", "version": "3.0.1"}""",
			cancellationToken
		);

		await Assert.That(await h.GetPropertyAsync("PackageVersion", cancellationToken)).IsEqualTo("3.0.1");
	}

	// ---------- opt-out disables version detection ----------

	[Test]
	public async Task Version_NotSetFromPackageJson_WhenUsePackageJsonVersionFalse(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			preImportProps: "<UsePackageJsonVersion>false</UsePackageJsonVersion><RootPackageJson>package.json</RootPackageJson>",
			cancellationToken: cancellationToken
		);
		await File.WriteAllTextAsync(
			Path.Combine(h.ProjectDirectory, "package.json"),
			"""{"name": "my-lib", "version": "9.9.9"}""",
			cancellationToken
		);

		// UsePackageJsonVersion=false → SDK falls back to its own default (0.0.1)
		await Assert.That(await h.GetPropertyAsync("Version", cancellationToken)).IsEqualTo("0.0.1");
	}

	// ---------- ValidatePackageJsonVersion build-time errors ----------

	[Test]
	public async Task Build_Errors_WhenExplicitPackageJsonNotFound(CancellationToken cancellationToken)
	{
		// RootPackageJson must be set pre-import so _RootPackageJsonWasSpecified=true,
		// which is required for ValidatePackageJsonVersion to emit the explicit-path error.
		await using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			preImportProps: "<RootPackageJson>missing-package.json</RootPackageJson>",
			cancellationToken: cancellationToken
		);

		var (success, output, errors) = await h.BuildAsync(cancellationToken: cancellationToken);

		await Assert.That(success).IsFalse();
		await Assert.That(output + errors).Contains("does not exist");
	}

	[Test]
	public async Task Build_Errors_WhenAutoDiscoveredRepoRootHasNoPackageJson(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync("MyLibrary", cancellationToken: cancellationToken);

		// .git marker makes RepoRoot = ProjectDirectory, but no package.json is created.
		await File.WriteAllTextAsync(Path.Combine(h.ProjectDirectory, ".git"), "", cancellationToken);

		var (success, output, errors) = await h.BuildAsync(cancellationToken: cancellationToken);

		await Assert.That(success).IsFalse();
		await Assert.That(output + errors).Contains("no package.json exists at");
	}

	[Test]
	public async Task Build_Errors_WhenPackageJsonHasNoVersionProperty(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			preImportProps: "<RootPackageJson>package.json</RootPackageJson>",
			cancellationToken: cancellationToken
		);
		await File.WriteAllTextAsync(
			Path.Combine(h.ProjectDirectory, "package.json"),
			"""{"name": "my-lib"}""",
			cancellationToken
		);

		var (success, output, errors) = await h.BuildAsync(cancellationToken: cancellationToken);

		await Assert.That(success).IsFalse();
		await Assert.That(output + errors).Contains("no version property could be read");
	}
}
