using com.mirle.ibg3k0.sc.Data.SECS;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;

namespace com.mirle.ibg3k0.sc.Data.DAO
{
    public class VCMD_MCSDao
    {
        public List<VACMD_MCS> loadVACMD_MCSIsUnfinished(DBConnection_EF con)
        {
            var query = from cmd in con.VACMD_MCS.AsNoTracking()
                        where cmd.TRANSFERSTATE >= E_TRAN_STATUS.Queue && cmd.TRANSFERSTATE <= E_TRAN_STATUS.Aborting
                        && cmd.CHECKCODE.Trim() == SECSConst.HCACK_Confirm
                        select cmd;

            return query.ToList();
        }

        public List<VACMD_MCS> loadAllVACMD(DBConnection_EF con)
        {
            var query = from cmd in con.VACMD_MCS.AsNoTracking()
                        select cmd;
            return query.ToList();
        }

        //public IQueryable getQueryAllSQL(DBConnection_EF con)
        //{
        //    var query = from vacmd_mcs in con.VACMD_MCS
        //                select vacmd_mcs;
        //    return query;
        //}

        public VACMD_MCS getVCMDByID(DBConnection_EF con, String cmd_id)
        {
            var query = from cmd in con.VACMD_MCS
                        where cmd.CMD_ID.Trim() == cmd_id.Trim()
                        select cmd;
            return query.SingleOrDefault();
        }

    }
}