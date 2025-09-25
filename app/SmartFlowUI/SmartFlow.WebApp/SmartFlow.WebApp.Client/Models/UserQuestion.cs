// Copyright (c) Microsoft. All rights reserved.

namespace SmartFlow.WebApp.Client.Models;

public readonly record struct UserQuestion(
    string Question,
    DateTime AskedOn);
