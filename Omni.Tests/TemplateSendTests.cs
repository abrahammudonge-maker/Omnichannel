using System.Net;
using Omni.Application.Services;
using Omni.Domain.Entities;
using Omni.Infrastructure.Database;
using Omni.Infrastructure.Providers;

namespace Omni.Tests;

public class TemplateSendTests
{
    [Theory]
    [InlineData(HttpStatusCode.BadRequest, """{"error":{"code":130429,"message":"Rate limit hit"}}""", true)]
    [InlineData(HttpStatusCode.BadRequest, """{"error":{"code":131056,"message":"Pair rate limit"}}""", true)]
    [InlineData(HttpStatusCode.InternalServerError, "not json", true)]
    [InlineData(HttpStatusCode.TooManyRequests, "", true)]
    [InlineData(HttpStatusCode.BadRequest, """{"error":{"code":131026,"message":"Message undeliverable"}}""", false)]
    [InlineData(HttpStatusCode.BadRequest, """{"error":{"code":132001,"message":"Template name does not exist"}}""", false)]
    [InlineData(HttpStatusCode.Unauthorized, """{"error":{"code":190,"message":"Invalid OAuth access token"}}""", false)]
    public void MetaApiException_classifies_transient_failures(HttpStatusCode status, string body, bool transient)
    {
        var ex = new MetaApiException("failed", status, body);
        Assert.Equal(transient, MetaApiException.IsTransientFailure(ex));
    }

    [Fact]
    public void MetaApiException_prefers_error_details_over_message()
    {
        var ex = new MetaApiException("failed", HttpStatusCode.BadRequest,
            """{"error":{"code":131008,"message":"Required parameter is missing","error_data":{"details":"body: number of parameters does not match"}}}""");
        Assert.Equal(131008, ex.ErrorCode);
        Assert.Equal("body: number of parameters does not match", ex.Details);
    }

    [Fact]
    public void Network_errors_and_timeouts_are_transient_but_bugs_are_not()
    {
        Assert.True(MetaApiException.IsTransientFailure(new HttpRequestException("connection reset")));
        Assert.True(MetaApiException.IsTransientFailure(new TaskCanceledException()));
        Assert.False(MetaApiException.IsTransientFailure(new InvalidOperationException("bad template")));
    }

    [Fact]
    public void Parameter_rules_match_what_Meta_rejects()
    {
        Assert.Null(WhatsAppTemplateRules.ValidateParameters(new[] { "Jane", "KES 4,500" }, 2));
        Assert.Contains("needs 2", WhatsAppTemplateRules.ValidateParameters(new[] { "Jane" }, 2));
        Assert.Contains("empty", WhatsAppTemplateRules.ValidateParameters(new[] { " " }, 1));
        Assert.Contains("new lines", WhatsAppTemplateRules.ValidateParameters(new[] { "a\nb" }, 1));
        Assert.Contains("four spaces", WhatsAppTemplateRules.ValidateParameters(new[] { "a     b" }, 1));
        Assert.Null(WhatsAppTemplateRules.ValidateParameters(new[] { "a    b" }, 1));
        Assert.Null(WhatsAppTemplateRules.ValidateParameters(Array.Empty<string>(), 0));
    }

    [Fact]
    public void Authentication_and_unapproved_templates_are_not_sendable()
    {
        Assert.True(WhatsAppTemplateRules.IsSendable(new MessageTemplate { Status = "APPROVED", Category = "UTILITY" }));
        Assert.False(WhatsAppTemplateRules.IsSendable(new MessageTemplate { Status = "APPROVED", Category = "AUTHENTICATION" }));
        Assert.False(WhatsAppTemplateRules.IsSendable(new MessageTemplate { Status = "PENDING", Category = "MARKETING" }));
        Assert.Equal(3, WhatsAppTemplateRules.CountBodyParameters("Hi {{1}}, {{2}} due {{3}}."));
    }

    [Fact]
    public void Migration_scripts_split_on_GO_lines_only()
    {
        var batches = DatabaseMigrator.SplitBatches("SET X ON;\r\nGO\r\nCREATE TABLE GOODS (id INT);\ngo\n\nSELECT 1;\nGO\n");
        Assert.Equal(new[] { "SET X ON;", "CREATE TABLE GOODS (id INT);", "SELECT 1;" }, batches);
    }

    [Fact]
    public void Every_listed_migration_script_is_embedded()
    {
        var resources = typeof(DatabaseMigrator).Assembly.GetManifestResourceNames();
        foreach (var script in DatabaseMigrator.Scripts)
        {
            Assert.Contains(resources, r => r.EndsWith("." + script, StringComparison.OrdinalIgnoreCase));
        }
    }
}
