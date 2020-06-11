using com.mirle.ibg3k0.sc.Data.SECS;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.mirle.ibg3k0.sc.Data.DAO.EntityFramework
{
    public class HCMD_OHTCDao
    {
        public void AddByBatch(DBConnection_EF con, List<HCMD_OHTC> cmd_ohtcs)
        {
            con.HCMD_OHTC.AddRange(cmd_ohtcs);
            con.SaveChanges();
        }
    }

}
