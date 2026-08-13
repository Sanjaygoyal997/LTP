using BodyPlyWebService.App_Data;
using BodyPlyWebService.Interfaces.ILogger;
using BodyPlyWebService.Interfaces.IRepositories;
using BodyPlyWebService.Interfaces.IServices;
using BodyPlyWebService.Models;
using BodyPlyWebService.Repositories;
using SmartMIS;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.NetworkInformation;
using System.Web;
using System.Web.Script.Serialization;

namespace BodyPlyWebService.Services
{
    public class BodyPlyASPXService : IBodyPlyASPXService
    {
        private readonly LogInfo _logInfo;
        private readonly IDBLogger _dBLogger;
        private string _transaction_Id;
        private IBodyPlyASPXService _bodyPlyASPXService;
        static IOPCManagerRepository _oPCManagerRepository;
        private readonly IBodyPlyRepository _bodyPlyRepository;
        
        public string WindUp1 { get; private set; }

        public BodyPlyASPXService(IBodyPlyASPXService bodyplyASPXService, IBodyPlyRepository bodyPlyRepository, LogInfo loginfo, IDBLogger dBLogger, string transaction_Id)
        {
            _logInfo = loginfo;
            _dBLogger = dBLogger;
            _bodyPlyASPXService = bodyplyASPXService;
            //_oPCManagerRepository = oPCManagerRepository;
            _bodyPlyRepository = bodyPlyRepository;
            _transaction_Id = transaction_Id;
            _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
        }
        string recipe = "", winder1, winder2,
            mESProgresswidth, poolingStart, length, print, status, progressLength, mHEScan = "", mHEScan2 = "",
            windUp_1 = "0", windUp_2 = "0", windupHooter = "0", porkChop1InputOk = "", inputInterlock = "False",
            recipeChangeEvent, recipeChangeEventOperatorconfirm, readRecipeLength = "", setLength, runningSetLength = "0",
            middleSetLength = "0", iiot_interlock = "false", steelAlarm="", porkChop1Alarm = "", porkChop2Alarm = "",
            porkChop2InputOk = "", ItemcodeItemFbodyplyic2 = "", MesReceipe = "", MiddleRecipe = "", compoundScan = "",
            advanceRecipe = "";
        bool productionStartStop = false;
        bool productionDetail = false;
        public string UpdateHMIService(string machineName,int equipment_id, string transaction_Id)
        {

            
            _transaction_Id = transaction_Id;
            var jsonSerialiser = new JavaScriptSerializer();
            ResultantDTO result = new ResultantDTO();
            try
            {
                ResponceData ResponceDataobj = new ResponceData();
                SmartLogic.SmartOPC AllTagStatus;
                SmartLogic.loginformation inf;
                AllTagStatus = _oPCManagerRepository.TBMHMI();
                inf = _oPCManagerRepository.TBMInf();

                try
                {
                    if (AllTagStatus != null)
                    {
                        if (AllTagStatus.opcRunningState())
                        {

                        }
                        else
                        {   // inf = AllTagStatus;
                            _oPCManagerRepository.Stopopc();
                            _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                            AllTagStatus = _oPCManagerRepository.TBMHMI();
                            inf = _oPCManagerRepository.TBMInf();
                            _oPCManagerRepository.Startopc();
                            System.Threading.Thread.Sleep(5000);
                            LogEvent("BodyPlyASPXService", "UpdateHMIService", "OPC Reconnect..1", _transaction_Id, "");
                        }
                    }
                    else
                    {
                        _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                        AllTagStatus = _oPCManagerRepository.TBMHMI();
                        inf = _oPCManagerRepository.TBMInf();
                        _oPCManagerRepository.Startopc();
                        System.Threading.Thread.Sleep(5000);
                        LogEvent("BodyPlyASPXService", "UpdateHMIService", "OPC Reconnect..1", _transaction_Id, "");
                    }
                }
                catch (Exception exc)
                {
                    // Log unexpected error in catch block and log into DB
                    LogError("BodyPlyASPXService", "UpdateHMIService", exc, _transaction_Id);
                }

                List<Feederlist> Feeder = new List<Feederlist>();
                List<MHElist> MHE = new List<MHElist>();
                List<MHE> MHEScanTag = new List<MHE>();
                List<MHE> MHEScanTag2 = new List<MHE>();
                List<List<Feederlist>> Feederlist = new List<List<Feederlist>>();

                int x = 0;

                try
                {
                    if (AllTagStatus.opcRunningState() && AllTagStatus != null)
                    {
                        try
                        {
                            recipe = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("recipe").ToLower())).ToString();
                            MesReceipe = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("MesRecipe").ToLower())).ToString();
                        }
                        catch (Exception exc)
                        {

                            LogError("BodyPlyASPXService", "UpdateHMIService", exc, _transaction_Id);
                            if (exc.ToString().Contains("NullReferenceException"))
                            {
                                try
                                {
                                    _oPCManagerRepository.Stopopc();
                                }
                                catch (Exception ex)
                                { LogError("BodyPlyASPXService", "UpdateHMIService", ex, _transaction_Id); }
                                _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                                _oPCManagerRepository.Stopopc();
                                AllTagStatus = _oPCManagerRepository.TBMHMI();
                                _oPCManagerRepository.Startopc();
                                System.Threading.Thread.Sleep(5000);
                                _bodyPlyASPXService.UpdateHMIService(machineName, equipment_id, _transaction_Id);
                            }
                        }


                        mESProgresswidth = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("width").ToLower())).ToString();
                        poolingStart = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("Production_Start_Stop").ToLower())).ToString();

                       
                        if (productionStartStop != Convert.ToBoolean(poolingStart))
                        {
                            productionStartStop = Convert.ToBoolean(poolingStart);
                            productionDetail = true;
                        }

                        length = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("SetLength").ToLower())).ToString();
                        print = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("Print").ToLower())).ToString();
                        status = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("Status").ToLower())).ToString();
                        progressLength = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("ProgressLengthActual").ToLower())).ToString();
                        mHEScan = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("MHEScan").ToLower())).ToString();
                        mHEScan2 = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("MHEScan1").ToLower())).ToString();
                        windupHooter = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("Hooter").ToLower())).ToString();

                        try
                        {
                            inputInterlock = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("inputinterlock").ToLower())).ToString();
                        }
                        catch (Exception exc)
                        {
                            LogError("BodyPlyASPXService", "UpdateHMIService", exc, _transaction_Id);
                        }

                        try
                        {
                            recipeChangeEvent = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("recipechangeevent").ToLower())).ToString();
                            recipeChangeEventOperatorconfirm = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("recipechangeeventoperatorconfirm").ToLower())).ToString();

                            if ((recipeChangeEvent == "1" || recipeChangeEvent == "true" || recipeChangeEvent == "True") && (recipeChangeEventOperatorconfirm == "1" || recipeChangeEventOperatorconfirm == "true" || recipeChangeEventOperatorconfirm == "True"))
                            {
                                recipeChangeEvent = "true";

                            }
                            else
                            { recipeChangeEvent = "false"; }
                        }
                        catch (Exception exc)
                        {
                            recipeChangeEvent = "false";
                            LogError("BodyPlyASPXService", "UpdateHMIService", exc, _transaction_Id);
                        }
                        
                        try
                        {
                            readRecipeLength = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("RecipeLength").ToLower())).ToString();
                        }
                        catch (Exception ex)
                        {
                            LogEvent("BodyPlyASPXService", "UpdateHMIService", $"RecipeLength: { readRecipeLength }  Error in read tag", "", "");
                            LogEvent("BodyPlyASPXService", "UpdateHMIService", $"RecipeLength: { ex }  Error in read tag", "", "");
                        }

                        setLength = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("SetLength").ToLower())).ToString();

                        try
                        {
                            runningSetLength = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("SetLength").ToLower())).ToString();
                            // middleSetLength = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("MiddleSetlength").ToLower())).ToString();
                        }
                        catch (Exception ex)
                        {
                            LogEvent("BodyPlyASPXService", "UpdateHMIService", $"Error in RunningSetLength: {ex}", "", "");
                        }

                        try
                        {
                            iiot_interlock = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("IIOT_INTERLOCK").ToLower())).ToString();
                        }
                        catch (Exception ex)
                        {
                            LogEvent("BodyPlyASPXService", "UpdateHMIService", $"Error in iiot_interlock: {ex}", "", "");
                        }

                        string TagStatus = "";
                        if (TagStatus == "Save")
                        { AllTagStatus.WriteData("8", inf.opcItemID[inf.opcItemID.IndexOf(("print").ToLower())]); }
                        else
                        {
                            if (mHEScan != "" && mHEScan != "0")
                            {
                                MHEScanTag.Add(new MHE
                                {
                                    MHEScanQrcode = mHEScan,
                                    scan_status = true

                                });
                            }
                            else
                            {
                                MHEScanTag.Add(new MHE
                                {
                                    MHEScanQrcode = mHEScan,
                                    scan_status = false

                                });
                            }

                            

                            //Feeder list is built from dbo.bomextrudermapping joined to
                            //dbo.master_extruder. Adding, removing or renaming an extruder is a
                            //configuration change, so nothing here is specific to a machine.
                            List<ExtruderConfig> configuredExtruders =
                                ExtruderConfigProvider.Get(equipment_id.ToString(), _bodyPlyRepository);

                            if (configuredExtruders != null)
                            {
                                foreach (ExtruderConfig configuredExtruder in configuredExtruders)
                                {
                                    string extruderItemValue;
                                    if (!TryReadTag(AllTagStatus, inf, configuredExtruder.MesItemCountTag, out extruderItemValue))
                                    {
                                        extruderItemValue = "";
                                    }

                                    //Hooter state is the extruder's own hooter tag, which
                                    //ResetExtruderMaterial drives to 1 when the extruder has no
                                    //material scanned. It is reported for a scanned and an
                                    //unscanned extruder alike, because the unscanned one is
                                    //where the hooter actually sounds.
                                    string extruderHooterValue;
                                    bool extruderHooterStatus = false;
                                    if (TryReadTag(AllTagStatus, inf, configuredExtruder.ExtruderHooterTag, out extruderHooterValue))
                                    {
                                        extruderHooterStatus = TagValueToBoolean(extruderHooterValue);
                                    }

                                    //Extruder state is the extruder's scan ok tag, which
                                    //ResetExtruderMaterial drives to 1 once the extruder has
                                    //material scanned against it.
                                    string extruderScanOkValue;
                                    bool extruderScanOkStatus = false;
                                    if (TryReadTag(AllTagStatus, inf, configuredExtruder.ExtruderScanOkTag, out extruderScanOkValue))
                                    {
                                        extruderScanOkStatus = TagValueToBoolean(extruderScanOkValue);
                                    }

                                    List<Items> ItemsForExtruder = new List<Items>();

                                    if (extruderItemValue != "" && extruderItemValue != "0")
                                    {
                                        ItemsForExtruder.Add(new Items
                                        {
                                            item_name = extruderItemValue,
                                            scan_status = true,
                                            Hooter = "Hooter",
                                            Hooter_status = extruderHooterStatus,
                                            Extruder_name = configuredExtruder.ExtruderName + " status",
                                            Extruder_status = extruderScanOkStatus,
                                            Setting_status = false,
                                            blending_status = false,
                                            Slitting_status = false,
                                            blendCount = "0",
                                            slittingCount = "0"

                                        });
                                    }
                                    else
                                    {
                                        ItemsForExtruder.Add(new Items
                                        {

                                            item_name = extruderItemValue,
                                            scan_status = false,
                                            Hooter = "Hooter",
                                            Hooter_status = extruderHooterStatus,
                                            Extruder_name = configuredExtruder.ExtruderName + " status",
                                            Extruder_status = extruderScanOkStatus

                                        });
                                    }

                                    Feeder.Add(new Feederlist
                                    {
                                        Feedername = configuredExtruder.ExtruderName,
                                        ItemScanStatus = ItemsForExtruder

                                    });
                                }
                            }


                            Feederlist.Add(Feeder);

                            MHE.Add(new MHElist
                            {
                                MHEname = "MHE1",
                                ItemScanStatus = MHEScanTag

                            });

                           

                            bool recipematch = false;

                            if (MesReceipe == MiddleRecipe)
                            {
                                recipematch = true;
                            }

                            string recipeexist = "", remark = "";

                            _bodyPlyRepository.RecipeName = MesReceipe;

                            DataTable dt = _bodyPlyRepository.VerifyRecipeExist(MesReceipe);

                            if (dt.Rows.Count > 0)
                            {
                                recipeexist = "";
                                remark = dt.Rows[0]["Remark"].ToString();
                            }
                            else
                            {
                                MesReceipe = MesReceipe + "  This Recipe NOT Available MES System";
                                remark = "No Remark";
                            }

                            //_bodyPlyRepository.RecipeName = MesReceipe;
                            //dt = _bodyPlyRepository.VerifyRecipeExist(MesReceipe);
                            //if (dt.Rows.Count > 0)
                            //{
                            //    recipeexist = "";
                            //    //  remark = dt.Rows[0]["Remark"].ToString();
                            //}
                            //else
                            //{ MiddleRecipe = MiddleRecipe + "  This Recipe NOT Available MES System"; }

                            if (MesReceipe == recipe)
                            { MiddleRecipe = ""; }
                            else { MiddleRecipe = recipe; }
                            _bodyPlyRepository.RecipeName = recipe;
                            dt = _bodyPlyRepository.VerifyRecipeExist(recipe);
                            if (dt.Rows.Count > 0)
                            {
                                AllTagStatus.WriteData(1, inf.opcItemID[inf.opcItemID.IndexOf(("InputInterlock").ToLower())]);

                            }
                            else
                            { AllTagStatus.WriteData(0, inf.opcItemID[inf.opcItemID.IndexOf(("InputInterlock").ToLower())]); }

                            DataTable dtPrinterIp = _bodyPlyRepository.SelectPrinterDetailbyName(machineName);
                            if (dtPrinterIp.Rows.Count > 0)
                            {
                                string IPadress = dtPrinterIp.Rows[0]["ipAddress"].ToString();
                                string port = dtPrinterIp.Rows[0]["PortNumber"].ToString();

                                Ping ping = new Ping();
                                PingReply pingresult = ping.Send(IPadress);
                                if (pingresult.Status.ToString() == "Success")
                                {
                                    ResponceDataobj.PrinterCommunication = true;
                                }
                                else
                                { ResponceDataobj.PrinterCommunication = false; }
                            }
                            else
                            {
                                ResponceDataobj.PrinterCommunication = false;
                                //LogEvent("BodyPlyASPXService", "UpdateHMIService", $"Printer Not Configure!", "", "");
                            }

                            DataTable dtShiftcount = _bodyPlyRepository.GetBodyPlyShiftDataCount(equipment_id);

                            if (dtShiftcount.Rows.Count > 0)
                            {
                                ResponceDataobj.ShiftA = dtShiftcount.Rows[0]["shifta"].ToString();
                                ResponceDataobj.ShiftB = dtShiftcount.Rows[0]["shiftb"].ToString();
                                ResponceDataobj.ShiftC = dtShiftcount.Rows[0]["shiftc"].ToString();
                            }
                            else
                            {
                                ResponceDataobj.ShiftA = "";
                                ResponceDataobj.ShiftB = "";
                                ResponceDataobj.ShiftC = "";
                            }

                            if (recipematch)
                            {
                                ResponceDataobj.PlcRecipe = "";
                                middleSetLength = "0";
                            }
                            else { ResponceDataobj.PlcRecipe = MiddleRecipe; }

                            ResponceDataobj.reciepe_name = MesReceipe;
                            ResponceDataobj.advanceRecipe = advanceRecipe;
                            ResponceDataobj.compoundscan = compoundScan;
                            ResponceDataobj.Feeders = Feeder;
                            ResponceDataobj.print = print;
                            ResponceDataobj.PoolingStart = poolingStart;
                            if (MesReceipe != MiddleRecipe)
                            {
                                ResponceDataobj.Recipechange = true;
                            }
                            else
                            { ResponceDataobj.Recipechange = false; }

                            try
                            {
                                ResponceDataobj.length = Convert.ToString(Convert.ToDecimal(runningSetLength));
                            }
                            catch (Exception ex)
                            {
                                Uitility.LogEvent("UpdateHMIService"+ $"Error in RunningSetLength conversion { ex}");
                               // LogEvent("BodyPlyASPXService", "UpdateHMIService", $"Error in RunningSetLength conversion { ex}", "", "");
                            }
                            ResponceDataobj.status = status;

                            try
                            {
                                ResponceDataobj.ProgressLength = Convert.ToString(Convert.ToDecimal(progressLength) );
                            }
                            catch (Exception ex)
                            {
                                Uitility.LogEvent("UpdateHMIService" + $"Error in ProgressLength conversion { ex}");
                                //LogEvent("BodyPlyASPXService", "UpdateHMIService", $"Error in ProgressLength conversion  { ex}", "", "");
                            }

                            try
                            {
                                ResponceDataobj.Interlock = Convert.ToBoolean(iiot_interlock);
                                ResponceDataobj.InputInterlock = Convert.ToBoolean(inputInterlock);
                            }
                            catch (Exception ex)
                            {
                                Uitility.LogEvent("UpdateHMIService" + $"Error in Interlock conversion { ex}");
                                //  LogEvent("BodyPlyASPXService", "UpdateHMIService", $"Error in Interlock conversion  { ex}", "", "");
                            }

                            try
                            {
                                ResponceDataobj.Runningrecipesetlength = Convert.ToString(Convert.ToDecimal(runningSetLength)) + " M";
                                ResponceDataobj.Nextrecipesetlength = Convert.ToString(Convert.ToDecimal(middleSetLength)) + " M";
                            }
                            catch (Exception ex)
                            {
                                Uitility.LogEvent("UpdateHMIService" + $"Error in Runningrecipesetlength conversion { ex}");
                                //  LogEvent("BodyPlyASPXService", "UpdateHMIService", $"Error in Runningrecipesetlength conversion  { ex}", "", "");
                            }
                            ResponceDataobj.ProductionId = "";
                            ResponceDataobj.RunningWindup = "";

                            try
                            {
                                ResponceDataobj.RecipeChangeEventStatus = Convert.ToBoolean(recipeChangeEvent);
                            }
                            catch (Exception ex)
                            {
                                Uitility.LogEvent("UpdateHMIService" + $"Error in RecipeChangeEventStatus conversion { ex}");
                                // LogEvent("BodyPlyASPXService", "UpdateHMIService", $"Error in RecipeChangeEventStatus conversion { ex}", "", "");
                            }
                            ResponceDataobj.MHES = MHE;
                            ResponceDataobj.RefreshHistory = productionDetail;
                            ResponceDataobj.ScadaRecipe = recipe;

                            try
                            {
                                mESProgresswidth = "1";
                                ResponceDataobj.WidthActual = Convert.ToString(Convert.ToDecimal(mESProgresswidth) / 10) + " mm";
                            }
                            catch (Exception ex)
                            {
                                Uitility.LogEvent("UpdateHMIService" + $"Error in WidthActual conversion { ex}");
                                // LogEvent("BodyPlyASPXService", "UpdateHMIService", $"Error in WidthActual conversion { ex}", "", "");
                            }

                            try
                            {
                                ResponceDataobj.windup1 = Convert.ToBoolean(winder1);
                                ResponceDataobj.windup2 = Convert.ToBoolean(winder2);
                            }
                            catch (Exception ex)
                            {
                                Uitility.LogEvent("UpdateHMIService" + $"Error in windup1 conversion { ex}");
                                //LogEvent("BodyPlyASPXService", "UpdateHMIService", $"Error in windup1 conversion { ex}", "", "");
                            }

                            ResponceDataobj.Remark = remark;
                            try
                            {
                                ResponceDataobj.winduphooter = Convert.ToBoolean(windupHooter);
                                
                            }
                            catch (Exception ex)
                            {
                                Uitility.LogEvent("UpdateHMIService" + $"Error in winduphooter { ex}");
                                //LogEvent("BodyPlyASPXService", "UpdateHMIService", $"Error in winduphooter  { ex}", "", "");
                            }

                            DataTable dtmachinConfig = _bodyPlyRepository.GetMachineConfig("BodyPly");

                            if (dtmachinConfig.Rows.Count > 0)
                            {
                                ResponceDataobj.IsManualProductionCapture = Convert.ToBoolean(dtmachinConfig.Rows[0]["ismanualproductioncapture"]);
                                ResponceDataobj.IsManualRecipeSelection = Convert.ToBoolean(dtmachinConfig.Rows[0]["ismanualrecipeselection"]);
                            }
                        }
                        result.Status = 1;
                        result.Message = "Sucess";
                        result.Data = ResponceDataobj;
                    }
                    else
                    {
                        _oPCManagerRepository.Stopopc();
                        _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                        AllTagStatus = _oPCManagerRepository.TBMHMI();
                        inf = _oPCManagerRepository.TBMInf();
                        _oPCManagerRepository.Startopc();
                        System.Threading.Thread.Sleep(10000);
                        Uitility.LogEvent("UpdateHMIService" + $"OPC Reconnect..1");
                        //  LogEvent("BodyPlyASPXService", "UpdateHMIService", "OPC Reconnect..1", _transaction_Id, "");
                    }
                    ResetExtruderMaterial(equipment_id);
                }
                catch (Exception exc)
                {
                    Uitility.LogEvent("UpdateHMIService" + exc.ToString());
                    // Log unexpected error in catch block and log into DB
                    //LogError("BodyPlyASPXService", "UpdateHMIService", exc, _transaction_Id);
                }
                finally
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }

            }
            catch (Exception exc)
            {
                Uitility.LogEvent("UpdateHMIService" + exc.ToString());
                // Log unexpected error in catch block and log into DB
                //   LogError("BodyPlyASPXService", "UpdateHMIService", exc, _transaction_Id);
            }
            //ResetExtruderMaterial already runs above, inside the try, once the OPC state
            //has been confirmed. Calling it a second time here repeated every database read
            //and every PLC write for the same poll.
            var json = jsonSerialiser.Serialize(result);
            return json;
        }

        public void ResetExtruderMaterial(int equipment_id)
        {
            SmartLogic.SmartOPC AllTagStatus;
            SmartLogic.loginformation inf;
            AllTagStatus = _oPCManagerRepository.TBMHMI();
            inf = _oPCManagerRepository.TBMInf();

            try
            {
                if (AllTagStatus != null)
                {
                    if (AllTagStatus.opcRunningState())
                    {

                    }
                    else
                    {   // inf = AllTagStatus;
                        _oPCManagerRepository.Stopopc();
                        _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                        AllTagStatus = _oPCManagerRepository.TBMHMI();
                        inf = _oPCManagerRepository.TBMInf();
                        _oPCManagerRepository.Startopc();
                        System.Threading.Thread.Sleep(5000);
                        LogEvent("BodyPlyASPXService", "ResetExtruderMaterial", "OPC Reconnect..1", _transaction_Id, "");
                    }
                }
                else
                {
                    _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                    AllTagStatus = _oPCManagerRepository.TBMHMI();
                    inf = _oPCManagerRepository.TBMInf();
                    _oPCManagerRepository.Startopc();
                    System.Threading.Thread.Sleep(5000);
                    LogEvent("BodyPlyASPXService", "ResetExtruderMaterial", "OPC Reconnect..1", _transaction_Id, "");
                }
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyASPXService", "ResetExtruderMaterial", exc, _transaction_Id);
            }

            if (AllTagStatus.opcRunningState() && AllTagStatus != null)
            {
                //Every configured extruder is refreshed from the same rows UpdateHMIService
                //reads, so the values written here and the values reported to the HMI cannot
                //diverge. The scan count goes to the item count tag, and the scan ok and
                //hooter tags are driven as a pair.
                List<ExtruderConfig> configuredExtruders =
                    ExtruderConfigProvider.Get(equipment_id.ToString(), _bodyPlyRepository);

                if (configuredExtruders != null)
                {
                    foreach (ExtruderConfig configuredExtruder in configuredExtruders)
                    {
                        //Scoped per extruder so a missing tag or a failed write does not
                        //abandon the extruders that follow.
                        try
                        {
                            DataTable dtscan = _bodyPlyRepository.GetTotalBodyPlyScan(
                                configuredExtruder.ExtruderName, equipment_id.ToString());

                            int scanCount = (dtscan == null) ? 0 : dtscan.Rows.Count;
                            bool materialScanned = scanCount > 0;

                            TryWriteTag(AllTagStatus, inf, configuredExtruder.MesItemCountTag, scanCount);
                            TryWriteTag(AllTagStatus, inf, configuredExtruder.ExtruderHooterTag, materialScanned ? 0 : 1);
                            TryWriteTag(AllTagStatus, inf, configuredExtruder.ExtruderScanOkTag, materialScanned ? 1 : 0);
                        }
                        catch (Exception exc)
                        {
                            // Log unexpected error in catch block and log into DB
                            LogError("BodyPlyASPXService", "ResetExtruderMaterial", exc, _transaction_Id);
                        }
                    }
                }
            }
            else
            {
                _oPCManagerRepository.Stopopc();
                _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                AllTagStatus = _oPCManagerRepository.TBMHMI();
                inf = _oPCManagerRepository.TBMInf();
                _oPCManagerRepository.Startopc();
                System.Threading.Thread.Sleep(10000);
                LogEvent("BodyPlyASPXService", "ResetExtruderMaterial", "OPC Reconnect..9", _transaction_Id, "");
            }

        }

        public string GetRecipeListService(string transaction_Id)
        {
            string PorkChop1Recipe = "";
            SmartLogic.SmartOPC AllTagStatus;
            SmartLogic.loginformation inf;
            AllTagStatus = _oPCManagerRepository.TBMHMI();
            inf = _oPCManagerRepository.TBMInf();

            try
            {
                if (AllTagStatus != null)
                {
                    if (AllTagStatus.opcRunningState())
                    {

                    }
                    else
                    {   // inf = AllTagStatus;
                        _oPCManagerRepository.Stopopc();
                        _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                        AllTagStatus = _oPCManagerRepository.TBMHMI();
                        inf = _oPCManagerRepository.TBMInf();
                        _oPCManagerRepository.Startopc();
                        System.Threading.Thread.Sleep(5000);
                        LogEvent("BodyPlyASPXService", "GetRecipeListService", "OPC Reconnect..1", _transaction_Id, "");
                    }
                }
                else
                {
                    _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                    AllTagStatus = _oPCManagerRepository.TBMHMI();
                    inf = _oPCManagerRepository.TBMInf();
                    _oPCManagerRepository.Startopc();
                    System.Threading.Thread.Sleep(5000);
                    LogEvent("BodyPlyASPXService", "GetRecipeListService", "OPC Reconnect..1", _transaction_Id, "");
                }
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyASPXService", "GetRecipeListService", exc, _transaction_Id);
            }

            var jsonSerialiser = new JavaScriptSerializer();

            ResultantDTO Result = new ResultantDTO();

            try
            {
                RecipeListReturn RecipeListReturn = new RecipeListReturn();
                List<RecipeList> objRecipeList = new List<RecipeList>();
                RecipeList recipeList = new RecipeList();

                DataTable dt = _bodyPlyRepository.GetRecipe("BodyPly");
                var DetailProduction = dt.AsEnumerable()
                 .Select(s => new
                 {
                     RecipeName = s.Field<string>("FormulaCode")
                 })
                 .ToList();

                foreach (var Recipe in DetailProduction)
                {
                    recipeList.RecipeName = Recipe.ToString();
                    objRecipeList.Add(recipeList);
                }

                if (AllTagStatus.opcRunningState() && AllTagStatus != null)
                {
                    PorkChop1Recipe = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("recipe").ToLower())).ToString();
                    //PorkChop2Recipe = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("porkchop2recipe").ToLower())).ToString();
                }
                else
                {
                    _oPCManagerRepository.Stopopc();
                    _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                    AllTagStatus = _oPCManagerRepository.TBMHMI();
                    inf = _oPCManagerRepository.TBMInf();
                    _oPCManagerRepository.Startopc();
                    System.Threading.Thread.Sleep(5000);
                    LogEvent("BodyPlyASPXService", "GetRecipeListService", "OPC Reconnect..1", _transaction_Id, "");
                }
                RecipeListReturn.Porkchop1Recipe = PorkChop1Recipe;
                // RecipeListReturn.Porkchop2Recipe = PorkChop2Recipe;
                //RecipeListReturn.Porkchop1Recipe = "1";
                //RecipeListReturn.Porkchop2Recipe = "2";
                RecipeListReturn.RecipeNameList = DetailProduction;

                Result.Status = 1;
                Result.Message = "Sucessfully get Recipe.";
                Result.Data = RecipeListReturn;
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyASPXService", "GetRecipeListService", exc, _transaction_Id);
            }
            var json = jsonSerialiser.Serialize(Result);

            return json;
        }

        public string GetTodayProductionDetailsService(string machineName,string transaction_Id)
        {
            string status = "";
            ResultantDTO Result = new ResultantDTO();
            var jsonSerialiser = new JavaScriptSerializer();
            DataTable dt = new DataTable();
            List<TodayProduction> objShiftData = new List<TodayProduction>();
            try
            {
                string fromdate = "";
                DateTime dttimenow = new DateTime();
                DateTime shiftdatetime = new DateTime();
                dttimenow = DateTime.Now.AddDays(-3);
                shiftdatetime = DateTime.Now;

                fromdate = dttimenow.ToString("dd/MMM/yyyy HH:mm:ss");
                string todate = Convert.ToDateTime(shiftdatetime).ToString("dd/MMM/yyyy  HH:mm:ss");

                dt = _bodyPlyRepository.GetProductionDetails(fromdate, todate, machineName);

                if (dt.Rows.Count > 0)
                {
                    foreach (DataRow dr in dt.Rows)
                    {
                        objShiftData.Add(new TodayProduction
                        {
                            ProductionId = dr["ProductionID"].ToString(),
                            ItemCode = dr["ItemCode"].ToString(),
                            RecipeName = dr["ItemName"].ToString(),
                            Date = dr["synctime"].ToString(),
                            Time = dr["Time"].ToString(),
                            Shift = dr["shift"].ToString(),
                            MHENo = dr["MHEcode"].ToString(),
                            Qty = dr["qty"].ToString(),
                        });
                    }
                }
                else
                {
                    objShiftData.Add(new TodayProduction
                    {
                        ProductionId = "",
                        ItemCode = "",
                        RecipeName = "",
                        Date = "",
                        Shift = "",
                    });
                }
            }
            catch (Exception exc)
            {
                Result.Status = 0;
                Result.Message = "System Unavailable";
                Result.Data = status;
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyASPXService", "GetTodayProductionDetailsService", exc, _transaction_Id);
            }
            var json = jsonSerialiser.Serialize(objShiftData);
            return json;
        }

        //Records a scanned material against the extruder it belongs to. dbo.bomextrudermapping
        //holds the bom and the consumed item for every extruder, so the scan is validated by
        //asking whether the scanned item is mapped to the extruder standing at this feeder
        //position. A row returned is the material being correct there, which the bill of
        //materials could only approximate: its material group separates calendered roll from
        //slitted material but cannot tell one side of a pair from the other.
        //Shared by the scanned and the manual paths, which differ only in how the QR data
        //reaches bsObj.
        private void RecordScannedMaterial(string recipe, QrcodeData bsObj, string Feeder,
                                           int equipment_id, string MachineName, string UserName,
                                           ResultantDTO Result)
        {
            string extruderName = "";

            try
            {
                DataTable dtextruder = _bodyPlyRepository.GetExtruderForConsumeItem(
                    equipment_id.ToString(), Feeder, recipe, bsObj.itemCode);

                if (dtextruder != null && dtextruder.Rows.Count > 0
                    && dtextruder.Columns.Contains("extrudername"))
                {
                    extruderName = dtextruder.Rows[0]["extrudername"].ToString();
                }
            }
            catch (Exception exc)
            {
                LogError("BodyPlyASPXService", "RecordScannedMaterial", exc, _transaction_Id);
            }

            if (extruderName == "")
            {
                Uitility.LogEvent("RecordScannedMaterial : " + bsObj.itemCode +
                                  " is not mapped to feeder " + Feeder + " for bom " + recipe +
                                  " on equipment " + equipment_id);
                Result.Status = 0;
                Result.Message = bsObj.itemCode + " Not Validate on this input feeder";
                Result.Data = "False";
                ResetExtruderMaterial(equipment_id);
                return;
            }

            Uitility.LogEvent("RecordScannedMaterial : feeder " + Feeder + " => " + extruderName +
                              " for bom " + recipe + " item " + bsObj.itemCode);
            Result.Status = 1;
            Result.Message = "Sucess";
            Result.Data = "True";

            try
            {
                Uitility.LogEvent("bsObj.lot_No:" + bsObj.lot_No + " bsObj.qty:" + bsObj.qty +
                                  " bsObj.itemCode:" + bsObj.itemCode + " " + extruderName +
                                  " MachineName:" + MachineName + " UserName" + UserName);
                _bodyPlyRepository.AddUpdateI_Material(bsObj.lot_No, bsObj.qty, bsObj.itemCode,
                    extruderName, MachineName, MachineName, UserName, "R",
                    equipment_id.ToString(), Feeder);
            }
            catch (Exception ex)
            {
                Uitility.LogEvent("AddUpdateI_Material:" + ex.ToString());
                LogError("BodyPlyASPXService", "RecordScannedMaterial", ex, _transaction_Id);
            }

            ResetExtruderMaterial(equipment_id);
        }

        public string ScanningQrcodeService(string QrCode, string Feeder, string numberofscan, string itemnumber, string isManual, string UserName,  string _transaction_Id,string MachineName,int equipment_id)
        {
            //LogEvent("BodyPlyASPXService", "ScanningQrcodeService", $"{DateTime.Now.ToString()} Scan Data=>   QrCode : {QrCode} Feeder: {Feeder} numberofscan: {numberofscan} itemnumber: {itemnumber} isManual: {isManual} " ,_transaction_Id, "");
            Uitility.LogEvent("QrCode:"+ QrCode+ " Feeder:"+ Feeder+ " numberofscan:"+ numberofscan+ " itemnumber:"+ itemnumber+ " isManual:"+ isManual+ " UserName:"+ UserName+ " MachineName:"+ MachineName);
            string recipe = "", readRecipeLength = "", compoundScan = "", PorkChop1Recipe = "", PorkChop2Recipe = "";

            string status = "";
            ResultantDTO Result = new ResultantDTO();
            SmartLogic.SmartOPC AllTagStatus;
            SmartLogic.loginformation inf;
            AllTagStatus = _oPCManagerRepository.TBMHMI();
            inf = _oPCManagerRepository.TBMInf();

            try
            {
                if (AllTagStatus != null)
                {
                    if (AllTagStatus.opcRunningState())
                    {

                    }
                    else
                    {   // inf = AllTagStatus;
                        _oPCManagerRepository.Stopopc();
                        _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                        AllTagStatus = _oPCManagerRepository.TBMHMI();
                        inf = _oPCManagerRepository.TBMInf();
                        _oPCManagerRepository.Startopc();
                        System.Threading.Thread.Sleep(5000);
                       // LogEvent("BodyPlyASPXService", "ScanningQrcodeService", "OPC Reconnect..1", _transaction_Id, "");
                    }
                }
                else
                {
                    _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                    AllTagStatus = _oPCManagerRepository.TBMHMI();
                    inf = _oPCManagerRepository.TBMInf();
                    _oPCManagerRepository.Startopc();
                    System.Threading.Thread.Sleep(5000);
                    //LogEvent("BodyPlyASPXService", "ScanningQrcodeService", "OPC Reconnect..1", _transaction_Id, "");
                }
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyASPXService", "ScanningQrcodeService", exc, _transaction_Id);
            }

            int x = 0;
            string type = "";
            var jsonSerialiser = new JavaScriptSerializer();
            QrcodeData bsObj = new QrcodeData();

            if (isManual == "0")
            {
                string[] checkQrCodeType = QrCode.Split('@');
                if (checkQrCodeType.Length > 1)
                {
                    type = "Raw";
                }
                else
                {
                    type = "Compound";
                }

                try
                {
                    if (AllTagStatus.opcRunningState() && AllTagStatus != null)
                    {
                        JavaScriptSerializer js = new JavaScriptSerializer();
                        if (type == "Compound")
                        {
                            Uitility.LogEvent("ScanningQrcodeService"+ "compound type");
                           // LogEvent("BodyPlyASPXService", "ScanningQrcodeService", "compound type", _transaction_Id, "");
                            bsObj = js.Deserialize<QrcodeData>(QrCode);
                        }
                        else if (type == "Raw")
                        {
                            Uitility.LogEvent("ScanningQrcodeService" + "raw type");
                            //LogEvent("BodyPlyASPXService", "ScanningQrcodeService", "raw type", _transaction_Id, "");
                            bsObj.lot_No = checkQrCodeType[1].ToString();
                            bsObj.itemCode = checkQrCodeType[0].ToString();
                            bsObj.qty = checkQrCodeType[2].ToString();
                        }
                        status = "";

                        
                            recipe = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("recipe").ToLower())).ToString();

                        PorkChop1Recipe = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("recipe").ToLower())).ToString();
                        PorkChop2Recipe = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("recipe").ToLower())).ToString();

                        try
                        {
                            readRecipeLength = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("RecipeLength").ToLower())).ToString();
                        }
                        catch (Exception exc)
                        {
                            Uitility.LogEvent("Exception" + exc.ToString());
                            //  LogEvent("BodyPlyASPXService", "ScanningQrcodeService", "RecipeLength:" + readRecipeLength + " Error in read tag", _transaction_Id, "");
                            // LogError("BodyPlyASPXService", "ScanningQrcodeService", exc, _transaction_Id);
                        }

                        //if (recipe.Length >= Convert.ToInt32(readRecipeLength))
                        //{
                        //    recipe = recipe.Substring(0, Convert.ToInt32(readRecipeLength));
                        //}

                        if (Feeder == "1")
                        {
                            if (PorkChop1Recipe != recipe && PorkChop1Recipe != "")
                            {
                                recipe = PorkChop1Recipe;
                            }
                        }
                       

                       
                        string result = _bodyPlyRepository.ValidateRecipe(bsObj.itemCode, recipe, bsObj.lot_No,
                                                                          equipment_id.ToString(), Feeder);
                        Uitility.LogEvent("ScanningQrcodeService result: " + result);
                        if (result.IndexOf("Successfully") >= 0)
                        {
                            Uitility.LogEvent("ScanningQrcodeService"+ "ValidateRecipe complete verify");
                            //LogEvent("BodyPlyASPXService", "ScanningQrcodeService", "ValidateRecipe complete verify", _transaction_Id, "");
                            RecordScannedMaterial(recipe, bsObj, Feeder, equipment_id,
                                                  MachineName, UserName, Result);
                        }
                        else
                        {
                            if (result.IndexOf("Reversal") >= 0)
                            {
                                status = "False";
                                Result.Status = 2;
                                Result.Message = result;
                                Result.Data = status;
                            }
                            else
                            {
                                status = "False";
                                Result.Status = 0;
                                Result.Message = result;
                                Result.Data = status;
                            }
                            
                        }
                    }
                    else
                    {
                        _oPCManagerRepository.Stopopc();
                        _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                        AllTagStatus = _oPCManagerRepository.TBMHMI();
                        inf = _oPCManagerRepository.TBMInf();
                        _oPCManagerRepository.Startopc();
                        System.Threading.Thread.Sleep(5000);
                        LogEvent("BodyPlyASPXService", "ScanningQrcodeService", "OPC Reconnect..2", _transaction_Id, "");
                    }
                }
                catch (Exception ex)
                {
                    Uitility.LogEvent("System Unavailable ScanningQrcodeService"+ex.ToString());
                    Result.Status = 0;
                    Result.Message = "System Unavailable";
                    Result.Data = status;
                    // Log unexpected error in catch block and log into DB
                   // LogError("BodyPlyASPXService", "ScanningQrcodeService", ex, _transaction_Id);
                }
            }
            else if (isManual == "1")
            {
                try
                {
                    if (AllTagStatus.opcRunningState() && AllTagStatus != null)
                    {
                        JavaScriptSerializer js = new JavaScriptSerializer();
                        DataTable dtprodetail = _bodyPlyRepository.GetRawandCompoundProductionDetailbyProductionId(QrCode);

                        bsObj.lot_No = QrCode;
                        Uitility.LogEvent("QrCode:" + $"{dtprodetail.Rows.Count}_{QrCode}");
                        //LogEvent("BodyPlyASPXService", "ScanningQrcodeService", $"{dtprodetail.Rows.Count}_{QrCode}", _transaction_Id, "");

                        if (dtprodetail.Rows.Count > 0)
                        {
                            Uitility.LogEvent("ScanningQrcodeService"+ $"{dtprodetail.Rows[0]["itemCode"]}_{dtprodetail.Rows[0]["quantity"]}");
                            //LogEvent("BodyPlyASPXService", "ScanningQrcodeService", "Data Found", _transaction_Id, "");
                            //LogEvent("BodyPlyASPXService", "ScanningQrcodeService", $"{dtprodetail.Rows[0]["itemCode"]}_{dtprodetail.Rows[0]["quantity"]}", _transaction_Id, "");

                            bsObj.itemCode = dtprodetail.Rows[0]["itemName"].ToString();
                            bsObj.qty = dtprodetail.Rows[0]["quantity"].ToString();
                        }
                        else
                        {
                            bsObj.itemCode = "";
                            bsObj.qty = "0";
                            Uitility.LogEvent("ScanningQrcodeService"+ "Detail Not Found");
                            //LogEvent("BodyPlyASPXService", "ScanningQrcodeService", "Detail Not Found", _transaction_Id, "");
                        }

                        status = "";

                       
                           recipe = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("recipe").ToLower())).ToString();
                        
                        PorkChop1Recipe = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("Recipe").ToLower())).ToString();
                        PorkChop2Recipe = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("Recipe").ToLower())).ToString();

                        try
                        {
                            readRecipeLength = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("RecipeLength").ToLower())).ToString();
                        }
                        catch (Exception exc)
                        {
                            Uitility.LogEvent("ScanningQrcodeService" + exc.ToString());
                            // LogEvent("BodyPlyASPXService", "ScanningQrcodeService", "RecipeLength:" + readRecipeLength + " Error in read tag", _transaction_Id, "");
                            //LogError("BodyPlyASPXService", "ScanningQrcodeService", exc, _transaction_Id);
                        }

                        //if (recipe.Length >= Convert.ToInt32(readRecipeLength))
                        //{
                        //    recipe = recipe.Substring(0, Convert.ToInt32(readRecipeLength));
                        //}

                        if (Feeder == "1")
                        {
                            if (PorkChop1Recipe != recipe && PorkChop1Recipe != "")
                            {
                                recipe = PorkChop1Recipe;
                            }
                        }
                        

                        string result = _bodyPlyRepository.ValidateRecipe(bsObj.itemCode, recipe, bsObj.lot_No,
                                                                          equipment_id.ToString(), Feeder);

                        if (result.IndexOf("Successfully") >= 0)
                        {
                            Uitility.LogEvent("ScanningQrcodeService"+ "ValidateRecipe complete verify");
                            // LogEvent("BodyPlyASPXService", "ScanningQrcodeService", "ValidateRecipe complete verify", _transaction_Id, "");
                            RecordScannedMaterial(recipe, bsObj, Feeder, equipment_id,
                                                  MachineName, UserName, Result);
                        }
                        else
                        {
                            if (result.IndexOf("Reversal") >= 0)
                            {
                                status = "False";
                                Result.Status = 2;
                                Result.Message = result;
                                Result.Data = status;
                            }
                            else
                            {
                                status = "False";
                                Result.Status = 0;
                                Result.Message = result;
                                Result.Data = status;
                            }

                        }
                    }
                    else
                    {
                        _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                        AllTagStatus = _oPCManagerRepository.TBMHMI();
                        inf = _oPCManagerRepository.TBMInf();
                        _oPCManagerRepository.Startopc();
                        System.Threading.Thread.Sleep(5000);
                    }
                }

                catch (Exception ex)
                {
                    Result.Status = 0;
                    Result.Message = "System Unavailable";
                    Result.Data = status;
                    LogError("BodyPlyASPXService", "ScanningQrcodeService", ex, _transaction_Id);
                }
            }
           
            var json = jsonSerialiser.Serialize(Result);
            return json;
        }

        public string removeMaterialService(string qrcode, string userID, string isManual, string Feeder)
        {
            LogEvent("BodyPlyASPXService", "ScanningQrcodeService", DateTime.Now.ToString() + "Scan Data " + Environment.NewLine + " QrCode :" + qrcode + Environment.NewLine + " Feeder :" + Feeder + " isManual:" + isManual, _transaction_Id, "");

            string type = "";
            string[] checkQrCodeType = qrcode.Split('@');
            if (checkQrCodeType.Length > 1)
            {
                type = "Raw";
            }
            else
            {
                type = "Compound";
            }

            QrcodeData bsObj = new QrcodeData();
            ResultantDTO Result = new ResultantDTO();
            var jsonSerialiser = new JavaScriptSerializer();
            JavaScriptSerializer js = new JavaScriptSerializer();

            if (isManual == "0")
            {
                if (type == "Compound")
                {
                    bsObj = js.Deserialize<QrcodeData>(qrcode);
                }
                else if (type == "Raw")
                {
                    bsObj.lot_No = checkQrCodeType[1].ToString();

                }

                string result = _bodyPlyRepository.ReverceLotno(bsObj.lot_No);
                LogEvent("BodyPlyASPXService", "ScanningQrcodeService", "ReverceLotno result" + result, _transaction_Id, "");

                if (result.IndexOf("Successfully") >= 0)
                {
                    string status = "True";
                    Result.Status = 1;
                    Result.Message = result;
                    Result.Data = status;
                }
                else
                {
                    string status = "False";
                    Result.Status = 0;
                    Result.Message = result;
                    Result.Data = status;
                }
            }

            if (isManual == "1")
            {
                bsObj.lot_No = qrcode;

                string result = _bodyPlyRepository.ReverceLotno(bsObj.lot_No);

                if (result.IndexOf("Successfully") >= 0)
                {
                    string status = "True";
                    Result.Status = 1;
                    Result.Message = result;
                    Result.Data = status;
                }
                else
                {
                    string status = "False";
                    Result.Status = 0;
                    Result.Message = result;
                    Result.Data = status;
                }
            }

            //ResetExtruderMaterial(equipment_id);
            var json = jsonSerialiser.Serialize(Result);
            return json;
        }

        public string MHEScanningService(string qrCode)
        {
            Uitility.LogEvent("Scan qrCode:"+ qrCode);
            SmartLogic.SmartOPC AllTagStatus;
            SmartLogic.loginformation inf;
            AllTagStatus = _oPCManagerRepository.TBMHMI();
            inf = _oPCManagerRepository.TBMInf();

            var jsonSerialiser = new JavaScriptSerializer();

            ResultantDTO Result = new ResultantDTO();

            try
            {
                if (AllTagStatus != null)
                {
                    if (AllTagStatus.opcRunningState())
                    {

                    }
                    else
                    {   // inf = AllTagStatus;
                        _oPCManagerRepository.Stopopc();
                        _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                        AllTagStatus = _oPCManagerRepository.TBMHMI();
                        inf = _oPCManagerRepository.TBMInf();
                        _oPCManagerRepository.Startopc();
                        System.Threading.Thread.Sleep(5000);
                        LogEvent("BodyPlyASPXService", "GetRecipeListService", "OPC Reconnect..1", _transaction_Id, "");
                    }
                }
                else
                {
                    _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                    AllTagStatus = _oPCManagerRepository.TBMHMI();
                    inf = _oPCManagerRepository.TBMInf();
                    _oPCManagerRepository.Startopc();
                    System.Threading.Thread.Sleep(5000);
                    LogEvent("BodyPlyASPXService", "GetRecipeListService", "OPC Reconnect..1", _transaction_Id, "");
                }

                Result.Status = 0;
                Result.Message = "";
                Result.Data = "";

                if (qrCode.Contains("CSBP"))
                {
                    AllTagStatus.WriteData(qrCode, inf.opcItemID[inf.opcItemID.IndexOf(("MHEScan").ToLower())]);
                    Result.Status = 1;
                    Result.Message = "MHE SCAN Successfully " + qrCode;
                    Result.Data = "MHE SCAN Successfully " + qrCode;
                }
                else
                {
                    Result.Status = 0;
                    Result.Message = "Incorrect MHE QR Code " + qrCode;
                    Result.Data = "Incorrect MHE QR Code " + qrCode;
                }

            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyASPXService", "GetRecipeListService", exc, _transaction_Id);
            }

            var json = jsonSerialiser.Serialize(Result);

            return json;
        }

        public string GetBOMService(string transaction_Id)
        {
            string readRecipeLength = "", recipe = "";
            var jsonSerialiser = new JavaScriptSerializer();
            ResultantDTO Result = new ResultantDTO();
            SmartLogic.SmartOPC AllTagStatus;
            SmartLogic.loginformation inf;
            AllTagStatus = _oPCManagerRepository.TBMHMI();
            inf = _oPCManagerRepository.TBMInf();

            try
            {
                if (AllTagStatus != null)
                {
                    if (AllTagStatus.opcRunningState())
                    {

                    }
                    else
                    {   // inf = AllTagStatus;
                        _oPCManagerRepository.Stopopc();
                        _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                        AllTagStatus = _oPCManagerRepository.TBMHMI();
                        inf = _oPCManagerRepository.TBMInf();
                        _oPCManagerRepository.Startopc();
                        System.Threading.Thread.Sleep(5000);
                        LogEvent("BodyPlyASPXService", "GetBOMService", "OPC Reconnect..1", _transaction_Id, "");
                    }
                }
                else
                {
                    _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                    AllTagStatus = _oPCManagerRepository.TBMHMI();
                    inf = _oPCManagerRepository.TBMInf();
                    _oPCManagerRepository.Startopc();
                    System.Threading.Thread.Sleep(5000);
                    LogEvent("BodyPlyASPXService", "GetBOMService", "OPC Reconnect..1", _transaction_Id, "");
                }
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyASPXService", "GetBOMService", exc, _transaction_Id);
            }

            try
            {

                if (AllTagStatus.opcRunningState() && AllTagStatus != null)
                {
                    try
                    {
                        readRecipeLength = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("RecipeLength").ToLower())).ToString();
                        recipe = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("Recipe").ToLower())).ToString();
                        //if (recipe.Length >= Convert.ToInt32(readRecipeLength))
                        //{
                        //    recipe = recipe.Substring(0, Convert.ToInt32(readRecipeLength));
                        //}
                        LogEvent("BodyPlyASPXService", "GetBOMService", recipe, _transaction_Id, "");
                    }
                    catch (Exception ex)
                    {
                        LogError("BodyPlyASPXService", "GetBOMService", ex, _transaction_Id);
                    }
                }
                else
                {
                    _oPCManagerRepository.Stopopc();
                    _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                    AllTagStatus = _oPCManagerRepository.TBMHMI();
                    inf = _oPCManagerRepository.TBMInf();
                    _oPCManagerRepository.Startopc();
                    System.Threading.Thread.Sleep(5000);
                    LogEvent("BodyPlyASPXService", "GetBOMService", "OPC Reconnect..1", _transaction_Id, "");
                }

                DataTable dt = _bodyPlyRepository.GetBom(recipe);

                var DetailProduction = dt.AsEnumerable()
                 .Select(s => new
                 {
                     ConsumeMaterial = s.Field<string>("ConsumeMaterialName")
                 }).ToList();

                Result.Status = 1;
                Result.Message = "Successfully Get BOM.";
                Result.Data = DetailProduction;
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyASPXService", "GetBOMService", exc, _transaction_Id);
            }
            var json = jsonSerialiser.Serialize(Result);

            return json;
        }

        public string RecipeChangeConfirmationService(Boolean RecipeChange)
        {
            LogEvent("BodyPlyASPXService", "Recipe Change status:" + RecipeChange.ToString(), "OPC Reconnect..1", _transaction_Id, "");
            var jsonSerialiser = new JavaScriptSerializer();
            ResultantDTO Result = new ResultantDTO();
            string status = "";
            SmartLogic.SmartOPC AllTagStatus;
            SmartLogic.loginformation inf;
            AllTagStatus = _oPCManagerRepository.TBMHMI();
            inf = _oPCManagerRepository.TBMInf();

            try
            {
                if (AllTagStatus != null)
                {
                    if (AllTagStatus.opcRunningState())
                    {

                    }
                    else
                    {   // inf = AllTagStatus;
                        _oPCManagerRepository.Stopopc();
                        _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                        AllTagStatus = _oPCManagerRepository.TBMHMI();
                        inf = _oPCManagerRepository.TBMInf();
                        _oPCManagerRepository.Startopc();
                        System.Threading.Thread.Sleep(5000);
                        LogEvent("BodyPlyASPXService", "RecipeChangeConfirmationService", "OPC Reconnect..1", _transaction_Id, "");
                    }
                }
                else
                {
                    _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                    AllTagStatus = _oPCManagerRepository.TBMHMI();
                    inf = _oPCManagerRepository.TBMInf();
                    _oPCManagerRepository.Startopc();
                    System.Threading.Thread.Sleep(5000);
                    LogEvent("BodyPlyASPXService", "RecipeChangeConfirmationService", "OPC Reconnect..1", _transaction_Id, "");
                }
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyASPXService", "RecipeChangeConfirmationService", exc, _transaction_Id);
            }

            int x = 0;

            try
            {
                if (AllTagStatus.opcRunningState() && AllTagStatus != null)
                {
                    if (RecipeChange == true)
                    {
                        LogEvent("BodyPlyASPXService", "RecipeChangeConfirmationService", "RecipeChange status true", _transaction_Id, "");
                        AllTagStatus.WriteData(0, inf.opcItemID[inf.opcItemID.IndexOf(("recipechangeevent").ToLower())]);
                        AllTagStatus.WriteData(0, inf.opcItemID[inf.opcItemID.IndexOf(("recipechangeeventoperatorconfirm").ToLower())]);
                        AllTagStatus.WriteData(0, inf.opcItemID[inf.opcItemID.IndexOf(("hooter").ToLower())]);
                        AllTagStatus.WriteData("", inf.opcItemID[inf.opcItemID.IndexOf(("MesRecipe").ToLower())]);
                        LogEvent("BodyPlyASPXService", "RecipeChangeConfirmationService", "RecipeChange status Complete", _transaction_Id, "");
                    }
                    else
                    {
                        AllTagStatus.WriteData(0, inf.opcItemID[inf.opcItemID.IndexOf(("recipechangeeventoperatorconfirm").ToLower())]);
                        AllTagStatus.WriteData(0, inf.opcItemID[inf.opcItemID.IndexOf(("hooter").ToLower())]);
                    }

                    Result.Status = 1;
                    Result.Message = "Sucessfully";
                    Result.Data = status;
                }
                else
                {   // inf = AllTagStatus;
                    _oPCManagerRepository.Stopopc();
                    _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                    AllTagStatus = _oPCManagerRepository.TBMHMI();
                    inf = _oPCManagerRepository.TBMInf();
                    _oPCManagerRepository.Startopc();
                    System.Threading.Thread.Sleep(5000);
                    LogEvent("BodyPlyASPXService", "RecipeChangeConfirmationService", "OPC Reconnect..1", _transaction_Id, "");
                }
            }
            catch (Exception exc)
            {
                Result.Status = 0;
                Result.Message = "System Unavailable";
                Result.Data = status;
                LogError("BodyPlyASPXService", "RecipeChangeConfirmationService", exc, _transaction_Id);
            }

            var json = jsonSerialiser.Serialize(Result);
            return json;
        }

        public string RecipeChangeService()
        {
            var jsonSerialiser = new JavaScriptSerializer();
            ResultantDTO Result = new ResultantDTO();
            string status = "";
            SmartLogic.SmartOPC AllTagStatus;
            SmartLogic.loginformation inf;
            AllTagStatus = _oPCManagerRepository.TBMHMI();
            inf = _oPCManagerRepository.TBMInf();

            try
            {
                if (AllTagStatus != null)
                {
                    if (AllTagStatus.opcRunningState())
                    {

                    }
                    else
                    {   // inf = AllTagStatus;
                        _oPCManagerRepository.Stopopc();
                        _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                        AllTagStatus = _oPCManagerRepository.TBMHMI();
                        inf = _oPCManagerRepository.TBMInf();
                        _oPCManagerRepository.Startopc();
                        System.Threading.Thread.Sleep(5000);
                        LogEvent("BodyPlyASPXService", "RecipeChangeService", "OPC Reconnect..1", _transaction_Id, "");
                    }
                }
                else
                {
                    _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                    AllTagStatus = _oPCManagerRepository.TBMHMI();
                    inf = _oPCManagerRepository.TBMInf();
                    _oPCManagerRepository.Startopc();
                    System.Threading.Thread.Sleep(5000);
                    LogEvent("BodyPlyASPXService", "RecipeChangeService", "OPC Reconnect..1", _transaction_Id, "");
                }
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyASPXService", "RecipeChangeService", exc, _transaction_Id);
            }

            int x = 0;

            try
            {
                if (AllTagStatus.opcRunningState() && AllTagStatus != null)
                {
                    JavaScriptSerializer js = new JavaScriptSerializer();

                    try
                    {
                        string Recipe_buffermove = AllTagStatus.opcValue.GetValue(inf.opcItemID.IndexOf(("Recipe").ToLower())).ToString();

                        AllTagStatus.WriteData(Recipe_buffermove, inf.opcItemID[inf.opcItemID.IndexOf(("MesRecipe").ToLower())]);
                        AllTagStatus.WriteData(0, inf.opcItemID[inf.opcItemID.IndexOf(("width").ToLower())]);
                        LogEvent("BodyPlyASPXService", "RecipeChangeService", "Recipe Load Using Button click Move :" + Recipe_buffermove, _transaction_Id, "");
                        Result.Status = 1;
                        Result.Message = "Success";
                        Result.Data = status;
                    }
                    catch (Exception exc)
                    {
                        Result.Status = 1;
                        Result.Message = "Sucess";
                        Result.Data = status;
                        LogError("BodyPlyASPXService", "RecipeChangeService", exc, _transaction_Id);
                    }
                }
                else
                {   // inf = AllTagStatus;
                    Result.Status = 0;
                    Result.Message = "System Unavailable";
                    Result.Data = status;
                    _oPCManagerRepository.Stopopc();
                    _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                    AllTagStatus = _oPCManagerRepository.TBMHMI();
                    inf = _oPCManagerRepository.TBMInf();
                    _oPCManagerRepository.Startopc();
                    System.Threading.Thread.Sleep(5000);
                    LogEvent("BodyPlyASPXService", "RecipeChangeService", "OPC Reconnect..1", _transaction_Id, "");
                }
            }
            catch (Exception exc)
            {
                Result.Status = 0;
                Result.Message = "System Unavailable";
                Result.Data = status;
                LogError("BodyPlyASPXService", "RecipeChangeService", exc, _transaction_Id);
            }

            var json = jsonSerialiser.Serialize(Result);
            return json;
        }

        public string MESRecipeChangeService(string recipeName)
        {
            var jsonSerialiser = new JavaScriptSerializer();
            ResultantDTO Result = new ResultantDTO();
            string status = "";
            SmartLogic.SmartOPC AllTagStatus;
            SmartLogic.loginformation inf;
            AllTagStatus = _oPCManagerRepository.TBMHMI();
            inf = _oPCManagerRepository.TBMInf();

            try
            {
                if (AllTagStatus != null)
                {
                    if (AllTagStatus.opcRunningState())
                    {

                    }
                    else
                    {   // inf = AllTagStatus;
                        _oPCManagerRepository.Stopopc();
                        _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                        AllTagStatus = _oPCManagerRepository.TBMHMI();
                        inf = _oPCManagerRepository.TBMInf();
                        _oPCManagerRepository.Startopc();
                        System.Threading.Thread.Sleep(5000);
                        LogEvent("BodyPlyASPXService", "MESRecipeChangeService", "OPC Reconnect..1", _transaction_Id, "");
                    }
                }
                else
                {
                    _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                    AllTagStatus = _oPCManagerRepository.TBMHMI();
                    inf = _oPCManagerRepository.TBMInf();
                    _oPCManagerRepository.Startopc();
                    System.Threading.Thread.Sleep(5000);
                    LogEvent("BodyPlyASPXService", "MESRecipeChangeService", "OPC Reconnect..1", _transaction_Id, "");
                }
            }
            catch (Exception exc)
            {
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyASPXService", "MESRecipeChangeService", exc, _transaction_Id);
            }

            int x = 0;

            try
            {
                if (AllTagStatus.opcRunningState() && AllTagStatus != null)
                {
                    JavaScriptSerializer js = new JavaScriptSerializer();

                    try
                    {
                        string[] splitRecipe = recipeName.Split('_');

                        //AllTagStatus.WriteData(splitRecipe[1], inf.opcItemID[inf.opcItemID.IndexOf(("MesRecipe").ToLower())]);
                        AllTagStatus.WriteData(splitRecipe[1], inf.opcItemID[inf.opcItemID.IndexOf(("Recipe").ToLower())]);
                        AllTagStatus.WriteData(splitRecipe[1].Length, inf.opcItemID[inf.opcItemID.IndexOf(("RecipeLength").ToLower())]);


                        LogEvent("BodyPlyASPXService", "MESRecipeChangeService", "Recipe Load Using Button click Move :" + splitRecipe[1], _transaction_Id, "");
                        Result.Status = 1;
                        Result.Message = "Success";
                        Result.Data = status;
                    }
                    catch (Exception exc)
                    {
                        Result.Status = 1;
                        Result.Message = "Sucess";
                        Result.Data = status;
                        LogError("BodyPlyASPXService", "MESRecipeChangeService", exc, _transaction_Id);
                    }
                }
                else
                {   // inf = AllTagStatus;
                    Result.Status = 0;
                    Result.Message = "System Unavailable";
                    Result.Data = status;
                    _oPCManagerRepository.Stopopc();
                    _oPCManagerRepository = new OPCManagerRepository(_logInfo, _dBLogger, _transaction_Id);
                    AllTagStatus = _oPCManagerRepository.TBMHMI();
                    inf = _oPCManagerRepository.TBMInf();
                    _oPCManagerRepository.Startopc();
                    System.Threading.Thread.Sleep(5000);
                    LogEvent("BodyPlyASPXService", "MESRecipeChangeService", "OPC Reconnect..1", _transaction_Id, "");
                }
            }
            catch (Exception exc)
            {
                Result.Status = 0;
                Result.Message = "System Unavailable";
                Result.Data = status;
                LogError("BodyPlyASPXService", "MESRecipeChangeService", exc, _transaction_Id);
            }

            var json = jsonSerialiser.Serialize(Result);
            return json;
        }

        public string GetFeederScanMaterialService(string Feeder,int equipment_id)
        {
            var jsonSerialiser = new JavaScriptSerializer();
            ResultantDTO Result = new ResultantDTO();
            string status = "";

            try
            {
                List<ScanMaterial> objscan = new List<ScanMaterial>();
                DataTable dtGetTotalbeadwindingPCRScan = _bodyPlyRepository.GetTotalBodyPlyScan(Feeder, equipment_id.ToString());
                if (dtGetTotalbeadwindingPCRScan.Rows.Count > 0)
                {
                    for (int i = 0; i < dtGetTotalbeadwindingPCRScan.Rows.Count; i++)
                    {
                        objscan.Add(new ScanMaterial
                        {
                            Lot_Id = dtGetTotalbeadwindingPCRScan.Rows[i]["Lot_Id"].ToString(),
                            MaterialCode = dtGetTotalbeadwindingPCRScan.Rows[i]["MaterialCode"].ToString(),
                            LotQty = dtGetTotalbeadwindingPCRScan.Rows[i]["Qty"].ToString(),
                            LiveQty = dtGetTotalbeadwindingPCRScan.Rows[i]["LiveQty"].ToString(),
                            OperatorName = dtGetTotalbeadwindingPCRScan.Rows[i]["UserId"].ToString(),
                            status = dtGetTotalbeadwindingPCRScan.Rows[i]["Lotstatus"].ToString(),
                            scantime = dtGetTotalbeadwindingPCRScan.Rows[i]["scantime"].ToString(),
                        });
                    }

                    Result.Status = 1;
                    Result.Message = "Success";
                    Result.Data = objscan;
                }
                else
                {
                    objscan.Add(new ScanMaterial
                    {
                        Lot_Id = "",
                        MaterialCode = "",
                        LotQty = "",
                        LiveQty = "",
                        OperatorName = ""


                    });
                    Result.Status = 1;
                    Result.Message = "No Data Available";
                    Result.Data = objscan;
                }
            }
            catch (Exception exc)
            {
                Result.Status = 0;
                Result.Message = "System Unavailable";
                Result.Data = status;
                // Log unexpected error in catch block and log into DB
                LogError("BodyPlyASPXService", "RecipeChangeService", exc, _transaction_Id);
            }

            var json = jsonSerialiser.Serialize(Result);
            return json;
        }

        public string PrintTagAndSaveDataService(string UserName, string RecipeName, string ProgressLength, string ProgressWidth,string machineName)
        {
            LogEvent("BodyPlyASPXService", "RecipeChangeService", "PrintTagAndSaveData  UserName : " + UserName + Environment.NewLine + " RecipeName :" + RecipeName + Environment.NewLine + " ProgressLength :" + ProgressLength + Environment.NewLine + " ProgressWidth :" + ProgressWidth, _transaction_Id, "");

            var jsonSerialiser = new JavaScriptSerializer();
            ResultantDTO Result = new ResultantDTO();
            PrintTag ResponceDataobj = new PrintTag();
            string status = "", PoolingStart = "";

            SmartLogic.SmartOPC AllTagStatus;
            SmartLogic.loginformation inf;
            AllTagStatus = _oPCManagerRepository.TBMHMI();
            inf = _oPCManagerRepository.TBMInf();

            _oPCManagerRepository.Startopc();

            int x = 0;
            for (int i = 0; i <= 1; i++)
            {
                x = _oPCManagerRepository.OPCStatus();
                if (_oPCManagerRepository.OPCStatus() == 0)
                {
                    i = 0;
                }
                else
                { i = 2; }
            }

            try
            {
                AllTagStatus.WriteData(0, inf.opcItemID[inf.opcItemID.IndexOf(("print").ToLower())]);

                string LotNo, ItemCode, Dtandtime = "";
                LotNo = "4R" + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss");

                Dtandtime = DateTime.Now.ToString("dd/MM/yyyy");
                LotNo = LotNo.Replace("/", "");
                LotNo = LotNo.Replace(":", "");
                LotNo = LotNo.Replace("-", "");
                LotNo = LotNo.Replace(" ", "");

                LogEvent("BodyPlyASPXService", "PrintTagAndSaveDataService", "LotNo Generated : " + LotNo, _transaction_Id, "");

                DataTable dt = _bodyPlyRepository.GetProduceItem(RecipeName);

                if (dt.Rows.Count > 0)
                {

                    LogEvent("BodyPlyASPXService", "PrintTagAndSaveDataService", "Produced Material :" + dt.Rows[0]["ProducedItemID"].ToString(), _transaction_Id, "");

                    if (PoolingStart == "1" || PoolingStart == "True" || PoolingStart == "true")
                    {
                        AllTagStatus.WriteData("3", inf.opcItemID[inf.opcItemID.IndexOf(("status").ToLower())]);
                        LogEvent("BodyPlyASPXService", "PrintTagAndSaveDataService", "Production Status 3", _transaction_Id, "");
                    }
                    else
                    {
                        AllTagStatus.WriteData("2", inf.opcItemID[inf.opcItemID.IndexOf(("status").ToLower())]);
                        LogEvent("BodyPlyASPXService", "PrintTagAndSaveDataService", "Production Status 2", _transaction_Id, "");
                    }

                    DataTable dtbom = _bodyPlyRepository.GetProduceItembom(dt.Rows[0]["Name"].ToString());

                    var DetailProduction = dtbom.AsEnumerable().Where(r => r.Field<String>("MaterialGroup_IN").Contains("FINAL COMPOUND")).Select(s => new
                    {
                        CompoundDetail = s.Field<string>("MaterialCodeGroup_IN"),

                    }).ToList();

                    var items = DetailProduction.ToArray();
                    string compoundname = "";
                    for (int i = 0; i <= (items.Length - 1); i++) // Run loop until get all the quantities in the array
                    {
                        compoundname = items[i].CompoundDetail.ToString();
                    }

                    ResponceDataobj.productionID = LotNo;
                    ResponceDataobj.recipeName = dt.Rows[0]["Name"].ToString();
                    ResponceDataobj.itemCode = dt.Rows[0]["ProducedItemID"].ToString();
                    ResponceDataobj.dtandtime = Dtandtime;
                    ResponceDataobj.stackingTime = DateTime.Now.ToString("hh:mm:ss");
                    ResponceDataobj.userName = UserName;
                    ResponceDataobj.qty = ProgressLength;
                    ResponceDataobj.compoundDetail = compoundname;
                    ResponceDataobj.remark = "";
                    ResponceDataobj.threadcolour = "";
                    ResponceDataobj.expire = DateTime.Now.AddDays(4).ToString("dd/MMM/yyyy");

                    DataTable dtshift = _bodyPlyRepository.GetShift();
                    ResponceDataobj.shift = "SHIFT " + dtshift.Rows[0]["shift"].ToString();

                    Result.Status = 1;
                    Result.Message = "Success";
                    Result.Data = ResponceDataobj;
                    LogEvent("BodyPlyASPXService", "PrintTagAndSaveDataService", "Return Print Data" + jsonSerialiser.Serialize(Result), _transaction_Id, "");
                }
                else
                {
                    ResponceDataobj.productionID = "";
                    ResponceDataobj.recipeName = "";
                    ResponceDataobj.itemCode = "";
                    ResponceDataobj.dtandtime = "";
                    ResponceDataobj.userName = "";
                    Result.Status = 0;
                    Result.Message = "Fail";
                    Result.Data = ResponceDataobj;
                }
            }
            catch (Exception exc)
            {
                LogEvent("BodyPlyASPXService", "PrintTagAndSaveDataService", "Error", _transaction_Id, "");
                LogError("BodyPlyASPXService", "PrintTagAndSaveDataService", exc, _transaction_Id);
                Result.Status = 0;
                Result.Message = "System Unavailable"; ;
                Result.Data = "";
            }

            var json = jsonSerialiser.Serialize(Result);
            return json;
        }

        public string PrintTagPRNDublicateService(string ProductionId,string machineName)
        {
            // LogEvent("BodyPlyASPXService", "RecipeChangeService", "Print Dublicate Tag of production Id: " + ProductionId + Environment.NewLine, _transaction_Id, "");
            Uitility.LogEvent("Print Dublicate Tag of production Id: " + ProductionId);
            var jsonSerialiser = new JavaScriptSerializer();
            ResultantDTO Result = new ResultantDTO();
            string status = "";

            try
            {
                DataTable dt = _bodyPlyRepository.GetBodyPlyPRNDublicateTag(ProductionId);

                if (dt.Rows.Count > 0)
                {
                    DataTable dtPrinterIp = _bodyPlyRepository.SelectPrinterDetailbyName(machineName);
                    if (dtPrinterIp.Rows.Count > 0)
                    {
                        string IPadress = dtPrinterIp.Rows[0]["ipAddress"].ToString();
                        string port = dtPrinterIp.Rows[0]["PortNumber"].ToString();
                        string materialcode, date, shift, qty, bookingtime, empcode, MHENo,
                            Remark, Usebefore, hour, Lot_No, colourcode, combination, ccodes, WinderDetails,
                            LastRemark, threadcolours, Width,desc; 

                        Lot_No = dt.Rows[0]["LotNo"].ToString();
                        materialcode = dt.Rows[0]["productItem"].ToString();
                        date = Convert.ToDateTime(dt.Rows[0]["Productiondatetime"].ToString()).ToString("dd/MMM/yyyy HH:mm:ss"); ;
                        DateTime prodate = new DateTime();
                        DateTime Productiondatetime = new DateTime();
                        prodate = Convert.ToDateTime(date);
                        Productiondatetime = prodate;
                        if (prodate >= Convert.ToDateTime(prodate.ToString("dd/MMM/yyyy") + " 00:00:01.000") && prodate <= Convert.ToDateTime(prodate.ToString("dd/MMM/yyyy") + " 05:59:59.999"))
                        {
                            Productiondatetime = prodate.AddDays(-1);
                            //if (UseBefore != "" && UseBefore != null)
                            //{
                            //    Usebefore = Convert.ToDateTime(UseBefore).AddDays(-1).ToString();
                            //}
                        }
                        qty = dt.Rows[0]["ProgressLength"].ToString();
                        bookingtime = Convert.ToDateTime(dt.Rows[0]["Productiondatetime"].ToString()).ToString("hh:mm tt");
                        empcode = dt.Rows[0]["UserName"].ToString();
                        MHENo = dt.Rows[0]["MHEScan"].ToString();
                        Remark = dt.Rows[0]["Remark"].ToString();
                        Usebefore = dt.Rows[0]["Expdate"].ToString();
                        shift = dt.Rows[0]["shiftname"].ToString();
                        ccodes = dt.Rows[0]["ccode"].ToString();
                        WinderDetails = dt.Rows[0]["Winder"].ToString();
                        LastRemark = dt.Rows[0]["LastRemark"].ToString();
                        threadcolours = dt.Rows[0]["Identification"].ToString();
                        Width = dt.Rows[0]["ProgressWidth"].ToString();
                        desc = dt.Rows[0]["desc"].ToString();
                        if (Width.Length >= 6)
                        {
                            try
                            {
                                Width = (Convert.ToDecimal(Width)).ToString("N1");
                            }
                            catch (Exception ex)
                            {
                                LogEvent("BodyPlyASPXService", "PrintTagPRNDublicateService", "Error in Width Conversion " + ex.ToString(), _transaction_Id, "");
                            }
                        }
                        else
                        {
                            try
                            {
                                Width = (Convert.ToDecimal(Width)).ToString("N1");
                            }
                            catch (Exception ex)
                            { LogEvent("BodyPlyASPXService", "PrintTagPRNDublicateService", "Error in Width Conversion " + ex.ToString(), _transaction_Id, ""); }
                        }
                        colourcode = "";
                        combination = dt.Rows[0]["bomcombination"].ToString();
                        string qrcode = "{lot_No:'" + Lot_No + "',itemCode:'" + materialcode + "',qty:'" + qty + "',ExpireDate:'" + Usebefore + "',DOM:'" + Productiondatetime.ToString("dd/MMM/yyyy HH:mm:ss") + "',shift:'" + shift + "',MHE:'" + MHENo + "' ,Width:'" + Width + "'}";
                        string remark1 = "", remark2 = "", remark3 = "";
                        if (LastRemark.Length >= 10)
                        {
                            remark1 = LastRemark.Substring(0, 10);
                        }
                        if (LastRemark.Length >= 10)
                        {
                            remark2 = LastRemark.Substring(10, (LastRemark.Length - 10));
                        }
                        LogEvent("BodyPlyASPXService", "PrintTagPRNDublicateService", qrcode, _transaction_Id, "");
                        if (materialcode == "" || materialcode == "NULL" || materialcode == "null" || materialcode == null)
                        { materialcode = "Unknown"; }

                        try
                        {
                            if (Usebefore != "")
                            {
                                Usebefore = Convert.ToDateTime(Usebefore).ToString("dd/MM/yyyy HH:mm:ss");
                            }

                        }
                        catch (Exception ex)
                        {
                            LogEvent("BodyPlyASPXService", "PrintTagPRNDublicateService", "error in converting Userbefore to properdate formate " + Usebefore, _transaction_Id, "");
                            LogError("BodyPlyASPXService", "PrintTagPRNDublicateService", ex, _transaction_Id);
                        }
                        date = Productiondatetime.ToString("dd/MM/yyyy");
                        date = date.Replace("-", "/");
                        Usebefore = Usebefore.Replace("-", "/");

                        string PRNString = @"'Seagull:2.1:DP
INPUT OFF
VERBOFF
INPUT ON
SYSVAR(48) = 0
ERROR 15,""FONT NOT FOUND""
ERROR 18,""DISK FULL""
ERROR 26,""PARAMETER TOO LARGE""
ERROR 27,""PARAMETER TOO SMALL""
ERROR 37,""CUTTER DEVICE NOT FOUND""
ERROR 1003,""FIELD OUT OF LABEL""
SYSVAR(35) = 0
OPEN ""tmp:setup.sys"" FOR OUTPUT AS #1
PRINT#1,""Printing,Media,Print Area,Media Margin (X),0""
PRINT#1,""Printing,Media,Clip Default,On""
CLOSE #1
SETUP ""tmp:setup.sys""
KILL ""tmp:setup.sys""
CLIP ON
CLIP BARCODE ON
LBLCOND 3,2
CLL
OPTIMIZE ""BATCH"" ON
PP784,41:PD1,518,4,""L""
PP54,557:PL732,4
PP54,559:DIR2
PL520,4
PP56,39:DIR1
PL733,4
PP110,557:DIR2
PL517,4
PP56,176:DIR1
PL53,4
PP56,453:PL56,4
PP166,558:DIR2
PL517,4
PP207,558:PL517,4
PP367,558:PL517,4
PP450,559:PL519,4
PP493,558:PL517,4
PP722,558:PL517,4
PP411,263:DIR1
PL313,4
PP65,461:AN7
DIR4
NASC 8
FT ""CG Times Bold"",7,0,64
PT ""SmartMES""
PP65,180:FT ""CG Times Bold"",7,0,94
PT  ""U3:" + machineName + @"""
PP115,207:AN1
DIR1
PL218,4
PP114,46:AN7
DIR4
FT ""CG Times"",7,0,83
PT ""Material Code""
PP176,66:FT ""CG Times"",7,0,89
PT ""Description""
PP519,325:BARSET ""QRCODE"",1,1,4,2,1
PB """ + qrcode + @"""
PP496,42:FT ""CG Times"",7,0,98
PT ""SHELL NO:""
PP454,37:FT ""CG Times"",7,0,98
PT ""WIDTH:""
PP419,269:FT ""CG Times"",7,0,98
PT ""QTY:""
PP335,47:FT ""CG Times"",6,0,96
PT ""Production ID:""
PP416,44:FT ""CG Times"",6,0,96
PT ""EMP NO:""
PP736,51:FT ""CG Times"",7,0,98
PT ""REMAKRS:""
PP103,229:FT ""CG Times Bold"",13,0,57
PT """ + materialcode + @"""
PP170,210:FT ""CG Times Bold""
FONTSIZE 7
FONTSLANT 0
PT """ + desc + @"""
PP328,216:FT ""CG Times Bold"",9,0,107
PT """ + Lot_No + @"""
PP409,360:FT ""CG Times Bold"",9,0,93
PT """ + qty + @" M""
PP367,216:FT ""CG Times Bold"",9,0,93
PT """ + Usebefore + @"""
PP732,156:FT ""CG Times Bold"",7,0,125
PT """ + Remark + @"""
PP272,557:AN1
DIR2
PL517,4
PP332,560:PL519,4
PP209,353:DIR1
PL124,4
PP213,84:AN7
DIR4
FT ""CG Times"",6,0,89
PT ""DATE""
PP273,225:FT ""CG Times"",6,0,89
PT ""TR COLOR""
PP273,371:FT ""CG Times"",6,0,89
PT ""COMBINATION""
PP233,44:FT ""CG Times Bold"",9,0,93
PT """ + date + @"""
PP294,261:FT ""CG Times Bold"",5,0,93
PT """ + threadcolours + @"""
PP293,367:FT ""CG Times Bold"",9,0,97
PT """ + combination + @"""
PP217,246:FT ""CG Times"",6,0,89
PT ""SHIFT""
PP232,264:FT ""CG Times Bold"",9,0,93
PT """ + shift + @"""
PP213,414:FT ""CG Times"",6,0,89
PT ""TIME""
PP232,373:FT ""CG Times Bold"",9,0,93
PT """ + bookingtime + @"""
PP275,81:FT ""CG Times"",6,0,89
PT ""C CODE""
PP293,78:FT ""CG Times Bold"",9,0,93
PT """ + ccodes + @"""
PP409,558:AN1
DIR2
PL517,4
PP381,42:AN7
DIR4
FT ""CG Times"",6,0,96
PT ""USE BEFORE:""
PP409,156:FT ""CG Times Bold"",9,0,93
PT """ + empcode + @"""
PP453,156:FT ""CG Times Bold"",9,0,93
PT """ + Width + @"""
PP458,273:FT ""CG Times"",7,0,98
PT ""WINDER:""
PP453,414:FT ""CG Times Bold"",9,0,93
PT """ + WinderDetails + @"""
PP528,51:FT ""CG Times Bold"",9,0,93
PT """ + MHENo + @"""
PP568,264:AN1
DIR2
PL224,4
PP576,51:AN7
DIR4
FT ""CG Times"",7,0,98
PT ""DETAILS:""
PP676,51:FT ""CG Times Bold"",7,0,94
PT """ + remark1 + @"""
PP641,51:FT ""CG Times Bold"",7,0,94
PT """ + remark2 + @"""
PP611,51:FT ""CG Times Bold"",7,0,94
PT """ + remark3 + @"""
LAYOUT RUN ""
PF
PRINT KEY OFF

";
                        Uitility.LogEvent("PRNString: " + PRNString);
                     //   LogEvent("BodyPlyASPXService", "PrintTagPRNDublicateService", "PRNString: " + PRNString, _transaction_Id, "");

                        try
                        {
                            // Open connection
                            System.Net.Sockets.TcpClient client = new System.Net.Sockets.TcpClient();
                            client.Connect(IPadress, Convert.ToInt32(port));

                            // Write ZPL String to connection
                            System.IO.StreamWriter writer = new System.IO.StreamWriter(client.GetStream());
                            writer.Write(PRNString);
                            writer.Flush();

                            // Close Connection
                            writer.Close();
                            client.Close();
                        }
                        catch (Exception ex)
                        {
                            LogError("BodyPlyASPXService", "PrintTagPRNDublicateService", ex, _transaction_Id);
                            // Catch Exception
                        }

                        Result.Status = 1;
                        Result.Message = "Print Successfully!";
                        Result.Data = status;
                    }
                    else
                    {
                        Result.Status = 0;
                        Result.Message = "Print Not configure!";
                        Result.Data = status;
                    }
                }
            }

            catch (Exception exc)
            {
                LogEvent("BodyPlyASPXService", "PrintTagPRNDublicateService", "error", _transaction_Id, "");
                LogError("BodyPlyASPXService", "PrintTagPRNDublicateService", exc, _transaction_Id);
                Result.Status = 0;
                Result.Message = "System Unavailable"; ;
                Result.Data = "";
            }


            var json = jsonSerialiser.Serialize(Result);

            return json;
        }

        //Method for Logging Error
        //Resolves an OPC friendly name against the tag list loaded from BodyPlyConfig.csv and
        //returns the value cached by the SmartOPC DataChange callback. Uses exactly the same
        //two calls as the inline reads, with guards for a tag that is absent from the csv
        //(IndexOf returns -1) and for a value not yet delivered by the first callback.
        private static bool TryReadTag(SmartLogic.SmartOPC opc, SmartLogic.loginformation inf,
                                       string tagName, out string value)
        {
            value = "";

            if (opc == null || inf == null || string.IsNullOrEmpty(tagName))
            {
                return false;
            }

            try
            {
                int tagIndex = inf.opcItemID.IndexOf(tagName.ToLower());
                if (tagIndex < 0)
                {
                    return false;
                }

                object rawValue = opc.opcValue.GetValue(tagIndex);
                if (rawValue == null)
                {
                    return false;
                }

                value = rawValue.ToString();
                return true;
            }
            catch (Exception exc)
            {
                Uitility.LogEvent("TryReadTag :" + tagName + " :" + exc.ToString());
                return false;
            }
        }

        //Writes a value to an OPC tag resolved by its friendly name. Mirrors TryReadTag:
        //a name that is absent from BodyPlyConfig.csv resolves to -1, which would throw on
        //the indexer, so it is reported rather than allowed to abort the caller.
        private bool TryWriteTag(SmartLogic.SmartOPC opc, SmartLogic.loginformation inf,
                                 string tagName, object value)
        {
            if (opc == null || inf == null || string.IsNullOrEmpty(tagName))
            {
                return false;
            }

            try
            {
                int tagIndex = inf.opcItemID.IndexOf(tagName.ToLower());
                if (tagIndex < 0)
                {
                    Uitility.LogEvent("TryWriteTag : tag not configured : " + tagName);
                    return false;
                }

                opc.WriteData(value, inf.opcItemID[tagIndex]);
                return true;
            }
            catch (Exception exc)
            {
                Uitility.LogEvent("TryWriteTag :" + tagName + " :" + exc.ToString());
                return false;
            }
        }

        //Converts an OPC tag value to a boolean. The value depends on how the tag is
        //configured in Kepware: a boolean tag reads back as True/False while a word or
        //integer tag reads back as 1/0. Convert.ToBoolean only accepts True/False and
        //throws on "1", so both forms are handled here.
        private static bool TagValueToBoolean(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            string tagValue = value.Trim();

            if (tagValue == "1") { return true; }
            if (tagValue == "0") { return false; }

            bool parsedBoolean;
            if (bool.TryParse(tagValue, out parsedBoolean))
            {
                return parsedBoolean;
            }

            decimal parsedNumber;
            if (decimal.TryParse(tagValue, out parsedNumber))
            {
                return parsedNumber != 0;
            }

            return false;
        }

        private void LogError(string folderName, string methodInfo, Exception exc, string transactionId)
        {
            _dBLogger.LogIntoDB(new LogInfo
            {
                EquipmentId = "BodyPly",
                LogType = "Error",
                LogCode = "500",
                Message = $"Exception: {exc.Message}, Inner Exception: {exc.InnerException?.Message}",
                FolderName = $"{folderName}",
                MethodInfo = methodInfo,
                DateTime = DateTime.Now.ToString(),
                TransactionID = transactionId
            });
        }

        //Method for Logging Event
        private void LogEvent(string folderName, string methodInfo, string message, string transactionId, object data)
        {
            _dBLogger.LogIntoDB(new LogInfo
            {
                EquipmentId = "BodyPly",
                LogType = "Event",
                LogCode = "200",
                Message = message,
                Data = data,
                FolderName = $"{folderName}",
                MethodInfo = methodInfo,
                DateTime = DateTime.Now.ToString(),
                TransactionID = transactionId
            });
        }

    }
}