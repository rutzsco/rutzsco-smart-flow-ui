﻿// Copyright (c) Microsoft. All rights reserved.

namespace ClientApp.Models;

public readonly record struct AnswerResult<TRequest>(
    bool IsSuccessful,
    ApproachResponse? Response,
    TRequest Request);
