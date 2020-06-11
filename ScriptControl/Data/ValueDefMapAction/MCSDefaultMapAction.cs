//*********************************************************************************
//      MESDefaultMapAction.cs
//*********************************************************************************
// File Name: MESDefaultMapAction.cs
// Description: 與EAP通訊的劇本
//
//(c) Copyright 2014, MIRLE Automation Corporation
//
// Date          Author         Request No.    Tag     Description
// ------------- -------------  -------------  ------  -----------------------------
//**********************************************************************************
using System;
using com.mirle.ibg3k0.bcf.App;
using com.mirle.ibg3k0.bcf.Controller;
using com.mirle.ibg3k0.bcf.Data.TimerAction;
using com.mirle.ibg3k0.bcf.Data.ValueDefMapAction;
using com.mirle.ibg3k0.bcf.Data.VO;
using com.mirle.ibg3k0.sc.App;
using com.mirle.ibg3k0.sc.BLL;
using com.mirle.ibg3k0.sc.Common;
using com.mirle.ibg3k0.sc.Data.SECS;
using com.mirle.ibg3k0.sc.Data.VO;
using com.mirle.ibg3k0.stc.Common;
using com.mirle.ibg3k0.stc.Data.SecsData;
using NLog;
using com.mirle.ibg3k0.bcf.Common;
using System.Collections.Generic;
using com.mirle.ibg3k0.sc.Data.VO.Interface;
using System.Linq;
using System.Transactions;
using System.Globalization;

namespace com.mirle.ibg3k0.sc.Data.ValueDefMapAction
{
    /// <summary>
    /// Class MESDefaultMapAction.
    /// </summary>
    /// <seealso cref="com.mirle.ibg3k0.bcf.Data.ValueDefMapAction.IValueDefMapAction" />
    public class MCSDefaultMapAction : IValueDefMapAction
    {
        const string DEVICE_NAME_MCS = "MCS";
        const string CALL_CONTEXT_KEY_WORD_SERVICE_ID_MCS = "MCS Service";

        private static Logger logger = LogManager.GetCurrentClassLogger();
        /// <summary>
        /// The glass TRN logger
        /// </summary>
        private static Logger GlassTrnLogger = LogManager.GetLogger("GlassTransferRpt_EAP");
        /// <summary>
        /// The logger_ map action log
        /// </summary>
        protected static Logger logger_MapActionLog = LogManager.GetLogger("MapActioLog");
        /// <summary>
        /// The sc application
        /// </summary>
        protected SCApplication scApp = null;
        /// <summary>
        /// The BCF application
        /// </summary>
        protected BCFApplication bcfApp = null;
        /// <summary>
        /// The line
        /// </summary>
        protected ALINE line = null;
        /// <summary>
        /// The event BLL
        /// </summary>
        protected EventBLL eventBLL = null;

        /// <summary>
        /// 僅在測試階段使用
        /// </summary>
        protected bool isOnlineWithMcs = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="MCSDefaultMapAction"/> class.
        /// </summary>
        public MCSDefaultMapAction()
        {
            scApp = SCApplication.getInstance();
            bcfApp = scApp.getBCFApplication();
            eventBLL = scApp.EventBLL;
        }

        /// <summary>
        /// Gets the identity key.
        /// </summary>
        /// <returns>System.String.</returns>
        public virtual string getIdentityKey()
        {
            return this.GetType().Name;
        }

        /// <summary>
        /// Sets the context.
        /// </summary>
        /// <param name="baseEQ">The base eq.</param>
        public virtual void setContext(BaseEQObject baseEQ)
        {
            this.line = baseEQ as ALINE;
        }

        /// <summary>
        /// Uns the register event.
        /// </summary>
        public virtual void unRegisterEvent()
        {

        }

        /// <summary>
        /// Does the share memory initialize.
        /// </summary>
        /// <param name="runLevel">The run level.</param>
        public virtual void doShareMemoryInit(BCFAppConstants.RUN_LEVEL runLevel)
        {
            try
            {
                switch (runLevel)
                {
                    case BCFAppConstants.RUN_LEVEL.ZERO:
                        SECSConst.setDicCEIDAndRPTID(scApp.CEIDBLL.loadDicCEIDAndRPTID());
                        SECSConst.setDicRPTIDAndVID(scApp.CEIDBLL.loadDicRPTIDAndVID());
                        break;
                    case BCFAppConstants.RUN_LEVEL.ONE:
                        break;
                    case BCFAppConstants.RUN_LEVEL.TWO:
                        break;
                    case BCFAppConstants.RUN_LEVEL.NINE:
                        scApp.CMDBLL.initialMapAction();
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exection:");
            }
        }

        #region Host Receive
        protected virtual void s1f13_Establish_Communication_Request(object sender, SECSEventArgs e)
        {
            try
            {
                S1F13_Empty s1f13 = ((S1F13_Empty)e.secsHandler.Parse<S1F13_Empty>(e));
                SCUtility.secsActionRecordMsg(scApp, true, s1f13);
                SCUtility.actionRecordMsg(scApp, s1f13.StreamFunction, line.Real_ID,
                        "Receive Establish Communication From MES.", "");
                //if (!isProcessEAP(s1f13)) { return; }
                S1F14 s1f14 = new S1F14();
                s1f14.SECSAgentName = scApp.EAPSecsAgentName;
                s1f14.SystemByte = s1f13.SystemByte;
                s1f14.COMMACK = "0";
                s1f14.VERSION_INFO = new string[2]
                { "OHS",
                  SCAppConstants.getMainFormVersion("") };

                SCUtility.secsActionRecordMsg(scApp, false, s1f14);
                TrxSECS.ReturnCode rtnCode = ISECSControl.replySECS(bcfApp, s1f14);
                SCUtility.actionRecordMsg(scApp, s1f13.StreamFunction, line.Real_ID,
                        "Reply Establish Communication To MES.", rtnCode.ToString());
                if (rtnCode != TrxSECS.ReturnCode.Normal)
                {
                    logger.Warn("Reply EAP S1F14 Error:{0}", rtnCode);
                }
                logger.Debug("s1f13Receive ok!");
                line.EstablishComm = true;
            }
            catch (Exception ex)
            {
                logger.Error("MESDefaultMapAction has Error[Line Name:{0}],[Error method:{1}],[Error Message:{2}",
                    line.LINE_ID, "s1f13_Receive_EstablishCommunication", ex.ToString());
            }
        }

        protected virtual void S1F17_Request_On_Line(object sender, SECSEventArgs e)
        {
            try
            {
                string msg = string.Empty; //A0.05

                //if (!line.EstablishComm)
                //{
                //    if (!sendS1F13_Establish_Comm())
                //        return;
                //}

                S1F17 s1f17 = ((S1F17)e.secsHandler.Parse<S1F17>(e));
                SCUtility.secsActionRecordMsg(scApp, true, s1f17);

                if (!isProcessEAP(s1f17)) { return; }

                S1F18 s1f18 = new S1F18();
                s1f18.SystemByte = s1f17.SystemByte;
                s1f18.SECSAgentName = scApp.EAPSecsAgentName;


                //檢查狀態是否允許連線
                if (DebugParameter.RejectEAPOnline)
                {
                    s1f18.ONLACK = SECSConst.ONLACK_Not_Accepted;
                }
                else if (line.Host_Control_State == SCAppConstants.LineHostControlState.HostControlState.Host_Online)
                {
                    s1f18.ONLACK = SECSConst.ONLACK_Equipment_Already_On_Line;
                    msg = "OHS is online remote ready!!"; //A0.05
                }
                else
                {
                    s1f18.ONLACK = SECSConst.ONLACK_Accepted;
                }


                TrxSECS.ReturnCode rtnCode = ISECSControl.replySECS(bcfApp, s1f18);
                SCUtility.secsActionRecordMsg(scApp, false, s1f18);
                if (rtnCode != TrxSECS.ReturnCode.Normal)
                {
                    logger.Warn("Reply EQPT S1F18 Error:{0}", rtnCode);
                }

                if (BCFUtility.isMatche(s1f18.ONLACK, SECSConst.ONLACK_Accepted))
                {
                    line.Host_Control_State = SCAppConstants.LineHostControlState.HostControlState.On_Line_Remote;
                    //   onLineScenario(SCAppConstants.LineHostMode.OnLineRemote);
                }
                else
                {
                    // SCUtility.systemTraceMsg(msg); //A0.05
                    return;
                }
            }
            catch (Exception ex)
            {
                logger.Error("MESDefaultMapAction has Error[Line Name:{0}],[Error method:{1}],[Error Message:{2}", line.LINE_ID, "S1F17_Receive_OnlineRequest", ex.ToString());
            }
        }

        protected void S1F3_Selected_Equipment_Status_Request(object sender, SECSEventArgs e)
        {
            try
            {
                S1F3 s1f3 = ((S1F3)e.secsHandler.Parse<S1F3>(e));
                SCUtility.secsActionRecordMsg(scApp, true, s1f3);
                int count = s1f3.SVID.Count();
                S1F4 s1f4 = new S1F4();
                s1f4.SECSAgentName = scApp.EAPSecsAgentName;
                s1f4.SystemByte = s1f3.SystemByte;
                s1f4.SV = new SXFY[count];
                for (int i = 0; i < count; i++)
                {
                    if (s1f3.SVID[i] == SECSConst.VID_AlarmsSet)
                    {
                        //TODO Set Alarm List
                        s1f4.SV[i] = new S6F11.RPTINFO.RPTITEM.VIDITEM_04();


                    }
                    else if (s1f3.SVID[i] == SECSConst.VID_ControlState)
                    {
                        string control_state = SCAppConstants.LineHostControlState.convert2MES(line.Host_Control_State);
                        s1f4.SV[i] = new S6F11.RPTINFO.RPTITEM.VIDITEM_06()
                        {
                            CONTROLSTATE = control_state
                        };
                    }
                    else if (s1f3.SVID[i] == SECSConst.VID_ActiveVehicles)
                    {
                        List<AVEHICLE> vhs = scApp.getEQObjCacheManager().getAllVehicle();
                        int vhs_count = vhs.Count;
                        S6F11.RPTINFO.RPTITEM.VIDITEM_71[] VEHICLEINFOs = new S6F11.RPTINFO.RPTITEM.VIDITEM_71[vhs_count];
                        for (int j = 0; j < vhs_count; j++)
                        {
                            VEHICLEINFOs[j] = new S6F11.RPTINFO.RPTITEM.VIDITEM_71()
                            {
                                VHINFO = new S6F11.RPTINFO.RPTITEM.VIDITEM_71.VEHICLEINFO()
                                {
                                    VEHICLE_ID = vhs[j].VEHICLE_ID,
                                    VEHICLE_STATE = "2"
                                }
                            };
                        }
                        s1f4.SV[i] = new S6F11.RPTINFO.RPTITEM.VIDITEM_53()
                        {
                            VEHICLEINFO = VEHICLEINFOs
                        };
                    }
                    else if (s1f3.SVID[i] == SECSConst.VID_SCState)
                    {
                        string sc_state = SCAppConstants.LineSCState.convert2MES(line.SCStats);
                        s1f4.SV[i] = new S6F11.RPTINFO.RPTITEM.VIDITEM_73()
                        {
                            SCSTATE = sc_state
                        };
                    }
                    else if (s1f3.SVID[i] == SECSConst.VID_EnhancedTransfers)
                    {
                        List<ACMD_MCS> mcs_cmds = scApp.CMDBLL.loadACMD_MCSIsUnfinished();
                        int cmd_count = mcs_cmds.Count;
                        S6F11.RPTINFO.RPTITEM.VIDITEM_13[] EnhancedTransferCmds = new S6F11.RPTINFO.RPTITEM.VIDITEM_13[cmd_count];
                        for (int k = 0; k < cmd_count; k++)
                        {
                            ACMD_MCS mcs_cmd = mcs_cmds[k];
                            string transfer_state = SCAppConstants.TransferState.convert2MES(mcs_cmd.TRANSFERSTATE);
                            EnhancedTransferCmds[k] = new S6F11.RPTINFO.RPTITEM.VIDITEM_13();
                            EnhancedTransferCmds[k].TRANSFER_STATE.TRANSFER_STATE = transfer_state;

                            EnhancedTransferCmds[k].COMMAND_INFO.COMMAND_ID.COMMAND_ID = mcs_cmd.CMD_ID;
                            EnhancedTransferCmds[k].COMMAND_INFO.PRIORITY.PRIORITY = mcs_cmd.PRIORITY.ToString();

                            EnhancedTransferCmds[k].TRANSFER_INFO.CARRIER_ID = mcs_cmd.CARRIER_ID;
                            EnhancedTransferCmds[k].TRANSFER_INFO.SOURCE_PORT = mcs_cmd.HOSTSOURCE;
                            EnhancedTransferCmds[k].TRANSFER_INFO.DESTINATION_PORT = mcs_cmd.HOSTDESTINATION;
                        }

                        s1f4.SV[i] = new S6F11.RPTINFO.RPTITEM.VIDITEM_76()
                        {
                            EnhancedTransferCmd = EnhancedTransferCmds
                        };
                    }
                    else if (s1f3.SVID[i] == SECSConst.VID_Spec_Version)
                    {
                        s1f4.SV[i] = new S6F11.RPTINFO.RPTITEM.VIDITEM_114()
                        {
                            SPEC_VERSION = string.Empty // TODO fill in
                        };
                    }
                    else if (s1f3.SVID[i] == SECSConst.VID_Enhanced_Carriers)
                    {
                        List<AVEHICLE> has_carry_vhs = scApp.getEQObjCacheManager().getAllVehicle().Where(vh => vh.HAS_CST == 1).ToList();
                        int carry_vhs_count = has_carry_vhs.Count;
                        S6F11.RPTINFO.RPTITEM.VIDITEM_10[] carrier_info = new S6F11.RPTINFO.RPTITEM.VIDITEM_10[carry_vhs_count];
                        for (int j = 0; j < carry_vhs_count; j++)
                        {
                            carrier_info[j] = new S6F11.RPTINFO.RPTITEM.VIDITEM_10();
                            carrier_info[j].CARRIER_ID_OBJ.CARRIER_ID = has_carry_vhs[j].CST_ID.Trim();
                            carrier_info[j].CARRIER_LOC_OBJ.CARRIER_LOC = has_carry_vhs[j].VEHICLE_ID;
                            carrier_info[j].INSTALL_TIME_OBJ.INSTALLTIME = string.Empty; //TODO 要填入進入的時間
                            carrier_info[j].CARRIER_ZONE_NAME = string.Empty;
                            carrier_info[j].CARRIER_STATE = string.Empty;
                        }


                        s1f4.SV[i] = new S6F11.RPTINFO.RPTITEM.VIDITEM_51()
                        {
                            ENHANCED_CARRIER_INFO = carrier_info
                        };
                    }
                    else if (s1f3.SVID[i] == SECSConst.VID_Current_Port_States)
                    {
                        List<APORTSTATION> port_station = scApp.MapBLL.loadAllPort();
                        int port_count = port_station.Count;
                        var vid_118 = new S6F11.RPTINFO.RPTITEM.VIDITEM_118();
                        vid_118.PORT_INFO = new S6F11.RPTINFO.RPTITEM.VIDITEM_354[port_count];
                        for (int j = 0; j < port_count; j++)
                        {
                            vid_118.PORT_INFO[j] = new S6F11.RPTINFO.RPTITEM.VIDITEM_354();
                            vid_118.PORT_INFO[j].PORT_ID.PORT_ID = port_station[j].PORT_ID;
                            vid_118.PORT_INFO[j].PORT_TRANSFTER_STATE.PORT_TRANSFER_STATE =
                                ((int)port_station[j].PORT_STATUS).ToString();
                        }
                        s1f4.SV[i] = vid_118;

                    }
                    else
                    {
                        s1f4.SV[i] = new S6F11.RPTINFO.RPTITEM.VIDITEM_04();
                    }
                }



                TrxSECS.ReturnCode rtnCode = ISECSControl.replySECS(bcfApp, s1f4);
                SCUtility.secsActionRecordMsg(scApp, false, s1f4);
            }
            catch (Exception ex)
            {
                logger.Error("MESDefaultMapAction has Error[Line Name:{0}],[Error method:{1}],[Error Message:{2}",
                    line.LINE_ID, "S1F3_Receive_Eqpt_Stat_Req", ex.ToString());
            }
        }




        protected virtual void S2F13_Receive(object sender, SECSEventArgs e)
        {
            try
            {
                S2F13 s2f13 = ((S2F13)e.secsHandler.Parse<S2F13>(e));
                SCUtility.secsActionRecordMsg(scApp, true, s2f13);
                //if (!isProcessEAP(s2f37)) { return; }

                S2F14 s2f14 = null;
                s2f14 = new S2F14();
                s2f14.SystemByte = s2f13.SystemByte;
                s2f14.SECSAgentName = scApp.EAPSecsAgentName;
                s2f14.ECVS = new string[] { "OHT" };

                TrxSECS.ReturnCode rtnCode = ISECSControl.replySECS(bcfApp, s2f14);
                SCUtility.secsActionRecordMsg(scApp, false, s2f14);
                if (rtnCode != TrxSECS.ReturnCode.Normal)
                {
                    logger.Warn("Reply EQPT S2F18 Error:{0}", rtnCode);
                }
            }
            catch (Exception ex)
            {
                logger.Error("MESDefaultMapAction has Error[Line Name:{0}],[Error method:{1}],[Error Message:{2}", line.LINE_ID, "S2F17_Receive_Date_Time_Req", ex.ToString());
            }
        }

        protected void S2F31_Receive_Date_Time_Set_Req(object sender, SECSEventArgs e)
        {
            try
            {
                S2F31 s2f31 = ((S2F31)e.secsHandler.Parse<S2F31>(e));

                SCUtility.secsActionRecordMsg(scApp, true, s2f31);
                SCUtility.actionRecordMsg(scApp, s2f31.StreamFunction, line.Real_ID,
                        "Receive Date Time Set Request From MES.", "");
                if (!isProcessEAP(s2f31)) { return; }

                S2F32 s2f32 = new S2F32();
                s2f32.SECSAgentName = scApp.EAPSecsAgentName;
                s2f32.SystemByte = s2f31.SystemByte;
                s2f32.TIACK = SECSConst.TIACK_Accepted;

                string timeStr = s2f31.TIME;
                DateTime mesDateTime = DateTime.Now;
                try
                {
                    mesDateTime = DateTime.ParseExact(timeStr.Trim(), SCAppConstants.TimestampFormat_16, CultureInfo.CurrentCulture);//A0.08
                }
                catch (Exception dtEx)
                {
                    s2f32.TIACK = SECSConst.TIACK_Error_not_done;
                }

                SCUtility.secsActionRecordMsg(scApp, false, s2f32);
                ISECSControl.replySECS(bcfApp, s2f32);

                if (!DebugParameter.DisableSyncTime)
                {
                    SCUtility.updateSystemTime(mesDateTime);
                }

                //TODO 與設備同步
            }
            catch (Exception ex)
            {
                logger.Error("MESDefaultMapAction has Error[Line Name:{0}],[Error method:{1}],[Error Message:{2}",
                    line.LINE_ID, "S2F31_Receive_Date_Time_Set_Req", ex.ToString());
            }
        }

        protected virtual void S2F37_EnableDisable_Event_Report(object sender, SECSEventArgs e)
        {
            try
            {
                S2F37 s2f37 = ((S2F37)e.secsHandler.Parse<S2F37>(e));
                SCUtility.secsActionRecordMsg(scApp, true, s2f37);
                if (!isProcessEAP(s2f37)) { return; }
                Boolean isValid = true;
                Boolean isEnable = BCFUtility.isMatche(s2f37.CEED, SECSConst.CEED_Enable);

                int cnt = s2f37.CEIDS.Length;
                if (cnt == 0)
                {
                    isValid &= scApp.EventBLL.enableAllEventReport(isEnable);
                }
                else
                {
                    //Check Data
                    for (int ix = 0; ix < cnt; ++ix)
                    {
                        string ceid = s2f37.CEIDS[ix];
                        Boolean isContain = SECSConst.CEID_ARRAY.Contains(ceid.Trim());
                        if (!isContain)
                        {
                            isValid = false;
                            break;
                        }
                    }
                    if (isValid)
                    {
                        for (int ix = 0; ix < cnt; ++ix)
                        {
                            string ceid = s2f37.CEIDS[ix];
                            isValid &= scApp.EventBLL.enableEventReport(ceid, isEnable);
                        }
                    }
                }

                S2F38 s2f18 = null;
                s2f18 = new S2F38()
                {
                    SystemByte = s2f37.SystemByte,
                    SECSAgentName = scApp.EAPSecsAgentName,
                    ERACK = isValid ? SECSConst.ERACK_Accepted : SECSConst.ERACK_Denied_At_least_one_CEID_dose_not_exist
                };

                TrxSECS.ReturnCode rtnCode = ISECSControl.replySECS(bcfApp, s2f18);
                SCUtility.secsActionRecordMsg(scApp, false, s2f18);
                if (rtnCode != TrxSECS.ReturnCode.Normal)
                {
                    logger.Warn("Reply EQPT S2F18 Error:{0}", rtnCode);
                }
            }
            catch (Exception ex)
            {
                logger.Error("MESDefaultMapAction has Error[Line Name:{0}],[Error method:{1}],[Error Message:{2}", line.LINE_ID, "S2F17_Receive_Date_Time_Req", ex.ToString());
            }
        }

        protected virtual void S2F33_Define_Report(object sender, SECSEventArgs e)
        {
            try
            {
                S2F33 s2f33 = ((S2F33)e.secsHandler.Parse<S2F33>(e));
                SCUtility.secsActionRecordMsg(scApp, true, s2f33);
                if (!isProcessEAP(s2f33)) { return; }

                S2F34 s2f34 = null;
                s2f34 = new S2F34();
                s2f34.SystemByte = s2f33.SystemByte;
                s2f34.SECSAgentName = scApp.EAPSecsAgentName;
                s2f34.DRACK = "0";


                TrxSECS.ReturnCode rtnCode = ISECSControl.replySECS(bcfApp, s2f34);
                SCUtility.secsActionRecordMsg(scApp, false, s2f34);


                if (rtnCode != TrxSECS.ReturnCode.Normal)
                {
                    logger.Warn("Reply EQPT S2F18 Error:{0}", rtnCode);
                }

                scApp.CEIDBLL.DeleteRptInfoByBatch();

                if (s2f33.RPTITEMS != null && s2f33.RPTITEMS.Length > 0)
                    scApp.CEIDBLL.buildRptsFromMCS(s2f33.RPTITEMS);



                SECSConst.setDicRPTIDAndVID(scApp.CEIDBLL.loadDicRPTIDAndVID());

            }
            catch (Exception ex)
            {
                logger.Error("MESDefaultMapAction has Error[Line Name:{0}],[Error method:{1}],[Error Message:{2}", line.LINE_ID, "S2F17_Receive_Date_Time_Req", ex.ToString());
            }
        }

        public void s2f35Test()
        {
            S2F33_Define_Report(null, null);
        }
        protected virtual void S2F35_Link_Event_Report(object sender, SECSEventArgs e)
        {
            try
            {
                S2F35 s2f35 = ((S2F35)e.secsHandler.Parse<S2F35>(e));
                SCUtility.secsActionRecordMsg(scApp, true, s2f35);
                if (!isProcessEAP(s2f35)) { return; }


                S2F36 s2f36 = null;
                s2f36 = new S2F36();
                s2f36.SystemByte = s2f35.SystemByte;
                s2f36.SECSAgentName = scApp.EAPSecsAgentName;
                s2f36.LRACK = "0";

                TrxSECS.ReturnCode rtnCode = ISECSControl.replySECS(bcfApp, s2f36);
                SCUtility.secsActionRecordMsg(scApp, false, s2f36);
                if (rtnCode != TrxSECS.ReturnCode.Normal)
                {
                    logger.Warn("Reply EQPT S2F18 Error:{0}", rtnCode);
                }

                scApp.CEIDBLL.DeleteCEIDInfoByBatch();

                if (s2f35.RPTITEMS != null && s2f35.RPTITEMS.Length > 0)
                    scApp.CEIDBLL.buildCEIDsFromMCS(s2f35.RPTITEMS);

                SECSConst.setDicCEIDAndRPTID(scApp.CEIDBLL.loadDicCEIDAndRPTID());

            }
            catch (Exception ex)
            {
                logger.Error("MESDefaultMapAction has Error[Line Name:{0}],[Error method:{1}],[Error Message:{2}", line.LINE_ID, "S2F17_Receive_Date_Time_Req", ex.ToString());
            }
        }

        protected virtual void S5F3_Enable_Disable_Alarm(object sender, SECSEventArgs e)
        {
            try
            {
                bool isSuccess = true;
                S5F3 s5f3 = ((S5F3)e.secsHandler.Parse<S5F3>(e));
                SCUtility.secsActionRecordMsg(scApp, true, s5f3);
                if (!isProcessEAP(s5f3)) { return; }
                Boolean isEnable = BCFUtility.isMatche(s5f3.ALED, SECSConst.ALED_Enable);
                string alarm_code = s5f3.ALID;


                isSuccess = scApp.AlarmBLL.enableAlarmReport(alarm_code, isEnable);

                S5F4 s5f4 = null;
                s5f4 = new S5F4();
                s5f4.SystemByte = s5f3.SystemByte;
                s5f4.SECSAgentName = scApp.EAPSecsAgentName;
                s5f4.ACKC5 = isSuccess ? SECSConst.ACKC5_Accepted : SECSConst.ACKC5_Not_Accepted;

                TrxSECS.ReturnCode rtnCode = ISECSControl.replySECS(bcfApp, s5f4);
                SCUtility.secsActionRecordMsg(scApp, false, s5f4);
                if (rtnCode != TrxSECS.ReturnCode.Normal)
                {
                    logger.Warn("Reply EQPT S2F18 Error:{0}", rtnCode);
                }
            }
            catch (Exception ex)
            {
                logger.Error("MESDefaultMapAction has Error[Line Name:{0}],[Error method:{1}],[Error Message:{2}", line.LINE_ID, "S2F17_Receive_Date_Time_Req", ex.ToString());
            }
        }


        protected virtual void S2F41_Host_Command_Receive(object sender, SECSEventArgs e)
        {
            try
            {
                S2F41 s2f41 = ((S2F41)e.secsHandler.Parse<S2F41>(e));
                SCUtility.secsActionRecordMsg(scApp, true, s2f41);
                //if (!isProcessEAP(s2f37)) { return; }

                S2F42 s2f42 = null;
                s2f42 = new S2F42();
                s2f42.SystemByte = s2f41.SystemByte;
                s2f42.SECSAgentName = scApp.EAPSecsAgentName;

                string mcs_cmd_id = string.Empty;
                switch (s2f41.RCMD)
                {
                    case SECSConst.RCMD_Pause:
                        if (line.SCStats == SCAppConstants.LineSCState.SCState.Pausing)
                            s2f42.HCACK = SECSConst.HCACK_Confirm_Executed;
                        else if (line.SCStats != SCAppConstants.LineSCState.SCState.Auto)
                            s2f42.HCACK = SECSConst.HCACK_Rejected;
                        else
                            s2f42.HCACK = SECSConst.HCACK_Confirm;

                        s2f42.RPYITEMS = new S2F42.RPYITEM[0];
                        break;
                    case SECSConst.RCMD_Resume:
                        if (line.SCStats == SCAppConstants.LineSCState.SCState.Auto)
                            s2f42.HCACK = SECSConst.HCACK_Confirm_Executed;
                        else if (line.SCStats != SCAppConstants.LineSCState.SCState.Paused)
                            s2f42.HCACK = SECSConst.HCACK_Rejected;
                        else
                            s2f42.HCACK = SECSConst.HCACK_Confirm;

                        s2f42.RPYITEMS = new S2F42.RPYITEM[0];
                        break;
                    case SECSConst.RCMD_Cancel:
                        mcs_cmd_id = s2f41.REPITEMS[0].CPVAL;
                        s2f42.HCACK = SECSConst.HCACK_Confirm;
                        s2f42.RPYITEMS = new S2F42.RPYITEM[0];
                        break;
                    case SECSConst.RCMD_Abort:
                        mcs_cmd_id = s2f41.REPITEMS[0].CPVAL;
                        s2f42.HCACK = SECSConst.HCACK_Confirm;
                        s2f42.RPYITEMS = new S2F42.RPYITEM[0];
                        break;
                    case SECSConst.RCMD_Rename:
                        s2f42.HCACK = SECSConst.HCACK_Confirm;
                        s2f42.RPYITEMS = new S2F42.RPYITEM[0];
                        break;
                    case SECSConst.RCMD_StageDelete:
                        s2f42.HCACK = SECSConst.HCACK_Confirm;
                        s2f42.RPYITEMS = new S2F42.RPYITEM[0];
                        break;
                    case SECSConst.RCMD_PriorityUpdate:
                        bool isSuccess = true;
                        isSuccess = ProcessPriorityUpdate(e);
                        s2f42.HCACK = isSuccess ? SECSConst.HCACK_Confirm : SECSConst.HCACK_Not_Able_Execute;
                        s2f42.RPYITEMS = new S2F42.RPYITEM[0];
                        break;
                }
                TrxSECS.ReturnCode rtnCode = ISECSControl.replySECS(bcfApp, s2f42);
                SCUtility.secsActionRecordMsg(scApp, false, s2f42);
                if (rtnCode != TrxSECS.ReturnCode.Normal)
                {
                    logger.Warn("Reply EQPT S2F18 Error:{0}", rtnCode);
                }
                if (s2f42.HCACK == SECSConst.HCACK_Confirm)
                {
                    switch (s2f41.RCMD)
                    {
                        case SECSConst.RCMD_Pause:
                            ProcessPauseCmd();
                            break;
                        case SECSConst.RCMD_Resume:
                            ProcessResumeCmd();
                            break;
                        case SECSConst.RCMD_Cancel:
                            ProcessCancelCmd(mcs_cmd_id);
                            break;
                        case SECSConst.RCMD_Abort:
                            ProcessAbortCmd(mcs_cmd_id);
                            break;
                        case SECSConst.RCMD_Rename:
                            ProcessRenameCmd(s2f41);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("MESDefaultMapAction has Error[Line Name:{0}],[Error method:{1}],[Error Message:{2}", line.LINE_ID, "S2F17_Receive_Date_Time_Req", ex.ToString());
            }
        }

        private bool ProcessPriorityUpdate(SECSEventArgs e)
        {
            bool isSuccess = true;
            S2F41_PriorityUpdate s2f41_PriorityUpdate = ((S2F41_PriorityUpdate)e.secsHandler.Parse<S2F41_PriorityUpdate>(e));
            string cmd_id = s2f41_PriorityUpdate.REPITEMS.CommandID_CP.CPVAL_ASCII;
            string spriority_update = s2f41_PriorityUpdate.REPITEMS.PRIORITY_CP.CPVAL_U2;
            ACMD_MCS cmd_mcs = scApp.CMDBLL.getCMD_MCSByID(cmd_id);
            if (cmd_mcs == null || cmd_mcs.TRANSFERSTATE != E_TRAN_STATUS.Queue)
            {
                isSuccess = false;
            }

            int ipriority_update = 0;
            if (isSuccess && int.TryParse(spriority_update, out ipriority_update))
            {
                isSuccess = scApp.CMDBLL.updateCMD_MCS_Priority(cmd_mcs, ipriority_update);
            }
            else
            {
                isSuccess = false;
            }

            return isSuccess;
        }

        private void ProcessResumeCmd()
        {
            sendS6F11_AutoInitial();



            line.SCStats = SCAppConstants.LineSCState.SCState.Auto;  //TODO_Kevin 改成使用狀態機控制
            sendS6F11_AutoCompleted();
        }


        private void ProcessPauseCmd()
        {
            line.SCStats = SCAppConstants.LineSCState.SCState.Pausing;
            sendS6F11_PauseInitial();




            line.SCStats = SCAppConstants.LineSCState.SCState.Paused;
            sendS6F11_PauseCompleted();
        }

        private void ProcessCancelCmd(string mcs_cmd_id)
        {
            ACMD_MCS mcs_cmd = scApp.CMDBLL.getCMD_MCSByID(mcs_cmd_id);
            bool can_cancel_cmd = true;
            if (mcs_cmd == null)
            {
                can_cancel_cmd = false;
            }
            if (mcs_cmd.TRANSFERSTATE >= E_TRAN_STATUS.Transferring)
            {
                can_cancel_cmd = false;
            }

            AMCSREPORTQUEUE queueTemp = null;
            List<AMCSREPORTQUEUE> AMCSREPORTQUEUEs = new List<AMCSREPORTQUEUE>();
            if (can_cancel_cmd)
            {
                bool isSuccess = true;
                // isSuccess = scApp.VehicleService.doCancelOHxCCmd(mcs_cmd_id, vh);
                if (isSuccess)
                {
                    try
                    {
                        using (var tx = SCUtility.getTransactionScope())
                        {
                            using (DBConnection_EF con = DBConnection_EF.GetUContext())
                            {
                                scApp.CMDBLL.updateCMD_MCS_TranStatus2Complete(mcs_cmd_id, E_TRAN_STATUS.Canceled);
                                if (scApp.ReportBLL.tryCreatReportMCSMessage(SECSConst.CEID_Transfer_Cancel_Initiated, mcs_cmd, out queueTemp))
                                    AMCSREPORTQUEUEs.Add(queueTemp);
                                if (scApp.ReportBLL.tryCreatReportMCSMessage(SECSConst.CEID_Transfer_Cancel_Completed, mcs_cmd, out queueTemp))
                                    AMCSREPORTQUEUEs.Add(queueTemp);
                                tx.Complete();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (scApp.ReportBLL.tryCreatReportMCSMessage(SECSConst.CEID_Transfer_Cancel_Failed, new string[] { mcs_cmd_id }, out queueTemp))
                            AMCSREPORTQUEUEs.Add(queueTemp);
                        logger.Warn(ex, "Do ProcessCancelCmd fail.");
                    }
                }
                else
                {
                    if (scApp.ReportBLL.tryCreatReportMCSMessage(SECSConst.CEID_Transfer_Cancel_Failed, new string[] { mcs_cmd_id }, out queueTemp))
                        AMCSREPORTQUEUEs.Add(queueTemp);
                }
            }
            else
            {
                if (scApp.ReportBLL.tryCreatReportMCSMessage(SECSConst.CEID_Transfer_Cancel_Failed, new string[] { mcs_cmd_id }, out queueTemp))
                    AMCSREPORTQUEUEs.Add(queueTemp);
            }
            scApp.ReportBLL.sendMCSMessageAsyn(AMCSREPORTQUEUEs);

        }

        private void ProcessAbortCmd(string mcs_cmd_id)
        {
            ACMD_MCS mcs_cmd = scApp.CMDBLL.getCMD_MCSByID(mcs_cmd_id);
            bool can_cancel_cmd = true;
            if (mcs_cmd == null)
            {
                can_cancel_cmd = false;
            }

            AVEHICLE vh = scApp.VehicleBLL.getVehicleByExcuteMCS_CMD_ID(mcs_cmd_id);
            if (can_cancel_cmd)
            {
                //   can_cancel_cmd = false;

                //TODO 之後要檢查是否可以進行Cancel
            }

            AMCSREPORTQUEUE queueTemp = null;
            List<AMCSREPORTQUEUE> AMCSREPORTQUEUEs = new List<AMCSREPORTQUEUE>();
            if (can_cancel_cmd)
            {
                bool isSuccess = true;
                isSuccess = scApp.VehicleService.doAbortOHxCCmd(mcs_cmd_id, vh);
                if (isSuccess)
                {
                    if (scApp.ReportBLL.tryCreatReportMCSMessage(SECSConst.CEID_Transfer_Abort_Initiated, new string[] { mcs_cmd_id }, out queueTemp))
                        AMCSREPORTQUEUEs.Add(queueTemp);
                }
                else
                {
                    if (scApp.ReportBLL.tryCreatReportMCSMessage(SECSConst.CEID_Transfer_Abort_Failed, new string[] { mcs_cmd_id }, out queueTemp))
                        AMCSREPORTQUEUEs.Add(queueTemp);
                }
            }
            else
            {
                if (scApp.ReportBLL.tryCreatReportMCSMessage(SECSConst.CEID_Transfer_Abort_Failed, new string[] { mcs_cmd_id }, out queueTemp))
                    AMCSREPORTQUEUEs.Add(queueTemp);
            }
            scApp.ReportBLL.sendMCSMessageAsyn(AMCSREPORTQUEUEs);
        }

        private void ProcessRenameCmd(S2F41 s2f41)
        {
            bool isSuccess = true;
            string carrier_id = string.Empty;
            string new_carrier_id = string.Empty;
            string carrier_loc = string.Empty;

            carrier_id = s2f41.REPITEMS[0].CPVAL;
            new_carrier_id = s2f41.REPITEMS[1].CPVAL;
            carrier_loc = s2f41.REPITEMS[2].CPVAL;


            AVEHICLE carried_vh = scApp.VehicleBLL.getVehicleByCarrierID(carrier_id);

            //isSuccess = scApp.VehicleService.CarrierIDRenameRequset(carried_vh.VEHICLE_ID, carrier_id);

            if (isSuccess)
            {
                scApp.VehicleBLL.updataVehicleCSTID(carried_vh.VEHICLE_ID, new_carrier_id);

                AMCSREPORTQUEUE queueTemp = null;
                List<AMCSREPORTQUEUE> AMCSREPORTQUEUEs = new List<AMCSREPORTQUEUE>();
                if (scApp.ReportBLL.
                    tryCreatReportMCSMessage_CarrierInstall_And_Removed(SECSConst.CEID_Carrier_Removed, new string[] { carrier_id, carrier_loc }, out queueTemp))
                    AMCSREPORTQUEUEs.Add(queueTemp);
                if (scApp.ReportBLL.
                    tryCreatReportMCSMessage_CarrierInstall_And_Removed(SECSConst.CEID_Carrier_Installed, new string[] { new_carrier_id, carrier_loc }, out queueTemp))
                    AMCSREPORTQUEUEs.Add(queueTemp);
                scApp.ReportBLL.sendMCSMessageAsyn(AMCSREPORTQUEUEs);
            }
        }

        public void removeCmdTest(string cst_id, string cst_loc)
        {
            AMCSREPORTQUEUE queueTemp = null;
            List<AMCSREPORTQUEUE> AMCSREPORTQUEUEs = new List<AMCSREPORTQUEUE>();
            if (scApp.ReportBLL.
                tryCreatReportMCSMessage_CarrierInstall_And_Removed(SECSConst.CEID_Carrier_Removed, new string[] { cst_id, cst_loc }, out queueTemp))
                AMCSREPORTQUEUEs.Add(queueTemp);
            scApp.ReportBLL.sendMCSMessageAsyn(AMCSREPORTQUEUEs);
        }

        //public virtual void S2F49_Enhanced_Remote_Command_Extension(object sender, SECSEventArgs e)
        //{
        //    try
        //    {
        //        if (scApp.getEQObjCacheManager().getLine().ServerPreStop)
        //            return;
        //        string errorMsg = string.Empty;
        //        S2F49_TRANSFER s2f49 = ((S2F49_TRANSFER)e.secsHandler.Parse<S2F49_TRANSFER>(e));
        //        SCUtility.secsActionRecordMsg(scApp, true, s2f49);
        //        SCUtility.RecodeReportInfo(s2f49);



        //        //if (!isProcessEAP(s2f49)) { return; }

        //        S2F50 s2f50 = new S2F50();
        //        s2f50.SystemByte = s2f49.SystemByte;
        //        s2f50.SECSAgentName = scApp.EAPSecsAgentName;
        //        s2f50.HCACK = SECSConst.HCACK_Confirm;

        //        string cmdID = s2f49.REPITEMS.COMMINFO.COMMAINFO.COMMANDIDINFO.CommandID;

        //        string rtnStr = "";
        //        //檢查CST Size及Glass Data


        //        //string cmdID = s2f49.REPITEMS.COMMINFO.COMMAINFO.COMMANDIDINFO.CommandID;
        //        string priority = s2f49.REPITEMS.COMMINFO.COMMAINFO.REPLACE.CommandID;
        //        string cstID = s2f49.REPITEMS.TRANINFO.CARRINFO.CARRIERIDINFO.CarrierID;
        //        string source = s2f49.REPITEMS.TRANINFO.CARRINFO.SOUINFO.Source;
        //        bool sourceNotPort = scApp.MapBLL.getPortByPortID(source) == null;
        //        if (sourceNotPort)
        //        {
        //            source = string.Empty;
        //        }

        //        string dest = s2f49.REPITEMS.TRANINFO.CARRINFO.DESTINFO.Dest;

        //        //檢查搬送命令

        //        s2f50.HCACK = scApp.CMDBLL.doCheckMCSCommand(cmdID, priority, cstID, source, dest, out rtnStr);
        //        //if (s2f50.HCACK == SECSConst.HCACK_Confirm)
        //        //{
        //        using (TransactionScope tx = SCUtility.getTransactionScope())
        //        {
        //            using (DBConnection_EF con = DBConnection_EF.GetUContext())
        //            {
        //                bool isCreatScuess = true;
        //                isCreatScuess &= scApp.CMDBLL.doCreatMCSCommand(cmdID, priority, cstID, source, dest, s2f50.HCACK);
        //                if (s2f50.HCACK == SECSConst.HCACK_Confirm)
        //                    isCreatScuess &= scApp.SysExcuteQualityBLL.creatSysExcuteQuality(cmdID, source, dest);
        //                if (isCreatScuess)
        //                {
        //                    TrxSECS.ReturnCode rtnCode = ISECSControl.replySECS(bcfApp, s2f50);
        //                    SCUtility.secsActionRecordMsg(scApp, false, s2f50);
        //                    if (rtnCode != TrxSECS.ReturnCode.Normal)
        //                    {
        //                        logger_MapActionLog.Warn("Reply EQPT S2F50) Error:{0}", rtnCode);
        //                        isCreatScuess = false;
        //                    }
        //                    SCUtility.RecodeReportInfo(s2f50, cmdID, rtnCode.ToString());
        //                }
        //                if (isCreatScuess)
        //                {
        //                    tx.Complete();
        //                }
        //                else
        //                {
        //                    return;
        //                }
        //            }
        //        }
        //        //}
        //        if (s2f50.HCACK == SECSConst.HCACK_Confirm)
        //            scApp.CMDBLL.checkMCS_TransferCommand();
        //        else
        //        {
        //            BCFApplication.onWarningMsg(rtnStr);
        //        }

        //    }
        //    catch (Exception ex)
        //    {
        //        logger.Error("MESDefaultMapAction has Error[Line Name:{0}],[Error method:{1}],[Error Message:{2}", line.LINE_ID, "S2F49_Receive_Remote_Command", ex);
        //    }
        //}

        public virtual void S2F49_Enhanced_Remote_Command_Extension(object sender, SECSEventArgs e)
        {
            try
            {
                if (scApp.getEQObjCacheManager().getLine().ServerPreStop)
                    return;
                string errorMsg = string.Empty;
                S2F49 s2f49 = ((S2F49)e.secsHandler.Parse<S2F49>(e));

                switch (s2f49.RCMD)
                {
                    case "TRANSFER":
                        S2F49_TRANSFER s2f49_transfer = ((S2F49_TRANSFER)e.secsHandler.Parse<S2F49_TRANSFER>(e));
                        SCUtility.secsActionRecordMsg(scApp, true, s2f49_transfer);
                        SCUtility.RecodeReportInfo(s2f49_transfer);
                        //if (!isProcessEAP(s2f49)) { return; }

                        S2F50 s2f50 = new S2F50();
                        s2f50.SystemByte = s2f49_transfer.SystemByte;
                        s2f50.SECSAgentName = scApp.EAPSecsAgentName;
                        s2f50.HCACK = SECSConst.HCACK_Confirm;

                        string cmdID = s2f49_transfer.REPITEMS.COMMINFO.COMMAINFO.COMMANDIDINFO.CommandID;

                        string rtnStr = "";
                        //檢查CST Size及Glass Data


                        //string cmdID = s2f49.REPITEMS.COMMINFO.COMMAINFO.COMMANDIDINFO.CommandID;
                        string priority = s2f49_transfer.REPITEMS.COMMINFO.COMMAINFO.REPLACE.CommandID;
                        string cstID = s2f49_transfer.REPITEMS.TRANINFO.CARRINFO.CARRIERIDINFO.CarrierID;
                        string source = s2f49_transfer.REPITEMS.TRANINFO.CARRINFO.SOUINFO.Source;
                        bool sourceNotPort = scApp.MapBLL.getPortByPortID(source) == null;
                        if (sourceNotPort)
                        {
                            source = string.Empty;
                        }

                        string dest = s2f49_transfer.REPITEMS.TRANINFO.CARRINFO.DESTINFO.Dest;

                        //檢查搬送命令

                        s2f50.HCACK = scApp.CMDBLL.doCheckMCSCommand(cmdID, priority, cstID, source, dest, out rtnStr);
                        //if (s2f50.HCACK == SECSConst.HCACK_Confirm)
                        //{
                        using (TransactionScope tx = SCUtility.getTransactionScope())
                        {
                            using (DBConnection_EF con = DBConnection_EF.GetUContext())
                            {
                                bool isCreatScuess = true;
                                isCreatScuess &= scApp.CMDBLL.doCreatMCSCommand(cmdID, priority, cstID, source, dest, s2f50.HCACK);
                                if (s2f50.HCACK == SECSConst.HCACK_Confirm)
                                    isCreatScuess &= scApp.SysExcuteQualityBLL.creatSysExcuteQuality(cmdID, source, dest);
                                if (isCreatScuess)
                                {
                                    TrxSECS.ReturnCode rtnCode = ISECSControl.replySECS(bcfApp, s2f50);
                                    SCUtility.secsActionRecordMsg(scApp, false, s2f50);
                                    if (rtnCode != TrxSECS.ReturnCode.Normal)
                                    {
                                        logger_MapActionLog.Warn("Reply EQPT S2F50) Error:{0}", rtnCode);
                                        isCreatScuess = false;
                                    }
                                    SCUtility.RecodeReportInfo(s2f50, cmdID, rtnCode.ToString());
                                }
                                if (isCreatScuess)
                                {
                                    tx.Complete();
                                }
                                else
                                {
                                    return;
                                }
                            }
                        }
                        //}
                        if (s2f50.HCACK == SECSConst.HCACK_Confirm)
                            scApp.CMDBLL.checkMCS_TransferCommand();
                        else
                        {
                            BCFApplication.onWarningMsg(rtnStr);
                        }
                        break;
                    case "STAGE":
                        S2F49_STAGE s2f49_stage = ((S2F49_STAGE)e.secsHandler.Parse<S2F49_STAGE>(e));

                        S2F50 s2f50_stage = new S2F50();
                        s2f50_stage.SystemByte = s2f49_stage.SystemByte;
                        s2f50_stage.SECSAgentName = scApp.EAPSecsAgentName;
                        s2f50_stage.HCACK = SECSConst.HCACK_Confirm;

                        string source_port_id = s2f49_stage.REPITEMS.TRANSFERINFO.CPVALUE.SOURCEPORT_CP.CPVAL_ASCII;
                        TrxSECS.ReturnCode rtnCode_stage = ISECSControl.replySECS(bcfApp, s2f50_stage);
                        SCUtility.secsActionRecordMsg(scApp, false, s2f50_stage);

                        //TODO Stage
                        //將收下來的Stage命令先放到Redis上
                        //等待Timer發現後會將此命令取下來並下命令給車子去執行
                        //(此處將再考慮是要透過Timer或是開Thread來監控這件事)

                        var port = scApp.MapBLL.getPortByPortID(source_port_id);
                        AVEHICLE vh_test = scApp.VehicleBLL.findBestSuitableVhStepByStepFromAdr(port.ADR_ID, port.LD_VH_TYPE);
                        scApp.VehicleBLL.callVehicleToMove(vh_test, port.ADR_ID);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.Error("MESDefaultMapAction has Error[Line Name:{0}],[Error method:{1}],[Error Message:{2}", line.LINE_ID, "S2F49_Receive_Remote_Command", ex);
            }
        }
        #endregion Host Receive

        #region Host Send



        public Boolean sendS1F13_Establish_Comm()
        {
            try
            {
                //if (!line.S1F13Active)
                //    return true;

                S1F13 s1f13 = new S1F13();
                s1f13.SECSAgentName = scApp.EAPSecsAgentName;
                s1f13.MDLN = bcfApp.BC_ID;
                s1f13.SOFTREV = SCApplication.getMessageString("SYSTEM_VERSION");

                S1F14 s1f14 = null;
                string rtnMsg = string.Empty;
                SXFY abortSecs = null;
                SCUtility.secsActionRecordMsg(scApp, false, s1f13);

                TrxSECS.ReturnCode rtnCode = ISECSControl.sendRecv<S1F14>(bcfApp, s1f13, out s1f14, out abortSecs, out rtnMsg, null);
                SCUtility.actionRecordMsg(scApp, s1f13.StreamFunction, line.Real_ID, "Establish Communication.", rtnCode.ToString());

                if (rtnCode == TrxSECS.ReturnCode.Normal)
                {
                    SCUtility.secsActionRecordMsg(scApp, true, s1f14);
                    line.EstablishComm = true;
                    return true;
                }
                else
                {
                    line.EstablishComm = false;
                    logger.Warn("Send Establish Communication[S1F13] Error!");
                }
            }
            catch (Exception ex)
            {
                logger.Error("MESDefaultMapAction has Error[Line Name:{0}],[Error method:{1}],[Error Message:{2}", line.LINE_ID, " sendS1F13_Establish_Comm", ex.ToString());
            }
            return false;
        }


        public Boolean sendS5F1_Alarm_Report_Send(string vehicle_id, S5F1 s5f1)
        {
            try
            {

                S5F2 s5f2 = null;
                SXFY abortSecs = null;
                String rtnMsg = string.Empty;
                if (isSendEAP(s5f1))
                {
                    TrxSECS.ReturnCode rtnCode = ISECSControl.sendRecv<S5F2>(bcfApp, s5f1, out s5f2,
                        out abortSecs, out rtnMsg, null);
                    LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(MCSDefaultMapAction), Device: DEVICE_NAME_MCS,
                       Data: s5f1,
                       VehicleID: vehicle_id);
                    LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(MCSDefaultMapAction), Device: DEVICE_NAME_MCS,
                       Data: s5f2,
                       VehicleID: vehicle_id);
                    SCUtility.actionRecordMsg(scApp, s5f1.StreamFunction, line.Real_ID,
                        "Send Alarm Report.", rtnCode.ToString());
                    if (rtnCode != TrxSECS.ReturnCode.Normal)
                    {
                        logger.Warn("Send Alarm Report[S5F1] Error![rtnCode={0}]", rtnCode);
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception:");
                return false;
            }

        }

        private void sendS6F11_AutoInitial()
        {
            S6F11 s6f11_auto_initial = new S6F11()
            {
                CEID = SECSConst.CEID_TSC_Auto_Initiated,
                SECSAgentName = scApp.EAPSecsAgentName
            };
            s6f11_auto_initial.INFO.ITEM = new S6F11.RPTINFO.RPTITEM[0];
            sendS6F11_Message("", "", s6f11_auto_initial);
        }
        private void sendS6F11_AutoCompleted()
        {
            S6F11 s6f11_auto_cmp = new S6F11()
            {
                CEID = SECSConst.CEID_TSC_Auto_Completed,
                SECSAgentName = scApp.EAPSecsAgentName
            };
            s6f11_auto_cmp.INFO.ITEM = new S6F11.RPTINFO.RPTITEM[0];
            sendS6F11_Message("", "", s6f11_auto_cmp);
        }
        private void sendS6F11_PauseCompleted()
        {
            S6F11 s6f11_cmp = new S6F11()
            {
                CEID = SECSConst.CEID_TSC_Pause_Completed,
                SECSAgentName = scApp.EAPSecsAgentName
            };
            s6f11_cmp.INFO.ITEM = new S6F11.RPTINFO.RPTITEM[0];
            sendS6F11_Message("", "", s6f11_cmp);
        }
        private void sendS6F11_PauseInitial()
        {
            S6F11 s6f11_initial = new S6F11()
            {
                CEID = SECSConst.CEID_TSC_Pause_Initiated,
                SECSAgentName = scApp.EAPSecsAgentName
            };
            s6f11_initial.INFO.ITEM = new S6F11.RPTINFO.RPTITEM[0];
            sendS6F11_Message("", "", s6f11_initial);
        }


        public bool sendS6F11_Message(string vh_id, string mcs_cmd_id, S6F11 s6f11)
        {
            try
            {

                LogHelper.setCallContextKey_ServiceID(CALL_CONTEXT_KEY_WORD_SERVICE_ID_MCS);

                S6F12 s6f12 = null;
                SXFY abortSecs = null;
                String rtnMsg = string.Empty;

                if (!isSendEAP(s6f11))
                    return false;


                SCUtility.RecodeReportInfo(vh_id, mcs_cmd_id, s6f11, s6f11.CEID);
                SCUtility.secsActionRecordMsg(scApp, false, s6f11);
                TrxSECS.ReturnCode rtnCode = ISECSControl.sendRecv<S6F12>(bcfApp, s6f11, out s6f12,
                    out abortSecs, out rtnMsg, null);
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(MCSDefaultMapAction), Device: DEVICE_NAME_MCS,
                   Data: s6f11,
                   VehicleID: vh_id);
                SCUtility.secsActionRecordMsg(scApp, false, s6f12);
                SCUtility.actionRecordMsg(scApp, s6f11.StreamFunction, line.Real_ID,
                            "sendS6F11_common.", rtnCode.ToString());
                SCUtility.RecodeReportInfo(vh_id, mcs_cmd_id, s6f12, s6f11.CEID, rtnCode.ToString());
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(MCSDefaultMapAction), Device: DEVICE_NAME_MCS,
                   Data: s6f12,
                   VehicleID: vh_id);

                if (rtnCode != TrxSECS.ReturnCode.Normal)
                {
                    logger_MapActionLog.Warn("Send Transfer Initiated[S6F11] Error![rtnCode={0}]", rtnCode);
                    return false;
                }
                //#region 在進行MCS測試時，特別發給STK的訊號，方便測試
                //if (s6f11.CEID == SECSConst.CEID_Transfer_Completed)
                //{
                //    foreach (var item in s6f11.INFO.ITEM)
                //    {
                //        foreach (var vid in item.VIDITEM)
                //        {
                //            if (vid is S6F11.RPTINFO.RPTITEM.VIDITEM_84)
                //            {
                //                var vid_84 = vid as S6F11.RPTINFO.RPTITEM.VIDITEM_84;
                //                string dest_port = vid_84.DESTINATION_PORT.Trim();
                //                string cst_id = vid_84.CARRIER_ID.Trim();
                //                string ToDevice = dest_port.Substring(8);

                //                scApp.webClientManager.postInfo2Stock($"{ToDevice}.mirle.com.tw", dest_port, cst_id, "waitin");
                //                logger.Trace($"Send waitin To STK,ToDevice:{ToDevice},Dest Port:{dest_port},CST ID:{cst_id}");
                //                return true;
                //            }
                //        }
                //    }
                //}

                //if (s6f11.CEID == SECSConst.CEID_Vehicle_Acquire_Completed)
                //{
                //    string source_port = string.Empty;
                //    string cst_id = string.Empty;

                //    foreach (var item in s6f11.INFO.ITEM)
                //    {
                //        foreach (var vid in item.VIDITEM)
                //        {
                //            if (vid is S6F11.RPTINFO.RPTITEM.VIDITEM_115)
                //            {
                //                var vid_115 = vid as S6F11.RPTINFO.RPTITEM.VIDITEM_115;
                //                source_port = vid_115.PORT_ID.Trim();

                //            }
                //            if (vid is S6F11.RPTINFO.RPTITEM.VIDITEM_54)
                //            {
                //                var vid_115 = vid as S6F11.RPTINFO.RPTITEM.VIDITEM_54;
                //                cst_id = vid_115.CARRIER_ID.Trim();
                //            }
                //        }
                //        if (!SCUtility.isEmpty(source_port) && !SCUtility.isEmpty(cst_id))
                //        {
                //            string ToDevice = source_port.Substring(8);
                //            scApp.webClientManager.postInfo2Stock($"{ToDevice}.mirle.com.tw", source_port, cst_id, "remove");
                //            logger.Trace($"Send Remove To STK,ToDevice:{ToDevice},Source Port:{source_port},CST ID:{cst_id}");
                //            return true;
                //        }
                //    }
                //}
                //#endregion 在進行MCS測試時，特別發給STK的訊號，方便測試

                return true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception:");
                return false;
            }
        }


        #endregion Host Send
        /// <summary>
        /// 發送Are You There給MES(S1F1)
        /// </summary>
        /// <returns>Boolean.</returns>
        public Boolean sendS1F1_AreYouThere()
        {
            try
            {
                S1F1 s1f1 = new S1F1()
                {
                    SECSAgentName = scApp.EAPSecsAgentName
                };
                S1F2 s1f2 = null;
                string rtnMsg = string.Empty;
                SXFY abortSecs = null;
                //SCUtility.secsActionRecordMsg(scApp, false, s1f1);
                TrxSECS.ReturnCode rtnCode = ISECSControl.sendRecv<S1F2>(bcfApp, s1f1, out s1f2, out abortSecs, out rtnMsg, null);
                SCUtility.actionRecordMsg(scApp, s1f1.StreamFunction, line.Real_ID,
                                "Send Are You There To MES.", rtnCode.ToString());
                if (rtnCode == TrxSECS.ReturnCode.Normal)
                {
                    //SCUtility.secsActionRecordMsg(scApp, false, s1f2);
                    return true;
                }
                else if (rtnCode == TrxSECS.ReturnCode.Abort)
                {
                    SCUtility.secsActionRecordMsg(scApp, false, abortSecs);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exection:");
            }
            return false;
        }


        public Boolean isProcessEAP(SXFY sxfy)
        {
            Boolean isProcess = false;
            string streamFunction = sxfy.StreamFunction;
            if (line.Host_Control_State == SCAppConstants.LineHostControlState.HostControlState.EQ_Off_line)
            {
                if (sxfy is S1F17)
                {
                    isProcess = true;
                }
                else if (sxfy is S2F41)
                {
                    string rcmd = (sxfy as S2F41).RCMD;
                }
                else
                {
                    isProcess = false;
                }
            }
            else
            {
                isProcess = true;
            }
            if (!isProcess)
            {
                S1F0 sxf0 = new S1F0()
                {
                    SECSAgentName = scApp.EAPSecsAgentName,
                    StreamFunction = sxfy.getAbortFunctionName(),
                    SystemByte = sxfy.SystemByte
                };
                SCUtility.secsActionRecordMsg(scApp, false, sxf0);
                TrxSECS.ReturnCode rtnCode = ISECSControl.replySECS(bcfApp, sxf0);
                SCUtility.actionRecordMsg(scApp, sxf0.StreamFunction, line.Real_ID,
                            "Reply Abort To MES.", rtnCode.ToString());
            }
            return isProcess;
        }
        public Boolean isSendEAP(SXFY sxfy)
        {
            Boolean result = false;
            try
            {
                return isOnlineWithMcs;

                //result = (line.HOST_MODE == SCAppConstants.LineHostMode.OnLineRemote || line.HOST_MODE == SCAppConstants.LineHostMode.OnLineLocal) ?
                //true : false;
            }
            catch (Exception ex)
            {
                logger.Error("MESDefaultMapAction has Error[Line Name:{0}],[Error method:{1}],[Error Message:{2}",
                    line.LINE_ID, "isSendEAP", ex.ToString());
            }
            return result;
        }
        protected void ReciveS1F1(object sender, SECSEventArgs e)
        {

        }
        #region SECS Link Status

        public void secsConnected()
        {
            secsConnected(null, null);
        }
        /// <summary>
        /// Secses the connected.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="SECSEventArgs"/> instance containing the event data.</param>
        protected void secsConnected(object sender, SECSEventArgs e)
        {
            if (line.Secs_Link_Stat == SCAppConstants.LinkStatus.LinkOK) return;
            Dictionary<string, CommuncationInfo> dicCommunactionInfo =
                scApp.getEQObjCacheManager().CommonInfo.dicCommunactionInfo;
            if (dicCommunactionInfo.ContainsKey("MCS"))
            {
                dicCommunactionInfo["MCS"].IsConnectinoSuccess = true;
            }
            line.Secs_Link_Stat = SCAppConstants.LinkStatus.LinkOK;
            isOnlineWithMcs = true;
            line.connInfoUpdate_Connection();
            SCUtility.RecodeConnectionInfo
                ("MCS",
                SCAppConstants.RecodeConnectionInfo_Type.Connection.ToString(),
                line.StopWatch_mcsDisconnectionTime.Elapsed.TotalSeconds);

            ITimerAction timer = scApp.getBCFApplication().getTimerAction("SECSHeartBeat");
            if (timer != null && !timer.IsStarted)
            {
                timer.start();
            }
            //sendS1F13_Establish_Comm();
        }

        /// <summary>
        /// The host status timer
        /// </summary>
        HostStatusTimerAction hostStatusTimer = new HostStatusTimerAction("HostStatusTimerAction",
            SystemParameter.ControlStateKeepTimeSec * 1000);
        public void secsDisconnected()
        {
            secsDisconnected(null, null);
        }
        /// <summary>
        /// Secses the disconnected.
        /// </summary>
        /// <param name="sender">The sender.</param>
        /// <param name="e">The <see cref="SECSEventArgs"/> instance containing the event data.</param>
        protected void secsDisconnected(object sender, SECSEventArgs e)
        {
            if (line.Secs_Link_Stat == SCAppConstants.LinkStatus.LinkFail) return;
            //not implement
            Dictionary<string, CommuncationInfo> dicCommunactionInfo =
                scApp.getEQObjCacheManager().CommonInfo.dicCommunactionInfo;
            if (dicCommunactionInfo.ContainsKey("MCS"))
            {
                dicCommunactionInfo["MCS"].IsConnectinoSuccess = false;
            }
            isOnlineWithMcs = false;
            line.Secs_Link_Stat = SCAppConstants.LinkStatus.LinkFail;
            line.connInfoUpdate_Disconnection();

            SCUtility.RecodeConnectionInfo
                ("MCS",
                SCAppConstants.RecodeConnectionInfo_Type.Disconnection.ToString(),
                line.StopWatch_mcsConnectionTime.Elapsed.TotalSeconds);
        }


        /// <summary>
        /// Class HostStatusTimerAction.
        /// </summary>
        /// <seealso cref="com.mirle.ibg3k0.bcf.Data.TimerAction.ITimerAction" />
        public class HostStatusTimerAction : ITimerAction
        {
            /// <summary>
            /// The is over desconnected time
            /// </summary>
            public Boolean isOverDesconnectedTime = true;
            /// <summary>
            /// The logger
            /// </summary>
            private static Logger logger = LogManager.GetCurrentClassLogger();
            /// <summary>
            /// The line
            /// </summary>
            ALINE line = SCApplication.getInstance().getEQObjCacheManager().getLine();
            /// <summary>
            /// Initializes a new instance of the <see cref="HostStatusTimerAction"/> class.
            /// </summary>
            /// <param name="name">The name.</param>
            /// <param name="intervalMilliSec">The interval milli sec.</param>
            public HostStatusTimerAction(string name, long intervalMilliSec)
                : base(name, intervalMilliSec)
            {

            }

            /// <summary>
            /// Initializes the start.
            /// </summary>
            public override void initStart()
            {
                this.IntervalMilliSec = SystemParameter.ControlStateKeepTimeSec * 1000;
                procCnt = 0;
            }

            /// <summary>
            /// The proc count
            /// </summary>
            private int procCnt = 0;
            /// <summary>
            /// Timer Action的執行動作
            /// </summary>
            /// <param name="obj">The object.</param>
            public override void doProcess(object obj)
            {
                if (IntervalMilliSec > 0 && procCnt++ <= 0) { return; }
                logger.Info("Disconnected Over Time[{0}]", IntervalMilliSec);
                //逾時一律Offline
                line.Host_Control_State = SCAppConstants.LineHostControlState.HostControlState.EQ_Off_line;
                isOverDesconnectedTime = true;
                this.stop();
            }
        }
        #endregion SECS Link Status

        /// <summary>
        /// Detecteds the disable over time flag.
        /// </summary>
        private void detectedDisableOverTimeFlag()
        {
            //if (line.HOST_MODE != SCAppConstants.LineHostMode.OffLine)
            //{
            //    hostStatusTimer.isOverDesconnectedTime = false;
            //}
        }

        /// <summary>
        /// Does the initialize.
        /// </summary>
        public virtual void doInit()
        {
            string eapSecsAgentName = scApp.EAPSecsAgentName;

            ISECSControl.addSECSReceivedHandler(bcfApp, eapSecsAgentName, "S1F1", ReciveS1F1);
            ISECSControl.addSECSReceivedHandler(bcfApp, eapSecsAgentName, "S1F3", S1F3_Selected_Equipment_Status_Request);
            ISECSControl.addSECSReceivedHandler(bcfApp, eapSecsAgentName, "S1F13", s1f13_Establish_Communication_Request);
            ISECSControl.addSECSReceivedHandler(bcfApp, eapSecsAgentName, "S1F17", S1F17_Request_On_Line);

            ISECSControl.addSECSReceivedHandler(bcfApp, eapSecsAgentName, "S2F31", S2F31_Receive_Date_Time_Set_Req);
            ISECSControl.addSECSReceivedHandler(bcfApp, eapSecsAgentName, "S2F33", S2F33_Define_Report);
            ISECSControl.addSECSReceivedHandler(bcfApp, eapSecsAgentName, "S2F35", S2F35_Link_Event_Report);
            ISECSControl.addSECSReceivedHandler(bcfApp, eapSecsAgentName, "S2F37", S2F37_EnableDisable_Event_Report);
            ISECSControl.addSECSReceivedHandler(bcfApp, eapSecsAgentName, "S2F41", S2F41_Host_Command_Receive);
            ISECSControl.addSECSReceivedHandler(bcfApp, eapSecsAgentName, "S2F49", S2F49_Enhanced_Remote_Command_Extension);

            ISECSControl.addSECSReceivedHandler(bcfApp, eapSecsAgentName, "S5F3", S5F3_Enable_Disable_Alarm);

            ISECSControl.addSECSReceivedHandler(bcfApp, eapSecsAgentName, "S2F13", S2F13_Receive);


            ISECSControl.addSECSConnectedHandler(bcfApp, eapSecsAgentName, secsConnected);
            ISECSControl.addSECSDisconnectedHandler(bcfApp, eapSecsAgentName, secsDisconnected);


        }
    }
}
