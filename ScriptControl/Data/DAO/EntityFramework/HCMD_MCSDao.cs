using com.mirle.ibg3k0.sc.Data.SECS;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.mirle.ibg3k0.sc.Data.DAO.EntityFramework
{
    public class HCMD_MCSDao
    {
        public void AddByBatch(DBConnection_EF con, List<HCMD_MCS> cmd_mcss)
        {
            con.HCMD_MCS.AddRange(cmd_mcss);
            con.SaveChanges();
        }
    }

}
