namespace Purview.DotNetProjectSdk.Tests;

/// <summary>
/// Verifies that Shared*.csproj projects are automatically referenced by all non-test
/// and non-shared projects. This tests the SDK's auto-discovery and auto-referencing
/// feature that allows projects to transparently depend on shared libraries without
/// explicit ProjectReference declarations.
/// </summary>
public sealed class AutoSharedProjectReferencingTests
{
	/// <summary>
	/// Normalizes a path to use forward slashes for consistent comparison.
	/// </summary>
	static string NormalizePath(string path) => path.Replace('\\', '/');

	/// <summary>
	/// Helper to create a complete project structure with SDK boilerplate,
	/// then evaluate the project to get items.
	/// </summary>
	static async Task<(
		SimpleProjectHarness Harness,
		IReadOnlyList<string> ProjectReferences
	)> CreateProjectStructureAsync(
		string projectName,
		Action<string>? createSiblings = null,
		CancellationToken cancellationToken = default
	)
	{
		var workDir = Path.Combine(Path.GetTempPath(), "PurviewSdkTests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(workDir);

		// Create the main project directory
		var projectDir = Path.Combine(workDir, projectName);
		Directory.CreateDirectory(projectDir);

		// Allow caller to create sibling projects before we set up the SDK
		createSiblings?.Invoke(workDir);

		// Now create the SDK boilerplate for the main project
		await File.WriteAllTextAsync(
			Path.Combine(projectDir, "Directory.Build.props"),
			$"""
			<Project>
				<PropertyGroup>
					<NamespacePrefix>Test</NamespacePrefix>
				</PropertyGroup>
				<Import Project="{SdkPaths.SdkDirectory}/Sdk.props" />
			</Project>
			""",
			cancellationToken
		);

		await File.WriteAllTextAsync(
			Path.Combine(projectDir, "Directory.Build.targets"),
			$"""
			<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
				<Import Project="{SdkPaths.SdkDirectory}/Sdk.targets" />
			</Project>
			""",
			cancellationToken
		);

		await File.WriteAllTextAsync(
			Path.Combine(projectDir, "Directory.Packages.props"),
			"""
			<Project>
				<PropertyGroup>
					<CentralPackageFloatingVersionsEnabled>true</CentralPackageFloatingVersionsEnabled>
				</PropertyGroup>
			</Project>
			""",
			cancellationToken
		);

		// Create the project file
		await File.WriteAllTextAsync(
			Path.Combine(projectDir, $"{projectName}.csproj"),
			"""
			<Project Sdk="Microsoft.NET.Sdk">
				<PropertyGroup>
					<TargetFramework>net10.0</TargetFramework>
				</PropertyGroup>
			</Project>
			""",
			cancellationToken
		);

		// Create harness manually to use our custom setup
		var harness = new SimpleProjectHarness(projectDir, projectName, workDir);
		var projectReferences = await harness.GetItemIdentitiesAsync("ProjectReference", cancellationToken);

		return (harness, projectReferences);
	}

	[Test]
	public async Task NonTestProject_AutoReferences_SharedProject_InSiblingDirectory(
		CancellationToken cancellationToken
	)
	{
		var (harness, projectReferences) = await CreateProjectStructureAsync(
			"MyLibrary",
			workDir =>
			{
				var sharedDir = Path.Combine(workDir, "Shared");
				Directory.CreateDirectory(sharedDir);
				File.WriteAllText(
					Path.Combine(sharedDir, "Shared.csproj"),
					"""
					<Project Sdk="Microsoft.NET.Sdk">
						<PropertyGroup>
							<TargetFramework>net10.0</TargetFramework>
						</PropertyGroup>
					</Project>
					"""
				);
			},
			cancellationToken
		);

		await using (harness)
		{
			var normalized = projectReferences.Select(NormalizePath).ToList();
			await Assert.That(normalized).Contains("../Shared/Shared.csproj");
		}
	}

	[Test]
	public async Task NonTestProject_AutoReferences_SharedFramework_Project(CancellationToken cancellationToken)
	{
		var (harness, projectReferences) = await CreateProjectStructureAsync(
			"MyLibrary",
			workDir =>
			{
				var sharedDir = Path.Combine(workDir, "SharedFramework");
				Directory.CreateDirectory(sharedDir);
				File.WriteAllText(
					Path.Combine(sharedDir, "SharedFramework.csproj"),
					"""
					<Project Sdk="Microsoft.NET.Sdk">
						<PropertyGroup>
							<TargetFramework>net10.0</TargetFramework>
						</PropertyGroup>
					</Project>
					"""
				);
			},
			cancellationToken
		);

		await using (harness)
		{
			var normalized = projectReferences.Select(NormalizePath).ToList();
			await Assert.That(normalized).Contains("../SharedFramework/SharedFramework.csproj");
		}
	}

	[Test]
	public async Task NonTestProject_AutoReferences_Multiple_SharedProjects(CancellationToken cancellationToken)
	{
		var (harness, projectReferences) = await CreateProjectStructureAsync(
			"MyLibrary",
			workDir =>
			{
				// Create Shared project
				var sharedDir = Path.Combine(workDir, "Shared");
				Directory.CreateDirectory(sharedDir);
				File.WriteAllText(
					Path.Combine(sharedDir, "Shared.csproj"),
					"""
					<Project Sdk="Microsoft.NET.Sdk">
						<PropertyGroup>
							<TargetFramework>net10.0</TargetFramework>
						</PropertyGroup>
					</Project>
					"""
				);

				// Create SharedUtils project
				var sharedUtilsDir = Path.Combine(workDir, "SharedUtils");
				Directory.CreateDirectory(sharedUtilsDir);
				File.WriteAllText(
					Path.Combine(sharedUtilsDir, "SharedUtils.csproj"),
					"""
					<Project Sdk="Microsoft.NET.Sdk">
						<PropertyGroup>
							<TargetFramework>net10.0</TargetFramework>
						</PropertyGroup>
					</Project>
					"""
				);
			},
			cancellationToken
		);

		await using (harness)
		{
			var normalized = projectReferences.Select(NormalizePath).ToList();
			await Assert.That(normalized).Contains("../Shared/Shared.csproj");
			await Assert.That(normalized).Contains("../SharedUtils/SharedUtils.csproj");
		}
	}

	[Test]
	public async Task NonTestProject_AutoReferences_SharedInfrastructure_Project(CancellationToken cancellationToken)
	{
		var (harness, projectReferences) = await CreateProjectStructureAsync(
			"MyLibrary",
			workDir =>
			{
				var sharedDir = Path.Combine(workDir, "SharedInfrastructure");
				Directory.CreateDirectory(sharedDir);
				File.WriteAllText(
					Path.Combine(sharedDir, "SharedInfrastructure.csproj"),
					"""
					<Project Sdk="Microsoft.NET.Sdk">
						<PropertyGroup>
							<TargetFramework>net10.0</TargetFramework>
						</PropertyGroup>
					</Project>
					"""
				);
			},
			cancellationToken
		);

		await using (harness)
		{
			var normalized = projectReferences.Select(NormalizePath).ToList();
			await Assert.That(normalized).Contains("../SharedInfrastructure/SharedInfrastructure.csproj");
		}
	}

	[Test]
	public async Task NonTestProject_DoesNotAutoReference_SharedTestingProject(CancellationToken cancellationToken)
	{
		var (harness, projectReferences) = await CreateProjectStructureAsync(
			"MyLibrary",
			workDir =>
			{
				var sharedDir = Path.Combine(workDir, "SharedTestingFramework");
				Directory.CreateDirectory(sharedDir);
				File.WriteAllText(
					Path.Combine(sharedDir, "SharedTestingFramework.csproj"),
					"""
					<Project Sdk="Microsoft.NET.Sdk">
						<PropertyGroup>
							<TargetFramework>net10.0</TargetFramework>
						</PropertyGroup>
					</Project>
					"""
				);
			},
			cancellationToken
		);

		await using (harness)
		{
			var normalized = projectReferences.Select(NormalizePath).ToList();
			// SharedTesting* projects should be excluded from non-test projects
			await Assert.That(normalized).DoesNotContain("../SharedTestingFramework/SharedTestingFramework.csproj");
		}
	}

	[Test]
	public async Task TestProject_DoesNotAutoReference_SharedProject(CancellationToken cancellationToken)
	{
		var (harness, projectReferences) = await CreateProjectStructureAsync(
			"MyLibrary.UnitTests",
			workDir =>
			{
				var sharedDir = Path.Combine(workDir, "Shared");
				Directory.CreateDirectory(sharedDir);
				File.WriteAllText(
					Path.Combine(sharedDir, "Shared.csproj"),
					"""
					<Project Sdk="Microsoft.NET.Sdk">
						<PropertyGroup>
							<TargetFramework>net10.0</TargetFramework>
						</PropertyGroup>
					</Project>
					"""
				);
			},
			cancellationToken
		);

		await using (harness)
		{
			var normalized = projectReferences.Select(NormalizePath).ToList();
			// Test projects should NOT auto-reference regular Shared*.csproj projects
			await Assert.That(normalized).DoesNotContain("../Shared/Shared.csproj");
		}
	}

	[Test]
	public async Task TestProject_AutoReferences_SharedTestingProject(CancellationToken cancellationToken)
	{
		var (harness, projectReferences) = await CreateProjectStructureAsync(
			"MyLibrary.UnitTests",
			workDir =>
			{
				var sharedDir = Path.Combine(workDir, "SharedTestingFramework");
				Directory.CreateDirectory(sharedDir);
				File.WriteAllText(
					Path.Combine(sharedDir, "SharedTestingFramework.csproj"),
					"""
					<Project Sdk="Microsoft.NET.Sdk">
						<PropertyGroup>
							<TargetFramework>net10.0</TargetFramework>
						</PropertyGroup>
					</Project>
					"""
				);
			},
			cancellationToken
		);

		await using (harness)
		{
			var normalized = projectReferences.Select(NormalizePath).ToList();
			// Test projects SHOULD auto-reference SharedTesting* projects
			await Assert.That(normalized).Contains("../SharedTestingFramework/SharedTestingFramework.csproj");
		}
	}

	[Test]
	public async Task TestProject_AutoReferences_TargetProject_InSiblingDirectory(CancellationToken cancellationToken)
	{
		var (harness, projectReferences) = await CreateProjectStructureAsync(
			"MyLibrary.UnitTests",
			workDir =>
			{
				var targetDir = Path.Combine(workDir, "MyLibrary");
				Directory.CreateDirectory(targetDir);
				File.WriteAllText(
					Path.Combine(targetDir, "MyLibrary.csproj"),
					"""
					<Project Sdk="Microsoft.NET.Sdk">
						<PropertyGroup>
							<TargetFramework>net10.0</TargetFramework>
						</PropertyGroup>
					</Project>
					"""
				);
			},
			cancellationToken
		);

		await using (harness)
		{
			var normalized = projectReferences.Select(NormalizePath).ToList();
			// Test projects should auto-reference their target project (MyLibrary in this case)
			await Assert.That(normalized).Contains("../MyLibrary/MyLibrary.csproj");
		}
	}

	[Test]
	public async Task SharedProject_DoesNotAutoReference_SharedProjects(CancellationToken cancellationToken)
	{
		var (harness, projectReferences) = await CreateProjectStructureAsync(
			"Shared",
			workDir =>
			{
				var anotherSharedDir = Path.Combine(workDir, "SharedUtils");
				Directory.CreateDirectory(anotherSharedDir);
				File.WriteAllText(
					Path.Combine(anotherSharedDir, "SharedUtils.csproj"),
					"""
					<Project Sdk="Microsoft.NET.Sdk">
						<PropertyGroup>
							<TargetFramework>net10.0</TargetFramework>
						</PropertyGroup>
					</Project>
					"""
				);
			},
			cancellationToken
		);

		await using (harness)
		{
			var normalized = projectReferences.Select(NormalizePath).ToList();
			// Shared projects should NOT auto-reference other shared projects
			await Assert.That(normalized).DoesNotContain("../SharedUtils/SharedUtils.csproj");
		}
	}

	[Test]
	[Arguments("SharedLibrary")]
	[Arguments("SharedLib")]
	[Arguments("SharedHelpers")]
	[Arguments("SharedUtilities")]
	[Arguments("SharedUtils")]
	[Arguments("SharedInfra")]
	[Arguments("SharedInfrastructure")]
	public async Task NonTestProject_AutoReferences_AllWellKnownSharedProjectNames(
		string sharedProjectName,
		CancellationToken cancellationToken
	)
	{
		var (harness, projectReferences) = await CreateProjectStructureAsync(
			"MyLibrary",
			workDir =>
			{
				var sharedDir = Path.Combine(workDir, sharedProjectName);
				Directory.CreateDirectory(sharedDir);
				File.WriteAllText(
					Path.Combine(sharedDir, $"{sharedProjectName}.csproj"),
					"""
					<Project Sdk="Microsoft.NET.Sdk">
						<PropertyGroup>
							<TargetFramework>net10.0</TargetFramework>
						</PropertyGroup>
					</Project>
					"""
				);
			},
			cancellationToken
		);

		await using (harness)
		{
			var normalized = projectReferences.Select(NormalizePath).ToList();
			await Assert.That(normalized).Contains($"../{sharedProjectName}/{sharedProjectName}.csproj");
		}
	}
}

/// <summary>
/// Simple test helper for evaluating MSBuild items without using the full ProjectHarness.
/// </summary>
sealed class SimpleProjectHarness(string projectDirectory, string projectName, string workDir) : IAsyncDisposable
{
	public string ProjectName { get; } = projectName;

	public string ProjectDirectory { get; } = projectDirectory;

	public string ProjectFilePath { get; } = Path.Combine(projectDirectory, $"{projectName}.csproj");

	/// <summary>
	/// Evaluates one or more MSBuild items via <c>dotnet msbuild -getItem</c>
	/// without triggering a build or package restore.
	/// </summary>
	public async Task<IReadOnlyList<string>> GetItemIdentitiesAsync(
		string itemType,
		CancellationToken cancellationToken = default
	)
	{
		var args = $"msbuild \"{ProjectFilePath}\" -nologo -noconlog -getItem:{itemType}";

		using var process = new System.Diagnostics.Process
		{
			StartInfo = new System.Diagnostics.ProcessStartInfo
			{
				FileName = "dotnet",
				Arguments = args,
				WorkingDirectory = ProjectDirectory,
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

		var stdout = await stdoutTask;

		var jsonStart = stdout.Trim().IndexOf('{', StringComparison.Ordinal);
		if (jsonStart < 0)
			return [];

		try
		{
			using var doc = System.Text.Json.JsonDocument.Parse(stdout[jsonStart..]);
			if (
				doc.RootElement.TryGetProperty("Items", out var itemsEl)
				&& itemsEl.TryGetProperty(itemType, out var typeEl)
			)
			{
				var ids = new List<string>();
				foreach (var item in typeEl.EnumerateArray())
				{
					if (item.TryGetProperty("Identity", out var id))
						ids.Add(id.GetString() ?? string.Empty);
				}

				return ids;
			}
		}
		catch (System.Text.Json.JsonException)
		{ /* fall through */
		}

		return [];
	}

	public async ValueTask DisposeAsync()
	{
		try
		{
			if (Directory.Exists(workDir))
				Directory.Delete(workDir, recursive: true);
		}
		catch (IOException)
		{
			// Best-effort cleanup; don't fail tests on leftover temp files.
		}
	}
}
