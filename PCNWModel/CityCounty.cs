﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace SyncRoutineWS.PCNWModel;

public partial class CityCounty
{
    public int CityCountyId { get; set; }

    public string City { get; set; }

    public int? CountyId { get; set; }

    public int SyncStatus { get; set; }

    public int? SyncCitCouId { get; set; }
}