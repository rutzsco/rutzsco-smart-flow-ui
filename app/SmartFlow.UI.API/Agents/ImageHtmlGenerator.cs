// Copyright (c) Microsoft. All rights reserved.

namespace MinimalApi.Agents;

/// <summary>
/// Utility class for generating enhanced HTML for image display with download functionality
/// </summary>
public static class ImageHtmlGenerator
{
    /// <summary>
    /// Generates enhanced HTML for displaying an image with styled borders and a download button
    /// </summary>
    /// <param name="imageUrl">The URL of the image to display</param>
    /// <param name="imageId">Unique identifier for the image element</param>
    /// <returns>HTML string with styled image and download button</returns>
    public static string GenerateEnhancedImageHtml(string imageUrl, string imageId)
    {
        return $"""
        <br/>
        <div class="image-wrapper" style="position: relative; display: inline-block; margin: 10px 0;">
            <div class="image-container" style="position: relative; display: inline-block;">
                <img src="{imageUrl}" 
                     style="border: 2px solid #e0e0e0; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); max-width: 750px; padding: 0;"
                     id="img-{imageId}" />
            </div>
            <div style="display: flex; justify-content: flex-end; margin-top: 8px;">
                <button onclick="downloadImage('{imageUrl}', 'image-{imageId}.png')" 
                        style="background: #f5f5f5; border: 1px solid #ddd; border-radius: 6px; padding: 4px 8px; cursor: pointer; box-shadow: 0 1px 3px rgba(0,0,0,0.2); display: flex; align-items: center; gap: 6px; color: #666; font-size: 10px;"
                        title="Download Image">
                    <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">
                        <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
                        <polyline points="7,10 12,15 17,10"></polyline>
                        <line x1="12" y1="15" x2="12" y2="3"></line>
                    </svg>
                    Download
                </button>
            </div>
        </div>
        <br/>
        """;
    }
}
