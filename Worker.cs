using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SyncRoutineWS.OCPCModel;
using SyncRoutineWS.PCNWModel;

namespace SyncRoutineWS
{
    public class Worker : BackgroundService
    {
        private static OCPCProjectDBContext _OCOCContext;
        private static PCNWProjectDBContext _PCNWContext;

        private int processCount = 0;
        private Timer? _timer;
        private int elapesedTime;
        private readonly IConfiguration _configuration;
        private string LogFileDirectory = @"G:\MyLogs\SyncRoutineLogs";
        private readonly ILogger<Worker> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private UserManager<IdentityUser> _userManager;
        private RoleManager<IdentityRole> _roleManager;

        public Worker(IServiceScopeFactory scopeFactory, ILogger<Worker> logger, OCPCProjectDBContext OCPCcont1, PCNWProjectDBContext PCNWcont2, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _OCOCContext = OCPCcont1;
            _PCNWContext = PCNWcont2;
            _scopeFactory = scopeFactory;
            
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            AppendMessageToFile(LogFileDirectory, "->>> OPERATION STARTED");
            if (DoWork != null)
            {
                TimerCallback doWork = DoWork;

                // Uncomment the following line to run the service immediately 
                //_timer = new Timer(doWork, null, TimeSpan.Zero, TimeSpan.FromDays(1));

                // Uncomment the following block to run the service at midnight (for production)                
                DateTime now = DateTime.Now;
                DateTime nextMidnight = now.Date.AddDays(1); 
                TimeSpan timeUntilMidnight = nextMidnight - now;
                _timer = new Timer(doWork, null, timeUntilMidnight, TimeSpan.FromDays(1));
                

                // Cancel the timer when the service is stopping
                _ = stoppingToken.Register(() => _timer?.Change(Timeout.Infinite, 0));
            }
            else
            {
                _logger.LogWarning("DoWork delegate is not initialized.");
            }

            await Task.CompletedTask;
        }


        private void DoWork(object state)
        {
            AppendMessageToFile(LogFileDirectory, "[ PROCESS " + ++processCount + " ]");
            AppendMessageToFile(LogFileDirectory, "->>> SYNC STARTED");

            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    _userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
                    _roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

                    // Test1 ->> Test2
                    AppendMessageToFile(LogFileDirectory, "->>> SYNC FROM OCPCLive - PCNWTest STARTED");

                    #region SYNC FROM OCPCLive - PCNWTest

                    //Member Sync code
                    var businessEntityEmails = _PCNWContext.BusinessEntities
                        .Select(be => be.BusinessEntityEmail)
                        .ToHashSet();

                    Func<string, int, bool> emailCheck = (email, syncStatus) =>
                        syncStatus == 1 && !businessEntityEmails.Contains(email);

                    var tblOCPCMember = (from mem in _OCOCContext.TblMembers
                                         join con in _OCOCContext.TblContacts
                                         on mem.Id equals con.Id
                                         where (con.SyncStatus == 1 && !businessEntityEmails.Contains(con.Email))
                                         || con.SyncStatus == 2
                                         select mem)
                                         .Take(1).OrderBy(m => m.Id)
                                         .AsNoTracking()
                                         .ToList();

                    var memberids = tblOCPCMember.Select(m => m.Id).ToList();

                    var tblOCPCContact = _OCOCContext.TblContacts.Where(con => (con.SyncStatus == 1 || con.SyncStatus == 2))
                        .AsNoTracking()
                        .ToList();

                    tblOCPCContact = tblOCPCContact.Where(m => memberids.Contains(m.Id)).ToList();
                    ProcessMemberFunctionality(tblOCPCMember, tblOCPCContact);

                   // project sync code

                    var tblProjects = _OCOCContext.TblProjects
                        .Where(proj => proj.SyncStatus == 1 || proj.SyncStatus == 2).Take(1)
                        .AsNoTracking()
                        .ToList();

                    var tblProjCounty = tblProjects.Any()
                        ? _OCOCContext.TblProjCounties
                            .Where(projCounty => projCounty.ProjId == tblProjects[0].ProjId &&
                                                 (projCounty.SyncStatus == 1 || projCounty.SyncStatus == 2))
                            .AsNoTracking()
                            .ToList()
                        : new List<TblProjCounty>();

                    ProcessProjectFunctionality(tblProjects, tblProjCounty);

                   // Query Arch Owners
                   var tblArch = _OCOCContext.TblArchOwners
                       .Where(arch => emailCheck(arch.Email, arch.SyncStatus) || arch.SyncStatus == 2)
                       .AsNoTracking()
                       .ToList();

                    var tblProArc = _OCOCContext.TblProjAos
                        .Where(po => po.SyncStatus == 1 || po.SyncStatus == 2)
                        .AsNoTracking()
                        .ToList();

                    ProcessArchOwnerFunctionality(tblArch, tblProArc);

                    var tblCont = _OCOCContext.TblContractors
                        .Where(cont => emailCheck(cont.Email, cont.SyncStatus) || cont.SyncStatus == 2)
                        .AsNoTracking()
                        .ToList();

                    var tblProCon = _OCOCContext.TblProjCons
                        .Where(pc => pc.SyncStatus == 1 || pc.SyncStatus == 2)
                        .AsNoTracking()
                        .ToList();

                    ProcessContractorFunctionality(tblCont, tblProCon);

                    var tblAddenda = _OCOCContext.TblAddenda
                        .Where(adden => adden.SyncStatus == 1 || adden.SyncStatus == 2)
                        .AsNoTracking()
                        .ToList();

                    ProcessAddendaFunctionality(tblAddenda);

                    #endregion SYNC FROM OCPCLive - PCNWTest

                    AppendMessageToFile(LogFileDirectory, "->>> SYNC FROM OCPCLive - PCNWTest COMPLETED");
                    Console.WriteLine("Sync Completed....");
                }
            }
            catch (Exception ex)
            {
                AppendMessageToFile(LogFileDirectory, ex.Message);
                if (ex.InnerException != null)
                    AppendMessageToFile(LogFileDirectory, ex.InnerException.ToString()); ;
            }

            //Log Entry and Success Message
            AppendMessageToFile(LogFileDirectory, "->>> SYNC COMPLETED");
            AppendMessageToFile(LogFileDirectory, "->>> OPERATION COMPLETED");
        }

        private void ProcessAddendaFunctionality(List<TblAddendum> tblAddenda)
        {
            int SuccessAddendaProcess = 0, FailAddendaProcess = 0;
            AppendMessageToFile(LogFileDirectory, "->> tblAddenda ITEMS COUNT: " + tblAddenda.Count);

            if (tblAddenda != null && tblAddenda.Count > 0)
            {
                foreach (TblAddendum adden in tblAddenda)
                {
                    try
                    {
                        Addendum propAddenda;
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
                                    //propAddenda.ParentFolder=
                                    //propAddenda.Deleted=
                                    //propAddenda.ParentId =;
                                    SyncStatus = 0,
                                    SyncAddendaId = adden.AddendaId
                                };
                                _ = _PCNWContext.Addenda.Add(propAddenda);
                            }
                            else
                            {
                                AppendMessageToFile(LogFileDirectory, "->> NO PROJECT FOUND FOR ADDENDA ID " + adden.AddendaId);
                            }
                        }
                        else if (adden.SyncStatus == 2)
                        {
                            propAddenda = (from adddenda in _PCNWContext.Addenda where adddenda.SyncAddendaId == adden.AddendaId select adddenda).FirstOrDefault();
                            if (propAddenda!=null)
                            {
                                propAddenda.AddendaNo = adden.AddendaNo;
                                propAddenda.MoreInfo = adden.MoreInfo;
                                propAddenda.InsertDt = adden.InsertDt;
                                propAddenda.MvwebPath = adden.MvwebPath;
                                propAddenda.IssueDt = adden.IssueDt;
                                propAddenda.PageCnt = adden.PageCnt;
                                propAddenda.NewBd = adden.NewBd;
                                _PCNWContext.Entry(propAddenda).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                            }
                        }
                        _ = _PCNWContext.SaveChanges();

                        adden.SyncStatus = 3;
                        _OCOCContext.Entry(adden).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                        _ = _OCOCContext.SaveChanges();

                        SuccessAddendaProcess++;
                    }
                    catch (Exception ecAddenda)
                    {
                        FailAddendaProcess++;
                        AppendMessageToFile(LogFileDirectory, "->> EXCEPTION OCCURED FOR ADDENDA ID " + adden.AddendaId + " IN ProcessAddendaFunctionality FUNCTION");
                        AppendMessageToFile(LogFileDirectory, "EXCEPTION MESSAGE: " + ecAddenda.Message);
                        if (ecAddenda.InnerException != null)
                            AppendMessageToFile(LogFileDirectory, "INNER EXCEPTION: " + ecAddenda.InnerException.ToString());
                    }
                }
                AppendMessageToFile(LogFileDirectory, "->> SUCCESSFUL ADDENDA PROCESSED: " + SuccessAddendaProcess + "\nFAILED ADDENDA PROCESSED: " + FailAddendaProcess);
            }
            else
            {
                AppendMessageToFile(LogFileDirectory, "->> NO ADDENDA FOUND IN tblAddenda");
            }
        }

        private void ProcessContractorFunctionality(List<TblContractor> tblCont, List<TblProjCon> tblProCon)
        {
            int SuccessContractorProcess = 0, FailContractorProcess = 0, SuccessPCProcess = 0, FailPCProcess = 0;
            AppendMessageToFile(LogFileDirectory, "->> tblkContractor ITEMS COUNT: " + tblCont.Count);
            AppendMessageToFile(LogFileDirectory, "->> tblProjCon ITEMS COUNT: " + tblProCon.Count);

            if (tblCont != null && tblCont.Count > 0)
            {
                foreach (TblContractor con in tblCont)
                {
                    try
                    {
                        int lastContractorBusinessEntityId = 0;
                        BusinessEntity propBussEnt;
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
                        }
                        else if (con.SyncStatus == 2)
                        {
                            propBussEnt = (from be in _PCNWContext.BusinessEntities where be.BusinessEntityName == con.Name select be).FirstOrDefault();
                            propBussEnt.BusinessEntityName = con.Name;
                            propBussEnt.BusinessEntityEmail = con.Email;
                            propBussEnt.BusinessEntityPhone = con.Phone;
                            _PCNWContext.Entry(propBussEnt).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                            _ = _PCNWContext.SaveChanges();
                            lastContractorBusinessEntityId = propBussEnt.BusinessEntityId;
                        }

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
                                //propAdd.AddressName = "";
                                SyncStatus = 0,
                                SyncConId = con.Id
                            };
                            _ = _PCNWContext.Addresses.Add(propAdd);
                        }
                        else if (con.SyncStatus == 2)
                        {
                            propAdd = (from addAO in _PCNWContext.Addresses where addAO.BusinessEntityId == lastContractorBusinessEntityId select addAO).FirstOrDefault();
                            propAdd.Addr1 = con.Addr1;
                            propAdd.City = con.City;
                            propAdd.State = con.State;
                            propAdd.Zip = con.Zip;
                            _PCNWContext.Entry(propAdd).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                        }
                        _ = _PCNWContext.SaveChanges();

                        List<TblProjCon> lstPrcON = (from filProjCon in tblProCon where filProjCon.ConId == con.Id select filProjCon).ToList();
                        if (lstPrcON.Count > 0)
                        {
                            SuccessPCProcess = 0;
                            FailPCProcess = 0;
                            foreach (TblProjCon tpc in lstPrcON)
                            {
                                try
                                {
                                    Entity propEnty;
                                    if (tpc.SyncStatus == 1)
                                    {
                                        if (tpc.ProjId != null)
                                        {
                                            var propProj = (from c in _PCNWContext.Projects where c.SyncProId == tpc.ProjId select c).FirstOrDefault();
                                            if (propProj != null)
                                            {
                                                propEnty = new()
                                                {
                                                    EnityName = con.Name,
                                                    //propEnty.EntityType =;
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
                                            }
                                            else
                                            {
                                                AppendMessageToFile(LogFileDirectory, "->> NO PROJECT FOUND FOR PROJ CONTRACTOR ID " + tpc.ProjConId);
                                            }
                                        }
                                        else
                                        {
                                            propEnty = new()
                                            {
                                                EnityName = con.Name,
                                                //propEnty.EntityType =;
                                                ProjId = null,
                                                ProjNumber = null,
                                                IsActive = null,
                                                NameId = lastContractorBusinessEntityId,
                                                ChkIssue = (bool)tpc.IssuingOffice,
                                                CompType = 2,
                                                SyncStatus = 0,
                                                SyncProjConId = tpc.ProjConId
                                            };
                                            _ = _PCNWContext.Entities.Add(propEnty);
                                        }
                                    }
                                    else if (tpc.SyncStatus == 2)
                                    {
                                        propEnty = (from ent in _PCNWContext.Entities where ent.SyncProjConId == tpc.ProjConId select ent).FirstOrDefault();
                                        propEnty.EnityName = con.Name;
                                        _PCNWContext.Entry(propEnty).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                                    }

                                    _ = _PCNWContext.SaveChanges();

                                    tpc.SyncStatus = 3;
                                    _OCOCContext.Entry(tpc).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                                    _ = _OCOCContext.SaveChanges();
                                    SuccessPCProcess++;
                                }
                                catch (Exception exProjCon)
                                {
                                    FailPCProcess++;
                                    AppendMessageToFile(LogFileDirectory, "->> EXCEPTION OCCURED FOR PROJ CONTRACTOR ID " + tpc.ProjConId + " IN ProcessContractorFunctionality FUNCTION");
                                    AppendMessageToFile(LogFileDirectory, "EXCEPTION MESSAGE: " + exProjCon.Message);
                                    if (exProjCon.InnerException != null)
                                        AppendMessageToFile(LogFileDirectory, "INNER EXCEPTION: " + exProjCon.InnerException.ToString());
                                }
                            }
                            AppendMessageToFile(LogFileDirectory, "->> SUCCESSFUL PROJ CONTRACTOR PROCESSED: " + SuccessContractorProcess + "\nFAILED PROJ CONTRACTOR PROCESSED: " + FailContractorProcess + " FOR CONTRACTOR ID: " + con.Id);
                        }
                        else
                        {
                            AppendMessageToFile(LogFileDirectory, "->> NO PROJ CON FOUND FOR CONTRACTOR ID: " + con.Id);
                        }
                        con.SyncStatus = 3;
                        _OCOCContext.Entry(con).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                        _ = _OCOCContext.SaveChanges();
                        SuccessContractorProcess++;
                    }
                    catch (Exception exContractor)
                    {
                        FailContractorProcess++;
                        AppendMessageToFile(LogFileDirectory, "->> EXCEPTION OCCURED FOR CONTRACTOR ID " + con.Id + " IN ProcessContractorFunctionality FUNCTION");
                        AppendMessageToFile(LogFileDirectory, "EXCEPTION MESSAGE: " + exContractor.Message);
                        if (exContractor.InnerException != null)
                            AppendMessageToFile(LogFileDirectory, "INNER EXCEPTION: " + exContractor.InnerException.ToString());
                    }
                }
                AppendMessageToFile(LogFileDirectory, "->> SUCCESSFUL CONTRACTOR PROCESSED: " + SuccessContractorProcess + "\nFAILED CONTRACTOR PROCESSED: " + FailContractorProcess);
            }
            else
            {
                AppendMessageToFile(LogFileDirectory, "->> NO CONTRACTOR FOUND IN tblContractor");
            }
        }

        private void ProcessArchOwnerFunctionality(List<TblArchOwner> tblArchOwner, List<TblProjAo> tblProArcOwn)
        {
            int SuccessAOProcess = 0, FailAOProcess = 0, SuccessPAOProcess = 0, FailPAOProcess = 0;
            AppendMessageToFile(LogFileDirectory, "->> tblArchOwner ITEMS COUNT: " + tblArchOwner.Count);
            AppendMessageToFile(LogFileDirectory, "->> tblProjAO ITEMS COUNT: " + tblProArcOwn.Count);

            if (tblArchOwner != null && tblArchOwner.Count > 0)
            {
                foreach (TblArchOwner archOw in tblArchOwner)
                {
                    try
                    {
                        int lastAOBusinessEntityId = 0;
                        BusinessEntity propBussEnt;
                        if (archOw.SyncStatus == 1)
                        {
                            propBussEnt = new()
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
                            _ = _PCNWContext.BusinessEntities.Add(propBussEnt);
                            _ = _PCNWContext.SaveChanges();

                            lastAOBusinessEntityId = propBussEnt.BusinessEntityId;
                        }
                        else if (archOw.SyncStatus == 2)
                        {
                            propBussEnt = (from be in _PCNWContext.BusinessEntities where be.BusinessEntityName == archOw.Name select be).FirstOrDefault();
                            propBussEnt.BusinessEntityName = archOw.Name;
                            propBussEnt.BusinessEntityEmail = archOw.Email;
                            propBussEnt.BusinessEntityPhone = archOw.Phone;
                            _PCNWContext.Entry(propBussEnt).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                            _ = _PCNWContext.SaveChanges();

                            lastAOBusinessEntityId = (int)propBussEnt.SyncAoid;
                        }

                        Address propAdd;
                        if (archOw.SyncStatus == 1)
                        {
                            propAdd = new()
                            {
                                BusinessEntityId = lastAOBusinessEntityId,
                                Addr1 = archOw.Addr1,
                                City = archOw.City,
                                State = archOw.State,
                                Zip = archOw.Zip,
                                //propAdd.AddressName = "";
                                SyncStatus = 0,
                                SyncAoid = archOw.Id
                            };
                            _ = _PCNWContext.Addresses.Add(propAdd);
                        }
                        else if (archOw.SyncStatus == 2)
                        {
                            propAdd = (from addAO in _PCNWContext.Addresses where addAO.BusinessEntityId == lastAOBusinessEntityId select addAO).FirstOrDefault();
                            propAdd.Addr1 = archOw.Addr1;
                            propAdd.City = archOw.City;
                            propAdd.State = archOw.State;
                            propAdd.Zip = archOw.Zip;
                            _PCNWContext.Entry(propAdd).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                        }
                        _ = _PCNWContext.SaveChanges();

                        List<TblProjAo> lstpao = (from filtAO in tblProArcOwn where filtAO.ArchOwnerId == archOw.Id select filtAO).ToList();
                        if (lstpao.Count > 0)
                        {
                            SuccessPAOProcess = 0;
                            FailPAOProcess = 0;
                            foreach (TblProjAo ProjAO in lstpao)
                            {
                                try
                                {
                                    Entity propEnty;
                                    if (ProjAO.SyncStatus == 1)
                                    {
                                        if (ProjAO.ProjId != null)
                                        {
                                            var propProj = (from c in _PCNWContext.Projects where c.SyncProId == ProjAO.ProjId select c).FirstOrDefault();
                                            if (propProj != null)
                                            {
                                                propEnty = new()
                                                {
                                                    EnityName = archOw.Name,
                                                    //propEnty.EntityType =;
                                                    ProjId = propProj.ProjId,
                                                    ProjNumber = Convert.ToInt32(propProj.ProjNumber),
                                                    IsActive = propProj.IsActive,
                                                    NameId = lastAOBusinessEntityId,
                                                    //propEnty.ChkIssue =ProjAO.
                                                    CompType = 3,
                                                    SyncStatus = 0,
                                                    SyncProjAoid = ProjAO.ArchOwnerId
                                                };
                                                _ = _PCNWContext.Entities.Add(propEnty);
                                            }
                                            else
                                            {
                                                AppendMessageToFile(LogFileDirectory, "->> NO PROJECT FOUND FOR PROJ ARCH OWNER ID " + ProjAO.ArchOwnerId);
                                            }
                                        }
                                        else
                                        {
                                            propEnty = new()
                                            {
                                                EnityName = archOw.Name,
                                                //propEnty.EntityType =;
                                                ProjId = null,
                                                ProjNumber = null,
                                                IsActive = null,
                                                NameId = lastAOBusinessEntityId,
                                                //propEnty.ChkIssue =;
                                                CompType = 3,
                                                SyncStatus = 0,
                                                SyncProjAoid = ProjAO.ArchOwnerId
                                            };
                                            _ = _PCNWContext.Entities.Add(propEnty);
                                        }
                                    }
                                    else if (ProjAO.SyncStatus == 2)
                                    {
                                        propEnty = (from ent in _PCNWContext.Entities where ent.SyncProjConId == ProjAO.ArchOwnerId select ent).FirstOrDefault();
                                        propEnty.EnityName = archOw.Name;
                                        _PCNWContext.Entry(propEnty).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                                    }
                                    _ = _PCNWContext.SaveChanges();

                                    ProjAO.SyncStatus = 3;
                                    _OCOCContext.Entry(ProjAO).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                                    _ = _OCOCContext.SaveChanges();
                                }
                                catch (Exception exProjAO)
                                {
                                    AppendMessageToFile(LogFileDirectory, "->> EXCEPTION OCCURED FOR PROJ AO ID " + ProjAO.ProjAo + " IN ProcessArchOwnerFunctionality FUNCTION");
                                    AppendMessageToFile(LogFileDirectory, "EXCEPTION MESSAGE: " + exProjAO.Message);
                                    if (exProjAO.InnerException != null)
                                        AppendMessageToFile(LogFileDirectory, "INNER EXCEPTION: " + exProjAO.InnerException.ToString());
                                    continue;
                                }
                            }
                            AppendMessageToFile(LogFileDirectory, "->> SUCCESSFUL PROJ AO PROCESSED: " + SuccessPAOProcess + "\nFAILED PROJ AO PROCESSED: " + FailPAOProcess + " FOR ARCH OWNER ID: " + archOw.Id);
                        }
                        else
                        {
                            AppendMessageToFile(LogFileDirectory, "->> NO PROJ AO FOUND FOR ARCH OWNER ID: " + archOw.Id);
                        }
                        archOw.SyncStatus = 3;
                        _OCOCContext.Entry(archOw).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                        _ = _OCOCContext.SaveChanges();
                    }
                    catch (Exception exAO)
                    {
                        FailAOProcess++;
                        AppendMessageToFile(LogFileDirectory, "->> EXCEPTION OCCURED FOR ARCHOWNER ID " + archOw.Id + " IN ProcessArchOwnerFunctionality FUNCTION");
                        AppendMessageToFile(LogFileDirectory, "EXCEPTION MESSAGE: " + exAO.Message);
                        if (exAO.InnerException != null)
                            AppendMessageToFile(LogFileDirectory, "INNER EXCEPTION: " + exAO.InnerException.ToString());
                        continue;
                    }

                    SuccessAOProcess++;
                }
                AppendMessageToFile(LogFileDirectory, "->> SUCCESSFUL ARCH OWNER PROCESSED: " + SuccessAOProcess + "\nFAILED ARCH OWNER PROCESSED: " + FailAOProcess);
            }
            else
            {
                AppendMessageToFile(LogFileDirectory, "->> NO ARCH OWNER FOUND IN tblArchOwner");
            }
        }

        private void ProcessProjectFunctionality(List<TblProject> tblProjects, List<TblProjCounty> tblProjCounty)
        {
            int SuccessCountyProcess = 0, FailCountytProcess = 0, SuccessProjectProcess = 0, FailProjectProcess = 0, SuccessProjCountyProcess = 0, FailProjCountytProcess = 0;
            AppendMessageToFile(LogFileDirectory, "->> tblProjects ITEMS COUNT: " + tblProjects.Count);
            AppendMessageToFile(LogFileDirectory, "->> tblProjCounty ITEMS COUNT: " + tblProjCounty.Count);

            if (tblProjects != null)
            {
                foreach (TblProject proj in tblProjects)
                {
                    try
                    {
                        Project propProject;
                        int RecentProjectId = 0;

                        var data = _PCNWContext.Projects.AsNoTracking().FirstOrDefault(m => m.SyncProId == proj.ProjId);

                        if (data != null)
                        {
                            var record = _OCOCContext.TblProjects.AsNoTracking().FirstOrDefault(m => m.ProjId == proj.ProjId);
                            record.SyncStatus = 3;
                            _OCOCContext.Entry(record).State = EntityState.Modified;
                            _ = _OCOCContext.SaveChanges();
                            continue;
                        }

                        if (proj.SyncStatus == 1)
                        {
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
                            _ = _PCNWContext.Projects.Add(propProject);
                            _ = _PCNWContext.SaveChanges();

                            RecentProjectId = (from pro in _PCNWContext.Projects select pro.ProjId).Max();
                        }
                        else
                        {
                            propProject = (from pro in _PCNWContext.Projects where pro.SyncProId == proj.ProjId select pro).FirstOrDefault();
                            propProject.AdSpacer = proj.AdSpacer;
                            propProject.ArrivalDt = proj.ArrivalDt;
                            //propProject.BackProjNumber = proj.BackProjNumber;
                            propProject.BendPc = proj.BendPc;
                            propProject.BidBond = proj.BidBond;
                            propProject.BidDt = proj.BidDt;
                            propProject.BidDt2 = proj.BidDt2;
                            propProject.BidDt3 = proj.BidDt3;
                            propProject.BidDt4 = proj.BidDt4;
                            //propProject.BidDt5 = proj.BidDt5;
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
                            //propProject.GeogPt=proj.geog
                            propProject.Hold = proj.Hold;
                            propProject.ImportDt = proj.ImportDt;
                            //propProject.IndexPDFFiles = proj.IndexPDFFiles;
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
                            _PCNWContext.Entry(propProject).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                            _ = _PCNWContext.SaveChanges();

                            RecentProjectId = (int)propProject.SyncProId;
                        }

                        AppendMessageToFile(LogFileDirectory, "->> tblProject PROJECT ID " + proj.ProjId + " SYNC STATUS UPDATED");

                        Project proje = (from project in _PCNWContext.Projects where project.ProjId == RecentProjectId select project).FirstOrDefault();
                        AppendMessageToFile(LogFileDirectory, "->> PROJECT ID " + proj.ProjId + " PROCESSING WITH RECENT PROJECT ID: " + RecentProjectId);

                        List<TblProjCounty> lstpc = (from prCou in tblProjCounty where prCou.ProjId == proje.SyncProId select prCou).ToList();
                        AppendMessageToFile(LogFileDirectory, "->> tblProjCounty ITEMS COUNT: " + tblProjCounty.Count);
                        SuccessProjCountyProcess = 0;
                        FailProjCountytProcess = 0;
                        foreach (TblProjCounty pc in lstpc)
                        {
                            try
                            {
                                ProjCounty projCounty;
                                if (pc.SyncStatus == 1)
                                {
                                    var county = (from c in _PCNWContext.Counties where c.CountyId == pc.CountyId select c).FirstOrDefault();
                                    if (county != null)
                                    {
                                        projCounty = new ProjCounty();
                                        projCounty.ProjId = RecentProjectId;
                                        projCounty.CountyId = pc.CountyId;
                                        projCounty.SyncStatus = 0;
                                        projCounty.SyncProCouId = pc.ProjCountyId;
                                        _ = _PCNWContext.ProjCounties.Add(projCounty);
                                    }
                                    else
                                    {
                                        AppendMessageToFile(LogFileDirectory, "->> NO COUNTY FOUND FOR PROJ COUNTY ID: " + pc.ProjCountyId);
                                    }
                                }
                                else if (pc.SyncStatus == 2)
                                {
                                    projCounty = (from procou in _PCNWContext.ProjCounties where procou.SyncProCouId == pc.ProjCountyId select procou).FirstOrDefault();
                                    projCounty.ProjId = RecentProjectId;
                                    projCounty.CountyId = pc.CountyId;
                                    _PCNWContext.Entry(projCounty).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                                }
                                _ = _PCNWContext.SaveChanges();

                                pc.SyncStatus = 3;
                                _OCOCContext.Entry(pc).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                                _ = _OCOCContext.SaveChanges();
                                AppendMessageToFile(LogFileDirectory, "->> tblProjCounty PROJ COUNTY ID " + pc.ProjCountyId + " SYNC STATUS UPDATED");
                            }
                            catch (Exception exProjCounty)
                            {
                                FailProjCountytProcess++;
                                AppendMessageToFile(LogFileDirectory, "->> EXCEPTION OCCURED FOR PROJ COUNTY ID " + pc.CountyId + " IN ProcessProjectFunctionality FUNCTION");
                                AppendMessageToFile(LogFileDirectory, "EXCEPTION MESSAGE: " + exProjCounty.Message);
                                if (exProjCounty.InnerException != null)
                                    AppendMessageToFile(LogFileDirectory, "INNER EXCEPTION: " + exProjCounty.InnerException.ToString());
                                continue;
                            }
                            SuccessProjCountyProcess++;
                        }
                        AppendMessageToFile(LogFileDirectory, "->> PROCESSED " + SuccessProjCountyProcess + " SUCCESSFUL AND " + FailProjCountytProcess + " FAILED PROJ COUNTY FOR PROJECT ID " + proj.ProjId);

                        if (proj.SyncStatus == 1)
                        {
                            if (proj.PreBidDt != null)
                            {
                                var PreBidDt = (DateTime)proj.PreBidDt;
                                PreBidInfo propPBI = new PreBidInfo
                                {
                                    PreBidDate = PreBidDt.Date,
                                    PreBidTime = PreBidDt.ToString("HH:mm"),
                                    Location = proj.PreBidLoc,
                                    PreBidAnd = (bool)proj.PrebidAnd,
                                    ProjId = RecentProjectId,
                                    UndecidedPreBid = false,
                                    Pst = "PT",
                                    SyncStatus = 0
                                };
                                _ = _PCNWContext.PreBidInfos.Add(propPBI);
                            }

                            if (proj.PreBidDt2 != null)
                            {
                                var PreBidDt = (DateTime)proj.PreBidDt2;
                                PreBidInfo propPBI2 = new PreBidInfo
                                {
                                    PreBidDate = PreBidDt.Date,
                                    PreBidTime = PreBidDt.ToString("HH:mm"),
                                    Location = proj.PreBidLoc2,
                                    Pst = "PT",
                                    PreBidAnd = (bool)proj.PrebidAnd,
                                    ProjId = RecentProjectId,
                                    UndecidedPreBid = null,
                                    SyncStatus = 0
                                };
                                _ = _PCNWContext.PreBidInfos.Add(propPBI2);
                            }
                            if (proj.EstCost != null && !string.Equals(proj.EstCost, "N/A", StringComparison.OrdinalIgnoreCase))
                            {
                                EstCostDetail propECD = new();
                                if (proj.EstCost.Contains("-"))
                                {
                                    var costs = proj.EstCost.Split('-');
                                    propECD.EstCostTo = costs[0].Trim().Replace("$", "");
                                    propECD.EstCostFrom = costs[1].Trim().Replace("$", "");
                                }
                                else
                                {
                                    propECD.EstCostTo = proj.EstCost.Trim().Replace("$", "");
                                }
                                propECD.Description = null;
                                propECD.ProjId = RecentProjectId;
                                propECD.Removed = false;
                                propECD.RangeSign = "=";
                                propECD.SyncStatus = 0;
                                _ = _PCNWContext.EstCostDetails.Add(propECD);
                            }
                            if (proj.EstCost2 != null && !string.Equals(proj.EstCost2, "N/A", StringComparison.OrdinalIgnoreCase))
                            {
                                EstCostDetail propECD2 = new();
                                if (proj.EstCost2.Contains('-'))
                                {
                                    var costs = proj.EstCost.Split('-');
                                    propECD2.EstCostTo = costs[0].Trim().Replace("$", "");
                                    propECD2.EstCostFrom = costs[1].Trim().Replace("$", "");
                                }
                                else
                                {
                                    propECD2.EstCostTo = proj.EstCost2.Trim().Replace("$", "");
                                }
                                propECD2.Description = null;
                                propECD2.ProjId = RecentProjectId;
                                propECD2.Removed = false;
                                propECD2.RangeSign = "=";
                                propECD2.SyncStatus = 0;
                                _ = _PCNWContext.EstCostDetails.Add(propECD2);
                            }

                            if (proj.EstCost3 != null && !string.Equals(proj.EstCost3, "N/A", StringComparison.OrdinalIgnoreCase))
                            {
                                EstCostDetail propECD3 = new();
                                if (proj.EstCost3.Contains('-'))
                                {
                                    var costs = proj.EstCost.Split('-');
                                    propECD3.EstCostTo = costs[0].Trim().Replace("$", "");
                                    propECD3.EstCostFrom = costs[1].Trim().Replace("$", "");
                                }
                                else
                                {
                                    propECD3.EstCostTo = proj.EstCost3.Trim().Replace("$", "");
                                }
                                propECD3.Description = null;
                                propECD3.ProjId = RecentProjectId;
                                propECD3.Removed = false;
                                propECD3.RangeSign = "=";
                                propECD3.SyncStatus = 0;
                                _ = _PCNWContext.EstCostDetails.Add(propECD3);
                            }

                            if (proj.EstCost4 != null && !string.Equals(proj.EstCost4, "N/A", StringComparison.OrdinalIgnoreCase))
                            {
                                EstCostDetail propECD4 = new();
                                if (proj.EstCost4.Contains('-'))
                                {
                                    var costs = proj.EstCost.Split('-');
                                    propECD4.EstCostTo = costs[0].Trim().Replace("$", "");
                                    propECD4.EstCostFrom = costs[1].Trim().Replace("$", "");
                                }
                                else
                                {
                                    propECD4.EstCostTo = proj.EstCost4.Trim().Replace("$", "");
                                }
                                propECD4.Description = null;
                                propECD4.ProjId = RecentProjectId;
                                propECD4.Removed = false;
                                propECD4.RangeSign = "=";
                                propECD4.SyncStatus = 0;
                                _ = _PCNWContext.EstCostDetails.Add(propECD4);
                            }

                            _ = _PCNWContext.SaveChanges();
                        }
                        proj.SyncStatus = 3;
                        _OCOCContext.Entry(proj).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                        _ = _OCOCContext.SaveChanges();
                    }
                    catch (Exception exProject)
                    {
                        FailProjectProcess++;
                        AppendMessageToFile(LogFileDirectory, "->> EXCEPTION OCCURED FOR PROJECT ID " + proj.ProjId + " IN ProcessProjectFunctionality FUNCTION");
                        AppendMessageToFile(LogFileDirectory, "EXCEPTION MESSAGE: " + exProject.Message);
                        if (exProject.InnerException != null)
                            AppendMessageToFile(LogFileDirectory, "INNER EXCEPTION: " + exProject.InnerException.ToString());
                        continue;
                    }
                    SuccessProjectProcess++;
                }
                AppendMessageToFile(LogFileDirectory, "->> SUCCESSFUL PROJECT PROCESSED: " + SuccessProjectProcess + "\nFAILED COUNTY PROCESSED: " + FailProjectProcess);
            }
        }

        private void ProcessMemberFunctionality(List<TblMember> tblOCPCMember, List<TblContact> tblOCPCContact)
        {
            int SuccessMemberProcess = 0, FailMemberProcess = 0;
            AppendMessageToFile(LogFileDirectory, "->> tblMember ITEMS COUNT: " + tblOCPCContact.Count);
            AppendMessageToFile(LogFileDirectory, "->> tblContact ITEMS COUNT: " + tblOCPCContact.Count);

            if (tblOCPCMember != null && tblOCPCMember.Count > 0)
            {
                int lastBusinessEntityId = 0;
                foreach (var member in tblOCPCMember)
                {
                    try
                    {
                        BusinessEntity propBussEnt;
                        switch (member.SyncStatus)
                        {
                            case 1:
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
                                    _ = _PCNWContext.BusinessEntities.Add(propBussEnt);
                                    _ = _PCNWContext.SaveChanges();

                                    lastBusinessEntityId = (from BId in _PCNWContext.BusinessEntities select BId.BusinessEntityId).Max();
                                    break;
                                }

                            case 2:
                                {
                                    propBussEnt = (from be in _PCNWContext.BusinessEntities where be.BusinessEntityName == member.Company select be).FirstOrDefault();
                                    propBussEnt.BusinessEntityName = member.Company;
                                    propBussEnt.BusinessEntityEmail = member.Email;
                                    _PCNWContext.Entry(propBussEnt).State = EntityState.Modified;
                                    _ = _PCNWContext.SaveChanges();

                                    lastBusinessEntityId = propBussEnt.BusinessEntityId;
                                    break;
                                }

                            default:
                                break;
                        }

                        AppendMessageToFile(LogFileDirectory, "->> MEMBER ID " + member.Id + " PROCESSING WITH BUSINESS ENTITY ID: " + lastBusinessEntityId);

                        //MEMBER SYNCHRONIZATION
                        SyncOCPC_MemberToPCNW_Member_Address(lastBusinessEntityId, member);

                        member.SyncStatus = 3;
                         _OCOCContext.Entry(member).State = EntityState.Modified;
                        _ = _OCOCContext.SaveChanges();
                        AppendMessageToFile(LogFileDirectory, "->> tblMember MEMBER ID " + member.Id + " SYNC STATUS UPDATED");
                        var contacts = tblOCPCContact.Where(m =>
                        {
                            return m.Id == member.Id && (m.SyncStatus == 1 || m.SyncStatus == 2);
                        });
                        foreach (var contact in contacts)
                        {
                            if (contact != null)
                            {
                                Contact propCont;

                                switch (contact.SyncStatus)
                                {
                                    case 1:
                                        {
                                            Guid userid = new();
                                            bool mainContactExists = true;
                                            if (!string.IsNullOrEmpty(contact.Email) && !string.IsNullOrEmpty(contact.Password))
                                            {
                                                var user = new IdentityUser
                                                {
                                                    Email = contact.Email,
                                                    UserName = contact.Email
                                                };

                                                var result = _userManager.CreateAsync(user, contact.Password).GetAwaiter().GetResult();
                                                if (!result.Succeeded)
                                                {
                                                    throw new Exception(message: result.Errors.ToString());
                                                }

                                                var addrole = _userManager.AddToRoleAsync(user, "Member").GetAwaiter().GetResult();
                                                if (!addrole.Succeeded)
                                                {
                                                    throw new Exception(addrole.Errors.ToString());
                                                }
                                                if (!Guid.TryParse(user.Id, out userid))
                                                {
                                                    throw new Exception($"User ID '{user.Id}' is not a valid GUID.");
                                                }

                                                mainContactExists = _PCNWContext.Contacts.Any(c => c.BusinessEntityId == lastBusinessEntityId && c.MainContact == true);
                                            }
                                            propCont = new Contact
                                            {
                                                UserId = userid,
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
                                            _ = _PCNWContext.Contacts.Add(propCont);
                                            break;
                                        }

                                    case 2:
                                        {
                                            propCont = (from con in _PCNWContext.Contacts where con.BusinessEntityId == lastBusinessEntityId select con).FirstOrDefault();
                                            propCont.ContactName = contact.Contact;
                                            propCont.ContactEmail = contact.Email;
                                            propCont.ContactPhone = contact.Phone;
                                            propCont.ContactTitle = contact.Title;
                                            _PCNWContext.Entry(propCont).State = EntityState.Modified;
                                            break;
                                        }
                                }
                                _ = _PCNWContext.SaveChanges();

                                AppendMessageToFile(LogFileDirectory, "->> MEMBER ID " + member.Id + " SUCCESSFUL PROCESSED FOR CONTACT ID " + contact.ConId + " WITH BUSINESS ENTITY ID: " + lastBusinessEntityId);

                                contact.SyncStatus = 3;
                                _OCOCContext.Entry(contact).State = EntityState.Modified;
                                _ = _OCOCContext.SaveChanges();
                                AppendMessageToFile(LogFileDirectory, "->> tblContact CONTACT ID " + contact.ConId + " SYNC STATUS UPDATED");
                            }
                            else
                            {
                                AppendMessageToFile(LogFileDirectory, "->> NO CONTACT FOUND FOR MEMBER ID " + member.Id + " IN LIVE DATABASE");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        FailMemberProcess++;
                        AppendMessageToFile(LogFileDirectory, "->> EXCEPTION OCCURED FOR MEMBER ID " + member.Id + " IN ProcessMemberFunctionality FUNCTION");
                        AppendMessageToFile(LogFileDirectory, "EXCEPTION MESSAGE: " + ex.Message);
                        if (ex.InnerException != null)
                            AppendMessageToFile(LogFileDirectory, "INNER EXCEPTION: " + ex.InnerException.ToString());
                        continue;
                    }
                    SuccessMemberProcess++;
                }
                AppendMessageToFile(LogFileDirectory, "->> SUCCESSFUL MEMBER PROCESSED: " + SuccessMemberProcess + "\nFAILED MEMBERS PROCESSED: " + FailMemberProcess);
            }
            else
            {
                AppendMessageToFile(LogFileDirectory, "->> NO MEMBER FOUND IN OCPC LIVE DATABASE");
            }
        }

        private void SyncOCPC_MemberToPCNW_Member_Address(int lastBusinessEntityId, TblMember member)
        {
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
                _PCNWContext.Entry(propMem).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
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
                _PCNWContext.Entry(propAdd).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
            }
            _ = _PCNWContext.SaveChanges();
            AppendMessageToFile(LogFileDirectory, "->> MEMBER ID " + member.Id + " SUCCESSFUL PROCESSED FOR MEMBER AND ADDRESS WITH BUSINESS ENTITY ID: " + lastBusinessEntityId);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Worker Service is stopping.");
            _timer?.Change(Timeout.Infinite, 0);
            return base.StopAsync(cancellationToken);
        }

        private static void AppendMessageToFile(string LogFileDirectory, string message)
        {
            string LogFileFullPath = Path.Combine(LogFileDirectory, "Log-" + DateTime.Now.ToString("MM-dd-yyyy") + ".txt");
            try
            {
                // Ensure directory exists
                if (!Directory.Exists(LogFileDirectory))
                {
                    _ = Directory.CreateDirectory(LogFileDirectory);
                }

                // Append message to file, create file if it doesn't exist
                using (FileStream fs = new FileStream(LogFileFullPath, FileMode.Append, FileAccess.Write))
                {
                    using (StreamWriter writer = new StreamWriter(fs))
                    {
                        if (message.Contains("PROCESS ") || message.Contains("ITEM "))
                            writer.WriteLine(message);
                        else if (message.Contains("OPERATION COMPLETED"))
                            writer.WriteLine(message + " [" + DateTime.Now + "]\n");
                        else
                            writer.WriteLine(message + " [" + DateTime.Now + "]");
                    }
                }
            }
            catch (Exception ex)
            {
                using FileStream fs = new(LogFileFullPath, FileMode.Append, FileAccess.Write);
                using StreamWriter writer = new(fs);
                writer.WriteLine("\nEXCEPTION OCCURED - " + ex.Message + " [" + DateTime.Now + "]");
                //_logger.LogError(ex, "An error occurred while writing to the file.");
            }
        }
    }
}