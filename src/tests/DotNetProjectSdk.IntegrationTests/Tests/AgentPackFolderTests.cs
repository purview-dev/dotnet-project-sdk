using System.IO.Compression;
using System.Text.Json;
using Purview.DotNetProjectSdk.Harness;

namespace Purview.DotNetProjectSdk.Tests;

/// <summary>
/// Verifies the opt-in AgentPack folder packaging workflow.
/// </summary>
public sealed class AgentPackFolderTests
{
	[Test]
	public async Task EnabledAgentFolderInPackage_ExposesAgentPackFolderInProjectTree(
		CancellationToken cancellationToken
	)
	{
		var sdkProjectPath = Path.GetFullPath(
			Path.Combine(SdkPaths.SdkDirectory, "..", "DotNetProjectSdk.csproj")
		);

		var (exitCode, stdOut, stdErr) = await RunProcessAsync(
			"dotnet",
			$"msbuild \"{sdkProjectPath}\" -nologo -noconlog -t:PrepareForBuild -getItem:None",
			Path.GetDirectoryName(sdkProjectPath)!,
			cancellationToken
		);

		await Assert.That(exitCode).IsEqualTo(0).Because(TestHelpers.GenerateError(stdOut, stdErr));

		var jsonStart = stdOut.IndexOf('{', StringComparison.Ordinal);
		await Assert.That(jsonStart >= 0).IsTrue();

		using var doc = JsonDocument.Parse(stdOut[jsonStart..]);
		var noneItems = doc.RootElement.GetProperty("Items").GetProperty("None").EnumerateArray();
		var expectedPath = Path.GetFullPath(
			Path.Combine(
				Path.GetDirectoryName(sdkProjectPath)!,
				"..",
				"..",
				"AgentPack",
				"skills",
				"sdk-configuration-reference",
				"SKILL.md"
			)
		);

		var AgentPackEntry = noneItems.FirstOrDefault(item =>
			string.Equals(
				Path.GetFullPath(item.GetProperty("Identity").GetString()!),
				expectedPath,
				StringComparison.OrdinalIgnoreCase
			)
		);

		await Assert.That(AgentPackEntry.ValueKind).IsEqualTo(JsonValueKind.Object);
		await Assert
			.That(AgentPackEntry.GetProperty("Link").GetString())
			.IsEqualTo("AgentPack\\skills\\sdk-configuration-reference\\SKILL.md");
	}

	[Test]
	public async Task EnabledAgentFolderInPackage_PacksAgentPackSkillsIntoNuGetPackage(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var h = await ProjectHarness.CreateAsync(
			"PackableProject",
			extraProps: "<IsPackable>true</IsPackable><EnabledAgentFolderInPackage>true</EnabledAgentFolderInPackage>",
			cancellationToken: cancellationToken
		);

		await File.WriteAllTextAsync(
			Path.Combine(h.SolutionDirectory, ".git"),
			string.Empty,
			cancellationToken
		);
		await File.WriteAllTextAsync(
			Path.Combine(h.SolutionDirectory, "package.json"),
			/*lang=json,strict*/
			"""{"name": "packable-project", "version": "1.0.0"}""",
			cancellationToken
		);
		await File.WriteAllTextAsync(
			Path.Combine(h.SolutionDirectory, "Directory.Packages.props"),
			"""
			<Project>
				<PropertyGroup>
					<CentralPackageFloatingVersionsEnabled>true</CentralPackageFloatingVersionsEnabled>
				</PropertyGroup>
				<ItemGroup>
					<PackageVersion Include="Microsoft.SourceLink.GitHub" Version="*" />
					<PackageVersion Include="Purview.Telemetry.SourceGenerator" Version="*" />
					<PackageVersion Include="Microsoft.Extensions.Telemetry.Abstractions" Version="*" />
				</ItemGroup>
			</Project>
			""",
			cancellationToken
		);

		var agentPackSkillsDirectory = Path.Combine(
			h.SolutionDirectory,
			"AgentPack",
			"skills",
			"observability"
		);
		Directory.CreateDirectory(agentPackSkillsDirectory);
		await File.WriteAllTextAsync(
			Path.Combine(agentPackSkillsDirectory, "SKILL.md"),
			"# Observability\n",
			cancellationToken
		);

		var feedDirectory = Path.Combine(h.SolutionDirectory, "feed");
		Directory.CreateDirectory(feedDirectory);
		var packageVersion = $"0.0.0-integration-test-{Guid.NewGuid():N}";

		// Act
		var (exitCode, stdOut, stdErr) = await RunProcessAsync(
			"dotnet",
			$"pack \"{h.ProjectFilePath}\" -c Release -o \"{feedDirectory}\" -p:PackageVersion={packageVersion} -p:Version={packageVersion}",
			h.SolutionDirectory,
			cancellationToken
		);

		// Assert
		await Assert.That(exitCode).IsEqualTo(0).Because(TestHelpers.GenerateError(stdOut, stdErr));

		var packagePath = Directory
			.GetFiles(
				feedDirectory,
				$"PackableProject.{packageVersion}.nupkg",
				SearchOption.TopDirectoryOnly
			)
			.SingleOrDefault();

		await Assert
			.That(packagePath)
			.IsNotNull()
			.Because("The packed project package was not created.");

		using var zip = await ZipFile.OpenReadAsync(packagePath!, cancellationToken);
		var entries = zip.Entries.Select(entry => entry.FullName).ToList();
		await Assert.That(entries).Contains("agents/skills/observability/SKILL.md");
		await Assert
			.That(entries)
			.Contains("agents/skills/observability/.gitignore")
			.Because($"{stdOut}\n{stdErr}\n--- package entries ---\n{string.Join("\n", entries)}");

		var gitIgnoreEntry = zip.Entries.Single(entry =>
			entry.FullName == "agents/skills/observability/.gitignore"
		);
		using var gitIgnoreStream = await gitIgnoreEntry.OpenAsync(cancellationToken);
		using var reader = new StreamReader(gitIgnoreStream);
		var gitIgnoreContent = (await reader.ReadToEndAsync(cancellationToken)).ReplaceLineEndings(
			"\n"
		);
		await Assert
			.That(gitIgnoreContent)
			.IsEqualTo(
				"# Ignore all files\n*\n\n# Don't ignore directories, so Git can traverse them\n!*/\n\n# Keep this file\n!.gitignore"
			);
	}

	[Test]
	public async Task EnabledAgentFolderInPackage_PacksAgentPackContentOutsideSkillsIntoNuGetPackage(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var h = await ProjectHarness.CreateAsync(
			"PackableProject",
			extraProps: "<IsPackable>true</IsPackable><EnabledAgentFolderInPackage>true</EnabledAgentFolderInPackage>",
			cancellationToken: cancellationToken
		);

		await File.WriteAllTextAsync(
			Path.Combine(h.SolutionDirectory, ".git"),
			string.Empty,
			cancellationToken
		);
		await File.WriteAllTextAsync(
			Path.Combine(h.SolutionDirectory, "package.json"),
			/*lang=json,strict*/
			"""{"name": "packable-project", "version": "1.0.0"}""",
			cancellationToken
		);
		await File.WriteAllTextAsync(
			Path.Combine(h.SolutionDirectory, "Directory.Packages.props"),
			"""
			<Project>
				<PropertyGroup>
					<CentralPackageFloatingVersionsEnabled>true</CentralPackageFloatingVersionsEnabled>
				</PropertyGroup>
				<ItemGroup>
					<PackageVersion Include="Microsoft.SourceLink.GitHub" Version="*" />
					<PackageVersion Include="Purview.Telemetry.SourceGenerator" Version="*" />
					<PackageVersion Include="Microsoft.Extensions.Telemetry.Abstractions" Version="*" />
				</ItemGroup>
			</Project>
			""",
			cancellationToken
		);

		var AgentPackPromptsDirectory = Path.Combine(
			h.SolutionDirectory,
			"AgentPack",
			"prompts",
			"example"
		);
		Directory.CreateDirectory(AgentPackPromptsDirectory);
		await File.WriteAllTextAsync(
			Path.Combine(AgentPackPromptsDirectory, "PROMPT.md"),
			"# Prompt\n",
			cancellationToken
		);

		var feedDirectory = Path.Combine(h.SolutionDirectory, "feed");
		Directory.CreateDirectory(feedDirectory);
		var packageVersion = $"0.0.0-integration-test-{Guid.NewGuid():N}";

		// Act
		var (exitCode, stdOut, stdErr) = await RunProcessAsync(
			"dotnet",
			$"pack \"{h.ProjectFilePath}\" -c Release -o \"{feedDirectory}\" -p:PackageVersion={packageVersion} -p:Version={packageVersion}",
			h.SolutionDirectory,
			cancellationToken
		);

		// Assert
		await Assert.That(exitCode).IsEqualTo(0).Because(TestHelpers.GenerateError(stdOut, stdErr));

		var packagePath = Directory
			.GetFiles(
				feedDirectory,
				$"PackableProject.{packageVersion}.nupkg",
				SearchOption.TopDirectoryOnly
			)
			.SingleOrDefault();

		await Assert
			.That(packagePath)
			.IsNotNull()
			.Because("The packed project package was not created.");

		using var zip = await ZipFile.OpenReadAsync(packagePath!, cancellationToken);
		var entries = zip.Entries.Select(entry => entry.FullName).ToList();
		await Assert.That(entries).Contains("agents/prompts/example/PROMPT.md");
		await Assert
			.That(entries)
			.Contains("agents/prompts/example/.gitignore")
			.Because($"{stdOut}\n{stdErr}\n--- package entries ---\n{string.Join("\n", entries)}");
	}

	[Test]
	public async Task EnabledAgentFolderInPackage_ErrorsWhenAgentPackFolderIsMissing(
		CancellationToken cancellationToken
	)
	{
		// Arrange
		using var h = await ProjectHarness.CreateAsync(
			"PackableProject",
			extraProps: "<IsPackable>true</IsPackable><EnabledAgentFolderInPackage>true</EnabledAgentFolderInPackage>",
			cancellationToken: cancellationToken
		);

		await File.WriteAllTextAsync(
			Path.Combine(h.SolutionDirectory, ".git"),
			string.Empty,
			cancellationToken
		);
		await File.WriteAllTextAsync(
			Path.Combine(h.SolutionDirectory, "package.json"),
			/*lang=json,strict*/
			"""{"name": "packable-project", "version": "1.0.0"}""",
			cancellationToken
		);
		await File.WriteAllTextAsync(
			Path.Combine(h.SolutionDirectory, "Directory.Packages.props"),
			"""
			<Project>
				<PropertyGroup>
					<CentralPackageFloatingVersionsEnabled>true</CentralPackageFloatingVersionsEnabled>
				</PropertyGroup>
				<ItemGroup>
					<PackageVersion Include="Microsoft.SourceLink.GitHub" Version="*" />
					<PackageVersion Include="Purview.Telemetry.SourceGenerator" Version="*" />
					<PackageVersion Include="Microsoft.Extensions.Telemetry.Abstractions" Version="*" />
				</ItemGroup>
			</Project>
			""",
			cancellationToken
		);
		var feedDirectory = Path.Combine(h.SolutionDirectory, "feed");
		Directory.CreateDirectory(feedDirectory);
		var packageVersion = $"0.0.0-integration-test-{Guid.NewGuid():N}";

		// Act
		var (exitCode, stdOut, stdErr) = await RunProcessAsync(
			"dotnet",
			$"pack \"{h.ProjectFilePath}\" -c Release -o \"{feedDirectory}\" -p:PackageVersion={packageVersion} -p:Version={packageVersion}",
			h.SolutionDirectory,
			cancellationToken
		);

		// Assert
		await Assert.That(exitCode).IsNotEqualTo(0);
		await Assert.That(stdOut + stdErr).Contains("EnabledAgentFolderInPackage is true");
		await Assert.That(stdOut + stdErr).Contains("AgentPack folder was not found");
	}

	static async Task<(int Code, string StdOut, string StdErr)> RunProcessAsync(
		string fileName,
		string arguments,
		string workingDirectory,
		CancellationToken cancellationToken
	)
	{
		using var process = new System.Diagnostics.Process
		{
			StartInfo = new System.Diagnostics.ProcessStartInfo
			{
				FileName = fileName,
				Arguments = arguments,
				WorkingDirectory = workingDirectory,
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

		return (process.ExitCode, await stdoutTask, await stderrTask);
	}
}
