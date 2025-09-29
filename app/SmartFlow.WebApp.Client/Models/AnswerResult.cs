// Copyright (c) Microsoft. All rights reserved.

namespace SmartFlow.WebApp.Client.Models;

public readonly record struct AnswerResult<TRequest>(
    bool IsSuccessful,
    ApproachResponse? Response,
    TRequest Request);
