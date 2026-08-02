using Purview.DotNetProjectSdk.Harness;

namespace Purview.DotNetProjectSdk.Tests;

/// <summary>
/// Verifies SDK version detection from package.json: property defaults, explicit and
/// auto-discovered package.json paths, opt-out behaviour, and build-time validation errors.
/// </summary>
public sealed partial class VersionDetectionTests
{
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
	public async Task UsePackageJsonVersion_CanBeSetStrict(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			extraProps: "<UsePackageJsonVersion>Strict</UsePackageJsonVersion>",
			cancellationToken: cancellationToken
		);
		await Assert.That(await h.GetPropertyAsync("UsePackageJsonVersion", cancellationToken)).IsEqualTo("Strict");
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

	[Test]
	public async Task Version_SetFromGitHubWorkspacePackageJson_WhenGitMarkersAreUnavailable(
		CancellationToken cancellationToken
	)
	{
		var workspaceRoot = Path.Combine(Path.GetTempPath(), $"PurviewSdkWorkspace_{Guid.NewGuid():N}");

		Directory.CreateDirectory(workspaceRoot);

		try
		{
			await using var h = await ProjectHarness.CreateAsync(
				"MyLibrary",
				extraEnv: new Dictionary<string, string> { ["GITHUB_WORKSPACE"] = workspaceRoot },
				cancellationToken: cancellationToken
			);

			await File.WriteAllTextAsync(
				Path.Combine(workspaceRoot, "package.json"),
				"""{"name": "my-lib", "version": "2.4.6"}""",
				cancellationToken
			);

			await Assert.That(await h.GetPropertyAsync("Version", cancellationToken)).IsEqualTo("2.4.6");
		}
		finally
		{
			if (Directory.Exists(workspaceRoot))
				Directory.Delete(workspaceRoot, recursive: true);
		}
	}

	[Test]
	public async Task VersionDetection_UsesSessionCache_WhenGitMarkerIsRemoved(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync("MyLibrary", cancellationToken: cancellationToken);

		await File.WriteAllTextAsync(Path.Combine(h.ProjectDirectory, ".git"), "", cancellationToken);
		await File.WriteAllTextAsync(
			Path.Combine(h.ProjectDirectory, "package.json"),
			"""{"name": "my-lib", "version": "6.7.8"}""",
			cancellationToken
		);

		var cacheFile = await h.GetPropertyAsync("VersionDetectionCacheFile", cancellationToken);
		await Assert.That(cacheFile).IsNotEqualTo(string.Empty);

		using var process = new System.Diagnostics.Process
		{
			StartInfo = new System.Diagnostics.ProcessStartInfo
			{
				FileName = "dotnet",
				Arguments = $"msbuild \"{h.ProjectFilePath}\" -nologo -t:WriteVersionDetectionCache",
				WorkingDirectory = h.ProjectDirectory,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			},
		};

		process.Start();
		var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
		var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
		await process.WaitForExitAsync(cancellationToken);
		_ = (await stdoutTask) + (await stderrTask);

		await Assert.That(process.ExitCode).IsEqualTo(0);
		await Assert.That(File.Exists(cacheFile)).IsTrue();

		File.Delete(Path.Combine(h.ProjectDirectory, ".git"));

		// If cache isn't used, RepoRoot discovery fails and Version falls back to 0.0.1.
		await Assert.That(await h.GetPropertyAsync("Version", cancellationToken)).IsEqualTo("6.7.8");
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

	[Test]
	public async Task Build_LogsDetectedVersion_WithSourceFileAndVersion(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			preImportProps: "<RootPackageJson>package.json</RootPackageJson>",
			cancellationToken: cancellationToken
		);

		await File.WriteAllTextAsync(
			Path.Combine(h.ProjectDirectory, "package.json"),
			"""{"name": "my-lib", "version": "5.4.3"}""",
			cancellationToken
		);

		using var process = new System.Diagnostics.Process
		{
			StartInfo = new System.Diagnostics.ProcessStartInfo
			{
				FileName = "dotnet",
				Arguments = $"msbuild \"{h.ProjectFilePath}\" -nologo -v:minimal -t:ValidatePackageJsonVersion",
				WorkingDirectory = h.ProjectDirectory,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			},
		};

		process.Start();
		var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
		var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
		await process.WaitForExitAsync(cancellationToken);

		var output = (await stdoutTask) + (await stderrTask);

		await Assert.That(process.ExitCode).IsEqualTo(0);
		await Assert.That(output).Contains("Detected package version '5.4.3' from");
		await Assert.That(output).Contains("package.json");
	}

	[Test]
	public async Task Build_LogsDetectedVersion_OnlyOncePerSessionId(CancellationToken cancellationToken)
	{
		const string sessionId = "it-session-123";

		await using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			preImportProps: $"<RootPackageJson>package.json</RootPackageJson><VersionDetectionLogSessionId>{sessionId}</VersionDetectionLogSessionId>",
			cancellationToken: cancellationToken
		);

		await File.WriteAllTextAsync(
			Path.Combine(h.ProjectDirectory, "package.json"),
			"""{"name": "my-lib", "version": "5.5.5"}""",
			cancellationToken
		);

		var stampFile = await h.GetPropertyAsync("VersionDetectionLogStampFile", cancellationToken);
		if (!string.IsNullOrWhiteSpace(stampFile) && File.Exists(stampFile))
			File.Delete(stampFile);

		using var firstProcess = new System.Diagnostics.Process
		{
			StartInfo = new System.Diagnostics.ProcessStartInfo
			{
				FileName = "dotnet",
				Arguments = $"msbuild \"{h.ProjectFilePath}\" -nologo -v:minimal -t:ValidatePackageJsonVersion",
				WorkingDirectory = h.ProjectDirectory,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			},
		};

		firstProcess.Start();
		var firstStdOutTask = firstProcess.StandardOutput.ReadToEndAsync(cancellationToken);
		var firstStdErrTask = firstProcess.StandardError.ReadToEndAsync(cancellationToken);
		await firstProcess.WaitForExitAsync(cancellationToken);

		var firstOutput = (await firstStdOutTask) + (await firstStdErrTask);

		await Assert.That(firstProcess.ExitCode).IsEqualTo(0);
		await Assert.That(firstOutput).Contains("Detected package version '5.5.5' from");

		using var secondProcess = new System.Diagnostics.Process
		{
			StartInfo = new System.Diagnostics.ProcessStartInfo
			{
				FileName = "dotnet",
				Arguments = $"msbuild \"{h.ProjectFilePath}\" -nologo -v:minimal -t:ValidatePackageJsonVersion",
				WorkingDirectory = h.ProjectDirectory,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			},
		};

		secondProcess.Start();
		var secondStdOutTask = secondProcess.StandardOutput.ReadToEndAsync(cancellationToken);
		var secondStdErrTask = secondProcess.StandardError.ReadToEndAsync(cancellationToken);
		await secondProcess.WaitForExitAsync(cancellationToken);

		var secondOutput = (await secondStdOutTask) + (await secondStdErrTask);

		await Assert.That(secondProcess.ExitCode).IsEqualTo(0);
		await Assert.That(secondOutput).DoesNotContain("Detected package version '5.5.5' from");
	}

	[Test]
	public async Task VersionDetectionLogStampFile_IsSet_WhenNoExternalSessionIdIsProvided(
		CancellationToken cancellationToken
	)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			extraEnv: new Dictionary<string, string> { ["DOTNET_CLI_CONTEXT_SESSIONID"] = string.Empty },
			cancellationToken: cancellationToken
		);

		await File.WriteAllTextAsync(Path.Combine(h.ProjectDirectory, ".git"), string.Empty, cancellationToken);
		await File.WriteAllTextAsync(
			Path.Combine(h.ProjectDirectory, "package.json"),
			"""{"name": "my-lib", "version": "4.4.4"}""",
			cancellationToken
		);

		var properties = await h.GetPropertiesAsync(
			cancellationToken,
			"RootPackageJson",
			"VersionDetectionLogStampFile"
		);

		await Assert.That(properties["RootPackageJson"]).IsNotEqualTo(string.Empty);
		await Assert.That(properties["VersionDetectionLogStampFile"]).IsNotEqualTo(string.Empty);
	}

	[Test]
	public async Task Build_LogsDetectedVersion_ExactlyOnce_DuringBuildOperation(CancellationToken cancellationToken)
	{
		// Arrange
		const string dotnetCliSessionId = "it-dotnet-cli-session-001";
		await using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			targetFramework: "net10.0",
			extraProps: "<ExcludePurviewTelemetry>true</ExcludePurviewTelemetry><ExcludeMSTelemetryExtension>true</ExcludeMSTelemetryExtension><DisableSourceLink>true</DisableSourceLink>",
			preImportProps: "<RootPackageJson>package.json</RootPackageJson>",
			extraEnv: new Dictionary<string, string> { ["DOTNET_CLI_CONTEXT_SESSIONID"] = dotnetCliSessionId },
			cancellationToken: cancellationToken
		);

		await File.WriteAllTextAsync(
			Path.Combine(h.ProjectDirectory, "package.json"),
			"""{"name": "my-lib", "version": "6.6.6"}""",
			cancellationToken
		);

		var stampFile = await h.GetPropertyAsync("VersionDetectionLogStampFile", cancellationToken);
		if (!string.IsNullOrWhiteSpace(stampFile) && File.Exists(stampFile))
			File.Delete(stampFile);

		// Act
		using var process = new System.Diagnostics.Process
		{
			StartInfo = new System.Diagnostics.ProcessStartInfo
			{
				FileName = "dotnet",
				Arguments = $"build \"{h.ProjectFilePath}\" -nologo -v:minimal -t:Build;Build",
				WorkingDirectory = h.ProjectDirectory,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			},
		};

		process.StartInfo.Environment["DOTNET_CLI_CONTEXT_SESSIONID"] = dotnetCliSessionId;

		process.Start();
		var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
		var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
		await process.WaitForExitAsync(cancellationToken);

		var output = await stdoutTask;
		var errors = await stderrTask;

		// Assert
		await Assert.That(process.ExitCode).IsEqualTo(0).Because(output + errors);

		var combinedOutput = output + errors;
		var messageCount = DetectVersion().Count(combinedOutput);

		await Assert.That(messageCount).IsEqualTo(1).Because(combinedOutput);
	}

	[Test]
	public async Task Build_Errors_WhenUsePackageJsonVersionStrict_AndNoPackageJsonSourceIsFound(
		CancellationToken cancellationToken
	)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			preImportProps: "<UsePackageJsonVersion>Strict</UsePackageJsonVersion>",
			cancellationToken: cancellationToken
		);

		using var process = new System.Diagnostics.Process
		{
			StartInfo = new System.Diagnostics.ProcessStartInfo
			{
				FileName = "dotnet",
				Arguments = $"msbuild \"{h.ProjectFilePath}\" -nologo -v:minimal -t:ValidatePackageJsonVersion",
				WorkingDirectory = h.ProjectDirectory,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			},
		};

		process.Start();
		var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
		var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
		await process.WaitForExitAsync(cancellationToken);

		var output = (await stdoutTask) + (await stderrTask);

		await Assert.That(process.ExitCode).IsNotEqualTo(0);
		await Assert.That(output).Contains("UsePackageJsonVersion=Strict requires resolving version from package.json");
	}

	[Test]
	public async Task Build_Strict_Succeeds_WhenRepoHasGitDirectoryMarkerAndRootPackageJson(
		CancellationToken cancellationToken
	)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"MyLibrary",
			preImportProps: "<UsePackageJsonVersion>Strict</UsePackageJsonVersion>",
			cancellationToken: cancellationToken
		);

		Directory.CreateDirectory(Path.Combine(h.ProjectDirectory, ".git"));
		await File.WriteAllTextAsync(
			Path.Combine(h.ProjectDirectory, ".git", "HEAD"),
			"ref: refs/heads/main",
			cancellationToken
		);
		await File.WriteAllTextAsync(
			Path.Combine(h.ProjectDirectory, "package.json"),
			"""{"name": "my-lib", "version": "8.8.8"}""",
			cancellationToken
		);

		using var process = new System.Diagnostics.Process
		{
			StartInfo = new System.Diagnostics.ProcessStartInfo
			{
				FileName = "dotnet",
				Arguments = $"msbuild \"{h.ProjectFilePath}\" -nologo -v:minimal -t:ValidatePackageJsonVersion",
				WorkingDirectory = h.ProjectDirectory,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			},
		};

		process.Start();
		var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
		var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
		await process.WaitForExitAsync(cancellationToken);

		var output = (await stdoutTask) + (await stderrTask);

		await Assert.That(process.ExitCode).IsEqualTo(0);
		await Assert.That(output).Contains("Detected package version '8.8.8' from");
	}

	[System.Text.RegularExpressions.GeneratedRegex("Detected package version '6.6.6' from"
	)]
	private static partial System.Text.RegularExpressions.Regex DetectVersion();
}
