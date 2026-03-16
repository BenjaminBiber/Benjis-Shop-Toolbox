namespace Toolbox.Data.Models;

public class VmCustomerMapping
{
    public int Id { get; set; }
    public string VmName { get; set; } = "";
    public string CustomerName { get; set; } = "";
    public string TfsProjectName { get; set; } = "";
    public string? CustomerId { get; set; }
    public string? RdpUsername { get; set; }
    public string? RdpPassword { get; set; }
    public DateTime LastSynced { get; set; }
}
