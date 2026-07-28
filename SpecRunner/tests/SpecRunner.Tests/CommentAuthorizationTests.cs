using SpecRunner.Core;
using SpecRunner.Core.Configuration;

namespace SpecRunner.Tests;

public class CommentAuthorizationTests
{
    [Fact]
    public void AllowedAssociationAuthorizes()
    {
        var options = new SpecRunnerOptions();

        var result = CommentAuthorization.IsAuthorized("someone", "OWNER", options);

        Assert.True(result);
    }

    [Fact]
    public void DisallowedAssociationWithoutAllowlistMatchDoesNotAuthorize()
    {
        var options = new SpecRunnerOptions();

        var result = CommentAuthorization.IsAuthorized("rando", "NONE", options);

        Assert.False(result);
    }

    [Fact]
    public void ExplicitTriggerUserAuthorizesDespiteDisallowedAssociation()
    {
        var options = new SpecRunnerOptions { AllowedTriggerUsers = new() { "trusted-external" } };

        var result = CommentAuthorization.IsAuthorized("trusted-external", "NONE", options);

        Assert.True(result);
    }

    [Fact]
    public void AuthorComparisonIsCaseInsensitive()
    {
        var options = new SpecRunnerOptions { AllowedTriggerUsers = new() { "trusted-external" } };

        var result = CommentAuthorization.IsAuthorized("Trusted-External", "NONE", options);

        Assert.True(result);
    }

    [Fact]
    public void AssociationComparisonIsCaseInsensitive()
    {
        var options = new SpecRunnerOptions();

        var result = CommentAuthorization.IsAuthorized("someone", "owner", options);

        Assert.True(result);
    }
}
