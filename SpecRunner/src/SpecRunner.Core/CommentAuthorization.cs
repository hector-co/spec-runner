using SpecRunner.Core.Configuration;

namespace SpecRunner.Core;

public static class CommentAuthorization
{
    public static bool IsAuthorized(string author, string authorAssociation, SpecRunnerOptions options)
    {
        var isAllowedUser = options.AllowedTriggerUsers.Any(u => string.Equals(u, author, StringComparison.OrdinalIgnoreCase));
        var isAllowedAssociation = options.AllowedAuthorAssociations.Any(a => string.Equals(a, authorAssociation, StringComparison.OrdinalIgnoreCase));

        return isAllowedUser || isAllowedAssociation;
    }
}
