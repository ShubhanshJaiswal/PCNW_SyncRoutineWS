namespace SyncRoutineWS.PCNWModel;

public partial class Address
{
    public int AddressId { get; set; }

    public int BusinessEntityId { get; set; }

    public string AddressName { get; set; } = "";

    public string Addr1 { get; set; } = "";

    public string City { get; set; } = "";

    public string State { get; set; } = "";

    public string Zip { get; set; } = "";

    public int SyncStatus { get; set; }

    public int? SyncMemId { get; set; }

    public int? SyncAoid { get; set; }

    public int? SyncConId { get; set; }

    public virtual BusinessEntity BusinessEntity { get; set; } 
}