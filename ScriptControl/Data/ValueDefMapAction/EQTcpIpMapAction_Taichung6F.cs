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
using Google.Protobuf;
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

namespace com.mirle.ibg3k0.sc.Data.ValueDefMapAction
{
    /// <summary>
    /// </summary>
    /// <seealso cref="com.mirle.ibg3k0.sc.Data.ValueDefMapAction.ValueDefMapActionBase" />
    public class EQTcpIpMapAction_Taichung6F : ValueDefMapActionBase
    {
        const string FORMAT_ADDRESS_ID_LENGTH = "D4";
        const string FORMAT_SECTION_ID_LENGTH = "D4";
        public const int PACKET_SECTION_COUNT = 23;
        string tcpipAgentName = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="EQType2SecsMapAction"/> class.
        /// </summary>
        public EQTcpIpMapAction_Taichung6F()
            : base()
        {

        }

        /// <summary>
        /// Gets the identity key.
        /// </summary>
        /// <returns>System.String.</returns>
        public override string getIdentityKey()
        {
            return this.GetType().Name;
        }
        // protected AVEHICLE eqpt = null;

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
                        VehicleStatusChangeFromPLC(null, null);
                        VehicleCSTInterfaceIndexChanged(null, null);

                        //先讓車子一開始都當作是"VehicleInstall"的狀態
                        //之後要從DB得知上次的狀態，是否為Remove
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



        //todo 需掛上實際資料
        public override bool doDataSysc()
        {
            bool isSyscCmp = false;
            //DateTime ohtDataVersion = new DateTime(2017, 03, 27, 10, 30, 00);
            //if (ohtDataVersion.CompareTo(eqpt.VhBasisDataVersionTime) != 0)
            ////{
            //if (sned_Str11() &&
            //    sned_Str13() &&
            //    sned_Str15() &&
            //    sned_Str17() &&
            //    sned_Str19() &&
            //    sned_Str21() &&
            //    sned_Str23())
            //{
            //    isSyscCmp = true;
            //}
            return isSyscCmp;
            //}
            //else
            //{
            //    isSyscCmp = true;
            //}

            //if (isSyscCmp)
            //{
            //    if (sned_Str1())
            //    {
            //        eqpt.VHStateMach.Fire(SCAppConstants.E_VH_EVENT.DataSyncComplete);
            //    }
            //    else
            //    {
            //        eqpt.VHStateMach.Fire(SCAppConstants.E_VH_EVENT.DataSyncFail);
            //    }
            //}
            //else
            //{
            //    eqpt.VHStateMach.Fire(SCAppConstants.E_VH_EVENT.DataSyncFail);
            //}
        }

        //todo 需掛上實際資料
        public override bool sned_Str1(ID_1_HOST_BASIC_INFO_VERSION_REP sned_gpp, out ID_101_HOST_BASIC_INFO_VERSION_RESPONSE receive_gpp)
        {
            string rtnMsg = string.Empty;
            STR_VHMSG_HOST_KISO_VERSION_REP strSend = GPP2STR_1(sned_gpp);
            STR_VHMSG_HOST_KISO_VERSION_RESP strReceive;
            TrxTcpIp.ReturnCode rtnCode = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, strSend, out strReceive, out rtnMsg);
            receive_gpp = STR2GPP_101(strReceive);

            //STR_VHMSG_HOST_KISO_VERSION_REP stSend;
            //STR_VHMSG_HOST_KISO_VERSION_RESP stRecv;
            //stSend = new STR_VHMSG_HOST_KISO_VERSION_REP();
            //stSend.PacketID = VHMSGIF.ID_HOST_KISO_VERSION_REPORT;
            //stSend.DataDateTime = new char[14] { '2', '0', '1', '7', '0', '3', '2', '7', '1', '0', '3', '0', '0', '0' };
            //DateTime crtTime = DateTime.Now;
            //stSend.LocalYear = (UInt16)crtTime.Year;
            //stSend.LocalMonth = (UInt16)crtTime.Month;
            //stSend.LocalDay = (UInt16)crtTime.Day;
            //stSend.LocalHour = (UInt16)crtTime.Hour;
            //stSend.LocalMinute = (UInt16)crtTime.Minute;
            //stSend.LocalSecond = (UInt16)crtTime.Second;

            return rtnCode == TrxTcpIp.ReturnCode.Normal;
        }

        //todo 需掛上實際資料
        public override bool sned_Str11(ID_11_BASIC_INFO_REP send_gpp, out ID_111_BASIC_INFO_RESPONSE receive_gpp)
        {
            string rtnMsg = string.Empty;
            STR_VHMSG_KISO_LIST_COUNT_REP strSend = GPP2STR_11(send_gpp);
            STR_VHMSG_KISO_LIST_COUNT_RESP strReceive;
            TrxTcpIp.ReturnCode rtnCode = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, strSend, out strReceive, out rtnMsg);
            receive_gpp = STR2GPP_111(strReceive);
            return rtnCode == TrxTcpIp.ReturnCode.Normal;

            //int travel_base_data_count = 1;
            //int section_data_count = 0;
            //int address_data_coune = 0;
            //int scale_base_data_count = 1;
            //int control_data_count = 1;
            //int guide_base_data_count = 1;
            //section_data_count = scApp.DataSyncBLL.getCount_ReleaseVSections();
            //address_data_coune = scApp.MapBLL.getCount_AddressCount();

            //string rtnMsg = string.Empty;
            //STR_VHMSG_KISO_LIST_COUNT_REP stSend;
            //STR_VHMSG_KISO_LIST_COUNT_RESP stRecv;
            //stSend = new STR_VHMSG_KISO_LIST_COUNT_REP();
            //stSend.PacketID = VHMSGIF.ID_KISO_LIST_COUNT_REPORT;
            //stSend.ListTable = convertvBits2UInt16(bit0: true, bit1: true, bit2: true, bit3: true, bit4: true);
            //stSend.ListCount1 = (UInt16)travel_base_data_count;
            //stSend.ListCount2 = (UInt16)section_data_count;
            //stSend.ListCount3 = (UInt16)address_data_coune;
            //stSend.ListCount4 = (UInt16)scale_base_data_count;
            //stSend.ListCount5 = (UInt16)control_data_count;
            //stSend.ListCount6 = (UInt16)guide_base_data_count;

            //com.mirle.iibg3k0.ttc.Common.TrxTcpIp.ReturnCode result = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, stSend, out stRecv, out rtnMsg);
            //return result == TrxTcpIp.ReturnCode.Normal;
        }

        //todo 需掛上實際資料
        public override bool sned_Str13(ID_13_TAVELLING_DATA_REP send_gpp, out ID_113_TAVELLING_DATA_RESPONSE receive_gpp)
        {
            string rtnMsg = string.Empty;
            STR_VHMSG_KISO_TRAVEL_REP strSend = GPP2STR_13(send_gpp);
            STR_VHMSG_KISO_TRAVEL_RESP strReceive;
            TrxTcpIp.ReturnCode rtnCode = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, strSend, out strReceive, out rtnMsg);
            receive_gpp = STR2GPP_113(strReceive);
            return rtnCode == TrxTcpIp.ReturnCode.Normal;
            //AVEHICLE_CONTROL_100 data = scApp.DataSyncBLL.getReleaseVehicleControlData_100(eqpt.VEHICLE_ID);
            //string rtnMsg = string.Empty;
            //STR_VHMSG_KISO_TRAVEL_REP stSend;
            //STR_VHMSG_KISO_TRAVEL_RESP stRecv;
            //stSend = new STR_VHMSG_KISO_TRAVEL_REP();
            //stSend.PacketID = VHMSGIF.ID_KISO_TRAVEL_REPORT;
            //stSend.Resolution = (UInt32)data.TRAVEL_RESOLUTION;
            //stSend.StartStopSpd = (UInt32)data.TRAVEL_START_STOP_SPEED;
            //stSend.MaxSpeed = (UInt32)data.TRAVEL_MAX_SPD;
            //stSend.AccelTime = (UInt32)data.TRAVEL_ACCEL_DECCEL_TIME;
            //stSend.SCurveRate = (UInt16)data.TRAVEL_S_CURVE_RATE;
            //stSend.OriginDir = (UInt16)data.TRAVEL_HOME_DIR;
            //stSend.OriginSpd = (UInt32)data.TRAVEL_HOME_SPD;
            //stSend.BeaemSpd = (UInt32)data.TRAVEL_KEEP_DIS_SPD;
            //stSend.ManualHSpd = (UInt32)data.TRAVEL_MANUAL_HIGH_SPD;
            //stSend.ManualLSpd = (UInt32)data.TRAVEL_MANUAL_LOW_SPD;
            //stSend.TeachingSpd = (UInt32)data.TRAVEL_TEACHING_SPD;
            //stSend.RotateDir = (UInt16)data.TRAVEL_TRAVEL_DIR;
            //stSend.EncoderPole = (UInt16)data.TRAVEL_ENCODER_POLARITY;
            //stSend.FLimit = (UInt16)data.TRAVEL_F_DIR_LIMIT;
            //stSend.RLimit = (UInt16)data.TRAVEL_R_DIR_LIMIT;
            //stSend.KeepDistFar = (UInt32)data.TRAVEL_OBS_DETECT_LONG;
            //stSend.KeepDistNear = (UInt32)data.TRAVEL_OBS_DETECT_SHORT;
            //com.mirle.iibg3k0.ttc.Common.TrxTcpIp.ReturnCode result = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, stSend, out stRecv, out rtnMsg);
            //return result == TrxTcpIp.ReturnCode.Normal;
        }

        //todo 需掛上實際資料
        public override bool sned_Str15(ID_15_SECTION_DATA_REP send_gpp, out ID_115_SECTION_DATA_RESPONSE receive_gpp)
        {
            string rtnMsg = string.Empty;
            List<STR_VHMSG_KISO_SECTION_REP> strSend = GPP2STR_15(send_gpp);
            STR_VHMSG_KISO_SECTION_RESP strReceive;
            TrxTcpIp.ReturnCode rtnCode = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, strSend, out rtnMsg);
            //receive_gpp = STR2GPP_115(strReceive);
            receive_gpp = null;
            return rtnCode == TrxTcpIp.ReturnCode.Normal;

            //List<VSECTION_100> vSecs = scApp.DataSyncBLL.loadReleaseVSections();

            //string rtnMsg = string.Empty;
            //STR_VHMSG_KISO_SECTION_REP stSend;
            //List<STR_VHMSG_KISO_SECTION_REP> lstStSend = new List<STR_VHMSG_KISO_SECTION_REP>();
            //foreach (VSECTION_100 vSec in vSecs)
            //{
            //    stSend = new STR_VHMSG_KISO_SECTION_REP();
            //    stSend.PacketID = VHMSGIF.ID_KISO_SECTION_REPORT;
            //    stSend.DriveDir = (UInt16)vSec.DIRC_DRIV;
            //    stSend.GuideDir = (UInt16)vSec.DIRC_GUID;
            //    stSend.AeraSecsor = (UInt16)(vSec.AREA_SECSOR.HasValue ? vSec.AREA_SECSOR.Value : 0);
            //    UInt16 iSec_id = 0;
            //    if (!UInt16.TryParse(vSec.SEC_ID, out iSec_id))
            //    {
            //        logger.Warn($"{nameof(vSec.SEC_ID)}:{vSec.SEC_ID}, parse to UInt16 fail.");
            //        return false;
            //    }
            //    UInt32 iFromAdr = 0;
            //    if (!UInt32.TryParse(vSec.FROM_ADR_ID, out iFromAdr))
            //    {
            //        logger.Warn($"{nameof(vSec.FROM_ADR_ID)}:{vSec.FROM_ADR_ID}, parse to UInt16 fail.");
            //        return false;
            //    }
            //    UInt32 iToAdr = 0;
            //    if (!UInt32.TryParse(vSec.TO_ADR_ID, out iToAdr))
            //    {
            //        logger.Warn($"{nameof(vSec.TO_ADR_ID)}:{vSec.TO_ADR_ID}, parse to UInt16 fail.");
            //        return false;
            //    }
            //    stSend.SectionID = iSec_id;
            //    stSend.FromAddr = iFromAdr;
            //    stSend.ToAddr = iToAdr;
            //    stSend.ControlTable = convertvSec2ControlTable(vSec);
            //    stSend.Speed = (uint)vSec.SEC_SPD;
            //    stSend.Distance = (uint)vSec.SEC_DIS;
            //    stSend.ChangeAreaSensor1 = (UInt16)vSec.CHG_AREA_SECSOR_1;
            //    stSend.ChangeGuideDir1 = (UInt16)vSec.CDOG_1;

            //    string chang_sec_id_1 = scApp.MapBLL.getFirstSecIDBySegmentID(vSec.CHG_SEG_NUM_1);
            //    UInt16 chg_sec_num1 = 0;
            //    if (!UInt16.TryParse(chang_sec_id_1, out chg_sec_num1))
            //    {
            //        //logger.Warn($"{nameof(vSec.CHG_SEG_NUM_1)}:{vSec.CHG_SEG_NUM_1} of first sec id:{chang_sec_id_1}, parse to UInt16 fail.");
            //    }
            //    stSend.ChangeSegNum1 = chg_sec_num1;
            //    stSend.ChangeAreaSensor2 = (UInt16)vSec.CHG_AREA_SECSOR_2;
            //    stSend.ChangeGuideDir2 = (UInt16)vSec.CDOG_2;

            //    string chang_sec_id_2 = scApp.MapBLL.getFirstSecIDBySegmentID(vSec.CHG_SEG_NUM_2);
            //    UInt16 chg_seg_num2 = 0;
            //    if (!UInt16.TryParse(chang_sec_id_2, out chg_seg_num2))
            //    {
            //        //logger.Warn($"{nameof(vSec.CHG_SEG_NUM_2)}:{vSec.CHG_SEG_NUM_2} of first sec id:{chang_sec_id_2}, parse to UInt16 fail.");
            //    }
            //    stSend.ChangeSegNum2 = chg_seg_num2;
            //    UInt16 seg_num = 0;
            //    if (!UInt16.TryParse(vSec.SEG_NUM, out seg_num))
            //    {
            //        logger.Warn($"{nameof(vSec.SEG_NUM)}:{vSec.SEG_NUM}, parse to UInt16 fail.");
            //        return false;
            //    }

            //    stSend.AtSegment = seg_num;
            //    if (vSec == vSecs.Last())
            //    {
            //        stSend.MultiFlag = (UInt16)0;
            //    }
            //    else
            //    {
            //        stSend.MultiFlag = (UInt16)1;
            //    }

            //    lstStSend.Add(stSend);
            ////(lstStSend.Last() as VSECTION_100).MultiFlag = (UInt16)0;

            //com.mirle.iibg3k0.ttc.Common.TrxTcpIp.ReturnCode result = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, lstStSend, out rtnMsg);
            //return true;
        }

        private UInt16 convertvSec2ControlTable(VSECTION_100 vSec)
        {
            System.Collections.BitArray bitArray = new System.Collections.BitArray(16);
            bitArray[0] = SCUtility.int2Bool(vSec.PRE_ADD_REPR);
            bitArray[1] = vSec.BRANCH_FLAG;
            bitArray[2] = vSec.HID_CONTROL;
            bitArray[3] = false;
            bitArray[4] = vSec.CAN_GUIDE_CHG;
            bitArray[6] = false;
            bitArray[7] = false;
            bitArray[8] = vSec.IS_ADR_RPT;
            bitArray[9] = false;
            bitArray[10] = false;
            bitArray[11] = false;
            bitArray[12] = SCUtility.int2Bool(vSec.RANGE_SENSOR_F);
            bitArray[13] = SCUtility.int2Bool(vSec.OBS_SENSOR_F);
            bitArray[14] = SCUtility.int2Bool(vSec.OBS_SENSOR_R);
            bitArray[15] = SCUtility.int2Bool(vSec.OBS_SENSOR_L);
            return SCUtility.getUInt16FromBitArray(bitArray);
        }

        private static UInt16 convertvBits2UInt16(bool bit0 = false, bool bit1 = false, bool bit2 = false, bool bit3 = false,
                                           bool bit4 = false, bool bit5 = false, bool bit6 = false, bool bit7 = false,
                                           bool bit8 = false, bool bit9 = false, bool bit10 = false, bool bit11 = false,
                                           bool bit12 = false, bool bit13 = false, bool bit14 = false, bool bit15 = false)
        {
            System.Collections.BitArray bitArray = new System.Collections.BitArray(new bool[]
                                                        {  bit0,  bit1,  bit2,  bit3,
                                                           bit4,  bit5,  bit6,  bit7,
                                                           bit8,  bit9,  bit10,  bit11,
                                                           bit12,  bit13,  bit14,  bit15 });
            return SCUtility.getUInt16FromBitArray(bitArray);
        }

        public override bool sned_Str17(ID_17_ADDRESS_DATA_REP send_gpp, out ID_117_ADDRESS_DATA_RESPONSE receive_gpp)
        {

            string rtnMsg = string.Empty;
            List<STR_VHMSG_KISO_ADDRESS_REP> strSend = GPP2STR_17(send_gpp);
            STR_VHMSG_KISO_ADDRESS_RESP strReceive;
            TrxTcpIp.ReturnCode rtnCode = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, strSend, out rtnMsg);
            //receive_gpp = STR2GPP_117(strReceive);
            receive_gpp = null;
            return rtnCode == TrxTcpIp.ReturnCode.Normal;


            //List<AADDRESS_DATA> adrs = scApp.DataSyncBLL.loadReleaseADDRESS_DATAs(eqpt.VEHICLE_ID);
            //string rtnMsg = string.Empty;
            //STR_VHMSG_KISO_ADDRESS_REP stSend;
            //List<STR_VHMSG_KISO_ADDRESS_REP> lstStSend = new List<STR_VHMSG_KISO_ADDRESS_REP>();

            //foreach (AADDRESS_DATA adr in adrs)
            //{
            //    stSend = new STR_VHMSG_KISO_ADDRESS_REP();
            //    stSend.PacketID = VHMSGIF.ID_KISO_ADDRESS_REPORT;
            //    UInt32 iAdr_id = 0;
            //    if (!SCUtility.tryParseUInt32AndRecord(logger, nameof(adr.ADR_ID), adr.ADR_ID, out iAdr_id)) return false;
            //    stSend.Addr = iAdr_id;
            //    stSend.Resolution = (UInt32)adr.RESOLUTION;
            //    stSend.Loaction = (UInt32)adr.LOACTION;
            //    if (adr == adrs.Last())
            //        stSend.MultiFlag = 0;
            //    else
            //        stSend.MultiFlag = 1;
            //    lstStSend.Add(stSend);
            //}

            //com.mirle.iibg3k0.ttc.Common.TrxTcpIp.ReturnCode result = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, lstStSend, out rtnMsg);
            //return true;

        }

        public override bool sned_Str19(ID_19_SCALE_DATA_REP send_gpp, out ID_119_SCALE_DATA_RESPONSE receive_gpp)
        {

            string rtnMsg = string.Empty;
            STR_VHMSG_KISO_SCALE_REP strSend = GPP2STR_19(send_gpp);
            STR_VHMSG_KISO_SCALE_RESP strReceive;
            TrxTcpIp.ReturnCode rtnCode = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, strSend, out strReceive, out rtnMsg);
            receive_gpp = STR2GPP_119(strReceive);
            return rtnCode == TrxTcpIp.ReturnCode.Normal;

            //SCALE_BASE_DATA data = scApp.DataSyncBLL.getReleaseSCALE_BASE_DATA();
            //string rtnMsg = string.Empty;
            //STR_VHMSG_KISO_SCALE_REP stSend;
            //STR_VHMSG_KISO_SCALE_RESP stRecv;
            //stSend = new STR_VHMSG_KISO_SCALE_REP();
            //stSend.PacketID = VHMSGIF.ID_KISO_SCALE_REPORT;
            //stSend.Resolution = (UInt32)data.RESOLUTION;
            //stSend.InposArea = (UInt32)data.INPOSITION_AREA;
            //stSend.InposStability = (UInt32)data.INPOSITION_STABLE_TIME;
            //stSend.ScalePulse = (UInt32)data.TOTAL_SCALE_PULSE;
            //stSend.ScaleOffset = (UInt32)data.SCALE_OFFSET;
            //stSend.ScaleReset = (UInt32)data.SCALE_RESE_DIST;
            //stSend.ReadDir = (UInt16)data.READ_DIR;
            //com.mirle.iibg3k0.ttc.Common.TrxTcpIp.ReturnCode result = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, stSend, out stRecv, out rtnMsg);
            //return result == TrxTcpIp.ReturnCode.Normal;
        }

        public override bool sned_Str21(ID_21_CONTROL_DATA_REP send_gpp, out ID_121_CONTROL_DATA_RESPONSE receive_gpp)
        {

            string rtnMsg = string.Empty;
            STR_VHMSG_KISO_CONTROL_REP strSend = GPP2STR_21(send_gpp);
            STR_VHMSG_KISO_CONTROL_RESP strReceive;
            TrxTcpIp.ReturnCode rtnCode = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, strSend, out strReceive, out rtnMsg);
            receive_gpp = STR2GPP_121(strReceive);
            return rtnCode == TrxTcpIp.ReturnCode.Normal;

            //CONTROL_DATA data = scApp.DataSyncBLL.getReleaseCONTROL_DATA();
            //string rtnMsg = string.Empty;
            //STR_VHMSG_KISO_CONTROL_REP stSend;
            //STR_VHMSG_KISO_CONTROL_RESP stRecv;
            //stSend = new STR_VHMSG_KISO_CONTROL_REP();
            //stSend.PacketID = VHMSGIF.ID_KISO_CONTROL_REPORT;
            //stSend.TimeoutT1 = (UInt32)data.T1;
            //stSend.TimeoutT2 = (UInt32)data.T2;
            //stSend.TimeoutT3 = (UInt32)data.T3;
            //stSend.TimeoutT4 = (UInt32)data.T4;
            //stSend.TimeoutT5 = (UInt32)data.T5;
            //stSend.TimeoutT6 = (UInt32)data.T6;
            //stSend.TimeoutT7 = (UInt32)data.T7;
            //stSend.TimeoutT8 = (UInt32)data.T8;
            //stSend.TimeoutBlock = (UInt32)data.BLOCK_REQ_TIME_OUT;
            //com.mirle.iibg3k0.ttc.Common.TrxTcpIp.ReturnCode result = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, stSend, out stRecv, out rtnMsg);
            //return result == TrxTcpIp.ReturnCode.Normal;
        }

        public override bool sned_Str23(ID_23_GUIDE_DATA_REP send_gpp, out ID_123_GUIDE_DATA_RESPONSE receive_gpp)
        {
            string rtnMsg = string.Empty;
            STR_VHMSG_KISO_GUIDE_REP strSend = GPP2STR_23(send_gpp);
            STR_VHMSG_KISO_GUIDE_RESP strReceive;
            TrxTcpIp.ReturnCode rtnCode = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, strSend, out strReceive, out rtnMsg);
            receive_gpp = STR2GPP_123(strReceive);
            return rtnCode == TrxTcpIp.ReturnCode.Normal;

            //AVEHICLE_CONTROL_100 data = scApp.DataSyncBLL.getReleaseVehicleControlData_100(eqpt.VEHICLE_ID);
            //string rtnMsg = string.Empty;
            //STR_VHMSG_KISO_GUIDE_REP stSend;
            //STR_VHMSG_KISO_GUIDE_RESP stRecv;
            //stSend = new STR_VHMSG_KISO_GUIDE_REP();
            //stSend.PacketID = VHMSGIF.ID_KISO_GUIDE_REPORT;
            //stSend.StartStopSpd = (UInt32)data.GUIDE_START_STOP_SPEED;
            //stSend.MaxSpeed = (UInt32)data.GUIDE_MAX_SPD;
            //stSend.AccelTime = (UInt32)data.GUIDE_ACCEL_DECCEL_TIME;
            //stSend.SCurveRate = (UInt16)data.GUIDE_S_CURVE_RATE;
            //stSend.Resv1 = 0;
            //stSend.NormalSpd = (UInt32)data.GUIDE_RUN_SPD;
            //stSend.ManualHSpd = (UInt32)data.GUIDE_MANUAL_HIGH_SPD;
            //stSend.ManualLSpd = (UInt32)data.GUIDE_MANUAL_LOW_SPD;
            //stSend.LFLockPos = (UInt32)data.GUIDE_LF_LOCK_POSITION;
            //stSend.LBLockPos = (UInt32)data.GUIDE_LB_LOCK_POSITION;
            //stSend.RFLockPos = (UInt32)data.GUIDE_RF_LOCK_POSITION;
            //stSend.RBLockPos = (UInt32)data.GUIDE_RB_LOCK_POSITION;
            //stSend.ChangeStabilityTime = (UInt32)data.GUIDE_CHG_STABLE_TIME;
            //com.mirle.iibg3k0.ttc.Common.TrxTcpIp.ReturnCode result = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, stSend, out stRecv, out rtnMsg);
            //return result == TrxTcpIp.ReturnCode.Normal;
        }

        public override bool sned_Str61(ID_61_INDIVIDUAL_UPLOAD_REQ send_gpp, out ID_161_INDIVIDUAL_UPLOAD_RESPONSE receive_gpp)
        {
            string rtnMsg = string.Empty;
            STR_VHMSG_INDIVIDUAL_UPLOAD_REQ strSend = GPP2STR_61(send_gpp);
            STR_VHMSG_INDIVIDUAL_UPLOAD_REP strReceive;
            TrxTcpIp.ReturnCode rtnCode = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, strSend, out strReceive, out rtnMsg);
            receive_gpp = STR2GPP_161(strReceive);
            return rtnCode == TrxTcpIp.ReturnCode.Normal;
        }
        public override bool sned_Str63(ID_63_INDIVIDUAL_CHANGE_REQ send_gpp, out ID_163_INDIVIDUAL_CHANGE_RESPONSE receive_gpp)
        {
            string rtnMsg = string.Empty;
            STR_VHMSG_INDIVIDUAL_CHANGE_REQ strSend = GPP2STR_63(send_gpp);
            STR_VHMSG_INDIVIDUAL_CHANGE_RESP strReceive;
            TrxTcpIp.ReturnCode rtnCode = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, strSend, out strReceive, out rtnMsg);
            receive_gpp = STR2GPP_163(strReceive);
            return rtnCode == TrxTcpIp.ReturnCode.Normal;
        }
        public override bool sned_Str41(ID_41_MODE_CHANGE_REQ send_gpp, out ID_141_MODE_CHANGE_RESPONSE receive_gpp)
        {
            string rtnMsg = string.Empty;
            STR_VHMSG_MODE_CHANGE_REQ strSend = GPP2STR_41(send_gpp);
            STR_VHMSG_MODE_CHANGE_RESP strReceive;
            TrxTcpIp.ReturnCode rtnCode = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, strSend, out strReceive, out rtnMsg);
            receive_gpp = STR2GPP_141(strReceive);
            return rtnCode == TrxTcpIp.ReturnCode.Normal;
        }
        public override bool send_Str43(ID_43_STATUS_REQUEST send_gpp, out ID_143_STATUS_RESPONSE receive_gpp)
        {
            string rtnMsg = string.Empty;
            STR_VHMSG_STATUS_REQ strSend = GPP2STR_43(send_gpp);
            STR_VHMSG_STATUS_RESP strReceive;
            TrxTcpIp.ReturnCode rtnCode = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, strSend, out strReceive, out rtnMsg);
            receive_gpp = STR2GPP_143(strReceive);
            return rtnCode == TrxTcpIp.ReturnCode.Normal;
        }
        public override bool sned_Str45(ID_45_POWER_OPE_REQ send_gpp, out ID_145_POWER_OPE_RESPONSE receive_gpp)
        {
            string rtnMsg = string.Empty;
            STR_VHMSG_POWER_OPE_REQ strSend = GPP2STR_45(send_gpp);
            STR_VHMSG_POWER_OPE_RESP strReceive;
            TrxTcpIp.ReturnCode rtnCode = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, strSend, out strReceive, out rtnMsg);
            receive_gpp = STR2GPP_145(strReceive);
            return rtnCode == TrxTcpIp.ReturnCode.Normal;
        }
        public override bool sned_Str91(ID_91_ALARM_RESET_REQUEST send_gpp, out ID_191_ALARM_RESET_RESPONSE receive_gpp)
        {
            string rtnMsg = string.Empty;
            STR_VHMSG_ALARM_RESET_REQ strSend = GPP2STR_91(send_gpp);
            STR_VHMSG_ALARM_RESET_RESP strReceive;
            TrxTcpIp.ReturnCode rtnCode = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, strSend, out strReceive, out rtnMsg);
            receive_gpp = STR2GPP_191(strReceive);
            return rtnCode == TrxTcpIp.ReturnCode.Normal;
        }


        public override bool send_Str31(ID_31_TRANS_REQUEST send_gpp, out ID_131_TRANS_RESPONSE receive_gpp, out string reason)
        {
            bool isSuccess = false;
            try
            {
                List<STR_VHMSG_TRANS_REQ> sTR_VHMSG_TRANS_REQs = GPP2STR_31(send_gpp);
                STR_VHMSG_TRANS_RESP sTR_VHMSG_TRANS_RESP;
                TrxTcpIp.ReturnCode rtnCode = sendRecv(sTR_VHMSG_TRANS_REQs, out sTR_VHMSG_TRANS_RESP, out reason);
                receive_gpp = STR2GPP_131(sTR_VHMSG_TRANS_RESP);
                isSuccess = rtnCode == TrxTcpIp.ReturnCode.Normal;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception");
                receive_gpp = null;
                reason = "命令下達時發生錯誤!";
            }
            return isSuccess;
        }

        public override bool send_Str35(ID_35_CST_ID_RENAME_REQUEST send_gpp, out ID_135_CST_ID_RENAME_RESPONSE receive_gpp)
        {
            string rtnMsg = string.Empty;
            STR_VHMSG_CSTID_RENAME_REQ strSend = GPP2STR_35(send_gpp);
            STR_VHMSG_CSTID_RENAME_RESP strReceive;
            TrxTcpIp.ReturnCode rtnCode = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, strSend, out strReceive, out rtnMsg);
            receive_gpp = STR2GPP_135(strReceive);
            return rtnCode == TrxTcpIp.ReturnCode.Normal;
        }

        public override bool send_Str37(string cmd_id, CMDCancelType actType)
        {
            bool isScuess = false;
            try
            {

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
                    isScuess = stRecv.ReplyCode == 0;
                }
                else
                {
                    isScuess = false;
                }
                isScuess = result == TrxTcpIp.ReturnCode.Normal;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception");
            }
            return isScuess;
        }
        public override bool send_Str39(ID_39_PAUSE_REQUEST send_gpp, out ID_139_PAUSE_RESPONSE receive_gpp)
        {
            string rtnMsg = string.Empty;
            STR_VHMSG_PAUSE_REQ strSend = GPP2STR_39(send_gpp);
            STR_VHMSG_PAUSE_RESP strReceive;
            TrxTcpIp.ReturnCode rtnCode = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, strSend, out strReceive, out rtnMsg);
            receive_gpp = STR2GPP_139(strReceive);
            return rtnCode == TrxTcpIp.ReturnCode.Normal;

            //bool isScuess = false;
            //try
            //{
            //    string rtnMsg = string.Empty;
            //    ID_39_PAUSE_REQUEST stSend;
            //    ID_139_PAUSE_RESPONSE stRecv;
            //    stSend = new ID_39_PAUSE_REQUEST()
            //    {
            //        EventType = eventType,
            //        PauseType = pauseType
            //    };

            //    WrapperMessage wrapper = new WrapperMessage
            //    {
            //        ID = VHMSGIF.ID_PAUSE_REQUEST,
            //        PauseReq = stSend
            //    };
            //    com.mirle.iibg3k0.ttc.Common.TrxTcpIp.ReturnCode result = snedRecv(wrapper, out stRecv, out rtnMsg);
            //    isScuess = result == TrxTcpIp.ReturnCode.Normal;
            //}
            //catch (Exception ex)
            //{
            //    logger.Error(ex, "Exception");
            //}
            //return isScuess;
        }


        public override bool send_Str71(ID_71_RANGE_TEACHING_REQUEST send_gpp, out ID_171_RANGE_TEACHING_RESPONSE receive_gpp)
        {
            string rtnMsg = string.Empty;
            STR_VHMSG_TEACHING_REQ strSend = GPP2STR_71(send_gpp);
            STR_VHMSG_TEACHING_RESP strReceive;
            TrxTcpIp.ReturnCode rtnCode;
            lock (sendRecv_LockObj)
            {
                rtnCode = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, strSend, out strReceive, out rtnMsg);
            }
            receive_gpp = STR2GPP_171(strReceive);

            return rtnCode == TrxTcpIp.ReturnCode.Normal;
        }


        protected void str102_ReceiveProcess(object sender, TcpIpEventArgs e)
        {
            STR_VHMSG_VHCL_KISO_VERSION_REP recive_str = TCPUtility._Packet2Str<STR_VHMSG_VHCL_KISO_VERSION_REP>((byte[])e.objPacket, eqpt.TcpIpAgentName);
            ID_102_BASIC_INFO_VERSION_REP recive_gpp = STR2GPP_102(recive_str);
            dynamic service = scApp.VehicleService;
            service.BasicInfoVersionReport(bcfApp, eqpt, recive_gpp, e.iSeqNum);
        }

        protected void str162_ReceiveProcess(object sender, TcpIpEventArgs e)
        {
            STR_VHMSG_GUIDE_DATA_UPLOAD_REQ recive_str = TCPUtility._Packet2Str<STR_VHMSG_GUIDE_DATA_UPLOAD_REQ>((byte[])e.objPacket, eqpt.TcpIpAgentName);
            ID_162_GUIDE_DATA_UPLOAD_REP recive_gpp = STR2GPP_162(recive_str);
            dynamic service = scApp.VehicleService;
            service.GuideDataUploadRequest(bcfApp, eqpt, recive_gpp, e.iSeqNum);
        }
        protected void str174_ReceiveProcess(object sender, TcpIpEventArgs e)
        {
            STR_VHMSG_ADDRESS_TEACH_REP recive_str = TCPUtility._Packet2Str<STR_VHMSG_ADDRESS_TEACH_REP>((byte[])e.objPacket, eqpt.TcpIpAgentName);
            ID_174_ADDRESS_TEACH_REPORT recive_gpp = STR2GPP_174(recive_str);
            dynamic service = scApp.VehicleService;
            service.AddressTeachReport(bcfApp, eqpt, recive_gpp, e.iSeqNum);
        }
        protected void str194_ReceiveProcess(object sender, TcpIpEventArgs e)
        {
            STR_VHMSG_ALARM_REP recive_str = TCPUtility._Packet2Str<STR_VHMSG_ALARM_REP>((byte[])e.objPacket, eqpt.TcpIpAgentName);
            ID_194_ALARM_REPORT recive_gpp = STR2GPP_194(recive_str);
            dynamic service = scApp.VehicleService;
            service.AlarmReport(bcfApp, eqpt, recive_gpp, e.iSeqNum);
        }

        protected void str132_Receive(object sender, TcpIpEventArgs e)
        {
            STR_VHMSG_TRANS_COMP_REP recive_str = TCPUtility._Packet2Str<STR_VHMSG_TRANS_COMP_REP>((byte[])e.objPacket, eqpt.TcpIpAgentName);
            ID_132_TRANS_COMPLETE_REPORT recive_gpp = STR2GPP_132(recive_str);
            scApp.VehicleBLL.setAndPublishPositionReportInfo2Redis(eqpt.VEHICLE_ID, recive_gpp);

            dynamic service = scApp.VehicleService;
            service.CommandCompleteReport(tcpipAgentName, bcfApp, eqpt, recive_gpp, e.iSeqNum);
        }

        object str134_lockObj = new object();
        protected void str134_136_Receive(object sender, TcpIpEventArgs e)
        {
            string ID_TRANS_EVENT_REPORT = VHMSGIF.ID_TRANS_PASS_EVENT_REPORT.ToString();
            int threadID = System.Threading.Thread.CurrentThread.ManagedThreadId;
            Stopwatch sw = scApp.StopwatchPool.GetObject();
            string lockTraceInfo = string.Format("VH ID:{0},Pack ID:{1},Seq Num:{2},ThreadID:{3},"
                , eqpt.VEHICLE_ID
                , e.iPacketID
                , e.iSeqNum.ToString()
                , threadID.ToString());
            try
            {
                int iPacketID = 0;
                int.TryParse(e.iPacketID, out iPacketID);
                sw.Start();
                LogManager.GetLogger("LockInfo").Debug(string.Concat(lockTraceInfo, "Wait Lock In"));
                switch (iPacketID)
                {
                    case VHMSGIF.ID_TRANS_PASS_EVENT_REPORT:
                        SCUtility.LockWithTimeout(str134_lockObj, SCAppConstants.LOCK_TIMEOUT_MS, str134_ReceiveProcess, sender, e);
                        break;
                    case VHMSGIF.ID_TRANS_EVENT_REPORT:
                        SCUtility.LockWithTimeout(str134_lockObj, SCAppConstants.LOCK_TIMEOUT_MS, str136_ReceiveProcess, sender, e);
                        break;
                }
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
        protected void str134_ReceiveProcess(object sender, TcpIpEventArgs e)
        {
            STR_VHMSG_TRANS_PASS_EVENT_REP recive_str = TCPUtility._Packet2Str<STR_VHMSG_TRANS_PASS_EVENT_REP>((byte[])e.objPacket, eqpt.TcpIpAgentName);
            ID_134_TRANS_EVENT_REP recive_gpp = STR2GPP_134(recive_str);
            //TODO 需比較是否有位置重複的問題 => OK
            //if (!SCUtility.isMatche(eqpt.CUR_SEC_ID, recive_str.CurrentAdrID) || !SCUtility.isMatche(eqpt.CUR_SEC_ID, recive_str.CurrentSecID))
            //{
            SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, 0, recive_gpp);
            scApp.VehicleBLL.setAndPublishPositionReportInfo2Redis(eqpt.VEHICLE_ID, recive_gpp);
            //scApp.VehicleBLL.setPositionReportInfo2Redis(eqpt.VEHICLE_ID, recive_gpp);
            //scApp.VehicleBLL.PublishPositionReportInfo2Redis(eqpt.VEHICLE_ID, recive_gpp);
            //}

            //dynamic service = scApp.VehicleService;
            //service.PositionReport_100(bcfApp, eqpt, recive_gpp, e.iSeqNum);
        }
        protected void str136_ReceiveProcess(object sender, TcpIpEventArgs e)
        {
            STR_VHMSG_TRANS_EVENT_REP recive_str = TCPUtility._Packet2Str<STR_VHMSG_TRANS_EVENT_REP>((byte[])e.objPacket, eqpt.TcpIpAgentName);
            ID_136_TRANS_EVENT_REP recive_gpp = STR2GPP_136(recive_str);

            if (recive_gpp.EventType == EventType.AdrOrMoveArrivals)
            {
                List<string> adrs = new List<string>();
                adrs.Add(SCUtility.Trim(recive_gpp.CurrentAdrID));
                List<ASECTION> Secs = scApp.MapBLL.loadSectionByToAdrs(adrs);
                if (Secs.Count > 0)
                {
                    recive_gpp.CurrentSecID = Secs[0].SEC_ID.Trim();
                }
            }

            //switch (recive_str.EventType)
            //{
            //    default:
            //        scApp.VehicleBLL.setAndPublishPositionReportInfo2Redis(eqpt.VEHICLE_ID, recive_gpp);
            //        break;
            //}
            dynamic service = scApp.VehicleService;
            service.TranEventReport(bcfApp, eqpt, recive_gpp, e.iSeqNum);
        }
        protected void str144_ReceiveProcess(object sender, TcpIpEventArgs e)
        {
            STR_VHMSG_STATUS_CHANGE_REP recive_str = TCPUtility._Packet2Str<STR_VHMSG_STATUS_CHANGE_REP>((byte[])e.objPacket, eqpt.TcpIpAgentName);
            ID_144_STATUS_CHANGE_REP recive_gpp = STR2GPP_144(recive_str);
            scApp.VehicleBLL.setAndPublishPositionReportInfo2Redis(eqpt.VEHICLE_ID, recive_gpp);
            dynamic service = scApp.VehicleService;
            service.StatusReport(bcfApp, eqpt, recive_gpp, e.iSeqNum);

            ID_44_STATUS_CHANGE_RESPONSE send_str = new ID_44_STATUS_CHANGE_RESPONSE
            {
                ReplyCode = 0
            };
            WrapperMessage wrapper = new WrapperMessage
            {
                SeqNum = e.iSeqNum,
                StatusChangeResp = send_str
            };

            //Boolean resp_cmp = ITcpIpControl.sendGoogleMsg(bcfApp, eqpt.TcpIpAgentName, wrapper, true);
            Boolean resp_cmp = eqpt.sendMessage(wrapper, true);
            LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(EQTcpIpMapAction_Taichung6F), Device: "OHxC",
              seq_num: e.iSeqNum, Data: send_str,
              VehicleID: eqpt.VEHICLE_ID,
              CarrierID: eqpt.CST_ID);
            SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, e.iSeqNum, send_str, resp_cmp.ToString());
        }
        protected void str172_ReceiveProcess(object sender, TcpIpEventArgs e)
        {
            STR_VHMSG_TEACHING_COMP_REP recive_str = TCPUtility._Packet2Str<STR_VHMSG_TEACHING_COMP_REP>((byte[])e.objPacket, eqpt.TcpIpAgentName);
            ID_172_RANGE_TEACHING_COMPLETE_REPORT recive_gpp = STR2GPP_172(recive_str);
            dynamic service = scApp.VehicleService;
            service.RangeTeachingCompleteReport(tcpipAgentName, bcfApp, eqpt, recive_gpp, e.iSeqNum);
        }


        public override bool snedMessage(WrapperMessage wrapper, bool isReply = false)
        {
            IMessage sendEntityMsg = null;
            var value = TCPUtility.getGoogleBeSetToOneOf(wrapper, out sendEntityMsg);
            switch (value.FieldNumber)
            {
                case WrapperMessage.StatusChangeRespFieldNumber:
                    STR_VHMSG_STATUS_CHANGE_RESP sendStr_status_resp = GPP2STR_44(wrapper.SeqNum, sendEntityMsg as ID_44_STATUS_CHANGE_RESPONSE);
                    return ITcpIpControl.sendMsg(bcfApp, tcpipAgentName, ref sendStr_status_resp, isReply);
                case WrapperMessage.TranCmpRespFieldNumber:
                    STR_VHMSG_TRANS_COMP_RESP sendStrTransComp = GPP2STR_32(wrapper.SeqNum, sendEntityMsg as ID_32_TRANS_COMPLETE_RESPONSE);
                    return ITcpIpControl.sendMsg(bcfApp, tcpipAgentName, ref sendStrTransComp, isReply);
                case WrapperMessage.RangeTeachingCmpRespFieldNumber:
                    STR_VHMSG_TEACHING_COMP_RESP sendStrTeachComp = GPP2STR_72(wrapper.SeqNum, sendEntityMsg as ID_72_RANGE_TEACHING_COMPLETE_RESPONSE);
                    ITcpIpControl.sendMsg(bcfApp, tcpipAgentName, ref sendStrTeachComp, isReply);
                    return true;
                case WrapperMessage.BasicInfoVersionRespFieldNumber:
                    STR_VHMSG_VHCL_KISO_VERSION_RESP sendVersionResp = GPP2STR_2(wrapper.SeqNum, sendEntityMsg as ID_2_BASIC_INFO_VERSION_RESPONSE);
                    ITcpIpControl.sendMsg(bcfApp, tcpipAgentName, ref sendVersionResp, isReply);
                    return true;
                case WrapperMessage.GUIDEDataUploadRespFieldNumber:
                    STR_VHMSG_GUIDE_DATA_UPLOAD_REP sendIndividualDownloadResp = GPP2STR_62(wrapper.SeqNum, sendEntityMsg as ID_62_GUID_DATA_UPLOAD_RESPONSE);
                    ITcpIpControl.sendMsg(bcfApp, tcpipAgentName, ref sendIndividualDownloadResp, isReply);
                    return true;
                case WrapperMessage.ImpTransEventRespFieldNumber:
                    STR_VHMSG_TRANS_EVENT_RESP sendTransEventResp = GPP2STR_36(wrapper.SeqNum, sendEntityMsg as ID_36_TRANS_EVENT_RESPONSE);
                    ITcpIpControl.sendMsg(bcfApp, tcpipAgentName, ref sendTransEventResp, isReply);
                    return true;
                case WrapperMessage.AddressTeachRespFieldNumber:
                    STR_VHMSG_ADDRESS_TEACH_RESP sendAddressTeachResp = GPP2STR_74(wrapper.SeqNum, sendEntityMsg as ID_74_ADDRESS_TEACH_RESPONSE);
                    ITcpIpControl.sendMsg(bcfApp, tcpipAgentName, ref sendAddressTeachResp, isReply);
                    return true;
                case WrapperMessage.AlarmRespFieldNumber:
                    STR_VHMSG_ALARM_RESP sendAlarmResp = GPP2STR_94(wrapper.SeqNum, sendEntityMsg as ID_94_ALARM_RESPONSE);

                    return ITcpIpControl.sendMsg(bcfApp, tcpipAgentName, ref sendAlarmResp, isReply);

                default:
                    return false;
            }
        }
        object sendRecv_LockObj = new object();
        public override com.mirle.iibg3k0.ttc.Common.TrxTcpIp.ReturnCode snedRecv<TSource2>(WrapperMessage wrapper, out TSource2 stRecv, out string rtnMsg)
        {
            bool lockTaken = false;
            try
            {
                Monitor.TryEnter(sendRecv_LockObj, SCAppConstants.LOCK_TIMEOUT_MS, ref lockTaken);
                if (!lockTaken)
                    throw new TimeoutException("snedRecv time out lock happen");
                TrxTcpIp.ReturnCode rtnCode = default(TrxTcpIp.ReturnCode);
                IMessage sendEntityMsg = null;
                var value = TCPUtility.getGoogleBeSetToOneOf(wrapper, out sendEntityMsg);
                stRecv = default(TSource2);
                rtnMsg = string.Empty;
                switch (value.FieldNumber)
                {
                    case WrapperMessage.TransReqFieldNumber:
                        List<STR_VHMSG_TRANS_REQ> sTR_VHMSG_TRANS_REQs = GPP2STR_31(sendEntityMsg as ID_31_TRANS_REQUEST);
                        STR_VHMSG_TRANS_RESP sTR_VHMSG_TRANS_RESP;
                        rtnCode = sendRecv(sTR_VHMSG_TRANS_REQs, out sTR_VHMSG_TRANS_RESP, out rtnMsg);
                        //stRecv = (TSource2)STR2GPP_131(sTR_VHMSG_TRANS_RESP);
                        return rtnCode;
                    case WrapperMessage.TransCancelReqFieldNumber:
                        STR_VHMSG_TRANS_CANCEL_REQ sTR_VHMSG_TRANS_CANCEL_REQ = GPP2STR_37(sendEntityMsg as ID_37_TRANS_CANCEL_REQUEST);
                        STR_VHMSG_TRANS_CANCEL_RESP sTR_VHMSG_TRANS_CANCEL_RESP;
                        rtnCode = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, sTR_VHMSG_TRANS_CANCEL_REQ, out sTR_VHMSG_TRANS_CANCEL_RESP, out rtnMsg);
                        if (rtnCode == TrxTcpIp.ReturnCode.Normal) stRecv = (TSource2)STR2GPP_137(sTR_VHMSG_TRANS_CANCEL_RESP);
                        return rtnCode;
                    //case WrapperMessage.PauseReqFieldNumber:
                    //    STR_VHMSG_PAUSE_REQ sTR_VHMSG_PAUSE_REQ = GPP2STR_39(sendEntityMsg as ID_39_PAUSE_REQUEST);
                    //    STR_VHMSG_PAUSE_RESP sTR_VHMSG_PAUSE_RESP;
                    //    rtnCode = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, sTR_VHMSG_PAUSE_REQ, out sTR_VHMSG_PAUSE_RESP, out rtnMsg);
                    //    if (rtnCode == TrxTcpIp.ReturnCode.Normal) stRecv = (TSource2)STR2GPP_139(sTR_VHMSG_PAUSE_RESP);
                    //    return rtnCode;
                    case WrapperMessage.RangeTeachingReqFieldNumber:
                        //STR_VHMSG_TEACHING_REQ sTR_VHMSG_TEACHING_REQ = GPP2STR_71(sendEntityMsg as ID_71_RANGE_TEACHING_REQUEST);
                        //STR_VHMSG_TEACHING_RESP sTR_VHMSG_TEACHING_RESP;
                        //rtnCode = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, sTR_VHMSG_TEACHING_REQ, out sTR_VHMSG_TEACHING_RESP, out rtnMsg);
                        //if (rtnCode == TrxTcpIp.ReturnCode.Normal) stRecv = (TSource2)STR2GPP_171(sTR_VHMSG_TEACHING_RESP);
                        return rtnCode;
                    default:
                        stRecv = default(TSource2);
                        rtnMsg = string.Empty;
                        return TrxTcpIp.ReturnCode.SendDataFail;
                }
            }
            finally
            {
                if (lockTaken) Monitor.Exit(sendRecv_LockObj);
            }
        }
        public TrxTcpIp.ReturnCode sendRecv(List<STR_VHMSG_TRANS_REQ> inStrs, out STR_VHMSG_TRANS_RESP ourStr, out string rtnMsg)
        {
            rtnMsg = "";
            ourStr = new STR_VHMSG_TRANS_RESP();
            for (int i = 0; i < inStrs.Count; i++)
            {
                TrxTcpIp.ReturnCode result = ITcpIpControl.sendRecv(bcfApp, tcpipAgentName, inStrs[i], out ourStr, out rtnMsg);
                if (result == TrxTcpIp.ReturnCode.Normal)
                {
                    if (ourStr.RespCode != 0)
                    {
                        break;
                    }
                }
                else
                {
                    return result;
                }
            }
            return TrxTcpIp.ReturnCode.Normal;
        }


        static public STR_VHMSG_HOST_KISO_VERSION_REP GPP2STR_1(ID_1_HOST_BASIC_INFO_VERSION_REP send_gpp)
        {
            char[] charArray_Year = send_gpp.DataDateTimeYear.ToCharArray();
            char[] charArray_Month = send_gpp.DataDateTimeMonth.ToCharArray();
            char[] charArray_Day = send_gpp.DataDateTimeDay.ToCharArray();
            char[] charArray_Hour = send_gpp.DataDateTimeHour.ToCharArray();
            char[] charArray_Minute = send_gpp.DataDateTimeMinute.ToCharArray();
            char[] charArray_Second = send_gpp.DataDateTimeSecond.ToCharArray();
            List<char[]> ListDataDateTime = new List<char[]>()
            { charArray_Year, charArray_Month,charArray_Day, charArray_Hour,charArray_Minute,charArray_Second};
            char[] dataDateTime = SCUtility.CombinedArray(ListDataDateTime);

            STR_VHMSG_HOST_KISO_VERSION_REP send_str = new STR_VHMSG_HOST_KISO_VERSION_REP()
            {
                PacketID = VHMSGIF.ID_HOST_KISO_VERSION_REPORT,
                DataDateTime = dataDateTime,
                LocalYear = SCUtility.convertToUInt16(send_gpp.CurrentTimeYear),
                LocalMonth = SCUtility.convertToUInt16(send_gpp.CurrentTimeMonth),
                LocalDay = SCUtility.convertToUInt16(send_gpp.CurrentTimeDay),
                LocalHour = SCUtility.convertToUInt16(send_gpp.CurrentTimeHour),
                LocalMinute = SCUtility.convertToUInt16(send_gpp.CurrentTimeMinute),
                LocalSecond = SCUtility.convertToUInt16(send_gpp.CurrentTimeSecond)
            };
            return send_str;
        }
        static public ID_101_HOST_BASIC_INFO_VERSION_RESPONSE STR2GPP_101(STR_VHMSG_HOST_KISO_VERSION_RESP receive_str)
        {
            ID_101_HOST_BASIC_INFO_VERSION_RESPONSE receive_gpp = new ID_101_HOST_BASIC_INFO_VERSION_RESPONSE()
            {
                ReplyCode = receive_str.RespCode
            };
            return receive_gpp;
        }

        static public ID_102_BASIC_INFO_VERSION_REP STR2GPP_102(STR_VHMSG_VHCL_KISO_VERSION_REP receive_str)
        {
            string year = getDateFromArray(receive_str.VerionStr, 0, 4);
            string month = getDateFromArray(receive_str.VerionStr, 4, 2);
            string day = getDateFromArray(receive_str.VerionStr, 6, 2);
            string hour = getDateFromArray(receive_str.VerionStr, 8, 2);
            string minute = getDateFromArray(receive_str.VerionStr, 10, 2);
            string second = getDateFromArray(receive_str.VerionStr, 12, 2);
            ID_102_BASIC_INFO_VERSION_REP receive_gpp = new ID_102_BASIC_INFO_VERSION_REP()
            {
                BasicInfoVersionYear = year,
                BasicInfoVersionMonth = month,
                BasicInfoVersionDay = day,
                BasicInfoVersionHour = hour,
                BasicInfoVersionMinute = minute,
                BasicInfoVersionSecond = second
            };
            return receive_gpp;
        }
        static public STR_VHMSG_VHCL_KISO_VERSION_RESP GPP2STR_2(int seq_num, ID_2_BASIC_INFO_VERSION_RESPONSE send_gpp)
        {
            STR_VHMSG_VHCL_KISO_VERSION_RESP send_str = new STR_VHMSG_VHCL_KISO_VERSION_RESP()
            {
                PacketID = VHMSGIF.ID_VHCL_KISO_VERSION_RESPONSE,
                SeqNum = (UInt16)seq_num,
                RespCode = (UInt16)send_gpp.ReplyCode
            };
            return send_str;
        }
        static string getDateFromArray(char[] dateArray, int index, int length)
        {
            char[] subArray = dateArray.SubArray(index, length);
            return new string(subArray);
        }

        static public STR_VHMSG_KISO_LIST_COUNT_REP GPP2STR_11(ID_11_BASIC_INFO_REP send_gpp)
        {
            UInt16 ListTable = convertvBits2UInt16
                (bit0: send_gpp.TravelBasicDataCount > 0,
                 bit1: send_gpp.SectionDataCount > 0,
                 bit2: send_gpp.AddressDataCount > 0,
                 bit3: send_gpp.ScaleDataCount > 0,
                 bit4: send_gpp.ContrlDataCount > 0,
                 bit5: send_gpp.GuideDataCount > 0);
            STR_VHMSG_KISO_LIST_COUNT_REP send_str = new STR_VHMSG_KISO_LIST_COUNT_REP()
            {
                PacketID = VHMSGIF.ID_KISO_LIST_COUNT_REPORT,
                ListTable = ListTable,
                ListCount1 = (UInt16)send_gpp.TravelBasicDataCount,
                ListCount2 = (UInt16)send_gpp.SectionDataCount,
                ListCount3 = (UInt16)send_gpp.AddressDataCount,
                ListCount4 = (UInt16)send_gpp.ScaleDataCount,
                ListCount5 = (UInt16)send_gpp.ContrlDataCount,
                ListCount6 = (UInt16)send_gpp.GuideDataCount
            };
            return send_str;
        }
        static public ID_111_BASIC_INFO_RESPONSE STR2GPP_111(STR_VHMSG_KISO_LIST_COUNT_RESP receive_str)
        {
            ID_111_BASIC_INFO_RESPONSE sned_gpp = new ID_111_BASIC_INFO_RESPONSE()
            {
                ReplyCode = receive_str.RespCode
            };
            return sned_gpp;
        }

        static public STR_VHMSG_KISO_TRAVEL_REP GPP2STR_13(ID_13_TAVELLING_DATA_REP send_gpp)
        {
            STR_VHMSG_KISO_TRAVEL_REP send_str = new STR_VHMSG_KISO_TRAVEL_REP()
            {
                PacketID = VHMSGIF.ID_KISO_TRAVEL_REPORT,
                Resolution = (UInt32)send_gpp.Resolution,
                StartStopSpd = (UInt32)send_gpp.StartStopSpd,
                MaxSpeed = (UInt32)send_gpp.MaxSpeed,
                AccelTime = (UInt32)send_gpp.AccelTime,
                SCurveRate = (UInt16)send_gpp.SCurveRate,
                OriginDir = (UInt16)send_gpp.OriginDir,
                OriginSpd = (UInt32)send_gpp.OriginSpd,
                BeaemSpd = (UInt32)send_gpp.BeaemSpd,
                ManualHSpd = (UInt32)send_gpp.ManualHSpd,
                ManualLSpd = (UInt32)send_gpp.ManualLSpd,
                TeachingSpd = (UInt32)send_gpp.TeachingSpd,
                RotateDir = (UInt16)send_gpp.RotateDir,
                EncoderPole = (UInt16)send_gpp.EncoderPole,
                PositionCompensation = (UInt32)send_gpp.PositionCompensation,
                KeepDistFar = (UInt32)send_gpp.KeepDistFar,
                KeepDistNear = (UInt32)send_gpp.KeepDistNear
            };
            return send_str;
        }
        static public ID_113_TAVELLING_DATA_RESPONSE STR2GPP_113(STR_VHMSG_KISO_TRAVEL_RESP receive_gpp)
        {
            ID_113_TAVELLING_DATA_RESPONSE sned_gpp = new ID_113_TAVELLING_DATA_RESPONSE()
            {
                ReplyCode = receive_gpp.RespCode
            };
            return sned_gpp;
        }

        static public List<STR_VHMSG_KISO_SECTION_REP> GPP2STR_15(ID_15_SECTION_DATA_REP send_gpp)
        {
            string ErrorMessage = "GPP2STR_15,Argument Error";
            List<STR_VHMSG_KISO_SECTION_REP> send_strs = new List<STR_VHMSG_KISO_SECTION_REP>();
            foreach (var section in send_gpp.Sections)
            {
                bool isSuccess = true;
                UInt16 section_id = 0;
                UInt16 from_adr = 0;
                UInt16 to_adr = 0;
                UInt16 seg_num_1 = 0;
                UInt16 seg_num_2 = 0;
                UInt16 at_seg_num = 0;
                isSuccess &= UInt16.TryParse(section.SectionID, out section_id); if (!isSuccess) { throw new ArgumentException(ErrorMessage, nameof(section.SectionID)); }
                isSuccess &= UInt16.TryParse(section.FromAddr, out from_adr); if (!isSuccess) { throw new ArgumentException(ErrorMessage, nameof(section.FromAddr)); }
                isSuccess &= UInt16.TryParse(section.ToAddr, out to_adr); if (!isSuccess) { throw new ArgumentException(ErrorMessage, nameof(section.ToAddr)); }
                isSuccess &= UInt16.TryParse(section.ChangeSegNum1, out seg_num_1);
                isSuccess &= UInt16.TryParse(section.ChangeSegNum2, out seg_num_2);
                isSuccess &= UInt16.TryParse(section.AtSegment, out at_seg_num);
                STR_VHMSG_KISO_SECTION_REP send_str = new STR_VHMSG_KISO_SECTION_REP()
                {
                    PacketID = VHMSGIF.ID_KISO_SECTION_REPORT,
                    DriveDir = (UInt16)section.DriveDir,
                    AeraSecsor = (UInt16)section.AeraSecsor,
                    GuideDir = (UInt16)section.GuideDir,
                    SectionID = section_id,
                    FromAddr = from_adr,
                    ToAddr = to_adr,
                    ControlTable = (UInt16)section.ControlTable,
                    Speed = (UInt32)section.Speed,
                    Distance = (UInt32)section.Distance,
                    ChangeAreaSensor1 = (UInt16)section.ChangeAreaSensor1,
                    ChangeGuideDir1 = (UInt16)section.ChangeGuideDir1,
                    ChangeSegNum1 = seg_num_1,
                    ChangeAreaSensor2 = (UInt16)section.ChangeAreaSensor2,
                    ChangeGuideDir2 = (UInt16)section.ChangeGuideDir2,
                    ChangeSegNum2 = seg_num_2,
                    AtSegment = at_seg_num,
                    MultiFlag = send_gpp.Sections.Last() == section ? (UInt16)0 : (UInt16)1
                };
                send_strs.Add(send_str);
            }
            return send_strs;
        }
        static public ID_115_SECTION_DATA_RESPONSE STR2GPP_115(STR_VHMSG_KISO_SECTION_RESP receive_gpp)
        {
            ID_115_SECTION_DATA_RESPONSE sned_gpp = new ID_115_SECTION_DATA_RESPONSE()
            {
                ReplyCode = receive_gpp.RespCode
            };
            return sned_gpp;
        }



        static public List<STR_VHMSG_KISO_ADDRESS_REP> GPP2STR_17(ID_17_ADDRESS_DATA_REP send_gpp)
        {
            List<STR_VHMSG_KISO_ADDRESS_REP> send_strs = new List<STR_VHMSG_KISO_ADDRESS_REP>();
            foreach (var adr in send_gpp.Addresss)
            {
                UInt16 adr_id = 0;
                UInt16.TryParse(adr.Addr, out adr_id);
                STR_VHMSG_KISO_ADDRESS_REP send_str = new STR_VHMSG_KISO_ADDRESS_REP()
                {
                    PacketID = VHMSGIF.ID_KISO_ADDRESS_REPORT,
                    Addr = adr_id,
                    Resolution = (UInt32)adr.Resolution,
                    Loaction = (UInt32)adr.Loaction,
                    BlockRelease = (UInt16)adr.BlockRelease,
                    HIDRelease = (UInt16)adr.HIDRelease,
                    MultiFlag = send_gpp.Addresss.Last() == adr ? (UInt16)0 : (UInt16)1
                };
                send_strs.Add(send_str);
            }

            return send_strs;
        }
        static public ID_117_ADDRESS_DATA_RESPONSE STR2GPP_117(STR_VHMSG_KISO_ADDRESS_RESP receive_gpp)
        {
            ID_117_ADDRESS_DATA_RESPONSE sned_gpp = new ID_117_ADDRESS_DATA_RESPONSE()
            {
                ReplyCode = receive_gpp.RespCode
            };
            return sned_gpp;
        }

        static public STR_VHMSG_KISO_SCALE_REP GPP2STR_19(ID_19_SCALE_DATA_REP send_gpp)
        {
            STR_VHMSG_KISO_SCALE_REP send_str = new STR_VHMSG_KISO_SCALE_REP()
            {
                PacketID = VHMSGIF.ID_KISO_SCALE_REPORT,
                Resolution = (UInt32)send_gpp.Resolution,
                InposArea = (UInt32)send_gpp.InposArea,
                InposStability = (UInt32)send_gpp.InposStability,
                ScalePulse = (UInt32)send_gpp.ScalePulse,
                ScaleOffset = (UInt32)send_gpp.ScaleOffset,
                ScaleReset = (UInt32)send_gpp.ScaleReset,
                ReadDir = (UInt16)send_gpp.ReadDir
            };
            return send_str;
        }
        static public ID_119_SCALE_DATA_RESPONSE STR2GPP_119(STR_VHMSG_KISO_SCALE_RESP receive_gpp)
        {
            ID_119_SCALE_DATA_RESPONSE sned_gpp = new ID_119_SCALE_DATA_RESPONSE()
            {
                ReplyCode = receive_gpp.RespCode
            };
            return sned_gpp;
        }

        static public STR_VHMSG_KISO_CONTROL_REP GPP2STR_21(ID_21_CONTROL_DATA_REP send_gpp)
        {
            STR_VHMSG_KISO_CONTROL_REP send_str = new STR_VHMSG_KISO_CONTROL_REP()
            {
                PacketID = VHMSGIF.ID_KISO_CONTROL_REPORT,
                TimeoutT1 = (UInt32)send_gpp.TimeoutT1,
                TimeoutT2 = (UInt32)send_gpp.TimeoutT2,
                TimeoutT3 = (UInt32)send_gpp.TimeoutT3,
                TimeoutT4 = (UInt32)send_gpp.TimeoutT4,
                TimeoutT5 = (UInt32)send_gpp.TimeoutT5,
                TimeoutT6 = (UInt32)send_gpp.TimeoutT6,
                TimeoutT7 = (UInt32)send_gpp.TimeoutT7,
                TimeoutT8 = (UInt32)send_gpp.TimeoutT8,
                TimeoutBlock = (UInt32)send_gpp.TimeoutBlock
            };
            return send_str;
        }
        static public ID_121_CONTROL_DATA_RESPONSE STR2GPP_121(STR_VHMSG_KISO_CONTROL_RESP receive_gpp)
        {
            ID_121_CONTROL_DATA_RESPONSE sned_gpp = new ID_121_CONTROL_DATA_RESPONSE()
            {
                ReplyCode = receive_gpp.RespCode
            };
            return sned_gpp;
        }

        static public STR_VHMSG_KISO_GUIDE_REP GPP2STR_23(ID_23_GUIDE_DATA_REP send_gpp)
        {
            STR_VHMSG_KISO_GUIDE_REP send_str = new STR_VHMSG_KISO_GUIDE_REP()
            {
                PacketID = VHMSGIF.ID_KISO_GUIDE_REPORT,
                StartStopSpd = (UInt32)send_gpp.StartStopSpd,
                MaxSpeed = (UInt32)send_gpp.MaxSpeed,
                AccelTime = (UInt32)send_gpp.AccelTime,
                SCurveRate = (UInt16)send_gpp.SCurveRate,
                NormalSpd = (UInt32)send_gpp.NormalSpd,
                ManualHSpd = (UInt32)send_gpp.ManualHSpd,
                ManualLSpd = (UInt32)send_gpp.ManualLSpd,
                LFLockPos = (UInt32)send_gpp.LFLockPos,
                LBLockPos = (UInt32)send_gpp.LBLockPos,
                RFLockPos = (UInt32)send_gpp.RFLockPos,
                RBLockPos = (UInt32)send_gpp.RBLockPos,
                ChangeStabilityTime = (UInt32)send_gpp.ChangeStabilityTime
            };
            return send_str;
        }
        static public ID_123_GUIDE_DATA_RESPONSE STR2GPP_123(STR_VHMSG_KISO_GUIDE_RESP receive_gpp)
        {
            ID_123_GUIDE_DATA_RESPONSE sned_gpp = new ID_123_GUIDE_DATA_RESPONSE()
            {
                ReplyCode = receive_gpp.RespCode
            };
            return sned_gpp;
        }



        static public STR_VHMSG_INDIVIDUAL_UPLOAD_REQ GPP2STR_61(ID_61_INDIVIDUAL_UPLOAD_REQ gpp)
        {
            STR_VHMSG_INDIVIDUAL_UPLOAD_REQ send_str = new STR_VHMSG_INDIVIDUAL_UPLOAD_REQ()
            {
                PacketID = VHMSGIF.ID_INDIVIDUAL_DATA_UPLOAD_REQUEST
            };
            return send_str;
        }
        static public ID_161_INDIVIDUAL_UPLOAD_RESPONSE STR2GPP_161(STR_VHMSG_INDIVIDUAL_UPLOAD_REP str)
        {
            ID_161_INDIVIDUAL_UPLOAD_RESPONSE sned_gpp = new ID_161_INDIVIDUAL_UPLOAD_RESPONSE()
            {
                OffsetGuideFL = str.OffsetGuideFL,
                OffsetGuideRL = str.OffsetGuideRL,
                OffsetGuideFR = str.OffsetGuideFR,
                OffsetGuideRR = str.OffsetGuideRR
            };
            return sned_gpp;
        }

        static public ID_162_GUIDE_DATA_UPLOAD_REP STR2GPP_162(STR_VHMSG_GUIDE_DATA_UPLOAD_REQ str)
        {
            ID_162_GUIDE_DATA_UPLOAD_REP sned_gpp = new ID_162_GUIDE_DATA_UPLOAD_REP()
            {
                OffsetGuideFL = str.OffsetGuideFL,
                OffsetGuideRL = str.OffsetGuideRL,
                OffsetGuideFR = str.OffsetGuideFR,
                OffsetGuideRR = str.OffsetGuideRR
            };
            return sned_gpp;
        }
        static public STR_VHMSG_GUIDE_DATA_UPLOAD_REP GPP2STR_62(int seq_num, ID_62_GUID_DATA_UPLOAD_RESPONSE gpp)
        {
            STR_VHMSG_GUIDE_DATA_UPLOAD_REP send_str = new STR_VHMSG_GUIDE_DATA_UPLOAD_REP()
            {
                PacketID = VHMSGIF.ID_GUIDE_DATA_UPLOAD_REPORT,
                SeqNum = (UInt16)seq_num,
                RespCode = (UInt16)gpp.ReplyCode
            };
            return send_str;
        }

        static public STR_VHMSG_INDIVIDUAL_CHANGE_REQ GPP2STR_63(ID_63_INDIVIDUAL_CHANGE_REQ gpp)
        {
            STR_VHMSG_INDIVIDUAL_CHANGE_REQ send_str = new STR_VHMSG_INDIVIDUAL_CHANGE_REQ()
            {
                PacketID = VHMSGIF.ID_INDIVIDUAL_DATA_CHANGE_REQUEST,
                OffsetGuideFL = (UInt16)gpp.OffsetGuideFL,
                OffsetGuideRL = (UInt16)gpp.OffsetGuideRL,
                OffsetGuideFR = (UInt16)gpp.OffsetGuideFR,
                OffsetGuideRR = (UInt16)gpp.OffsetGuideRR
            };
            return send_str;
        }
        static public ID_163_INDIVIDUAL_CHANGE_RESPONSE STR2GPP_163(STR_VHMSG_INDIVIDUAL_CHANGE_RESP str)
        {
            ID_163_INDIVIDUAL_CHANGE_RESPONSE sned_gpp = new ID_163_INDIVIDUAL_CHANGE_RESPONSE()
            {
                ReplyCode = str.RespCode
            };
            return sned_gpp;
        }




        static public STR_VHMSG_MODE_CHANGE_REQ GPP2STR_41(ID_41_MODE_CHANGE_REQ gpp)
        {
            STR_VHMSG_MODE_CHANGE_REQ send_str = new STR_VHMSG_MODE_CHANGE_REQ()
            {
                PacketID = VHMSGIF.ID_MODE_CHANGE_REQUEST,
                Mode = (UInt16)gpp.OperatingVHMode
            };
            return send_str;
        }
        static public ID_141_MODE_CHANGE_RESPONSE STR2GPP_141(STR_VHMSG_MODE_CHANGE_RESP str)
        {
            ID_141_MODE_CHANGE_RESPONSE sned_gpp = new ID_141_MODE_CHANGE_RESPONSE()
            {
                ReplyCode = str.RespCode
            };
            return sned_gpp;
        }

        static public STR_VHMSG_STATUS_REQ GPP2STR_43(ID_43_STATUS_REQUEST gpp)
        {
            STR_VHMSG_STATUS_REQ send_str = new STR_VHMSG_STATUS_REQ()
            {
                PacketID = VHMSGIF.ID_STATUS_REQUEST
            };
            return send_str;
        }
        static public ID_143_STATUS_RESPONSE STR2GPP_143(STR_VHMSG_STATUS_RESP str)
        {
            VhPowerStatus vHPwrSts = (VhPowerStatus)str.PwrSts;
            VHModeStatus vHMode = (VHModeStatus)str.Mode;
            VHActionStatus vHActionStatus = (VHActionStatus)str.ActSts;
            VhStopSingle obstacSts = (VhStopSingle)str.ObstacleSts;
            VhStopSingle blockSts = (VhStopSingle)str.BlockSts;
            VhStopSingle PauseSts = (VhStopSingle)str.PauseSts;
            VhStopSingle HIDSts = (VhStopSingle)str.HIDSts;
            VhStopSingle ErrorFlag = (VhStopSingle)str.ErrorFlag;
            VhLoadCSTStatus loadCSTStatus = (VhLoadCSTStatus)str.HasCst;
            int obst_dist = (int)str.KeepDistance;
            uint sec_dist = str.TravelDistance;
            UInt64 cmd_id = str.CmdID;
            //VHModeStatus vHMode = str.Mode;

            ID_143_STATUS_RESPONSE gpp = new ID_143_STATUS_RESPONSE()
            {
                CurrentAdrID = str.CurrentAdrID.ToString("0000"),
                CurrentSecID = str.CurrentSecID.ToString("0000"),
                PowerStatus = vHPwrSts,
                ModeStatus = vHMode,
                ActionStatus = vHActionStatus,
                ObstacleStatus = obstacSts,
                BlockingStatus = blockSts,
                HIDStatus = HIDSts,
                PauseStatus = PauseSts,
                ErrorStatus = ErrorFlag,
                ObstDistance = obst_dist,
                SecDistance = sec_dist,
                StoppedBlockID = str.StoppedBlockID.ToString("0000"),
                StoppedHIDID = str.StoppedHIDID.ToString("0000"),
                HasCST = loadCSTStatus,
                CmdID = cmd_id.ToString()
            };
            return gpp;

            //VhPowerStatus vHPwrSts = (VhPowerStatus)str.PwrSts;
            //VHModeStatus vHMode = (VHModeStatus)str.Mode;
            //VHActionStatus vHActionStatus = (VHActionStatus)str.ActSts;
            //VhStopSingle obstacSts = (VhStopSingle)str.ObstacleSts;
            //VhStopSingle blockSts = (VhStopSingle)str.BlockSts;
            //VhStopSingle PauseSts = (VhStopSingle)str.PauseSts;
            //VhStopSingle HIDSts = (VhStopSingle)str.HIDSts;
            //VhStopSingle ErrorFlag = (VhStopSingle)str.ErrorFlag;
            //VhLoadCSTStatus HasCst = (VhLoadCSTStatus)str.HasCst;
            //ulong  cmd_id = str.CmdID;
            //int  cs = str.CmdID;
            //int obst_dist = (int)str.KeepDistance;
            //uint sec_dist = str.TravelDistance;
            ////VHModeStatus vHMode = str.Mode;
            //ID_143_STATUS_RESPONSE send_gpp = new ID_143_STATUS_RESPONSE()
            //{
            //    CurrentAdrID = str.CurrentAdrID.ToString("0000"),
            //    CurrentSecID = str.CurrentSecID.ToString("0000"),
            //    PowerStatus = vHPwrSts,
            //    ModeStatus = vHMode,
            //    ActionStatus = vHActionStatus,
            //    ObstacleStatus = obstacSts,
            //    BlockingStatus = blockSts,
            //    PauseStatus = PauseSts,
            //    HIDStatus = HIDSts,
            //    ErrorStatus = ErrorFlag,
            //    ObstDistance = obst_dist,
            //    SecDistance = sec_dist,
            //    StoppedBlockID = str.StoppedBlockID.ToString("0000"),
            //    StoppedHIDID = str.StoppedHIDID.ToString("0000"),
            //    HasCST = HasCst,
            //    CmdID = 
            //};
            //return send_gpp;
        }

        static public STR_VHMSG_POWER_OPE_REQ GPP2STR_45(ID_45_POWER_OPE_REQ gpp)
        {
            STR_VHMSG_POWER_OPE_REQ str = new STR_VHMSG_POWER_OPE_REQ()
            {
                PacketID = VHMSGIF.ID_POWER_OPE_REQUEST,
                PwrMode = (UInt16)gpp.OperatingPowerMode
            };
            return str;
        }
        static public ID_145_POWER_OPE_RESPONSE STR2GPP_145(STR_VHMSG_POWER_OPE_RESP str)
        {
            ID_145_POWER_OPE_RESPONSE sned_gpp = new ID_145_POWER_OPE_RESPONSE()
            {
                ReplyCode = str.RespCode
            };
            return sned_gpp;
        }

        static public ID_174_ADDRESS_TEACH_REPORT STR2GPP_174(STR_VHMSG_ADDRESS_TEACH_REP str)
        {
            ID_174_ADDRESS_TEACH_REPORT sned_gpp = new ID_174_ADDRESS_TEACH_REPORT()
            {
                Addr = str.Address.ToString("0000"),
                Position = (int)str.Position
            };
            return sned_gpp;
        }
        static public STR_VHMSG_ADDRESS_TEACH_RESP GPP2STR_74(int seq_num, ID_74_ADDRESS_TEACH_RESPONSE gpp)
        {
            STR_VHMSG_ADDRESS_TEACH_RESP str = new STR_VHMSG_ADDRESS_TEACH_RESP()
            {
                PacketID = VHMSGIF.ID_ADDRESS_TEACH_RESPONSE,
                SeqNum = (UInt16)seq_num,
                RespCode = (UInt16)gpp.ReplyCode
            };
            return str;
        }

        static public STR_VHMSG_ALARM_RESET_REQ GPP2STR_91(ID_91_ALARM_RESET_REQUEST gpp)
        {
            STR_VHMSG_ALARM_RESET_REQ str = new STR_VHMSG_ALARM_RESET_REQ()
            {
                PacketID = VHMSGIF.ID_ALARM_RESET_REQUEST
            };
            return str;
        }
        static public ID_191_ALARM_RESET_RESPONSE STR2GPP_191(STR_VHMSG_ALARM_RESET_RESP str)
        {
            ID_191_ALARM_RESET_RESPONSE sned_gpp = new ID_191_ALARM_RESET_RESPONSE()
            {
                ReplyCode = str.RespCode
            };
            return sned_gpp;
        }

        static public ID_194_ALARM_REPORT STR2GPP_194(STR_VHMSG_ALARM_REP str)
        {
            ID_194_ALARM_REPORT sned_gpp = new ID_194_ALARM_REPORT()
            {
                ErrCode = str.ErrCode.ToString(),
                ErrStatus = (ErrorStatus)str.ErrStatus
            };
            return sned_gpp;
        }
        static public STR_VHMSG_ALARM_RESP GPP2STR_94(int seq_num, ID_94_ALARM_RESPONSE gpp)
        {
            STR_VHMSG_ALARM_RESP str = new STR_VHMSG_ALARM_RESP()
            {
                PacketID = VHMSGIF.ID_ALARM_RESPONSE,
                SeqNum = (UInt16)seq_num,
                RespCode = (UInt16)gpp.ReplyCode
            };
            return str;
        }


        static public List<STR_VHMSG_TRANS_REQ> GPP2STR_31(ID_31_TRANS_REQUEST gpp)
        {
            List<STR_VHMSG_TRANS_REQ> lstStSend = new List<STR_VHMSG_TRANS_REQ>();
            UInt64 icmd_id = 0;
            UInt16 ifromAdr = 0;
            UInt16 itoAdr = 0;
            // SCAppConstants.MirleActiveType mirleActiveType = default(SCAppConstants.MirleActiveType);
            //switch (gpp.ActType)
            //{
            //    case ActiveType.Move: mirleActiveType = SCAppConstants.MirleActiveType.Move; break;
            //    case ActiveType.Load: mirleActiveType = SCAppConstants.MirleActiveType.Load; break;
            //    case ActiveType.Unload: mirleActiveType = SCAppConstants.MirleActiveType.Unload; break;
            //    case ActiveType.Loadunload: mirleActiveType = SCAppConstants.MirleActiveType.LoadUnload; break;
            //    case ActiveType.Home: mirleActiveType = SCAppConstants.MirleActiveType.Teaching; break;
            //    case ActiveType.Mtlhome: mirleActiveType = SCAppConstants.MirleActiveType.MTLHome; break;
            //    case ActiveType.Cstidrename: mirleActiveType = SCAppConstants.MirleActiveType.Rename; break;
            //    case ActiveType.Techingmove: mirleActiveType = SCAppConstants.MirleActiveType.TechMove; break;
            //    case ActiveType.Override: mirleActiveType = SCAppConstants.MirleActiveType.Override; break;
            //    case ActiveType.Movetomtl: mirleActiveType = SCAppConstants.MirleActiveType.; break;
            //    default: return null;
            //}


            if (gpp.ActType != ActiveType.Home && gpp.ActType != ActiveType.Mtlhome)
            {
                if ((SCUtility.isEmpty(gpp.LoadAdr) || SCUtility.tryParseUInt16AndRecord(logger, nameof(gpp.LoadAdr), gpp.LoadAdr, out ifromAdr)) &&
                    (SCUtility.isEmpty(gpp.ToAdr) || SCUtility.tryParseUInt16AndRecord(logger, nameof(gpp.ToAdr), gpp.ToAdr, out itoAdr)) &&
                    SCUtility.tryParseUInt64AndRecord(logger, nameof(gpp.CmdID), gpp.CmdID, out icmd_id))
                {
                    //Not thing...
                }
                else
                {
                    return null;
                }
            }
            UInt16[] secids = Array.ConvertAll(gpp.GuideSections.ToArray(), UInt16.Parse);
            List<UInt16> sections = secids.ToList();
            List<List<UInt16>> sectionSplitList = SCUtility.SpiltList(sections, PACKET_SECTION_COUNT);
            foreach (List<UInt16> lst in sectionSplitList)
            {
                UInt16[] secIDs = lst.ToArray();
                UInt16[] sendSecIDs = new UInt16[PACKET_SECTION_COUNT];
                Array.Copy(secIDs, sendSecIDs, secIDs.Length);
                UInt16 count = (UInt16)secIDs.Length;
                STR_VHMSG_TRANS_REQ str = new STR_VHMSG_TRANS_REQ()
                {
                    PacketID = VHMSGIF.ID_TRANS_REQUEST,
                    CmdID = icmd_id,
                    ReqType = (UInt16)gpp.ActType,
                    FromAddr = ifromAdr,
                    ToAddr = itoAdr,
                    CstID = SCUtility.string2CharArray(gpp.CSTID, VHMSGIF.LEN_ITEM_CSTID),
                    SecCount = count,
                    Sections = sendSecIDs,
                    MultiFlag = sectionSplitList.Last().Equals(lst) ? (ushort)0 : (ushort)1
                };
                lstStSend.Add(str);
            }
            return lstStSend;
        }
        static public ID_131_TRANS_RESPONSE STR2GPP_131(STR_VHMSG_TRANS_RESP str)
        {
            ID_131_TRANS_RESPONSE gpp = new ID_131_TRANS_RESPONSE()
            {
                CmdID = str.CmdID.ToString(),
                ReplyCode = str.RespCode
            };
            return gpp;
        }

        static public STR_VHMSG_CSTID_RENAME_REQ GPP2STR_35(ID_35_CST_ID_RENAME_REQUEST gpp)
        {
            STR_VHMSG_CSTID_RENAME_REQ send_str = new STR_VHMSG_CSTID_RENAME_REQ()
            {
                PacketID = VHMSGIF.ID_CSTID_RENAME_REQUEST,
                CstID = SCUtility.string2CharArray(gpp.NEWCSTID, VHMSGIF.LEN_ITEM_CSTID)
            };
            return send_str;
        }
        static public ID_135_CST_ID_RENAME_RESPONSE STR2GPP_135(STR_VHMSG_CSTID_RENAME_RESP str)
        {
            ID_135_CST_ID_RENAME_RESPONSE sned_gpp = new ID_135_CST_ID_RENAME_RESPONSE()
            {
                ReplyCode = str.RespCode
            };
            return sned_gpp;
        }


        static public STR_VHMSG_TRANS_CANCEL_REQ GPP2STR_37(ID_37_TRANS_CANCEL_REQUEST gpp)
        {
            UInt64 icmd_id = 0;
            SCUtility.tryParseUInt64AndRecord(logger, nameof(gpp.CmdID), gpp.CmdID, out icmd_id);
            UInt16 requestType = 0;
            switch (gpp.ActType)
            {
                case CMDCancelType.CmdCancel:
                    requestType = 0;
                    break;
                case CMDCancelType.CmdAbort:
                    requestType = 1;
                    break;
            }
            STR_VHMSG_TRANS_CANCEL_REQ str = new STR_VHMSG_TRANS_CANCEL_REQ()
            {
                PacketID = VHMSGIF.ID_TRANS_CANCEL_REQUEST,
                CmdID = icmd_id,
                //ReqType = (UInt16)gpp.ActType
                ReqType = requestType
            };
            return str;
        }


        static public object STR2GPP_137(STR_VHMSG_TRANS_CANCEL_RESP str)
        {
            ID_137_TRANS_CANCEL_RESPONSE gpp = new ID_137_TRANS_CANCEL_RESPONSE()
            {
                CmdID = str.CmdID.ToString(),
                ReplyCode = str.RespCode
            };
            return gpp;
        }

        static public STR_VHMSG_PAUSE_REQ GPP2STR_39(ID_39_PAUSE_REQUEST gpp)
        {
            STR_VHMSG_PAUSE_REQ send_str = new STR_VHMSG_PAUSE_REQ()
            {
                PacketID = VHMSGIF.ID_PAUSE_REQUEST,
                EventType = (UInt16)gpp.EventType,
                PauseType = (UInt16)gpp.PauseType
            };
            return send_str;
        }
        static public ID_139_PAUSE_RESPONSE STR2GPP_139(STR_VHMSG_PAUSE_RESP str)
        {
            ID_139_PAUSE_RESPONSE sned_gpp = new ID_139_PAUSE_RESPONSE()
            {
                ReplyCode = str.RespCode
            };
            return sned_gpp;
        }

        private ID_132_TRANS_COMPLETE_REPORT STR2GPP_132(STR_VHMSG_TRANS_COMP_REP str)
        {
            if (!Enum.IsDefined(typeof(CompleteStatus), (Int32)str.CompSts))
            {
                throw new ArgumentException($"Proc func: {nameof(this.STR2GPP_132)}, but completeStatus:{ str.CompSts } notimplemented");
            }
            CompleteStatus completeStatus = (CompleteStatus)str.CompSts;
            //switch (str.CompSts)
            //{
            //    case (UInt16)SCAppConstants.MirleCompleteStatus.Normal: completeStatus = CompleteStatus.Normal; break;
            //    case (UInt16)SCAppConstants.MirleCompleteStatus.Cancel: completeStatus = CompleteStatus.Cancel; break;
            //    case (UInt16)SCAppConstants.MirleCompleteStatus.Abort: completeStatus = CompleteStatus.Abort; break;
            //    case (UInt16)SCAppConstants.MirleCompleteStatus.InterlockError: completeStatus = CompleteStatus.InterlockError; break;
            //    case (UInt16)SCAppConstants.MirleCompleteStatus.IDMisMatch: completeStatus = CompleteStatus.IdMismatch; break;
            //    default: throw new ArgumentException($"Proc func: {nameof(this.STR2GPP_132)}, but completeStatus:{ completeStatus } notimplemented");
            //}

            ID_132_TRANS_COMPLETE_REPORT gpp = new ID_132_TRANS_COMPLETE_REPORT()
            {
                CmdID = str.CmdID.ToString(),
                CmpStatus = completeStatus,
                CSTID = new string(str.CstID),
                CurrentAdrID = str.Address.ToString("0000"),
                CurrentSecID = str.Section.ToString("0000"),
                CmdDistance = (int)str.TotalTravelDis
            };
            return gpp;
        }

        private STR_VHMSG_TRANS_COMP_RESP GPP2STR_32(int seq_num, ID_32_TRANS_COMPLETE_RESPONSE gpp)
        {
            STR_VHMSG_TRANS_COMP_RESP str = new STR_VHMSG_TRANS_COMP_RESP()
            {
                PacketID = VHMSGIF.ID_TRANS_COMPLETE_RESPONSE,
                SeqNum = (UInt16)seq_num,
                RespCode = (UInt16)gpp.ReplyCode
            };
            return str;
        }

        static public ID_134_TRANS_EVENT_REP STR2GPP_134(STR_VHMSG_TRANS_PASS_EVENT_REP str)
        {
            ID_134_TRANS_EVENT_REP gpp = new ID_134_TRANS_EVENT_REP()
            {
                EventType = (EventType)str.EventType,
                CurrentSecID = str.CurrentSecID.ToString("0000"),
                CurrentAdrID = str.CurrentAdrID.ToString("0000"),
                SecDistance = str.Sec_Distance
            };
            return gpp;
        }

        static public ID_136_TRANS_EVENT_REP STR2GPP_136(STR_VHMSG_TRANS_EVENT_REP str)
        {
            ID_136_TRANS_EVENT_REP gpp = new ID_136_TRANS_EVENT_REP()
            {
                EventType = (EventType)str.EventType,
                CurrentSecID = str.CurrentSecID.ToString("0000"),
                CurrentAdrID = str.CurrentAdrID.ToString("0000"),
                RequestBlockID = str.Request_Block_ID.ToString("0000"),
                RequestHIDID = str.Request_HID_ID.ToString("0000"),
                ReleaseBlockAdrID = str.BlockReleaseAdrID.ToString("0000"),
                ReleaseHIDAdrID = str.HIDReleaseAdrID.ToString("0000"),
                CSTID = new string(str.CstID)
            };
            return gpp;
        }
        static public STR_VHMSG_TRANS_EVENT_RESP GPP2STR_36(int seq_num, ID_36_TRANS_EVENT_RESPONSE gpp)
        {
            STR_VHMSG_TRANS_EVENT_RESP str = new STR_VHMSG_TRANS_EVENT_RESP()
            {
                PacketID = VHMSGIF.ID_TRANS_EVENT_RESPONSE,
                SeqNum = (UInt16)seq_num,
                Is_Block_Pass = (UInt16)gpp.IsBlockPass,
                Is_HID_Pass = (UInt16)gpp.IsHIDPass
            };
            return str;
        }

        static public ID_144_STATUS_CHANGE_REP STR2GPP_144(STR_VHMSG_STATUS_CHANGE_REP str)
        {
            VhPowerStatus vHPwrSts = (VhPowerStatus)str.PwrSts;
            VHModeStatus vHMode = (VHModeStatus)str.Mode;
            VHActionStatus vHActionStatus = (VHActionStatus)str.ActSts;
            VhStopSingle obstacSts = (VhStopSingle)str.ObstacleSts;
            VhStopSingle blockSts = (VhStopSingle)str.BlockSts;
            VhStopSingle PauseSts = (VhStopSingle)str.PauseSts;
            VhStopSingle HIDSts = (VhStopSingle)str.HIDSts;
            VhStopSingle ErrorFlag = (VhStopSingle)str.ErrorFlag;
            VhLoadCSTStatus loadCSTStatus = (VhLoadCSTStatus)str.HasCst;
            int obst_dist = (int)str.KeepDistance;
            uint sec_dist = str.TravelDistance;
            UInt64 cmd_id = str.CmdID;
            //VHModeStatus vHMode = str.Mode;

            ID_144_STATUS_CHANGE_REP gpp = new ID_144_STATUS_CHANGE_REP()
            {
                CurrentAdrID = str.CurrentAdrID.ToString("0000"),
                CurrentSecID = str.CurrentSecID.ToString("0000"),
                PowerStatus = vHPwrSts,
                ModeStatus = vHMode,
                ActionStatus = vHActionStatus,
                ObstacleStatus = obstacSts,
                BlockingStatus = blockSts,
                HIDStatus = HIDSts,
                PauseStatus = PauseSts,
                ErrorStatus = ErrorFlag,
                ObstDistance = obst_dist,
                SecDistance = sec_dist,
                StoppedBlockID = str.StoppedBlockID.ToString("0000"),
                StoppedHIDID = str.StoppedHIDID.ToString("0000"),
                HasCST = loadCSTStatus,
                CmdID = cmd_id.ToString(),
                CSTID = new string(str.CstID)
            };
            return gpp;
        }
        static public STR_VHMSG_STATUS_CHANGE_RESP GPP2STR_44(int seq_num, ID_44_STATUS_CHANGE_RESPONSE gpp)
        {
            STR_VHMSG_STATUS_CHANGE_RESP str = new STR_VHMSG_STATUS_CHANGE_RESP()
            {
                PacketID = VHMSGIF.ID_STATUS_CHANGE_RESPONSE,
                SeqNum = (ushort)seq_num,
                RespCode = (ushort)gpp.ReplyCode
            };
            return str;
        }

        private STR_VHMSG_TEACHING_REQ GPP2STR_71(ID_71_RANGE_TEACHING_REQUEST gpp)
        {
            UInt32 from_adr = 0;
            SCUtility.tryParseUInt32AndRecord(logger, nameof(gpp.FromAdr), gpp.FromAdr, out from_adr);
            UInt32 to_adr = 0;
            SCUtility.tryParseUInt32AndRecord(logger, nameof(gpp.ToAdr), gpp.ToAdr, out to_adr);
            STR_VHMSG_TEACHING_REQ str = new STR_VHMSG_TEACHING_REQ()
            {
                PacketID = VHMSGIF.ID_SECTION_TEACH_REQUEST,
                FromAddress = from_adr,
                ToAddress = to_adr
            };
            return str;
        }
        private ID_171_RANGE_TEACHING_RESPONSE STR2GPP_171(STR_VHMSG_TEACHING_RESP str)
        {
            ID_171_RANGE_TEACHING_RESPONSE iD_171_RANGE_TEACHING_RESPONSE = new ID_171_RANGE_TEACHING_RESPONSE()
            {
                ReplyCode = str.RespCode
            };
            return iD_171_RANGE_TEACHING_RESPONSE;
        }

        private ID_172_RANGE_TEACHING_COMPLETE_REPORT STR2GPP_172(STR_VHMSG_TEACHING_COMP_REP str)
        {
            ID_172_RANGE_TEACHING_COMPLETE_REPORT gpp = new ID_172_RANGE_TEACHING_COMPLETE_REPORT()
            {
                FromAdr = str.FromAddress.ToString("0000"),
                ToAdr = str.ToAddress.ToString("0000"),
                SecDistance = str.Distance
            };
            return gpp;
        }
        private STR_VHMSG_TEACHING_COMP_RESP GPP2STR_72(int seq_num, ID_72_RANGE_TEACHING_COMPLETE_RESPONSE gpp)
        {
            STR_VHMSG_TEACHING_COMP_RESP str = new STR_VHMSG_TEACHING_COMP_RESP()
            {
                PacketID = VHMSGIF.ID_SECTION_TEACH_COMPLETE_RESPONSE,
                SeqNum = (UInt16)seq_num
            };
            return str;
        }



        //protected override void Connection(object sender, TcpIpEventArgs e)
        //{
        //    eqpt.isTcpIpConnect = true;
        //}
        //protected override void Disconnection(object sender, TcpIpEventArgs e)
        //{
        //    eqpt.isTcpIpConnect = false;
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


        const string PLC_DEFUALT_VALUE = "0000";
        public virtual void VehicleStatusChangeFromPLC(object sender, ValueChangedEventArgs args)
        {
            //var function =
            //    scApp.getFunBaseObj<VehicleStatus>(eqpt.VEHICLE_ID) as VehicleStatus;
            VehicleInfoFromPLC function = new VehicleInfoFromPLC();
            try
            {
                function.EQ_ID = eqpt.VEHICLE_ID;
                //1.建立各個Function物件
                function.Timestamp = DateTime.Now;
                function.Read(bcfApp, eqpt.EqptObjectCate, eqpt.VEHICLE_ID);
                //2.read log
                LogManager.GetLogger("RecodeVehicleInfoFromPLC").Info(function.ToString());
                eqpt.Status_Info_PLC = function;
                //3.logical (include db save)
                string sec_id = function.cur_sec_id.ToString(FORMAT_SECTION_ID_LENGTH);
                string add_id = function.cur_adr_id.ToString(FORMAT_ADDRESS_ID_LENGTH);
                UInt32 sec_dist = function.ACC_SEC_DIST;
                UInt32 hasCst = function.HAS_CST;

                if (SCUtility.isMatche(PLC_DEFUALT_VALUE, sec_id) ||
                    SCUtility.isMatche(PLC_DEFUALT_VALUE, add_id))
                {
                    return;
                }

                if (!SCUtility.isMatche(eqpt.CUR_SEC_ID, sec_id) ||
                    !SCUtility.isMatche(eqpt.CUR_SEC_ID, sec_id) ||
                    eqpt.HAS_CST != hasCst ||
                    eqpt.ACC_SEC_DIST != sec_dist)
                {
                    //scApp.VehicleBLL.setAndPublishPositionReportInfo2Redis(eqpt.VEHICLE_ID, sec_id, add_id, sec_dist);
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception");
            }
            finally
            {
                //scApp.putFunBaseObj<VehicleStatus>(function);
            }
        }



        public override void doCatchPLCCSTInterfaceLog()
        {

            VehicleCSTInterfaceIndexChanged(null, null);
        }
        object plc_cst_interface_lock_obj = new object();
        DateTime LastHandshakeTime = DateTime.Now;
        public virtual void VehicleCSTInterfaceIndexChanged(object sender, ValueChangedEventArgs args)
        {
            lock (plc_cst_interface_lock_obj)
            {
                var function =
                    scApp.getFunBaseObj<VehicleCSTInterface>(eqpt.VEHICLE_ID) as VehicleCSTInterface;
                try
                {
                    //1.建立各個Function物件
                    function.Read(bcfApp, eqpt.EqptObjectCate, eqpt.VEHICLE_ID, 20);
                    function.Details = function.Details.
                                       Where(detail => detail.Timestamp > LastHandshakeTime).
                                       OrderBy(detail => detail.Timestamp).
                                       ToList();
                    if (function.Details.Count() > 0)
                        LastHandshakeTime = function.Details.Last().Timestamp;
                    //2.read log
                    foreach (var detail in function.Details)
                    {
                        LogManager.GetLogger("RecodeVehicleCSTInterface").Info(detail.ToString());
                    }
                    //3.logical (include db save)
                    //eqpt.Inline_Mode = function.InlineMode;
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Exception");
                }
                finally
                {
                    scApp.putFunBaseObj<VehicleInfoFromPLC>(function);
                }
            }
        }

        public override void PLC_Control_TrunOn()
        {
            Task.Run(() => ContinuedToIncreasePLCControlIndex());
        }

        public override void PLC_Control_TrunOff()
        {
            setVehicleControlItemForPLC(new bool[16]);
        }

        private long syncPointForPLCControl = 0;
        private void ContinuedToIncreasePLCControlIndex()
        {
            if (System.Threading.Interlocked.Exchange(ref syncPointForPLCControl, 1) == 0)
            {
                try
                {

                    ValueWrite controlIndexVW = scApp.getBCFApplication().getWriteValueEvent(SCAppConstants.EQPT_OBJECT_CATE_EQPT, eqpt.VEHICLE_ID, "PLC_CONTORL_INDEX");
                    while (eqpt.isPLCInControl)
                    {
                        UInt16 isAliveIndex = (UInt16)controlIndexVW.getText();
                        int x = isAliveIndex + 1;
                        if (x > 9999) { x = 1; }
                        controlIndexVW.setWriteValue((UInt16)x);
                        ISMControl.writeDeviceBlock(scApp.getBCFApplication(), controlIndexVW);
                        SpinWait.SpinUntil(() => false, 1900);
                    }
                    controlIndexVW.initWriteValue();
                    ISMControl.writeDeviceBlock(scApp.getBCFApplication(), controlIndexVW);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Exception");
                }
                finally
                {
                    System.Threading.Interlocked.Exchange(ref syncPointForPLCControl, 0);
                }
            }
        }

        public override bool setVehicleControlItemForPLC(Boolean[] items)
        {
            ValueWrite controlItemVW = scApp.getBCFApplication().getWriteValueEvent(SCAppConstants.EQPT_OBJECT_CATE_EQPT, eqpt.VEHICLE_ID, "PLC_CONTORL_ITEMS");
            if (controlItemVW == null) return false;
            controlItemVW.setWriteValue(items);
            return ISMControl.writeDeviceBlock(scApp.getBCFApplication(), controlItemVW);
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
                //ITcpIpControl.addTcpIpReceivedHandler(bcfApp, tcpipAgentName, VHMSGIF.ID_VHCL_KISO_VERSION_REPORT.ToString(), str102_Receive);
                //ITcpIpControl.addTcpIpReceivedHandler(bcfApp, tcpipAgentName, VHMSGIF.ID_INDIVIDUAL_DATA_DOWNLOAD_REQUEST.ToString(), str162_Receive);
                //ITcpIpControl.addTcpIpReceivedHandler(bcfApp, tcpipAgentName, VHMSGIF.ID_ALARM_REPORT.ToString(), str194_Receive);

                ValueRead vr = null;
                if (bcfApp.tryGetReadValueEventstring(eqpt.EqptObjectCate, eqpt.VEHICLE_ID, "VH_STATUS_BLOCK", out vr))
                {
                    vr.afterValueChange += (_sender, _e) => VehicleStatusChangeFromPLC(_sender, _e);
                }
                if (bcfApp.tryGetReadValueEventstring(eqpt.EqptObjectCate, eqpt.VEHICLE_ID, "IF_INDEX", out vr))
                {
                    vr.afterValueChange += (_sender, _e) => VehicleCSTInterfaceIndexChanged(_sender, _e);
                }


                ITcpIpControl.addTcpIpReceivedHandler(bcfApp, tcpipAgentName, VHMSGIF.ID_TRANS_COMPLETE_REPORT.ToString(), str132_Receive);
                ITcpIpControl.addTcpIpReceivedHandler(bcfApp, tcpipAgentName, VHMSGIF.ID_VHCL_KISO_VERSION_REPORT.ToString(), str102_ReceiveProcess);
                ITcpIpControl.addTcpIpReceivedHandler(bcfApp, tcpipAgentName, VHMSGIF.ID_GUIDE_DATA_UPLOAD_REQUEST.ToString(), str162_ReceiveProcess);
                ITcpIpControl.addTcpIpReceivedHandler(bcfApp, tcpipAgentName, VHMSGIF.ID_ADDRESS_TEACH_REPORT.ToString(), str174_ReceiveProcess);
                ITcpIpControl.addTcpIpReceivedHandler(bcfApp, tcpipAgentName, VHMSGIF.ID_ALARM_REPORT.ToString(), str194_ReceiveProcess);


                ITcpIpControl.addTcpIpReceivedHandler(bcfApp, tcpipAgentName, VHMSGIF.ID_TRANS_PASS_EVENT_REPORT.ToString(), str134_136_Receive);
                ITcpIpControl.addTcpIpReceivedHandler(bcfApp, tcpipAgentName, VHMSGIF.ID_TRANS_EVENT_REPORT.ToString(), str134_136_Receive);
                ITcpIpControl.addTcpIpReceivedHandler(bcfApp, tcpipAgentName, VHMSGIF.ID_STATUS_CHANGE_REPORT.ToString(), str144_ReceiveProcess);
                ITcpIpControl.addTcpIpReceivedHandler(bcfApp, tcpipAgentName, VHMSGIF.ID_SECTION_TEACH_COMPLETE_REPORT.ToString(), str172_ReceiveProcess);


                ITcpIpControl.addTcpIpConnectedHandler(bcfApp, tcpipAgentName, Connection);
                ITcpIpControl.addTcpIpDisconnectedHandler(bcfApp, tcpipAgentName, Disconnection);

                ITcpIpControl.addTcpIpReplyTimeOutHandler(bcfApp, tcpipAgentName, ReplyTimeOutHandler);
                ITcpIpControl.addTcpIpSendErrorHandler(bcfApp, tcpipAgentName, SendErrorHandler);
                ITcpIpControl.addSendRecvStateChangeHandler(bcfApp, tcpipAgentName, SendRecvStateChangeHandler);

            }
            catch (Exception ex)
            {
                scApp.getBCFApplication().onSMAppError(0, "MapActionEQType2Secs doInit");
                logger.Error(ex, "Exection:");
            }

        }

    }
}
