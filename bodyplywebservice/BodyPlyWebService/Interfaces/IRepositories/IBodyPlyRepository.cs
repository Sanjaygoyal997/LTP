using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BodyPlyWebService.Interfaces.IRepositories
{
    public interface IBodyPlyRepository
    {
        string RecipeName { get; set; }
        string PorkChop1recipe { get; set; }
        string PorkChop2recipe { get; set; }
        string ScadaRecipe { get; set; }
        string BufferRecipe { get; set; }
        string ItemName { get; set; }
        string ProductionID { get; set; }
        string ProgressWidth { get; set; }
        string Qty { get; set; }
        string ItemCode { get; set; }
        string ProductionQuantityLength { get; set; }
        string FromDate { get; set; }
        string Todate { get; set; }
        string FeederCode { get; set; }
        string MachineName { get; set; }
        string MachineCode { get; set; }
        string UserId { get; set; }
        string SyncStatus { get; set; }
        string EquipmentName { get; set; }
        string PrinterName { get; set; }


        string ValidateRecipe(string itemName, string recipe, string productionId);
        DataTable GetRecipe(string equipmentName);
        DataTable GetMachineConfig(string equipmentName);
        DataTable GetProduceItem(string recipeName);
        DataTable GetProduceItembom(string recipe);
        DataTable GetShift();
        string AddUpdateI_Material(string lot_No, string qty, string itemCode, string feeder, string machineName, string machineCode, string userId, string syncStatus);
        string AddUpdateI_Production();
        DataTable GetProductionDetails(string fromdate, string todate, string machineName);
        DataTable GetRawandCompoundProductionDetailbyProductionId(string qrCode);
        DataTable CheckMaterialQty();
        DataTable CheckMaterailAvailable();
        DataTable VerifyRecipeExist(string RecipeName);
        DataTable GetBodyPlyPRNDublicateTag(string ProductionI);
        DataTable SelectPrinterDetailbyName(string machineName);
        DataTable GetBom(string RecipeName);
        DataTable GetFollowRemark();
        DataTable GetBodyPlyShiftDataCount(int equipmentId);
        string ReverceLotno(string lotNo);
        string UpdateBodyPlyI_Material();
        DataTable GetTotalBodyPlyScan(string feederName,string equipment_id);
       // DataTable GetTotalBodyPlyScan(string feederName,int equipment_id);
        DataTable GetEquipmentExtruder(string equipmentId);
        DataTable GetExtruderForConsumeItem(string equipmentId, string sequenceNo, string bomCode, string consumeItemCode);

    }
}
