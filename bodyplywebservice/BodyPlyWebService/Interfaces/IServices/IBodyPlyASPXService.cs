using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BodyPlyWebService.Interfaces.IServices
{
    public interface IBodyPlyASPXService
    {
        string UpdateHMIService(string machineName,int equipment_id, string transaction_Id);
        string GetRecipeListService(string transaction_Id);
        string GetTodayProductionDetailsService(string machineName,string transaction_Id);
        string ScanningQrcodeService(string qrCode, string feeder, string numberofscan, string itemnumber, string isManual,  string UserName, string _transaction_Id,string MachineName,int equipment_id);
        string removeMaterialService(string qrcode, string userID, string isManual, string Feeder);
        string MHEScanningService(string qrCode);
        string GetBOMService(string transaction_Id);
        string RecipeChangeConfirmationService(Boolean RecipeChange);
        string RecipeChangeService();
        string MESRecipeChangeService(string recipeName);
        string GetFeederScanMaterialService(string FeederName,int equipment_id);
        string PrintTagAndSaveDataService(string UserName, string RecipeName, string ProgressLength, string ProgressWidth, string machineName);
        string PrintTagPRNDublicateService(string ProductionId,string machineName);
        void ResetExtruderMaterial(int equipmentId);
    }
}
