using System;
using System.Collections.Generic;

namespace SyncRoutineWS.PCNWModel;

public partial class Addendum
{
    public int AddendaId { get; set; }

    public string? AddendaNo { get; set; }

    public bool? MoreInfo { get; set; }

    public int? ProjId { get; set; }

    public DateTime? InsertDt { get; set; }

    public string? MvwebPath { get; set; }

    public DateTime? IssueDt { get; set; }

    public string? PageCnt { get; set; }

    public bool? NewBd { get; set; }

    public string? ParentFolder { get; set; }

    public bool Deleted { get; set; }

    public int ParentId { get; set; }

    public int SyncStatus { get; set; }

    public int? SyncAddendaId { get; set; }

    public virtual Project? Proj { get; set; }
}
