// Copyright (c) Microsoft. All rights reserved.

using SmartFlowUI.Services.Profile;

namespace SmartFlowUI.Services;

public interface IChatService
{
    IAsyncEnumerable<ChatChunkResponse> ReplyAsync(UserInformation user, ProfileDefinition profile, ChatRequest request, CancellationToken cancellationToken = default);
}
