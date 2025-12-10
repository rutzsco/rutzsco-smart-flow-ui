// Copyright (c) Microsoft. All rights reserved.

namespace MinimalApi.Services.Search.IndexDefinitions;

public class CustomProductKnowledgeV1IndexDefinition : IKnowledgeSource
{
    public required string chunk_id { get; set; }

    public required string parent_id { get; set; }

    public required string chunk_index { get; set; }

    public required string content { get; set; }

    public required string title { get; set; }

    public required string blob_path { get; set; }

    public required string file_name { get; set; }

    public required string content_type { get; set; }

    public required DateTime created_on { get; set; }

    public required DateTime blob_last_modified { get; set; }

    public required string equipment_category { get; set; }

    public required string equipment_subcategory { get; set; }

    public required string equipment_part { get; set; }

    public required string equipment_part_subcategory { get; set; }

    public required string product { get; set; }

    public required string manufacturer { get; set; }

    public required string document_type { get; set; }

    public required bool is_required_for_cde { get; set; }
    
    public required bool last_indexed { get; set; }

    public KnowledgeSource GetSource(bool useSourcepage = false)
    {
        return new KnowledgeSource(GetFilepath(useSourcepage), content);
    }

    public string GetFilepath(bool useSourcepage = false)
    {
        // Use metadata as the source identifier, fallback to id if metadata is empty
        return !string.IsNullOrEmpty(blob_path) ? blob_path : chunk_id;
    }

    public static string EmbeddingsFieldName = "content_vector";
    public static List<string> SelectFieldNames = new List<string> { "chunk_id", "content", "blob_path", "file_name", "content_type", "created_on", "blob_last_modified", "equipment_category", "equipment_subcategory", "equipment_part", "equipment_part_subcategory", "product", "manufacturer", "document_type", "is_required_for_cde", "last_indexed" };
    public static string Name = "CustomProductKnowledgeV1";
}
