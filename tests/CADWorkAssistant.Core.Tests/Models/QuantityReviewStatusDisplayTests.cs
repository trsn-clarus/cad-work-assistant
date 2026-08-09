using CADWorkAssistant.Core.Models;

namespace CADWorkAssistant.Core.Tests.Models;

public class QuantityReviewStatusDisplayTests
{
    [Theory]
    [InlineData(QuantityReviewStatus.Verified, "검토 완료")]
    [InlineData(QuantityReviewStatus.NeedsReview, "확인 필요")]
    [InlineData(QuantityReviewStatus.Unreviewed, "미검토")]
    public void Label_MatchesStatus(QuantityReviewStatus status, string expectedLabel)
    {
        Assert.Equal(expectedLabel, QuantityReviewStatusDisplay.Label(status));
    }
}
