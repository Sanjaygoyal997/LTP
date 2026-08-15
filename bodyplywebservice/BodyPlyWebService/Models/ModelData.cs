using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BodyPlyWebService.Models
{
    public class ModelData
    {
    }

    public class ResultantDTO
    {
        public int Status { get; set; }
        public string Message { get; set; }
        public object Data { get; set; }
    }

    public class Parameterbodyplyrollcalender
    {
        public string SaveStatus { get; set; }

    }

    public class ResponceData
    {
        public string reciepe_name { get; set; }
        public string advanceRecipe { get; set; }
        public string PlcRecipe { get; set; }
        public List<Feederlist> Feeders { get; set; }
        public List<MHElist> MHES { get; set; }
        public string PoolingStart { get; set; }
        public string print { get; set; }
        public string length { get; set; }
        public string ProgressLength { get; set; }
        public string ProductionId { get; set; }
        public string status { get; set; }
        public string compoundscan { get; set; }
        public bool PrinterCommunication { get; set; }
        public bool RefreshHistory { get; set; }
        public string ShiftA { get; set; }
        public string ShiftB { get; set; }
        public string ShiftC { get; set; }
        public string Runningrecipesetlength { get; set; }
        public string Nextrecipesetlength { get; set; }
        public bool Interlock { get; set; }
        public bool RecipeChangeEventStatus { get; set; }
        public string ScadaRecipe { get; set; }
        public string WidthActual { get; set; }
        public bool windup1 { get; set; }
        public bool windup2 { get; set; }
        public string Remark { get; set; }
        public string RunningWindup { get; set; }
        public bool winduphooter { get; set; }
        public bool steelHooter { get; set; }
        public bool letOff1Hooter { get; set; }
        public bool letOff2Hooter { get; set; }
        public bool Recipechange { get; set; }
        public bool InputInterlock { get; set; }
        public bool IsManualRecipeSelection { get; set; }
        public bool IsManualProductionCapture { get; set; }
    }
    public class Items
    {
        public string item_name { get; set; }
        public bool scan_status { get; set; }
        public string Hooter { get; set; }
        public bool Hooter_status { get; set; }
        public string Extruder_name { get; set; }
        public bool Extruder_status { get; set; }
        public bool Setting_status { get; set; }
        public bool Slitting_status { get; set; }
        public bool blending_status { get; set; }

        public string slittingCount { get; set; }
        public string blendCount { get; set; }

    }

    //public class Items
    //{
    //    public string item_name { get; set; }
    //    public bool scan_status { get; set; }

    //}

    public class Feederlist
    {
        public string Feedername { get; set; }
        public List<Items> ItemScanStatus { get; set; }

    }

    //Extruder/feeder configuration read from dbo.bomextrudermapping joined with dbo.master_extruder.
    //The tag properties hold OPC friendly names (the ItemName column of BodyPlyConfig.csv),
    //never Kepware addresses.
    public class ExtruderConfig
    {
        public string ExtruderName { get; set; }
        public int SequenceNo { get; set; }
        //True when the running recipe requires a scan on this extruder. A machine may carry
        //extruders a recipe does not use, and those are listed but not demanded.
        public bool IsRequired { get; set; }
        public string MesItemCountTag { get; set; }
        public string ExtruderScanOkTag { get; set; }
        public string ExtruderHooterTag { get; set; }
    }
    public class Feeder
    {
        public string feeder_name { get; set; }
        public bool status { get; set; }

    }
    public class MHElist
    {
        public string MHEname { get; set; }

        public List<MHE> ItemScanStatus { get; set; }

    }
    public class MHE
    {

        public string MHEScanQrcode { get; set; }
        public bool scan_status { get; set; }

    }
    public class PrintTag
    {
        public string productionID { get; set; }
        public string recipeName { get; set; }
        public string itemCode { get; set; }
        public string dtandtime { get; set; }
        public string stackingTime { get; set; }
        public string shift { get; set; }
        public string userName { get; set; }
        public string qty { get; set; }
        public string compoundDetail { get; set; }
        public string remark { get; set; }
        public string expire { get; set; }
        public string threadcolour { get; set; }



    }
    class QrcodeData
    {
        public string lot_No { get; set; }
        public string itemCode { get; set; }
        public string qty { get; set; }
    }

    public class ShiftData
    {

        public string RecipeName { get; set; }
        public string shift { get; set; }
        public string Productioncount { get; set; }

    }
    public class TodayProduction
    {

        public string ProductionId { get; set; }
        public string ItemCode { get; set; }
        public string RecipeName { get; set; }
        public string Date { get; set; }
        public string Shift { get; set; }
        public string Time { get; set; }
        public string MHENo { get; set; }
        public string Qty { get; set; }
    }

    public class RecipeList
    {
        public string RecipeName { get; set; }

    }
    public class RecipeListReturn
    {
        public object RecipeNameList { get; set; }

        public string Porkchop1Recipe { get; set; }
        public string Porkchop2Recipe { get; set; }

    }

    class ScanMaterial
    {
        public string MaterialCode { get; set; }
        public string LotQty { get; set; }

        public string LiveQty { get; set; }
        public string Lot_Id { get; set; }
        public string OperatorName { get; set; }
        public string status { get; set; }
        public string scantime { get; set; }
    }
}