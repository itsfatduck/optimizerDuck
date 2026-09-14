using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Execution;

namespace optimizerDuck.Services.History;

/// <summary>
///     Stores what one apply did, one file per item, so the details view can show it again after
///     the app restarts. It writes outside the revert data on purpose: a revert deletes its own
///     file, and what a run did should stay readable afterwards.
/// </summary>
public static class ChangeRecordStore
{
    /// <summary>The file this item's most recent apply is recorded in.</summary>
    public static string PathFor(Guid id) => Path.Combine(Shared.HistoryDirectory, $"{id}.json");

    /// <summary>
    ///     Writes a record, replacing the item's previous one. Best effort: a record that cannot
    ///     be written is logged and never changes the outcome of the apply, so callers do not
    ///     have to guard the call.
    /// </summary>
    public static void TryWrite(ChangeRecord record, ILogger? logger = null)
    {
        try
        {
            var path = PathFor(record.Id);
            Directory.CreateDirectory(Shared.HistoryDirectory);

            // Atomic like the revert files: a torn report would be worse than no report. No file
            // lock is needed because an item is applied through one command at a time, and the
            // replace makes a partial write impossible.
            var tempPath = path + ".tmp";
            File.WriteAllText(
                tempPath,
                JsonConvert.SerializeObject(record, Formatting.Indented),
                Encoding.UTF8
            );

            if (File.Exists(path))
                File.Replace(tempPath, path, destinationBackupFileName: null);
            else
                File.Move(tempPath, path);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(
                ex,
                "Failed to write the change record for {Key}",
                record.OptimizationKey
            );
        }
    }

    /// <summary>
    ///     Reads the record of the item's last apply, or <see langword="null" /> when there is
    ///     none or it cannot be read. An unreadable record is reported as no record, never as an
    ///     empty run.
    /// </summary>
    public static ChangeRecord? TryRead(Guid id, ILogger? logger = null)
    {
        try
        {
            var path = PathFor(id);
            if (!File.Exists(path))
                return null;

            return JsonConvert.DeserializeObject<ChangeRecord>(
                File.ReadAllText(path, Encoding.UTF8)
            );
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Failed to read the change record for {Id}", id);
            return null;
        }
    }
}
