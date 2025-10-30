// Copyright (c) Microsoft. All rights reserved.
namespace Shared.Models;

public record class UserInformation(bool IsIdentityEnabled, string UserName,string UserId, string SessionId, IEnumerable<ProfileSummary> Profiles, IEnumerable<string> Groups);
