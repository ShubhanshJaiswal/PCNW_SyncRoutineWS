using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PCNW.Models;
using Polly;
using Serilog;
using SyncRoutineWS.OCPCModel;
using SyncRoutineWS.PCNWModel;
using System.Data;
using System.IO;
using System.Runtime.Intrinsics.X86;
using System.Text.RegularExpressions;

namespace SyncRoutineWS.Controllers;

public class SyncController
{
    private static OCPCProjectDBContext? _OCOCContext;
    private static PCNWProjectDBContext? _PCNWContext;

    private readonly IConfiguration _configuration;
    private readonly ILogger<SyncController> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private UserManager<IdentityUser>? _userManager;
    private readonly string _fileUploadPath; 
    private readonly string _liveProjectsRoot;
    private readonly int _copyDegreeOfParallelism = 4;

    public SyncController(IServiceScopeFactory scopeFactory, ILogger<SyncController> logger, OCPCProjectDBContext OCPCcont1, PCNWProjectDBContext PCNWcont2, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _OCOCContext = OCPCcont1;
        _PCNWContext = PCNWcont2;
        _scopeFactory = scopeFactory;
        _fileUploadPath = _configuration.GetSection("AppSettings")["FileUploadPath"] ?? string.Empty;
        _liveProjectsRoot = _configuration.GetSection("AppSettings")["LiveProjectsRoot"] ?? string.Empty;
    }

    public async Task SyncDatabases()
    {
        Log.Information("Sync Started.");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            _userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

            var baseDirectory = _fileUploadPath;
            if (!Directory.Exists(baseDirectory))
            {
                _logger.LogError("base directory not found: {basedirectory}", baseDirectory);
                throw new DirectoryNotFoundException($"base directory not found: {baseDirectory}");
            }
            _logger.LogInformation($"Application started at {DateTime.Now}", DateTime.Now);

            #region SYNC FROM OCPCLive - PCNWTest

            //Member Sync code

            //var businessEntityEmails = _PCNWContext.BusinessEntities
            //                    .Select(be => be.BusinessEntityEmail)
            //                    .ToHashSet();
            //var memids = _PCNWContext.BusinessEntities.Select(m => m.SyncMemId).ToHashSet(); ;

            //var tblOCPCMember = (from mem in _OCOCContext.TblMembers
            //                     where (!(memids.Contains(mem.Id)) && mem.Id == 10987)
            //                     select mem).OrderBy(m => m.Id)
            //                     .AsNoTracking()
            //                     .ToList();

            //var memberids = tblOCPCMember.Select(m => m.Id).ToList();

            //var tblOCPCContact = _OCOCContext.TblContacts
            //    .AsNoTracking()
            //    .ToList();

            //tblOCPCContact = tblOCPCContact.Where(m => memberids.Contains(m.Id)).ToList();
            //ProcessMemberFunctionality(tblOCPCMember, tblOCPCContact);

            // project sync code

            var syncedProjectsIds = _PCNWContext.Projects
                .Where(p => p.SyncProId != null)
                .AsNoTracking()
                .Select(p => p.SyncProId!.Value)
                .ToHashSet();

            var allprojectrecords = _OCOCContext.TblProjects.AsNoTracking().ToHashSet();

            var tblProjects = allprojectrecords
                .Where(proj =>
                    (proj.SyncStatus == 1 || !syncedProjectsIds.Contains(proj.ProjId)) &&
                    proj.Publish == true)
                .ToList();



            //var tblProjects = _OCOCContext.TblProjects.Where(m => m.ProjId == 249587).ToList();


            var tblProjectIds = tblProjects.Select(m => m.ProjId);
            var allcountiesrecords = _OCOCContext.TblProjCounties.AsNoTracking().ToList();
            var tblProjCounty = tblProjects.Count != 0
                ? [.. allcountiesrecords
                        .Where(projCounty => tblProjectIds.Contains(projCounty.ProjId))]
                : new List<TblProjCounty>();

            ProcessProjectFunctionality(tblProjects, tblProjCounty);

            var updateProjectsIds = _OCOCContext.TblProjFieldChngs
                .Where(proj => proj.FieldName == "SyncProject" && proj.SyncDt == null)
                .AsNoTracking()
                .Select(m => m.ProjId).ToHashSet();
            var updateProjects = _OCOCContext.TblProjects.Where(m => updateProjectsIds.Contains(m.ProjId) && m.Publish.HasValue && m.Publish.Value).ToList();

            var tblupdateProjCounty = updateProjects.Count != 0
                ? [.. _OCOCContext.TblProjCounties
                        .Where(projCounty => updateProjectsIds.Contains(projCounty.ProjId))
                        .AsNoTracking()]
                : new List<TblProjCounty>();
            UpdateProjectFunctionality(updateProjects, tblupdateProjCounty);


            //creating the directory for projnumbers(past 2 months)
            var pastMonthDate = DateTime.Now.AddMonths(-1);
            int pastMonth = pastMonthDate.Month;
            int pastYear = pastMonthDate.Year;

            var ProjNumbers = _PCNWContext.Projects.Where(m => m.ArrivalDt.HasValue && m.ArrivalDt.Value.Month >= pastMonth && m.ArrivalDt.Value.Year == pastYear).AsEnumerable().Select(m => m.ProjNumber);

            UpdateDirectory(ProjNumbers);

            var syncedProjIds = _PCNWContext.Projects
    .Where(p => p.SyncProId != null && p.ArrivalDt > DateTime.Now.AddMonths(-30)).OrderByDescending(m => m.ProjId)
    .Select(p => p.SyncProId.Value)
    .ToList();

            // Step 2: Identify mismatched counties between OCPC and PCNW
            var mismatchedProjectIds = new List<long>();

            foreach (var projId in syncedProjIds)
            {
                var ocpcCountyIds = _OCOCContext.TblProjCounties
                    .Where(c => c.ProjId == projId)
                    .Select(c => c.CountyId)
                    .OrderBy(c => c)
                    .ToList();

                var pcnwCountyIds = _PCNWContext.ProjCounties
                    .Where(c => c.ProjId == _PCNWContext.Projects
                                                    .Where(p => p.SyncProId == projId)
                                                    .Select(p => p.ProjId)
                                                    .FirstOrDefault())
                    .Select(c => c.CountyId)
                    .OrderBy(c => c)
                    .ToList();

                bool isMismatch = !ocpcCountyIds.SequenceEqual(pcnwCountyIds);

                if (isMismatch)
                {
                    _logger.LogInformation("Found ProjID: {0} .", projId);
                    mismatchedProjectIds.Add(projId);

                }
            }

            // Step 3: Fetch the mismatched project records and counties from OCPC
            var ids = mismatchedProjectIds.ToArray(); // force simple SQL generation

            var mismatchedProjects = _OCOCContext.TblProjects
    .AsEnumerable() // switch to LINQ to Objects
    .Where(p => mismatchedProjectIds.Contains(p.ProjId))
    .ToList();

            var mismatchedProjCounties = _OCOCContext.TblProjCounties
    .AsEnumerable() // switch to LINQ to Objects
                .Where(c => ids.Contains(c.ProjId))
                .ToList();
            _logger.LogInformation("Found {0} mismatched projects.", mismatchedProjectIds.Count);
            // Step 4: Call your existing function
            UpdateProjectFunctionality(mismatchedProjects, mismatchedProjCounties);

            // Query Arch Owners

            //var tblArch = _OCOCContext.TblArchOwners
            //    .Where(arch => (arch.SyncStatus == 1 && !businessEntityEmails.Contains(arch.Email)) || arch.SyncStatus == 2)
            //    .AsNoTracking()
            //    .ToList();

            // var tblProArc = _OCOCContext.TblProjAos
            //     .Where(po => po.SyncStatus == 1 || po.SyncStatus == 2)
            //     .AsNoTracking()
            //     .ToList();

            // ProcessArchOwnerFunctionality(tblArch, tblProArc);

            //var tblCont = _OCOCContext.TblContractors
            //    .Where(cont => (cont.SyncStatus == 1 && !businessEntityEmails.Contains(cont.Email)) || cont.SyncStatus == 2)
            //    .AsNoTracking()
            //    .ToList();

            //var tblProCon = _OCOCContext.TblProjCons
            //    .Where(pc => pc.SyncStatus == 1 || pc.SyncStatus == 2)
            //    .AsNoTracking()
            //    .ToList();

            //ProcessContractorFunctionality(tblCont, tblProCon);

            //var tblAddenda = _OCOCContext.TblAddenda
            //    .Where(adden => adden.SyncStatus == 1 || adden.SyncStatus == 2)
            //    .AsNoTracking()
            //    .ToList();

            //ProcessAddendaFunctionality(tblAddenda);
            await CleanupStorageByPolicyAsync();
            await SyncFilesFromLiveToBetaAsync();
            #endregion SYNC FROM OCPCLive - PCNWTest

            _logger.LogInformation("Sync from OCPCProjectDB to PCNWProjectDB is completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while syncing database.");
        }

        //Log Entry and Success Message
        _logger.LogInformation("Sync Completed Successfully..");
    }

    public void UpdateDirectory(IEnumerable<string> projectNumbers)
    {
        try
        {
            if (projectNumbers is not null)
            {
                foreach (var item in projectNumbers)
                {
                    try
                    {
                        string basePath = _fileUploadPath;
                        string projectPath = Path.Combine(basePath, string.Concat("20", item.AsSpan(0, 2)), item.Substring(2, 2), item);
                        if (string.IsNullOrEmpty(projectPath))
                        {
                            continue;
                        }
                        if (!Directory.Exists(projectPath))
                        {
                            _logger.LogInformation($"Starting Directory Creation for Project Number {item}.");

                            LocalCreateFolder(Path.Combine(projectPath, "Uploads"));
                            LocalCreateFolder(Path.Combine(projectPath, "Addenda"));
                            LocalCreateFolder(Path.Combine(projectPath, "Bid Results"));
                            LocalCreateFolder(Path.Combine(projectPath, "PHL"));
                            LocalCreateFolder(Path.Combine(projectPath, "Plans"));
                            LocalCreateFolder(Path.Combine(projectPath, "Specs"));

                            _logger.LogInformation($"Directory Created for Project Number {item}.");
                        }
                    }
                    catch (Exception)
                    {
                        _logger.LogError($"An error occurred while Directory Creation of Project Number {item}.");
                        continue;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while Directory Creation.");
            throw;
        }
    }
    public string? GetProjectPath(Project propProject)
    {
        string basePath = _fileUploadPath;

        string year = string.Empty;
        string month = string.Empty;
        string projNumber = string.Empty;
        if (string.IsNullOrEmpty(propProject.ProjNumber))
        {

            var project = _PCNWContext.Projects
                .Where(p => p.ProjId == propProject.ProjId)
                .Select(p => new { p.ProjNumber, p.ArrivalDt })
                .FirstOrDefault();

            if (project != null)
            {
                if (!string.IsNullOrEmpty(project.ProjNumber))
                {
                    year = string.Concat("20", project.ProjNumber.AsSpan(0, 2));
                    month = project.ProjNumber.Substring(2, 2);
                    projNumber = project.ProjNumber;
                }
                else if (project.ArrivalDt != null)
                {
                    string projYearMonth = project.ArrivalDt.Value.ToString("yyMM");

                    int projCount = _PCNWContext.Projects
                        .Where(p => p.ArrivalDt.HasValue && p.ArrivalDt.Value.ToString("yyMM") == projYearMonth)
                        .Count();

                    projNumber = projYearMonth + projCount.ToString("D4");

                    var projToUpdate = _PCNWContext.Projects.Find(propProject.ProjId);
                    if (projToUpdate != null)
                    {
                        projToUpdate.ProjNumber = projNumber;
                        _PCNWContext.SaveChanges();
                    }

                    year = string.Concat("20", projNumber.AsSpan(0, 2));
                    month = projNumber.Substring(2, 2);
                }

                string projectPath = Path.Combine(basePath, year, month, projNumber);
                return projectPath;
            }
            else if (propProject.ArrivalDt != null)
            {
                string projYearMonth = propProject.ArrivalDt.Value.ToString("yyMM");

                int projCount = _PCNWContext.Projects
                    .Where(p => p.ArrivalDt.HasValue && p.ArrivalDt.Value.ToString("yyMM") == projYearMonth)
                    .Count() + 1;

                projNumber = projYearMonth + projCount.ToString("D4");

                var projToUpdate = _PCNWContext.Projects.Find(propProject.ProjId);
                if (projToUpdate != null)
                {
                    projToUpdate.ProjNumber = projNumber;
                    _PCNWContext.SaveChanges();
                }

                year = string.Concat("20", projNumber.AsSpan(0, 2));
                month = projNumber.Substring(2, 2);

                string projectPath = Path.Combine(basePath, year, month, projNumber);
                return projectPath;
            }
        }
        else
        {
            year = string.Concat("20", propProject.ProjNumber.AsSpan(0, 2));
            month = propProject.ProjNumber.Substring(2, 2);
            projNumber = propProject.ProjNumber;

            string projectPath = Path.Combine(basePath, year, month, projNumber);
            return projectPath;
        }

        return null;
    }

    public string FetchProjNumber(DateTime? ArrivalDt)
    {
        try
        {
            if (!ArrivalDt.HasValue)
                throw new ArgumentException("Arrival date cannot be null");

            string projYearMonth = ArrivalDt.Value.ToString("yyMM");

            //// Find the maximum sequence number for the given month-year
            //int projYear = 2000 + int.Parse(projYearMonth.Substring(0, 2));
            //int projMonth = int.Parse(projYearMonth.Substring(2, 2));

            //int maxProjSequence = _PCNWContext.Projects
            //    .Where(p => p.ArrivalDt.HasValue &&
            //                p.ArrivalDt.Value.Year == projYear &&
            //                p.ArrivalDt.Value.Month == projMonth &&
            //                p.ProjNumber.StartsWith(projYearMonth))
            //    .AsEnumerable()
            //    .Select(p => (int?)int.Parse(p.ProjNumber.Substring(4)))
            //    .Max() ?? 0;


            //int projSequence = maxProjSequence + 1;
            int projSequence = 1;
            string newProjNumber;
            int maxRetries = 9999;
            int retry = 0;

            do
            {
                newProjNumber = projYearMonth + projSequence.ToString("D4");

                // Check if the generated ProjNumber already exists
                bool exists = _PCNWContext.Projects.Any(p => p.ProjNumber == newProjNumber);

                if (!exists)
                    return newProjNumber;

                projSequence++;
                retry++;

            } while (retry <= maxRetries);

            throw new Exception("Failed to generate unique ProjNumber after multiple attempts.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Exception occurred while generating the ProjNumber");
            throw;
        }
    }
    private void CreateProjectDirectory(Project propProject)
    {
        if (propProject != null)
        {
            string projectPath = GetProjectPath(propProject);
            if (string.IsNullOrEmpty(projectPath))
            {
                return;
            }

            LocalCreateFolder(Path.Combine(projectPath, "Uploads"));
            LocalCreateFolder(Path.Combine(projectPath, "Addenda"));
            LocalCreateFolder(Path.Combine(projectPath, "Bid Results"));
            LocalCreateFolder(Path.Combine(projectPath, "PHL"));
            LocalCreateFolder(Path.Combine(projectPath, "Plans"));
            LocalCreateFolder(Path.Combine(projectPath, "Specs"));
        }
    }

    private void LocalCreateFolder(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Exception occurred while creating Directory path: {path}");
            throw new DirectoryNotFoundException($"Base directory not found: {path}");
        }
    }

    private void ProcessAddendaFunctionality(List<TblAddendum> tblAddenda)
    {
        int SuccessAddendaProcess = 0, FailAddendaProcess = 0;

        // Log the count of Addenda items
        Log.Information("tblAddenda ITEMS COUNT: {AddendaCount}", tblAddenda?.Count ?? 0);

        if (tblAddenda != null && tblAddenda.Count > 0)
        {
            foreach (TblAddendum adden in tblAddenda)
            {
                try
                {
                    Addendum propAddenda;

                    Log.Information("Processing addenda with ID: {AddendaId} and SyncStatus: {SyncStatus}", adden.AddendaId, adden.SyncStatus);

                    // If SyncStatus is 1, add new addendum
                    if (adden.SyncStatus == 1)
                    {
                        var propProj = (from c in _PCNWContext.Projects where c.SyncProId == adden.ProjId select c).FirstOrDefault();

                        if (propProj != null)
                        {
                            propAddenda = new()
                            {
                                AddendaNo = adden.AddendaNo,
                                MoreInfo = adden.MoreInfo,
                                ProjId = propProj.SyncProId,
                                InsertDt = adden.InsertDt,
                                MvwebPath = adden.MvwebPath,
                                IssueDt = adden.IssueDt,
                                PageCnt = adden.PageCnt,
                                NewBd = adden.NewBd,
                                SyncStatus = 0,
                                SyncAddendaId = adden.AddendaId
                            };

                            _ = _PCNWContext.Addenda.Add(propAddenda);
                            Log.Information("Added new addenda with ID: {AddendaId} for project ID: {ProjId}", adden.AddendaId, propProj.SyncProId);
                        }
                        else
                        {
                            Log.Warning("No project found for addenda ID: {AddendaId}", adden.AddendaId);
                        }
                    }
                    // If SyncStatus is 2, update existing addendum
                    else if (adden.SyncStatus == 2)
                    {
                        propAddenda = (from adddenda in _PCNWContext.Addenda where adddenda.SyncAddendaId == adden.AddendaId select adddenda).FirstOrDefault();

                        if (propAddenda != null)
                        {
                            propAddenda.AddendaNo = adden.AddendaNo;
                            propAddenda.MoreInfo = adden.MoreInfo;
                            propAddenda.InsertDt = adden.InsertDt;
                            propAddenda.MvwebPath = adden.MvwebPath;
                            propAddenda.IssueDt = adden.IssueDt;
                            propAddenda.PageCnt = adden.PageCnt;
                            propAddenda.NewBd = adden.NewBd;

                            _PCNWContext.Entry(propAddenda).Property(p => p.SyncStatus).IsModified = true;
                            Log.Information("Updated addenda with ID: {AddendaId}", adden.AddendaId);
                        }
                        else
                        {
                            Log.Warning("No addenda found to update for addenda ID: {AddendaId}", adden.AddendaId);
                        }
                    }

                    _ = _PCNWContext.SaveChanges();

                    _OCOCContext.Database.ExecuteSqlRaw("DISABLE TRIGGER [dbo].[tr_Addenda_IU] ON [dbo].[tblAddenda];");

                    adden.SyncStatus = 3;
                    _OCOCContext.Entry(adden).Property(p => p.SyncStatus).IsModified = true;

                    _OCOCContext.SaveChanges();

                    // Re-enable the trigger
                    _OCOCContext.Database.ExecuteSqlRaw("ENABLE TRIGGER [dbo].[tr_Addenda_IU] ON [dbo].[tblAddenda];");

                    SuccessAddendaProcess++;
                    Log.Information("Successfully processed addenda with ID: {AddendaId}", adden.AddendaId);
                }
                catch (Exception exAddenda)
                {
                    FailAddendaProcess++;
                    Log.Error(exAddenda, "Exception occurred while processing addenda ID: {AddendaId}", adden.AddendaId);
                }
            }

            // Log summary of processed addenda
            Log.Information("Finished processing addenda. Successful: {SuccessAddendaProcess}, Failed: {FailAddendaProcess}", SuccessAddendaProcess, FailAddendaProcess);
        }
        else
        {
            Log.Warning("No addenda found in tblAddenda to process.");
        }
    }

    private void ProcessContractorFunctionality(List<TblContractor> tblCont, List<TblProjCon> tblProCon)
    {
        int SuccessContractorProcess = 0, FailContractorProcess = 0, SuccessPCProcess = 0, FailPCProcess = 0;

        // Log the count of contractors and project contractors
        Log.Information("Contractor list count: {ContractorCount}", tblCont?.Count ?? 0);
        Log.Information("Project contractor list count: {ProjConCount}", tblProCon?.Count ?? 0);

        if (tblCont != null && tblCont.Count > 0)
        {
            foreach (TblContractor con in tblCont)
            {
                try
                {
                    int lastContractorBusinessEntityId = 0;
                    BusinessEntity propBussEnt;

                    Log.Information("Processing contractor with ID: {ContractorId} and SyncStatus: {SyncStatus}", con.Id, con.SyncStatus);

                    // Create new business entity if SyncStatus is 1
                    if (con.SyncStatus == 1)
                    {
                        propBussEnt = new()
                        {
                            BusinessEntityName = con.Name,
                            BusinessEntityEmail = con.Email,
                            BusinessEntityPhone = con.Phone,
                            IsMember = false,
                            IsContractor = true,
                            IsArchitect = false,
                            OldMemId = 0,
                            OldConId = con.Id,
                            OldAoId = 0,
                            SyncStatus = 0,
                            SyncConId = con.Id
                        };

                        _ = _PCNWContext.BusinessEntities.Add(propBussEnt);
                        _ = _PCNWContext.SaveChanges();

                        lastContractorBusinessEntityId = (from BId in _PCNWContext.BusinessEntities select BId.BusinessEntityId).Max();

                        Log.Information("Added new contractor entity with ID: {BusinessEntityId}", lastContractorBusinessEntityId);
                    }
                    // Update existing business entity if SyncStatus is 2
                    else if (con.SyncStatus == 2)
                    {
                        propBussEnt = (from be in _PCNWContext.BusinessEntities where be.BusinessEntityName == con.Name select be).FirstOrDefault();

                        if (propBussEnt != null)
                        {
                            propBussEnt.BusinessEntityName = con.Name;
                            propBussEnt.BusinessEntityEmail = con.Email;
                            propBussEnt.BusinessEntityPhone = con.Phone;

                            _PCNWContext.Entry(propBussEnt).Property(p => p.SyncStatus).IsModified = true;
                            _ = _PCNWContext.SaveChanges();

                            lastContractorBusinessEntityId = propBussEnt.BusinessEntityId;

                            Log.Information("Updated contractor entity with ID: {BusinessEntityId}", lastContractorBusinessEntityId);
                        }
                        else
                        {
                            Log.Warning("No contractor entity found to update for contractor ID: {ContractorId}", con.Id);
                        }
                    }

                    // Handle contractor address
                    Address propAdd;
                    if (con.SyncStatus == 1)
                    {
                        propAdd = new()
                        {
                            BusinessEntityId = lastContractorBusinessEntityId,
                            Addr1 = con.Addr1,
                            City = con.City,
                            State = con.State,
                            Zip = con.Zip,
                            SyncStatus = 0,
                            SyncConId = con.Id
                        };

                        _ = _PCNWContext.Addresses.Add(propAdd);
                        Log.Information("Added address for contractor ID: {ContractorId}", con.Id);
                    }
                    else if (con.SyncStatus == 2)
                    {
                        propAdd = (from addAO in _PCNWContext.Addresses where addAO.BusinessEntityId == lastContractorBusinessEntityId select addAO).FirstOrDefault();

                        if (propAdd != null)
                        {
                            propAdd.Addr1 = con.Addr1;
                            propAdd.City = con.City;
                            propAdd.State = con.State;
                            propAdd.Zip = con.Zip;

                            _PCNWContext.Entry(propAdd).Property(p => p.SyncStatus).IsModified = true;
                            Log.Information("Updated address for contractor ID: {ContractorId}", con.Id);
                        }
                        else
                        {
                            Log.Warning("No address found to update for contractor ID: {ContractorId}", con.Id);
                        }
                    }

                    _ = _PCNWContext.SaveChanges();

                    // Process project contractors associated with this contractor
                    List<TblProjCon> lstPrcON = (from filProjCon in tblProCon where filProjCon.ConId == con.Id select filProjCon).ToList();
                    if (lstPrcON.Count > 0)
                    {
                        SuccessPCProcess = 0;
                        FailPCProcess = 0;

                        Log.Information("Processing {ProjConCount} project contractors for contractor ID: {ContractorId}", lstPrcON.Count, con.Id);

                        foreach (TblProjCon tpc in lstPrcON)
                        {
                            try
                            {
                                Entity propEnty;

                                if (tpc.SyncStatus == 1)
                                {
                                    var propProj = (from c in _PCNWContext.Projects where c.SyncProId == tpc.ProjId select c).FirstOrDefault();

                                    if (propProj != null)
                                    {
                                        propEnty = new()
                                        {
                                            EnityName = con.Name,
                                            ProjId = propProj.ProjId,
                                            ProjNumber = Convert.ToInt32(propProj.ProjNumber),
                                            IsActive = propProj.IsActive,
                                            NameId = lastContractorBusinessEntityId,
                                            ChkIssue = (bool)tpc.IssuingOffice,
                                            CompType = 2,
                                            SyncStatus = 0,
                                            SyncProjConId = tpc.ProjConId
                                        };

                                        _ = _PCNWContext.Entities.Add(propEnty);
                                        Log.Information("Added project entity for project contractor ID: {ProjConId}", tpc.ProjConId);
                                    }
                                    else
                                    {
                                        Log.Warning("No project found for project contractor ID: {ProjConId}", tpc.ProjConId);
                                    }
                                }
                                else if (tpc.SyncStatus == 2)
                                {
                                    propEnty = (from ent in _PCNWContext.Entities where ent.SyncProjConId == tpc.ProjConId select ent).FirstOrDefault();

                                    if (propEnty != null)
                                    {
                                        propEnty.EnityName = con.Name;
                                        _PCNWContext.Entry(propEnty).Property(p => p.SyncStatus).IsModified = true;

                                        Log.Information("Updated project entity for project contractor ID: {ProjConId}", tpc.ProjConId);
                                    }
                                    else
                                    {
                                        Log.Warning("No entity found to update for project contractor ID: {ProjConId}", tpc.ProjConId);
                                    }
                                }

                                _ = _PCNWContext.SaveChanges();

                                tpc.SyncStatus = 3;
                                _OCOCContext.Entry(tpc).Property(p => p.SyncStatus).IsModified = true;
                                _ = _OCOCContext.SaveChanges();
                                SuccessPCProcess++;
                            }
                            catch (Exception exProjCon)
                            {
                                FailPCProcess++;
                                Log.Error(exProjCon, "Exception occurred while processing project contractor ID: {ProjConId}", tpc.ProjConId);
                            }
                        }

                        Log.Information("Successfully processed {SuccessPCProcess} project contractors and failed {FailPCProcess} for contractor ID: {ContractorId}", SuccessPCProcess, FailPCProcess, con.Id);
                    }
                    else
                    {
                        Log.Warning("No project contractors found for contractor ID: {ContractorId}", con.Id);
                    }

                    con.SyncStatus = 3;
                    _OCOCContext.Entry(con).Property(p => p.SyncStatus).IsModified = true;
                    _ = _OCOCContext.SaveChanges();
                    SuccessContractorProcess++;
                }
                catch (Exception exContractor)
                {
                    FailContractorProcess++;
                    Log.Error(exContractor, "Exception occurred while processing contractor ID: {ContractorId}", con.Id);
                }
            }

            Log.Information("Finished processing contractors. Success: {SuccessContractorProcess}, Failures: {FailContractorProcess}", SuccessContractorProcess, FailContractorProcess);
        }
        else
        {
            Log.Warning("No contractors found to process in tblContractor.");
        }
    }

    private void ProcessArchOwnerFunctionality(List<TblArchOwner> tblArchOwner, List<TblProjAo> tblProArcOwn)
    {
        int SuccessAOProcess = 0, FailAOProcess = 0, SuccessPAOProcess = 0, FailPAOProcess = 0;

        Log.Information("Starting ProcessArchOwnerFunctionality");
        Log.Information("tblArchOwner ITEMS COUNT: {Count}", tblArchOwner?.Count ?? 0);
        Log.Information("tblProjAO ITEMS COUNT: {Count}", tblProArcOwn?.Count ?? 0);

        if (tblArchOwner != null && tblArchOwner.Count > 0)
        {
            foreach (TblArchOwner archOw in tblArchOwner)
            {
                Log.Information("Processing ArchOwner ID: {ArchOwnerId}", archOw.Id);

                try
                {
                    int lastAOBusinessEntityId = 0;
                    BusinessEntity propBussEnt;

                    // Step 1: Handle new or updated BusinessEntity
                    if (archOw.SyncStatus == 1) // New Business Entity
                    {
                        Log.Information("Adding new BusinessEntity for ArchOwner ID: {ArchOwnerId}", archOw.Id);
                        propBussEnt = new BusinessEntity
                        {
                            BusinessEntityName = archOw.Name,
                            BusinessEntityEmail = archOw.Email,
                            BusinessEntityPhone = archOw.Phone,
                            IsMember = false,
                            IsContractor = false,
                            IsArchitect = true,
                            OldMemId = 0,
                            OldConId = 0,
                            OldAoId = archOw.Id,
                            SyncStatus = 0,
                            SyncAoid = archOw.Id
                        };
                        _PCNWContext.BusinessEntities.Add(propBussEnt);
                        _PCNWContext.SaveChanges();
                        Log.Information("BusinessEntity added with ID: {BusinessEntityId}", propBussEnt.BusinessEntityId);

                        lastAOBusinessEntityId = propBussEnt.BusinessEntityId;
                    }
                    else if (archOw.SyncStatus == 2) // Update Business Entity
                    {
                        Log.Information("Updating existing BusinessEntity for ArchOwner ID: {ArchOwnerId}", archOw.Id);
                        propBussEnt = _PCNWContext.BusinessEntities
                            .FirstOrDefault(be => be.BusinessEntityName == archOw.Name);

                        if (propBussEnt != null)
                        {
                            propBussEnt.BusinessEntityName = archOw.Name;
                            propBussEnt.BusinessEntityEmail = archOw.Email;
                            propBussEnt.BusinessEntityPhone = archOw.Phone;
                            _PCNWContext.Entry(propBussEnt).State = EntityState.Modified;
                            _PCNWContext.SaveChanges();
                            Log.Information("BusinessEntity updated with ID: {BusinessEntityId}", propBussEnt.BusinessEntityId);

                            lastAOBusinessEntityId = (int)propBussEnt.SyncAoid;
                        }
                        else
                        {
                            Log.Warning("BusinessEntity not found for ArchOwner ID: {ArchOwnerId}", archOw.Id);
                        }
                    }

                    // Step 2: Handle Address for BusinessEntity
                    Address propAdd;
                    if (archOw.SyncStatus == 1) // New Address
                    {
                        Log.Information("Adding new Address for BusinessEntity ID: {BusinessEntityId}", lastAOBusinessEntityId);
                        propAdd = new Address
                        {
                            BusinessEntityId = lastAOBusinessEntityId,
                            Addr1 = archOw.Addr1,
                            City = archOw.City,
                            State = archOw.State,
                            Zip = archOw.Zip,
                            SyncStatus = 0,
                            SyncAoid = archOw.Id
                        };
                        _PCNWContext.Addresses.Add(propAdd);
                    }
                    else if (archOw.SyncStatus == 2) // Update Address
                    {
                        Log.Information("Updating Address for BusinessEntity ID: {BusinessEntityId}", lastAOBusinessEntityId);
                        propAdd = _PCNWContext.Addresses
                            .FirstOrDefault(addAO => addAO.BusinessEntityId == lastAOBusinessEntityId);

                        if (propAdd != null)
                        {
                            propAdd.Addr1 = archOw.Addr1;
                            propAdd.City = archOw.City;
                            propAdd.State = archOw.State;
                            propAdd.Zip = archOw.Zip;
                            _PCNWContext.Entry(propAdd).State = EntityState.Modified;
                        }
                        else
                        {
                            Log.Warning("Address not found for BusinessEntity ID: {BusinessEntityId}", lastAOBusinessEntityId);
                        }
                    }
                    _PCNWContext.SaveChanges();
                    Log.Information("Address saved for BusinessEntity ID: {BusinessEntityId}", lastAOBusinessEntityId);

                    // Step 3: Process TblProjAo associated with the current ArchOwner
                    var lstpao = tblProArcOwn.Where(filtAO => filtAO.ArchOwnerId == archOw.Id).ToList();
                    if (lstpao.Count > 0)
                    {
                        Log.Information("Processing {Count} TblProjAo for ArchOwner ID: {ArchOwnerId}", lstpao.Count, archOw.Id);
                        SuccessPAOProcess = 0;
                        FailPAOProcess = 0;

                        foreach (TblProjAo ProjAO in lstpao)
                        {
                            try
                            {
                                Entity propEnty;

                                // Step 4: Handle new or updated Entity for ProjAo
                                if (ProjAO.SyncStatus == 1) // New Entity
                                {
                                    var propProj = _PCNWContext.Projects
                                        .FirstOrDefault(c => c.SyncProId == ProjAO.ProjId);

                                    if (propProj != null)
                                    {
                                        Log.Information("Adding new Entity for Project ID: {ProjId}", propProj.ProjId);
                                        propEnty = new Entity
                                        {
                                            EnityName = archOw.Name,
                                            ProjId = propProj.ProjId,
                                            ProjNumber = Convert.ToInt32(propProj.ProjNumber),
                                            IsActive = propProj.IsActive,
                                            NameId = lastAOBusinessEntityId,
                                            CompType = 3,
                                            SyncStatus = 0,
                                            SyncProjAoid = ProjAO.ArchOwnerId
                                        };
                                        _PCNWContext.Entities.Add(propEnty);
                                    }
                                    else
                                    {
                                        Log.Warning("No project found for ProjAo ID: {ProjAoId}", ProjAO.ArchOwnerId);
                                    }
                                }
                                else if (ProjAO.SyncStatus == 2) // Update Entity
                                {
                                    Log.Information("Updating Entity for ProjAo ID: {ProjAoId}", ProjAO.ArchOwnerId);
                                    propEnty = _PCNWContext.Entities
                                        .FirstOrDefault(ent => ent.SyncProjConId == ProjAO.ArchOwnerId);

                                    if (propEnty != null)
                                    {
                                        propEnty.EnityName = archOw.Name;
                                        _PCNWContext.Entry(propEnty).State = EntityState.Modified;
                                    }
                                    else
                                    {
                                        Log.Warning("Entity not found for ProjAo ID: {ProjAoId}", ProjAO.ArchOwnerId);
                                    }
                                }
                                _PCNWContext.SaveChanges();
                                Log.Information("Entity saved for ProjAo ID: {ProjAoId}", ProjAO.ArchOwnerId);

                                ProjAO.SyncStatus = 3;
                                _OCOCContext.Entry(ProjAO).State = EntityState.Modified;
                                _OCOCContext.SaveChanges();
                                Log.Information("TblProjAo updated with SyncStatus 3 for ProjAo ID: {ProjAoId}", ProjAO.ArchOwnerId);

                                SuccessPAOProcess++;
                            }
                            catch (Exception exProjAO)
                            {
                                Log.Error(exProjAO, "Exception occurred while processing TblProjAo for ProjAo ID: {ProjAoId}", ProjAO.ArchOwnerId);
                                FailPAOProcess++;
                            }
                        }

                        Log.Information("Successfully processed {SuccessCount} TblProjAo, failed to process {FailCount} for ArchOwner ID: {ArchOwnerId}", SuccessPAOProcess, FailPAOProcess, archOw.Id);
                    }
                    else
                    {
                        Log.Warning("No TblProjAo found for ArchOwner ID: {ArchOwnerId}", archOw.Id);
                    }

                    // Step 5: Mark ArchOwner as processed
                    archOw.SyncStatus = 3;
                    _OCOCContext.Entry(archOw).State = EntityState.Modified;
                    _OCOCContext.SaveChanges();
                    Log.Information("ArchOwner ID {ArchOwnerId} marked as processed (SyncStatus 3)", archOw.Id);

                    SuccessAOProcess++;
                }
                catch (Exception exAO)
                {
                    Log.Error(exAO, "Exception occurred while processing ArchOwner ID: {ArchOwnerId}", archOw.Id);
                    FailAOProcess++;
                }
            }

            Log.Information("Successfully processed {SuccessCount} ArchOwners, failed to process {FailCount}", SuccessAOProcess, FailAOProcess);
        }
        else
        {
            Log.Warning("No ArchOwner found in tblArchOwner");
        }

        Log.Information("Finished ProcessArchOwnerFunctionality");
    }

    private void UpdateProjectFunctionality(List<TblProject> tblProjects, List<TblProjCounty> tblProjCounty)
    {
        int SuccessCountyProcess = 0,
            FailCountyProcess = 0,
            SuccessProjectProcess = 0,
            FailProjectProcess = 0,
            SuccessProjCountyProcess = 0,
            FailProjCountyProcess = 0;

        _logger.LogInformation($"Total UpdatetblProjects ITEMS COUNT: {tblProjects.Count}");
        _logger.LogInformation($"Total tblProjCounty ITEMS COUNT: {tblProjCounty.Count}");

        if (tblProjects == null || tblProjects.Count == 0)
        {
            _logger.LogWarning("No tblProjects found to process.");
            return;
        }

        foreach (TblProject proj in tblProjects)
        {
            try

            {
                _logger.LogInformation($"Starting sync for Project ID {proj.ProjId}.");
                Project propProject;
                int RecentProjectId = 0;

                // Check if project already exists in _PCNWContext
                if (!_PCNWContext.Projects.AsNoTracking().Any(m => m.SyncProId == proj.ProjId))
                {
                    _logger.LogInformation($"Project ID {proj.ProjId} does not exist in PCNWContext.");
                    var countiesforProj = tblProjCounty.Where(m => m.ProjId == proj.ProjId).ToList();
                    ProcessProjectFunctionality([proj], countiesforProj);
                }
                else
                {
                    _logger.LogInformation($"Updating Project ID {proj.ProjId}.");
                    propProject = _PCNWContext.Projects.FirstOrDefault(pro => pro.SyncProId == proj.ProjId);
                    if (propProject != null)
                    {
                        propProject.AdSpacer = proj.AdSpacer;
                        propProject.ArrivalDt = proj.ArrivalDt;
                        propProject.BendPc = proj.BendPc;
                        propProject.BidBond = proj.BidBond;
                        propProject.BidDt = proj.BidDt;
                        propProject.BidDt2 = proj.BidDt2;
                        propProject.BidDt3 = proj.BidDt3;
                        propProject.BidDt4 = proj.BidDt4;
                        propProject.BidNo = proj.BidNo;
                        propProject.BidPkg = proj.BidPkg;
                        propProject.Brnote = proj.Brnote;
                        propProject.BrresultsFrom = proj.BrresultsFrom;
                        propProject.BuildSolrIndex = proj.BuildSolrIndex;
                        propProject.CallBack = proj.CallBack;
                        propProject.CheckSentDt = proj.CheckSentDt;
                        propProject.CompleteDt = proj.CompleteDt;
                        propProject.Contact = proj.Contact;
                        propProject.Deposit = proj.Deposit;
                        propProject.Dfnote = proj.Dfnote;
                        propProject.DiPath = proj.DiPath;
                        propProject.DirtId = proj.DirtId;
                        propProject.DrawingPath = proj.DrawingPath;
                        propProject.DrawingVols = proj.DrawingVols;
                        propProject.Dup1 = proj.Dup1;
                        propProject.Dup2 = proj.Dup2;
                        propProject.DupArDt = proj.DupArDt;
                        //propProject.GeogPt = proj.GeogPt;
                        propProject.DupTitle = proj.DupTitle;
                        propProject.DwChk = proj.DwChk;
                        propProject.EstCost = proj.EstCost;
                        propProject.EstCost2 = proj.EstCost2;
                        propProject.EstCost3 = proj.EstCost3;
                        propProject.EstCost4 = proj.EstCost4;
                        propProject.EstCostNum = proj.EstCostNum;
                        propProject.EstCostNum2 = proj.EstCostNum2;
                        propProject.EstCostNum3 = proj.EstCostNum3;
                        propProject.EstCostNum4 = proj.EstCostNum4;
                        propProject.ExtendedDt = proj.ExtendedDt;
                        propProject.FutureWork = proj.FutureWork;
                        propProject.Hold = proj.Hold;
                        propProject.ImportDt = proj.ImportDt;
                        //propProject.InternalNote = proj.InternalNote;
                        propProject.InternetDownload = proj.InternetDownload;
                        propProject.IssuingOffice = proj.IssuingOffice;
                        propProject.LastBidDt = proj.LastBidDt;
                        propProject.Latitude = proj.Latitude;
                        propProject.LocAddr1 = proj.LocAddr1;
                        propProject.LocAddr2 = proj.LocAddr2;
                        propProject.LocCity = proj.LocCity;
                        propProject.LocCity2 = proj.LocCity2;
                        propProject.LocCity3 = proj.LocCity3;
                        propProject.LocState = proj.LocState;
                        propProject.LocState2 = proj.LocState2;
                        propProject.LocState3 = proj.LocState3;
                        propProject.LocZip = proj.LocZip;
                        propProject.Longitude = proj.Longitude;
                        propProject.Mandatory = proj.Mandatory;
                        propProject.Mandatory2 = proj.Mandatory2;
                        propProject.MaxViewPath = proj.MaxViewPath;
                        propProject.NonRefundAmt = proj.NonRefundAmt;
                        propProject.NoPrint = proj.NoPrint;
                        propProject.NoSpecs = proj.NoSpecs;
                        propProject.OnlineNote = proj.OnlineNote;
                        propProject.Phldone = proj.Phldone;
                        propProject.Phlnote = proj.Phlnote;
                        propProject.Phltimestamp = proj.Phltimestamp;
                        propProject.PhlwebLink = proj.PhlwebLink;
                        propProject.PlanNo = proj.PlanNo;
                        propProject.PlanNoMain = proj.PlanNoMain;
                        propProject.PrebidAnd = proj.PrebidAnd;
                        propProject.PreBidDt = proj.PreBidDt;
                        propProject.PreBidDt2 = proj.PreBidDt2;
                        propProject.PreBidLoc = proj.PreBidLoc;
                        propProject.PreBidLoc2 = proj.PreBidLoc2;
                        propProject.PrebidOr = proj.PrebidOr;
                        propProject.PrevailingWage = proj.PrevailingWage;
                        propProject.ProjIdMain = proj.ProjIdMain;
                        //propProject.ProjNote = proj.ProjNote;
                        propProject.InternalNote = proj.ProjNote;
                        propProject.ProjTimeStamp = proj.ProjTimeStamp;
                        propProject.ProjTypeId = proj.ProjTypeId;
                        propProject.Publish = proj.Publish;
                        propProject.PublishedFrom = proj.PublishedFrom;
                        propProject.PublishedFromDt = proj.PublishedFromDt;
                        propProject.Recycle = proj.Recycle;
                        propProject.RefundAmt = proj.RefundAmt;
                        propProject.RegionId = proj.RegionId;
                        propProject.RenChk = proj.RenChk;
                        propProject.ResultDt = proj.ResultDt;
                        propProject.S11x17 = proj.S11x17;
                        propProject.S18x24 = proj.S18x24;
                        propProject.S24x36 = proj.S24x36;
                        propProject.S30x42 = proj.S30x42;
                        propProject.S36x48 = proj.S36x48;
                        propProject.ShipCheck = proj.ShipCheck;
                        propProject.ShowBr = proj.ShowBr;
                        propProject.ShowOnWeb = proj.ShowOnWeb;
                        propProject.ShowToAll = proj.ShowToAll;
                        propProject.SolrIndexDt = proj.SolrIndexDt;
                        propProject.SolrIndexPdfdt = proj.SolrIndexPdfdt;
                        propProject.SpcChk = proj.SpcChk;
                        propProject.SpecPath = proj.SpecPath;
                        propProject.SpecsOnPlans = proj.SpecsOnPlans;
                        propProject.SpecVols = proj.SpecVols;
                        propProject.Story = proj.Story;
                        propProject.StoryUnf = proj.StoryUnf;
                        propProject.StrAddenda = proj.StrAddenda;
                        propProject.StrBidDt = proj.StrBidDt;
                        propProject.StrBidDt2 = proj.StrBidDt2;
                        propProject.StrBidDt3 = proj.StrBidDt3;
                        propProject.StrBidDt4 = proj.StrBidDt4;
                        propProject.StrPreBidDt = proj.StrPreBidDt;
                        propProject.StrPreBidDt2 = proj.StrPreBidDt2;
                        propProject.SubApprov = proj.SubApprov;
                        propProject.Title = proj.Title;
                        propProject.TopChk = proj.TopChk;
                        propProject.Uc = proj.Uc;
                        propProject.Ucpublic = proj.Ucpublic;
                        propProject.Ucpwd = proj.Ucpwd;
                        propProject.Ucpwd2 = proj.Ucpwd2;
                        propProject.UnderCounter = proj.UnderCounter;

                        if (propProject.PlanNo == 400) propProject.FutureWork = true;


                        _logger.LogInformation($"{proj.ProjId} Publish : {proj.Publish}");

                        _PCNWContext.Entry(propProject).State = EntityState.Modified;
                        var result = _PCNWContext.SaveChanges();
                        RecentProjectId = (int)propProject.ProjId;
                        SuccessProjectProcess++;
                    }
                    else
                    {
                        _logger.LogWarning($"Project ID {proj.ProjId} not found for update.");
                        FailProjectProcess++;
                        continue;
                    }
                    try
                    {
                        _logger.LogInformation($"Creating Directory for Project ID {proj.ProjId}");
                        CreateOrSyncLocalFiles(propProject);
                        _logger.LogInformation($"Successfully Created Directory for Project ID {proj.ProjId}");
                    }
                    catch (Exception)
                    {
                        _logger.LogInformation($"Failed in Creating Directory for Project ID {proj.ProjId}.");
                        break;
                    }
                }
                List<TblProjCounty> lstpc = tblProjCounty.Where(prCou => prCou.ProjId == proj.ProjId).ToList();

                UpdateOrAddCounties(proj, lstpc, RecentProjectId, SuccessProjCountyProcess, FailProjCountyProcess);
                try
                {
                    UpdateOrAddPreBidInfo(proj, RecentProjectId);
                    UpdateOrAddEstCostDetail(proj, RecentProjectId);

                    var count = _PCNWContext.SaveChanges();
                    if (count > 0)
                        _logger.LogInformation($"PrebidInfo & EstCost Table Updated for Project ID {proj.ProjId} Updated.");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Exception occured for updating  PrebidInfo & EstCostDetails tables of Project ID {proj.ProjId}.");
                    _logger.LogError(ex, "An error occurred while updating PrebidInfo & EstCostDetails.");
                    continue;
                }
                var tblprojfieldchng = _OCOCContext.TblProjFieldChngs.Where(m => m.ProjId == proj.ProjId && m.SyncDt == null).ToList();

                var abc = _OCOCContext.TblProjFieldChngs.Where(m => m.ProjId == proj.ProjId && m.FieldName == "SyncProject" && m.SyncDt == null)
                    .ExecuteUpdate(s => s.SetProperty(u => u.SyncDt, u => DateTime.Now));

                _logger.LogInformation($"Sync Date updated Project ID {proj.ProjId} in TblProjFieldChng.");
                _logger.LogInformation($"Project ID {proj.ProjId} updated successfully.");
            }
            catch (Exception exProject)
            {
                _logger.LogError(exProject, "Exception occurred for Project ID {ProjectId}", proj.ProjId);
                FailProjectProcess++;
                continue;
            }
        }
    }

    private void ProcessProjectFunctionality(List<TblProject> tblProjects, List<TblProjCounty> tblProjCounty)
    {
        int SuccessCountyProcess = 0,
            FailCountyProcess = 0,
            SuccessProjectProcess = 0,
            FailProjectProcess = 0,
            SuccessProjCountyProcess = 0,
            FailProjCountyProcess = 0;

        _logger.LogInformation($"Total tblProjects ITEMS COUNT: {tblProjects.Count}");
        _logger.LogInformation($"Total tblProjCounty ITEMS COUNT: {tblProjCounty.Count}");

        if (tblProjects == null || tblProjects.Count == 0)
        {
            _logger.LogWarning("No tblProjects found to process.");
            return;
        }

        foreach (TblProject proj in tblProjects)
        {
            try
            {
                _logger.LogInformation($"Starting sync for OCPCProject ID {proj.ProjId}.");
                Project propProject;
                int RecentProjectId = 0;

                // Check if project already exists in _PCNWContext
                if (_PCNWContext.Projects.AsNoTracking().Any(m => m.SyncProId == proj.ProjId))
                {
                    _logger.LogInformation($"OCPCProject ID {proj.ProjId} already exists in PCNWContext.");
                    var record = _OCOCContext.TblProjects.AsNoTracking().FirstOrDefault(m => m.ProjId == proj.ProjId);
                    if (record != null)
                    {
                        proj.SyncStatus = 2;
                        _logger.LogInformation($"Sync status updated to 2 for Project ID {proj.ProjId}.");
                    }
                }

                if (proj.SyncStatus == 1 || !_PCNWContext.Projects.AsNoTracking().Any(m => m.SyncProId == proj.ProjId))
                {
                    // Insert new project
                    propProject = new Project
                    {
                        AdSpacer = proj.AdSpacer,
                        ArrivalDt = proj.ArrivalDt,
                        BendPc = proj.BendPc,
                        BidBond = proj.BidBond,
                        BidDt = proj.BidDt,
                        BidDt2 = proj.BidDt2,
                        BidDt3 = proj.BidDt3,
                        BidDt4 = proj.BidDt4,
                        BidNo = proj.BidNo,
                        BidPkg = proj.BidPkg,
                        Brnote = proj.Brnote,
                        BrresultsFrom = proj.BrresultsFrom,
                        BuildSolrIndex = proj.BuildSolrIndex,
                        CallBack = proj.CallBack,
                        CheckSentDt = proj.CheckSentDt,
                        CompleteDt = proj.CompleteDt,
                        Contact = proj.Contact,
                        Deposit = proj.Deposit,
                        Dfnote = proj.Dfnote,
                        DiPath = proj.DiPath,
                        DirtId = proj.DirtId,
                        DrawingPath = proj.DrawingPath,
                        DrawingVols = proj.DrawingVols,
                        Dup1 = proj.Dup1,
                        Dup2 = proj.Dup2,
                        DupArDt = proj.DupArDt,
                        //GeogPt = proj.GeogPt,
                        DupTitle = proj.DupTitle,
                        DwChk = proj.DwChk,
                        EstCost = proj.EstCost,
                        EstCost2 = proj.EstCost2,
                        EstCost3 = proj.EstCost3,
                        EstCost4 = proj.EstCost4,
                        EstCostNum = proj.EstCostNum,
                        EstCostNum2 = proj.EstCostNum2,
                        EstCostNum3 = proj.EstCostNum3,
                        EstCostNum4 = proj.EstCostNum4,
                        ExtendedDt = proj.ExtendedDt,
                        FutureWork = proj.FutureWork,
                        //propProject.GeogPt=proj.geog
                        Hold = proj.Hold,
                        ImportDt = proj.ImportDt,
                        //propProject.IndexPDFFiles = proj.IndexPDFFiles;
                        //InternalNote = proj.InternalNote,
                        InternetDownload = proj.InternetDownload,
                        IssuingOffice = proj.IssuingOffice,
                        LastBidDt = proj.LastBidDt,
                        Latitude = proj.Latitude,
                        LocAddr1 = proj.LocAddr1,
                        LocAddr2 = proj.LocAddr2,
                        LocCity = proj.LocCity,
                        LocCity2 = proj.LocCity2,
                        LocCity3 = proj.LocCity3,
                        LocState = proj.LocState,
                        LocState2 = proj.LocState2,
                        LocState3 = proj.LocState3,
                        LocZip = proj.LocZip,
                        Longitude = proj.Longitude,
                        Mandatory = proj.Mandatory,
                        Mandatory2 = proj.Mandatory2,
                        MaxViewPath = proj.MaxViewPath,
                        NonRefundAmt = proj.NonRefundAmt,
                        NoPrint = proj.NoPrint,
                        NoSpecs = proj.NoSpecs,
                        OnlineNote = proj.OnlineNote,
                        Phldone = proj.Phldone,
                        Phlnote = proj.Phlnote,
                        Phltimestamp = proj.Phltimestamp,
                        PhlwebLink = proj.PhlwebLink,
                        PlanNo = proj.PlanNo,
                        PlanNoMain = proj.PlanNoMain,
                        PrebidAnd = proj.PrebidAnd,
                        PreBidDt = proj.PreBidDt,
                        PreBidDt2 = proj.PreBidDt2,
                        PreBidLoc = proj.PreBidLoc,
                        PreBidLoc2 = proj.PreBidLoc2,
                        PrebidOr = proj.PrebidOr,
                        PrevailingWage = proj.PrevailingWage,
                        ProjIdMain = proj.ProjIdMain,
                        //ProjNote = proj.ProjNote,
                        InternalNote = proj.ProjNote,
                        ProjTimeStamp = proj.ProjTimeStamp,
                        ProjTypeId = proj.ProjTypeId,
                        Publish = proj.Publish,
                        PublishedFrom = proj.PublishedFrom,
                        PublishedFromDt = proj.PublishedFromDt,
                        Recycle = proj.Recycle,
                        RefundAmt = proj.RefundAmt,
                        RegionId = proj.RegionId,
                        RenChk = proj.RenChk,
                        ResultDt = proj.ResultDt,
                        S11x17 = proj.S11x17,
                        S18x24 = proj.S18x24,
                        S24x36 = proj.S24x36,
                        S30x42 = proj.S30x42,
                        S36x48 = proj.S36x48,
                        ShipCheck = proj.ShipCheck,
                        ShowBr = proj.ShowBr,
                        ShowOnWeb = proj.ShowOnWeb,
                        ShowToAll = proj.ShowToAll,
                        SolrIndexDt = proj.SolrIndexDt,
                        SolrIndexPdfdt = proj.SolrIndexPdfdt,
                        SpcChk = proj.SpcChk,
                        SpecPath = proj.SpecPath,
                        SpecsOnPlans = proj.SpecsOnPlans,
                        SpecVols = proj.SpecVols,
                        Story = proj.Story,
                        StoryUnf = proj.StoryUnf,
                        StrAddenda = proj.StrAddenda,
                        StrBidDt = proj.StrBidDt,
                        StrBidDt2 = proj.StrBidDt2,
                        StrBidDt3 = proj.StrBidDt3,
                        StrBidDt4 = proj.StrBidDt4,
                        StrPreBidDt = proj.StrPreBidDt,
                        StrPreBidDt2 = proj.StrPreBidDt2,
                        SubApprov = proj.SubApprov,
                        Title = proj.Title,
                        TopChk = proj.TopChk,
                        Uc = proj.Uc,
                        Ucpublic = proj.Ucpublic,
                        Ucpwd = proj.Ucpwd,
                        Ucpwd2 = proj.Ucpwd2,
                        UnderCounter = proj.UnderCounter,
                        SyncStatus = 0,
                        SyncProId = proj.ProjId,
                        ProjNumber = proj.ArrivalDt != null ? FetchProjNumber(proj.ArrivalDt) : null

                    };
                    if (propProject.PlanNo == 400) propProject.FutureWork = true;

                    // Create Project directory

                    _logger.LogInformation($"{proj.ProjId} Publish : {proj.Publish}");
                    _logger.LogInformation($"Inserting new Project ID {proj.ProjId} into PCNWContext.");
                    _PCNWContext.Projects.Add(propProject);

                    var retryPolicy = Policy.Handle<SqlException>(ex => ex.Number == 1205).WaitAndRetry(3, retryAttempt => TimeSpan.FromSeconds(retryAttempt));
                    retryPolicy.Execute(() => _PCNWContext.SaveChanges());

                    RecentProjectId = _PCNWContext.Projects.Max(pro => pro.ProjId);
                    _logger.LogInformation($"Project ID {proj.ProjId} inserted with new Project ID {RecentProjectId}.");

                    try
                    {
                        _logger.LogInformation($"Creating Directory for Project ID {proj.ProjId}");
                        CreateOrSyncLocalFiles(propProject);
                        _logger.LogInformation($"Successfully Created Directory for Project ID {proj.ProjId}");
                    }
                    catch (Exception)
                    {
                        _logger.LogInformation($"Failed in Creating Directory for Project ID {proj.ProjId}.");
                        break;
                    }
                    SuccessProjectProcess++;
                }

                // Handle related ProjCounty entries
                List<TblProjCounty> lstpc = tblProjCounty.Where(prCou => prCou.ProjId == proj.ProjId).ToList();
                _logger.LogInformation($"Processing {lstpc.Count} ProjCounty records for Project ID {proj.ProjId}.");

                UpdateOrAddCounties(proj, lstpc, RecentProjectId, SuccessProjCountyProcess, FailProjCountyProcess);

                try
                {
                    UpdateOrAddPreBidInfo(proj, RecentProjectId);
                    UpdateOrAddEstCostDetail(proj, RecentProjectId);

                    var count = _PCNWContext.SaveChanges();
                    if (count > 0)
                        _logger.LogInformation($"PrebidInfo & EstCost Table Updated for Project ID {proj.ProjId} Updated.");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Exception occured for updating  PrebidInfo & EstCostDetails tables of Project ID {proj.ProjId}.");
                    _logger.LogError(ex, "An error occurred while updating PrebidInfo & EstCostDetails.");
                    continue;
                }

                _OCOCContext.TblProjects
                    .Where(p => p.ProjId == proj.ProjId)
                    .ExecuteUpdate(setters => setters.SetProperty(p => p.SyncStatus, 3));
                _logger.LogInformation($"Sync status updated to 3 for Project ID {proj.ProjId}.");
            }
            catch (Exception exProject)
            {
                _logger.LogError(exProject, "Exception occurred for Project ID {ProjectId}", proj.ProjId);
                FailProjectProcess++;
                continue;
            }

            _logger.LogInformation($"Project ID {proj.ProjId} synchronization completed.");
        }

        _logger.LogInformation($"Total successful project processes: {SuccessProjectProcess}");
        _logger.LogInformation($"Total failed project processes: {FailProjectProcess}");
        _logger.LogInformation($"Total successful ProjCounty processes: {SuccessProjCountyProcess}");
        _logger.LogInformation($"Total failed ProjCounty processes: {FailProjCountyProcess}");
        _logger.LogInformation("TblProject synchronization completed.");
    }

    private void ProcessMemberFunctionality(List<TblMember> tblOCPCMember, List<TblContact> tblOCPCContact)
    {
        int SuccessMemberProcess = 0, FailMemberProcess = 0;
        _logger.LogInformation($"Total tblMember ITEMS COUNT: {tblOCPCMember?.Count ?? 0}");
        _logger.LogInformation($"Total tblContact ITEMS COUNT: {tblOCPCContact?.Count ?? 0}");

        if (tblOCPCMember != null && tblOCPCMember.Count > 0)
        {
            int lastBusinessEntityId = 0;

            foreach (var member in tblOCPCMember)
            {
                try
                {
                    BusinessEntity propBussEnt;
                    _logger.LogInformation($"Member ID {member.Id} sync started.");

                    // Processing based on SyncStatus
                    switch (member.SyncStatus)
                    {
                        case 1: // New Member
                            propBussEnt = new BusinessEntity
                            {
                                BusinessEntityName = member.Company,
                                BusinessEntityEmail = member.Email,
                                BusinessEntityPhone = "",
                                IsMember = true,
                                IsContractor = false,
                                IsArchitect = false,
                                OldMemId = member.Id,
                                OldConId = 0,
                                OldAoId = 0,
                                SyncStatus = 0,
                                SyncMemId = member.Id
                            };
                            _PCNWContext.BusinessEntities.Add(propBussEnt);
                            _PCNWContext.SaveChanges();
                            _logger.LogInformation($"Member ID {member.Id} added to BusinessEntity.");
                            lastBusinessEntityId = _PCNWContext.BusinessEntities.Max(be => be.BusinessEntityId);
                            break;

                        default: // Update Existing Member
                            propBussEnt = _PCNWContext.BusinessEntities.FirstOrDefault(be => be.SyncMemId == member.Id);

                            if (propBussEnt != null)
                            {
                                propBussEnt.BusinessEntityName = member.Company;
                                propBussEnt.BusinessEntityEmail = member.Email;
                                //_PCNWContext.Entry(propBussEnt).State = EntityState.Modified;
                                _PCNWContext.Update(propBussEnt);
                                _PCNWContext.SaveChanges();
                                _logger.LogInformation($"Member ID {member.Id} updated in BusinessEntity.");
                                lastBusinessEntityId = propBussEnt.BusinessEntityId;
                            }
                            else
                            {
                                propBussEnt = new BusinessEntity
                                {
                                    BusinessEntityName = member.Company,
                                    BusinessEntityEmail = member.Email,
                                    BusinessEntityPhone = "",
                                    IsMember = true,
                                    IsContractor = false,
                                    IsArchitect = false,
                                    OldMemId = member.Id,
                                    OldConId = 0,
                                    OldAoId = 0,
                                    SyncStatus = 0,
                                    SyncMemId = member.Id
                                };
                                _PCNWContext.BusinessEntities.Add(propBussEnt);
                                _PCNWContext.SaveChanges();
                                _logger.LogInformation($"Member ID {member.Id} added to BusinessEntity.");
                                lastBusinessEntityId = _PCNWContext.BusinessEntities.Max(be => be.BusinessEntityId);
                            }
                            break;

                    }

                    // Synchronize member data with PCNW tables
                    SyncOCPC_MemberToPCNW_Member_Address(lastBusinessEntityId, member);
                    member.SyncStatus = 3;
                    _OCOCContext.Entry(member).State = EntityState.Modified;
                    _OCOCContext.SaveChanges();
                    _logger.LogInformation($"Member ID {member.Id} successfully updated to BusinessEntity and related tables.");

                    // Process associated contacts
                    var contacts = tblOCPCContact;
                    _logger.LogInformation($"Total Contacts {contacts.Count()} found for Member ID {member.Id}.");

                    foreach (var contact in contacts)
                    {
                        if (contact == null) continue;

                        _logger.LogInformation($"Contact ID {contact.Id} sync started for Member ID {member.Id}.");
                        Contact propCont;
                        try
                        {
                            switch (contact.SyncStatus)
                            {
                                case 1: // New Contact
                                    Guid userId = Guid.Empty;
                                    bool mainContactExists = true;

                                    if (!string.IsNullOrEmpty(contact.Email) && !string.IsNullOrEmpty(contact.Password))
                                    {
                                        var user = new IdentityUser { Email = contact.Email, UserName = contact.Email };
                                        var result = _userManager.CreateAsync(user, contact.Password).GetAwaiter().GetResult();
                                        if (!result.Succeeded) throw new Exception($"Error creating user: {string.Join(", ", result.Errors.Select(e => e.Description))}");

                                        var addRole = _userManager.AddToRoleAsync(user, "Member").GetAwaiter().GetResult();
                                        if (!addRole.Succeeded) throw new Exception($"Error adding role: {string.Join(", ", addRole.Errors.Select(e => e.Description))}");

                                        if (!Guid.TryParse(user.Id, out userId)) throw new Exception($"User ID '{user.Id}' is not a valid GUID.");

                                        mainContactExists = _PCNWContext.Contacts.Any(c => c.BusinessEntityId == lastBusinessEntityId && c.MainContact == true);
                                    }

                                    propCont = new Contact
                                    {
                                        UserId = userId,
                                        CompType = 1,
                                        MainContact = !mainContactExists,
                                        Active = !member.Inactive,
                                        ContactName = contact.Contact,
                                        ContactEmail = contact.Email,
                                        Password = contact.Password,
                                        BusinessEntityId = lastBusinessEntityId,
                                        ContactPhone = contact.Phone,
                                        ContactTitle = contact.Title,
                                        SyncStatus = 0,
                                        SyncConId = contact.ConId
                                    };
                                    _PCNWContext.Contacts.Add(propCont);
                                    break;

                                default: // Update Existing Contact
                                    propCont = _PCNWContext.Contacts.FirstOrDefault(c => c.BusinessEntityId == lastBusinessEntityId && c.SyncConId == contact.ConId);
                                    if (propCont != null)
                                    {
                                        propCont.ContactName = contact.Contact;
                                        propCont.ContactEmail = contact.Email;
                                        propCont.ContactPhone = contact.Phone;
                                        propCont.ContactTitle = contact.Title;
                                        _PCNWContext.Entry(propCont).State = EntityState.Modified;
                                    }
                                    else
                                    {
                                        userId = Guid.Empty;
                                        mainContactExists = true;

                                        if (!string.IsNullOrEmpty(contact.Email) && !string.IsNullOrEmpty(contact.Password))
                                        {
                                            var user = new IdentityUser { Email = contact.Email, UserName = contact.Email };
                                            var result = _userManager.CreateAsync(user, contact.Password).GetAwaiter().GetResult();
                                            if (!result.Succeeded) throw new Exception($"Error creating user: {string.Join(", ", result.Errors.Select(e => e.Description))}");

                                            var addRole = _userManager.AddToRoleAsync(user, "Member").GetAwaiter().GetResult();
                                            if (!addRole.Succeeded) throw new Exception($"Error adding role: {string.Join(", ", addRole.Errors.Select(e => e.Description))}");

                                            if (!Guid.TryParse(user.Id, out userId)) throw new Exception($"User ID '{user.Id}' is not a valid GUID.");

                                            mainContactExists = _PCNWContext.Contacts.Any(c => c.BusinessEntityId == lastBusinessEntityId && c.MainContact == true);
                                        }

                                        propCont = new Contact
                                        {
                                            UserId = userId,
                                            CompType = 1,
                                            MainContact = !mainContactExists,
                                            Active = !member.Inactive,
                                            ContactName = contact.Contact,
                                            ContactEmail = contact.Email,
                                            Password = contact.Password,
                                            BusinessEntityId = lastBusinessEntityId,
                                            ContactPhone = contact.Phone,
                                            ContactTitle = contact.Title,
                                            SyncStatus = 0,
                                            SyncConId = contact.ConId
                                        };
                                        _PCNWContext.Contacts.Add(propCont);
                                    }
                                    break;
                            }

                            _PCNWContext.SaveChanges();
                            _logger.LogInformation($"Contact ID {contact.Id} successfully synced for Member ID {member.Id}.");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Exception occurred for Contact ID {contact.Id} for Member ID {member.Id}: {ex.Message}");
                            _logger.LogError(ex, "An error occurred while updating contact.");
                            continue;
                        }

                        contact.SyncStatus = 3;
                        _OCOCContext.Entry(contact).State = EntityState.Modified;
                        _OCOCContext.SaveChanges();
                    }

                    SuccessMemberProcess++;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Exception occurred for Member ID {member.Id}: {ex.Message}");
                    FailMemberProcess++;
                    continue;
                }
            }

            _logger.LogInformation($"Total Members Processed: {tblOCPCMember.Count}, Success: {SuccessMemberProcess}, Failures: {FailMemberProcess}");
        }
        else
        {
            _logger.LogWarning("No members to process.");
        }
    }

    private void SyncOCPC_MemberToPCNW_Member_Address(int lastBusinessEntityId, TblMember member)
    {
        try
        {
            _logger.LogInformation($"Member ID {member.Id} started syncing for Member and Addresswiht BusinessEntityID {lastBusinessEntityId}");

            Member propMem;
            if (member.SyncStatus == 1)
            {
                propMem = TblMemberToMember(member);

                propMem.BusinessEntityId = lastBusinessEntityId;
                propMem.SyncStatus = 0;
                propMem.SyncMemId = member.Id;
                _ = _PCNWContext.Members.Add(propMem);
            }
            else
            {
                propMem = (from mem in _PCNWContext.Members where mem.BusinessEntityId == lastBusinessEntityId select mem).FirstOrDefault()!;
                if (propMem == null)
                {
                    propMem = TblMemberToMember(member);
                    propMem.BusinessEntityId = lastBusinessEntityId;
                    propMem.SyncStatus = 0;
                    propMem.SyncMemId = member.Id;
                    _ = _PCNWContext.Members.Add(propMem);
                }
                else
                {
                    propMem = TblMemberToMember(member);
                    _PCNWContext.Update(propMem);
                    //_PCNWContext.Entry(propMem).Property(p => p.SyncStatus).IsModified = true;
                }
            }

            Address propAdd;
            if (member.SyncStatus == 1)
            {
                propAdd = new Address();
                propAdd.BusinessEntityId = lastBusinessEntityId;
                propAdd.Addr1 = member.BillAddress ?? member.MailAddress ?? "";
                propAdd.City = member.BillCity ?? member.MailCity ?? "";
                propAdd.State = member.BillState ?? member.MailState ?? "";
                propAdd.Zip = member.BillZip ?? member.MailZip ?? "";
                propAdd.SyncStatus = 0;
                propAdd.SyncMemId = member.Id;
                _ = _PCNWContext.Addresses.Add(propAdd);
            }
            else
            {
                propAdd = (from add in _PCNWContext.Addresses where add.BusinessEntityId == lastBusinessEntityId select add).FirstOrDefault();
                if (propAdd == null)
                {
                    propAdd = new Address();
                    propAdd.BusinessEntityId = lastBusinessEntityId;
                    propAdd.Addr1 = member.BillAddress ?? member.MailAddress ?? "";
                    propAdd.City = member.BillCity ?? member.MailCity ?? "";
                    propAdd.State = member.BillState ?? member.MailState ?? "";
                    propAdd.Zip = member.BillZip ?? member.MailZip ?? "";
                    propAdd.SyncStatus = 0;
                    propAdd.SyncMemId = member.Id;
                    _ = _PCNWContext.Addresses.Add(propAdd);
                }
                else
                {
                    propAdd.Addr1 = member.BillAddress ?? member.MailAddress ?? "";
                    propAdd.City = member.BillCity ?? member.MailCity ?? "";
                    propAdd.State = member.BillState ?? member.MailState ?? "";
                    propAdd.Zip = member.BillZip ?? member.MailZip ?? "";

                    _PCNWContext.Update(propAdd);
                    _PCNWContext.Entry(propAdd).Property(p => p.SyncStatus).IsModified = true;
                }

            }
            _ = _PCNWContext.SaveChanges();
            _logger.LogInformation($"Member and Address tables updated for Member ID {member.Id} with BusinessEntityID {lastBusinessEntityId}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Exception occured for Member and Address tables updated for Member ID {member.Id}.");
            _logger.LogError(ex, "An error occurred while updating Member and Address tables.");
            throw;
        }
    }

    public Member TblMemberToMember(TblMember member)
    {
        var propMem = new Member
        {
            Inactive = member.Inactive,
            InsertDate = (DateTime)member.InsertDate!,
            LastPayDate = member.LastPayDate,
            RenewalDate = member.RenewalDate,
            Term = member.Term,
            Div = member.Div,
            Discipline = member.Discipline,
            Note = member.Note,
            MinorityStatus = member.MinorityStatus,
            MemberType = member.MemberType,
            AcceptedTerms = member.AcceptedTerms,
            AcceptedTermsDt = member.AcceptedTermsDt,
            DailyEmail = member.DailyEmail,
            Html = member.Html,
            Overdue = member.Overdue,
            Cod = member.Cod,
            PaperlessBilling = member.PaperlessBilling,
            MemberCost = member.MemberCost,
            MagCost = member.MagCost,
            ArchPkgCost = member.ArchPkgCost,
            AddPkgCost = member.AddPkgCost,
            ResourceDate = member.ResourceDate,
            ResourceCost = member.ResourceCost,
            WebAdDate = member.WebAdDate,
            WebAdCost = member.WebAdCost,
            Phl = member.Phl,
            Email = member.Email,
            NameField = member.NameField,
            FavExp = member.FavExp,
            Grace = member.Grace,
            ConId = member.ConId,
            Gcservices = member.Gcservices,
            ResourceStandard = member.ResourceStandard,
            ResourceColor = member.ResourceColor,
            ResourceLogo = member.ResourceLogo,
            ResourceAdd = member.ResourceAdd,
            Dba = member.Dba,
            Dba2 = member.Dba2,
            Fka = member.Fka,
            Suspended = member.Suspended,
            SuspendedDt = member.SuspendedDt,
            Fax = member.Fax,
            MailAddress = member.MailAddress,
            MailCity = member.MailCity,
            MailState = member.MailState,
            MailZip = member.MailZip,
            OverdueAmt = member.OverdueAmt,
            OverdueDt = member.OverdueDt,
            CalSort = member.CalSort,
            Pdfpkg = member.Pdfpkg,
            ArchPkg = member.ArchPkg,
            AddPkg = member.AddPkg,
            Bend = member.Bend,
            Credits = member.Credits,
            FreelanceEstimator = member.FreelanceEstimator,
            HowdUhearAboutUs = member.HowdUhearAboutUs,
            TmStamp = member.TmStamp
        };
        return propMem;
    }

    private void UpdateOrAddCounties(TblProject proj, List<TblProjCounty> tblProjCounty, int RecentProjectId, int SuccessProjCountyProcess, int FailProjCountyProcess)
    {
        _logger.LogInformation($"Processing TblProjCounty records for Project ID {proj.ProjId}.");

        try
        {
            var projCountiesToSync = tblProjCounty.Where(pc => pc.ProjId == proj.ProjId).ToList();
            _logger.LogInformation($"Found {projCountiesToSync.Count} records to process.");

            var existingProjCounties = _PCNWContext.ProjCounties
                .Where(pc => pc.ProjId == RecentProjectId)
                .ToList();
            if (projCountiesToSync.Count > 0)
            {
                //_PCNWContext.ProjCounties.RemoveRange(existingProjCounties);
                _PCNWContext.ProjCounties.Where(pc => pc.ProjId == RecentProjectId).ExecuteDelete();

                //_logger.LogInformation($"Deleted {existingProjCounties.Count} existing ProjCounty records for Project ID {RecentProjectId}.");


                foreach (var pc in projCountiesToSync)
                {
                    try
                    {
                        var newProjCounty = new ProjCounty
                        {
                            ProjId = RecentProjectId,
                            CountyId = pc.CountyId,
                            SyncStatus = 0,
                            SyncProCouId = pc.ProjCountyId
                        };

                        _PCNWContext.ProjCounties.Add(newProjCounty);
                        _logger.LogInformation($"Added new ProjCounty for County ID {pc.CountyId}.");
                        SuccessProjCountyProcess++;
                        var abc = _OCOCContext.TblProjCounties.Where(m => m.ProjId == proj.ProjId)
                            .ExecuteUpdate(s => s.SetProperty(u => u.SyncStatus, u => 3));
                    }
                    catch (Exception innerEx)
                    {
                        _logger.LogError($"Error processing TblProjCounty ID {pc.ProjCountyId}: {innerEx.Message}");
                        FailProjCountyProcess++;
                    }
                }

                _PCNWContext.SaveChanges();
            }
            _logger.LogInformation($"Completed processing TblProjCounty records for Project ID {proj.ProjId}.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing TblProjCounty for Project ID {proj.ProjId}: {ex.Message}");
        }
    }
    private void UpdateOrAddPreBidInfo(TblProject proj, int recentPCNWProjectId)
    {
        try
        {
            // Log entry
            _logger.LogInformation("Starting UpdateOrAddPreBidInfo for ProjectId: {ProjectId}", recentPCNWProjectId);

            // Get all existing PreBidInfos for the project
            var existingPreBids = _PCNWContext.PreBidInfos
                .Where(p => p.ProjId == recentPCNWProjectId)
                .ToList();

            if (proj.PreBidDt != null || proj.PreBidDt2 != null)
            {
                _logger.LogInformation("Deleting existing PreBidInfos for ProjectId: {ProjectId}", recentPCNWProjectId);
                _PCNWContext.PreBidInfos
                    .Where(pc => pc.ProjId == recentPCNWProjectId)
                    .ExecuteDelete();
            }

            // Handle PreBidDt
            if (proj.PreBidDt != null)
            {
                var preBidDate = proj.PreBidDt.Value.Date;

                var newPreBidInfo = new PreBidInfo
                {
                    PreBidDate = preBidDate,
                    PreBidTime = proj.PreBidDt.Value.ToString("HH:mm"),
                    Location = proj.PreBidLoc,
                    PreBidAnd = proj.PrebidAnd ?? false,
                    ProjId = recentPCNWProjectId,
                    UndecidedPreBid = false,
                    Pst = "PT",
                    SyncStatus = 0
                };

                _logger.LogInformation("Adding new PreBidInfo (PreBidDt) for ProjectId: {ProjectId}", recentPCNWProjectId);
                _PCNWContext.PreBidInfos.Add(newPreBidInfo);
            }

            // Handle PreBidDt2
            if (proj.PreBidDt2 != null)
            {
                var preBidDate = proj.PreBidDt2.Value.Date;

                var newPreBidInfo = new PreBidInfo
                {
                    PreBidDate = preBidDate,
                    PreBidTime = proj.PreBidDt2.Value.ToString("HH:mm"),
                    Location = proj.PreBidLoc2,
                    PreBidAnd = proj.PrebidAnd ?? false,
                    ProjId = recentPCNWProjectId,
                    UndecidedPreBid = false,
                    Pst = "PT",
                    SyncStatus = 0
                };

                _logger.LogInformation("Adding new PreBidInfo (PreBidDt2) for ProjectId: {ProjectId}", recentPCNWProjectId);
                _PCNWContext.PreBidInfos.Add(newPreBidInfo);
            }

            _PCNWContext.SaveChanges();
            _logger.LogInformation("Successfully completed UpdateOrAddPreBidInfo for ProjectId: {ProjectId}", recentPCNWProjectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while updating or adding PreBidInfo for ProjectId: {ProjectId}", recentPCNWProjectId);
            // Optional: rethrow or handle as per application flow
            throw;
        }
    }
    private void UpdateOrAddEstCostDetail(TblProject proj, int recentPCNWProjectId)
    {
        try
        {
            _logger.LogInformation("Starting UpdateOrAddEstCostDetail for ProjectId: {ProjectId}", recentPCNWProjectId);

            var existingEstCosts = _PCNWContext.EstCostDetails
                .Where(e => e.ProjId == recentPCNWProjectId)
                .ToList();

            bool hasValidEstCost = (!string.IsNullOrWhiteSpace(proj.EstCost) && !string.Equals(proj.EstCost, "N/A", StringComparison.OrdinalIgnoreCase)) ||
                                   (!string.IsNullOrWhiteSpace(proj.EstCost2) && !string.Equals(proj.EstCost2, "N/A", StringComparison.OrdinalIgnoreCase)) ||
                                   (!string.IsNullOrWhiteSpace(proj.EstCost3) && !string.Equals(proj.EstCost3, "N/A", StringComparison.OrdinalIgnoreCase)) ||
                                   (!string.IsNullOrWhiteSpace(proj.EstCost4) && !string.Equals(proj.EstCost4, "N/A", StringComparison.OrdinalIgnoreCase));

            if (hasValidEstCost)
            {
                _logger.LogInformation("Deleting existing EstCostDetails for ProjectId: {ProjectId}", recentPCNWProjectId);
                _PCNWContext.EstCostDetails
                    .Where(pc => pc.ProjId == recentPCNWProjectId)
                    .ExecuteDelete();
            }

            void ProcessEstCost(string estCost, string fieldName)
            {
                if (!string.IsNullOrWhiteSpace(estCost) && !string.Equals(estCost, "N/A", StringComparison.OrdinalIgnoreCase))
                {
                    string description = null;
                    string rangeSign = "0";
                    string costTo = string.Empty;
                    string costFrom = string.Empty;

                    var descriptionMatch = Regex.Match(estCost, @"\(([^)]+)\)");
                    if (descriptionMatch.Success)
                    {
                        description = descriptionMatch.Groups[1].Value.Trim();
                    }

                    var cleanedEstCost = Regex.Replace(estCost, @"\(([^)]+)\)", "").Trim();

                    if (cleanedEstCost.Contains('<'))
                    {
                        rangeSign = "1";
                        costFrom = cleanedEstCost.Replace("<", "").Trim().Replace("$", "");
                    }
                    else if (cleanedEstCost.Contains('>'))
                    {
                        rangeSign = "2";
                        costFrom = cleanedEstCost.Replace(">", "").Trim().Replace("$", "");
                    }
                    else if (cleanedEstCost.Contains('-'))
                    {
                        rangeSign = "0";
                        var costs = cleanedEstCost.Split('-');
                        costFrom = costs[0].Trim().Replace("$", "");
                        costTo = costs[1].Trim().Replace("$", "");
                    }
                    else
                    {
                        costFrom = cleanedEstCost.Trim().Replace("$", "");
                    }

                    _logger.LogInformation("Parsed {FieldName} for ProjectId {ProjectId}: From=${CostFrom}, To=${CostTo}, Desc={Description}, RangeSign={RangeSign}",
                        fieldName, recentPCNWProjectId, costFrom, costTo, description, rangeSign);

                    var newEstCostDetail = new EstCostDetail
                    {
                        EstCostTo = costTo,
                        EstCostFrom = costFrom,
                        Description = description,
                        ProjId = recentPCNWProjectId,
                        Removed = false,
                        RangeSign = rangeSign,
                        SyncStatus = 0
                    };

                    _PCNWContext.EstCostDetails.Add(newEstCostDetail);
                }
            }

            ProcessEstCost(proj.EstCost, nameof(proj.EstCost));
            ProcessEstCost(proj.EstCost2, nameof(proj.EstCost2));
            ProcessEstCost(proj.EstCost3, nameof(proj.EstCost3));
            ProcessEstCost(proj.EstCost4, nameof(proj.EstCost4));

            _PCNWContext.SaveChanges();
            _logger.LogInformation("Successfully completed UpdateOrAddEstCostDetail for ProjectId: {ProjectId}", recentPCNWProjectId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred in UpdateOrAddEstCostDetail for ProjectId: {ProjectId}", recentPCNWProjectId);
            throw;
        }
    }
    private static int ParseRetentionMonths(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        return value.Trim().ToLowerInvariant() switch
        {
            "1 month" => 1,
            "6 months" => 6,
            "1 year" => 12,
            "18 months" => 18,
            "2 years" => 24,
            _ => 0
        };
    }

    // Build path from a ProjNumber using the same convention you use elsewhere:
    // base / "20" + YY / MM / ProjNumber  (e.g., FileUploadPath/2025/09/2509xxxx)
    private string GetProjectPathForNumber(string projNumber)
    {
        var basePath = _fileUploadPath;
        var year = string.Concat("20", projNumber.AsSpan(0, 2));
        var month = projNumber.Substring(2, 2);
        return Path.Combine(basePath, year, month, projNumber);
    }

    public async Task CleanupStorageByPolicyAsync(bool whatIf = false, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PCNWProjectDBContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<SyncController>>();

        // 1) Load storage policy
        var storageRow = await db.FileStorages
            .AsNoTracking()
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);

        var months = ParseRetentionMonths(storageRow?.FileStorage);
        if (months <= 0)
        {
            logger.LogInformation("Cleanup skipped: no valid FileStorage value set.");
            return;
        }

        var cutoff = DateTime.Today.AddMonths(-months);
        logger.LogInformation("Cleanup policy: {Policy} ⇒ cutoff {Cutoff:d}.", storageRow!.FileStorage, cutoff);

        // 2) Pick candidate projects
        var candidates = await db.Projects
            .AsNoTracking()
            .Where(p => p.BidDt != null && p.BidDt < cutoff)
            .Select(p => new { p.ProjId, p.ProjNumber })
            .ToListAsync(ct);

        if (candidates.Count == 0)
        {
            logger.LogInformation("Cleanup found 0 projects older than {Cutoff:d}.", cutoff);
            return;
        }

        var deleteIds = candidates.Select(c => c.ProjId).Distinct().ToList();
        var deleteNumbers = candidates.Select(c => c.ProjNumber)
                                      .Where(n => !string.IsNullOrWhiteSpace(n))
                                      .Distinct()
                                      .ToList();

        logger.LogInformation("Cleanup will remove {Count} projects.", deleteIds.Count);

        if (whatIf)
        {
            // Per-project WHATIF logging (counts only)
            foreach (var pid in deleteIds)
            {
                var counts = await CountChildrenAsync(db, pid, ct);
                logger.LogInformation(
                    "WHATIF ProjId {ProjId}: ProjCounties={Cnt1}, Entities={Cnt2}, PhlInfos={Cnt3}, PreBidInfos={Cnt4}, EstCostDetails={Cnt5}, Project=1",
                    pid, counts.ProjCounties, counts.Entities, counts.PhlInfos, counts.PreBidInfos, counts.EstCostDetails);
            }
            logger.LogInformation("WHATIF mode: no deletions performed.");
            return;
        }

        // 3) DB deletes (pure EF RemoveRange) — do it per project to keep memory low and to log per-project
        //    Everything in a single transaction for consistency.
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Recommended: process in batches of projects (e.g., 200) to avoid long transactions if delete set is huge
        const int projectBatchSize = 200;
        for (int i = 0; i < deleteIds.Count; i += projectBatchSize)
        {
            var batch = deleteIds.Skip(i).Take(projectBatchSize).ToList();

            foreach (var pid in batch)
            {
                // Count first (for logging)
                var counts = await CountChildrenAsync(db, pid, ct);

                // Load + RemoveRange for each child table (pure EF)
                // NOTE: If you know the entity keys, you can optimize by projecting keys and attaching stubs to delete.
                // Here we ToListAsync() to keep it simple and safe.
                var counties = await db.ProjCounties.Where(x => x.ProjId == pid).ToListAsync(ct);
                db.ProjCounties.RemoveRange(counties);

                var entities = await db.Entities.Where(x => x.ProjId == pid).ToListAsync(ct);
                db.Entities.RemoveRange(entities);

                var phls = await db.PhlInfos.Where(x => x.ProjId == pid).ToListAsync(ct);
                db.PhlInfos.RemoveRange(phls);

                var prebids = await db.PreBidInfos.Where(x => x.ProjId == pid).ToListAsync(ct);
                db.PreBidInfos.RemoveRange(prebids);

                var estCosts = await db.EstCostDetails.Where(x => x.ProjId == pid).ToListAsync(ct);
                db.EstCostDetails.RemoveRange(estCosts);

                // Parent last
                var project = await db.Projects.FirstOrDefaultAsync(p => p.ProjId == pid, ct);
                if (project != null)
                {
                    db.Projects.Remove(project);
                }

                // Save per project (or per few projects) to avoid huge change tracker
                await db.SaveChangesAsync(ct);

                logger.LogInformation(
                    "Deleted ProjId {ProjId}: ProjCounties={Cnt1}, Entities={Cnt2}, PhlInfos={Cnt3}, PreBidInfos={Cnt4}, EstCostDetails={Cnt5}, Project={ParentCnt}",
                    pid,
                    counts.ProjCounties,
                    counts.Entities,
                    counts.PhlInfos,
                    counts.PreBidInfos,
                    counts.EstCostDetails,
                    project != null ? 1 : 0);
            }
        }

        await tx.CommitAsync(ct);

        // 4) Filesystem cleanup (best-effort; non-fatal) — logs per project folder
        if (!string.IsNullOrWhiteSpace(_fileUploadPath) && Directory.Exists(_fileUploadPath))
        {
            int fsDeleted = 0, fsErrors = 0;

            foreach (var projNo in deleteNumbers)
            {
                try
                {
                    var projDir = GetProjectPathForNumber(projNo);
                    if (!string.IsNullOrWhiteSpace(projDir) && Directory.Exists(projDir))
                    {
                        Directory.Delete(projDir, recursive: true);
                        fsDeleted++;
                        logger.LogInformation("Deleted folder for project {ProjNumber}: {Path}", projNo, projDir);
                    }
                    else
                    {
                        logger.LogInformation("No folder found for project {ProjNumber} at {Path}", projNo, projDir);
                    }
                }
                catch (Exception ex)
                {
                    fsErrors++;
                    logger.LogWarning(ex, "Failed to delete directory for project {ProjNumber}", projNo);
                }
            }

            logger.LogInformation("Filesystem cleanup: deleted {Ok} folders, {Err} errors.", fsDeleted, fsErrors);
        }
        else
        {
            logger.LogInformation("Filesystem cleanup skipped: base directory missing/not set: {Base}", _fileUploadPath);
        }

        logger.LogInformation("Cleanup finished. Deleted {Count} projects older than {Cutoff:d}.", deleteIds.Count, cutoff);
    }

    // --------- Helpers (pure C#) ---------

    private sealed record ChildCounts(
        int ProjCounties,
        int Entities,
        int PhlInfos,
        int PreBidInfos,
        int EstCostDetails
    );

    private static async Task<ChildCounts> CountChildrenAsync(PCNWProjectDBContext db, int projectId, CancellationToken ct)
    {
        var c1 = await db.ProjCounties.Where(x => x.ProjId == projectId).CountAsync(ct);
        var c2 = await db.Entities.Where(x => x.ProjId == projectId).CountAsync(ct);
        var c3 = await db.PhlInfos.Where(x => x.ProjId == projectId).CountAsync(ct);
        var c4 = await db.PreBidInfos.Where(x => x.ProjId == projectId).CountAsync(ct);
        var c5 = await db.EstCostDetails.Where(x => x.ProjId == projectId).CountAsync(ct);
        return new ChildCounts(c1, c2, c3, c4, c5);
    }
    private static string TruncateSafe(string? s, int max)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        return s.Length > max ? s.Substring(0, max).Trim() : s.Trim();
    }

    // Optional: strip forbidden path chars from title
    private static string SanitizeForPath(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    private static string BuildLiveFolderName(long planNumber, long SyncProId, string? title)
    {
        var t = SanitizeForPath(TruncateSafe(title, 15));
        return $"({planNumber}) ({SyncProId}) {t}";
    }
    private static readonly Regex _liveDirRegex =
    new Regex(@"\((\d+)\)\s*\((\d+)\)\s*(.*)$", RegexOptions.Compiled);

    // Fallback regex if some folders are "(255899) ..." only
    private static readonly Regex _fallbackProjRegex =
        new Regex(@"\((\d+)\)", RegexOptions.Compiled);

    private async Task<Dictionary<long, string>> BuildLiveDirIndexAsync(CancellationToken ct)
    {
        var index = new Dictionary<long, string>(); // projId -> fullpath

        if (string.IsNullOrWhiteSpace(_liveProjectsRoot) || !Directory.Exists(_liveProjectsRoot))
            return index;

        // Enumerate top-level project folders
        foreach (var path in Directory.EnumerateDirectories(_liveProjectsRoot))
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileName(path);

            var m = _liveDirRegex.Match(name);
            if (m.Success && long.TryParse(m.Groups[2].Value, out var projId2))
            {
                index[projId2] = path;
                continue;
            }

            // Fallback: any (...) will be treated as projId when exact format is missing
            var mf = _fallbackProjRegex.Match(name);
            if (mf.Success && long.TryParse(mf.Groups[1].Value, out var projIdOnly))
            {
                // prefer exact match if already present
                if (!index.ContainsKey(projIdOnly))
                    index[projIdOnly] = path;
            }
        }

        return await Task.FromResult(index);
    }
    private string GetBetaProjectDir(string projNumber)
    {
        var year = "20" + projNumber.Substring(0, 2);
        var month = projNumber.Substring(2, 2);
        return Path.Combine(_fileUploadPath, year, month, projNumber);
    }
  

    public async Task SyncFilesFromLiveToBetaAsync(CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_liveProjectsRoot) || !Directory.Exists(_liveProjectsRoot))
            {
                _logger.LogInformation("Live projects root not found or not configured: {LiveRoot}", _liveProjectsRoot);
                return;
            }
            if (string.IsNullOrWhiteSpace(_fileUploadPath) || !Directory.Exists(_fileUploadPath))
            {
                _logger.LogInformation("Beta file root not found or not configured: {BetaRoot}", _fileUploadPath);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PCNWProjectDBContext>();

            // Pull current projects we keep in PCNW (post-cleanup)
            var projects = await db.Projects
                .AsNoTracking()
                .Select(p => new
                {
                    p.ProjNumber,
                    p.PlanNo,     
                    p.Title,  
                    p.SyncProId
                })
                .ToListAsync(ct);

            if (projects.Count == 0)
            {
                _logger.LogInformation("No projects found to sync files.");
                return;
            }

            // Build Live index once
            _logger.LogInformation("Indexing Live project folders at {Root} ...", _liveProjectsRoot);
            var liveIndex = await BuildLiveDirIndexAsync(ct);
            _logger.LogInformation("Indexed {Count} Live folders.", liveIndex.Count);

            int total = 0, copied = 0, missing = 0, errors = 0;

            // Parallelize for speed
            var po = new ParallelOptions
            {
                CancellationToken = ct,
                MaxDegreeOfParallelism = _copyDegreeOfParallelism
            };

            await Task.Run(() =>
            {
                Parallel.ForEach(projects, po, proj =>
                {
                    po.CancellationToken.ThrowIfCancellationRequested();
                    Interlocked.Increment(ref total);

                    try
                    {
                        // Prefer direct index lookup by ProjId
                        string? liveDir = null;
                        if (liveIndex.TryGetValue(proj.SyncProId.Value, out var indexed))
                        {
                            liveDir = indexed;
                        }
                        else
                        {
                            // Build expected name, then check (handles title/spacing variants when exact exists)
                            var expected = BuildLiveFolderName(proj.PlanNo.Value, proj.SyncProId.Value, proj.Title);
                            var candidate = Path.Combine(_liveProjectsRoot, expected);
                            if (Directory.Exists(candidate))
                                liveDir = candidate;
                        }

                        if (string.IsNullOrEmpty(liveDir) || !Directory.Exists(liveDir))
                        {
                            Interlocked.Increment(ref missing);
                            _logger.LogDebug("Live folder missing for ProjId {ProjId} (ProjNumber {ProjNumber}).", proj.SyncProId.Value, proj.ProjNumber);
                            return;
                        }

                        var betaDir = GetBetaProjectDir(proj.ProjNumber);
                        Directory.CreateDirectory(betaDir);

                        // Copy incrementally (counts are approximate; we log per-project success)
                        CopyDirectoryIncremental(liveDir, betaDir, po.CancellationToken);
                        Interlocked.Increment(ref copied);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref errors);
                        _logger.LogWarning(ex, "File sync failed for ProjId {ProjId} (ProjNumber {ProjNumber}).", proj.SyncProId, proj.ProjNumber);
                    }
                });
            }, ct);

            _logger.LogInformation("File sync finished. Projects processed: {Total}. Copied/updated: {Copied}. Missing live folder: {Missing}. Errors: {Errors}.",
                total, copied, missing, errors);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("File sync canceled.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File sync crashed.");
            throw;
        }
    }

    /// Try resolve the **Live** folder for a single project.
    /// Strategy: scan one level of G:\Data\Projects and match by "(plan) (projId) Title",
    /// falling back to any "(projId)" match if formatting varies.
    private string? TryResolveLiveDir(long projId, long planNumber, string? title)
    {
        if (string.IsNullOrWhiteSpace(_liveProjectsRoot) || !Directory.Exists(_liveProjectsRoot))
            return null;

        // First: exact expected name
        var exact = Path.Combine(_liveProjectsRoot, BuildLiveFolderName(planNumber, projId, title));
        if (Directory.Exists(exact)) return exact;

        // Fallback: scan one level, pull any that embeds (projId)
        foreach (var path in Directory.EnumerateDirectories(_liveProjectsRoot))
        {
            var name = Path.GetFileName(path);
            var m = _liveDirRegex.Match(name);
            if (m.Success && long.TryParse(m.Groups[2].Value, out var pid) && pid == projId)
                return path;

            var mf = _fallbackProjRegex.Match(name);
            if (mf.Success && long.TryParse(mf.Groups[1].Value, out var only) && only == projId)
                return path;
        }
        return null;
    }

    private void CreateOrSyncLocalFiles(Project project, CancellationToken ct = default)
    {
        // 1) Ensure local folders exist (your existing method)
        CreateProjectDirectory(project);

        try
        {
            var localPath = GetProjectPath(project); // year/month/ProjNumber
            if (string.IsNullOrWhiteSpace(localPath) || !Directory.Exists(localPath))
            {
                _logger.LogWarning("Skipping project {ProjId} (ProjNumber {ProjNumber}) because local path is invalid or missing: {LocalPath}",
                    project.ProjId, project.ProjNumber, localPath);
                return;
            }

            var liveDir = TryResolveLiveDir(project.SyncProId.Value,
                                            project.PlanNo.Value,
                                            project.Title);

            if (!string.IsNullOrEmpty(liveDir) && Directory.Exists(liveDir))
            {
                _logger.LogInformation("Syncing project {ProjId} (ProjNumber {ProjNumber}) from live dir {LiveDir} → local dir {LocalDir}",
                    project.ProjId, project.ProjNumber, liveDir, localPath);

                CopyDirectoryIncremental(liveDir, localPath, ct);
            }
            else
            {
                _logger.LogWarning("Live directory not found for project {ProjId} (ProjNumber {ProjNumber}): {LiveDir}",
                    project.ProjId, project.ProjNumber, liveDir);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Live→Local copy failed for ProjId {ProjId} (ProjNumber {ProjNumber}).",
                project.ProjId, project.ProjNumber);
        }
    }

    private void CopyDirectoryIncremental(string sourceDir, string destDir, CancellationToken ct)
    {
        Directory.CreateDirectory(destDir);
        _logger.LogInformation("Scanning directory {SourceDir}", sourceDir);

        // Files
        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            ct.ThrowIfCancellationRequested();
            var fileName = Path.GetFileName(file);
            var destPath = Path.Combine(destDir, fileName);

            try
            {
                var srcInfo = new FileInfo(file);
                var dstInfo = new FileInfo(destPath);

                bool shouldCopy =
                    !dstInfo.Exists ||
                    dstInfo.Length != srcInfo.Length ||
                    dstInfo.LastWriteTimeUtc < srcInfo.LastWriteTimeUtc;

                if (shouldCopy)
                {
                    _logger.LogInformation("Copying file {FileName} from {Source} → {Destination}",
                        fileName, file, destPath);

                    File.Copy(file, destPath, overwrite: true);
                    File.SetLastWriteTimeUtc(destPath, srcInfo.LastWriteTimeUtc);
                }
                else
                {
                    _logger.LogDebug("Skipping unchanged file {FileName}", fileName);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to copy file {FileName} from {Source} to {Destination}",
                    fileName, file, destPath);
            }
        }

        // Subdirectories (recurse)
        foreach (var sub in Directory.EnumerateDirectories(sourceDir))
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileName(sub);
            var dst = Path.Combine(destDir, name);

            _logger.LogInformation("Descending into subdirectory {SubDir}", sub);

            CopyDirectoryIncremental(sub, dst, ct);
        }
    }

    private static SqlParameter ToIntTvp(string name, IEnumerable<int> values)
    {
        var table = new DataTable();
        table.Columns.Add("Value", typeof(int));
        foreach (var v in values)
            table.Rows.Add(v);

        return new SqlParameter(name, SqlDbType.Structured)
        {
            TypeName = "dbo.IntList",  // must match the type you created in SQL Server
            Value = table
        };
    }


}