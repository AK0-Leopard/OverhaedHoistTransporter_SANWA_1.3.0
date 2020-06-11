//*********************************************************************************
//      MESDefaultMapAction.cs
//*********************************************************************************
// File Name: MESDefaultMapAction.cs
// Description: Type 1 Function
//
//(c) Copyright 2014, MIRLE Automation Corporation
//
// Date          Author         Request No.    Tag     Description
// ------------- -------------  -------------  ------  -----------------------------
//**********************************************************************************
using com.mirle.ibg3k0.bcf.Controller;
using com.mirle.ibg3k0.bcf.Data.TimerAction;
using com.mirle.ibg3k0.sc.App;
using NLog;
using System;

namespace com.mirle.ibg3k0.sc.Data.TimerAction
{
    public class TransferCommandTimerAction : ITimerAction
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();
        protected SCApplication scApp = null;
        protected MPLCSMControl smControl;
        private ALINE line;
        double MCS_Auto_Assign_Keep_sec = 300;

        public TransferCommandTimerAction(string name, long intervalMilliSec)
            : base(name, intervalMilliSec)
        {

        }

        public override void initStart()
        {
            scApp = SCApplication.getInstance();
            line = scApp.getEQObjCacheManager().getLine();
        }

        private long syncPoint = 0;
        public override void doProcess(object obj)
        {
            //if (System.Threading.Interlocked.Exchange(ref syncPoint, 1) == 0)
            //{
            //    try
            //    {
            //        if (scApp.getEQObjCacheManager().getLine().ServiceMode
            //            != SCAppConstants.AppServiceMode.Active)
            //            return;
            //        scApp.CMDBLL.checkMCS_TransferCommand();
            //    }
            //    catch (Exception ex)
            //    {
            //        logger.Error(ex, "Exception");
            //    }
            //    finally
            //    {
            //        System.Threading.Interlocked.Exchange(ref syncPoint, 0);
            //    }
            //}
            try
            {
                if (!line.MCSCommandAutoAssign)
                {
                    if (line.MCSAutoAssignLastOffTime.AddSeconds(MCS_Auto_Assign_Keep_sec)<DateTime.Now)
                    {
                        line.MCSCommandAutoAssign = true;//如果太久沒有重新打開AutoAssign，就自動打開，避免命令一直不執行
                    }
                }
                scApp.CMDBLL.checkMCS_TransferCommand();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception");
            }
        }


    }

}
