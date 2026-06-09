using Purview.DotNetProjectSdk.Harness;

namespace Purview.DotNetProjectSdk.Tests;

/// <summary>
/// Verifies that IsCLIProject property is correctly detected based on project naming conventions.
/// Projects ending with CLI, Console, or CommandLine (case-insensitive) should be classified
/// as CLI projects.
/// </summary>
public sealed class IsCLIProjectTests
{
	/// <summary>
	/// Helper to create a project and evaluate its IsCLIProject property.
	/// </summary>
	static async Task<(ProjectHarness Harness, bool IsCLIProject)> CreateProjectAndEvaluateAsync(
		string projectName,
		CancellationToken cancellationToken = default
	)
	{
		var harness = await ProjectHarness.CreateAsync(projectName, cancellationToken: cancellationToken);
		var isCLIProjectValue = await harness.GetPropertyAsync("IsCLIProject", cancellationToken);
		var isCLIProject = isCLIProjectValue.Equals("true", StringComparison.OrdinalIgnoreCase);

		return (harness, isCLIProject);
	}

	[Test]
	public async Task ProjectEndingWithCLI_Uppercase_IsCLIProject(CancellationToken cancellationToken)
	{
		var (harness, isCLIProject) = await CreateProjectAndEvaluateAsync("MyCLI", cancellationToken);

		await using (harness)
		{
			await Assert.That(isCLIProject).IsTrue();
		}
	}

	[Test]
	public async Task ProjectEndingWithCLI_PartOfCompoundName_IsCLIProject(CancellationToken cancellationToken)
	{
		var (harness, isCLIProject) = await CreateProjectAndEvaluateAsync("MyAppCLI", cancellationToken);

		await using (harness)
		{
			await Assert.That(isCLIProject).IsTrue();
		}
	}

	[Test]
	public async Task ProjectEndingWithConsole_Uppercase_IsCLIProject(CancellationToken cancellationToken)
	{
		var (harness, isCLIProject) = await CreateProjectAndEvaluateAsync("MyConsole", cancellationToken);

		await using (harness)
		{
			await Assert.That(isCLIProject).IsTrue();
		}
	}

	[Test]
	public async Task ProjectEndingWithConsoleApp_IsCLIProject(CancellationToken cancellationToken)
	{
		var (harness, isCLIProject) = await CreateProjectAndEvaluateAsync("MyAppConsole", cancellationToken);

		await using (harness)
		{
			await Assert.That(isCLIProject).IsTrue();
		}
	}

	[Test]
	public async Task ProjectEndingWithCommandLine_Uppercase_IsCLIProject(CancellationToken cancellationToken)
	{
		var (harness, isCLIProject) = await CreateProjectAndEvaluateAsync("MyCommandLine", cancellationToken);

		await using (harness)
		{
			await Assert.That(isCLIProject).IsTrue();
		}
	}

	[Test]
	public async Task ProjectEndingWithCommandLineApp_IsCLIProject(CancellationToken cancellationToken)
	{
		var (harness, isCLIProject) = await CreateProjectAndEvaluateAsync("MyAppCommandLine", cancellationToken);

		await using (harness)
		{
			await Assert.That(isCLIProject).IsTrue();
		}
	}

	[Test]
	public async Task ProjectEndingWithcli_Lowercase_IsNotCLIProject(CancellationToken cancellationToken)
	{
		var (harness, isCLIProject) = await CreateProjectAndEvaluateAsync("Myappcli", cancellationToken);

		await using (harness)
		{
			await Assert.That(isCLIProject).IsFalse();
		}
	}

	[Test]
	public async Task ProjectEndingWithconsole_Lowercase_IsNotCLIProject(CancellationToken cancellationToken)
	{
		var (harness, isCLIProject) = await CreateProjectAndEvaluateAsync("Myappconsole", cancellationToken);

		await using (harness)
		{
			await Assert.That(isCLIProject).IsFalse();
		}
	}

	[Test]
	public async Task ProjectEndingWithcommandline_Lowercase_IsNotCLIProject(CancellationToken cancellationToken)
	{
		var (harness, isCLIProject) = await CreateProjectAndEvaluateAsync("Myappcommandline", cancellationToken);

		await using (harness)
		{
			await Assert.That(isCLIProject).IsFalse();
		}
	}

	[Test]
	public async Task ProjectEndingWithCommandLIne_MixedCase_IsNotCLIProject(CancellationToken cancellationToken)
	{
		var (harness, isCLIProject) = await CreateProjectAndEvaluateAsync("MyappCommandLIne", cancellationToken);

		await using (harness)
		{
			await Assert.That(isCLIProject).IsFalse();
		}
	}

	[Test]
	[Arguments("MyLibrary")]
	[Arguments("MyService")]
	[Arguments("CoreAPI")]
	[Arguments("SharedUtilities")]
	[Arguments("DataAccess")]
	public async Task ProjectNotEndingWithCLIMarkers_IsNotCLIProject(
		string projectName,
		CancellationToken cancellationToken
	)
	{
		var (harness, isCLIProject) = await CreateProjectAndEvaluateAsync(projectName, cancellationToken);

		await using (harness)
		{
			await Assert.That(isCLIProject).IsFalse();
		}
	}

	[Test]
	public async Task ProjectWithCLIInMiddle_NotEndingSufficiently_IsNotCLIProject(CancellationToken cancellationToken)
	{
		var (harness, isCLIProject) = await CreateProjectAndEvaluateAsync("MyCLITools", cancellationToken);

		await using (harness)
		{
			await Assert.That(isCLIProject).IsFalse();
		}
	}

	[Test]
	public async Task ProjectWithConsoleInMiddle_NotEndingSufficiently_IsNotCLIProject(
		CancellationToken cancellationToken
	)
	{
		var (harness, isCLIProject) = await CreateProjectAndEvaluateAsync("MyConsoleTool", cancellationToken);

		await using (harness)
		{
			await Assert.That(isCLIProject).IsFalse();
		}
	}

	[Test]
	public async Task JustCLI_IsStillCLIProject(CancellationToken cancellationToken)
	{
		var (harness, isCLIProject) = await CreateProjectAndEvaluateAsync("CLI", cancellationToken);

		await using (harness)
		{
			await Assert.That(isCLIProject).IsTrue();
		}
	}
}
