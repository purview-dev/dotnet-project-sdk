using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;

namespace Purview.DotNetProjectSdk.Tests;

/// <summary>
/// Proves the packed SDK can be consumed from a local NuGet feed and that its .editorconfig
/// is resolved and applied as an EditorConfigFiles entry in a fresh consumer project.
/// </summary>
public sealed class SdkPackageConsumptionTests
{
	[Test]
	public async Task PackedSdk_Exposes_EditorConfig_To_NewConsumerProject(CancellationToken cancellationToken)
	{
		var tempRoot = Path.Combine(Path.GetTempPath(), $"PurviewSdkPackageConsumption-{Guid.NewGuid():N}");
		var feedDirectory = Path.Combine(tempRoot, "feed");
		var consumerDirectory = Path.Combine(tempRoot, "consumer");
		var consumerSrcDirectory = Path.Combine(consumerDirectory, "src");

		Directory.CreateDirectory(feedDirectory);
		Directory.CreateDirectory(consumerDirectory);
		Directory.CreateDirectory(consumerSrcDirectory);

		try
		{
			var sdkProjectPath = Path.GetFullPath(Path.Combine(SdkPaths.SdkDirectory, "..", "DotNetProjectSdk.csproj"));
			var sdkProjectDirectory =
				Path.GetDirectoryName(sdkProjectPath)
				?? throw new InvalidOperationException("Unable to determine SDK project directory.");

			var packageVersion = $"0.0.0-integration-test-{Guid.NewGuid():N}";
			var (code, stdOut, stdErr) = await RunProcessAsync(
				"dotnet",
				$"pack \"{sdkProjectPath}\" -c Release -o \"{feedDirectory}\" -p:PackageVersion={packageVersion} -p:Version={packageVersion}",
				sdkProjectDirectory,
				cancellationToken
			);
			await Assert.That(code).IsEqualTo(0).Because(TestHelpers.GenerateError(stdOut, stdErr));

			var packagePath = Directory
				.GetFiles(
					feedDirectory,
					$"Purview.DotNetProjectSdk.{packageVersion}.nupkg",
					SearchOption.TopDirectoryOnly
				)
				.SingleOrDefault();

			await Assert
				.That(packagePath)
				.IsNotNull()
				.Because($"The package {packageVersion} was not found in the feed directory.");

			using (var zip = await ZipFile.OpenReadAsync(packagePath!, cancellationToken))
			{
				await Assert
					.That(zip.Entries.Any(entry => entry.FullName == "Sdk/.editorconfig"))
					.IsTrue()
					.Because("The .editorconfig file is missing in the SDK package.");
			}

			(code, stdOut, stdErr) = await RunProcessAsync(
				"dotnet",
				"new sln -n Proof",
				consumerDirectory,
				cancellationToken
			);
			await Assert.That(code).IsEqualTo(0).Because(TestHelpers.GenerateError(stdOut, stdErr));

			(code, stdOut, stdErr) = await RunProcessAsync("git", "init", consumerDirectory, cancellationToken);
			await Assert.That(code).IsEqualTo(0).Because(TestHelpers.GenerateError(stdOut, stdErr));

			(code, stdOut, stdErr) = await RunProcessAsync(
				"dotnet",
				"new classlib -n Proof.Lib -o src\\Proof.Lib -f net10.0",
				consumerDirectory,
				cancellationToken
			);
			await Assert.That(code).IsEqualTo(0).Because(TestHelpers.GenerateError(stdOut, stdErr));

			var solutionPath = Directory
				.GetFiles(consumerDirectory, "Proof.sln*", SearchOption.TopDirectoryOnly)
				.FirstOrDefault();

			await Assert
				.That(solutionPath)
				.IsNotNull()
				.Because("The generated solution file was not found in the consumer directory.");

			(code, stdOut, stdErr) = await RunProcessAsync(
				"dotnet",
				$"sln \"{solutionPath}\" add \"{Path.Combine(consumerSrcDirectory, "Proof.Lib", "Proof.Lib.csproj")}\"",
				consumerDirectory,
				cancellationToken
			);
			await Assert.That(code).IsEqualTo(0).Because(TestHelpers.GenerateError(stdOut, stdErr));

			await File.WriteAllTextAsync(
				Path.Combine(consumerDirectory, "NuGet.Config"),
				$"""
				<?xml version="1.0" encoding="utf-8"?>
				<configuration>
					<packageSources>
						<clear />
						<add key="local" value="{feedDirectory.Replace('\\', '/')}" />
						<add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
					</packageSources>
					<packageSourceMapping>
						<clear />
						<packageSource key="local">
							<package pattern="Purview.DotNetProjectSdk" />
						</packageSource>
						<packageSource key="nuget.org">
							<package pattern="*" />
						</packageSource>
					</packageSourceMapping>
				</configuration>
				""",
				cancellationToken
			);
			var nugetConfigPath = Path.Combine(consumerDirectory, "NuGet.Config");

			await File.WriteAllTextAsync(
				Path.Combine(consumerSrcDirectory, "Directory.Build.props"),
				$"""
				<Project>
					<PropertyGroup>
						<NamespacePrefix>Proof</NamespacePrefix>
					</PropertyGroup>
					<Import Sdk="Purview.DotNetProjectSdk" Project="Sdk.props" Version="{packageVersion}" />
				</Project>
				""",
				cancellationToken
			);

			await File.WriteAllTextAsync(
				Path.Combine(consumerSrcDirectory, "Directory.Build.targets"),
				$"""
				<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
					<Import Sdk="Purview.DotNetProjectSdk" Project="Sdk.targets" Version="{packageVersion}" />
				</Project>
				""",
				cancellationToken
			);

			(code, stdOut, stdErr) = await RunProcessAsync(
				"dotnet",
				$"msbuild \"src\\Proof.Lib\\Proof.Lib.csproj\" -nologo -noconlog -p:RestoreConfigFile=\"{nugetConfigPath}\" -getProperty:EditorConfigFilePath -getItem:EditorConfigFiles",
				consumerDirectory,
				cancellationToken
			);
			await Assert.That(code).IsEqualTo(0).Because(TestHelpers.GenerateError(stdOut, stdErr));

			var evaluationJsonStart = stdOut.IndexOf('{', StringComparison.Ordinal);
			await Assert.That(evaluationJsonStart >= 0).IsTrue();
			var evaluationJson = stdOut[evaluationJsonStart..];

			using var doc = JsonDocument.Parse(evaluationJson);
			var editorConfigPath = doc
				.RootElement.GetProperty("Properties")
				.GetProperty("EditorConfigFilePath")
				.GetString();

			await Assert
				.That(string.IsNullOrWhiteSpace(editorConfigPath))
				.IsFalse()
				.Because("EditorConfigFilePath property is missing or empty.");
			await Assert
				.That(File.Exists(editorConfigPath!))
				.IsTrue()
				.Because($"EditorConfig file not found at path: {editorConfigPath}");

			var itemPaths = doc
				.RootElement.GetProperty("Items")
				.GetProperty("EditorConfigFiles")
				.EnumerateArray()
				.Select(item => item.GetProperty("Identity").GetString())
				.Where(path => !string.IsNullOrWhiteSpace(path))
				.Select(path => Path.GetFullPath(path!).TrimEnd('\\', '/'))
				.ToArray();

			var normalizedEditorConfigPath = Path.GetFullPath(editorConfigPath!).TrimEnd('\\', '/');
			await Assert
				.That(
					itemPaths.Any(path =>
						string.Equals(path, normalizedEditorConfigPath, StringComparison.OrdinalIgnoreCase)
					)
				)
				.IsTrue();

			var editorConfigContent = await File.ReadAllTextAsync(editorConfigPath!, cancellationToken);
			await Assert
				.That(editorConfigContent)
				.Contains("csharp_prefer_braces = when_possible:error")
				.Because($"EditorConfig file at path {editorConfigPath} does not contain the expected content.");

			(code, stdOut, stdErr) = await RunProcessAsync(
				"dotnet",
				$"msbuild \"src\\Proof.Lib\\Proof.Lib.csproj\" -nologo -p:RestoreConfigFile=\"{nugetConfigPath}\" -t:EnsureRepositoryEditorConfigTarget -getProperty:RepositoryEditorConfigFilePath",
				consumerDirectory,
				cancellationToken
			);
			await Assert.That(code).IsEqualTo(0).Because(TestHelpers.GenerateError(stdOut, stdErr));

			var repositoryEditorConfigPath = stdOut.Trim();
			await Assert
				.That(string.IsNullOrWhiteSpace(repositoryEditorConfigPath))
				.IsFalse()
				.Because("RepositoryEditorConfigFilePath property is missing or empty.");
			repositoryEditorConfigPath = Path.GetFullPath(repositoryEditorConfigPath!);
			await Assert
				.That(repositoryEditorConfigPath)
				.IsEqualTo(Path.Combine(consumerDirectory, ".editorconfig"))
				.Because("RepositoryEditorConfigFilePath does not match the expected path.");

			await Assert
				.That(File.Exists(repositoryEditorConfigPath))
				.IsTrue()
				.Because($"Repository EditorConfig file not found at path: {repositoryEditorConfigPath}");

			var repositoryEditorConfigContent = await File.ReadAllTextAsync(
				repositoryEditorConfigPath,
				cancellationToken
			);
			await Assert
				.That(repositoryEditorConfigContent)
				.Contains("csharp_prefer_braces = when_possible:error")
				.Because(
					$"Repository EditorConfig file at path {repositoryEditorConfigPath} does not contain the expected content."
				);

			(code, stdOut, stdErr) = await RunProcessAsync(
				"dotnet",
				$"msbuild \"src\\Proof.Lib\\Proof.Lib.csproj\" -nologo -p:RestoreConfigFile=\"{nugetConfigPath}\" -t:EnsureRepositoryGlobalJsonTarget -getProperty:RepositoryGlobalJsonFilePath",
				consumerDirectory,
				cancellationToken
			);
			await Assert.That(code).IsEqualTo(0).Because(TestHelpers.GenerateError(stdOut, stdErr));

			var repositoryGlobalJsonPath = stdOut.Trim();
			await Assert
				.That(string.IsNullOrWhiteSpace(repositoryGlobalJsonPath))
				.IsFalse()
				.Because("RepositoryGlobalJsonFilePath property is missing or empty.");
			repositoryGlobalJsonPath = Path.GetFullPath(repositoryGlobalJsonPath!);
			await Assert
				.That(repositoryGlobalJsonPath)
				.IsEqualTo(Path.Combine(consumerDirectory, "global.json"))
				.Because("RepositoryGlobalJsonFilePath does not match the expected path.");
			await Assert
				.That(File.Exists(repositoryGlobalJsonPath))
				.IsTrue()
				.Because($"Repository GlobalJson file not found at path: {repositoryGlobalJsonPath}");

			var repositoryGlobalJsonContent = await File.ReadAllTextAsync(repositoryGlobalJsonPath, cancellationToken);
			await Assert
				.That(repositoryGlobalJsonContent)
				.Contains("\"runner\": \"Microsoft.Testing.Platform\"")
				.Because(
					$"Repository GlobalJson file at path {repositoryGlobalJsonPath} does not contain the expected content."
				);
			await Assert
				.That(repositoryGlobalJsonContent)
				.Contains("\"Purview.DotNetProjectSdk\"")
				.Because(
					$"Repository GlobalJson file at path {repositoryGlobalJsonPath} does not contain the expected content."
				);
		}
		finally
		{
			if (Directory.Exists(tempRoot))
				Directory.Delete(tempRoot, recursive: true);
		}
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
