using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Polly;
using Serilog;
using SyncRoutineWS.OCPCModel;
using SyncRoutineWS.PCNWModel;
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

    public SyncController(IServiceScopeFactory scopeFactory, ILogger<SyncController> logger, OCPCProjectDBContext OCPCcont1, PCNWProjectDBContext PCNWcont2, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        _OCOCContext = OCPCcont1;
        _PCNWContext = PCNWcont2;
        _scopeFactory = scopeFactory;
        _fileUploadPath = _configuration.GetSection("AppSettings")["FileUploadPath"] ?? string.Empty;
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
            _logger.LogInformation($"Application started at {DateTime.Now}",DateTime.Now);

            #region SYNC FROM OCPCLive - PCNWTest

            //Member Sync code

            //var businessEntityEmails = _PCNWContext.BusinessEntities
            //                   .Select(be => be.BusinessEntityEmail)
            //                   .ToHashSet();

            //               var tblOCPCMember = (from mem in _OCOCContext.TblMembers
            //                                    join con in _OCOCContext.TblContacts
            //                                    on mem.Id equals con.Id
            //                                    where (con.SyncStatus == 1 && !businessEntityEmails.Contains(con.Email))
            //                                    || con.SyncStatus == 2
            //                                    select mem)
            //                                    .Take(1).OrderBy(m => m.Id)
            //                                    .AsNoTracking()
            //                                    .ToList();

            //               var memberids = tblOCPCMember.Select(m => m.Id).ToList();

            //               var tblOCPCContact = _OCOCContext.TblContacts.Where(con => (con.SyncStatus == 1 || con.SyncStatus == 2))
            //                   .AsNoTracking()
            //                   .ToList();

            //               tblOCPCContact = tblOCPCContact.Where(m => memberids.Contains(m.Id)).ToList();
            //               ProcessMemberFunctionality(tblOCPCMember, tblOCPCContact);

            // project sync code

            var syncedProjectsIds = _PCNWContext.Projects.Where(m => m.SyncProId != null)
                .AsNoTracking().Select(m => m.SyncProId).ToHashSet();

            var allprojectrecords= _OCOCContext.TblProjects.AsNoTracking().ToHashSet();

            var tblProjects = allprojectrecords.Where(proj => proj.SyncStatus == 1 || !syncedProjectsIds.Contains(proj.ProjId))
                .ToList();


            var tblProjectIds = tblProjects.Select(m => m.ProjId);
            var allcountiesrecords = _OCOCContext.TblProjCounties.AsNoTracking().ToList();
            var tblProjCounty = tblProjects.Count != 0
                ? [.. allcountiesrecords
                        .Where(projCounty =>(projCounty.SyncStatus == 1 || projCounty.SyncStatus == 2) && tblProjectIds.Contains(projCounty.ProjId))]
                : new List<TblProjCounty>();

            //ProcessProjectFunctionality(tblProjects, tblProjCounty);

            var updateProjectsIds = _OCOCContext.TblProjFieldChngs
                .Where(proj => proj.FieldName == "SyncProject" && proj.SyncDt == null)
                .AsNoTracking()
                .Select(m => m.ProjId).ToHashSet();
            var updateProjects = _OCOCContext.TblProjects.Where(m => updateProjectsIds.Contains(m.ProjId)).ToList();

            var tblupdateProjCounty = updateProjects.Count != 0
                ? [.. _OCOCContext.TblProjCounties
                        .Where(projCounty =>(projCounty.SyncStatus == 1 || projCounty.SyncStatus == 2) && updateProjectsIds.Contains(projCounty.ProjId))
                        .AsNoTracking()]
                : new List<TblProjCounty>();
            UpdateProjectFunctionality(updateProjects, tblupdateProjCounty);


            //var prj = _OCOCContext.TblProjects.Where(m => m.ProjId >= 244683).ToList();
            //var countied = _OCOCContext.TblProjCounties.Where(m => m.ProjId >= 244683).ToList();

            //UpdateProjectFunctionality(prj, countied);

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

    public string GetProjectPath(Project propProject)
    {
        string basePath = _fileUploadPath;

        string year = string.Empty;
        string month = string.Empty;
        string projNumber = string.Empty;

        var project = _PCNWContext.Projects
            .Where(p => p.ProjId == propProject.ProjId)
            .Select(p => new { p.ProjNumber, p.ArrivalDt })
            .FirstOrDefault();

        if (project != null)
        {
            if (!string.IsNullOrEmpty(project.ProjNumber))
            {
                year = "20" + project.ProjNumber.Substring(0, 2);
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

                year = "20" + projNumber.Substring(0, 2);
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

            year = "20" + projNumber.Substring(0, 2);
            month = projNumber.Substring(2, 2);

            string projectPath = Path.Combine(basePath, year, month, projNumber);
            return projectPath;
        }

        return null;
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
                        propProject.InternalNote = proj.InternalNote;
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
                        propProject.ProjNote = proj.ProjNote;
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
                _logger.LogError($"Exception occurred for Project ID {proj.ProjId}.", exProject);
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
                _logger.LogInformation($"Starting sync for Project ID {proj.ProjId}.");
                Project propProject;
                int RecentProjectId = 0;

                // Check if project already exists in _PCNWContext
                if (_PCNWContext.Projects.AsNoTracking().Any(m => m.SyncProId == proj.ProjId))
                {
                    _logger.LogInformation($"Project ID {proj.ProjId} already exists in PCNWContext.");
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
                        InternalNote = proj.InternalNote,
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
                        ProjNote = proj.ProjNote,
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
                        SyncProId = proj.ProjId
                    };
                    // Create Project directory

                    _logger.LogInformation($"Inserting new Project ID {proj.ProjId} into PCNWContext.");
                    _PCNWContext.Projects.Add(propProject);

                    var retryPolicy = Policy.Handle<SqlException>(ex => ex.Number == 1205).WaitAndRetry(3, retryAttempt => TimeSpan.FromSeconds(retryAttempt));
                    retryPolicy.Execute(() => _PCNWContext.SaveChanges());

                    RecentProjectId = _PCNWContext.Projects.Max(pro => pro.ProjId);
                    _logger.LogInformation($"Project ID {proj.ProjId} inserted with new Project ID {RecentProjectId}.");

                    try
                    {
                        _logger.LogInformation($"Creating Directory for Project ID {proj.ProjId}");
                        CreateProjectDirectory(propProject);
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

                proj.SyncStatus = 3;
                _OCOCContext.Entry(proj).Property(p => p.SyncStatus).IsModified = true;
                _OCOCContext.SaveChanges();
                _logger.LogInformation($"Sync status updated to 3 for Project ID {proj.ProjId}.");
            }
            catch (Exception exProject)
            {
                _logger.LogError($"Exception occurred for Project ID {proj.ProjId}.", exProject);
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

                        case 2: // Update Existing Member
                            propBussEnt = _PCNWContext.BusinessEntities.FirstOrDefault(be => be.BusinessEntityName == member.Company);
                            if (propBussEnt != null)
                            {
                                propBussEnt.BusinessEntityName = member.Company;
                                propBussEnt.BusinessEntityEmail = member.Email;
                                _PCNWContext.Entry(propBussEnt).State = EntityState.Modified;
                                _PCNWContext.SaveChanges();
                                _logger.LogInformation($"Member ID {member.Id} updated in BusinessEntity.");
                                lastBusinessEntityId = propBussEnt.BusinessEntityId;
                            }
                            break;

                        default:
                            _logger.LogWarning($"Member ID {member.Id} has unsupported SyncStatus: {member.SyncStatus}. Skipping.");
                            continue;
                    }

                    // Synchronize member data with PCNW tables
                    SyncOCPC_MemberToPCNW_Member_Address(lastBusinessEntityId, member);
                    member.SyncStatus = 3;
                    _OCOCContext.Entry(member).State = EntityState.Modified;
                    _OCOCContext.SaveChanges();
                    _logger.LogInformation($"Member ID {member.Id} successfully updated to BusinessEntity and related tables.");

                    // Process associated contacts
                    var contacts = tblOCPCContact.Where(c => c.Id == member.Id && (c.SyncStatus == 1 || c.SyncStatus == 2));
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

                                case 2: // Update Existing Contact
                                    propCont = _PCNWContext.Contacts.FirstOrDefault(c => c.BusinessEntityId == lastBusinessEntityId);
                                    if (propCont != null)
                                    {
                                        propCont.ContactName = contact.Contact;
                                        propCont.ContactEmail = contact.Email;
                                        propCont.ContactPhone = contact.Phone;
                                        propCont.ContactTitle = contact.Title;
                                        _PCNWContext.Entry(propCont).State = EntityState.Modified;
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
                propMem = new Member
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
                    TmStamp = member.TmStamp,
                    BusinessEntityId = lastBusinessEntityId,
                    SyncStatus = 0,
                    SyncMemId = member.Id
                };
                _ = _PCNWContext.Members.Add(propMem);
            }
            else if (member.SyncStatus == 2)
            {
                propMem = (from mem in _PCNWContext.Members where mem.BusinessEntityId == lastBusinessEntityId select mem).FirstOrDefault()!;
                propMem.Inactive = member.Inactive;
                propMem.InsertDate = (DateTime)member.InsertDate!;
                propMem.LastPayDate = member.LastPayDate;
                propMem.RenewalDate = member.RenewalDate;
                propMem.Term = member.Term;
                propMem.Div = member.Div;
                propMem.Discipline = member.Discipline;
                propMem.Note = member.Note;
                propMem.MinorityStatus = member.MinorityStatus;
                propMem.MemberType = member.MemberType;
                propMem.AcceptedTerms = member.AcceptedTerms;
                propMem.AcceptedTermsDt = member.AcceptedTermsDt;
                propMem.DailyEmail = member.DailyEmail;
                propMem.Html = member.Html;
                propMem.Overdue = member.Overdue;
                propMem.Cod = member.Cod;
                propMem.PaperlessBilling = member.PaperlessBilling;
                propMem.MemberCost = member.MemberCost;
                propMem.MagCost = member.MagCost;
                propMem.ArchPkgCost = member.ArchPkgCost;
                propMem.AddPkgCost = member.AddPkgCost;
                propMem.ResourceDate = member.ResourceDate;
                propMem.ResourceCost = member.ResourceCost;
                propMem.WebAdDate = member.WebAdDate;
                propMem.WebAdCost = member.WebAdCost;
                propMem.Phl = member.Phl;
                propMem.Email = member.Email;
                propMem.NameField = member.NameField;
                propMem.FavExp = member.FavExp;
                propMem.Grace = member.Grace;
                propMem.ConId = member.ConId;
                propMem.Gcservices = member.Gcservices;
                propMem.ResourceStandard = member.ResourceStandard;
                propMem.ResourceColor = member.ResourceColor;
                propMem.ResourceLogo = member.ResourceLogo;
                propMem.ResourceAdd = member.ResourceAdd;
                propMem.Dba = member.Dba;
                propMem.Dba2 = member.Dba2;
                propMem.Fka = member.Fka;
                propMem.Suspended = member.Suspended;
                propMem.SuspendedDt = member.SuspendedDt;
                propMem.Fax = member.Fax;
                propMem.MailAddress = member.MailAddress;
                propMem.MailCity = member.MailCity;
                propMem.MailState = member.MailState;
                propMem.MailZip = member.MailZip;
                propMem.OverdueAmt = member.OverdueAmt;
                propMem.OverdueDt = member.OverdueDt;
                propMem.CalSort = member.CalSort;
                propMem.Pdfpkg = member.Pdfpkg;
                propMem.ArchPkg = member.ArchPkg;
                propMem.AddPkg = member.AddPkg;
                propMem.Bend = member.Bend;
                propMem.Credits = member.Credits;
                propMem.FreelanceEstimator = member.FreelanceEstimator;
                propMem.HowdUhearAboutUs = member.HowdUhearAboutUs;
                propMem.TmStamp = member.TmStamp;
                _PCNWContext.Entry(propMem).Property(p => p.SyncStatus).IsModified = true;
            }

            Address propAdd;
            if (member.SyncStatus == 1)
            {
                propAdd = new Address();
                propAdd.BusinessEntityId = lastBusinessEntityId;
                propAdd.Addr1 = member.BillAddress;
                propAdd.City = member.BillCity;
                propAdd.State = member.BillState;
                propAdd.Zip = member.BillZip;
                propAdd.SyncStatus = 0;
                propAdd.SyncMemId = member.Id;
                _ = _PCNWContext.Addresses.Add(propAdd);
            }
            else if (member.SyncStatus == 2)
            {
                propAdd = (from add in _PCNWContext.Addresses where add.BusinessEntityId == lastBusinessEntityId select add).FirstOrDefault();
                propAdd.Addr1 = member.BillAddress;
                propAdd.City = member.BillCity;
                propAdd.State = member.BillState;
                propAdd.Zip = member.BillZip;
                _PCNWContext.Entry(propAdd).Property(p => p.SyncStatus).IsModified = true;
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
                    var abc = _OCOCContext.TblProjCounties.Where(m =>    m.ProjId == proj.ProjId)
                        .ExecuteUpdate(s => s.SetProperty(u => u.SyncStatus, u => 3));                    
                }
                catch (Exception innerEx)
                {
                    _logger.LogError($"Error processing TblProjCounty ID {pc.ProjCountyId}: {innerEx.Message}");
                    FailProjCountyProcess++;
                }
            }

            _PCNWContext.SaveChanges();
            _logger.LogInformation($"Completed processing TblProjCounty records for Project ID {proj.ProjId}.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing TblProjCounty for Project ID {proj.ProjId}: {ex.Message}");
        }
    }

    private void UpdateOrAddPreBidInfo(TblProject proj, int recentPCNWProjectId)
    {
        // Get all existing PreBidInfos for the project
        var existingPreBids = _PCNWContext.PreBidInfos
            .Where(p => p.ProjId == recentPCNWProjectId)
            .ToList();

        if(proj.PreBidDt !=null || proj.PreBidDt2 != null)
        {
            _PCNWContext.PreBidInfos.Where(pc => pc.ProjId == recentPCNWProjectId).ExecuteDelete();
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
                _PCNWContext.PreBidInfos.Add(newPreBidInfo);
        }

    }

    private void UpdateOrAddEstCostDetail(TblProject proj, int recentPCNWProjectId)
    {
        var existingEstCosts = _PCNWContext.EstCostDetails
            .Where(e => e.ProjId == recentPCNWProjectId)
            .ToList();


        void ProcessEstCost(string estCost)
        {
            if (!string.IsNullOrWhiteSpace(estCost) && !string.Equals(estCost, "N/A", StringComparison.OrdinalIgnoreCase))
            {
                string description = null;
                var rangeSign = "0";
                var costTo = string.Empty;
                var costFrom = string.Empty;

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

                //costTo = Regex.Replace(costTo, @"[^\d]", "");
                //costFrom = Regex.Replace(costFrom, @"[^\d]", "");



                _PCNWContext.EstCostDetails.Where(pc => pc.ProjId == recentPCNWProjectId).ExecuteDelete();

                
                    // Add new record
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

        ProcessEstCost(proj.EstCost);
        ProcessEstCost(proj.EstCost2);
        ProcessEstCost(proj.EstCost3);
        ProcessEstCost(proj.EstCost4);
        
    }
}