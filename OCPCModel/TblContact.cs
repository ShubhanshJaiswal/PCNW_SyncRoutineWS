﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;

namespace SyncRoutineWS.OCPCModel;

public partial class TblContact
{
    public int ConId { get; set; }

    public int Id { get; set; }

    public string? Contact { get; set; }

    /// <summary>
    /// Owner, Estimator
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// If there are multiple contacts, which is the main contact?
    /// </summary>
    public bool? MainContact { get; set; }

    public string? Phone { get; set; }

    /// <summary>
    /// Send Daily Email?
    /// </summary>
    public bool? Daily { get; set; }

    public bool? TextMsg { get; set; }

    public string? Email { get; set; }

    public string? Uid { get; set; }

    public string? Password { get; set; }

    public bool? Message { get; set; }

    public DateTime? MessageDt { get; set; }

    public bool? AutoSearch { get; set; }

    public string? LastName { get; set; }

    public string? FirstName { get; set; }

    public int? SyncStatus { get; set; }

    public virtual TblMember IdNavigation { get; set; } = null!;
}