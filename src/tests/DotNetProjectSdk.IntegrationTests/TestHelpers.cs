namespace Purview.DotNetProjectSdk;

public static class TestHelpers
{
	public static string GenerateError(string stdOut, string stdErr)
	{
		var msg = "";
		if (!string.IsNullOrWhiteSpace(stdOut))
			msg += "Standard Output:\n" + stdOut + "\n";
		if (!string.IsNullOrWhiteSpace(stdErr))
			msg += "Standard Error:\n" + stdErr + "\n";

		msg = msg?.Trim();

		if (string.IsNullOrWhiteSpace(msg))
			msg = "No additional information returned";

		return msg;
	}
}
