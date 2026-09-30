using System.Net;
using System.Text;
using GuildArena.Web.Services;

namespace GuildArena.Web.UnitTests.Services;

public class RecruitmentFeedbackTests
{
    [Fact]
    public async Task RejectedPurchaseDisplaysServerReason()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"detail\":\"Purchasing this hero costs 100 gold, but your guild only has 40.\"}", Encoding.UTF8, "application/problem+json")
        };

        var (success, message) = await RecruitmentFeedback.ReadResultAsync(response);

        Assert.False(success);
        Assert.Contains("100 gold", message);
    }

    [Fact]
    public async Task UnconfirmedSuccessDoesNotReportRecruitment()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"success\":false,\"message\":\"Hero not recruited\"}", Encoding.UTF8, "application/json")
        };

        var (success, message) = await RecruitmentFeedback.ReadResultAsync(response);

        Assert.False(success);
        Assert.Equal("Hero not recruited", message);
    }

    [Fact]
    public async Task ServerFailureDoesNotExposeServerResponse()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("internal error")
        };

        var (success, message) = await RecruitmentFeedback.ReadResultAsync(response);

        Assert.False(success);
        Assert.Contains("Please try again", message);
        Assert.DoesNotContain("internal error", message);
    }
}
