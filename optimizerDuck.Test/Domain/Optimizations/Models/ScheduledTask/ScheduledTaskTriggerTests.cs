using System.Globalization;
using optimizerDuck.Domain.Optimizations.Models.ScheduledTask;
using optimizerDuck.Services.Configuration;

namespace optimizerDuck.Test.Domain.Optimizations.Models.ScheduledTask;

public class ScheduledTaskTriggerTests
{
    [Fact]
    public void TriggerBadges_ResolveLocalizedLabelsFromInfos()
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
            model.TriggerBadges.Select(b => b.Label)
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
    public void TriggerBadges_CarryTheDetailEachChipTooltips()
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

        var detail = string.Format(
            CultureInfo.CurrentCulture,
            Loc.Instance["ScheduledTasks.TriggerDetail.Daily"],
            "1",
            "9:00 AM"
        );

        // A trigger without its own detail falls back to the label, so the tooltip still reads.
        Assert.Equal(Loc.Instance["ScheduledTasks.Trigger.Logon"], model.TriggerBadges[0].Detail);
        Assert.Equal(detail, model.TriggerBadges[1].Detail);
    }

    [Fact]
    public void TriggerBadges_AfterCultureChange_ResolveInNewLanguage()
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
            var vi = model.TriggerBadges[0].Label;

            Loc.Instance.ChangeCulture(new CultureInfo("en-US"));
            var en = model.TriggerBadges[0].Label;

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
        Assert.Equal(Loc.Instance["ScheduledTasks.Trigger.Event"], model.TriggerBadges[0].Label);
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
        Assert.Equal(
            [Loc.Instance["ScheduledTasks.Trigger.Daily"]],
            model.TriggerBadges.Select(b => b.Label)
        );
    }
}
