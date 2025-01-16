using Azure.AI.Inference;

namespace SmartFlowUI.Extensions
{
    public record SKResult(string Answer, CompletionsUsage? Usage, long DurationMilliseconds);
}
