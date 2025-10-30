// Copyright (c) Microsoft. All rights reserved.

using System.Linq;
using System.Threading.Tasks;
using MinimalApi.Services.Profile;
using MinimalApi.Services.Security;

namespace MinimalApi.Extensions;

internal static class UserExtensions
{
    public static async Task<UserInformation> GetUserInfoAsync(this HttpContext context, ProfileInfo? profileInfo = null)
    {
        var id = context.Request.Headers["X-MS-CLIENT-PRINCIPAL-ID"];
        var name = context.Request.Headers["X-MS-CLIENT-PRINCIPAL-NAME"];
        var claimsPrincipal = ClaimsPrincipalParser.Parse(context.Request);
        var userGroups = claimsPrincipal.Claims.Where(c => c.Type == "groups").Select(c => c.Value).ToList();
        var session = claimsPrincipal.Claims.Where(c => c.Type == "nonce").Select(c => c.Value).FirstOrDefault();

        if (string.IsNullOrEmpty(id))
        {
            id = "LocalDevUser";
            name = "LocalDevUser";
            userGroups = new List<string> { "LocalDevUser" };
            session = "test-session";
        }

        var enableLogout = !string.IsNullOrEmpty(id);

        var user = new UserInformation(enableLogout, name, id, session, [], userGroups);
        try
        {
            if (profileInfo == null)
            {
                var profileService = context.RequestServices.GetRequiredService<ProfileService>();
                profileInfo = await profileService.GetProfileDataAsync();
            }
            var profiles = profileInfo?.Profiles?.Count > 0 ? profileInfo.Profiles.GetAuthorizedProfiles(userGroups).Select(x => new ProfileSummary(x.Id, x.Name, string.Empty, (ProfileApproach)Enum.Parse(typeof(ProfileApproach), x.Approach, true), x.SampleQuestions, x.UserPromptTemplates, SupportsUserSelections(x), x.AllowFileUpload)) : [];
            user = new UserInformation(enableLogout, name, id, session, profiles, userGroups);
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error loading embedded profile data: {ex.Message}";
            Debug.WriteLine(errorMsg);
        }

        return user;
    }

    public static bool SupportsUserSelections(ProfileDefinition p)
    {
        return p.RAGSettings != null && p.RAGSettings.ProfileUserSelectionOptions != null && p.RAGSettings.ProfileUserSelectionOptions.Any();
    }
}
