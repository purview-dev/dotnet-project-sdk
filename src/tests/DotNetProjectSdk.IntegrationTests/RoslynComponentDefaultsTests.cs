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
		var packagePath = Directory.GetFiles(packageDirectory, "Test.Consumer.*.nupkg").Single();
		using var package = await ZipFile.OpenReadAsync(packagePath, cancellationToken);
		await Assert
			.That(package.Entries.Select(entry => entry.FullName))
			.Contains("analyzers/dotnet/cs/Test.SourceGeneration.dll");
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
			"ExcludePurviewTelemetry",
			"IncludeBuildOutput"
		);

		await Assert.That(properties["TargetFramework"]).IsEqualTo("netstandard2.0");
		await Assert.That(properties["EnforceExtendedAnalyzerRules"]).IsEqualTo("true");
		await Assert.That(properties["DisableSourceLink"]).IsEqualTo("true");
		await Assert.That(properties["EmbedUntrackedSources"]).IsEqualTo("false");
		await Assert.That(properties["GenerateDependencyFile"]).IsEqualTo("false");
		await Assert
			.That(TestHelpers.NormalizePath(properties["CompilerGeneratedFilesOutputPath"]))
			.IsEqualTo(TestHelpers.NormalizePath(Path.Combine("obj", "Debug", "netstandard2.0", "generated")));
		await Assert
			.That(properties["SymbolPackageFormat"])
			.IsEqualTo("snupkg")
			.Because("Roslyn components must use the modern snupkg format, never the legacy symbols.nupkg.");
		await Assert.That(properties["ExcludePurviewTelemetry"]).IsEqualTo("true");
		await Assert.That(properties["IncludeBuildOutput"]).IsEqualTo("false");
	}

	[Test]
	public async Task RoslynComponent_AppliesLatestCompilerDefaults(CancellationToken cancellationToken)
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
			"LangVersion",
			"Nullable",
			"TreatWarningsAsErrors",
			"IncludeSymbols",
			"NoWarn"
		);

		await Assert.That(properties["LangVersion"]).IsEqualTo("latest");
		await Assert.That(properties["Nullable"]).IsEqualTo("enable");
		await Assert.That(properties["TreatWarningsAsErrors"]).IsEqualTo("true");
		await Assert
			.That(properties["IncludeSymbols"])
			.IsEqualTo("false")
			.Because("Roslyn components must not produce a symbol package by default.");
		await Assert
			.That(properties["NoWarn"])
			.Contains("NU5128")
			.Because("Analyzer-only packages need NU5128 suppressed so TreatWarningsAsErrors does not fail the pack.");
	}

	[Test]
	public async Task RoslynComponent_ExplicitCompilerDefaults_ArePreserved(CancellationToken cancellationToken)
	{
		using var harness = await ProjectHarness
			.For("SourceGeneration")
			.WithProjectFileContent(
				"""
				<Project Sdk="Microsoft.NET.Sdk">
					<PropertyGroup>
						<IsRoslynComponent>true</IsRoslynComponent>
						<LangVersion>12.0</LangVersion>
						<Nullable>disable</Nullable>
						<TreatWarningsAsErrors>false</TreatWarningsAsErrors>
					</PropertyGroup>
				</Project>
				"""
			)
			.BuildAsync(cancellationToken);

		var properties = await harness.GetPropertiesAsync(
			cancellationToken,
			"LangVersion",
			"Nullable",
			"TreatWarningsAsErrors"
		);

		await Assert.That(properties["LangVersion"]).IsEqualTo("12.0");
		await Assert.That(properties["Nullable"]).IsEqualTo("disable");
		await Assert.That(properties["TreatWarningsAsErrors"]).IsEqualTo("false");
	}

	[Test]
	public async Task PackableRoslynComponent_Pack_AnalyzerDllAndPdb_NoLib_NoSymbolsPackage(
		CancellationToken cancellationToken
	)
	{
		using var harness = await ProjectHarness
			.For("SourceGeneration")
			.WithProjectFileContent(
				"""
				<Project Sdk="Microsoft.NET.Sdk">
					<PropertyGroup>
						<IsRoslynComponent>true</IsRoslynComponent>
						<IsPackable>true</IsPackable>
					</PropertyGroup>
				</Project>
				"""
			)
			.BuildAsync(cancellationToken);

		var packageDirectory = Path.Combine(harness.SolutionDirectory, "packages");
		var packageVersion = $"0.0.0-integration-test-{Guid.NewGuid():N}";
		var (exitCode, stdOut, stdErr) = await harness.RunMSBuildAsync(
			$"-restore -t:Pack -p:PackageOutputPath=\"{packageDirectory}\" -p:PackageVersion={packageVersion} -p:Version={packageVersion}",
			cancellationToken
		);

		await Assert.That(exitCode).IsEqualTo(0).Because(TestHelpers.GenerateError(stdOut, stdErr));

		var nupkgFiles = Directory.GetFiles(packageDirectory, "Test.SourceGeneration.*.nupkg");
		await Assert
			.That(nupkgFiles)
			.HasSingleItem()
			.Because("Only the main .nupkg must be produced; no .symbols.nupkg or .snupkg is generated by default.");

		using var package = await ZipFile.OpenReadAsync(nupkgFiles[0], cancellationToken);
		var entries = package.Entries.Select(entry => entry.FullName).ToList();
		await Assert.That(entries).Contains("analyzers/dotnet/cs/Test.SourceGeneration.dll");
		await Assert.That(entries).Contains("analyzers/dotnet/cs/Test.SourceGeneration.pdb");
		await Assert
			.That(entries.Any(entry => entry.StartsWith("lib/", StringComparison.OrdinalIgnoreCase)))
			.IsFalse()
			.Because("IncludeBuildOutput=false must keep the lib/ output empty.");
	}

	[Test]
	public async Task NonPackableRoslynComponent_DoesNotPackAnalyzerAsset(CancellationToken cancellationToken)
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

		var noneItems = await harness.GetItemIdentitiesAsync("None", cancellationToken);
		await Assert
			.That(noneItems.Any(item => item.EndsWith("Test.SourceGeneration.dll", StringComparison.OrdinalIgnoreCase)))
			.IsFalse()
			.Because("Only packable Roslyn components get the analyzer asset packaging.");
	}

	[Test]
	public async Task RoslynComponent_RoslynDevDependencies_PrivateAssetsAll(CancellationToken cancellationToken)
	{
		using var harness = await ProjectHarness
			.For("SourceGeneration")
			.WithProjectFileContent(
				"""
				<Project Sdk="Microsoft.NET.Sdk">
					<PropertyGroup>
						<IsRoslynComponent>true</IsRoslynComponent>
					</PropertyGroup>
					<ItemGroup>
						<PackageReference Include="Microsoft.CodeAnalysis.CSharp" />
						<PackageReference Include="Microsoft.CodeAnalysis.Analyzers" />
					</ItemGroup>
				</Project>
				"""
			)
			.BuildAsync(cancellationToken);

		var csharpPrivateAssets = await harness.GetItemMetadataValuesAsync(
			"PackageReference",
			"PrivateAssets",
			"Microsoft.CodeAnalysis.CSharp",
			cancellationToken
		);
		await Assert.That(csharpPrivateAssets).Contains("all");

		var analyzersPrivateAssets = await harness.GetItemMetadataValuesAsync(
			"PackageReference",
			"PrivateAssets",
			"Microsoft.CodeAnalysis.Analyzers",
			cancellationToken
		);
		await Assert.That(analyzersPrivateAssets).Contains("all");

		var analyzersIncludeAssets = await harness.GetItemMetadataValuesAsync(
			"PackageReference",
			"IncludeAssets",
			"Microsoft.CodeAnalysis.Analyzers",
			cancellationToken
		);
		await Assert
			.That(analyzersIncludeAssets.Any(value => value.Contains("analyzers", StringComparison.OrdinalIgnoreCase)))
			.IsTrue()
			.Because("Microsoft.CodeAnalysis.Analyzers should flow as an analyzer-only development asset.");
	}
}
