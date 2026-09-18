using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using optimizerDuck.Common.Helpers;
using Xunit;

[assembly: AssemblyFixture(typeof(optimizerDuck.Test.Infrastructure.AppDataCleanupFixture))]

namespace optimizerDuck.Test.Infrastructure;

/// <summary>
///     Removes what the suite's own doubles leave behind in the app's data folders once the whole
///     assembly has finished. Only items whose recorded name looks like a test double are deleted,
///     so a real optimization's revert data or record is never touched, even when the app is used
///     while the suite runs.
/// </summary>
public sealed class AppDataCleanupFixture : IDisposable
{
    private static readonly string[] TestNameMarkers = ["Test", "Stub", "Sample", "Fake", "Mock"];

    /// <summary>The folder, and the JSON field that names the item inside it.</summary>
    private static readonly (string Directory, string NameField)[] Folders =
    [
        (Shared.RevertDirectory, "OptimizationName"),
        (Shared.HistoryDirectory, "OptimizationKey"),
    ];

    public void Dispose()
    {
        var removed = 0;

        foreach (var (directory, nameField) in Folders)
        {
            if (!Directory.Exists(directory))
                continue;

            foreach (
                var file in Directory
                    .GetFiles(directory, "*.json")
                    .Concat(Directory.GetFiles(directory, "*.tmp"))
            )
            {
                try
                {
                    var name = JObject
                        .Parse(File.ReadAllText(file, Encoding.UTF8))[nameField]
                        ?.ToString();
                    if (
                        name is null
                        || !TestNameMarkers.Any(marker =>
                            name.Contains(marker, StringComparison.OrdinalIgnoreCase)
                        )
                    )
                        continue;

                    File.Delete(file);
                    removed++;
                }
                catch
                {
                    // A file that cannot be read or deleted is left where it is: cleaning up is a
                    // courtesy, and guessing would risk a real optimization's data.
                }
            }
        }

        Console.WriteLine($"Cleaned {removed} test file(s) from the app data folders.");
    }
}
