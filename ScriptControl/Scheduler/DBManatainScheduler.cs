using com.mirle.ibg3k0.sc.App;
using com.mirle.ibg3k0.sc.Common;
using com.mirle.ibg3k0.sc.Data;
using Quartz;
using Quartz.Impl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace com.mirle.ibg3k0.sc.Scheduler
{
    public class DBManatainScheduler : IJob
    {
        NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        SCApplication scApp = SCApplication.getInstance();
        const int BLOCK_QUEUE_KEEP_TIME_N_Day = 7;
        public void Execute(IJobExecutionContext context)
        {
            try
            {
                using (TransactionScope tx = SCUtility.getTransactionScope())
                {
                    using (DBConnection_EF con = DBConnection_EF.GetUContext())
                    {
                        MoveACMD_MCSToHCMD_MCS();

                        MoveACMD_OHTCToHCMD_OHTC();

                        RemoveNDayAgoBlockQueue(BLOCK_QUEUE_KEEP_TIME_N_Day);

                        RemoveOHTCCMDDetail();
                        tx.Complete();
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception");
            }
        }

        private void MoveACMD_MCSToHCMD_MCS()
        {
            var finish_cmd_mcs_list = scApp.CMDBLL.loadFinishCMD_MCS();
            if (finish_cmd_mcs_list != null && finish_cmd_mcs_list.Count > 0)
            {
                scApp.CMDBLL.remoteCMD_MCSByBatch(finish_cmd_mcs_list);
                List<HCMD_MCS> hcmd_mcs_list = finish_cmd_mcs_list.Select(cmd => cmd.ToHCMD_MCS()).ToList();
                scApp.CMDBLL.CreatHCMD_MCSs(hcmd_mcs_list);
            }
        }
        private void MoveACMD_OHTCToHCMD_OHTC()
        {
            var finish_cmd_ohtc_list = scApp.CMDBLL.loadFinishCMD_OHTC();
            if (finish_cmd_ohtc_list != null && finish_cmd_ohtc_list.Count > 0)
            {
                scApp.CMDBLL.remoteCMD_OHTCByBatch(finish_cmd_ohtc_list);
                List<HCMD_OHTC> hcmd_ohtc_list = finish_cmd_ohtc_list.Select(cmd => cmd.ToHCMD_OHTC()).ToList();
                scApp.CMDBLL.CreatHCMD_OHTCs(hcmd_ohtc_list);
            }
        }
        private void RemoveNDayAgoBlockQueue(int nDay)
        {
            var n_day_ago_block_queue = scApp.MapBLL.loadAllNDayAgoAndFinishBlockQueue(nDay);
            if (n_day_ago_block_queue != null && n_day_ago_block_queue.Count > 0)
            {
                scApp.MapBLL.removeBlockQueueByBatch(n_day_ago_block_queue);
            }
        }

        private void RemoveOHTCCMDDetail()
        {
            var allCMDDetail = scApp.CMDBLL.LoadAllCMDDetail();
            var allcmdids = scApp.CMDBLL.loadAllCMDID();
            List<string> excute_ids = new List<string>();
            if (allCMDDetail != null && allCMDDetail.Count > 0)
            {
                foreach(var detail in allCMDDetail)
                {
                    if (excute_ids.Contains(detail.CMD_ID))
                    {
                        continue;
                    }
                    excute_ids.Add(detail.CMD_ID);
                    if (!allcmdids.Contains(detail.CMD_ID))
                    {
                        scApp.CMDBLL.DeleteCommand_OHTC_DetailByCmdID(detail.CMD_ID);
                    }
                }
            }
        }
    }

}
