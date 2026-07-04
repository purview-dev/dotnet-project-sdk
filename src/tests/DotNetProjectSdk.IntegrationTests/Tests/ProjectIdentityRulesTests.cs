using System.Diagnostics;
using System.Text.Json;
using Purview.DotNetProjectSdk.Harness;

namespace Purview.DotNetProjectSdk.Tests;

public sealed class ProjectIdentityRulesTests
{
	[Test]
	public async Task RootProject_UsesNamespacePrefixAsIs(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"ExampleProject",
			namespacePrefix: "ExampleProject",
			cancellationToken: cancellationToken
		);
		var props = await h.GetPropertiesAsync(cancellationToken, "AssemblyName", "RootNamespace");
		await Assert.That(props["AssemblyName"]).IsEqualTo("ExampleProject");
		await Assert.That(props["RootNamespace"]).IsEqualTo("ExampleProject");
	}

	[Test]
	public async Task ShortChildProject_IsPrefixed(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"SourceGenerator",
			namespacePrefix: "ExampleProject",
			cancellationToken: cancellationToken
		);
		var props = await h.GetPropertiesAsync(cancellationToken, "AssemblyName", "RootNamespace");
		await Assert.That(props["AssemblyName"]).IsEqualTo("ExampleProject.SourceGenerator");
		await Assert.That(props["RootNamespace"]).IsEqualTo("ExampleProject.SourceGenerator");
	}

	[Test]
	public async Task FullyQualifiedChildProject_IsNotDoublePrefixed(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"ExampleProject.SourceGenerator",
			namespacePrefix: "ExampleProject",
			cancellationToken: cancellationToken
		);
		var props = await h.GetPropertiesAsync(cancellationToken, "AssemblyName", "RootNamespace");
		await Assert.That(props["AssemblyName"]).IsEqualTo("ExampleProject.SourceGenerator");
		await Assert.That(props["RootNamespace"]).IsEqualTo("ExampleProject.SourceGenerator");
	}

	[Test]
	public async Task PartialPrefix_IsNotTreatedAsQualified(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"ExampleProjectExtra",
			namespacePrefix: "ExampleProject",
			cancellationToken: cancellationToken
		);
		var props = await h.GetPropertiesAsync(cancellationToken, "AssemblyName", "RootNamespace");
		await Assert.That(props["AssemblyName"]).IsEqualTo("ExampleProject.ExampleProjectExtra");
		await Assert.That(props["RootNamespace"]).IsEqualTo("ExampleProject.ExampleProjectExtra");
	}

	[Test]
	public async Task AssemblyName_DefaultsToFullyQualifiedLogicalName(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"Api",
			namespacePrefix: "ExampleProject",
			cancellationToken: cancellationToken
		);
		await Assert.That(await h.GetPropertyAsync("AssemblyName", cancellationToken)).IsEqualTo("ExampleProject.Api");
	}

	[Test]
	public async Task RootNamespace_DefaultsToFullyQualifiedLogicalName(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"Api",
			namespacePrefix: "ExampleProject",
			cancellationToken: cancellationToken
		);
		await Assert.That(await h.GetPropertyAsync("RootNamespace", cancellationToken)).IsEqualTo("ExampleProject.Api");
	}

	[Test]
	public async Task ExplicitAssemblyName_IsRespected(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"SourceGenerator",
			namespacePrefix: "ExampleProject",
			extraProps: "<AssemblyName>Custom.Assembly</AssemblyName>",
			cancellationToken: cancellationToken
		);
		await Assert.That(await h.GetPropertyAsync("AssemblyName", cancellationToken)).IsEqualTo("Custom.Assembly");
	}

	[Test]
	public async Task ExplicitRootNamespace_IsRespected(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"SourceGenerator",
			namespacePrefix: "ExampleProject",
			extraProps: "<RootNamespace>Custom.Namespace</RootNamespace>",
			cancellationToken: cancellationToken
		);
		await Assert.That(await h.GetPropertyAsync("RootNamespace", cancellationToken)).IsEqualTo("Custom.Namespace");
	}

	[Test]
	public async Task ChildUnitTests_AreFullyQualified(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"SourceGenerator.UnitTests",
			namespacePrefix: "ExampleProject",
			cancellationToken: cancellationToken
		);
		var props = await h.GetPropertiesAsync(cancellationToken, "AssemblyName", "RootNamespace");
		await Assert.That(props["AssemblyName"]).IsEqualTo("ExampleProject.SourceGenerator.UnitTests");
		await Assert.That(props["RootNamespace"]).IsEqualTo("ExampleProject.SourceGenerator.UnitTests");
	}

	[Test]
	public async Task RootUnitTests_RemainCorrectlyQualified(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"ExampleProject.UnitTests",
			namespacePrefix: "ExampleProject",
			cancellationToken: cancellationToken
		);
		var props = await h.GetPropertiesAsync(cancellationToken, "AssemblyName", "RootNamespace");
		await Assert.That(props["AssemblyName"]).IsEqualTo("ExampleProject.UnitTests");
		await Assert.That(props["RootNamespace"]).IsEqualTo("ExampleProject.UnitTests");
	}

	[Test]
	public async Task IntegrationTests_AreFullyQualified(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"Api.IntegrationTests",
			namespacePrefix: "ExampleProject",
			cancellationToken: cancellationToken
		);
		await Assert.That(await h.GetPropertyAsync("AssemblyName", cancellationToken)).IsEqualTo("ExampleProject.Api.IntegrationTests");
	}

	[Test]
	public async Task ArchitectureTests_AreFullyQualified(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"Core.ArchitectureTests",
			namespacePrefix: "ExampleProject",
			cancellationToken: cancellationToken
		);
		await Assert.That(await h.GetPropertyAsync("AssemblyName", cancellationToken)).IsEqualTo("ExampleProject.Core.ArchitectureTests");
	}

	[Test]
	public async Task ContractTests_AreFullyQualified(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"Core.ContractTests",
			namespacePrefix: "ExampleProject",
			cancellationToken: cancellationToken
		);
		await Assert.That(await h.GetPropertyAsync("AssemblyName", cancellationToken)).IsEqualTo("ExampleProject.Core.ContractTests");
	}

	[Test]
	public async Task FunctionalTests_AreFullyQualified(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"Core.FunctionalTests",
			namespacePrefix: "ExampleProject",
			cancellationToken: cancellationToken
		);
		await Assert.That(await h.GetPropertyAsync("AssemblyName", cancellationToken)).IsEqualTo("ExampleProject.Core.FunctionalTests");
	}

	[Test]
	public async Task InternalsVisibleTo_UsesFullyQualifiedTestAssemblyNames(CancellationToken cancellationToken)
	{
		await using var h = await ProjectHarness.CreateAsync(
			"SourceGenerator",
			namespacePrefix: "ExampleProject",
			cancellationToken: cancellationToken
		);

		var (exitCode, stdOut, stdErr) = await h.RunMSBuildAsync(
			"-t:InternalsVisibleToTarget -noconlog -getItem:AssemblyAttribute",
			cancellationToken
		);
		await Assert.That(exitCode).IsEqualTo(0).Because(TestHelpers.GenerateError(stdOut, stdErr));
		var friendAssemblies = ExtractItemMetadataValues(stdOut, "AssemblyAttribute", "_Parameter1");

		await Assert.That(friendAssemblies).Contains("ExampleProject.SourceGenerator.UnitTests");
		await Assert.That(friendAssemblies).Contains("ExampleProject.SourceGenerator.IntegrationTests");
		await Assert.That(friendAssemblies).Contains("ExampleProject.SourceGenerator.ArchitectureTests");
		await Assert.That(friendAssemblies).Contains("ExampleProject.SourceGenerator.ContractTests");
		await Assert.That(friendAssemblies).Contains("ExampleProject.SourceGenerator.FunctionalTests");
	}

	[Test]
	public async Task ProjectFileNamingConvention_AcceptsShortProjectFilenames(CancellationToken cancellationToken)
	{
		var (exitCode, output) = await ValidateProjectFileNamingAsync(
			"SourceGenerator",
			"SourceGenerator.csproj",
			cancellationToken
		);
		await Assert.That(exitCode).IsEqualTo(0).Because(output);
	}

	[Test]
	public async Task ProjectFileNamingConvention_RejectsRedundantFullyQualifiedFileNames(
		CancellationToken cancellationToken
	)
	{
		var (exitCode, output) = await ValidateProjectFileNamingAsync(
			"SourceGenerator.UnitTests",
			"ExampleProject.SourceGenerator.UnitTests.csproj",
			cancellationToken
		);
		await Assert.That(exitCode).IsNotEqualTo(0);
		await Assert.That(output).Contains("PurviewProjectFileNameMismatch");
	}

	[Test]
	public async Task ProjectDirectoryAndProjectFileName_MustMatch(CancellationToken cancellationToken)
	{
		var (exitCode, output) = await ValidateProjectFileNamingAsync(
			"SourceGenerator.UnitTests",
			"SourceGenerator.ArchitectureTests.csproj",
			cancellationToken
		);
		await Assert.That(exitCode).IsNotEqualTo(0);
		await Assert.That(output).Contains("PurviewProjectFileNameMismatch");
	}

	static async Task<(int ExitCode, string Output)> ValidateProjectFileNamingAsync(
		string projectDirectoryName,
		string projectFileName,
		CancellationToken cancellationToken
	)
	{
		var tempRoot = Path.Combine(Path.GetTempPath(), "PurviewSdkTests", Guid.NewGuid().ToString("N"));
		var projectDirectory = Path.Combine(tempRoot, projectDirectoryName);
		var projectFilePath = Path.Combine(projectDirectory, projectFileName);

		Directory.CreateDirectory(projectDirectory);

		try
		{
			await File.WriteAllTextAsync(
				Path.Combine(projectDirectory, "Directory.Build.props"),
				$"""
				<Project>
					<PropertyGroup>
						<NamespacePrefix>ExampleProject</NamespacePrefix>
					</PropertyGroup>
					<Import Project="{SdkPaths.SdkDirectory}/Sdk.props" />
				</Project>
				""",
				cancellationToken
			);

			await File.WriteAllTextAsync(
				Path.Combine(projectDirectory, "Directory.Build.targets"),
				$"""
				<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
					<Import Project="{SdkPaths.SdkDirectory}/Sdk.targets" />
				</Project>
				""",
				cancellationToken
			);

			await File.WriteAllTextAsync(
				Path.Combine(projectDirectory, "Directory.Packages.props"),
				"""
				<Project>
					<PropertyGroup>
						<CentralPackageFloatingVersionsEnabled>true</CentralPackageFloatingVersionsEnabled>
					</PropertyGroup>
				</Project>
				""",
				cancellationToken
			);

			await File.WriteAllTextAsync(
				projectFilePath,
				"""
				<Project Sdk="Microsoft.NET.Sdk">
					<PropertyGroup>
						<TargetFramework>net10.0</TargetFramework>
					</PropertyGroup>
				</Project>
				""",
				cancellationToken
			);

			var (exitCode, stdOut, stdErr) = await RunProcessAsync(
				"dotnet",
				$"msbuild \"{projectFilePath}\" -nologo -t:ValidateProjectFileNamingConventionTarget",
				projectDirectory,
				cancellationToken
			);

			return (exitCode, stdOut + stdErr);
		}
		finally
		{
			if (Directory.Exists(tempRoot))
				Directory.Delete(tempRoot, recursive: true);
		}
	}

	static List<string> ExtractItemMetadataValues(
		string msbuildOutput,
		string itemType,
		string metadataName
	)
	{
		var jsonStart = msbuildOutput.IndexOf('{', StringComparison.Ordinal);
		if (jsonStart < 0)
			return [];

		try
		{
			using var doc = JsonDocument.Parse(msbuildOutput[jsonStart..]);
			if (
				doc.RootElement.TryGetProperty("Items", out var itemsEl)
				&& itemsEl.TryGetProperty(itemType, out var typeEl)
			)
			{
				var values = new List<string>();
				foreach (var item in typeEl.EnumerateArray())
				{
					if (item.TryGetProperty(metadataName, out var metadataValue))
						values.Add(metadataValue.GetString() ?? string.Empty);
				}

				return values;
			}
		}
		catch (JsonException)
		{
			return [];
		}

		return [];
	}

	static async Task<(int Code, string StdOut, string StdErr)> RunProcessAsync(
		string fileName,
		string arguments,
		string workingDirectory,
		CancellationToken cancellationToken
	)
	{
		using var process = new Process
		{
			StartInfo = new ProcessStartInfo
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
		var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
		var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);
		await process.WaitForExitAsync(cancellationToken);

		return (process.ExitCode, await stdOutTask, await stdErrTask);
	}
}
