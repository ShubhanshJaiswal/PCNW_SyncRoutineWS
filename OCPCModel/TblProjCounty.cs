﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace SyncRoutineWS.OCPCModel;

public partial class TblProjCounty
{
    public int ProjCountyId { get; set; }

    public int CountyId { get; set; }

    public int ProjId { get; set; }

    public int SyncStatus { get; set; }

    public virtual TblCounty County { get; set; }

    public virtual TblProject Proj { get; set; }
}