using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Identity.Client;
using SyncRoutineWS.OCPCModel;
using SyncRoutineWS.PCNWModel;
using System.Diagnostics.Metrics;
using System.Numerics;
using System.Reflection.PortableExecutable;
using System.Runtime.Intrinsics.X86;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
        private string LogFileDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
        private readonly ILogger<Worker> _logger;

        public Worker(ILogger<Worker> logger, OCPCProjectDBContext OCPCcont1, PCNWProjectDBContext PCNWcont2, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
            _OCOCContext = OCPCcont1;
            _PCNWContext = PCNWcont2;

            if (configuration != null)
            {
                if (!int.TryParse(configuration["minutes"], out elapesedTime))
                {
                    elapesedTime = 10;
                }
                else
                {
                    elapesedTime = Convert.ToInt32(configuration["minutes"]);
                }
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (DoWork != null)
            {
                TimerCallback doWork = DoWork;
                _timer = new Timer(doWork, null, TimeSpan.Zero, TimeSpan.FromMinutes(elapesedTime));
                stoppingToken.Register(() => _timer?.Change(Timeout.Infinite, 0));
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
            AppendMessageToFile(LogFileDirectory, "->>> OPERATION STARTED");
            AppendMessageToFile(LogFileDirectory, "->> SYNC STARTED");

            try
            {
                // Test1 ->> Test2
                AppendMessageToFile(LogFileDirectory, "->> SYNC FROM OCPCLive - PCNWTest STARTED");

                #region SYNC FROM OCPCLive - PCNWTest

                List<TblMember> tblOCPCMember = (from mem in _OCOCContext.TblMembers where mem.SyncStatus == 1 || mem.SyncStatus == 2 select mem).ToList();
                List<TblContact> tblOCPCContact = (from con in _OCOCContext.TblContacts where con.SyncStatus == 1 || con.SyncStatus == 2 select con).ToList();
                ProcessMembberFunctionality(tblOCPCMember, tblOCPCContact);

                List<TblProject> tblProjects = (from proj in _OCOCContext.TblProjects where proj.ProjId == 237933 && (proj.SyncStatus == 1 || proj.SyncStatus == 2) select proj).ToList();
                List<TblProjCounty> tblProjCounty = (from projCouny in _OCOCContext.TblProjCounties where projCouny.ProjId == tblProjects[0].ProjId && (projCouny.SyncStatus == 1 || projCouny.SyncStatus == 2) select projCouny).ToList();
                //List<TblCounty> tblCounty = (from county in _OCOCContext.TblCounties where (county.CountyId == 3 || county.CountyId == 34) && county.SyncStatus == 1 || county.SyncStatus == 2 select county).ToList();
                //List<TblCityCounty> tblCityCounty = (from citycounty in _OCOCContext.TblCityCounties where (citycounty.CountyId == 3 || citycounty.CountyId == 34) && citycounty.SyncStatus == 1 || citycounty.SyncStatus == 2 select citycounty).ToList();
                ProcessProjectFunctionality(tblProjects, tblProjCounty);

                List<TblArchOwner> tblArch = (from arch in _OCOCContext.TblArchOwners where arch.Id == 11314 && arch.SyncStatus == 1 || arch.SyncStatus == 2 select arch).ToList();
                List<TblProjAo> tblProArc = (from po in _OCOCContext.TblProjAos where po.ArchOwnerId == 11314 && po.SyncStatus == 1 || po.SyncStatus == 2 select po).ToList();
                ProcessArchOwnerFunctionality(tblArch, tblProArc);

                List<TblContractor> tblCont = (from Contt in _OCOCContext.TblContractors where Contt.Id == 40779 && Contt.SyncStatus == 1 || Contt.SyncStatus == 2 select Contt).ToList();
                List<TblProjCon> tblProCon = (from pc in _OCOCContext.TblProjCons where pc.ConId == 40779 && pc.SyncStatus == 1 || pc.SyncStatus == 2 select pc).ToList();
                ProcessContractorFunctionality(tblCont, tblProCon);

                List<TblAddendum> tblAddenda = (from adden in _OCOCContext.TblAddenda where adden.SyncStatus == 1 || adden.SyncStatus == 2 select adden).ToList();
                ProcessAddendaFunctionality(tblAddenda);

                #endregion SYNC FROM OCPCLive - PCNWTest

                AppendMessageToFile(LogFileDirectory, "->> SYNC FROM OCPCLive - PCNWTest COMPLETED");
            }
            catch (Exception ex)
            {
                AppendMessageToFile(LogFileDirectory, ex.Message);
                if (ex.InnerException != null)
                    AppendMessageToFile(LogFileDirectory, ex.InnerException.ToString()); ;
            }

            //Log Entry and Success Message
            AppendMessageToFile(LogFileDirectory, "->> SYNC COMPLETED");
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
                                propAddenda = new Addendum();
                                propAddenda.AddendaNo = adden.AddendaNo;
                                propAddenda.MoreInfo = adden.MoreInfo;
                                propAddenda.ProjId = propProj.SyncProId;
                                propAddenda.InsertDt = adden.InsertDt;
                                propAddenda.MvwebPath = adden.MvwebPath;
                                propAddenda.IssueDt = adden.IssueDt;
                                propAddenda.PageCnt = adden.PageCnt;
                                propAddenda.NewBd = adden.NewBd;
                                //propAddenda.ParentFolder=
                                //propAddenda.Deleted=
                                //propAddenda.ParentId =;
                                propAddenda.SyncStatus = 0;
                                propAddenda.SyncAddendaId = adden.AddendaId;
                                _PCNWContext.Addenda.Add(propAddenda);
                            }
                            else
                            {
                                AppendMessageToFile(LogFileDirectory, "->> NO PROJECT FOUND FOR ADDENDA ID " + adden.AddendaId);
                            }
                        }
                        else if (adden.SyncStatus == 2)
                        {
                            propAddenda = (from adddenda in _PCNWContext.Addenda where adddenda.SyncAddendaId == adden.AddendaId select adddenda).FirstOrDefault();
                            propAddenda.AddendaNo = adden.AddendaNo;
                            propAddenda.MoreInfo = adden.MoreInfo;
                            propAddenda.InsertDt = adden.InsertDt;
                            propAddenda.MvwebPath = adden.MvwebPath;
                            propAddenda.IssueDt = adden.IssueDt;
                            propAddenda.PageCnt = adden.PageCnt;
                            propAddenda.NewBd = adden.NewBd;
                            _PCNWContext.Entry(propAddenda).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                        }
                        _PCNWContext.SaveChanges();

                        adden.SyncStatus = 3;
                        _OCOCContext.Entry(adden).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                        _OCOCContext.SaveChanges();

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
                            propBussEnt = new BusinessEntity();
                            propBussEnt.BusinessEntityName = con.Name;
                            propBussEnt.BusinessEntityEmail = con.Email;
                            propBussEnt.BusinessEntityPhone = con.Phone;
                            propBussEnt.IsMember = false;
                            propBussEnt.IsContractor = true;
                            propBussEnt.IsArchitect = false;
                            propBussEnt.OldMemId = 0;
                            propBussEnt.OldConId = con.Id;
                            propBussEnt.OldAoId = 0;
                            propBussEnt.SyncStatus = 0;
                            propBussEnt.SyncConId = con.Id;
                            _PCNWContext.BusinessEntities.Add(propBussEnt);
                            _PCNWContext.SaveChanges();
                            lastContractorBusinessEntityId = (from BId in _PCNWContext.BusinessEntities select BId.BusinessEntityId).Max();
                        }
                        else if (con.SyncStatus == 2)
                        {
                            propBussEnt = (from be in _PCNWContext.BusinessEntities where be.BusinessEntityName == con.Name select be).FirstOrDefault();
                            propBussEnt.BusinessEntityName = con.Name;
                            propBussEnt.BusinessEntityEmail = con.Email;
                            propBussEnt.BusinessEntityPhone = con.Phone;
                            _PCNWContext.Entry(propBussEnt).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                            _PCNWContext.SaveChanges();
                            lastContractorBusinessEntityId = propBussEnt.BusinessEntityId;
                        }

                        //lastContractorBusinessEntityId = (from BId in _PCNWContext.BusinessEntities select BId.BusinessEntityId).Max();
                        Address propAdd;
                        if (con.SyncStatus == 1)
                        {
                            propAdd = new();
                            propAdd.BusinessEntityId = lastContractorBusinessEntityId;
                            propAdd.Addr1 = con.Addr1;
                            propAdd.City = con.City;
                            propAdd.State = con.State;
                            propAdd.Zip = con.Zip;
                            //propAdd.AddressName = "";
                            propAdd.SyncStatus = 0;
                            propAdd.SyncConId = con.Id;
                            _PCNWContext.Addresses.Add(propAdd);
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
                        _PCNWContext.SaveChanges();

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
                                                propEnty = new();
                                                propEnty.EnityName = con.Name;
                                                //propEnty.EntityType =;
                                                propEnty.ProjId = propProj.ProjId;
                                                propEnty.ProjNumber = Convert.ToInt32(propProj.ProjNumber);
                                                propEnty.IsActive = propProj.IsActive;
                                                propEnty.NameId = lastContractorBusinessEntityId;
                                                propEnty.ChkIssue = (bool)tpc.IssuingOffice;
                                                propEnty.CompType = 2;
                                                propEnty.BusinessEntityId = lastContractorBusinessEntityId;
                                                propEnty.SyncStatus = 0;
                                                propEnty.SyncProjConId = tpc.ProjConId;
                                                _PCNWContext.Entities.Add(propEnty);
                                            }
                                            else
                                            {
                                                AppendMessageToFile(LogFileDirectory, "->> NO PROJECT FOUND FOR PROJ CONTRACTOR ID " + tpc.ProjConId);
                                            }
                                        }
                                        else
                                        {
                                            propEnty = new();
                                            propEnty.EnityName = con.Name;
                                            //propEnty.EntityType =;
                                            propEnty.ProjId = null;
                                            propEnty.ProjNumber = null;
                                            propEnty.IsActive = null;
                                            propEnty.NameId = lastContractorBusinessEntityId;
                                            propEnty.ChkIssue = (bool)tpc.IssuingOffice;
                                            propEnty.CompType = 2;
                                            propEnty.SyncStatus = 0;
                                            propEnty.SyncProjConId = tpc.ProjConId;
                                            _PCNWContext.Entities.Add(propEnty);
                                        }
                                    }
                                    else if (tpc.SyncStatus == 2)
                                    {
                                        propEnty = (from ent in _PCNWContext.Entities where ent.SyncProjConId == tpc.ProjConId select ent).FirstOrDefault();
                                        propEnty.EnityName = con.Name;
                                        _PCNWContext.Entry(propEnty).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                                    }

                                    //            ProjCon propProjCon;
                                    //            if (tpc.SyncStatus == 1)
                                    //            {
                                    //                var propProj = (from c in _PCNWContext.Projects where c.SyncProId == tpc.ProjId select c).FirstOrDefault();
                                    //                if (propProj != null)
                                    //                {
                                    //                    propProjCon = new ProjCon();
                                    //                    propProjCon.ConId = lastContractorBusinessEntityId;
                                    //                    propProjCon.ConTypeId = tpc.ConTypeId;
                                    //                    propProjCon.BidAmt = tpc.BidAmt;
                                    //                    propProjCon.Rank = tpc.Rank;
                                    //                    propProjCon.AwardedTo = tpc.AwardedTo;
                                    //                    propProjCon.Apparent = tpc.Apparent;
                                    //                    propProjCon.IssuingOffice = tpc.IssuingOffice;
                                    //                    propProjCon.SubBid = tpc.SubBid;
                                    //                    propProjCon.Ucpwd = tpc.Ucpwd;
                                    //                    propProjCon.PrivatePwd = tpc.PrivatePwd;
                                    //                    propProjCon.HostingPublic = tpc.HostingPublic;
                                    //                    propProjCon.HostingDateExtention = tpc.HostingDateExtention;
                                    //                    propProjCon.Note = tpc.Note;
                                    //                    propProjCon.ProjId = propProj.ProjId;
                                    //                    propProjCon.SortOrder = tpc.SortOrder;
                                    //                    propProjCon.BidDt = tpc.BidDt;
                                    //                    propProjCon.Bidding = tpc.Bidding;
                                    //                    propProjCon.Lm = tpc.Lm;
                                    //                    propProjCon.NotBidding = tpc.NotBidding;
                                    //                    propProjCon.Person = tpc.Person;
                                    //                    propProjCon.TimeStamp = tpc.TimeStamp;
                                    //                    propProjCon.SyncStatus = 0;
                                    //                    propProjCon.SyncProjConId = tpc.ProjConId;
                                    //                    _PCNWContext.ProjCons.Add(propProjCon);
                                    //                }
                                    //                else
                                    //                {
                                    //                    AppendMessageToFile(LogFileDirectory, "->> NO PROJECT FOUND FOR PROJ CONTRACTOR ID " + tpc.ProjConId);
                                    //                }
                                    //            }
                                    //            else if (tpc.SyncStatus == 2)
                                    //            {
                                    //                propProjCon = (from conttt in _PCNWContext.ProjCons where conttt.SyncProjConId == tpc.ProjConId select conttt).FirstOrDefault();
                                    //                propProjCon.ConTypeId = tpc.ConTypeId;
                                    //                propProjCon.BidAmt = tpc.BidAmt;
                                    //                propProjCon.Rank = tpc.Rank;
                                    //                propProjCon.AwardedTo = tpc.AwardedTo;
                                    //                propProjCon.Apparent = tpc.Apparent;
                                    //                propProjCon.IssuingOffice = tpc.IssuingOffice;
                                    //                propProjCon.SubBid = tpc.SubBid;
                                    //                propProjCon.Ucpwd = tpc.Ucpwd;
                                    //                propProjCon.PrivatePwd = tpc.PrivatePwd;
                                    //                propProjCon.HostingPublic = tpc.HostingPublic;
                                    //                propProjCon.HostingDateExtention = tpc.HostingDateExtention;
                                    //                propProjCon.Note = tpc.Note;
                                    //                propProjCon.SortOrder = tpc.SortOrder;
                                    //                propProjCon.BidDt = tpc.BidDt;
                                    //                propProjCon.Bidding = tpc.Bidding;
                                    //                propProjCon.Lm = tpc.Lm;
                                    //                propProjCon.NotBidding = tpc.NotBidding;
                                    //                propProjCon.Person = tpc.Person;
                                    //                propProjCon.TimeStamp = tpc.TimeStamp;
                                    //                _PCNWContext.Entry(propProjCon).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                                    //            }
                                    _PCNWContext.SaveChanges();

                                    tpc.SyncStatus = 3;
                                    _OCOCContext.Entry(tpc).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                                    _OCOCContext.SaveChanges();
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
                        _OCOCContext.SaveChanges();
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
                            propBussEnt = new BusinessEntity();
                            propBussEnt.BusinessEntityName = archOw.Name;
                            propBussEnt.BusinessEntityEmail = archOw.Email;
                            propBussEnt.BusinessEntityPhone = archOw.Phone;
                            propBussEnt.IsMember = false;
                            propBussEnt.IsContractor = false;
                            propBussEnt.IsArchitect = true;
                            propBussEnt.OldMemId = 0;
                            propBussEnt.OldConId = 0;
                            propBussEnt.OldAoId = archOw.Id;
                            propBussEnt.SyncStatus = 0;
                            propBussEnt.SyncAoid = archOw.Id;
                            _PCNWContext.BusinessEntities.Add(propBussEnt);
                            _PCNWContext.SaveChanges();

                            lastAOBusinessEntityId = (from BId in _PCNWContext.BusinessEntities select BId.BusinessEntityId).Max();
                        }
                        else if (archOw.SyncStatus == 2)
                        {
                            propBussEnt = (from be in _PCNWContext.BusinessEntities where be.BusinessEntityName == archOw.Name select be).FirstOrDefault();
                            propBussEnt.BusinessEntityName = archOw.Name;
                            propBussEnt.BusinessEntityEmail = archOw.Email;
                            propBussEnt.BusinessEntityPhone = archOw.Phone;
                            _PCNWContext.Entry(propBussEnt).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                            _PCNWContext.SaveChanges();

                            lastAOBusinessEntityId = (int)propBussEnt.SyncAoid;
                        }

                        Address propAdd;
                        if (archOw.SyncStatus == 1)
                        {
                            propAdd = new();
                            propAdd.BusinessEntityId = lastAOBusinessEntityId;
                            propAdd.Addr1 = archOw.Addr1;
                            propAdd.City = archOw.City;
                            propAdd.State = archOw.State;
                            propAdd.Zip = archOw.Zip;
                            //propAdd.AddressName = "";
                            propAdd.SyncStatus = 0;
                            propAdd.SyncAoid = archOw.Id;
                            _PCNWContext.Addresses.Add(propAdd);
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
                        _PCNWContext.SaveChanges();

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
                                                propEnty = new();
                                                propEnty.EnityName = archOw.Name;
                                                //propEnty.EntityType =;
                                                propEnty.ProjId = propProj.ProjId;
                                                propEnty.ProjNumber = Convert.ToInt32(propProj.ProjNumber);
                                                propEnty.IsActive = propProj.IsActive;
                                                propEnty.NameId = lastAOBusinessEntityId;
                                                //propEnty.ChkIssue =ProjAO.
                                                propEnty.CompType = 3;
                                                propEnty.SyncStatus = 0;
                                                propEnty.SyncProjAoid = ProjAO.ArchOwnerId;
                                                _PCNWContext.Entities.Add(propEnty);
                                            }
                                            else
                                            {
                                                AppendMessageToFile(LogFileDirectory, "->> NO PROJECT FOUND FOR PROJ ARCH OWNER ID " + ProjAO.ArchOwnerId);
                                            }
                                        }
                                        else
                                        {
                                            propEnty = new();
                                            propEnty.EnityName = archOw.Name;
                                            //propEnty.EntityType =;
                                            propEnty.ProjId = null;
                                            propEnty.ProjNumber = null;
                                            propEnty.IsActive = null;
                                            propEnty.NameId = lastAOBusinessEntityId;
                                            //propEnty.ChkIssue =;
                                            propEnty.CompType = 3;
                                            propEnty.SyncStatus = 0;
                                            propEnty.SyncProjAoid = ProjAO.ArchOwnerId;
                                            _PCNWContext.Entities.Add(propEnty);
                                        }
                                    }
                                    else if (ProjAO.SyncStatus == 2)
                                    {
                                        propEnty = (from ent in _PCNWContext.Entities where ent.SyncProjConId == ProjAO.ArchOwnerId select ent).FirstOrDefault();
                                        propEnty.EnityName = archOw.Name;
                                        _PCNWContext.Entry(propEnty).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                                    }

                                    //ProjAo propProjAO;
                                    //if (ProjAO.SyncStatus == 1)
                                    //{
                                    //    var propProj = (from c in _PCNWContext.Projects where c.SyncProId == ProjAO.ProjId select c).FirstOrDefault();
                                    //    if (propProj != null)
                                    //    {
                                    //        propProjAO = new ProjAo();
                                    //        propProjAO.AotypeId = ProjAO.AotypeId;
                                    //        propProjAO.ArchOwnerId = lastAOBusinessEntityId;
                                    //        propProjAO.BoldBp = ProjAO.BoldBp;
                                    //        propProjAO.ShowOnResults = ProjAO.ShowOnResults;
                                    //        propProjAO.ProjId = propProj.ProjId;
                                    //        propProjAO.SortOrder = ProjAO.SortOrder;
                                    //        propProjAO.SyncStatus = 0;
                                    //        propProjAO.SyncProjAoid = ProjAO.ProjAo;
                                    //        _PCNWContext.ProjAos.Add(propProjAO);
                                    //    }
                                    //    else
                                    //    {
                                    //        AppendMessageToFile(LogFileDirectory, "->> NO PROJECT FOUND FOR PROJ AO ID " + ProjAO.ProjAo);
                                    //    }
                                    //}
                                    //else if (ProjAO.SyncStatus == 2)
                                    //{
                                    //    propProjAO = (from projjao in _PCNWContext.ProjAos where projjao.SyncProjAoid == ProjAO.ArchOwnerId select projjao).FirstOrDefault();
                                    //    propProjAO.AotypeId = ProjAO.AotypeId;
                                    //    //propProjAO.ArchOwnerId = pao.ArchOwnerId;
                                    //    propProjAO.BoldBp = ProjAO.BoldBp;
                                    //    propProjAO.ShowOnResults = ProjAO.ShowOnResults;
                                    //    propProjAO.SortOrder = ProjAO.SortOrder;
                                    //    _PCNWContext.Entry(propProjAO).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                                    //}
                                    _PCNWContext.SaveChanges();

                                    ProjAO.SyncStatus = 3;
                                    _OCOCContext.Entry(ProjAO).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                                    _OCOCContext.SaveChanges();
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
                        _OCOCContext.SaveChanges();
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
            //AppendMessageToFile(LogFileDirectory, "->> tblCounty ITEMS COUNT: " + tblCounty.Count);
            AppendMessageToFile(LogFileDirectory, "->> tblProjects ITEMS COUNT: " + tblProjects.Count);
            AppendMessageToFile(LogFileDirectory, "->> tblProjCounty ITEMS COUNT: " + tblProjCounty.Count);
            //AppendMessageToFile(LogFileDirectory, "->> tblCityCounty ITEMS COUNT: " + tblCityCounty.Count);

            //if (tblCounty != null)
            //{
            //    County propCounty;
            //    foreach (TblCounty county in tblCounty)
            //    {
            //        try
            //        {
            //            if (county.SyncStatus == 1)
            //            {
            //                propCounty = new County();
            //                propCounty.County1 = county.County;
            //                propCounty.State = county.State;
            //                propCounty.SyncStatus = 0;
            //                propCounty.SyncCouId = county.CountyId;
            //                _PCNWContext.Counties.Add(propCounty);
            //            }
            //            else if (county.SyncStatus == 2)
            //            {
            //                propCounty = (from pro in _PCNWContext.Counties where pro.SyncCouId == county.CountyId select pro).FirstOrDefault();
            //                propCounty.County1 = county.County;
            //                propCounty.State = county.State;
            //                _PCNWContext.Entry(propCounty).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
            //            }
            //            _PCNWContext.SaveChanges();

            //            county.SyncStatus = 3;
            //            _OCOCContext.Entry(county).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
            //            _OCOCContext.SaveChanges();
            //            AppendMessageToFile(LogFileDirectory, "->> tblCounty COUNTY ID " + county.CountyId + " SYNC STATUS UPDATED");
            //        }
            //        catch (Exception exCounty)
            //        {
            //            FailCountytProcess++;
            //            AppendMessageToFile(LogFileDirectory, "->> EXCEPTION OCCURED FOR COUNTY ID " + county.CountyId + " IN ProcessProjectFunctionality FUNCTION");
            //            AppendMessageToFile(LogFileDirectory, "EXCEPTION MESSAGE: " + exCounty.Message);
            //            if (exCounty.InnerException != null)
            //                AppendMessageToFile(LogFileDirectory, "INNER EXCEPTION: " + exCounty.InnerException.ToString());
            //            continue;
            //        }
            //        SuccessCountyProcess++;
            //    }
            //    AppendMessageToFile(LogFileDirectory, "->> SUCCESSFUL COUNTY PROCESSED: " + SuccessCountyProcess + "\nFAILED COUNTY PROCESSED: " + FailCountytProcess);
            //}

            if (tblProjects != null)
            {
                foreach (TblProject proj in tblProjects)
                {
                    try
                    {
                        Project propProject;
                        int RecentProjectId = 0;
                        if (proj.SyncStatus == 1)
                        {
                            propProject = new Project();
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
                            propProject.SyncStatus = 0;
                            propProject.SyncProId = proj.ProjId;
                            _PCNWContext.Projects.Add(propProject);
                            _PCNWContext.SaveChanges();

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
                            _PCNWContext.SaveChanges();

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
                                        _PCNWContext.ProjCounties.Add(projCounty);
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
                                _PCNWContext.SaveChanges();

                                pc.SyncStatus = 3;
                                _OCOCContext.Entry(pc).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                                _OCOCContext.SaveChanges();
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
                                PreBidInfo propPBI = new PreBidInfo();
                                propPBI.PreBidDate = PreBidDt.Date;
                                propPBI.PreBidTime = PreBidDt.ToString("HH:mm");
                                propPBI.Location = proj.PreBidLoc;
                                propPBI.PreBidAnd = (bool)proj.PrebidAnd;
                                propPBI.ProjId = RecentProjectId;
                                propPBI.UndecidedPreBid = null;
                                propPBI.Pst = "PT";
                                propPBI.SyncStatus = 0;
                                _PCNWContext.PreBidInfos.Add(propPBI);
                            }

                            if (proj.PreBidDt2 != null)
                            {
                                var PreBidDt = (DateTime)proj.PreBidDt2;
                                PreBidInfo propPBI2 = new PreBidInfo();
                                propPBI2.PreBidDate = PreBidDt.Date;
                                propPBI2.PreBidTime = PreBidDt.ToString("HH:mm");
                                propPBI2.Location = proj.PreBidLoc2;
                                propPBI2.Pst = "PT";
                                propPBI2.PreBidAnd = (bool)proj.PrebidAnd;
                                propPBI2.ProjId = RecentProjectId;
                                propPBI2.UndecidedPreBid = null;
                                propPBI2.SyncStatus = 0;
                                _PCNWContext.PreBidInfos.Add(propPBI2);
                            }
                            if (proj.EstCost != null && !string.Equals(proj.EstCost, "N/A", StringComparison.OrdinalIgnoreCase))
                            {
                                EstCostDetail propECD = new EstCostDetail();
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
                                _PCNWContext.EstCostDetails.Add(propECD);
                            }
                            if (proj.EstCost2 != null && !string.Equals(proj.EstCost2, "N/A", StringComparison.OrdinalIgnoreCase))
                            {
                                EstCostDetail propECD2 = new EstCostDetail();
                                if (proj.EstCost2.Contains("-"))
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
                                _PCNWContext.EstCostDetails.Add(propECD2);
                            }

                            if (proj.EstCost3 != null && !string.Equals(proj.EstCost3, "N/A", StringComparison.OrdinalIgnoreCase))
                            {
                                EstCostDetail propECD3 = new EstCostDetail();
                                if (proj.EstCost3.Contains("-"))
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
                                _PCNWContext.EstCostDetails.Add(propECD3);
                            }

                            if (proj.EstCost4 != null && !string.Equals(proj.EstCost4, "N/A", StringComparison.OrdinalIgnoreCase))
                            {
                                EstCostDetail propECD4 = new EstCostDetail();
                                if (proj.EstCost4.Contains("-"))
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
                                _PCNWContext.EstCostDetails.Add(propECD4);
                            }

                            _PCNWContext.SaveChanges();
                        }
                        proj.SyncStatus = 3;
                        _OCOCContext.Entry(proj).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                        _OCOCContext.SaveChanges();
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

        private void ProcessMembberFunctionality(List<TblMember> tblOCPCMember, List<TblContact> tblOCPCContact)
        {
            int SuccessMemberProcess = 0, FailMemberProcess = 0;
            AppendMessageToFile(LogFileDirectory, "->> tblMember ITEMS COUNT: " + tblOCPCContact.Count);
            AppendMessageToFile(LogFileDirectory, "->> tblContact ITEMS COUNT: " + tblOCPCContact.Count);

            if (tblOCPCMember != null && tblOCPCMember.Count > 0)
            {
                int lastBusinessEntityId = 0;
                foreach (TblMember member in tblOCPCMember)
                {
                    try
                    {
                        BusinessEntity propBussEnt;
                        if (member.SyncStatus == 1)
                        {
                            propBussEnt = new BusinessEntity();
                            propBussEnt.BusinessEntityName = member.Company;
                            propBussEnt.BusinessEntityEmail = member.Email;
                            propBussEnt.BusinessEntityPhone = "";
                            propBussEnt.IsMember = true;
                            propBussEnt.IsContractor = false;
                            propBussEnt.IsArchitect = false;
                            propBussEnt.OldMemId = member.Id;
                            propBussEnt.OldConId = 0;
                            propBussEnt.OldAoId = 0;
                            propBussEnt.SyncStatus = 0;
                            propBussEnt.SyncMemId = member.Id;
                            _PCNWContext.BusinessEntities.Add(propBussEnt);
                            _PCNWContext.SaveChanges();

                            lastBusinessEntityId = (from BId in _PCNWContext.BusinessEntities select BId.BusinessEntityId).Max();
                        }
                        else if (member.SyncStatus == 2)
                        {
                            propBussEnt = (from be in _PCNWContext.BusinessEntities where be.BusinessEntityName == member.Company select be).FirstOrDefault();
                            propBussEnt.BusinessEntityName = member.Company;
                            propBussEnt.BusinessEntityEmail = member.Email;
                            _PCNWContext.Entry(propBussEnt).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                            _PCNWContext.SaveChanges();

                            lastBusinessEntityId = propBussEnt.BusinessEntityId;
                        }

                        AppendMessageToFile(LogFileDirectory, "->> MEMBER ID " + member.Id + " PROCESSING WITH BUSINESS ENTITY ID: " + lastBusinessEntityId);

                        //MEMBER SYNCHRONIZATION
                        SyncOCPC_MemberToPCNW_Member_Address(lastBusinessEntityId, member);

                        member.SyncStatus = 3;
                        _OCOCContext.Entry(member).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                        _OCOCContext.SaveChanges();
                        AppendMessageToFile(LogFileDirectory, "->> tblMember MEMBER ID " + member.Id + " SYNC STATUS UPDATED");

                        TblContact? contact = (from c in tblOCPCContact where c.Id == member.Id && (c.SyncStatus == 1 || c.SyncStatus == 2) select c).FirstOrDefault();
                        if (contact != null)
                        {
                            Contact propCont;
                            if (contact.SyncStatus == 1)
                            {
                                propCont = new Contact();
                                propCont.ContactName = contact.Contact;
                                propCont.ContactEmail = contact.Email;
                                propCont.BusinessEntityId = lastBusinessEntityId;
                                propCont.ContactPhone = contact.Phone;
                                propCont.ContactTitle = contact.Title;
                                //propCont.Active=
                                //propCont.BillEmail=
                                //propCont.CompType
                                //propCont.ContactState=
                                //propCont.ContactCity =
                                //propCont.ContactCounty =
                                //propCont.ContactZip =
                                //propCont.Extension=
                                //propCont.LocId=
                                //propCont.UserId=
                                propCont.SyncStatus = 0;
                                propCont.SyncConId = contact.ConId;
                                _PCNWContext.Contacts.Add(propCont);
                            }
                            else if (contact.SyncStatus == 2)
                            {
                                propCont = (from con in _PCNWContext.Contacts where con.BusinessEntityId == lastBusinessEntityId select con).FirstOrDefault();
                                propCont.ContactName = contact.Contact;
                                propCont.ContactEmail = contact.Email;
                                propCont.ContactPhone = contact.Phone;
                                propCont.ContactTitle = contact.Title;
                                _PCNWContext.Entry(propCont).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                            }
                            _PCNWContext.SaveChanges();

                            AppendMessageToFile(LogFileDirectory, "->> MEMBER ID " + member.Id + " SUCCESSFUL PROCESSED FOR CONTACT WITH BUSINESS ENTITY ID: " + lastBusinessEntityId);

                            contact.SyncStatus = 3;
                            _OCOCContext.Entry(contact).State = Microsoft.EntityFrameworkCore.EntityState.Modified;
                            _OCOCContext.SaveChanges();
                            AppendMessageToFile(LogFileDirectory, "->> tblContact CONTACT ID " + contact.ConId + " SYNC STATUS UPDATED");
                        }
                        else
                        {
                            AppendMessageToFile(LogFileDirectory, "->> NO CONTACT FOUND FOR MEMBER ID " + member.Id + " IN LIVE DATABASE");
                        }
                    }
                    catch (Exception ex)
                    {
                        FailMemberProcess++;
                        AppendMessageToFile(LogFileDirectory, "->> EXCEPTION OCCURED FOR MEMBER ID " + member.Id + " IN ProcessMembberFunctionality FUNCTION");
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
                propMem = new Member();
                propMem.Inactive = member.Inactive;
                propMem.InsertDate = (DateTime)member.InsertDate;
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
                propMem.BusinessEntityId = lastBusinessEntityId;
                //propMem.IsAutoRenew = member.IsAutoRenew;
                //propMem.CompanyPhone = member.CompanyPhone;
                //propMem.Logo = member.Logo;
                //propMem.CheckDirectory = member.CheckDirectory;
                //propMem.MemId = member.MemID;
                //propMem.InvoiceId = member.InvoiceId;
                //propMem.Discount = member.Discount;
                //propMem.PayModeRef = member.PayModeRef;
                //propMem.CreatedBy = member.CreatedBy;
                //propMem.IsDelete = member.IsDelete;
                //propMem.AddGraceDate = member.AddGraceDate;
                //propMem.ActualRenewalDate = member.ActualRenewalDate;
                propMem.SyncStatus = 0;
                propMem.SyncMemId = member.Id;
                _PCNWContext.Members.Add(propMem);
            }
            else if (member.SyncStatus == 2)
            {
                propMem = (from mem in _PCNWContext.Members where mem.BusinessEntityId == lastBusinessEntityId select mem).FirstOrDefault();
                propMem.Inactive = member.Inactive;
                propMem.InsertDate = (DateTime)member.InsertDate;
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
                //propAdd.AddressName = "";
                propAdd.SyncStatus = 0;
                propAdd.SyncMemId = member.Id;
                _PCNWContext.Addresses.Add(propAdd);
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
            _PCNWContext.SaveChanges();
            AppendMessageToFile(LogFileDirectory, "->> MEMBER ID " + member.Id + " SUCCESSFUL PROCESSED FOR MEMBER AND ADDRESS WITH BUSINESS ENTITY ID: " + lastBusinessEntityId);
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            await base.StopAsync(stoppingToken);
        }

        private static void AppendMessageToFile(string LogFileDirectory, string message)
        {
            string LogFileFullPath = Path.Combine(LogFileDirectory, "Log-" + DateTime.Now.ToString("MM-dd-yyyy") + ".txt");
            try
            {
                // Ensure directory exists
                if (!Directory.Exists(LogFileDirectory))
                {
                    Directory.CreateDirectory(LogFileDirectory);
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
                using (FileStream fs = new FileStream(LogFileFullPath, FileMode.Append, FileAccess.Write))
                {
                    using (StreamWriter writer = new StreamWriter(fs))
                    {
                        writer.WriteLine("\nEXCEPTION OCCURED - " + ex.Message + " [" + DateTime.Now + "]");
                    }
                }
                //_logger.LogError(ex, "An error occurred while writing to the file.");
            }
        }
    }
}