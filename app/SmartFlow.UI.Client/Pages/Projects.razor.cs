// Copyright (c) Microsoft. All rights reserved.

using Shared.Models;

namespace SmartFlow.UI.Client.Pages;

public sealed partial class Projects : IDisposable
{
    private const long MaxIndividualFileSize = 1_024 * 1_024 * 10;

    private MudForm _form = null!;
    private MudForm _createProjectForm = null!;
    private bool _createProjectFormValid = false;

    private bool _isLoadingDocuments = false;
    private bool _isUploadingDocuments = false;
    private bool _isIndexingDocuments = false;
    private bool _isLoadingProjects = false;
    private bool _showUploadSection = false; // Hidden by default - user clicks "Upload Document" to show
    private string _filter = "";
    private string _projectFilter = "";
    private int _filteredProjectCount = 0;
    private HashSet<string> _processingFiles = new(); // Track files being processed
    private HashSet<string> _deletingFiles = new(); // Track files being deleted

    // Store a cancelation token that will be used to cancel if the user disposes of this component.
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    [Inject] public required ApiClient Client { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required ILogger<Projects> Logger { get; set; }
    [Inject] public required IJSRuntime JSRuntime { get; set; }
    [Inject] public required HttpClient HttpClient { get; set; }
    [Inject] public required IDialogService DialogService { get; set; }

    // Project management
    private List<CollectionInfo> _projects = new();
    private string _selectedProject = "";
    private CollectionInfo? _selectedProjectInfo = null;
    private List<ContainerFileInfo> _projectFiles = new();
    private bool _showCreateProjectForm = false;
    private string _newProjectName = "";
    private string _newProjectDescription = "";
    private string _newProjectType = "";

    // Edit project metadata
    private bool _showEditMetadataForm = false;
    private string _editProjectDescription = "";
    private string _editProjectType = "";

    protected override async Task OnInitializedAsync()
    {
        // Load projects
        await LoadProjectsAsync();
    }

    protected override void OnAfterRender(bool firstRender)
    {
        if (!firstRender)
        {
            // Reset counter before filter runs
            _filteredProjectCount = 0;
        }
    }

    private async Task LoadProjectsAsync()
    {
        _isLoadingProjects = true;
        try
        {
            _projects = await Client.GetProjectsAsync();
            // Auto-select first project if available
            if (_projects.Any())
            {
                await SelectProjectAsync(_projects.First().Name);
            }
            else
            {
                _selectedProject = "";
                _selectedProjectInfo = null;
                _projectFiles.Clear();
                _fileUploads.Clear();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading projects");
            SnackBarError("Failed to load projects");
        }
        finally
        {
            _isLoadingProjects = false;
            StateHasChanged();
        }
    }

    private async Task SelectProjectAsync(string projectName)
    {
        if (_selectedProject != projectName)
        {
            _selectedProject = projectName;
            _selectedProjectInfo = _projects.FirstOrDefault(c => c.Name == projectName);
            _fileUploads.Clear(); // Clear any selected files when switching projects
            _filter = ""; // Clear filter when switching projects
            _showCreateProjectForm = false; // Hide create form when selecting a project
            _showUploadSection = false; // Hide upload section when switching projects
            _showEditMetadataForm = false; // Hide edit metadata form when switching projects
            await LoadProjectFilesAsync();
        }
    }

    private async Task LoadProjectFilesAsync()
    {
        if (string.IsNullOrEmpty(_selectedProject))
            return;

        _isLoadingDocuments = true;
        try
        {
            _projectFiles = await Client.GetProjectFilesAsync(_selectedProject);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading project files for {Project}", _selectedProject);
            SnackBarError($"Failed to load files from project '{_selectedProject}'");
        }
        finally
        {
            _isLoadingDocuments = false;
            StateHasChanged();
        }
    }

    private void ShowCreateProjectForm()
    {
        _newProjectName = "";
        _newProjectDescription = "";
        _newProjectType = "";
        _createProjectFormValid = false;
        _showCreateProjectForm = true;
        _showEditMetadataForm = false;
    }

    private void CancelCreateProject()
    {
        _showCreateProjectForm = false;
        _newProjectName = "";
        _newProjectDescription = "";
        _newProjectType = "";
        _createProjectFormValid = false;
    }

    private string ValidateProjectName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Project name is required";

        // Azure Storage container naming rules
        if (name.Length < 3 || name.Length > 63)
            return "Project name must be between 3 and 63 characters";

        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-z0-9]([a-z0-9-]*[a-z0-9])?$"))
            return "Project name must contain only lowercase letters, numbers, and hyphens, and must start and end with a letter or number";

        if (name.Contains("--"))
            return "Project name cannot contain consecutive hyphens";

        if (_projects.Any(c => c.Name == name))
            return "A project with this name already exists";

        return null!;
    }

    private async Task CreateProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(_newProjectName) || !_createProjectFormValid)
        {
            SnackBarError("Please enter a valid project name");
            return;
        }

        try
        {
            var success = await Client.CreateProjectAsync(
                _newProjectName, 
                string.IsNullOrWhiteSpace(_newProjectDescription) ? null : _newProjectDescription,
                string.IsNullOrWhiteSpace(_newProjectType) ? null : _newProjectType);
            
            if (success)
            {
                SnackBarMessage($"Project '{_newProjectName}' created successfully");
                _showCreateProjectForm = false;
                var createdProjectName = _newProjectName;
                _newProjectName = "";
                _newProjectDescription = "";
                _newProjectType = "";
                await LoadProjectsAsync();
                // Auto-select the newly created project
                await SelectProjectAsync(createdProjectName);
            }
            else
            {
                SnackBarError($"Failed to create project '{_newProjectName}'");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error creating project {ProjectName}", _newProjectName);
            SnackBarError($"Error creating project: {ex.Message}");
        }
    }

    private void ShowEditMetadataForm()
    {
        if (_selectedProjectInfo != null)
        {
            _editProjectDescription = _selectedProjectInfo.Description ?? "";
            _editProjectType = _selectedProjectInfo.Type ?? "";
            _showEditMetadataForm = true;
            _showCreateProjectForm = false;
            _showUploadSection = false;
        }
    }

    private void CancelEditMetadata()
    {
        _showEditMetadataForm = false;
        _editProjectDescription = "";
        _editProjectType = "";
    }

    private async Task ShowDeleteProjectDialogAsync()
    {
        if (string.IsNullOrEmpty(_selectedProject))
        {
            SnackBarError("No project selected");
            return;
        }

        // Show confirmation dialog
        var parameters = new DialogParameters
        {
            { "ContentText", $"Are you sure you want to delete project '{_selectedProject}'? This action cannot be undone. All files and metadata in this project will be deleted." },
            { "ButtonText", "Delete Project" },
            { "Color", Color.Error }
        };

        var dialog = await DialogService.ShowAsync<ConfirmationDialog>("Confirm Project Deletion", parameters);
        var result = await dialog.Result;

        if (result.Canceled)
            return;

        await DeleteProjectAsync();
    }

    private async Task DeleteProjectAsync()
    {
        if (string.IsNullOrEmpty(_selectedProject))
        {
            SnackBarError("No project selected");
            return;
        }

        try
        {
            var projectToDelete = _selectedProject;
            var success = await Client.DeleteProjectAsync(projectToDelete);

            if (success)
            {
                SnackBarMessage($"Project '{projectToDelete}' deleted successfully");
                _selectedProject = "";
                _selectedProjectInfo = null;
                _projectFiles.Clear();
                _fileUploads.Clear();
                await LoadProjectsAsync();
            }
            else
            {
                SnackBarError($"Failed to delete project '{projectToDelete}'");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error deleting project {ProjectName}", _selectedProject);
            SnackBarError($"Error deleting project: {ex.Message}");
        }
    }

    private async Task UpdateProjectMetadataAsync()
    {
        if (string.IsNullOrEmpty(_selectedProject))
        {
            SnackBarError("No project selected");
            return;
        }

        try
        {
            var success = await Client.UpdateProjectMetadataAsync(
                _selectedProject,
                string.IsNullOrWhiteSpace(_editProjectDescription) ? null : _editProjectDescription,
                string.IsNullOrWhiteSpace(_editProjectType) ? null : _editProjectType);

            if (success)
            {
                SnackBarMessage($"Project '{_selectedProject}' metadata updated successfully");
                _showEditMetadataForm = false;
                
                // Refresh the project info
                _selectedProjectInfo = await Client.GetProjectMetadataAsync(_selectedProject);
                
                // Update the project in the list
                var projectInList = _projects.FirstOrDefault(c => c.Name == _selectedProject);
                if (projectInList != null && _selectedProjectInfo != null)
                {
                    projectInList.Description = _selectedProjectInfo.Description;
                    projectInList.Type = _selectedProjectInfo.Type;
                }
                
                StateHasChanged();
            }
            else
            {
                SnackBarError($"Failed to update metadata for project '{_selectedProject}'");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating project metadata for {ProjectName}", _selectedProject);
            SnackBarError($"Error updating project metadata: {ex.Message}");
        }
    }

    private bool OnFileFilter(ContainerFileInfo fileInfo) => 
        string.IsNullOrWhiteSpace(_filter) || fileInfo.FileName.Contains(_filter, StringComparison.OrdinalIgnoreCase);

    private bool OnProjectFilter(CollectionInfo projectInfo)
    {
        if (string.IsNullOrWhiteSpace(_projectFilter))
        {
            _filteredProjectCount = _projects.Count;
            return true;
        }

        var filter = _projectFilter.ToLower();
        
        var matches = projectInfo.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                     (!string.IsNullOrWhiteSpace(projectInfo.Type) && 
                      projectInfo.Type.Contains(filter, StringComparison.OrdinalIgnoreCase)) ||
                     (!string.IsNullOrWhiteSpace(projectInfo.Description) && 
                      projectInfo.Description.Contains(filter, StringComparison.OrdinalIgnoreCase));

        if (matches)
        {
            _filteredProjectCount++;
        }

        return matches;
    }

    private async Task OnProjectSelectionChangedAsync(CollectionInfo? project)
    {
        if (project != null)
        {
            await SelectProjectAsync(project.Name);
        }
    }

    private async Task RefreshAsync()
    {
        await LoadProjectFilesAsync();
    }

    private async Task SubmitFilesForUploadAsync()
    {
        if (!_fileUploads.Any())
        {
            SnackBarError("Please select files to upload");
            return;
        }

        if (string.IsNullOrEmpty(_selectedProject))
        {
            SnackBarError("Please select a project to upload to");
            return;
        }

        _isUploadingDocuments = true;

        try
        {
            var metadata = new Dictionary<string, string>();
            var result = await Client.UploadFilesToProjectAsync(
                _fileUploads.ToArray(), 
                MaxIndividualFileSize, 
                _selectedProject, 
                metadata);

            Logger.LogInformation("Upload result: {Result}", result);

            if (result.IsSuccessful)
            {
                SnackBarMessage($"Uploaded {result.UploadedFiles.Length} document(s) to '{_selectedProject}'");
                _fileUploads.Clear();
                _showUploadSection = false; // Hide upload section after successful upload
            }
            else
            {
                SnackBarError($"Failed to upload documents. {result.Error}");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error uploading files to project {Project}", _selectedProject);
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

    private async Task ProcessDocumentLayoutAsync(string fileName)
    {
        if (string.IsNullOrEmpty(_selectedProject) || string.IsNullOrEmpty(fileName))
            return;

        // Add to processing set
        _processingFiles.Add(fileName);
        StateHasChanged();

        try
        {
            Logger.LogInformation("Processing document layout for {FileName} in {Project}", fileName, _selectedProject);
            
            var success = await Client.ProcessProjectDocumentLayoutAsync(_selectedProject, fileName);
            
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
        if (string.IsNullOrEmpty(_selectedProject) || string.IsNullOrEmpty(fileName))
            return;

        try
        {
            var fileUrl = await Client.GetProjectFileUrlAsync(_selectedProject, fileName, isProcessingFile);
            
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
        if (string.IsNullOrEmpty(_selectedProject) || string.IsNullOrEmpty(fileName))
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

        if (result.Canceled)
            return;

        // Add to deleting set
        _deletingFiles.Add(fileName);
        StateHasChanged();

        try
        {
            Logger.LogInformation("Deleting file {FileName} from {Project}", fileName, _selectedProject);
            
            var success = await Client.DeleteFileFromProjectAsync(_selectedProject, fileName);
            
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

    public void Dispose() => _cancellationTokenSource.Cancel();
}
