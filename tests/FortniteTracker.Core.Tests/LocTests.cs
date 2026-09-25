using FortniteTracker.Core;

namespace FortniteTracker.Core.Tests;

// Loc.Language is global, so these run one after another.
[Collection(nameof(LocTests))]
public sealed class LocTests : IDisposable
{
    public void Dispose() => Loc.Language = "en";

    [Fact]
    public void English_is_passed_through()
    {
        Loc.Language = "en";
        Assert.Equal("Eliminated by Bob", Loc.T("Eliminated by {0}", "Bob"));
        Assert.Equal("Gold II", Loc.Name("Gold II"));
    }

    [Fact]
    public void French_translates_and_formats_numbers_the_French_way()
    {
        Loc.Language = "fr";
        Assert.Equal("Éliminé par Bob", Loc.T("Eliminated by {0}", "Bob"));
        Assert.Equal("K/D 6,80 · 16,4 % de victoires · 128 parties", Loc.T("K/D {0:0.00} · {1:0.#}% wins · {2} matches", 6.8, 16.4, 128));
        Assert.Equal("Some text nobody translated", Loc.T("Some text nobody translated"));
    }

    [Theory]
    [InlineData("Gold II", "Or II")]
    [InlineData("Unranked", "Non classé")]
    [InlineData("Ranked Zero Build", "Classé Zéro construction")]
    [InlineData("Reload Build Squads", "Reload Construction Section")]
    public void French_rank_and_mode_names(string english, string french)
    {
        Loc.Language = "fr";
        Assert.Equal(french, Loc.Name(english));
    }
}
