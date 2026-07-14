using Purview.DotNetProjectSdk.Harness;

namespace Purview.DotNetProjectSdk.Tests;

/// <summary>
/// Verifies that Shared*.csproj projects are automatically referenced by all non-test
/// and non-shared projects. This tests the SDK's auto-discovery and auto-referencing
/// feature that allows projects to transparently depend on shared libraries without
/// explicit ProjectReference declarations.
/// </summary>
public sealed class AutoSharedProjectReferencingTests
{
	[Test]
	public async Task AspireHostProject_WithSharedProject_HasIsAspireProjectResourceFalse(
		CancellationToken cancellationToken
	)
	{
		await using var sharedHarness = await ProjectHarness
			.For("Shared")
			.WithTargetFramework("net10.0")
			.BuildAsync(cancellationToken);

		await using var appHostHarness = await ProjectHarness
			.For("Acme.AppHost")
			.WithProjectFileContent(
				$"""
				<Project>
					<Import Project="{SdkPaths.SdkDirectory}/Sdk.props" />
					<PropertyGroup>
						<NamespacePrefix>Acme</NamespacePrefix>
						<DisableNamespacePrefixCheck>true</DisableNamespacePrefixCheck>
						<TargetFramework>net10.0</TargetFramework>
					</PropertyGroup>
					<!--
					  <Project Sdk="Aspire.Sdk.Host" />
					-->
				</Project>
				"""
			)
			.WithSolutionDirectory(sharedHarness.SolutionDirectory)
			.BuildAsync(cancellationToken);

		var projectReferences = await appHostHarness.GetProjectReferencesAsync(cancellationToken);
		var normalized = projectReferences.Select(TestHelpers.NormalizePath).ToList();

		await Assert.That(normalized).Contains("../Shared/Shared.csproj");

		var aspireResourceFlags = await appHostHarness.GetItemMetadataValuesAsync(
			"ProjectReference",
			"IsAspireProjectResource",
			cancellationToken
		);

		await Assert.That(aspireResourceFlags).Contains("false");
	}

	[Test]
	public async Task NonTestProject_AutoReferences_SharedProject_InSiblingDirectory(
		CancellationToken cancellationToken
	)
	{
		var (harness, projectReferences) = await TestHelpers.CreateProjectStructureAsync(
			"MyLibrary",
			async workDir =>
			{
				var sharedDir = Path.Combine(workDir, "Shared");
				Directory.CreateDirectory(sharedDir);
				await File.WriteAllTextAsync(
					Path.Combine(sharedDir, "Shared.csproj"),
					"""
					<Project Sdk="Microsoft.NET.Sdk">
						<PropertyGroup>
							<TargetFramework>net10.0</TargetFramework>
						</PropertyGroup>
					</Project>
					""",
					cancellationToken
				);
			},
			null,
			cancellationToken
		);

		await using (harness)
		{
			var normalized = projectReferences.Select(TestHelpers.NormalizePath).ToList();
			await Assert.That(normalized).Contains("../Shared/Shared.csproj");
		}
	}

	[Test]
	public async Task NonTestProject_AutoReferences_SharedFramework_Project(CancellationToken cancellationToken)
	{
		var (harness, projectReferences) = await TestHelpers.CreateProjectStructureAsync(
			"MyLibrary",
			async workDir =>
			{
				var sharedDir = Path.Combine(workDir, "SharedFramework");
				Directory.CreateDirectory(sharedDir);
				await File.WriteAllTextAsync(
					Path.Combine(sharedDir, "SharedFramework.csproj"),
					"""
					<Project Sdk="Microsoft.NET.Sdk">
						<PropertyGroup>
							<TargetFramework>net10.0</TargetFramework>
						</PropertyGroup>
					</Project>
					""",
					cancellationToken
				);
			},
			null,
			cancellationToken
		);

		await using (harness)
		{
			var normalized = projectReferences.Select(TestHelpers.NormalizePath).ToList();
			await Assert.That(normalized).Contains("../SharedFramework/SharedFramework.csproj");
		}
	}

	[Test]
	public async Task NonTestProject_AutoReferences_Multiple_SharedProjects(CancellationToken cancellationToken)
	{
		var (harness, projectReferences) = await TestHelpers.CreateProjectStructureAsync(
			"MyLibrary",
			async workDir =>
			{
				// Create Shared project
				var sharedDir = Path.Combine(workDir, "Shared");
				Directory.CreateDirectory(sharedDir);
				await File.WriteAllTextAsync(
					Path.Combine(sharedDir, "Shared.csproj"),
					"""
					<Project Sdk="Microsoft.NET.Sdk">
						<PropertyGroup>
							<TargetFramework>net10.0</TargetFramework>
						</PropertyGroup>
					</Project>
					""",
					cancellationToken
				);

				// Create SharedUtils project
				var sharedUtilsDir = Path.Combine(workDir, "SharedUtils");
				Directory.CreateDirectory(sharedUtilsDir);
				await File.WriteAllTextAsync(
					Path.Combine(sharedUtilsDir, "SharedUtils.csproj"),
					"""
					<Project Sdk="Microsoft.NET.Sdk">
						<PropertyGroup>
							<TargetFramework>net10.0</TargetFramework>
						</PropertyGroup>
					</Project>
					""",
					cancellationToken
				);
			},
			null,
			cancellationToken
		);

		await using (harness)
		{
			var normalized = projectReferences.Select(TestHelpers.NormalizePath).ToList();
			await Assert.That(normalized).Contains("../Shared/Shared.csproj");
			await Assert.That(normalized).Contains("../SharedUtils/SharedUtils.csproj");
		}
	}

	[Test]
	public async Task NonTestProject_AutoReferences_SharedInfrastructure_Project(CancellationToken cancellationToken)
	{
		var (harness, projectReferences) = await TestHelpers.CreateProjectStructureAsync(
			"MyLibrary",
			async workDir =>
			{
				var sharedDir = Path.Combine(workDir, "SharedInfrastructure");
				Directory.CreateDirectory(sharedDir);
				await File.WriteAllTextAsync(
					Path.Combine(sharedDir, "SharedInfrastructure.csproj"),
					"""
					<Project Sdk="Microsoft.NET.Sdk">
						<PropertyGroup>
							<TargetFramework>net10.0</TargetFramework>
						</PropertyGroup>
					</Project>
					""",
					cancellationToken
				);
			},
			null,
			cancellationToken
		);

		await using (harness)
		{
			var normalized = projectReferences.Select(TestHelpers.NormalizePath).ToList();
			await Assert.That(normalized).Contains("../SharedInfrastructure/SharedInfrastructure.csproj");
		}
	}

	[Test]
	public async Task NonTestProject_DoesNotAutoReference_SharedTestingProject(CancellationToken cancellationToken)
	{
		var (harness, projectReferences) = await TestHelpers.CreateProjectStructureAsync(
			"MyLibrary",
			async workDir =>
			{
				var sharedDir = Path.Combine(workDir, "SharedTestingFramework");
				Directory.CreateDirectory(sharedDir);
				await File.WriteAllTextAsync(
					Path.Combine(sharedDir, "SharedTestingFramework.csproj"),
					"""
					<Project Sdk="Microsoft.NET.Sdk">
						<PropertyGroup>
							<TargetFramework>net10.0</TargetFramework>
						</PropertyGroup>
					</Project>
					""",
					cancellationToken
				);
			},
			null,
			cancellationToken
		);

		await using (harness)
		{
			var normalized = projectReferences.Select(TestHelpers.NormalizePath).ToList();
			// SharedTesting* projects should be excluded from non-test projects
			await Assert.That(normalized).DoesNotContain("../SharedTestingFramework/SharedTestingFramework.csproj");
		}
	}

	[Test]
	public async Task TestProject_DoesNotAutoReference_SharedProject(CancellationToken cancellationToken)
	{
		var (harness, projectReferences) = await TestHelpers.CreateProjectStructureAsync(
			"MyLibrary.UnitTests",
			async workDir =>
			{
				var sharedDir = Path.Combine(workDir, "Shared");
				Directory.CreateDirectory(sharedDir);
				await File.WriteAllTextAsync(
					Path.Combine(sharedDir, "Shared.csproj"),
					"""
					<Project Sdk="Microsoft.NET.Sdk">
						<PropertyGroup>
							<TargetFramework>net10.0</TargetFramework>
						</PropertyGroup>
					</Project>
					""",
					cancellationToken
				);
			},
			null,
			cancellationToken
		);

		await using (harness)
		{
			var normalized = projectReferences.Select(TestHelpers.NormalizePath).ToList();
			// Test projects should NOT auto-reference regular Shared*.csproj projects
			await Assert.That(normalized).DoesNotContain("../Shared/Shared.csproj");
		}
	}

	[Test]
	public async Task TestProject_AutoReferences_SharedTestingProject(CancellationToken cancellationToken)
	{
		var (harness, projectReferences) = await TestHelpers.CreateProjectStructureAsync(
			"MyLibrary.UnitTests",
			async workDir =>
			{
				var sharedDir = Path.Combine(workDir, "SharedTestingFramework");
				Directory.CreateDirectory(sharedDir);
				await File.WriteAllTextAsync(
					Path.Combine(sharedDir, "SharedTestingFramework.csproj"),
					"""
					<Project Sdk="Microsoft.NET.Sdk">
						<PropertyGroup>
							<TargetFramework>net10.0</TargetFramework>
						</PropertyGroup>
					</Project>
					""",
					cancellationToken
				);
			},
			null,
			cancellationToken
		);

		await using (harness)
		{
			var normalized = projectReferences.Select(TestHelpers.NormalizePath).ToList();
			// Test projects SHOULD auto-reference SharedTesting* projects
			await Assert.That(normalized).Contains("../SharedTestingFramework/SharedTestingFramework.csproj");
		}
	}

	[Test]
	public async Task TestProject_AutoReferences_TargetProject_InSiblingDirectory(CancellationToken cancellationToken)
	{
		var (harness, projectReferences) = await TestHelpers.CreateProjectStructureAsync(
			"MyLibrary.UnitTests",
			async workDir =>
			{
				var targetDir = Path.Combine(workDir, "MyLibrary");
				Directory.CreateDirectory(targetDir);
				await File.WriteAllTextAsync(
					Path.Combine(targetDir, "MyLibrary.csproj"),
					"""
					<Project Sdk="Microsoft.NET.Sdk">
						<PropertyGroup>
							<TargetFramework>net10.0</TargetFramework>
						</PropertyGroup>
					</Project>
					""",
					cancellationToken
				);
			},
			null,
			cancellationToken
		);

		await using (harness)
		{
			var normalized = projectReferences.Select(TestHelpers.NormalizePath).ToList();
			// Test projects should auto-reference their target project (MyLibrary in this case)
			await Assert.That(normalized).Contains("../MyLibrary/MyLibrary.csproj");
		}
	}

	[Test]
	public async Task SharedProject_DoesNotAutoReference_SharedProjects(CancellationToken cancellationToken)
	{
		var (harness, projectReferences) = await TestHelpers.CreateProjectStructureAsync(
			"Shared",
			async workDir =>
			{
				var anotherSharedDir = Path.Combine(workDir, "SharedUtils");
				Directory.CreateDirectory(anotherSharedDir);
				await File.WriteAllTextAsync(
					Path.Combine(anotherSharedDir, "SharedUtils.csproj"),
					"""
					<Project Sdk="Microsoft.NET.Sdk">
						<PropertyGroup>
							<TargetFramework>net10.0</TargetFramework>
						</PropertyGroup>
					</Project>
					""",
					cancellationToken
				);
			},
			null,
			cancellationToken
		);

		await using (harness)
		{
			var normalized = projectReferences.Select(TestHelpers.NormalizePath).ToList();
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
		var (harness, projectReferences) = await TestHelpers.CreateProjectStructureAsync(
			"MyLibrary",
			async workDir =>
			{
				var sharedDir = Path.Combine(workDir, sharedProjectName);
				Directory.CreateDirectory(sharedDir);
				await File.WriteAllTextAsync(
					Path.Combine(sharedDir, $"{sharedProjectName}.csproj"),
					"""
					<Project Sdk="Microsoft.NET.Sdk">
						<PropertyGroup>
							<TargetFramework>net10.0</TargetFramework>
						</PropertyGroup>
					</Project>
					""",
					cancellationToken
				);
			},
			null,
			cancellationToken
		);

		await using (harness)
		{
			var normalized = projectReferences.Select(TestHelpers.NormalizePath).ToList();
			await Assert.That(normalized).Contains($"../{sharedProjectName}/{sharedProjectName}.csproj");
		}
	}
}
