namespace SyncRoutineWS.PCNWModel;

public partial class BusinessEntity
{
    public int BusinessEntityId { get; set; }

    public string BusinessEntityName { get; set; } = null!;

    public string? BusinessEntityEmail { get; set; }

    public string? BusinessEntityPhone { get; set; }

    public bool IsMember { get; set; }

    public bool IsArchitect { get; set; }

    public bool IsContractor { get; set; }

    public int OldAoId { get; set; }

    public int OldConId { get; set; }

    public int OldMemId { get; set; }

    public int SyncStatus { get; set; }

    public int? SyncMemId { get; set; }

    public int? SyncAoid { get; set; }

    public int? SyncConId { get; set; }

    public virtual ICollection<Address> Addresses { get; set; } = new List<Address>();

    public virtual ICollection<Contact> Contacts { get; set; } = new List<Contact>();
}