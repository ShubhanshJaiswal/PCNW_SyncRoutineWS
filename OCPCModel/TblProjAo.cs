﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace SyncRoutineWS.OCPCModel;

public partial class TblProjAo
{
    public int ProjAo { get; set; }

    public int? AotypeId { get; set; }

    public int? ArchOwnerId { get; set; }

    public bool? BoldBp { get; set; }

    public bool? ShowOnResults { get; set; }

    public int? ProjId { get; set; }

    public int? SortOrder { get; set; }

    public int SyncStatus { get; set; }

    public virtual TblArchOwner ArchOwner { get; set; }

    public virtual TblProject Proj { get; set; }
}