namespace TKS.LeadPilot.Core.Models;

public class PipelineStage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public string ColorHex { get; set; } = "#3B82F6";
    public bool IsWonStage { get; set; } = false;
    public bool IsLostStage { get; set; } = false;
}
