using optimizerDuck.Common.Helpers;

namespace optimizerDuck.Test.Common.Helpers;

public class GitHubSourceHelperTests
{
    [Theory]
    [InlineData("2.26.2", "v2.26.2")]
    [InlineData("2.26.2.0", "v2.26.2")]
    [InlineData("10.20.30", "v10.20.30")]
    [InlineData("1.0", "v1.0")]
    public void GetTagForVersion_ValidVersion_ReturnsExpectedTag(
        string fileVersion,
        string expected
    )
    {
        Assert.Equal(expected, GitHubSourceHelper.GetTagForVersion(fileVersion));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-version")]
    [InlineData("v2.26.2")]
    public void GetTagForVersion_InvalidVersion_ReturnsNull(string? fileVersion)
    {
        Assert.Null(GitHubSourceHelper.GetTagForVersion(fileVersion));
    }

    [Fact]
    public void FindClassLineNumber_ClassDeclaration_ReturnsZeroBasedLineIndex()
    {
        const string source = "using System;\n\nnamespace Demo;\n\npublic class Foo : Base\n{\n}\n";

        Assert.Equal(4, GitHubSourceHelper.FindClassLineNumber(source, "Foo"));
    }

    [Fact]
    public void FindClassLineNumber_NestedIndentedClass_IsFound()
    {
        const string source =
            "namespace Demo;\n\npublic class Container\n{\n"
            + "    internal class Foo : BaseOptimization\n    {\n    }\n}\n";

        Assert.Equal(4, GitHubSourceHelper.FindClassLineNumber(source, "Foo", "BaseOptimization"));
    }

    [Fact]
    public void FindClassLineNumber_SubstringClassName_DoesNotMatchPartialNames()
    {
        const string source = "class MyFoo : Base\n{\n}\n\nclass FooProcessor : Base\n{\n}\n";

        Assert.Equal(-1, GitHubSourceHelper.FindClassLineNumber(source, "Foo", "Base"));
    }

    [Fact]
    public void FindClassLineNumber_BaseClassPattern_MatchesOnlyDerivedClass()
    {
        const string source =
            "public class Foo : RegistryToggle\n{\n}\n\npublic class Bar : Other\n{\n}\n";

        Assert.Equal(0, GitHubSourceHelper.FindClassLineNumber(source, "Foo", "RegistryToggle"));
        Assert.Equal(-1, GitHubSourceHelper.FindClassLineNumber(source, "Bar", "RegistryToggle"));
    }

    [Fact]
    public void FindClassLineNumber_DeclarationIsCaseInsensitive()
    {
        const string source = "CLASS foo : basesetting\n{\n}\n";

        Assert.Equal(0, GitHubSourceHelper.FindClassLineNumber(source, "Foo", "BaseSetting"));
    }

    [Fact]
    public void FindClassLineNumber_NoMatch_ReturnsMinusOne()
    {
        const string source = "public class SomethingElse\n{\n}\n";

        Assert.Equal(-1, GitHubSourceHelper.FindClassLineNumber(source, "Foo"));
    }
}
