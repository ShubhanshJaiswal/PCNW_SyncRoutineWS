﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace SyncRoutineWS.OCPCModel;

public partial class TblArchOwner
{
    public int Id { get; set; }

    public string Name { get; set; }

    public string Addr1 { get; set; }

    public string Type1 { get; set; }

    public string Email { get; set; }

    public string City { get; set; }

    public string State { get; set; }

    public string Zip { get; set; }

    public string Phone { get; set; }

    public string Fax { get; set; }

    public int? AotypeId { get; set; }

    public string Uid { get; set; }

    public string Pwd { get; set; }

    public string WebAddress { get; set; }

    public string Cnote { get; set; }

    public int SyncStatus { get; set; }

    public virtual ICollection<TblProjAo> TblProjAos { get; set; } = new List<TblProjAo>();
}