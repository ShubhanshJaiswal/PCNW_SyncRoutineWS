﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace SyncRoutineWS.OCPCModel;

public partial class TblCounty
{
    public int CountyId { get; set; }

    public string County { get; set; }

    public string State { get; set; }

    public int SyncStatus { get; set; }

    public virtual ICollection<TblProjCounty> TblProjCounties { get; set; } = new List<TblProjCounty>();
}