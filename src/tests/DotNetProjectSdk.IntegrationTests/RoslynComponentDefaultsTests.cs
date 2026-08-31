using System.IO.Compression;
using Purview.DotNetProjectSdk.Harness;
using Purview.DotNetProjectSdk.Infra;

namespace Purview.DotNetProjectSdk;

/// <summary>
/// Verifies the source-generator defaults applied to Roslyn component projects.
/// </summary>
public sealed class RoslynComponentDefaultsTests
{
	[Test]
	public async Task RepositoryTargetFramework_DoesNotOverrideRoslynComponentDefault(
		CancellationToken cancellationToken
	)
	{
		using var harness = await ProjectHarness
			.For("SourceGeneration")
			.WithPreImportProperty("TargetFramework", "net8.0")
			.WithProjectFileContent(
				"""
				<Project Sdk="Microsoft.NET.Sdk">
					<PropertyGroup>
						<IsRoslynComponent>true</IsRoslynComponent>
					</PropertyGroup>
				</Project>
				"""
			)
			.BuildAsync(cancellationToken);

		var properties = await harness.GetPropertiesAsync(cancellationToken, "TargetFramework");
		await Assert.That(properties["TargetFramework"]).IsEqualTo("netstandard2.0");
	}

	[Test]
	public async Task PackableProject_AutomaticallyPacksAnalyzerProjectReference(CancellationToken cancellationToken)
	{
		using var generator = await ProjectHarness
			.For("SourceGeneration")
			.WithProjectFileContent(
				"""
				<Project Sdk="Microsoft.NET.Sdk">
					<PropertyGroup>
						<IsRoslynComponent>true</IsRoslynComponent>
					</PropertyGroup>
					<ItemGroup>
						<SourceGeneratorRuntimeDependency Include="$([MSBuild]::NormalizePath('$(MSBuildProjectDirectory)', '$(IntermediateOutputPath)', 'RuntimeDependency.dll'))" />
					</ItemGroup>
					<Target Name="CreateRuntimeDependency" BeforeTargets="CopySourceGeneratorRuntimeDependencies">
						<WriteLinesToFile File="$(IntermediateOutputPath)RuntimeDependency.dll" Lines="runtime" Overwrite="true" />
					</Target>
				</Project>
				"""
			)
			.BuildAsync(cancellationToken);

		using var consumer = await ProjectHarness
			.For("Consumer")
			.WithSolutionDirectory(generator.SolutionDirectory)
			.WithProjectFileContent(
				"""
				<Project Sdk="Microsoft.NET.Sdk">
					<PropertyGroup>
						<TargetFramework>net10.0</TargetFramework>
						<IsPackable>true</IsPackable>
						<ExcludePurviewTelemetry>true</ExcludePurviewTelemetry>
						<DisableSourceLink>true</DisableSourceLink>
					</PropertyGroup>
					<ItemGroup>
						<ProjectReference
							Include="..\SourceGeneration\SourceGeneration.csproj"
							PrivateAssets="all"
							ReferenceOutputAssembly="false"
							OutputItemType="Analyzer"
						/>
					</ItemGroup>
				</Project>
				"""
			)
			.BuildAsync(cancellationToken);

		var packageDirectory = Path.Combine(generator.SolutionDirectory, "packages");
		var packageVersion = $"0.0.0-integration-test-{Guid.NewGuid():N}";
		var (exitCode, stdOut, stdErr) = await consumer.RunMSBuildAsync(
			$"-restore -t:Pack -p:PackageOutputPath=\"{packageDirectory}\" -p:PackageVersion={packageVersion} -p:Version={packageVersion}",
			cancellationToken
		);

		await Assert.That(exitCode).IsEqualTo(0).Because(TestHelpers.GenerateError(stdOut, stdErr));
		var packagePath = Directory.GetFiles(packageDirectory, "Consumer.*.nupkg").Single();
		using var package = await ZipFile.OpenReadAsync(packagePath, cancellationToken);
		await Assert
			.That(package.Entries.Select(entry => entry.FullName))
			.Contains("analyzers/dotnet/cs/SourceGeneration.dll");
		await Assert
			.That(package.Entries.Select(entry => entry.FullName))
			.Contains("analyzers/dotnet/cs/RuntimeDependency.dll");
	}

	[Test]
	public async Task RoslynComponent_ExposesAnalyzerProjectReferenceTarget(CancellationToken cancellationToken)
	{
		using var harness = await ProjectHarness
			.For("SourceGeneration")
			.WithProjectFileContent(
				"""
				<Project Sdk="Microsoft.NET.Sdk">
					<PropertyGroup>
						<IsRoslynComponent>true</IsRoslynComponent>
					</PropertyGroup>
				</Project>
				"""
			)
			.BuildAsync(cancellationToken);

		var (exitCode, stdOut, stdErr) = await harness.RunMSBuildAsync(
			"-restore -t:GetSourceGeneratorAnalyzerFiles",
			cancellationToken
		);

		await Assert.That(exitCode).IsEqualTo(0).Because(TestHelpers.GenerateError(stdOut, stdErr));
	}

	[Test]
	public async Task IsRoslynComponent_True_AppliesSourceGeneratorDefaults(CancellationToken cancellationToken)
	{
		using var harness = await ProjectHarness
			.For("SourceGeneration")
			.WithProjectFileContent(
				"""
				<Project Sdk="Microsoft.NET.Sdk">
					<PropertyGroup>
						<IsRoslynComponent>true</IsRoslynComponent>
					</PropertyGroup>
				</Project>
				"""
			)
			.BuildAsync(cancellationToken);

		var properties = await harness.GetPropertiesAsync(
			cancellationToken,
			"TargetFramework",
			"EnforceExtendedAnalyzerRules",
			"DisableSourceLink",
			"EmbedUntrackedSources",
			"GenerateDependencyFile",
			"CompilerGeneratedFilesOutputPath",
			"SymbolPackageFormat",
			"TargetsForTfmSpecificDebugSymbolsInPackage",
			"ExcludePurviewTelemetry",
			"IncludeBuildOutput"
		);

		await Assert.That(properties["TargetFramework"]).IsEqualTo("netstandard2.0");
		await Assert.That(properties["EnforceExtendedAnalyzerRules"]).IsEqualTo("true");
		await Assert.That(properties["DisableSourceLink"]).IsEqualTo("true");
		await Assert.That(properties["EmbedUntrackedSources"]).IsEqualTo("false");
		await Assert.That(properties["GenerateDependencyFile"]).IsEqualTo("false");
		await Assert
			.That(properties["CompilerGeneratedFilesOutputPath"])
			.IsEqualTo(Path.Combine("obj", "Debug", "netstandard2.0", "generated"));
		await Assert.That(properties["SymbolPackageFormat"]).IsEqualTo("symbols.nupkg");
		await Assert
			.That(properties["TargetsForTfmSpecificDebugSymbolsInPackage"])
			.Contains("PackSourceGeneratorSymbols");
		await Assert.That(properties["ExcludePurviewTelemetry"]).IsEqualTo("true");
		await Assert.That(properties["IncludeBuildOutput"]).IsEqualTo("false");
	}
}
