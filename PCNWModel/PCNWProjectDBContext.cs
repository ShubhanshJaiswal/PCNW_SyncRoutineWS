using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PCNW.Models;

namespace SyncRoutineWS.PCNWModel;

public partial class PCNWProjectDBContext : IdentityDbContext<IdentityUser>
{

    public PCNWProjectDBContext(DbContextOptions<PCNWProjectDBContext> options) : base(options)
    {
    }

    public virtual DbSet<Addendum> Addenda { get; set; }

    public virtual DbSet<Address> Addresses { get; set; }

    public virtual DbSet<BusinessEntity> BusinessEntities { get; set; }

    public virtual DbSet<CityCounty> CityCounties { get; set; }

    public virtual DbSet<Contact> Contacts { get; set; }

    public virtual DbSet<County> Counties { get; set; }

    public virtual DbSet<Entity> Entities { get; set; }

    public virtual DbSet<EstCostDetail> EstCostDetails { get; set; }

    public virtual DbSet<Member> Members { get; set; }

    public virtual DbSet<PreBidInfo> PreBidInfos { get; set; }

    public virtual DbSet<ProjCounty> ProjCounties { get; set; }

    public virtual DbSet<Project> Projects { get; set; }
    public virtual DbSet<TblFileStorage> FileStorages{ get; set; }
    public virtual DbSet<PhlInfo> PhlInfos { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Addendum>(entity =>
        {
            entity.HasKey(e => e.AddendaId).HasName("PK_tblAddenda");

            entity.ToTable(tb =>
                {
                    tb.HasTrigger("tr_Addenda_D");
                    tb.HasTrigger("tr_Addenda_IU");
                });

            entity.HasIndex(e => e.ProjId, "FHProjId").IsDescending();

            entity.Property(e => e.AddendaId).HasColumnName("AddendaID");
            entity.Property(e => e.AddendaNo).HasMaxLength(50);
            entity.Property(e => e.InsertDt).HasColumnType("smalldatetime");
            entity.Property(e => e.IssueDt).HasColumnType("smalldatetime");
            entity.Property(e => e.MoreInfo).HasDefaultValue(false);
            entity.Property(e => e.MvwebPath)
                .HasMaxLength(200)
                .HasColumnName("MVWebPath");
            entity.Property(e => e.NewBd)
                .HasDefaultValue(false)
                .HasColumnName("NewBD");
            entity.Property(e => e.PageCnt).HasMaxLength(10);
            entity.Property(e => e.ParentFolder).HasMaxLength(50);
            entity.Property(e => e.SyncAddendaId).HasColumnName("SyncAddendaID");
            entity.Property(e => e.SyncStatus).HasDefaultValue(1);

            entity.HasOne(d => d.Proj).WithMany(p => p.Addenda)
                .HasForeignKey(d => d.ProjId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_tblAddenda_tblProject");
        });

        modelBuilder.Entity<Address>(entity =>
        {
            entity.ToTable("Address");

            entity.Property(e => e.AddressId).HasColumnName("AddressID");
            entity.Property(e => e.Addr1)
                .HasMaxLength(255)
                .HasDefaultValue("");
            entity.Property(e => e.AddressName)
                .HasMaxLength(20)
                .HasDefaultValue("");
            entity.Property(e => e.BusinessEntityId).HasColumnName("BusinessEntityID");
            entity.Property(e => e.City)
                .HasMaxLength(50)
                .HasDefaultValue("");
            entity.Property(e => e.State)
                .HasMaxLength(50)
                .HasDefaultValue("");
            entity.Property(e => e.SyncAoid).HasColumnName("SyncAOID");
            entity.Property(e => e.SyncConId).HasColumnName("SyncConID");
            entity.Property(e => e.SyncMemId).HasColumnName("SyncMemID");
            entity.Property(e => e.Zip)
                .HasMaxLength(50)
                .HasDefaultValue("");

            entity.HasOne(d => d.BusinessEntity).WithMany(p => p.Addresses)
                .HasForeignKey(d => d.BusinessEntityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Address_BusinessEntity");
        });

        modelBuilder.Entity<BusinessEntity>(entity =>
        {
            entity.ToTable("BusinessEntity");

            entity.Property(e => e.BusinessEntityId).HasColumnName("BusinessEntityID");
            entity.Property(e => e.BusinessEntityEmail)
                .HasMaxLength(50)
                .HasDefaultValue("");
            entity.Property(e => e.BusinessEntityName)
                .HasMaxLength(50)
                .HasDefaultValue("");
            entity.Property(e => e.BusinessEntityPhone)
                .HasMaxLength(50)
                .HasDefaultValue("");
            entity.Property(e => e.OldAoId).HasColumnName("OldAoID");
            entity.Property(e => e.OldConId).HasColumnName("OldConID");
            entity.Property(e => e.OldMemId).HasColumnName("OldMemID");
            entity.Property(e => e.SyncAoid).HasColumnName("SyncAOID");
            entity.Property(e => e.SyncConId).HasColumnName("SyncConID");
            entity.Property(e => e.SyncMemId).HasColumnName("SyncMemID");
        });

        modelBuilder.Entity<CityCounty>(entity =>
        {
            entity.HasKey(e => e.CityCountyId).HasName("PK_tblCityCounty");

            entity.ToTable("CityCounty");

            entity.Property(e => e.CityCountyId).HasColumnName("CityCountyID");
            entity.Property(e => e.City).HasMaxLength(50);
            entity.Property(e => e.CountyId).HasColumnName("CountyID");
            entity.Property(e => e.SyncCitCouId).HasColumnName("SyncCitCouID");
        });

        modelBuilder.Entity<Contact>(entity =>
        {
            entity.ToTable("Contact");

            entity.Property(e => e.ContactId).HasColumnName("ContactID");
            entity.Property(e => e.BillEmail).HasMaxLength(50);
            entity.Property(e => e.BusinessEntityId).HasColumnName("BusinessEntityID");
            entity.Property(e => e.ContactAddress).HasMaxLength(50);
            entity.Property(e => e.ContactCity).HasMaxLength(50);
            entity.Property(e => e.ContactCounty).HasMaxLength(50);
            entity.Property(e => e.ContactEmail)
                .HasMaxLength(50)
                .HasDefaultValue("");
            entity.Property(e => e.ContactName)
                .HasMaxLength(50)
                .HasDefaultValue("");
            entity.Property(e => e.ContactPhone)
                .HasMaxLength(50)
                .HasDefaultValue("");
            entity.Property(e => e.ContactState).HasMaxLength(50);
            entity.Property(e => e.ContactTitle)
                .HasMaxLength(50)
                .HasDefaultValue("");
            entity.Property(e => e.ContactZip).HasMaxLength(50);
            entity.Property(e => e.Extension).HasMaxLength(50);
            entity.Property(e => e.MessageDt).HasColumnType("datetime");
            entity.Property(e => e.Password).HasMaxLength(128);
            entity.Property(e => e.SyncConId).HasColumnName("SyncConID");
            entity.Property(e => e.Uid)
                .HasMaxLength(256)
                .HasColumnName("UID");
            entity.Property(e => e.UserId).HasDefaultValueSql("(newid())");

            entity.HasOne(d => d.BusinessEntity).WithMany(p => p.Contacts)
                .HasForeignKey(d => d.BusinessEntityId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Contact_BusinessEntity");
        });

        modelBuilder.Entity<County>(entity =>
        {
            entity.HasKey(e => e.CountyId).HasName("PK_tblCounty");

            entity.ToTable("County");

            entity.Property(e => e.CountyId).HasColumnName("CountyID");
            entity.Property(e => e.County1)
                .HasMaxLength(50)
                .HasColumnName("County");
            entity.Property(e => e.State).HasMaxLength(2);
            entity.Property(e => e.SyncCouId).HasColumnName("SyncCouID");
        });

        modelBuilder.Entity<Entity>(entity =>
        {
            entity.HasKey(e => e.EntityId).HasName("PK_tblEntity");

            entity.ToTable("Entity");

            entity.Property(e => e.EntityId).HasColumnName("EntityID");
            entity.Property(e => e.ChkIssue).HasColumnName("chkIssue");
            entity.Property(e => e.CompType).HasDefaultValue(1);
            entity.Property(e => e.EnityName)
                .HasMaxLength(250)
                .IsUnicode(false);
            entity.Property(e => e.EntityType)
                .HasMaxLength(250)
                .IsUnicode(false);
            entity.Property(e => e.SyncProjAoid).HasColumnName("SyncProjAOId");
            entity.Property(e => e.SyncStatus).HasDefaultValue(1);
        });

        modelBuilder.Entity<EstCostDetail>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__tblEstCo__3214EC07C8AA5F4D");

            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.EstCostFrom)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.EstCostTo)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.RangeSign)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Member>(entity =>
        {
            entity.ToTable("Member");

            entity.Property(e => e.MemberId).HasColumnName("MemberID");
            entity.Property(e => e.AcceptedTermsDt).HasColumnType("datetime");
            entity.Property(e => e.ActualRenewalDate).HasColumnType("datetime");
            entity.Property(e => e.AddGraceDate).HasColumnType("datetime");
            entity.Property(e => e.AddPkgCost)
                .HasColumnType("money")
                .HasColumnName("AddPkg_Cost");
            entity.Property(e => e.ArchPkgCost)
                .HasColumnType("money")
                .HasColumnName("ArchPkg_Cost");
            entity.Property(e => e.BusinessEntityId).HasColumnName("BusinessEntityID");
            entity.Property(e => e.CalSort).HasMaxLength(50);
            entity.Property(e => e.Cod).HasColumnName("COD");
            entity.Property(e => e.CompanyPhone)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ConId).HasColumnName("Con_ID");
            entity.Property(e => e.CreatedBy).HasMaxLength(200);
            entity.Property(e => e.Credits).HasColumnName("credits");
            entity.Property(e => e.DailyEmail)
                .HasMaxLength(255)
                .HasColumnName("Daily_Email");
            entity.Property(e => e.Dba)
                .HasMaxLength(100)
                .HasColumnName("DBA");
            entity.Property(e => e.Dba2)
                .HasMaxLength(100)
                .HasColumnName("DBA2");
            entity.Property(e => e.Discipline).HasMaxLength(255);
            entity.Property(e => e.Discount).HasMaxLength(500);
            entity.Property(e => e.Div).HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(50);
            entity.Property(e => e.FavExp).HasColumnType("datetime");
            entity.Property(e => e.Fax).HasMaxLength(50);
            entity.Property(e => e.Fka)
                .HasMaxLength(100)
                .HasColumnName("FKA");
            entity.Property(e => e.Gcservices).HasColumnName("GCservices");
            entity.Property(e => e.HowdUhearAboutUs)
                .HasMaxLength(255)
                .HasColumnName("HowdUHearAboutUs");
            entity.Property(e => e.Html).HasColumnName("HTML");
            entity.Property(e => e.InsertDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.LastPayDate)
                .HasMaxLength(50)
                .HasDefaultValue("");
            entity.Property(e => e.MagCost)
                .HasColumnType("money")
                .HasColumnName("Mag_Cost");
            entity.Property(e => e.MailAddress).HasMaxLength(50);
            entity.Property(e => e.MailCity).HasMaxLength(50);
            entity.Property(e => e.MailState).HasMaxLength(2);
            entity.Property(e => e.MailZip).HasMaxLength(50);
            entity.Property(e => e.MemId).HasColumnName("MemID");
            entity.Property(e => e.MemberCost)
                .HasColumnType("money")
                .HasColumnName("Member_Cost");
            entity.Property(e => e.MinorityStatus).HasMaxLength(50);
            entity.Property(e => e.NameField).HasColumnType("decimal(18, 0)");
            entity.Property(e => e.Note).HasColumnType("ntext");
            entity.Property(e => e.OverdueAmt).HasColumnType("money");
            entity.Property(e => e.OverdueDt).HasColumnType("datetime");
            entity.Property(e => e.PaperlessBilling)
                .HasMaxLength(50)
                .HasColumnName("Paperless_billing");
            entity.Property(e => e.PayModeRef)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Pdfpkg).HasColumnName("PDFPkg");
            entity.Property(e => e.Phl).HasColumnName("PHL");
            entity.Property(e => e.RenewalDate).HasColumnType("datetime");
            entity.Property(e => e.ResourceAdd)
                .HasMaxLength(50)
                .HasColumnName("Resource_Add");
            entity.Property(e => e.ResourceColor)
                .HasMaxLength(50)
                .HasColumnName("Resource_Color");
            entity.Property(e => e.ResourceCost)
                .HasColumnType("money")
                .HasColumnName("Resource_cost");
            entity.Property(e => e.ResourceDate)
                .HasMaxLength(50)
                .HasColumnName("Resource_date");
            entity.Property(e => e.ResourceLogo)
                .HasMaxLength(50)
                .HasColumnName("Resource_Logo");
            entity.Property(e => e.ResourceStandard)
                .HasMaxLength(50)
                .HasColumnName("Resource_Standard");
            entity.Property(e => e.SuspendedDt).HasMaxLength(50);
            entity.Property(e => e.SyncMemId).HasColumnName("SyncMemID");
            entity.Property(e => e.Term).HasMaxLength(50);
            entity.Property(e => e.TmStamp)
                .IsRowVersion()
                .IsConcurrencyToken()
                .HasColumnName("tmStamp");
            entity.Property(e => e.WebAdCost)
                .HasColumnType("money")
                .HasColumnName("WebAd_cost");
            entity.Property(e => e.WebAdDate)
                .HasMaxLength(50)
                .HasColumnName("WebAd_date");
        });

        modelBuilder.Entity<PreBidInfo>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__tblPreBi__3214EC07F2D28E24");

            entity.ToTable("PreBidInfo");

            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.Property(e => e.Location)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.PreBidDate).HasColumnType("datetime");
            entity.Property(e => e.PreBidTime)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.Pst)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("PST");
        });

        modelBuilder.Entity<ProjCounty>(entity =>
        {
            entity.HasKey(e => e.ProjCountyId).HasName("PK_tblProjCounty");

            entity.ToTable("ProjCounty");

            entity.Property(e => e.ProjCountyId).HasColumnName("ProjCountyID");
            entity.Property(e => e.CountyId).HasColumnName("CountyID");
            entity.Property(e => e.ProjId).HasColumnName("ProjID");
            entity.Property(e => e.SyncProCouId).HasColumnName("SyncProCouID");

            entity.HasOne(d => d.Proj).WithMany(p => p.ProjCounties)
                .HasForeignKey(d => d.ProjId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_tblProjCounty_tblProject");
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.ProjId).HasName("PK_tblProject");

            entity.ToTable("Project", tb =>
            {
                tb.HasTrigger("trg_generate_projnumber");
            });

            entity.HasIndex(e => e.LocState, "LocState");

            entity.HasIndex(e => e.ProjNumber, "NOC_INDX_tblProject_ProjNumber");

            entity.HasIndex(e => e.BidDt, "PK_tblProjectBidDt");

            entity.HasIndex(e => e.PlanNo, "PK_tblProjectPlanNo");

            entity.HasIndex(e => e.Title, "PK_tblProjectTitle");

            entity.HasIndex(e => new { e.BidDt3, e.BidDt, e.BidDt2, e.BidDt4, e.PlanNo }, "_dta_index_tblProject_7_772197801__K101_K9_K99_K103_K15_1_2_5_17_26_39_43_49_64");

            entity.HasIndex(e => e.BidDt4, "_dta_index_tblProject_7_772197801__K103");

            entity.HasIndex(e => e.BidDt2, "_dta_index_tblProject_7_772197801__K99");

            entity.HasIndex(e => new { e.BidDt, e.ProjId, e.PlanNo }, "_dta_index_tblProject_7_772197801__K9_K1_K15_2_26_68_84_85_86_87");

            entity.Property(e => e.AddendaNote).HasMaxLength(2000);
            entity.Property(e => e.ArrivalDt).HasColumnType("smalldatetime");
            entity.Property(e => e.BackProjNumber)
                .HasMaxLength(50)
                .HasDefaultValueSql("((0))");
            entity.Property(e => e.BendPc)
                .HasDefaultValue(false)
                .HasColumnName("BendPC");
            entity.Property(e => e.BidBond).HasMaxLength(20);
            entity.Property(e => e.BidDt).HasColumnType("datetime");
            entity.Property(e => e.BidDt2).HasColumnType("datetime");
            entity.Property(e => e.BidDt3).HasColumnType("datetime");
            entity.Property(e => e.BidDt4).HasColumnType("datetime");
            entity.Property(e => e.BidDt5).HasColumnType("datetime");
            entity.Property(e => e.BidPkg).HasDefaultValue(false);
            entity.Property(e => e.Brnote)
                .HasMaxLength(180)
                .HasColumnName("BRNote");
            entity.Property(e => e.BrresultsFrom)
                .HasMaxLength(180)
                .HasColumnName("BRResultsFrom");
            entity.Property(e => e.BuildSolrIndex).HasDefaultValue(true);
            entity.Property(e => e.CallBack).HasDefaultValue(false);
            entity.Property(e => e.CheckSentDt).HasColumnType("smalldatetime");
            entity.Property(e => e.CompleteDt).HasMaxLength(150);
            entity.Property(e => e.Contact)
                .HasMaxLength(50)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.CountyId)
                .HasDefaultValue(0)
                .HasColumnName("CountyID");
            entity.Property(e => e.CreatedBy)
                .HasMaxLength(150)
                .HasColumnName("createdBy");
            entity.Property(e => e.CreatedDate)
                .HasColumnType("datetime")
                .HasColumnName("createdDate");
            entity.Property(e => e.Deposit).HasColumnType("money");
            entity.Property(e => e.Dfnote)
                .HasColumnType("ntext")
                .HasColumnName("DFNote");
            entity.Property(e => e.DiPath).HasMaxLength(100);
            entity.Property(e => e.DirtId)
                .HasDefaultValue(false)
                .HasColumnName("DirtID");
            entity.Property(e => e.DrawingPath).HasMaxLength(100);
            entity.Property(e => e.DupArDt).HasColumnType("smalldatetime");
            entity.Property(e => e.DupTitle).HasMaxLength(20);
            entity.Property(e => e.DwChk).HasDefaultValue(false);
            entity.Property(e => e.EstCost).HasMaxLength(70);
            entity.Property(e => e.EstCost2).HasMaxLength(70);
            entity.Property(e => e.EstCost3).HasMaxLength(70);
            entity.Property(e => e.EstCost4).HasMaxLength(70);
            entity.Property(e => e.EstCost5).HasMaxLength(150);
            entity.Property(e => e.ExtendedDt).HasColumnType("datetime");
            entity.Property(e => e.FutureWork).HasDefaultValue(false);
            entity.Property(e => e.Hold).HasDefaultValue(false);
            entity.Property(e => e.ImportDt).HasColumnType("smalldatetime");
            entity.Property(e => e.IndexPdffiles)
                .HasDefaultValue(true)
                .HasColumnName("IndexPDFFiles");
            entity.Property(e => e.InternalNote).HasColumnType("ntext");
            entity.Property(e => e.InternetDownload).HasDefaultValue(false);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IssuingOffice).HasMaxLength(80);
            entity.Property(e => e.LastBidDt).HasMaxLength(150);
            entity.Property(e => e.Latitude).HasDefaultValue(0.0);
            entity.Property(e => e.LocAddr1).HasMaxLength(150);
            entity.Property(e => e.LocAddr2).HasMaxLength(150);
            entity.Property(e => e.LocCity).HasMaxLength(50);
            entity.Property(e => e.LocCity2).HasMaxLength(50);
            entity.Property(e => e.LocCity3).HasMaxLength(50);
            entity.Property(e => e.LocState).HasMaxLength(2);
            entity.Property(e => e.LocState2).HasMaxLength(2);
            entity.Property(e => e.LocState3).HasMaxLength(2);
            entity.Property(e => e.LocZip).HasMaxLength(10);
            entity.Property(e => e.Longitude).HasDefaultValue(0.0);
            entity.Property(e => e.MachineIp)
                .HasMaxLength(250)
                .HasColumnName("machineIP");
            entity.Property(e => e.Mandatory).HasDefaultValue(false);
            entity.Property(e => e.Mandatory2).HasDefaultValue(false);
            entity.Property(e => e.MaxViewPath).HasMaxLength(200);
            entity.Property(e => e.MemberId).HasColumnName("memberId");
            entity.Property(e => e.NoPrint).HasDefaultValue(false);
            entity.Property(e => e.NoSpecs).HasDefaultValue(false);
            entity.Property(e => e.NonRefundAmt).HasColumnType("money");
            entity.Property(e => e.OnlineNote).HasMaxLength(80);
            entity.Property(e => e.Phldone)
                .HasDefaultValue(false)
                .HasColumnName("PHLdone");
            entity.Property(e => e.Phlnote)
                .HasMaxLength(150)
                .HasColumnName("PHLnote");
            entity.Property(e => e.Phltimestamp)
                .HasColumnType("datetime")
                .HasColumnName("PHLtimestamp");
            entity.Property(e => e.PhlwebLink)
                .HasMaxLength(150)
                .HasColumnName("PHLwebLink");
            entity.Property(e => e.PreBidDt).HasColumnType("datetime");
            entity.Property(e => e.PreBidDt2).HasColumnType("datetime");
            entity.Property(e => e.PreBidDt3).HasColumnType("datetime");
            entity.Property(e => e.PreBidDt4).HasColumnType("datetime");
            entity.Property(e => e.PreBidDt5).HasColumnType("datetime");
            entity.Property(e => e.PreBidLoc).HasMaxLength(150);
            entity.Property(e => e.PreBidLoc2).HasMaxLength(150);
            entity.Property(e => e.PreBidLoc3).HasMaxLength(150);
            entity.Property(e => e.PreBidLoc4).HasMaxLength(150);
            entity.Property(e => e.PreBidLoc5).HasMaxLength(150);
            entity.Property(e => e.PrebidAnd)
                .HasDefaultValue(false)
                .HasColumnName("PrebidAND");
            entity.Property(e => e.PrebidNote).HasMaxLength(2000);
            entity.Property(e => e.PrebidOr)
                .HasDefaultValue(false)
                .HasColumnName("PrebidOR");
            entity.Property(e => e.PrevailingWage).HasDefaultValue(false);
            entity.Property(e => e.ProjNote).HasColumnType("ntext");
            entity.Property(e => e.ProjNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ProjScope).HasMaxLength(250);
            entity.Property(e => e.ProjTimeStamp)
                .IsRowVersion()
                .IsConcurrencyToken();
            entity.Property(e => e.Publish).HasDefaultValue(false);
            entity.Property(e => e.PublishedFrom).HasMaxLength(30);
            entity.Property(e => e.PublishedFromDt).HasColumnType("smalldatetime");
            entity.Property(e => e.Recycle).HasDefaultValue(false);
            entity.Property(e => e.RefundAmt).HasColumnType("money");
            entity.Property(e => e.RegionId).HasColumnName("RegionID");
            entity.Property(e => e.RenChk).HasDefaultValue(false);
            entity.Property(e => e.ResultDt).HasColumnType("smalldatetime");
            entity.Property(e => e.S11x17).HasDefaultValue(false);
            entity.Property(e => e.S18x24).HasDefaultValue(false);
            entity.Property(e => e.S24x36).HasDefaultValue(false);
            entity.Property(e => e.S30x42).HasDefaultValue(false);
            entity.Property(e => e.S36x48)
                .HasDefaultValue(false)
                .HasColumnName("S36X48");
            entity.Property(e => e.ShipCheck).HasColumnType("money");
            entity.Property(e => e.ShowBr)
                .HasDefaultValue(false)
                .HasColumnName("ShowBR");
            entity.Property(e => e.ShowOnWeb).HasDefaultValue(false);
            entity.Property(e => e.ShowToAll).HasDefaultValue(false);
            entity.Property(e => e.SolrIndexDt).HasColumnType("datetime");
            entity.Property(e => e.SolrIndexPdfdt)
                .HasColumnType("datetime")
                .HasColumnName("SolrIndexPDFDt");
            entity.Property(e => e.SpcChk).HasDefaultValue(false);
            entity.Property(e => e.SpecPath).HasMaxLength(100);
            entity.Property(e => e.SpecsOnPlans).HasDefaultValue(false);
            entity.Property(e => e.Story).HasColumnType("ntext");
            entity.Property(e => e.StoryUnf)
                .HasColumnType("ntext")
                .HasColumnName("StoryUNF");
            entity.Property(e => e.StrAddenda)
                .HasMaxLength(50)
                .HasDefaultValue("")
                .HasColumnName("strAddenda");
            entity.Property(e => e.StrBidDt)
                .HasMaxLength(30)
                .HasColumnName("strBidDt");
            entity.Property(e => e.StrBidDt2)
                .HasMaxLength(30)
                .HasColumnName("strBidDt2");
            entity.Property(e => e.StrBidDt3)
                .HasMaxLength(30)
                .HasColumnName("strBidDt3");
            entity.Property(e => e.StrBidDt4)
                .HasMaxLength(30)
                .HasColumnName("strBidDt4");
            entity.Property(e => e.StrBidDt5)
                .HasMaxLength(30)
                .HasColumnName("strBidDt5");
            entity.Property(e => e.StrPreBidDt)
                .HasMaxLength(30)
                .HasColumnName("strPreBidDt");
            entity.Property(e => e.StrPreBidDt2)
                .HasMaxLength(30)
                .HasColumnName("strPreBidDt2");
            entity.Property(e => e.StrPreBidDt3)
                .HasMaxLength(30)
                .HasColumnName("strPreBidDt3");
            entity.Property(e => e.StrPreBidDt4)
                .HasMaxLength(30)
                .HasColumnName("strPreBidDt4");
            entity.Property(e => e.StrPreBidDt5)
                .HasMaxLength(30)
                .HasColumnName("strPreBidDt5");
            entity.Property(e => e.SubApprov).HasMaxLength(50);
            entity.Property(e => e.SyncProId).HasColumnName("SyncProID");
            entity.Property(e => e.Title).HasMaxLength(255);
            entity.Property(e => e.TopChk)
                .HasDefaultValue(false)
                .HasComment("used in Maxviewprep to mark G3 prepped");
            entity.Property(e => e.Uc)
                .HasDefaultValue(false)
                .HasColumnName("UC");
            entity.Property(e => e.Ucpublic)
                .HasDefaultValue(false)
                .HasColumnName("UCPublic");
            entity.Property(e => e.Ucpwd)
                .HasMaxLength(50)
                .HasColumnName("UCPWD");
            entity.Property(e => e.Ucpwd2)
                .HasMaxLength(50)
                .HasColumnName("UCPWD2");
            entity.Property(e => e.Undecided)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.UnderCounter).HasDefaultValue(false);
            entity.Property(e => e.UpdatedBy)
                .HasMaxLength(150)
                .HasColumnName("updatedBy");
            entity.Property(e => e.UpdatedDate)
                .HasColumnType("datetime")
                .HasColumnName("updatedDate");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}