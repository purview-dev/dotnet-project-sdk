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
	static readonly string TempBase =
		Path.Combine(Path.GetTempPath(), "PurviewSdkTests");

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
	public static ProjectHarness Create(
		string projectName,
		string sdk = "Microsoft.NET.Sdk",
		string targetFramework = "net10.0",
		string namespacePrefix = "Test",
		bool withDockerfile = false,
		string? extraProps = null,
		string? extraItems = null,
		IDictionary<string, string>? extraEnv = null)
	{
		var workDir = Path.Combine(TempBase, Guid.NewGuid().ToString("N"));
		var harness = new ProjectHarness(workDir, projectName);
		harness.WriteBoilerplate(namespacePrefix);

		var propBlock = extraProps is null ? "" :
			$"\n\t<PropertyGroup>\n\t\t{extraProps}\n\t</PropertyGroup>";
		var itemBlock = extraItems is null ? "" :
			$"\n\t<ItemGroup>\n\t\t{extraItems}\n\t</ItemGroup>";

		File.WriteAllText(harness.ProjectFilePath,
			$"""
			<Project Sdk="{sdk}">
				<PropertyGroup>
					<TargetFramework>{targetFramework}</TargetFramework>
				</PropertyGroup>{propBlock}{itemBlock}
			</Project>
			""");

		if (withDockerfile)
			File.WriteAllText(
				Path.Combine(harness.ProjectDirectory, "Dockerfile"),
				"FROM mcr.microsoft.com/dotnet/runtime:10.0");

		harness._extraEnv = extraEnv;
		return harness;
	}

	/// <summary>
	/// Creates a consumer project with fully custom file content.
	/// The directory still gets the standard Directory.Build.props/targets bootstrapping.
	/// </summary>
	public static ProjectHarness CreateWithContent(
		string projectName,
		string projectFileContent,
		string namespacePrefix = "Test")
	{
		var workDir = Path.Combine(TempBase, Guid.NewGuid().ToString("N"));
		var harness = new ProjectHarness(workDir, projectName);
		harness.WriteBoilerplate(namespacePrefix);
		File.WriteAllText(harness.ProjectFilePath, projectFileContent);
		return harness;
	}

	void WriteBoilerplate(string namespacePrefix)
	{
		Directory.CreateDirectory(ProjectDirectory);

		File.WriteAllText(
			Path.Combine(ProjectDirectory, "Directory.Build.props"),
			$"""
			<Project>
				<PropertyGroup>
					<NamespacePrefix>{namespacePrefix}</NamespacePrefix>
				</PropertyGroup>
				<Import Project="{SdkPaths.SdkDirectory}/Sdk.props" />
			</Project>
			""");

		File.WriteAllText(
			Path.Combine(ProjectDirectory, "Directory.Build.targets"),
			$"""
			<Project xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
				<Import Project="{SdkPaths.SdkDirectory}/Sdk.targets" />
			</Project>
			""");

		File.WriteAllText(
			Path.Combine(ProjectDirectory, "Directory.Packages.props"),
			"""
			<Project>
				<PropertyGroup>
					<CentralPackageFloatingVersionsEnabled>true</CentralPackageFloatingVersionsEnabled>
				</PropertyGroup>
			</Project>
			""");
	}

	/// <summary>
	/// Evaluates one or more MSBuild properties via <c>dotnet msbuild -getProperty</c>
	/// without triggering a build or package restore.
	/// </summary>
	public async Task<IReadOnlyDictionary<string, string>> GetPropertiesAsync(
		params string[] propertyNames)
	{
		if (propertyNames.Length == 0)
			return ImmutableDictionary<string, string>.Empty;

		var propList = string.Join(",", propertyNames);
		var args = $"msbuild \"{ProjectFilePath}\" -nologo -noconlog -getProperty:{propList}";

		var (_, stdout, _) = await RunAsync("dotnet", args);

		stdout = stdout.Trim();

		// -getProperty outputs plain text for a single property, JSON for multiple.
		var jsonStart = stdout.IndexOf('{', StringComparison.Ordinal);
		if (jsonStart < 0)
		{
			// Single property — plain text value.
			return propertyNames.Length == 1
				? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
				{ [propertyNames[0]] = stdout }
				: propertyNames.ToDictionary(
					p => p, _ => (string)"", StringComparer.OrdinalIgnoreCase);
		}

		var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
		try
		{
			using var doc = JsonDocument.Parse(stdout[jsonStart..]);
			if (doc.RootElement.TryGetProperty("Properties", out var propsEl))
				foreach (var prop in propsEl.EnumerateObject())
					result[prop.Name] = prop.Value.GetString() ?? string.Empty;
		}
		catch (JsonException) { /* return what we have */ }

		return result;
	}

	/// <summary>Evaluates a single MSBuild property without building.</summary>
	public async Task<string> GetPropertyAsync(string propertyName)
	{
		var props = await GetPropertiesAsync(propertyName);
		return props.TryGetValue(propertyName, out var v) ? v : string.Empty;
	}

	/// <summary>
	/// Evaluates one or more MSBuild items via <c>dotnet msbuild -getItem</c>
	/// without triggering a build or package restore.
	/// </summary>
	public async Task<IReadOnlyList<string>> GetItemIdentitiesAsync(string itemType)
	{
		var args = $"msbuild \"{ProjectFilePath}\" -nologo -noconlog -getItem:{itemType}";
		var (_, stdout, _) = await RunAsync("dotnet", args);

		var jsonStart = stdout.Trim().IndexOf('{', StringComparison.Ordinal);
		if (jsonStart < 0)
			return [];

		try
		{
			using var doc = JsonDocument.Parse(stdout[jsonStart..]);
			if (doc.RootElement.TryGetProperty("Items", out var itemsEl) &&
				itemsEl.TryGetProperty(itemType, out var typeEl))
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
		catch (JsonException) { /* fall through */ }

		return [];
	}

	/// <summary>
	/// Runs a full build of the consumer project.
	/// Pass <paramref name="restore"/>=<c>true</c> when the project references NuGet packages.
	/// </summary>
	public async Task<(bool Success, string Output, string Errors)> BuildAsync(
		bool restore = false)
	{
		var restoreFlag = restore ? "" : "--no-restore ";
		var args = $"build \"{ProjectFilePath}\" {restoreFlag}-nologo -v:quiet";
		var (code, stdout, stderr) = await RunAsync("dotnet", args);
		return (code == 0, stdout, stderr);
	}

	async Task<(int Code, string StdOut, string StdErr)> RunAsync(
		string fileName, string arguments)
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
			}
		};

		if (_extraEnv is not null)
			foreach (var (key, value) in _extraEnv)
				process.StartInfo.Environment[key] = value;

		process.Start();
		var stdoutTask = process.StandardOutput.ReadToEndAsync();
		var stderrTask = process.StandardError.ReadToEndAsync();
		await process.WaitForExitAsync();

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
