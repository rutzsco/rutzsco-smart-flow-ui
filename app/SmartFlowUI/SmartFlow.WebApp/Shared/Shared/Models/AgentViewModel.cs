// Copyright (c) Microsoft. All rights reserved.

namespace Shared.Models;

public class AgentViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Model { get; set; } = string.Empty;
    public List<string> Tools { get; set; } = new();
}
