using System.Text.Json.Serialization;

namespace Toolbox.Data.Models;

public class ShopSystemConfig
{
    [JsonPropertyName("directories")]
    public ShopSystemDirectories Directories { get; set; } = new();
}

public class ShopSystemDirectories
{
    [JsonPropertyName("shopPath")] public string? ShopPath { get; set; }
    [JsonPropertyName("apiPath")] public string? ApiPath { get; set; }
    [JsonPropertyName("activityServiceTestAppPath")] public string? ActivityServiceTestAppPath { get; set; }
    [JsonPropertyName("backOfficePath")] public string? BackOfficePath { get; set; }
    [JsonPropertyName("communicationPath")] public string? CommunicationPath { get; set; }
    [JsonPropertyName("erpCommunicationPath")] public string? ErpCommunicationPath { get; set; }
    [JsonPropertyName("officeLineCommunicationPath")] public string? OfficeLineCommunicationPath { get; set; }
    [JsonPropertyName("orderServicePath")] public string? OrderServicePath { get; set; }
}

