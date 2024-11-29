namespace SyncRoutineWS.PCNWModel;

public partial class ProjCounty
{
    public int ProjCountyId { get; set; }

    public int CountyId { get; set; }

    public int ProjId { get; set; }

    public int SyncStatus { get; set; }

    public int? SyncProCouId { get; set; }

    public virtual Project Proj { get; set; } = null!;
}