using SmartLogic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BodyPlyWebService.Interfaces.IRepositories
{
    public interface IOPCManagerRepository
    {
        SmartOPC TBMHMI();
        loginformation TBMInf();
        void Stopopc();
        void Startopc();
        int OPCStatus();
        void OKCode();
        void NOKCode();
        void LoadConfiguration();
        void SetFilePath(String args);
    }
}
