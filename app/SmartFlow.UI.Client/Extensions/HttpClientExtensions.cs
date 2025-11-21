using System.Net;
using System.Text.Json;

namespace SmartFlow.UI.Client.Extensions;

public static class HttpClientExtensions
{
    public static async Task<T?> GetFromJsonWithErrorHandlingAsync<T>(
        this HttpClient httpClient,
        string requestUri,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetAsync(requestUri, cancellationToken);
            await EnsureSuccessStatusCodeWithDetails(response);
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new NotFoundException($"Resource not found: {requestUri}", ex);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedException("You are not authorized to access this resource", ex);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            throw new ForbiddenException("Access to this resource is forbidden", ex);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            var errorMessage = await TryGetErrorMessage(ex);
            throw new BadRequestException(errorMessage ?? "The request was invalid", ex);
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("The request was cancelled", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new TimeoutException("The request timed out", ex);
        }
    }

    public static async Task<TResponse?> PostAsJsonWithErrorHandlingAsync<TRequest, TResponse>(
        this HttpClient httpClient,
        string requestUri,
        TRequest content,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync(requestUri, content, cancellationToken);
            await EnsureSuccessStatusCodeWithDetails(response);
            return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
        {
            var errorMessage = await TryGetErrorMessage(ex);
            throw new BadRequestException(errorMessage ?? "The request was invalid", ex);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new UnauthorizedException("You are not authorized to perform this action", ex);
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("The request was cancelled", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new TimeoutException("The request timed out", ex);
        }
    }

    private static async Task EnsureSuccessStatusCodeWithDetails(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}: {content}",
                null,
                response.StatusCode);
        }
    }

    private static async Task<string?> TryGetErrorMessage(HttpRequestException ex)
    {
        try
        {
            // Try to extract error message from the exception
            var message = ex.Message;
            if (message.Contains("{") && message.Contains("}"))
            {
                var jsonStart = message.IndexOf('{');
                var jsonString = message.Substring(jsonStart);
                using var doc = JsonDocument.Parse(jsonString);
                if (doc.RootElement.TryGetProperty("error", out var errorProp))
                {
                    return errorProp.GetString();
                }
                if (doc.RootElement.TryGetProperty("message", out var messageProp))
                {
                    return messageProp.GetString();
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }
        return null;
    }
}

// Custom exception types
public class NotFoundException : Exception
{
    public NotFoundException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}

public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}

public class ForbiddenException : Exception
{
    public ForbiddenException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}

public class BadRequestException : Exception
{
    public BadRequestException(string message, Exception? innerException = null)
        : base(message, innerException) { }
}
