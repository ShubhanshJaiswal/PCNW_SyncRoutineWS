namespace SyncRoutineWS.PCNWModel;

public partial class Contact
{
    public int ContactId { get; set; }

    public int BusinessEntityId { get; set; }

    public Guid UserId { get; set; }

    public string? ContactName { get; set; }

    public string? ContactTitle { get; set; }

    public string? ContactPhone { get; set; }

    public string? ContactEmail { get; set; }

    public bool? MainContact { get; set; }

    public bool? Daily { get; set; }

    public string? Uid { get; set; }

    public bool? TextMsg { get; set; }

    public string? Password { get; set; }

    public bool? Message { get; set; }

    public DateTime? MessageDt { get; set; }

    public bool? AutoSearch { get; set; }

    public string? ContactAddress { get; set; }

    public string? ContactState { get; set; }

    public string? ContactCity { get; set; }

    public string? ContactZip { get; set; }

    public string? ContactCounty { get; set; }

    public int LocId { get; set; }

    public string? BillEmail { get; set; }

    public string? Extension { get; set; }

    public int? CompType { get; set; }

    public bool? Active { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public int SyncStatus { get; set; }

    public int? SyncConId { get; set; }

    public virtual BusinessEntity BusinessEntity { get; set; } = null!;
}