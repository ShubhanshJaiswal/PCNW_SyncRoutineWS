namespace SyncRoutineWS.PCNWModel;

public partial class Entity
{
    public int EntityId { get; set; }

    public string? EntityType { get; set; }

    public string? EnityName { get; set; }

    public int? ProjId { get; set; }

    public int? ProjNumber { get; set; }

    public bool? IsActive { get; set; }

    public int? NameId { get; set; }

    public bool ChkIssue { get; set; }

    public int CompType { get; set; }

    public int SyncStatus { get; set; }

    public int? SyncProjConId { get; set; }

    public int? SyncProjAoid { get; set; }
}