using System.Globalization;
using optimizerDuck.Domain.Optimizations.Models.ScheduledTask;
using optimizerDuck.Services.Configuration;

namespace optimizerDuck.Test.Domain.Optimizations.Models.ScheduledTask;

public class ScheduledTaskTriggerTests
{
    [Fact]
    public void TriggerTypes_ResolveLocalizedLabelsFromInfos()
    {
        var model = new ScheduledTaskModel
        {
            Name = "T",
            Path = "\\",
            FullPath = "\\T",
            TriggerInfos =
            [
                new("ScheduledTasks.Trigger.Logon", null, [], null),
                new(
                    "ScheduledTasks.Trigger.Daily",
                    "ScheduledTasks.TriggerDetail.Daily",
                    ["1", "9:00 AM"],
                    null
                ),
            ],
        };
        Assert.Equal(
            [
                Loc.Instance["ScheduledTasks.Trigger.Logon"],
                Loc.Instance["ScheduledTasks.Trigger.Daily"],
            ],
            model.TriggerTypes
        );
        Assert.Equal(
            string.Format(
                CultureInfo.CurrentCulture,
                "{0}; {1}",
                Loc.Instance["ScheduledTasks.Trigger.Logon"],
                string.Format(
                    CultureInfo.CurrentCulture,
                    Loc.Instance["ScheduledTasks.TriggerDetail.Daily"],
                    "1",
                    "9:00 AM"
                )
            ),
            model.TriggerSummary
        );
    }

    [Fact]
    public void TriggerTypes_AfterCultureChange_ResolveInNewLanguage()
    {
        var original = Loc.CurrentCulture;
        try
        {
            var model = new ScheduledTaskModel
            {
                Name = "T",
                Path = "\\",
                FullPath = "\\T",
                TriggerInfos = [new("ScheduledTasks.Trigger.Boot", null, [], null)],
            };

            Loc.Instance.ChangeCulture(new CultureInfo("vi"));
            var vi = model.TriggerTypes[0];

            Loc.Instance.ChangeCulture(new CultureInfo("en-US"));
            var en = model.TriggerTypes[0];

            // Keys exist in resx; the resolved text must not be the raw key or a raw ToString.
            Assert.DoesNotContain("ScheduledTasks.Trigger.Boot", vi);
            Assert.DoesNotContain("ScheduledTasks.Trigger.Boot", en);
            Assert.NotEqual("Boot", vi);
        }
        finally
        {
            Loc.Instance.ChangeCulture(original);
        }
    }

    [Fact]
    public void TriggerSummary_DetailNarratesTriggerData()
    {
        var model = new ScheduledTaskModel
        {
            Name = "T",
            Path = "\\",
            FullPath = "\\T",
            TriggerInfos =
            [
                new(
                    "ScheduledTasks.Trigger.Daily",
                    "ScheduledTasks.TriggerDetail.Daily",
                    ["1", "9:00 AM"],
                    null
                ),
            ],
        };

        var expected = string.Format(
            CultureInfo.CurrentCulture,
            Loc.Instance["ScheduledTasks.TriggerDetail.Daily"],
            "1",
            "9:00 AM"
        );
        Assert.Equal(expected, model.TriggerSummary);
    }

    [Fact]
    public void TriggerSummary_RawFallbackUsedOnlyWhenNoLocalizationFits()
    {
        var model = new ScheduledTaskModel
        {
            Name = "T",
            Path = "\\",
            FullPath = "\\T",
            TriggerInfos =
            [
                new("ScheduledTasks.Trigger.Event", null, [], "On workstation unlock"),
                new(
                    "ScheduledTasks.Trigger.Time",
                    "ScheduledTasks.TriggerDetail.Time",
                    ["x"],
                    null
                ),
            ],
        };

        var expectedTime = string.Format(
            CultureInfo.CurrentCulture,
            Loc.Instance["ScheduledTasks.TriggerDetail.Time"],
            "x"
        );
        Assert.Equal($"On workstation unlock; {expectedTime}", model.TriggerSummary);
        Assert.Equal(Loc.Instance["ScheduledTasks.Trigger.Event"], model.TriggerTypes[0]);
    }

    [Fact]
    public void TriggerSummary_RepetitionAppendedFromOwnTemplate()
    {
        var model = new ScheduledTaskModel
        {
            Name = "T",
            Path = "\\",
            FullPath = "\\T",
            TriggerInfos =
            [
                new(
                    "ScheduledTasks.Trigger.Daily",
                    "ScheduledTasks.TriggerDetail.Daily",
                    ["1", "9:00 AM"],
                    null,
                    "ScheduledTasks.TriggerDetail.Repeat",
                    ["00:10:00"]
                ),
            ],
        };

        var expected =
            string.Format(
                CultureInfo.CurrentCulture,
                Loc.Instance["ScheduledTasks.TriggerDetail.Daily"],
                "1",
                "9:00 AM"
            )
            + string.Format(
                CultureInfo.CurrentCulture,
                Loc.Instance["ScheduledTasks.TriggerDetail.Repeat"],
                "00:10:00"
            );
        Assert.Equal(expected, model.TriggerSummary);
        // Badges stay short labels; the repetition lives in the summary only.
        Assert.Equal([Loc.Instance["ScheduledTasks.Trigger.Daily"]], model.TriggerTypes);
    }
}
