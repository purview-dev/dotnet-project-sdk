using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;

namespace Purview.DotNetProjectSdk.Harness;

/// <summary>
/// Creates throwaway consumer projects on disk that import the SDK from source,
/// allowing integration tests to invoke MSBuild and assert on observable build behaviour.
/// </summary>
sealed class ProjectHarness : IAsyncDisposable
{
	static readonly string TempBase = Path.Combine(Path.GetTempPath(), "PurviewSdkTests");

	readonly string _workDir;

	IDictionary<string, string>? _extraEnv;

	public string ProjectName { get; }

	public string ProjectDirectory { get; }

	public string ProjectFilePath { get; }

	ProjectHarness(string workDir, string projectName)
	{
		_workDir = workDir;
		ProjectName = projectName;
		ProjectDirectory = Path.Combine(workDir, projectName);
		ProjectFilePath = Path.Combine(ProjectDirectory, $"{projectName}.csproj");
	}

	/// <summary>
	/// Creates a standard SDK-style consumer project.
	/// </summary>
	public static async Task<ProjectHarness> CreateAsync(
		string projectName,
		string sdk = "Microsoft.NET.Sdk",
		string targetFramework = "net10.0",
		string namespacePrefix = "Test",
		bool withDockerfile = false,
		string? extraProps = null,
		string? extraItems = null,
		IDictionary<string, string>? extraEnv = null,
		string? preImportProps = null,
		CancellationToken cancellationToken = default
	)
	{
		var workDir = Path.Combine(TempBase, Guid.NewGuid().ToString("N"));
		ProjectHarness harness = new(workDir, projectName);
		await harness.WriteBoilerplateAsync(namespacePrefix, preImportProps, cancellationToken);

		var propBlock = extraProps is null ? "" : $"\n\t<PropertyGroup>\n\t\t{extraProps}\n\t</PropertyGroup>";
		var itemBlock = extraItems is null ? "" : $"\n\t<ItemGroup>\n\t\t{extraItems}\n\t</ItemGroup>";

		await File.WriteAllTextAsync(
			harness.ProjectFilePath,
			$"""
			<Project Sdk="{sdk}">
				<PropertyGroup>
					<TargetFramework>{targetFramework}</TargetFramework>
				</PropertyGroup>{propBlock}{itemBlock}
			</Project>
			""",
			cancellationToken
		);

		if (withDockerfile)
		{
			await File.WriteAllTextAsync(
				Path.Combine(harness.ProjectDirectory, "Dockerfile"),
				"FROM mcr.microsoft.com/dotnet/runtime:10.0",
				cancellationToken
			);
		}

		harness._extraEnv = extraEnv;
		return harness;
	}

	/// <summary>
	/// Creates a consumer project with fully custom file content.
	/// The directory still gets the standard Directory.Build.props/targets bootstrapping.
	/// </summary>
	public static async Task<ProjectHarness> CreateWithContentAsync(
		string projectName,
		string projectFileContent,
		string namespacePrefix = "Test",
		CancellationToken cancellationToken = default
	)
	{
		var workDir = Path.Combine(TempBase, Guid.NewGuid().ToString("N"));
		ProjectHarness harness = new(workDir, projectName);
		await harness.WriteBoilerplateAsync(namespacePrefix, preImportProps: null, cancellationToken);
		await File.WriteAllTextAsync(harness.ProjectFilePath, projectFileContent, cancellationToken);

		return harness;
	}

	async Task WriteBoilerplateAsync(
		string namespacePrefix,
		string? preImportProps,
		CancellationToken cancellationToken
	)
	{
		Directory.CreateDirectory(ProjectDirectory);

		var preImportBlock = preImportProps is null
			? ""
			: $"\n\t<PropertyGroup>\n\t\t{preImportProps}\n\t</PropertyGroup>";

		await File.WriteAllTextAsync(
			Path.Combine(ProjectDirectory, "Directory.Build.props"),
			$"""
			<Project>
				<PropertyGroup>
					<NamespacePrefix>{namespacePrefix}</NamespacePrefix>
				</PropertyGroup>{preImportBlock}
				<Import Project="{SdkPaths.SdkDirectory}/Sdk.props" />
			</Project>
			""",
			cancellationToken
		);

		await File.WriteAllTextAsync(
			Path.Combine(ProjectDirectory, "Directory.Build.targets"),
			$"""
			<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
				<Import Project="{SdkPaths.SdkDirectory}/Sdk.targets" />
			</Project>
			""",
			cancellationToken
		);

		await File.WriteAllTextAsync(
			Path.Combine(ProjectDirectory, "Directory.Packages.props"),
			"""
			<Project>
				<PropertyGroup>
					<CentralPackageFloatingVersionsEnabled>true</CentralPackageFloatingVersionsEnabled>
				</PropertyGroup>
			</Project>
			""",
			cancellationToken
		);
	}

	/// <summary>
	/// Evaluates one or more MSBuild properties via <c>dotnet msbuild -getProperty</c>
	/// without triggering a build or package restore.
	/// </summary>
	public async Task<IReadOnlyDictionary<string, string>> GetPropertiesAsync(
		CancellationToken cancellationToken,
		params string[] propertyNames
	)
	{
		if (propertyNames.Length == 0)
			return ImmutableDictionary<string, string>.Empty;

		var propList = string.Join(",", propertyNames);
		var args = $"msbuild \"{ProjectFilePath}\" -nologo -noconlog -getProperty:{propList}";

		var (_, stdout, _) = await RunAsync("dotnet", args, cancellationToken);

		stdout = stdout.Trim();

		// -getProperty outputs plain text for a single property, JSON for multiple.
		var jsonStart = stdout.IndexOf('{', StringComparison.Ordinal);
		if (jsonStart < 0)
		{
			// Single property — plain text value.
			return propertyNames.Length == 1
				? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [propertyNames[0]] = stdout }
				: propertyNames.ToDictionary(p => p, _ => "", StringComparer.OrdinalIgnoreCase);
		}

		var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			using var doc = JsonDocument.Parse(stdout[jsonStart..]);
			if (doc.RootElement.TryGetProperty("Properties", out var propsEl))
				foreach (var prop in propsEl.EnumerateObject())
					result[prop.Name] = prop.Value.GetString() ?? string.Empty;
		}
		catch (JsonException)
		{ /* return what we have */
		}

		return result;
	}

	/// <summary>Evaluates a single MSBuild property without building.</summary>
	public async Task<string> GetPropertyAsync(string propertyName, CancellationToken cancellationToken)
	{
		var props = await GetPropertiesAsync(cancellationToken, propertyName);
		return props.TryGetValue(propertyName, out var v) ? v : string.Empty;
	}

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
		var (_, stdout, _) = await RunAsync("dotnet", args, cancellationToken);

		var jsonStart = stdout.Trim().IndexOf('{', StringComparison.Ordinal);
		if (jsonStart < 0)
			return [];

		try
		{
			using var doc = JsonDocument.Parse(stdout[jsonStart..]);
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
		catch (JsonException)
		{ /* fall through */
		}

		return [];
	}

	/// <summary>
	/// Evaluates metadata values for a given MSBuild item type.
	/// </summary>
	public async Task<IReadOnlyList<string>> GetItemMetadataValuesAsync(
		string itemType,
		string metadataName,
		CancellationToken cancellationToken = default
	)
	{
		var args = $"msbuild \"{ProjectFilePath}\" -nologo -noconlog -getItem:{itemType}";
		var (_, stdout, _) = await RunAsync("dotnet", args, cancellationToken);

		var jsonStart = stdout.Trim().IndexOf('{', StringComparison.Ordinal);
		if (jsonStart < 0)
			return [];

		try
		{
			using var doc = JsonDocument.Parse(stdout[jsonStart..]);
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
		{ /* fall through */
		}

		return [];
	}

	public async Task<(int Code, string StdOut, string StdErr)> RunMSBuildAsync(
		string msbuildArguments,
		CancellationToken cancellationToken = default
	)
	{
		var args = $"msbuild \"{ProjectFilePath}\" -nologo {msbuildArguments}";
		return await RunAsync("dotnet", args, cancellationToken);
	}

	/// <summary>
	/// Runs a full build of the consumer project.
	/// Pass <paramref name="restore"/>=<c>true</c> when the project references NuGet packages.
	/// </summary>
	public async Task<(bool Success, string Output, string Errors)> BuildAsync(
		bool restore = false,
		bool verbose = false,
		CancellationToken cancellationToken = default
	)
	{
		var restoreFlag = restore ? "" : "--no-restore ";
		var verbosityFlag = verbose ? "-v:detailed" : "-v:quiet";
		var args = $"build \"{ProjectFilePath}\" {restoreFlag}-nologo {verbosityFlag}";
		var (code, stdout, stderr) = await RunAsync("dotnet", args, cancellationToken);
		return (code == 0, stdout, stderr);
	}

	async Task<(int Code, string StdOut, string StdErr)> RunAsync(
		string fileName,
		string arguments,
		CancellationToken cancellationToken
	)
	{
		using var process = new Process
		{
			StartInfo = new ProcessStartInfo
			{
				FileName = fileName,
				Arguments = arguments,
				WorkingDirectory = ProjectDirectory,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			},
		};

		if (_extraEnv is not null)
		{
			foreach (var (key, value) in _extraEnv)
				process.StartInfo.Environment[key] = value;
		}

		process.Start();
		var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
		var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

		await process.WaitForExitAsync(cancellationToken);

		return (process.ExitCode, await stdoutTask, await stderrTask);
	}

	public async ValueTask DisposeAsync()
	{
		try
		{
			if (Directory.Exists(_workDir))
				Directory.Delete(_workDir, recursive: true);
		}
		catch (IOException)
		{
			// Best-effort cleanup; don't fail tests on leftover temp files.
		}
	}
}
