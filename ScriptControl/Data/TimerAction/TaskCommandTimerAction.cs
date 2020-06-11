using com.mirle.ibg3k0.bcf.Common;
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
using com.mirle.ibg3k0.sc.Common;
using com.mirle.ibg3k0.sc.Data.VO;
using com.mirle.ibg3k0.sc.ProtocolFormat.OHTMessage;
using com.mirle.ibg3k0.stc.Common.SECS;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace com.mirle.ibg3k0.sc.Data.TimerAction
{
    public class TaskCommandTimerAction : ITimerAction
    {
        const string CALL_CONTEXT_KEY_WORD_SERVICE_ID_TaskCmdTimerAction= "TaskCommandTimerAction Service";

        private static Logger logger = LogManager.GetCurrentClassLogger();
        protected SCApplication scApp = null;

        public TaskCommandTimerAction(string name, long intervalMilliSec)
            : base(name, intervalMilliSec)
        {

        }

        public override void initStart()
        {
            scApp = SCApplication.getInstance();
        }

        //private long wholeSyncPoint = 0;
        public override void doProcess(object obj)
        {
            try
            {
                LogHelper.setCallContextKey_ServiceID(CALL_CONTEXT_KEY_WORD_SERVICE_ID_TaskCmdTimerAction);
                scApp.CMDBLL.checkOHxC_TransferCommand();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exection:");
            }
        }

        //private void checkOHxC_TransferCommand()
        //{
        //    if (System.Threading.Interlocked.Exchange(ref wholeSyncPoint, 1) == 0)
        //    {
        //        try
        //        {
        //            if (scApp.getEQObjCacheManager().getLine().ServiceMode
        //                != SCAppConstants.AppServiceMode.Active)
        //                return;
        //            List<ACMD_OHTC> CMD_OHTC_Queues = scApp.CMDBLL.loadCMD_OHTCMDStatusIsQueue();
        //            if (CMD_OHTC_Queues == null)
        //                return;
        //            foreach (ACMD_OHTC cmd in CMD_OHTC_Queues)
        //            {
        //                string vehicle_id = cmd.VH_ID.Trim();
        //                AVEHICLE assignVH = scApp.VehicleBLL.getVehicleByID(vehicle_id);
        //                //if (scApp.CMDBLL.isCMD_OHTCExcuteByVh(vehicle_id))
        //                //if (assignVH.ACT_STATUS != ProtocolFormat.OHTMessage.VHActionStatus.CycleRun)
        //                //{
        //                //    //if ((!SCUtility.isEmpty(assignVH.OHTC_CMD) ||
        //                //    assignVH.ACT_STATUS != ProtocolFormat.OHTMessage.VHActionStatus.Stop))
        //                if (!assignVH.isTcpIpConnect || !SCUtility.isEmpty(assignVH.OHTC_CMD))
        //                {
        //                    continue;
        //                }
        //                //}
        //                //Task.Run(() =>
        //                //{
        //                //long syncPoint = syncPoints[vehicle_id];
        //                //if (System.Threading.Interlocked.Exchange(ref syncPoint, 1) == 0)
        //                //{
        //                //    try
        //                //    {

        //                scApp.VehicleService.doSendOHxCCmdToVh(assignVH, cmd);
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            logger.Error(ex, "Exection:");
        //        }
        //        finally
        //        {
        //            System.Threading.Interlocked.Exchange(ref wholeSyncPoint, 0);
        //        }
        //    }
        //}

        //private void doSendOHxCCmdToVh(AVEHICLE assignVH, ACMD_OHTC cmd)
        //{
        //    ActiveType activeType = default(ActiveType);
        //    string[] routeSections = null;
        //    string[] cycleRunSections = null;
        //    string[] minRouteSeg_Vh2From = null;
        //    string[] minRouteSeg_From2To = null;
        //    //如果失敗會將命令改成abonormal End
        //    if (scApp.CMDBLL.tryGenerateCmd_OHTC_Details(cmd, out activeType, out routeSections, out cycleRunSections
        //                                                                 , out minRouteSeg_Vh2From, out minRouteSeg_From2To))
        //    {
        //        bool isSuccess;
        //        //若下達的命令為Park、CycleRun時,會一併更新Table:AVEHICLE、APARKZONEDETAIL或ACYCLEZONEDETAIL來確保停車數量、在量的正確性。
        //        isSuccess = sendTransferCommandToVh(cmd, assignVH, activeType, routeSections, cycleRunSections);

        //        if (isSuccess)
        //        {
        //            if (!SCUtility.isEmpty(cmd.CMD_ID_MCS))
        //            {
        //                scApp.CMDBLL.updateCMD_MCS_TranStatus2Transferring(cmd.CMD_ID_MCS);
        //            }

        //            //TODO 在進行命令的改派後SysExecQity的資料要重新判斷一下要怎樣計算
        //            scApp.SysExcuteQualityBLL.updateSysExecQity_PassSecInfo(cmd.CMD_ID_MCS, assignVH.VEHICLE_ID, assignVH.CUR_SEC_ID,
        //                                    minRouteSeg_Vh2From, minRouteSeg_From2To);
        //            scApp.CMDBLL.setVhExcuteCmdToShow(cmd, assignVH, routeSections, cycleRunSections);
        //            assignVH.sw_speed.Restart();
        //        }
        //        else
        //        {
        //            if (!SCUtility.isEmpty(cmd.CMD_ID_MCS))
        //            {
        //                scApp.CMDBLL.updateCMD_MCS_TranStatus2Queue(cmd.CMD_ID_MCS);
        //            }
        //            scApp.CMDBLL.updateCommand_OHTC_StatusByCmdID(cmd.CMD_ID, E_CMD_STATUS.AbnormalEndByOHT);
        //        }
        //    }
        //}

        //private bool sendTransferCommandToVh(ACMD_OHTC cmd, AVEHICLE assignVH, ActiveType activeType, string[] routeSections, string[] cycleRunSections)
        //{
        //    bool isSuccess = true;
        //    try
        //    {
        //        List<AMCSREPORTQUEUE> reportqueues = null;
        //        using (var tx = SCUtility.getTransactionScope())
        //        {
        //            using (DBConnection_EF con = DBConnection_EF.GetUContext())
        //            {
        //                switch (cmd.CMD_TPYE)
        //                {
        //                    case E_CMD_TYPE.Move_Park:
        //                        APARKZONEDETAIL aPARKZONEDETAIL = scApp.ParkBLL.getParkDetailByAdr(cmd.DESTINATION);
        //                        //scApp.VehicleBLL.setVhIsParkingOnWay(cmd.VH_ID, cmd.DESTINATION);
        //                        if (assignVH.IS_PARKING)
        //                        {
        //                            scApp.ParkBLL.resetParkAdr(assignVH.PARK_ADR_ID);
        //                            //scApp.VehicleBLL.resetVhIsInPark(assignVH.VEHICLE_ID);
        //                        }
        //                        scApp.VehicleBLL.setVhIsParkingOnWay(cmd.VH_ID, cmd.DESTINATION);

        //                        break;
        //                    case E_CMD_TYPE.Round:
        //                        if (assignVH.IS_PARKING)
        //                        {
        //                            scApp.ParkBLL.resetParkAdr(assignVH.PARK_ADR_ID);
        //                            //scApp.VehicleBLL.resetVhIsInPark(assignVH.VEHICLE_ID);
        //                        }
        //                        scApp.VehicleBLL.setVhIsCycleRunOnWay(cmd.VH_ID, cmd.DESTINATION);
        //                        break;
        //                    default:
        //                        //這件事要搬到前面去做(每次多要判斷)
        //                        if (assignVH.IS_PARKING
        //                            || !SCUtility.isEmpty(assignVH.PARK_ADR_ID))
        //                        {
        //                            //改成找出該VH是停在哪個位置，並更新狀態
        //                            scApp.ParkBLL.resetParkAdr(assignVH.PARK_ADR_ID);
        //                            scApp.VehicleBLL.resetVhIsInPark(assignVH.VEHICLE_ID);
        //                        }
        //                        if (assignVH.IS_CYCLING)
        //                        {
        //                            scApp.VehicleBLL.resetVhIsCycleRun(assignVH.VEHICLE_ID);
        //                        }
        //                        break;
        //                }

        //                isSuccess &= scApp.VehicleBLL.updateVehicleExcuteCMD(cmd.VH_ID, cmd.CMD_ID, cmd.CMD_ID_MCS);

        //                isSuccess &= scApp.CMDBLL.updateCommand_OHTC_StatusByCmdID(cmd.CMD_ID, E_CMD_STATUS.Execution);
        //                if (!SCUtility.isEmpty(cmd.CMD_ID_MCS))
        //                {
        //                    isSuccess &= scApp.VIDBLL.upDateVIDCommandInfo(cmd.VH_ID, cmd.CMD_ID_MCS);
        //                    isSuccess &= scApp.ReportBLL.doReportBeginTransfer(assignVH.VEHICLE_ID, out reportqueues);
        //                }
        //                if (isSuccess)
        //                {
        //                    isSuccess &= scApp.VehicleService.TransferRequset
        //                        (cmd.VH_ID, cmd.CMD_ID, activeType, cmd.CARRIER_ID, routeSections, cycleRunSections
        //                        , cmd.SOURCE, cmd.DESTINATION);
        //                    //isSuccess &= assignVH.sned_Str31(cmd.CMD_ID, activeType, cmd.CARRIER_ID, routeSections, cycleRunSections
        //                    //    , cmd.SOURCE, cmd.DESTINATION, out Reason);
        //                }
        //                if (isSuccess)
        //                {
        //                    tx.Complete();
        //                }
        //                else
        //                {
        //                    //scApp.getEQObjCacheManager().restoreVhDataFromDB(assignVH);
        //                }
        //            }
        //        }

        //        if (isSuccess)
        //        {
        //            scApp.ReportBLL.sendMCSS6F11MessageAsyn(reportqueues);
        //        }
        //        else
        //        {
        //            scApp.getEQObjCacheManager().restoreVhDataFromDB(assignVH);
        //        }

        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Error(ex, "Exection:");
        //        isSuccess = false;
        //        scApp.getEQObjCacheManager().restoreVhDataFromDB(assignVH);
        //    }
        //    return isSuccess;
        //}
    }

}
