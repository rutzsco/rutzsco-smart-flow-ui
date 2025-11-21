using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace SmartFlow.UI.Client.Components;

public abstract class SafeComponentBase : ComponentBase
{
    [Inject]
    protected GlobalErrorHandler ErrorHandler { get; set; } = null!;

    [Inject]
    protected ISnackbar Snackbar { get; set; } = null!;

    [Inject]
    protected ILogger<SafeComponentBase> Logger { get; set; } = null!;

    protected async Task SafeExecuteAsync(Func<Task> action, string? operationName = null)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            HandleError(ex, operationName);
        }
    }

    protected async Task<T?> SafeExecuteAsync<T>(Func<Task<T>> action, string? operationName = null, T? defaultValue = default)
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            HandleError(ex, operationName);
            return defaultValue;
        }
    }

    protected void SafeExecute(Action action, string? operationName = null)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            HandleError(ex, operationName);
        }
    }

    protected T? SafeExecute<T>(Func<T> action, string? operationName = null, T? defaultValue = default)
    {
        try
        {
            return action();
        }
        catch (Exception ex)
        {
            HandleError(ex, operationName);
            return defaultValue;
        }
    }

    protected virtual void HandleError(Exception exception, string? operationName = null)
    {
        var componentName = GetType().Name;
        var context = string.IsNullOrEmpty(operationName)
            ? $"Component: {componentName}"
            : $"Component: {componentName}, Operation: {operationName}";

        ErrorHandler.HandleError(exception, context);
        Logger.LogError(exception, "Error in {ComponentName} during {Operation}", componentName, operationName ?? "unknown operation");

        ShowErrorNotification(operationName);
    }

    protected virtual void ShowErrorNotification(string? operationName = null)
    {
        var message = string.IsNullOrEmpty(operationName)
            ? "An error occurred. Please try again."
            : $"An error occurred during {operationName}. Please try again.";

        Snackbar.Add(message, Severity.Error, config =>
        {
            config.VisibleStateDuration = 5000;
            config.ShowCloseIcon = true;
        });
    }
}
