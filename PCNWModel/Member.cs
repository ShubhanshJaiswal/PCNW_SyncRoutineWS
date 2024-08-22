using System;
using System.Collections.Generic;

namespace SyncRoutineWS.PCNWModel;

public partial class Member
{
    public int MemberId { get; set; }

    public int BusinessEntityId { get; set; }

    public bool Inactive { get; set; }

    public DateTime InsertDate { get; set; }

    public string? LastPayDate { get; set; }

    public DateTime? RenewalDate { get; set; }

    public string? Term { get; set; }

    public string? Div { get; set; }

    public string? Discipline { get; set; }

    public string? Note { get; set; }

    public string? MinorityStatus { get; set; }

    public int? MemberType { get; set; }

    public bool AcceptedTerms { get; set; }

    public DateTime? AcceptedTermsDt { get; set; }

    public string? DailyEmail { get; set; }

    public bool? Html { get; set; }

    public bool? Overdue { get; set; }

    public bool? Cod { get; set; }

    public byte[]? TmStamp { get; set; }

    public string? PaperlessBilling { get; set; }

    public decimal? MemberCost { get; set; }

    public decimal? MagCost { get; set; }

    public decimal? ArchPkgCost { get; set; }

    public decimal? AddPkgCost { get; set; }

    public string? ResourceDate { get; set; }

    public decimal? ResourceCost { get; set; }

    public string? WebAdDate { get; set; }

    public decimal? WebAdCost { get; set; }

    public bool? Phl { get; set; }

    public string? Email { get; set; }

    public decimal? NameField { get; set; }

    public DateTime? FavExp { get; set; }

    public int? Grace { get; set; }

    public int? ConId { get; set; }

    public bool? Gcservices { get; set; }

    public string? ResourceStandard { get; set; }

    public string? ResourceColor { get; set; }

    public string? ResourceLogo { get; set; }

    public string? ResourceAdd { get; set; }

    public string? Dba { get; set; }

    public string? Dba2 { get; set; }

    public string? Fka { get; set; }

    public bool? Suspended { get; set; }

    public string? SuspendedDt { get; set; }

    public string? Fax { get; set; }

    public string? MailAddress { get; set; }

    public string? MailCity { get; set; }

    public string? MailState { get; set; }

    public string? MailZip { get; set; }

    public decimal? OverdueAmt { get; set; }

    public DateTime? OverdueDt { get; set; }

    public string? CalSort { get; set; }

    public bool? Pdfpkg { get; set; }

    public bool? ArchPkg { get; set; }

    public bool? AddPkg { get; set; }

    public bool? Bend { get; set; }

    public int? Credits { get; set; }

    public bool? FreelanceEstimator { get; set; }

    public string? HowdUhearAboutUs { get; set; }

    public bool? IsAutoRenew { get; set; }

    public string? CompanyPhone { get; set; }

    public string? Logo { get; set; }

    public bool CheckDirectory { get; set; }

    public int? MemId { get; set; }

    public int? InvoiceId { get; set; }

    public string? Discount { get; set; }

    public string? PayModeRef { get; set; }

    public string? CreatedBy { get; set; }

    public bool? IsDelete { get; set; }

    public DateTime? AddGraceDate { get; set; }

    public DateTime? ActualRenewalDate { get; set; }

    public int SyncStatus { get; set; }

    public int? SyncMemId { get; set; }
}
