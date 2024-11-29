namespace SyncRoutineWS.OCPCModel;

public partial class TblProjFieldChng
{
    public int ChngId { get; set; }

    public int? ProjId { get; set; }

    public DateTime? ChngDt { get; set; }

    public DateTime? EmailDt { get; set; }

    public string? FieldName { get; set; }

    public int? SortOrder { get; set; }

    public int? ChangedId { get; set; }

    public DateTime? SyncDt { get; set; }
}