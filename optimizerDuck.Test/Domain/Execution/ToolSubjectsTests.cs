using System.Reflection;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Abstractions;
using optimizerDuck.Domain.Execution;
using optimizerDuck.Domain.Optimizations.Models;

namespace optimizerDuck.Test.Domain.Execution;

/// <summary>
///     Pins that a tool subject identifies exactly one owner. A shared identifier would make a run
///     of either owner replace the other's record file.
/// </summary>
public class ToolSubjectsTests
{
    [Fact]
    public void EveryToolSubject_IsItsOwnRecord()
    {
        var subjects = DeclaredSubjects();
        Assert.NotEmpty(subjects);

        AssertDistinct(subjects, declared => declared.Subject.Id, nameof(OperationSubject.Id));
        AssertDistinct(subjects, declared => declared.Subject.Key, nameof(OperationSubject.Key));

        foreach (var declared in subjects)
        {
            Assert.False(
                string.IsNullOrWhiteSpace(declared.Subject.Key),
                $"{declared.Name} declares no {nameof(OperationSubject.Key)}."
            );
            Assert.False(
                string.IsNullOrWhiteSpace(declared.Subject.LogName),
                $"{declared.Name} declares no {nameof(OperationSubject.LogName)}."
            );
        }
    }

    [Fact]
    public void EveryToolSubject_UsesARecordIdNoOptimizationUses()
    {
        var optimizationIds = DiscoveredOptimizationIds();
        Assert.NotEmpty(optimizationIds);

        foreach (var declared in DeclaredSubjects())
            Assert.False(
                optimizationIds.Contains(declared.Subject.Id),
                $"{declared.Name} reuses the record id {declared.Subject.Id} of an optimization."
            );
    }

    /// <summary>The subjects the built-in tools declare.</summary>
    private static IReadOnlyList<DeclaredSubject> DeclaredSubjects() =>
        [
            .. typeof(ToolSubjects)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(field => new DeclaredSubject(
                    $"{nameof(ToolSubjects)}.{field.Name}",
                    (OperationSubject)field.GetValue(null)!
                )),
        ];

    /// <summary>The identities of every optimization the catalog declares.</summary>
    private static List<Guid> DiscoveredOptimizationIds()
    {
        var ids = new List<Guid>();

        foreach (
            var category in ReflectionHelper.FindImplementationsInLoadedAssemblies<IOptimizationCategory>()
        )
        {
            foreach (
                var nested in category
                    .GetNestedTypes(BindingFlags.Public)
                    .Where(t =>
                        typeof(IOptimization).IsAssignableFrom(t) && !t.IsAbstract && t.IsClass
                    )
            )
            {
                ids.Add(((BaseOptimization)Activator.CreateInstance(nested)!).Id);
            }
        }

        return ids;
    }

    /// <summary>Asserts that no two declared subjects share a value in one field.</summary>
    private static void AssertDistinct(
        IReadOnlyList<DeclaredSubject> subjects,
        Func<DeclaredSubject, object> field,
        string fieldName
    )
    {
        var owners = new Dictionary<object, string>();

        foreach (var declared in subjects)
        {
            var value = field(declared);
            Assert.False(
                owners.TryGetValue(value, out var owner),
                $"{declared.Name} and {owner} share one {fieldName}: {value}."
            );
            owners[value] = declared.Name;
        }
    }

    /// <summary>One declared subject with the name of the field it was read from.</summary>
    private sealed record DeclaredSubject(string Name, OperationSubject Subject);
}
