// Copyright (c) Microsoft. All rights reserved.

namespace Shared.Models;

/// <summary>
/// Represents a file in a container along with its associated processing files
/// </summary>
public class ContainerFileInfo
{
    /// <summary>
    /// The name of the main file in the container
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Collection of processing files associated with the main file from the extract container
    /// </summary>
    public List<string> ProcessingFiles { get; set; } = new();

    public ContainerFileInfo()
    {
    }

    public ContainerFileInfo(string fileName)
    {
        FileName = fileName;
    }

    public ContainerFileInfo(string fileName, List<string> processingFiles)
    {
        FileName = fileName;
        ProcessingFiles = processingFiles;
    }
}
