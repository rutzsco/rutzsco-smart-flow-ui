// Copyright (c) Microsoft. All rights reserved.

using System.Text.Json.Serialization;

namespace Shared.Models;

/// <summary>
/// Represents the equipment map result from spec analysis workflow.
/// </summary>
public class EquipmentMapResult
{
    [JsonPropertyName("project_name")]
    public string ProjectName { get; set; } = string.Empty;

    [JsonPropertyName("equipment_types")]
    public List<string> EquipmentTypes { get; set; } = new();

    [JsonPropertyName("sections_by_equipment")]
    public Dictionary<string, List<EquipmentSection>> SectionsByEquipment { get; set; } = new();

    [JsonPropertyName("equipment_by_section")]
    public Dictionary<string, List<string>> EquipmentBySection { get; set; } = new();

    [JsonPropertyName("summary")]
    public EquipmentMapSummary? Summary { get; set; }
}

/// <summary>
/// Represents a section associated with equipment.
/// </summary>
public class EquipmentSection
{
    [JsonPropertyName("section_code")]
    public string SectionCode { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("match_type")]
    public string MatchType { get; set; } = string.Empty;

    [JsonPropertyName("start_page")]
    public int StartPage { get; set; }

    [JsonPropertyName("end_page")]
    public int EndPage { get; set; }

    [JsonPropertyName("referenced_by")]
    public string? ReferencedBy { get; set; }
}

/// <summary>
/// Summary statistics for the equipment map.
/// </summary>
public class EquipmentMapSummary
{
    [JsonPropertyName("total_sections_mapped")]
    public int TotalSectionsMapped { get; set; }

    [JsonPropertyName("ahu_primary_sections")]
    public int AhuPrimarySections { get; set; }

    [JsonPropertyName("ahu_supporting_sections")]
    public int AhuSupportingSections { get; set; }

    [JsonPropertyName("chiller_primary_sections")]
    public int ChillerPrimarySections { get; set; }

    [JsonPropertyName("chiller_supporting_sections")]
    public int ChillerSupportingSections { get; set; }
}
