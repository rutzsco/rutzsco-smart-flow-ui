// Copyright (c) Microsoft. All rights reserved.

using Shared.Models;

namespace SmartFlow.UI.Client.Pages;

public sealed partial class Collections : IDisposable
{
    private const long MaxIndividualFileSize = 1_024 * 1_024 * 500; // 500MB

    private MudForm _form = null!;
    private MudForm _createCollectionForm = null!;
    private bool _createCollectionFormValid = false;

    private bool _isLoadingDocuments = false;
    private bool _isUploadingDocuments = false;
    private bool _isIndexingDocuments = false;
    private bool _isLoadingCollections = false;
    private bool _showUploadSection = false; // Hidden by default - user clicks "Upload Document" to show
    private string _filter = "";
    private HashSet<string> _processingFiles = new(); // Track files being processed
    private HashSet<string> _deletingFiles = new(); // Track files being deleted

    // Store a cancelation token that will be used to cancel if the user disposes of this component.
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    [Inject] public required ApiClient Client { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required ILogger<Collections> Logger { get; set; }
    [Inject] public required IJSRuntime JSRuntime { get; set; }
    [Inject] public required HttpClient HttpClient { get; set; }
    [Inject] public required IDialogService DialogService { get; set; }

    // Collection management
    private List<CollectionInfo> _collections = new();
    private List<SearchIndexInfo> _availableIndexes = new();
    private string _selectedCollection = "";
    private CollectionInfo? _selectedCollectionInfo = null;
    private List<ContainerFileInfo> _collectionFiles = new();
    private bool _showCreateCollectionForm = false;
    private string _newCollectionName = "";
    private string _newCollectionDescription = "";
    private string _newCollectionType = "";
    private string _newCollectionIndexName = "";

    // Edit collection metadata
    private bool _showEditMetadataForm = false;
    private string _editCollectionDescription = "";
    private string _editCollectionType = "";
    private string _editCollectionIndexName = "";

    protected override async Task OnInitializedAsync()
    {
        // Load collections and available indexes
        await Task.WhenAll(
            LoadCollectionsAsync(),
            LoadAvailableIndexesAsync()
        );
    }

    private async Task LoadAvailableIndexesAsync()
    {
        try
        {
            _availableIndexes = await Client.GetSearchIndexesAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading available indexes");
            // Don't show error to user - index field will just be empty
        }
    }

    private async Task LoadCollectionsAsync()
    {
        _isLoadingCollections = true;
        try
        {
            _collections = await Client.GetCollectionsAsync();
            // Auto-select first collection if available
            if (_collections.Any())
            {
                await SelectCollectionAsync(_collections.First().Name);
            }
            else
            {
                _selectedCollection = "";
                _selectedCollectionInfo = null;
                _collectionFiles.Clear();
                _fileUploads.Clear();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading collections");
            SnackBarError("Failed to load collections");
        }
        finally
        {
            _isLoadingCollections = false;
            StateHasChanged();
        }
    }

    private async Task SelectCollectionAsync(string collectionName)
    {
        if (_selectedCollection != collectionName)
        {
            _selectedCollection = collectionName;
            _selectedCollectionInfo = _collections.FirstOrDefault(c => c.Name == collectionName);
            _fileUploads.Clear(); // Clear any selected files when switching collections
            _filter = ""; // Clear filter when switching collections
            _showCreateCollectionForm = false; // Hide create form when selecting a collection
            _showUploadSection = false; // Hide upload section when switching collections
            _showEditMetadataForm = false; // Hide edit metadata form when switching collections
            await Task.WhenAll(
                LoadCollectionFilesAsync(),
                LoadFolderStructureAsync()
            );
        }
    }

    private async Task LoadCollectionFilesAsync()
    {
        if (string.IsNullOrEmpty(_selectedCollection))
            return;

        _isLoadingDocuments = true;
        try
        {
            _collectionFiles = await Client.GetCollectionFilesAsync(_selectedCollection);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading collection files for {Collection}", _selectedCollection);
            SnackBarError($"Failed to load files from collection '{_selectedCollection}'");
        }
        finally
        {
            _isLoadingDocuments = false;
            StateHasChanged();
        }
    }

    private IEnumerable<ContainerFileInfo> GetFilteredFiles()
    {
        if (_collectionFiles == null || !_collectionFiles.Any())
            return Enumerable.Empty<ContainerFileInfo>();

        // If no folder is selected or we're at the collection root, show all files at root level (no folder path)
        if (string.IsNullOrEmpty(_currentFolderPath) || _currentFolderPath == _selectedCollection)
        {
            return _collectionFiles.Where(f => string.IsNullOrEmpty(f.FolderPath));
        }

        // Show only files in the selected folder (not in subfolders)
        return _collectionFiles.Where(f => f.FolderPath == _currentFolderPath);
    }

    private void ShowCreateCollectionForm()
    {
        _newCollectionName = "";
        _newCollectionDescription = "";
        _newCollectionType = "";
        _newCollectionIndexName = "";
        _createCollectionFormValid = false;
        _showCreateCollectionForm = true;
        _showEditMetadataForm = false;
    }

    private void CancelCreateCollection()
    {
        _showCreateCollectionForm = false;
        _newCollectionName = "";
        _newCollectionDescription = "";
        _newCollectionType = "";
        _newCollectionIndexName = "";
        _createCollectionFormValid = false;
    }

    private string ValidateCollectionName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Collection name is required";

        // Azure Storage container naming rules
        if (name.Length < 3 || name.Length > 63)
            return "Collection name must be between 3 and 63 characters";

        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-z0-9]([a-z0-9-]*[a-z0-9])?$"))
            return "Collection name must contain only lowercase letters, numbers, and hyphens, and must start and end with a letter or number";

        if (name.Contains("--"))
            return "Collection name cannot contain consecutive hyphens";

        if (_collections.Any(c => c.Name == name))
            return "A collection with this name already exists";

        return null!;
    }

    private async Task CreateCollectionAsync()
    {
        if (string.IsNullOrWhiteSpace(_newCollectionName) || !_createCollectionFormValid)
        {
            SnackBarError("Please enter a valid collection name");
            return;
        }

        try
        {
            var success = await Client.CreateCollectionAsync(
                _newCollectionName,
                string.IsNullOrWhiteSpace(_newCollectionDescription) ? null : _newCollectionDescription,
                string.IsNullOrWhiteSpace(_newCollectionType) ? null : _newCollectionType,
                string.IsNullOrWhiteSpace(_newCollectionIndexName) ? null : _newCollectionIndexName);

            if (success)
            {
                SnackBarMessage($"Collection '{_newCollectionName}' created successfully");
                _showCreateCollectionForm = false;
                var createdCollectionName = _newCollectionName;
                _newCollectionName = "";
                _newCollectionDescription = "";
                _newCollectionType = "";
                _newCollectionIndexName = "";
                await LoadCollectionsAsync();
                // Auto-select the newly created collection
                await SelectCollectionAsync(createdCollectionName);
            }
            else
            {
                SnackBarError($"Failed to create collection '{_newCollectionName}'");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error creating collection {CollectionName}", _newCollectionName);
            SnackBarError($"Error creating collection: {ex.Message}");
        }
    }

    private void ShowEditMetadataForm()
    {
        if (_selectedCollectionInfo != null)
        {
            _editCollectionDescription = _selectedCollectionInfo.Description ?? "";
            _editCollectionType = _selectedCollectionInfo.Type ?? "";
            _editCollectionIndexName = _selectedCollectionInfo.IndexName ?? "";
            _showEditMetadataForm = true;
            _showCreateCollectionForm = false;
            _showUploadSection = false;
        }
    }

    private void CancelEditMetadata()
    {
        _showEditMetadataForm = false;
        _editCollectionDescription = "";
        _editCollectionType = "";
        _editCollectionIndexName = "";
    }

    private async Task ShowDeleteCollectionDialogAsync()
    {
        if (string.IsNullOrEmpty(_selectedCollection))
        {
            SnackBarError("No collection selected");
            return;
        }

        // Show confirmation dialog
        var parameters = new DialogParameters
        {
            { "ContentText", $"Are you sure you want to remove collection '{_selectedCollection}'? This will only remove the collection metadata tag. The container and all its files will remain intact and can be re-added as a collection later." },
            { "ButtonText", "Remove Collection" },
            { "Color", Color.Error }
        };

        var dialog = await DialogService.ShowAsync<ConfirmationDialog>("Confirm Collection Removal", parameters);
        var result = await dialog.Result;

        if (result!.Canceled)
            return;

        await DeleteCollectionAsync();
    }

    private async Task DeleteCollectionAsync()
    {
        if (string.IsNullOrEmpty(_selectedCollection))
        {
            SnackBarError("No collection selected");
            return;
        }

        try
        {
            var collectionToDelete = _selectedCollection!;
            var success = await Client.DeleteCollectionAsync(collectionToDelete);

            if (success)
            {
                SnackBarMessage($"Collection '{collectionToDelete}' removed successfully. The container and its files remain intact.");
                _selectedCollection = "";
                _selectedCollectionInfo = null;
                _collectionFiles.Clear();
                _fileUploads.Clear();
                await LoadCollectionsAsync();
            }
            else
            {
                SnackBarError($"Failed to remove collection '{collectionToDelete}'");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error removing collection {CollectionName}", _selectedCollection);
            SnackBarError($"Error removing collection: {ex.Message}");
        }
    }

    private async Task UpdateCollectionMetadataAsync()
    {
        if (string.IsNullOrEmpty(_selectedCollection))
        {
            SnackBarError("No collection selected");
            return;
        }

        try
        {
            var success = await Client.UpdateCollectionMetadataAsync(
                _selectedCollection,
                string.IsNullOrWhiteSpace(_editCollectionDescription) ? null : _editCollectionDescription,
                string.IsNullOrWhiteSpace(_editCollectionType) ? null : _editCollectionType,
                string.IsNullOrWhiteSpace(_editCollectionIndexName) ? null : _editCollectionIndexName);

            if (success)
            {
                SnackBarMessage($"Collection '{_selectedCollection}' metadata updated successfully");
                _showEditMetadataForm = false;

                // Refresh the collection info
                _selectedCollectionInfo = await Client.GetCollectionMetadataAsync(_selectedCollection);

                // Update the collection in the list
                var collectionInList = _collections.FirstOrDefault(c => c.Name == _selectedCollection);
                if (collectionInList != null && _selectedCollectionInfo != null)
                {
                    collectionInList.Description = _selectedCollectionInfo.Description;
                    collectionInList.Type = _selectedCollectionInfo.Type;
                    collectionInList.IndexName = _selectedCollectionInfo.IndexName;
                }

                StateHasChanged();
            }
            else
            {
                SnackBarError($"Failed to update metadata for collection '{_selectedCollection}'");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating collection metadata for {CollectionName}", _selectedCollection);
            SnackBarError($"Error updating collection metadata: {ex.Message}");
        }
    }

    private bool OnFileFilter(ContainerFileInfo fileInfo) =>
        string.IsNullOrWhiteSpace(_filter) || fileInfo.FileName.Contains(_filter, StringComparison.OrdinalIgnoreCase);

    private async Task RefreshAsync()
    {
        await LoadCollectionFilesAsync();
    }

    private async Task SubmitFilesForUploadAsync()
    {
        if (!_fileUploads.Any())
        {
            SnackBarError("Please select files to upload");
            return;
        }

        if (string.IsNullOrEmpty(_selectedCollection))
        {
            SnackBarError("Please select a collection to upload to");
            return;
        }

        _isUploadingDocuments = true;

        try
        {
            var metadata = new Dictionary<string, string>();

            // Try multiple ways to detect the current folder
            string? targetFolder = null;

            Logger.LogInformation("[CLIENT] _uploadFolderPath = '{UploadFolderPath}'", _uploadFolderPath ?? "NULL");
            Logger.LogInformation("[CLIENT] _currentFolderPath = '{CurrentFolderPath}'", _currentFolderPath ?? "NULL");
            Logger.LogInformation("[CLIENT] _selectedCollection = '{SelectedCollection}'", _selectedCollection ?? "NULL");

            // 1. Check if user manually entered a folder path in the upload form
            if (!string.IsNullOrEmpty(_uploadFolderPath))
            {
                targetFolder = _uploadFolderPath;
                Logger.LogInformation("[CLIENT] Using manually entered folder path: {FolderPath}", targetFolder);
            }
            // 2. Check if there's a folder selected in the tree
            else if (!string.IsNullOrEmpty(_currentFolderPath))
            {
                // If the current folder path equals the selected collection name,
                // we're at the root of the collection (container), so don't add a folder prefix
                if (_currentFolderPath == _selectedCollection)
                {
                    Logger.LogInformation("[CLIENT] Current folder is collection root, uploading to container root (no subfolder)");
                    targetFolder = null; // Upload to container root
                }
                else
                {
                    targetFolder = _currentFolderPath;
                    Logger.LogInformation("[CLIENT] Using selected folder from tree: {FolderPath}", targetFolder);
                }
            }
            // Note: Do not auto-detect folders from existing files since collections are blob containers

            if (!string.IsNullOrEmpty(targetFolder))
            {
                var normalizedFolder = targetFolder.Replace("\\", "/").Trim('/');
                if (!string.IsNullOrEmpty(normalizedFolder))
                {
                    metadata["folderPath"] = normalizedFolder;
                    Logger.LogInformation("[CLIENT] Uploading to folder: {FolderPath}", normalizedFolder);
                }
                else
                {
                    Logger.LogInformation("[CLIENT] Folder resolved to root after normalization");
                }
            }
            else
            {
                Logger.LogInformation("[CLIENT] No folder detected, uploading to root");
            }
            var result = await Client.UploadFilesToCollectionAsync(
                _fileUploads.ToArray(),
                MaxIndividualFileSize,
                _selectedCollection!,
                metadata);

            Logger.LogInformation("Upload result: {Result}", result);

            if (result.IsSuccessful)
            {
                var uploadLocation = !string.IsNullOrEmpty(targetFolder) ? $"'{_selectedCollection}/{targetFolder}'" : $"'{_selectedCollection}'";
                SnackBarMessage($"Uploaded {result.UploadedFiles.Length} document(s) to {uploadLocation}");
                _fileUploads.Clear();
                _uploadFolderPath = ""; // Clear the folder path input

                // Delay closing to prevent JSInterop errors with file reading
                await Task.Delay(100);
                _showUploadSection = false; // Hide upload section after successful upload
            }
            else
            {
                SnackBarError($"Failed to upload documents. {result.Error}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error uploading files to collection {Collection}", _selectedCollection);
            SnackBarError($"Error uploading files: {ex.Message}");
        }
        finally
        {
            _isUploadingDocuments = false;
            await RefreshAsync();
        }
    }

    private void SnackBarMessage(string? message) { SnackBarAdd(false, message); }
    private void SnackBarError(string? message) { SnackBarAdd(true, message); }
    private void SnackBarAdd(bool isError, string? message)
    {
        Snackbar.Add(
            message ?? "Error occurred!",
            isError ? Severity.Error : Severity.Success,
            static options =>
            {
                options.ShowCloseIcon = true;
                options.VisibleStateDuration = 10_000;
            });
    }

    private IList<IBrowserFile> _fileUploads = new List<IBrowserFile>();

    private void UploadFiles(IReadOnlyList<IBrowserFile> files)
    {
        foreach (var file in files)
        {
            _fileUploads.Add(file);
        }
    }

    private void RemoveFile(IBrowserFile file)
    {
        _fileUploads.Remove(file);
        StateHasChanged();
    }

    private async Task ProcessDocumentLayoutAsync(string fileName)
    {
        if (string.IsNullOrEmpty(_selectedCollection) || string.IsNullOrEmpty(fileName))
            return;

        // Add to processing set
        _processingFiles.Add(fileName);
        StateHasChanged();

        try
        {
            Logger.LogInformation("Processing document layout for {FileName} in {Collection}", fileName, _selectedCollection);

            var success = await Client.ProcessDocumentLayoutAsync(_selectedCollection, fileName);

            if (success)
            {
                SnackBarMessage($"Document '{fileName}' processing started successfully");
            }
            else
            {
                SnackBarError($"Failed to start processing for '{fileName}'");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing document layout for {FileName}", fileName);
            SnackBarError($"Error processing document: {ex.Message}");
        }
        finally
        {
            // Remove from processing set
            _processingFiles.Remove(fileName);
            StateHasChanged();
        }
    }

    private async Task ViewFileAsync(string fileName, bool isProcessingFile = false)
    {
        if (string.IsNullOrEmpty(_selectedCollection) || string.IsNullOrEmpty(fileName))
            return;

        try
        {
            var containerName = isProcessingFile ? $"{_selectedCollection}-extract" : _selectedCollection;
            var fileUrl = await Client.GetFileUrlAsync(containerName, fileName);

            if (!string.IsNullOrEmpty(fileUrl))
            {
                var extension = Path.GetExtension(fileName).ToLowerInvariant();

                if (extension == ".pdf")
                {
                    var parameters = new DialogParameters<CollectionPdfViewerDialog>
                    {
                        { x => x.FileName, Path.GetFileName(fileName) },
                        { x => x.FileUrl, fileUrl }
                    };
                    var options = new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true, CloseButton = true, CloseOnEscapeKey = true };
                    await DialogService.ShowAsync<CollectionPdfViewerDialog>(Path.GetFileName(fileName), parameters, options);
                }
                else if (extension == ".md")
                {
                    var parameters = new DialogParameters<MarkdownViewerDialog>
                    {
                        { x => x.FileName, Path.GetFileName(fileName) },
                        { x => x.FileUrl, fileUrl }
                    };
                    var options = new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true, CloseButton = true, CloseOnEscapeKey = true };
                    await DialogService.ShowAsync<MarkdownViewerDialog>(Path.GetFileName(fileName), parameters, options);
                }
                else if (extension == ".json")
                {
                    var parameters = new DialogParameters<JsonViewerDialog>
                    {
                        { x => x.FileName, Path.GetFileName(fileName) },
                        { x => x.FileUrl, fileUrl }
                    };
                    var options = new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true, CloseButton = true, CloseOnEscapeKey = true };
                    await DialogService.ShowAsync<JsonViewerDialog>(Path.GetFileName(fileName), parameters, options);
                }
                else
                {
                    SnackBarError($"File type '{extension}' is not supported for viewing");
                }
            }
            else
            {
                SnackBarError("Failed to generate file URL");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error viewing file {FileName}", fileName);
            SnackBarError($"Error viewing file: {ex.Message}");
        }
    }

    private bool CanViewFile(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension is ".pdf" or ".md" or ".json";
    }

    private async Task DeleteFileAsync(string fileName)
    {
        if (string.IsNullOrEmpty(_selectedCollection) || string.IsNullOrEmpty(fileName))
            return;

        // Show confirmation dialog
        var parameters = new DialogParameters
        {
            { "ContentText", $"Are you sure you want to delete '{fileName}'? This action cannot be undone. Associated processing files will also be deleted." },
            { "ButtonText", "Delete" },
            { "Color", Color.Error }
        };

        var dialog = await DialogService.ShowAsync<ConfirmationDialog>("Confirm Deletion", parameters);
        var result = await dialog.Result;

        if (result?.Canceled ?? true)
            return;

        // Add to deleting set
        _deletingFiles.Add(fileName);
        StateHasChanged();

        try
        {
            Logger.LogInformation("Deleting file {FileName} from {Collection}", fileName, _selectedCollection);

            var success = await Client.DeleteFileFromCollectionAsync(_selectedCollection, fileName);

            if (success)
            {
                SnackBarMessage($"File '{fileName}' deleted successfully");
                await RefreshAsync();
            }
            else
            {
                SnackBarError($"Failed to delete file '{fileName}'");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting file {FileName}", fileName);
            SnackBarError($"Error deleting file: {ex.Message}");
        }
        finally
        {
            // Remove from deleting set
            _deletingFiles.Remove(fileName);
            StateHasChanged();
        }
    }

    // Folder management
    private FolderNode _folderStructure = new();
    private FolderNode? _selectedFolder = null;
    private string _currentFolderPath = "";
    private string _uploadFolderPath = "";

    private string GetUploadTargetName()
    {
        if (!string.IsNullOrEmpty(_currentFolderPath) && _currentFolderPath != _selectedCollection)
        {
            return $"{_selectedCollection}/{_currentFolderPath}";
        }
        return _selectedCollection;
    }
    private bool _showFolderView = true;

    private async Task LoadFolderStructureAsync()
    {
        try
        {
            // Create root node with all collections as children (containers are the root folders)
            var rootNode = new FolderNode("root", "");

            foreach (var collection in _collections)
            {
                // Get the folder structure from the API (includes empty folders)
                var collectionNode = await Client.GetFolderStructureAsync(collection.Name);

                // Set collection properties
                collectionNode.Name = collection.Name;
                collectionNode.Path = collection.Name;
                collectionNode.IsExpanded = true;  // Expand collections by default to show all folders

                // Prepend collection name to all child folder paths so they are correctly identified as subfolders
                PrependCollectionNameToFolderPaths(collectionNode, collection.Name);

                Logger.LogInformation($"Collection: {collection.Name}, Children: {collectionNode.Children.Count}");

                // Recursively expand all folders in the tree
                ExpandAllFolders(collectionNode);

                LogTreeStructure(collectionNode, 1);

                rootNode.Children.Add(collectionNode);
            }

            _folderStructure = rootNode;
            Logger.LogInformation($"=== COMPLETE TREE STRUCTURE ===");
            Logger.LogInformation($"Root has {rootNode.Children.Count} collections");
            StateHasChanged();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading folder structure");
            SnackBarError("Failed to load folder structure");
            _folderStructure = new FolderNode("root", "");
        }
    }

    private void ExpandAllFolders(FolderNode node)
    {
        node.IsExpanded = true;
        foreach (var child in node.Children)
        {
            ExpandAllFolders(child);
        }
    }

    private void PrependCollectionNameToFolderPaths(FolderNode node, string collectionName)
    {
        foreach (var child in node.Children)
        {
            // Prepend collection name to child paths if not already present
            if (!child.Path.StartsWith(collectionName + "/"))
            {
                child.Path = $"{collectionName}/{child.Path}";
            }
            // Recursively update nested folders
            PrependCollectionNameToFolderPaths(child, collectionName);
        }
    }

    private void LogTreeStructure(FolderNode node, int level)
    {
        var indent = new string(' ', level * 2);
        Logger.LogInformation($"{indent}- {node.Name} (Children: {node.Children.Count}, IsExpanded: {node.IsExpanded}, Files: {node.FileCount})");
        foreach (var child in node.Children)
        {
            LogTreeStructure(child, level + 1);
        }
    }

    private async Task OnFolderSelectedAsync(FolderNode folder)
    {
        _selectedFolder = folder;

        // Check if this is a root-level folder (collection/container)
        // Root folders have paths that don't contain slashes
        if (!folder.Path.Contains('/') && !string.IsNullOrEmpty(folder.Path))
        {
            // This is a collection - select it
            _currentFolderPath = folder.Path;
            await SelectCollectionAsync(folder.Path);
        }
        else
        {
            // This is a subfolder within a collection
            // Extract the collection name (first part of path before /)
            var collectionName = folder.Path.Split('/')[0];

            // Remove the collection name prefix to get the actual folder path
            // E.g., "collection1/folder1/subfolder" -> "folder1/subfolder"
            var folderPath = folder.Path.Substring(collectionName.Length).TrimStart('/');
            _currentFolderPath = folderPath;

            Logger.LogInformation($"Selected folder: {folder.Path}, Extracted folder path: {folderPath}");

            if (_selectedCollection != collectionName)
            {
                _selectedCollection = collectionName;
                _selectedCollectionInfo = _collections.FirstOrDefault(c => c.Name == collectionName);
            }
            await LoadCollectionFilesAsync();
        }
    }

    private async Task ShowCreateFolderDialogAsync(FolderNode? parentFolder = null)
    {
        var parameters = new DialogParameters<TextInputDialog>
        {
            { x => x.ContentText, parentFolder != null
                ? $"Enter name for new subfolder in '{parentFolder.Name}'"
                : "Enter name for new folder" },
            { x => x.Label, "Folder Name" },
            { x => x.ButtonText, "CREATE" },
            { x => x.CancelText, "CANCEL" },
            { x => x.ButtonColor, Color.Primary },
            { x => x.HelperText, "Use any characters except ending with . / \\ or consecutive dots. Max 1,024 characters." },
            { x => x.MaxLength, 1024 },
            { x => x.ValidationFunc, new Func<string, string?>(ValidateFolderName) }
        };

        var options = new DialogOptions { CloseButton = true, MaxWidth = MaxWidth.Small, FullWidth = true };
        var dialog = await DialogService.ShowAsync<TextInputDialog>("Create Folder", parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: string folderName } && !string.IsNullOrWhiteSpace(folderName))
        {
            await CreateFolderAsync(folderName, parentFolder);
        }
    }

    private string? ValidateFolderName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Folder name is required";

        if (name.Length < 1 || name.Length > 1024)
            return "Folder name must be between 1 and 1,024 characters";

        // Check for invalid endings
        if (name.EndsWith('.') || name.EndsWith('/') || name.EndsWith('\\'))
            return "Folder name cannot end with . / or \\";

        // Check for consecutive dots
        if (name.Contains(".."))
            return "Folder name cannot contain consecutive dots";

        // Check for path segments ending with dot
        var segments = name.Split(new[] { '/', '\\' }, StringSplitOptions.None);
        if (segments.Any(s => s.EndsWith('.') && s != "."))
            return "Path segments cannot end with a dot";

        return null;
    }

    private async Task CreateFolderAsync(string folderName, FolderNode? parentFolder = null)
    {
        if (string.IsNullOrEmpty(_selectedCollection) || string.IsNullOrWhiteSpace(folderName))
            return;

        string folderPath;

        if (parentFolder != null && parentFolder.Path != _selectedCollection)
        {
            // Remove collection name from parent path to get the actual folder path
            var parentPath = parentFolder.Path.StartsWith(_selectedCollection + "/")
                ? parentFolder.Path.Substring(_selectedCollection.Length + 1)
                : parentFolder.Path;
            folderPath = $"{parentPath}/{folderName}";
        }
        else
        {
            // Creating at root level of collection
            folderPath = folderName;
        }

        Logger.LogInformation($"Creating folder: Collection='{_selectedCollection}', FolderPath='{folderPath}'");

        try
        {
            var success = await Client.CreateFolderAsync(_selectedCollection, folderPath);

            if (success)
            {
                SnackBarMessage($"Folder '{folderPath}' created in '{_selectedCollection}'");
                await LoadFolderStructureAsync();
            }
            else
            {
                SnackBarError($"Failed to create folder '{folderName}'");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error creating folder {FolderName}", folderName);
            SnackBarError($"Error creating folder: {ex.Message}");
        }
    }

    private async Task RenameFolderAsync(FolderNode folder)
    {
        if (string.IsNullOrEmpty(_selectedCollection) || folder == null)
            return;

        // Show input dialog for new name
        var result = await DialogService.ShowMessageBox(
            "Rename Folder",
            $"Enter new name for folder '{folder.Name}'",
            yesText: "Rename",
            cancelText: "Cancel");

        if (result == true)
        {
            // This is simplified - you'd want to get the actual new name from a proper input dialog
            var newFolderName = "renamed-folder";
            var pathParts = folder.Path.Split('/');
            pathParts[pathParts.Length - 1] = newFolderName;
            var newFolderPath = string.Join("/", pathParts);

            try
            {
                var success = await Client.RenameFolderAsync(_selectedCollection, folder.Path, newFolderPath);

                if (success)
                {
                    SnackBarMessage($"Folder renamed successfully");
                    await LoadFolderStructureAsync();
                }
                else
                {
                    SnackBarError("Failed to rename folder");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error renaming folder {FolderPath}", folder.Path);
                SnackBarError($"Error renaming folder: {ex.Message}");
            }
        }
    }

    private async Task DeleteFolderAsync(FolderNode folder)
    {
        if (string.IsNullOrEmpty(_selectedCollection) || folder == null)
            return;

        var parameters = new DialogParameters
        {
            { "ContentText", $"Are you sure you want to delete folder '{folder.Name}' and all its contents? This action cannot be undone." },
            { "ButtonText", "Delete" },
            { "Color", Color.Error }
        };

        var dialog = await DialogService.ShowAsync<ConfirmationDialog>("Confirm Folder Deletion", parameters);
        var result = await dialog.Result;

        if (result!.Canceled)
            return;

        try
        {
            // Extract folder path without collection name prefix
            var folderPath = folder.Path.StartsWith(_selectedCollection! + "/")
                ? folder.Path.Substring(_selectedCollection.Length + 1)
                : folder.Path;

            Logger.LogInformation($"Deleting folder: Collection='{_selectedCollection}', FolderPath='{folderPath}'");

            var success = await Client.DeleteFolderAsync(_selectedCollection, folderPath);

            if (success)
            {
                SnackBarMessage($"Folder '{folder.Name}' deleted successfully");
                _selectedFolder = null;
                _currentFolderPath = "";
                await LoadFolderStructureAsync();
            }
            else
            {
                SnackBarError($"Failed to delete folder '{folder.Name}'");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting folder {FolderPath}", folder.Path);
            SnackBarError($"Error deleting folder: {ex.Message}");
        }
    }

    private async Task ShowEditFileMetadataDialogAsync(ContainerFileInfo fileInfo)
    {
        if (string.IsNullOrEmpty(_selectedCollection) || fileInfo == null)
            return;

        try
        {
            // Load existing metadata
            var metadata = await Client.GetFileMetadataAsync(_selectedCollection, fileInfo.FileName);

            if (metadata == null)
            {
                // Create new metadata with default values
                metadata = new FileMetadata
                {
                    FileName = fileInfo.FileName,
                    BlobPath = string.IsNullOrEmpty(fileInfo.FolderPath)
                        ? fileInfo.FileName
                        : $"{fileInfo.FolderPath}/{fileInfo.FileName}"
                };
            }

            var parameters = new DialogParameters<FileMetadataDialog>
            {
                { x => x.FileName, fileInfo.FileName },
                { x => x.Metadata, metadata }
            };

            var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, CloseButton = true };
            var dialog = await DialogService.ShowAsync<FileMetadataDialog>("Edit File Metadata", parameters, options);
            var result = await dialog.Result;

            if (result is { Canceled: false, Data: FileMetadata updatedMetadata })
            {
                var success = await Client.UpdateFileMetadataAsync(_selectedCollection!, fileInfo.FileName, updatedMetadata);

                if (success)
                {
                    SnackBarMessage("File metadata updated successfully");
                    fileInfo.Metadata = updatedMetadata;
                    StateHasChanged();
                }
                else
                {
                    SnackBarError("Failed to update file metadata");
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating file metadata for {FileName}", fileInfo.FileName);
            SnackBarError($"Error updating file metadata: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
        _cancellationTokenSource.Dispose();
    }
}
