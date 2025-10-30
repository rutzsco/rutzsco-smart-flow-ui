// Copyright (c) Microsoft. All rights reserved.

namespace SmartFlow.UI.Client.Models;

public readonly record struct UserQuestion(
    string Question,
    DateTime AskedOn);
