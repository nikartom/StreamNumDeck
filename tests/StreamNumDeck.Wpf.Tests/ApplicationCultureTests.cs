using System.Globalization;
using StreamNumDeck.Wpf.Localization;

namespace StreamNumDeck.Wpf.Tests;

[TestClass]
public sealed class ApplicationCultureTests
{
    [TestMethod]
    public void ResolvesLanguageArgument()
    {
        var cultureName = ApplicationCulture.ResolveCommandLineOverride(["--language", "en"]);

        Assert.AreEqual("en-US", cultureName);
    }

    [TestMethod]
    public void ResolvesEqualsSyntaxAndSpecificCulture()
    {
        var cultureName = ApplicationCulture.ResolveCommandLineOverride(["--lang=pt-BR"]);

        Assert.AreEqual("pt-BR", cultureName);
    }

    [TestMethod]
    public void IgnoresUnsupportedCulture()
    {
        var cultureName = ApplicationCulture.ResolveCommandLineOverride(["--language", "eo"]);

        Assert.IsNull(cultureName);
    }

    [TestMethod]
    public void FallsBackToEnglishForUnsupportedSystemCulture()
    {
        var cultureName = ApplicationCulture.ResolveCultureName(CultureInfo.GetCultureInfo("eo"));

        Assert.AreEqual("en-US", cultureName);
    }
}
