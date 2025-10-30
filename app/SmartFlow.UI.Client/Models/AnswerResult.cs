// Copyright (c) Microsoft. All rights reserved.

namespace SmartFlow.UI.Client.Models;

public readonly record struct AnswerResult<TRequest>(
    bool IsSuccessful,
    ApproachResponse? Response,
    TRequest Request);
