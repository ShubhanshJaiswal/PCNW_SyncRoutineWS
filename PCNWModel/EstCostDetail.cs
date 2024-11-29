namespace SyncRoutineWS.PCNWModel;

public partial class EstCostDetail
{
    public int Id { get; set; }

    public string? EstCostTo { get; set; }

    public string? EstCostFrom { get; set; }

    public string? Description { get; set; }

    public int? ProjId { get; set; }

    public bool Removed { get; set; }

    public string? RangeSign { get; set; }

    public int SyncStatus { get; set; }
}