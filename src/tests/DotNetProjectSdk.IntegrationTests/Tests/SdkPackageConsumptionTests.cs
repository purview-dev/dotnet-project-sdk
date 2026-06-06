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

		Directory.CreateDirectory(feedDirectory);
		Directory.CreateDirectory(consumerDirectory);

		try
		{
			var sdkProjectPath = Path.GetFullPath(Path.Combine(SdkPaths.SdkDirectory, "..", "DotNetProjectSdk.csproj"));
			var sdkProjectDirectory = Path.GetDirectoryName(sdkProjectPath)
				?? throw new InvalidOperationException("Unable to determine SDK project directory.");

			var packResult = await RunProcessAsync(
				"dotnet",
				$"pack \"{sdkProjectPath}\" -c Release -o \"{feedDirectory}\"",
				sdkProjectDirectory,
				cancellationToken
			);
			await Assert.That(packResult.Code).IsEqualTo(0);

			var packagePath = Directory
				.GetFiles(feedDirectory, "Purview.DotNetProjectSdk.*.nupkg", SearchOption.TopDirectoryOnly)
				.Where(path => !path.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase))
				.OrderByDescending(path => path)
				.FirstOrDefault();
			await Assert.That(packagePath).IsNotNull();

			var packageVersion = Path
				.GetFileNameWithoutExtension(packagePath!)
				["Purview.DotNetProjectSdk.".Length..];
			await Assert.That(string.IsNullOrWhiteSpace(packageVersion)).IsFalse();

			using (var zip = await ZipFile.OpenReadAsync(packagePath!, cancellationToken))
			{
				await Assert.That(zip.Entries.Any(entry => entry.FullName == "Sdk/.editorconfig")).IsTrue();
			}

			var newSolutionResult = await RunProcessAsync(
				"dotnet",
				"new sln -n Proof",
				consumerDirectory,
				cancellationToken
			);
			await Assert.That(newSolutionResult.Code).IsEqualTo(0);

			var newProjectResult = await RunProcessAsync(
				"dotnet",
				"new classlib -n Proof.Lib -f net10.0",
				consumerDirectory,
				cancellationToken
			);
			await Assert.That(newProjectResult.Code).IsEqualTo(0);

			var solutionPath = Directory
				.GetFiles(consumerDirectory, "Proof.sln*", SearchOption.TopDirectoryOnly)
				.FirstOrDefault()
				?? throw new InvalidOperationException("Could not locate generated solution file.");

			var addProjectResult = await RunProcessAsync(
				"dotnet",
				$"sln \"{solutionPath}\" add \"{Path.Combine(consumerDirectory, "Proof.Lib", "Proof.Lib.csproj")}\"",
				consumerDirectory,
				cancellationToken
			);
			await Assert.That(addProjectResult.Code).IsEqualTo(0);

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
				</configuration>
				""",
				cancellationToken
			);

			await File.WriteAllTextAsync(
				Path.Combine(consumerDirectory, "Directory.Build.props"),
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
				Path.Combine(consumerDirectory, "Directory.Build.targets"),
				$"""
				<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
					<Import Sdk="Purview.DotNetProjectSdk" Project="Sdk.targets" Version="{packageVersion}" />
				</Project>
				""",
				cancellationToken
			);

			var evaluationResult = await RunProcessAsync(
				"dotnet",
				"msbuild \"Proof.Lib\\Proof.Lib.csproj\" -nologo -noconlog -getProperty:EditorConfigFilePath -getItem:EditorConfigFiles",
				consumerDirectory,
				cancellationToken
			);
			await Assert.That(evaluationResult.Code).IsEqualTo(0);

			var evaluationJsonStart = evaluationResult.StdOut.IndexOf('{', StringComparison.Ordinal);
			await Assert.That(evaluationJsonStart >= 0).IsTrue();
			var evaluationJson = evaluationResult.StdOut[evaluationJsonStart..];

			using var doc = JsonDocument.Parse(evaluationJson);
			var editorConfigPath = doc
				.RootElement.GetProperty("Properties")
				.GetProperty("EditorConfigFilePath")
				.GetString();

			await Assert.That(string.IsNullOrWhiteSpace(editorConfigPath)).IsFalse();
			await Assert.That(File.Exists(editorConfigPath!)).IsTrue();

			var itemPaths = doc
				.RootElement.GetProperty("Items")
				.GetProperty("EditorConfigFiles")
				.EnumerateArray()
				.Select(item => item.GetProperty("Identity").GetString())
				.Where(path => !string.IsNullOrWhiteSpace(path))
				.Select(path => Path.GetFullPath(path!).TrimEnd('\\', '/'))
				.ToArray();

			var normalizedEditorConfigPath = Path.GetFullPath(editorConfigPath!).TrimEnd('\\', '/');
			await Assert.That(itemPaths.Any(path =>
				string.Equals(path, normalizedEditorConfigPath, StringComparison.OrdinalIgnoreCase))).IsTrue();

			var editorConfigContent = await File.ReadAllTextAsync(editorConfigPath!, cancellationToken);
			await Assert.That(editorConfigContent).Contains("csharp_prefer_braces = when_possible:error");
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
