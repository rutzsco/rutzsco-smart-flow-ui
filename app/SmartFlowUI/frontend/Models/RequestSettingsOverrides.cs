﻿// Copyright (c) Microsoft. All rights reserved.

namespace ClientApp.Models;

public record RequestSettingsOverrides
{
    public RequestOverrides Overrides { get; set; } = new();
}
