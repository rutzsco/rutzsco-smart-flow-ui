// Copyright (c) Microsoft. All rights reserved.

namespace Shared.Models;

/// <summary>
/// Represents metadata information for a blob container
/// </summary>
public class ContainerInfo
{
    /// <summary>
    /// The name of the blob container
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the container
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional type/category of the container
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Optional Azure AI Search index name associated with this container
    /// </summary>
    public string? IndexName { get; set; }

    public ContainerInfo()
    {
    }

    public ContainerInfo(string name, string? description = null, string? type = null, string? indexName = null)
    {
        Name = name;
        Description = description;
        Type = type;
        IndexName = indexName;
    }
}

/// <summary>
/// Request model for creating a blob container with metadata
/// </summary>
public class CreateContainerRequest
{
    /// <summary>
    /// The name of the blob container to create
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the container
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional type/category of the container
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Optional Azure AI Search index name to associate with this container
    /// </summary>
    public string? IndexName { get; set; }
}

// Backwards compatibility aliases - UI displays "Collection" to users
// but internal code uses "Container" to align with Azure Blob Storage terminology
/// <summary>
/// Alias for ContainerInfo for backwards compatibility. UI displays "Collection" to users.
/// </summary>
public class CollectionInfo : ContainerInfo
{
    public CollectionInfo() : base() { }
    public CollectionInfo(string name, string? description = null, string? type = null, string? indexName = null)
        : base(name, description, type, indexName) { }
}

/// <summary>
/// Alias for CreateContainerRequest for backwards compatibility. UI displays "Collection" to users.
/// </summary>
public class CreateCollectionRequest : CreateContainerRequest { }
