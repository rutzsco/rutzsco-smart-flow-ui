namespace SmartFlow.UI.Client.Services;

public class GlobalErrorHandler
{
    private readonly ILogger<GlobalErrorHandler> _logger;
    private readonly List<ErrorRecord> _errorHistory = new();
    private const int MaxErrorHistory = 100;

    public event Action<Exception>? OnErrorOccurred;

    public GlobalErrorHandler(ILogger<GlobalErrorHandler> logger)
    {
        _logger = logger;
    }

    public void HandleError(Exception exception, string? context = null)
    {
        var errorRecord = new ErrorRecord
        {
            Exception = exception,
            Timestamp = DateTime.UtcNow,
            Context = context
        };

        _errorHistory.Insert(0, errorRecord);
        if (_errorHistory.Count > MaxErrorHistory)
        {
            _errorHistory.RemoveAt(_errorHistory.Count - 1);
        }

        _logger.LogError(exception, "Error occurred in context: {Context}", context ?? "Unknown");

        OnErrorOccurred?.Invoke(exception);
    }

    public IReadOnlyList<ErrorRecord> GetErrorHistory() => _errorHistory.AsReadOnly();

    public void ClearErrorHistory() => _errorHistory.Clear();
}

public class ErrorRecord
{
    public Exception Exception { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public string? Context { get; set; }
}
