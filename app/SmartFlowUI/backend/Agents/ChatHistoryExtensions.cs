using System.Text;
using Microsoft.SemanticKernel.ChatCompletion;

namespace MinimalApi.Agents;

public static class ChatHistoryExtensions
{
    /// <summary>
    /// Adds a user message to the chat history, including handling file uploads (images, PDFs, CSVs, etc.).
    /// </summary>
    public static void AddUserMessageWithUploads(
        this ChatHistory chatHistory,
        string userMessage,
        IEnumerable<FileSummary> fileUploads)
    {
        if (fileUploads != null && fileUploads.Any())
        {
            var chatMessageContentItemCollection = new ChatMessageContentItemCollection();
            chatMessageContentItemCollection.Add(new TextContent(userMessage));

            foreach (var file in fileUploads)
            {
                DataUriParser parser = new DataUriParser(file.DataUrl);
                if (parser.MediaType == "image/jpeg" || parser.MediaType == "image/png")
                {
                    chatMessageContentItemCollection.Add(new ImageContent(parser.Data, parser.MediaType));
                }
                else if (parser.MediaType == "application/pdf")
                {
                    string pdfData = PDFTextExtractor.ExtractTextFromPdf(parser.Data);
                    chatMessageContentItemCollection.Add(new TextContent(pdfData));
                }
                else
                {
                    string csvData = Encoding.UTF8.GetString(parser.Data);
                    chatMessageContentItemCollection.Add(new TextContent(csvData));
                }
            }

            chatHistory.AddUserMessage(chatMessageContentItemCollection);
        }
        else
        {
            chatHistory.AddUserMessage(userMessage);
        }
    }
}
