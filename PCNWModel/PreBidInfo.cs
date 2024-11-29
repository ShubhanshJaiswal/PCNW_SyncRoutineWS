namespace SyncRoutineWS.PCNWModel;

public partial class PreBidInfo
{
    public int Id { get; set; }

    public DateTime? PreBidDate { get; set; }

    public string? PreBidTime { get; set; }

    public string? Location { get; set; }

    public string? Pst { get; set; }

    public bool Mandatory { get; set; }

    public bool PreBidAnd { get; set; }

    public bool? IsDeleted { get; set; }

    public int? ProjId { get; set; }

    public bool? UndecidedPreBid { get; set; }

    public int SyncStatus { get; set; }
}