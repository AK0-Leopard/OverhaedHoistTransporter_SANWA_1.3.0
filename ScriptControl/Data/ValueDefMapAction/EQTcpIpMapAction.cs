//*********************************************************************************
//      EQType2SecsMapAction.cs
//*********************************************************************************
// File Name: EQType2SecsMapAction.cs
// Description: Type2 EQ Map Action
//
//(c) Copyright 2014, MIRLE Automation Corporation
//
// Date          Author         Request No.    Tag     Description
// ------------- -------------  -------------  ------  -----------------------------
//**********************************************************************************
using com.mirle.ibg3k0.bcf.App;
using com.mirle.ibg3k0.bcf.Common;
using com.mirle.ibg3k0.bcf.Controller;
using com.mirle.ibg3k0.bcf.Data.VO;
using com.mirle.ibg3k0.sc.App;
using com.mirle.ibg3k0.sc.Common;
using com.mirle.ibg3k0.sc.Data.PLC_Functions;
using com.mirle.ibg3k0.sc.Data.TcpIp;
using com.mirle.ibg3k0.sc.Data.VO;
using com.mirle.ibg3k0.sc.ProtocolFormat.OHTMessage;
using com.mirle.iibg3k0.ttc;
using com.mirle.iibg3k0.ttc.Common;
using com.mirle.iibg3k0.ttc.Common.TCPIP;
using KingAOP;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using static com.mirle.ibg3k0.sc.Data.PLC_Functions.VehicleCSTInterface;

namespace com.mirle.ibg3k0.sc.Data.ValueDefMapAction
{
    /// <summary>
    /// </summary>
    /// <seealso cref="com.mirle.ibg3k0.sc.Data.ValueDefMapAction.ValueDefMapActionBase" />
    public class EQTcpIpMapAction : ValueDefMapActionBase, IDynamicMetaObjectProvider
    {

        string tcpipAgentName = string.Empty;
        protected Logger logger_PLCConverLog;

        /// <summary>
        /// Initializes a new instance of the <see cref="EQType2SecsMapAction"/> class.
        /// </summary>
        public EQTcpIpMapAction()
            : base()
        {

        }
        public DynamicMetaObject GetMetaObject(Expression parameter)
        {
            return new AspectWeaver(parameter, this);
        }

        /// <summary>
        /// Gets the identity key.
        /// </summary>
        /// <returns>System.String.</returns>
        public override string getIdentityKey()
        {
            return this.GetType().Name;
        }
        //protected AVEHICLE eqpt = null;

        /// <summary>
        /// Sets the context.
        /// </summary>
        /// <param name="baseEQ">The base eq.</param>
        public override void setContext(BaseEQObject baseEQ)
        {
            this.eqpt = baseEQ as AVEHICLE;

        }
        /// <summary>
        /// Uns the register event.
        /// </summary>
        public override void unRegisterEvent()
        {
            //not implement
        }
        /// <summary>
        /// Does the share memory initialize.
        /// </summary>
        /// <param name="runLevel">The run level.</param>
        public override void doShareMemoryInit(BCFAppConstants.RUN_LEVEL runLevel)
        {
            try
            {
                switch (runLevel)
                {
                    case BCFAppConstants.RUN_LEVEL.ZERO:
                        //scApp.MapBLL.addVehicle(eqpt.VEHICLE_ID);
                        //eqpt.VID_Collection.VID_70_VehicleID.VEHILCE_ID = eqpt.VEHICLE_ID;
                        if (eqpt != null)
                        {
                            if (!SCUtility.isEmpty(eqpt.OHTC_CMD))
                            {
                                ACMD_OHTC aCMD_OHTC = scApp.CMDBLL.getExcuteCMD_OHTCByCmdID(eqpt.OHTC_CMD);
                                string[] PredictPath = scApp.CMDBLL.loadPassSectionByCMDID(eqpt.OHTC_CMD);
                                scApp.CMDBLL.setVhExcuteCmdToShow(aCMD_OHTC, this.eqpt, PredictPath, null);
                            }
                        }
                        //先讓車子一開始都當作是"VehicleInstall"的狀態
                        //之後要從DB得知上次的狀態，是否為Remove
                        if (eqpt.IS_INSTALLED)
                            eqpt.VehicleInstall();

                        break;
                    case BCFAppConstants.RUN_LEVEL.ONE:
                        break;
                    case BCFAppConstants.RUN_LEVEL.TWO:
                        break;
                    case BCFAppConstants.RUN_LEVEL.NINE:
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exection:");
            }
        }

        protected void onStateChange_Initial()
        {

        }

        //todo 需掛上實際資料
        protected void str102_Receive(object sender, TcpIpEventArgs e)
        {
            //Boolean resp_cmp = false;
            //STR_VHMSG_VHCL_KISO_VERSION_REP recive_str = TCPUtility._Packet2Str<STR_VHMSG_VHCL_KISO_VERSION_REP>((byte[])e.objPacket, eqpt.TcpIpAgentName);

            //STR_VHMSG_VHCL_KISO_VERSION_RESP reply_str = new STR_VHMSG_VHCL_KISO_VERSION_RESP()
            //{
            //    SeqNum = recive_str.SeqNum,
            //    PacketID = VHMSGIF.ID_VHCL_KISO_VERSION_RESPONSE,
            //    RespCode = 0
            //};

            //string vhVerionTime = new string(recive_str.VerionStr);
            //DateTime.TryParse(vhVerionTime, out eqpt.VhBasisDataVersionTime);

            //resp_cmp = ITcpIpControl.sendSecondary(bcfApp, tcpipAgentName, reply_str);
            //Console.WriteLine("Recive");

            //if (resp_cmp
            //    && eqpt.VHStateMach.CanFire(SCAppConstants.E_VH_EVENT.doDataSync))
            //{
            //    eqpt.VHStateMach.Fire(SCAppConstants.E_VH_EVENT.doDataSync);
            //    doDataSysc();
            //}
        }


        //todo 需掛上實際資料
        object str132_lockObj = new object();
        protected void str132_Receive(object sender, TcpIpEventArgs e)
        {
            if (scApp.getEQObjCacheManager().getLine().ServerPreStop)
                return;

            int threadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
            Stopwatch sw = scApp.StopwatchPool.GetObject();
            string lockTraceInfo = string.Format("VH ID:{0},Pack ID:{1},Seq Num:{2},ThreadID:{3},"
                , eqpt.VEHICLE_ID
                , e.iPacketID
                , e.iSeqNum.ToString()
                , threadID.ToString());
            try
            {
                sw.Start();
                LogManager.GetLogger("LockInfo").Debug(string.Concat(lockTraceInfo, "Wait Lock In"));
                //TODO Wait Lock In
                SCUtility.LockWithTimeout(str132_lockObj, SCAppConstants.LOCK_TIMEOUT_MS, str132_ReceiveProcess, sender, e);
                //Lock Out
            }
            catch (Exception ex)
            {
                LogManager.GetLogger("LockInfo").Error(string.Concat(lockTraceInfo, "Lock Exception"));
                logger.Error(ex, "(str132_Receive) Exception");
            }
            finally
            {
                long ElapsedMilliseconds = sw.ElapsedMilliseconds;
                LogManager.GetLogger("LockInfo").Debug(string.Concat(lockTraceInfo, "Lock Out. ElapsedMilliseconds:", ElapsedMilliseconds.ToString()));
                scApp.StopwatchPool.PutObject(sw);
            }
        }
        protected void str132_ReceiveProcess(object sender, TcpIpEventArgs e)
        {
            ID_132_TRANS_COMPLETE_REPORT recive_str = (ID_132_TRANS_COMPLETE_REPORT)e.objPacket;
            scApp.VehicleBLL.setAndPublishPositionReportInfo2Redis(eqpt.VEHICLE_ID, recive_str);

            dynamic service = scApp.VehicleService;
            service.CommandCompleteReport(tcpipAgentName, bcfApp, eqpt, recive_str, e.iSeqNum);
        }



        //todo 需掛上實際資料
        object str134_lockObj = new object();
        protected void str134_Receive(object sender, TcpIpEventArgs e)
        {
            if (scApp.getEQObjCacheManager().getLine().ServerPreStop)
                return;

            int threadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
            Stopwatch sw = scApp.StopwatchPool.GetObject();
            string lockTraceInfo = string.Format("VH ID:{0},Pack ID:{1},Seq Num:{2},ThreadID:{3},"
                , eqpt.VEHICLE_ID
                , e.iPacketID
                , e.iSeqNum.ToString()
                , threadID.ToString());
            try
            {
                sw.Start();
                LogManager.GetLogger("LockInfo").Debug(string.Concat(lockTraceInfo, "Wait Lock In"));
                //TODO Wait Lock In
                SCUtility.LockWithTimeout(str134_lockObj, SCAppConstants.LOCK_TIMEOUT_MS, str134_ReceiveProcess, sender, e);
                //Lock Out
            }
            catch (Exception ex)
            {
                LogManager.GetLogger("LockInfo").Error(string.Concat(lockTraceInfo, "Lock Exception"));
                logger.Error(ex, "(str134_Receive) Exception");
            }
            finally
            {
                long ElapsedMilliseconds = sw.ElapsedMilliseconds;
                LogManager.GetLogger("LockInfo").Debug(string.Concat(lockTraceInfo, "Lock Out. ElapsedMilliseconds:", ElapsedMilliseconds.ToString()));
                scApp.StopwatchPool.PutObject(sw);
            }
        }

        protected void str134_Receive_new(object sender, TcpIpEventArgs e)
        {
            if (scApp.getEQObjCacheManager().getLine().ServerPreStop)
                return;
            try
            {
                str134_ReceiveProcess(sender, e);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "(str134_Receive) Exception");
            }
        }

        const int IGNORE_SECTION_DISTANCE = 60;
        protected void str134_ReceiveProcess(object sender, TcpIpEventArgs e)
        {
            ID_134_TRANS_EVENT_REP recive_str = (ID_134_TRANS_EVENT_REP)e.objPacket;
            //TODO 需比較是否有位置重複的問題 => OK
            //if (!SCUtility.isMatche(eqpt.CUR_SEC_ID, recive_str.CurrentAdrID) || !SCUtility.isMatche(eqpt.CUR_SEC_ID, recive_str.CurrentSecID))
            //{
            SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, 0, recive_str);
            if (recive_str.SecDistance > IGNORE_SECTION_DISTANCE)
            {
                scApp.VehicleBLL.setAndPublishPositionReportInfo2Redis(eqpt.VEHICLE_ID, recive_str);
            }
            else
            {
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(EQTcpIpMapAction), Device: "OHxC",
                         Data: $"ignore vh:{eqpt.VEHICLE_ID} of position report,because current section distance:{recive_str.SecDistance} less then {IGNORE_SECTION_DISTANCE}",
                         VehicleID: eqpt.VEHICLE_ID);
            }
            //scApp.VehicleBLL.setPositionReportInfo2Redis(eqpt.VEHICLE_ID, recive_str);
            //scApp.VehicleBLL.PublishPositionReportInfo2Redis(eqpt.VEHICLE_ID, recive_str);
            //}
            //dynamic service = scApp.BlockControlServer;
            //dynamic service = scApp.VehicleService;
            //ID_134_TRANS_EVENT_REP recive_str = (ID_134_TRANS_EVENT_REP)e.objPacket;
            //service.PositionReport(bcfApp, eqpt, recive_str, e.iSeqNum);
        }



        object str136_lockObj = new object();
        protected void str136_Receive(object sender, TcpIpEventArgs e)
        {

            if (scApp.getEQObjCacheManager().getLine().ServerPreStop)
                return;

            int threadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
            Stopwatch sw = scApp.StopwatchPool.GetObject();
            string lockTraceInfo = string.Format("VH ID:{0},Pack ID:{1},Seq Num:{2},ThreadID:{3},"
                , eqpt.VEHICLE_ID
                , e.iPacketID
                , e.iSeqNum.ToString()
                , threadID.ToString());
            try
            {
                sw.Start();
                LogManager.GetLogger("LockInfo").Debug(string.Concat(lockTraceInfo, "Wait Lock In"));
                //TODO Wait Lock In
                SCUtility.LockWithTimeout(str136_lockObj, SCAppConstants.LOCK_TIMEOUT_MS, str136_ReceiveProcess, sender, e);
                //Lock Out
            }
            catch (Exception ex)
            {
                LogManager.GetLogger("LockInfo").Error(string.Concat(lockTraceInfo, "Lock Exception"));
                logger.Error(ex, "(str136_Receive) Exception");
            }
            finally
            {
                long ElapsedMilliseconds = sw.ElapsedMilliseconds;
                LogManager.GetLogger("LockInfo").Debug(string.Concat(lockTraceInfo, "Lock Out. ElapsedMilliseconds:", ElapsedMilliseconds.ToString()));
                scApp.StopwatchPool.PutObject(sw);
            }
        }
        protected void str136_ReceiveProcess(object sender, TcpIpEventArgs e)
        {
            //dynamic service = scApp.BlockControlServer;
            dynamic service = scApp.VehicleService;
            ID_136_TRANS_EVENT_REP recive_str = (ID_136_TRANS_EVENT_REP)e.objPacket;
            switch (recive_str.EventType)
            {
                case EventType.BlockRelease:
                case EventType.BlockReq:
                    break;
                default:
                    scApp.VehicleBLL.setAndPublishPositionReportInfo2Redis(eqpt.VEHICLE_ID, recive_str);
                    break;
            }
            service.TranEventReport(bcfApp, eqpt, recive_str, e.iSeqNum);
        }
        //protected void str144_Receive(object sender, TcpIpEventArgs e)
        //{

        //    ID_144_STATUS_CHANGE_REP recive_str = (ID_144_STATUS_CHANGE_REP)e.objPacket;
        //    ID_44_STATUS_CHANGE_RESPONSE send_str = null;
        //    SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, e.iSeqNum, recive_str);

        //    string currentSecID = recive_str.CurrentSecID;
        //    string currentAdrID = recive_str.CurrentAdrID;
        //    VHModeStatus modeStat = recive_str.ModeStatus;
        //    VHActionStatus actionStat = recive_str.ActionStatus;
        //    VhPowerStatus powerStat = recive_str.PowerStatus;
        //    VhLoadCSTStatus hasCST = recive_str.HasCST;
        //    VhStopSingle obstacleStat = recive_str.ObstacleStatus;
        //    VhStopSingle blockingStat = recive_str.BlockingStatus;
        //    VhStopSingle pauseStat = recive_str.PauseStatus;
        //    VhGuideStatus leftGuideStat = recive_str.LeftGuideLockStatus;
        //    VhGuideStatus rightGuideStat = recive_str.RightGuideLockStatus;
        //    int traveDIST = recive_str.SecDistance;
        //    int obstacleDIST = recive_str.ObstDistance;
        //    string obstacleVhID = recive_str.ObstVehicleID;

        //    VHActionStatus preActionStat = eqpt.ACT_STATUS;

        //    send_str = new ID_44_STATUS_CHANGE_RESPONSE
        //    {
        //        ReplyCode = 0
        //    };
        //    WrapperMessage wrapper = new WrapperMessage
        //    {
        //        SeqNum = e.iSeqNum,
        //        StatusChangeResp = send_str
        //    };
        //    Boolean resp_cmp = ITcpIpControl.sendGoogleMsg(bcfApp, tcpipAgentName, wrapper, true);

        //    scApp.VehicleBLL.doUpdateVehicleStatus(eqpt,
        //                           modeStat, actionStat,
        //                           blockingStat, pauseStat, obstacleStat,
        //                           (int)hasCST);

        //    if (preActionStat != actionStat)
        //    {
        //        switch (actionStat)
        //        {
        //            case VHActionStatus.Loading:
        //                scApp.ReportBLL.doReportLoading(eqpt.VEHICLE_ID);
        //                break;
        //            case VHActionStatus.Unloading:
        //                scApp.ReportBLL.doReportUnloading(eqpt.VEHICLE_ID);
        //                break;
        //        }
        //    }

        //    if (actionStat == VHActionStatus.Stop)
        //    {
        //        if (obstacleStat == VhStopSingle.StopSingleOn)
        //        {
        //            whenVhObstacle(obstacleVhID);
        //        }
        //    }
        //    SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, e.iSeqNum, send_str, resp_cmp.ToString());

        //    //eqpt.vhStatusChange(modeStat, actionStat,
        //    //                    hasCST == VhLoadCSTStatus.Exist,
        //    //                    obstacleStat == VhStopSingle.StopSingleOn,
        //    //                    blockingStat == VhStopSingle.StopSingleOn,
        //    //                    pauseStat == VhStopSingle.StopSingleOn);
        //    //scApp.VehicleBLL.doUpdateVehicleStatus(eqpt,
        //    //                                   modeStat, actionStat,
        //    //                                   blockingStat, pauseStat, obstacleStat,
        //    //                                   (int)hasCST);
        //    //AVEHICLE vh = scApp.VehicleBLL.getVehicleByID(eqpt.VEHICLE_ID);

        //    Console.WriteLine("Recive");
        //}
        object str144_lockObj = new object();
        protected void str144_Receive(object sender, TcpIpEventArgs e)
        {

            if (scApp.getEQObjCacheManager().getLine().ServerPreStop)
                return;

            int threadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
            Stopwatch sw = scApp.StopwatchPool.GetObject();
            string lockTraceInfo = string.Format("VH ID:{0},Pack ID:{1},Seq Num:{2},ThreadID:{3},"
                , eqpt.VEHICLE_ID
                , e.iPacketID
                , e.iSeqNum.ToString()
                , threadID.ToString());
            try
            {
                sw.Start();
                LogManager.GetLogger("LockInfo").Debug(string.Concat(lockTraceInfo, "Wait Lock In"));
                //TODO Wait Lock In
                SCUtility.LockWithTimeout(str144_lockObj, SCAppConstants.LOCK_TIMEOUT_MS, str144_ReceiveProcess, sender, e);
                //Lock Out
            }
            catch (Exception ex)
            {
                LogManager.GetLogger("LockInfo").Error(string.Concat(lockTraceInfo, "Lock Exception"));
                logger.Error(ex, "(str144_Receive) Exception");
            }
            finally
            {
                long ElapsedMilliseconds = sw.ElapsedMilliseconds;
                LogManager.GetLogger("LockInfo").Debug(string.Concat(lockTraceInfo, "Lock Out. ElapsedMilliseconds:", ElapsedMilliseconds.ToString()));
                scApp.StopwatchPool.PutObject(sw);
            }
        }

        protected void str144_ReceiveProcess(object sender, TcpIpEventArgs e)
        {
            dynamic service = scApp.VehicleService;
            ID_144_STATUS_CHANGE_REP recive_str = (ID_144_STATUS_CHANGE_REP)e.objPacket;

            scApp.VehicleBLL.setAndPublishPositionReportInfo2Redis(eqpt.VEHICLE_ID, recive_str);


            service.StatusReport(bcfApp, eqpt, recive_str, e.iSeqNum);

            //if (scApp.getEQObjCacheManager().getLine().ServerPreStop)
            //    return;

            //ID_144_STATUS_CHANGE_REP recive_str = (ID_144_STATUS_CHANGE_REP)e.objPacket;
            //SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, e.iSeqNum, recive_str);

            //VHModeStatus modeStat = recive_str.ModeStatus;
            //VHActionStatus actionStat = recive_str.ActionStatus;
            //VhPowerStatus powerStat = recive_str.PowerStatus;
            //VhLoadCSTStatus hasCST = recive_str.HasCST;
            //string cstID = recive_str.CSTID;
            //VhStopSingle obstacleStat = recive_str.ObstacleStatus;
            //VhStopSingle blockingStat = recive_str.BlockingStatus;
            //VhStopSingle pauseStat = recive_str.PauseStatus;
            //VhGuideStatus leftGuideStat = recive_str.LeftGuideLockStatus;
            //VhGuideStatus rightGuideStat = recive_str.RightGuideLockStatus;

            ////string currentSecID = recive_str.CurrentSecID;
            ////string currentAdrID = recive_str.CurrentAdrID;
            ////int traveDIST = recive_str.SecDistance;
            //int obstacleDIST = recive_str.ObstDistance;
            //string obstacleVhID = recive_str.ObstVehicleID;

            //VHActionStatus preActionStat = eqpt.ACT_STATUS;


            //if (!scApp.VehicleBLL.doUpdateVehicleStatus(eqpt,
            //                       modeStat, actionStat,
            //                       blockingStat, pauseStat, obstacleStat,
            //                       (int)hasCST, cstID))
            //{
            //    return;
            //}
            //List<AMCSREPORTQUEUE> reportqueues = null;
            //using (TransactionScope tx = SCUtility.getTransactionScope())
            //{
            //    using (DBConnection_EF con = DBConnection_EF.GetUContext())
            //    {
            //        bool isSuccess = true;
            //        switch (actionStat)
            //        {
            //            case VHActionStatus.Loading:
            //            case VHActionStatus.Unloading:
            //                if (preActionStat != actionStat)
            //                {
            //                    isSuccess = scApp.ReportBLL.ReportLoadingUnloading(eqpt.VEHICLE_ID, actionStat, out reportqueues);
            //                }
            //                break;
            //            default:
            //                isSuccess = true;
            //                break;
            //        }
            //        if (!isSuccess)
            //        {
            //            return;
            //        }
            //        if (reply_status_event_report(e))
            //        {
            //            //scApp.VehicleBLL.updateVehicleStatus_CacheMangerForAct(eqpt, actionStat);
            //            tx.Complete();
            //        }
            //    }
            //}
            //scApp.ReportBLL.sendMCSS6F11MessageAsyn(reportqueues);

            //if (actionStat == VHActionStatus.Stop)
            //{
            //    if (obstacleStat == VhStopSingle.StopSingleOn)
            //    {
            //        if (!SCUtility.isEmpty(obstacleVhID))
            //        {
            //            whenVhObstacle(obstacleVhID);
            //        }
            //    }
            //}
        }

        //todo 需掛上實際資料
        protected void str194_Receive(object sender, TcpIpEventArgs e)
        {
            ID_194_ALARM_REPORT recive_gpp = (ID_194_ALARM_REPORT)e.objPacket;

            dynamic service = scApp.VehicleService;
            service.AlarmReport(bcfApp, eqpt, recive_gpp, e.iSeqNum);

            //STR_VHMSG_ALARM_REP recive_str = TCPUtility._Packet2Str<STR_VHMSG_ALARM_REP>((byte[])e.objPacket, eqpt.TcpIpAgentName);



            //STR_VHMSG_ALARM_RESP reply_str = new STR_VHMSG_ALARM_RESP()
            //{
            //    SeqNum = recive_str.SeqNum,
            //    PacketID = VHMSGIF.ID_TRANS_EVENT_RESPONSE,
            //    RespCode = 1
            //};
            //ITcpIpControl.sendSecondary(bcfApp, tcpipAgentName, reply_str);
            //Console.WriteLine("Recive");

            //eqpt.VHStateMach.Fire(SCAppConstants.E_VH_EVENT.CompensationDataError);
        }

        //private bool reply_status_event_report(TcpIpEventArgs e)
        //{
        //    ID_44_STATUS_CHANGE_RESPONSE send_str = new ID_44_STATUS_CHANGE_RESPONSE
        //    {
        //        ReplyCode = 0
        //    };
        //    WrapperMessage wrapper = new WrapperMessage
        //    {
        //        SeqNum = e.iSeqNum,
        //        StatusChangeResp = send_str
        //    };
        //    Boolean resp_cmp = ITcpIpControl.sendGoogleMsg(bcfApp, tcpipAgentName, wrapper, true);
        //    SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, e.iSeqNum, send_str, resp_cmp.ToString());
        //    return resp_cmp;
        //}


        private static TransactionScope BegingTransaction()
        {
            TransactionScope tx = new TransactionScope
                (TransactionScopeOption.Required, new TransactionOptions { IsolationLevel = SCAppConstants.ISOLATION_LEVEL });
            return tx;
        }

        private void whenObstacleFinish()
        {
            AVEHICLE vh = scApp.VehicleBLL.getVehicleByID(eqpt.VEHICLE_ID);
            if (eqpt.ObstacleStatus == VhStopSingle.StopSingleOff &&
                !SCUtility.isEmpty(vh.MCS_CMD))
            {
                double OCSTime_ms = eqpt.watchObstacleTime.ElapsedMilliseconds;
                double OCSTime_s = OCSTime_ms / 1000;
                OCSTime_s = Math.Round(OCSTime_s, 1);
                if (eqpt.HAS_CST == 0)
                {
                    scApp.SysExcuteQualityBLL.updateSysExecQity_OCSTime2SurceOnTheWay(vh.MCS_CMD, OCSTime_s);
                }
                else
                {
                    scApp.SysExcuteQualityBLL.updateSysExecQity_OCSTime2DestnOnTheWay(vh.MCS_CMD, OCSTime_s);
                }
            }
        }
        private void whenBlockFinish()
        {
            AVEHICLE vh = scApp.VehicleBLL.getVehicleByID(eqpt.VEHICLE_ID);
            if (eqpt.BlockingStatus == VhStopSingle.StopSingleOff &&
                !SCUtility.isEmpty(vh.MCS_CMD))
            {
                double BlockTime_ms = eqpt.watchBlockTime.ElapsedMilliseconds;
                double BlockTime_s = BlockTime_ms / 1000;
                BlockTime_s = Math.Round(BlockTime_s, 1);
                if (eqpt.HAS_CST == 0)
                {
                    scApp.SysExcuteQualityBLL.
                        updateSysExecQity_BlockTime2SurceOnTheWay(vh.MCS_CMD, BlockTime_s);
                }
                else
                {
                    scApp.SysExcuteQualityBLL.
                        updateSysExecQity_BlockTime2DestnOnTheWay(vh.MCS_CMD, BlockTime_s);
                }
            }
        }
        private void whenPauseFinish()
        {
            AVEHICLE vh = scApp.VehicleBLL.getVehicleByID(eqpt.VEHICLE_ID);
            if (eqpt.PauseStatus == VhStopSingle.StopSingleOff &&
                !SCUtility.isEmpty(vh.MCS_CMD))
            {
                double PauseTime_ms = eqpt.watchPauseTime.ElapsedMilliseconds;
                double PauseTime_s = PauseTime_ms / 1000;
                PauseTime_s = Math.Round(PauseTime_s, 1);
                scApp.SysExcuteQualityBLL.updateSysExecQity_PauseTime(vh.MCS_CMD, PauseTime_s);
            }
        }

        //todo 需掛上實際資料
        protected void str162_Receive(object sender, TcpIpEventArgs e)
        {
            //Boolean resp_cmp = false;
            //STR_VHMSG_INDIVIDUAL_DOWNLOAD_REQ recive_str = TCPUtility._Packet2Str<STR_VHMSG_INDIVIDUAL_DOWNLOAD_REQ>((byte[])e.objPacket, eqpt.TcpIpAgentName);

            ////todo 修改成正確的內容
            //STR_VHMSG_INDIVIDUAL_DOWNLOAD_REP reply_str = new STR_VHMSG_INDIVIDUAL_DOWNLOAD_REP()
            //{
            //    SeqNum = recive_str.SeqNum,
            //    PacketID = VHMSGIF.ID_TRANS_EVENT_RESPONSE,
            //    OffsetAddrPos = 10000,
            //    OffsetGuideFL = 20000,
            //    OffsetGuideFR = 30000,
            //    OffsetGuideRL = 40000,
            //    OffsetGuideRR = 50000
            //};
            //resp_cmp = ITcpIpControl.sendSecondary(bcfApp, tcpipAgentName, reply_str);
            //Console.WriteLine("Recive");

            //if (resp_cmp)
            //    eqpt.VHStateMach.Fire(SCAppConstants.E_VH_EVENT.CompensationDataRep);
        }




        public override bool send_Str31(ID_31_TRANS_REQUEST send_gpp, out ID_131_TRANS_RESPONSE receive_gpp, out string reason)
        {
            bool isSuccess = false;
            try
            {

                WrapperMessage wrapper = new WrapperMessage
                {
                    ID = VHMSGIF.ID_TRANS_REQUEST,
                    TransReq = send_gpp
                };
                //SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, 0, send_gpp);
                com.mirle.iibg3k0.ttc.Common.TrxTcpIp.ReturnCode result = snedRecv(wrapper, out receive_gpp, out reason);
                //SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, 0, receive_gpp, result.ToString());
                isSuccess = result == TrxTcpIp.ReturnCode.Normal;
                reason = receive_gpp.NgReason;
                if (isSuccess)
                    isSuccess = receive_gpp.ReplyCode == 0;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception");
                receive_gpp = null;
                reason = "命令下達時發生錯誤!";
            }
            return isSuccess;
        }

        public override bool send_Str37(string cmd_id, CMDCancelType actType)
        {
            bool isScuess = false;
            try
            {
                //由於A00的要求，希望可以在收到命令後 走完一個Section後再進行Cancel，因此先加入此Function
                //WaitPassOneSection();

                string rtnMsg = string.Empty;
                ID_37_TRANS_CANCEL_REQUEST stSend;
                ID_137_TRANS_CANCEL_RESPONSE stRecv;
                stSend = new ID_37_TRANS_CANCEL_REQUEST()
                {
                    CmdID = cmd_id,
                    ActType = actType
                };

                WrapperMessage wrapper = new WrapperMessage
                {
                    ID = VHMSGIF.ID_TRANS_CANCEL_REQUEST,
                    TransCancelReq = stSend
                };

                SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, 0, stSend);
                com.mirle.iibg3k0.ttc.Common.TrxTcpIp.ReturnCode result = snedRecv(wrapper, out stRecv, out rtnMsg);
                SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, 0, stRecv, result.ToString());
                if (result == TrxTcpIp.ReturnCode.Normal)
                {
                    if (stRecv.ReplyCode == 0)
                    {
                        isScuess = true;
                    }
                    else
                    {
                        isScuess = false;
                    }
                }
                else
                {
                    isScuess = false;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception");
            }
            return isScuess;
        }

        public override bool send_Str39(ID_39_PAUSE_REQUEST send_gpp, out ID_139_PAUSE_RESPONSE receive_gpp)
        {
            bool isScuess = false;
            try
            {
                string rtnMsg = string.Empty;
                WrapperMessage wrapper = new WrapperMessage
                {
                    ID = VHMSGIF.ID_PAUSE_REQUEST,
                    PauseReq = send_gpp
                };
                // SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, 0, send_gpp);
                com.mirle.iibg3k0.ttc.Common.TrxTcpIp.ReturnCode result = snedRecv(wrapper, out receive_gpp, out rtnMsg);
                //SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, 0, receive_gpp, result.ToString());
                //isScuess = result == TrxTcpIp.ReturnCode.Normal;
                isScuess = result == TrxTcpIp.ReturnCode.Normal &&
                           receive_gpp.ReplyCode == 0;

                //ID_39_PAUSE_REQUEST stSend;
                //ID_139_PAUSE_RESPONSE stRecv;
                //stSend = new ID_39_PAUSE_REQUEST()
                //{
                //    EventType = eventType,
                //    PauseType = pauseType
                //};


                //SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, 0, stSend);
                //com.mirle.iibg3k0.ttc.Common.TrxTcpIp.ReturnCode result = snedRecv(wrapper, out stRecv, out rtnMsg);
                //SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, 0, stRecv, result.ToString());
                //isScuess = result == TrxTcpIp.ReturnCode.Normal;
            }
            catch (Exception ex)
            {
                receive_gpp = null;
                logger.Error(ex, "Exception");
            }
            return isScuess;

        }

        public override bool send_Str43(ID_43_STATUS_REQUEST send_gpp, out ID_143_STATUS_RESPONSE receive_gpp)
        {
            bool isScuess = false;
            try
            {
                string rtnMsg = string.Empty;
                WrapperMessage wrapper = new WrapperMessage
                {
                    ID = VHMSGIF.ID_PAUSE_REQUEST,
                    StatusReq = send_gpp
                };
                com.mirle.iibg3k0.ttc.Common.TrxTcpIp.ReturnCode result = snedRecv(wrapper, out receive_gpp, out rtnMsg);
                isScuess = result == TrxTcpIp.ReturnCode.Normal;
            }
            catch (Exception ex)
            {
                receive_gpp = null;
                logger.Error(ex, "Exception");
            }
            return isScuess;

        }

        public override bool send_Str71(ID_71_RANGE_TEACHING_REQUEST send_gpp, out ID_171_RANGE_TEACHING_RESPONSE receive_gpp)
        {
            bool isScuess = false;
            try
            {
                string rtnMsg = string.Empty;
                WrapperMessage wrapper = new WrapperMessage
                {
                    ID = VHMSGIF.ID_SECTION_TEACH_REQUEST,
                    RangeTeachingReq = send_gpp
                };
                com.mirle.iibg3k0.ttc.Common.TrxTcpIp.ReturnCode result = snedRecv(wrapper, out receive_gpp, out rtnMsg);
                isScuess = result == TrxTcpIp.ReturnCode.Normal;
            }
            catch (Exception ex)
            {
                receive_gpp = null;
                logger.Error(ex, "Exception");
            }
            return isScuess;

        }
        public override bool sned_Str41(ID_41_MODE_CHANGE_REQ send_gpp, out ID_141_MODE_CHANGE_RESPONSE receive_gpp)
        {
            bool isScuess = false;
            try
            {
                string rtnMsg = string.Empty;
                WrapperMessage wrapper = new WrapperMessage
                {
                    ID = VHMSGIF.ID_MODE_CHANGE_REQUEST,
                    ModeChangeReq = send_gpp
                };
                com.mirle.iibg3k0.ttc.Common.TrxTcpIp.ReturnCode result = snedRecv(wrapper, out receive_gpp, out rtnMsg);
                isScuess = result == TrxTcpIp.ReturnCode.Normal;
            }
            catch (Exception ex)
            {
                receive_gpp = null;
                logger.Error(ex, "Exception");
            }
            return isScuess;

        }
        public override bool sned_Str91(ID_91_ALARM_RESET_REQUEST send_gpp, out ID_191_ALARM_RESET_RESPONSE receive_gpp)
        {
            bool isScuess = false;
            try
            {
                string rtnMsg = string.Empty;
                WrapperMessage wrapper = new WrapperMessage
                {
                    ID = VHMSGIF.ID_PAUSE_REQUEST,
                    AlarmResetReq = send_gpp
                };
                com.mirle.iibg3k0.ttc.Common.TrxTcpIp.ReturnCode result = snedRecv(wrapper, out receive_gpp, out rtnMsg);
                isScuess = result == TrxTcpIp.ReturnCode.Normal;
            }
            catch (Exception ex)
            {
                receive_gpp = null;
                logger.Error(ex, "Exception");
            }
            return isScuess;
        }


        public override bool snedMessage(WrapperMessage wrapper, bool isReply = false)
        {
            Boolean resp_cmp = ITcpIpControl.sendGoogleMsg(bcfApp, tcpipAgentName, wrapper, true);
            return resp_cmp;
        }
        object sendRecv_LockObj = new object();
        public override com.mirle.iibg3k0.ttc.Common.TrxTcpIp.ReturnCode snedRecv<TSource2>(WrapperMessage wrapper, out TSource2 stRecv, out string rtnMsg)
        {
            //lock (sendRecv_LockObj)
            //{
            //    return ITcpIpControl.sendRecv_Google(bcfApp, tcpipAgentName, wrapper, out stRecv, out rtnMsg);
            //}
            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(sendRecv_LockObj, SCAppConstants.LOCK_TIMEOUT_MS, ref lockTaken);
                if (!lockTaken)
                    throw new TimeoutException("snedRecv time out lock happen");
                return ITcpIpControl.sendRecv_Google(bcfApp, tcpipAgentName, wrapper, out stRecv, out rtnMsg);
            }
            finally
            {
                if (lockTaken) Monitor.Exit(sendRecv_LockObj);
            }
        }

        public override void PLC_Control_TrunOff() { /*Nothing...*/ }




        //protected void Connection(object sender, TcpIpEventArgs e)
        //{
        //    scApp.getEQObjCacheManager().refreshVh(eqpt.VEHICLE_ID);
        //    eqpt.VhRecentTranEvent = EventType.AdrPass;
        //    //eqpt.NotifyVhPositionChange();
        //    //eqpt.NotifyVhStatusChange();
        //    eqpt.isTcpIpConnect = true;

        //    SCUtility.RecodeConnectionInfo
        //        (eqpt.VEHICLE_ID,
        //        SCAppConstants.RecodeConnectionInfo_Type.Connection.ToString(),
        //        eqpt.getDisconnectionIntervalTime(bcfApp));

        //}
        //protected void Disconnection(object sender, TcpIpEventArgs e)
        //{
        //    eqpt.isTcpIpConnect = false;

        //    SCUtility.RecodeConnectionInfo
        //        (eqpt.VEHICLE_ID,
        //        SCAppConstants.RecodeConnectionInfo_Type.Disconnection.ToString(),
        //        eqpt.getConnectionIntervalTime(bcfApp));
        //}
        protected void ReplyTimeOutHandler(object sender, TcpIpEventArgs e)
        {
            TcpIpExceptionEventArgs excptionArg = e as TcpIpExceptionEventArgs;
            if (e == null) return;
            scApp.AlarmBLL.onMainAlarm(SCAppConstants.MainAlarmCode.VH_WAIT_REPLY_TIME_OUT_0_1_2
                                       , eqpt.VEHICLE_ID
                                       , e.iPacketID
                                       , e.iSeqNum);
        }
        protected void SendErrorHandler(object sender, TcpIpEventArgs e)
        {
            TcpIpExceptionEventArgs excptionArg = e as TcpIpExceptionEventArgs;
            if (e == null) return;

            scApp.AlarmBLL.onMainAlarm(SCAppConstants.MainAlarmCode.VH_SEND_MSG_ERROR_0_1_2
                           , eqpt.VEHICLE_ID
                           , e.iPacketID
                           , e.iSeqNum);
        }

        protected void SendRecvStateChangeHandler(object sender, TcpIpAgent.E_Msg_STS msg_satae)
        {
            eqpt.TcpIp_Msg_State = msg_satae.ToString();
        }


        public static Google.Protobuf.IMessage unPackWrapperMsg(byte[] raw_data)
        {
            WrapperMessage WarpperMsg = ToObject<WrapperMessage>(raw_data);
            return WarpperMsg;
        }
        public static T ToObject<T>(byte[] buf) where T : Google.Protobuf.IMessage<T>, new()
        {
            if (buf == null)
                return default(T);
            Google.Protobuf.MessageParser<T> parser = new Google.Protobuf.MessageParser<T>(() => new T());
            return parser.ParseFrom(buf);
        }


        string event_id = string.Empty;
        /// <summary>
        /// Does the initialize.
        /// </summary>
        public override void doInit()
        {
            try
            {
                if (eqpt == null)
                    return;
                event_id = "EQTcpIpMapAction_" + eqpt.VEHICLE_ID;
                tcpipAgentName = eqpt.TcpIpAgentName;
                //======================================連線狀態=====================================================
                RegisteredTcpIpProcEvent();




                ITcpIpControl.addTcpIpConnectedHandler(bcfApp, tcpipAgentName, Connection);
                ITcpIpControl.addTcpIpDisconnectedHandler(bcfApp, tcpipAgentName, Disconnection);

                ITcpIpControl.addTcpIpReplyTimeOutHandler(bcfApp, tcpipAgentName, ReplyTimeOutHandler);
                ITcpIpControl.addTcpIpSendErrorHandler(bcfApp, tcpipAgentName, SendErrorHandler);
                ITcpIpControl.addSendRecvStateChangeHandler(bcfApp, tcpipAgentName, SendRecvStateChangeHandler);

                //d.str134_Receive(null, null);
                eqpt.addEventHandler(event_id
                    , BCFUtility.getPropertyName(() => eqpt.ObstacleStatus)
                    , (s1, e1) => { whenObstacleFinish(); });
                eqpt.addEventHandler(event_id
                    , BCFUtility.getPropertyName(() => eqpt.BlockingStatus)
                    , (s1, e1) => { whenBlockFinish(); });
                eqpt.addEventHandler(event_id
                    , BCFUtility.getPropertyName(() => eqpt.PauseStatus)
                    , (s1, e1) => { whenPauseFinish(); });
            }
            catch (Exception ex)
            {
                scApp.getBCFApplication().onSMAppError(0, "MapActionEQType2Secs doInit");
                logger.Error(ex, "Exection:");
            }

        }

        public override void RegisteredTcpIpProcEvent()
        {
            ITcpIpControl.addTcpIpReceivedHandler(bcfApp, tcpipAgentName, VHMSGIF.ID_VHCL_KISO_VERSION_REPORT.ToString(), str102_Receive);
            ITcpIpControl.addTcpIpReceivedHandler(bcfApp, tcpipAgentName, VHMSGIF.ID_GUIDE_DATA_UPLOAD_REQUEST.ToString(), str162_Receive);
            ITcpIpControl.addTcpIpReceivedHandler(bcfApp, tcpipAgentName, VHMSGIF.ID_ALARM_REPORT.ToString(), str194_Receive);

            ITcpIpControl.addTcpIpReceivedHandler(bcfApp, tcpipAgentName, VHMSGIF.ID_TRANS_COMPLETE_REPORT.ToString(), str132_Receive);
            ITcpIpControl.addTcpIpReceivedHandler(bcfApp, tcpipAgentName, VHMSGIF.ID_TRANS_PASS_EVENT_REPORT.ToString(), str134_Receive_new);
            ITcpIpControl.addTcpIpReceivedHandler(bcfApp, tcpipAgentName, VHMSGIF.ID_TRANS_EVENT_REPORT.ToString(), str136_Receive);
            ITcpIpControl.addTcpIpReceivedHandler(bcfApp, tcpipAgentName, VHMSGIF.ID_STATUS_CHANGE_REPORT.ToString(), str144_Receive);
        }
        public override void UnRgisteredProcEvent()
        {
            ITcpIpControl.removeTcpIpReceivedHandler(bcfApp, tcpipAgentName, VHMSGIF.ID_VHCL_KISO_VERSION_REPORT.ToString(), str102_Receive);
            ITcpIpControl.removeTcpIpReceivedHandler(bcfApp, tcpipAgentName, VHMSGIF.ID_GUIDE_DATA_UPLOAD_REQUEST.ToString(), str162_Receive);
            ITcpIpControl.removeTcpIpReceivedHandler(bcfApp, tcpipAgentName, VHMSGIF.ID_ALARM_REPORT.ToString(), str194_Receive);

            ITcpIpControl.removeTcpIpReceivedHandler(bcfApp, tcpipAgentName, VHMSGIF.ID_TRANS_COMPLETE_REPORT.ToString(), str132_Receive);
            ITcpIpControl.removeTcpIpReceivedHandler(bcfApp, tcpipAgentName, VHMSGIF.ID_TRANS_PASS_EVENT_REPORT.ToString(), str134_Receive_new);
            ITcpIpControl.removeTcpIpReceivedHandler(bcfApp, tcpipAgentName, VHMSGIF.ID_STATUS_CHANGE_REPORT.ToString(), str144_Receive);
        }
    }
}
