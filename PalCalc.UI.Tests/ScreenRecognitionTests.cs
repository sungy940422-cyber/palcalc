using PalCalc.Model;
using PalCalc.UI.ScreenRecognition;

namespace PalCalc.UI.Tests;

[TestClass]
public class ScreenRecognitionTests
{
    private static readonly PalDB Db = PalDB.LoadEmbedded();
    private static readonly KoreanGameTextMatcher Matcher = new(Db);

    [TestMethod]
    public void MatchesExactKoreanPalName()
    {
        var result = Matcher.MatchPal("아누비스");

        Assert.IsNotNull(result.Value);
        Assert.AreEqual("Anubis", result.Value.Name);
        Assert.AreEqual(1d, result.Confidence);
    }

    [TestMethod]
    public void CorrectsSmallOcrErrorInKoreanPalName()
    {
        var result = Matcher.MatchPal("아누비슷");

        Assert.IsNotNull(result.Value);
        Assert.AreEqual("Anubis", result.Value.Name);
        Assert.IsTrue(result.Confidence >= 0.75);
    }

    [TestMethod]
    public void MatchesFourPassivesFromReferenceScreenshot()
    {
        string[] names = ["초절기교", "불면", "악마의 손", "장인 기질"];
        var matches = names.Select(Matcher.MatchPassive).ToList();

        Assert.IsTrue(matches.All(match => match.Value != null));
        Assert.IsTrue(matches.All(match => match.Confidence == 1d));
        CollectionAssert.AreEquivalent(
            new[] { "Remarkable Craftsmanship", "Insomnia", "Demon’s Hand", "Artisan" },
            matches.Select(match => match.Value.Name).ToArray()
        );
    }

    [TestMethod]
    public void BuildsAutomaticallyAddableObservation()
    {
        var factory = new LivePalObservationFactory(Matcher);
        var observation = factory.Create(
            new OcrTextResult { Text = "아누비스", Confidence = 0.98 },
            PalGender.MALE,
            0.99,
            new[]
            {
                new OcrTextResult { Text = "초절기교", Confidence = 0.98 },
                new OcrTextResult { Text = "불면", Confidence = 0.98 },
                new OcrTextResult { Text = "악마의 손", Confidence = 0.98 },
                new OcrTextResult { Text = "장인 기질", Confidence = 0.98 }
            }
        );

        Assert.AreEqual("Anubis", observation.Pal.Name);
        Assert.AreEqual(PalGender.MALE, observation.Gender);
        Assert.AreEqual(4, observation.PassiveSkills.Count);
        Assert.IsTrue(observation.CanBeAutomaticallyAdded);
    }
}
