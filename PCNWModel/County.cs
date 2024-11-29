namespace SyncRoutineWS.PCNWModel;

public partial class County
{
    public int CountyId { get; set; }

    public string? County1 { get; set; }

    public string? State { get; set; }

    public int SyncStatus { get; set; }

    public int? SyncCouId { get; set; }
}