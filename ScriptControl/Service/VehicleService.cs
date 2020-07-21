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
// 2019/10/08    Kevin Wei      N/A            A0.01   Bug fix:修正再透過MCS 的Command去中斷車子的命令時，
//                                                     帶入錯誤的ID問題(帶到MCS的Command而非OHTC的Command)。
// 2020/01/15    Kevin Wei      N/A            A0.02   當因為產生"tryGenerateCmd_OHTC_Details"失敗時，
//                                                     會結束掉當前命令且若是MCS命令會將其改回Queue的狀態。
// 2020/06/02    Kevin Wei      N/A            A0.03   加入當發生Table:AVEHICLE 與 Table:ACMD_OHTC狀態不匹配時，
//                                                     會再次檢查兩邊的狀態，以防發生在趕車時，無法順利趕走的問題。(因為有命令殘留)
//**********************************************************************************
using com.mirle.ibg3k0.bcf.App;
using com.mirle.ibg3k0.bcf.Common;
using com.mirle.ibg3k0.sc.App;
using com.mirle.ibg3k0.sc.Common;
using com.mirle.ibg3k0.sc.Data;
using com.mirle.ibg3k0.sc.Data.PLC_Functions;
using com.mirle.ibg3k0.sc.Data.SECS.CSOT;
using com.mirle.ibg3k0.sc.Data.VO;
using com.mirle.ibg3k0.sc.ProtocolFormat.OHTMessage;
using KingAOP;
using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using static com.mirle.ibg3k0.sc.App.SCAppConstants;

namespace com.mirle.ibg3k0.sc.Service
{
    public class VehicleService : IDynamicMetaObjectProvider
    {
        public const string DEVICE_NAME_OHx = "OHx";
        Logger logger = LogManager.GetCurrentClassLogger();
        SCApplication scApp = null;
        public VehicleService()
        {

        }
        public void Start(SCApplication app)
        {
            scApp = app;
            SubscriptionPositionChangeEvent();

            List<AVEHICLE> vhs = scApp.getEQObjCacheManager().getAllVehicle();

            foreach (var vh in vhs)
            {
                vh.addEventHandler(nameof(VehicleService), nameof(vh.isTcpIpConnect), PublishVhInfo);
                vh.addEventHandler(nameof(VehicleService), vh.VhPositionChangeEvent, PublishVhInfo);
                vh.addEventHandler(nameof(VehicleService), vh.VhExcuteCMDStatusChangeEvent, PublishVhInfo);
                vh.addEventHandler(nameof(VehicleService), vh.VhStatusChangeEvent, PublishVhInfo);


                vh.LocationChange += Vh_LocationChange;
                vh.SegmentChange += Vh_SegementChange;
                vh.AssignCommandFailOverTimes += Vh_AssignCommandFailOverTimes;
                vh.StatusRequestFailOverTimes += Vh_StatusRequestFailOverTimes;
                vh.LongTimeNoCommuncation += Vh_LongTimeNoCommuncation;
                vh.LongTimeInaction += Vh_LongTimeInaction;
                vh.TimerActionStart();
            }
        }


        private void Vh_AssignCommandFailOverTimes(object sender, int failTimes)
        {
            AVEHICLE vh = (sender as AVEHICLE);
            if (vh.MODE_STATUS == VHModeStatus.AutoRemote)
            {
                VehicleAutoModeCahnge(vh.VEHICLE_ID, VHModeStatus.AutoLocal);
                string message = $"vh:{vh.VEHICLE_ID}, assign command fail times:{failTimes}, change to auto local mode";
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Warn, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: message,
                   VehicleID: vh.VEHICLE_ID,
                   CarrierID: vh.CST_ID);
                BCFApplication.onWarningMsg(message);
            }
        }

        private void Vh_LongTimeNoCommuncation(object sender, EventArgs e)
        {
            AVEHICLE vh = sender as AVEHICLE;
            if (vh == null) return;
            //當發生很久沒有通訊的時候，就會發送143去進行狀態的詢問，確保Control還與Vehicle連線著
            bool is_success = VehicleStatusRequest(vh.VEHICLE_ID);
            //如果連續三次 都沒有得到回覆時，就將Port關閉在重新打開
            if (!is_success)
            {
                vh.StatusRequestFailTimes++;
            }
            else
            {
                vh.StatusRequestFailTimes = 0;
            }
        }

        private void Vh_LongTimeInaction(object sender, string cmdID)
        {
            AVEHICLE vh = sender as AVEHICLE;
            if (vh == null) return;
            try
            {
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: $"Process vehicle long time inaction, cmd id:{cmdID}",
                   VehicleID: vh.VEHICLE_ID,
                   CarrierID: vh.CST_ID);
                vh.Stop();
                //上報Alamr Rerport給MCS
                scApp.LineService.ProcessAlarmReport(
                    vh.NODE_ID, vh.VEHICLE_ID, vh.Real_ID, "",
                    SCAppConstants.SystemAlarmCode.OHT_Issue.OHTLongInaction,
                    ProtocolFormat.OHTMessage.ErrorStatus.ErrSet);
            }
            catch (Exception ex)
            {
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Warn, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: ex,
                   VehicleID: vh.VEHICLE_ID,
                   CarrierID: vh.CST_ID);
            }
        }

        private void Vh_StatusRequestFailOverTimes(object sender, int e)
        {
            try
            {
                AVEHICLE vh = sender as AVEHICLE;
                vh.StatusRequestFailTimes = 0;
                //1.當Status要求失敗超過3次時，要將對應的Port關閉再開啟。
                //var endPoint = vh.getIPEndPoint(scApp.getBCFApplication());
                int port_num = vh.getPortNum(scApp.getBCFApplication());
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: $"Over {AVEHICLE.MAX_STATUS_REQUEST_FAIL_TIMES} times request status fail, begin restart tcpip server port:{port_num}...",
                   VehicleID: vh.VEHICLE_ID,
                   CarrierID: vh.CST_ID);

                stopVehicleTcpIpServer(vh);
                SpinWait.SpinUntil(() => false, 2000);
                startVehicleTcpIpServer(vh);
            }
            catch (Exception ex)
            {
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Warn, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: ex);
            }
        }


        public bool stopVehicleTcpIpServer(string vhID)
        {
            AVEHICLE vh = scApp.VehicleBLL.cache.getVhByID(vhID);
            return stopVehicleTcpIpServer(vh);
        }
        private bool stopVehicleTcpIpServer(AVEHICLE vh)
        {
            if (!vh.IsTcpIpListening(scApp.getBCFApplication()))
            {
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Warn, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: $"vh:{vh.VEHICLE_ID} of tcp/ip server already stopped!,IsTcpIpListening:{vh.IsTcpIpListening(scApp.getBCFApplication())}",
                   VehicleID: vh.VEHICLE_ID,
                   CarrierID: vh.CST_ID);
                return false;
            }

            int port_num = vh.getPortNum(scApp.getBCFApplication());
            LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
               Data: $"Stop vh:{vh.VEHICLE_ID} of tcp/ip server, port num:{port_num}",
               VehicleID: vh.VEHICLE_ID,
               CarrierID: vh.CST_ID);
            scApp.stopTcpIpServer(port_num);
            LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
               Data: $"Stop vh:{vh.VEHICLE_ID} of tcp/ip server finish, IsTcpIpListening:{vh.IsTcpIpListening(scApp.getBCFApplication())}",
               VehicleID: vh.VEHICLE_ID,
               CarrierID: vh.CST_ID);
            return true;
        }

        public bool startVehicleTcpIpServer(string vhID)
        {
            AVEHICLE vh = scApp.VehicleBLL.cache.getVhByID(vhID);
            return startVehicleTcpIpServer(vh);
        }

        private bool startVehicleTcpIpServer(AVEHICLE vh)
        {
            if (vh.IsTcpIpListening(scApp.getBCFApplication()))
            {
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Warn, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: $"vh:{vh.VEHICLE_ID} of tcp/ip server already listening!,IsTcpIpListening:{vh.IsTcpIpListening(scApp.getBCFApplication())}",
                   VehicleID: vh.VEHICLE_ID,
                   CarrierID: vh.CST_ID);
                return false;
            }

            int port_num = vh.getPortNum(scApp.getBCFApplication());
            LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
               Data: $"Start vh:{vh.VEHICLE_ID} of tcp/ip server, port num:{port_num}",
               VehicleID: vh.VEHICLE_ID,
               CarrierID: vh.CST_ID);
            scApp.startTcpIpServerListen(port_num);
            LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
               Data: $"Start vh:{vh.VEHICLE_ID} of tcp/ip server finish, IsTcpIpListening:{vh.IsTcpIpListening(scApp.getBCFApplication())}",
               VehicleID: vh.VEHICLE_ID,
               CarrierID: vh.CST_ID);
            return true;
        }



        private void Vh_LocationChange(object sender, LocationChangeEventArgs e)
        {
            AVEHICLE vh = sender as AVEHICLE;
            ASECTION leave_section = scApp.SectionBLL.cache.GetSection(e.LeaveSection);
            ASECTION entry_section = scApp.SectionBLL.cache.GetSection(e.EntrySection);
            leave_section?.Leave(vh.VEHICLE_ID);
            entry_section?.Entry(vh.VEHICLE_ID);

            if (vh.WillPassSectionID != null)
            {
                vh.WillPassSectionID.Remove(SCUtility.Trim(leave_section.SEC_ID, true));
            }

            //if (leave_section != null && entry_section != null)
            //{
            //    if (SCUtility.isMatche(leave_section.TO_ADR_ID, entry_section.FROM_ADR_ID))
            //    {
            //        string cross_address = leave_section.TO_ADR_ID;
            //        var release_result = doBlockRelease(vh, cross_address, false);
            //        if (release_result.hasRelease)
            //        {
            //            LogHelper.Log(logger: logger, LogLevel: LogLevel.Warn, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
            //               Data: $"Process block force release by ohxc, release address id:{cross_address}, " +
            //                     $"release entry section id:{release_result.releaseBlockMaster.ENTRY_SEC_ID}",
            //               VehicleID: vh.VEHICLE_ID,
            //               CarrierID: vh.CST_ID);
            //        }
            //    }
            //}

            if (scApp.getEQObjCacheManager().getLine().ServiceMode == AppServiceMode.Active)
                scApp.VehicleBLL.NetworkQualityTest(vh.VEHICLE_ID, e.EntrySection, vh.CUR_ADR_ID, 0);
        }

        private void Vh_SegementChange(object sender, SegmentChangeEventArgs e)
        {
            AVEHICLE vh = sender as AVEHICLE;
            ASEGMENT leave_segment = scApp.SegmentBLL.cache.GetSegment(e.LeaveSegment);
            ASEGMENT entry_segment = scApp.SegmentBLL.cache.GetSegment(e.EntrySegment);
            leave_segment?.Leave(vh);
            entry_segment?.Entry(vh, scApp.SectionBLL, leave_segment == null);


        }

        private void PublishVhInfo(object sender, PropertyChangedEventArgs e)
        {
            //Task.Run(() =>
            //{
            try
            {
                // AVEHICLE vh = sender as AVEHICLE;
                string vh_id = e.PropertyValue as string;
                AVEHICLE vh = scApp.VehicleBLL.getVehicleByID(vh_id);
                if (sender == null) return;
                byte[] vh_Serialize = BLL.VehicleBLL.Convert2GPB_VehicleInfo(vh);
                RecoderVehicleObjInfoLog(vh_id, vh_Serialize);
                //var vh_Serialize = ZeroFormatter.ZeroFormatterSerializer.Serialize(vh);
                //RecoderTESTLog(vh_Serialize, target_log_TEST_ZeroFormatter);
                //Task.Run(() => scApp.FlexsimCommandDao.setVhStatusToFlexsimDB(vh_id, vh.CUR_ADR_ID, vh.ACC_SEC_DIST, vh.VhRecentTranEvent, vh.CST_ID,
                //                                               vh.MODE_STATUS, vh.ACT_STATUS, vh.OBS_PAUSE, vh.BLOCK_PAUSE, vh.CMD_PAUSE,
                //                                               vh.HID_PAUSE, vh.ERROR, vh.EARTHQUAKE_PAUSE, vh.SAFETY_DOOR_PAUSE));

                scApp.getNatsManager().PublishAsync
                    (string.Format(SCAppConstants.NATS_SUBJECT_VH_INFO_0, vh.VEHICLE_ID.Trim()), vh_Serialize);

                scApp.getRedisCacheManager().ListSetByIndexAsync
                    (SCAppConstants.REDIS_LIST_KEY_VEHICLES, vh.VEHICLE_ID, vh.ToString());
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception:");
            }
            //});
        }

        private static void RecoderVehicleObjInfoLog(string vh_id, byte[] arrayByte)
        {
            string compressStr = SCUtility.CompressArrayByte(arrayByte);
            dynamic logEntry = new JObject();
            logEntry.RPT_TIME = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture);
            logEntry.OBJECT_ID = vh_id;
            logEntry.RAWDATA = compressStr;
            logEntry.Index = "ObjectHistoricalInfo";
            var json = logEntry.ToString(Newtonsoft.Json.Formatting.None);
            json = json.Replace("RPT_TIME", "@timestamp");
            LogManager.GetLogger("ObjectHistoricalInfo").Info(json);
        }

        public static string CompressArrayByte(byte[] arrayByte)
        {
            MemoryStream ms = new MemoryStream();
            GZipStream compressedzipStream = new GZipStream(ms, CompressionMode.Compress, true);
            compressedzipStream.Write(arrayByte, 0, arrayByte.Length);
            compressedzipStream.Close();
            string compressStr = (string)(Convert.ToBase64String(ms.ToArray()));
            return compressStr;
        }

        public void SubscriptionPositionChangeEvent()
        {
            //scApp.VehicleBLL.loadAllAndProcPositionReportFromRedis();
            scApp.getRedisCacheManager().SubscriptionEvent($"{SCAppConstants.REDIS_KEY_WORD_POSITION_REPORT}_*", scApp.VehicleBLL.VehiclePositionChangeHandler);
        }
        public void UnsubscribePositionChangeEvent()
        {
            scApp.getRedisCacheManager().UnsubscribeEvent($"{SCAppConstants.REDIS_KEY_WORD_POSITION_REPORT}_*", scApp.VehicleBLL.VehiclePositionChangeHandler);
        }

        #region Send Message To Vehicle
        #region Tcp/Ip
        public bool HostBasicVersionReport(string vh_id)
        {
            bool isSuccess = false;
            AVEHICLE vh = scApp.getEQObjCacheManager().getVehicletByVHID(vh_id);
            DateTime crtTime = DateTime.Now;
            ID_101_HOST_BASIC_INFO_VERSION_RESPONSE receive_gpp = null;
            ID_1_HOST_BASIC_INFO_VERSION_REP sned_gpp = new ID_1_HOST_BASIC_INFO_VERSION_REP()
            {
                DataDateTimeYear = "2018",
                DataDateTimeMonth = "10",
                DataDateTimeDay = "25",
                DataDateTimeHour = "15",
                DataDateTimeMinute = "22",
                DataDateTimeSecond = "50",
                CurrentTimeYear = crtTime.Year.ToString(),
                CurrentTimeMonth = crtTime.Month.ToString(),
                CurrentTimeDay = crtTime.Day.ToString(),
                CurrentTimeHour = crtTime.Hour.ToString(),
                CurrentTimeMinute = crtTime.Minute.ToString(),
                CurrentTimeSecond = crtTime.Second.ToString()
            };
            isSuccess = vh.send_Str1(sned_gpp, out receive_gpp);
            isSuccess = isSuccess && receive_gpp.ReplyCode == 0;
            return isSuccess;
        }
        public bool BasicInfoReport(string vh_id)
        {
            bool isSuccess = false;
            AVEHICLE vh = scApp.getEQObjCacheManager().getVehicletByVHID(vh_id);
            DateTime crtTime = DateTime.Now;
            ID_111_BASIC_INFO_RESPONSE receive_gpp = null;
            int travel_base_data_count = 1;
            int section_data_count = 0;
            int address_data_coune = 0;
            int scale_base_data_count = 1;
            int control_data_count = 1;
            int guide_base_data_count = 1;
            section_data_count = scApp.DataSyncBLL.getCount_ReleaseVSections();
            address_data_coune = scApp.MapBLL.getCount_AddressCount();
            ID_11_BASIC_INFO_REP sned_gpp = new ID_11_BASIC_INFO_REP()
            {
                TravelBasicDataCount = travel_base_data_count,
                SectionDataCount = section_data_count,
                AddressDataCount = address_data_coune,
                ScaleDataCount = scale_base_data_count,
                ContrlDataCount = control_data_count,
                GuideDataCount = guide_base_data_count
            };
            isSuccess = vh.sned_S11(sned_gpp, out receive_gpp);
            isSuccess = isSuccess && receive_gpp.ReplyCode == 0;
            return isSuccess;
        }
        public bool TavellingDataReport(string vh_id)
        {
            bool isSuccess = false;
            AVEHICLE vh = scApp.getEQObjCacheManager().getVehicletByVHID(vh_id);
            DateTime crtTime = DateTime.Now;
            AVEHICLE_CONTROL_100 data = scApp.DataSyncBLL.getReleaseVehicleControlData_100(vh_id);

            ID_113_TAVELLING_DATA_RESPONSE receive_gpp = null;
            ID_13_TAVELLING_DATA_REP sned_gpp = new ID_13_TAVELLING_DATA_REP()
            {
                Resolution = (UInt32)data.TRAVEL_RESOLUTION,
                StartStopSpd = (UInt32)data.TRAVEL_START_STOP_SPEED,
                MaxSpeed = (UInt32)data.TRAVEL_MAX_SPD,
                AccelTime = (UInt32)data.TRAVEL_ACCEL_DECCEL_TIME,
                SCurveRate = (UInt16)data.TRAVEL_S_CURVE_RATE,
                OriginDir = (UInt16)data.TRAVEL_HOME_DIR,
                OriginSpd = (UInt32)data.TRAVEL_HOME_SPD,
                BeaemSpd = (UInt32)data.TRAVEL_KEEP_DIS_SPD,
                ManualHSpd = (UInt32)data.TRAVEL_MANUAL_HIGH_SPD,
                ManualLSpd = (UInt32)data.TRAVEL_MANUAL_LOW_SPD,
                TeachingSpd = (UInt32)data.TRAVEL_TEACHING_SPD,
                RotateDir = (UInt16)data.TRAVEL_TRAVEL_DIR,
                EncoderPole = (UInt16)data.TRAVEL_ENCODER_POLARITY,
                PositionCompensation = (UInt16)data.TRAVEL_F_DIR_LIMIT, //TODO 要填入正確的資料
                //FLimit = (UInt16)data.TRAVEL_F_DIR_LIMIT, //TODO 要填入正確的資料
                //RLimit = (UInt16)data.TRAVEL_R_DIR_LIMIT,
                KeepDistFar = (UInt32)data.TRAVEL_OBS_DETECT_LONG,
                KeepDistNear = (UInt32)data.TRAVEL_OBS_DETECT_SHORT,
            };
            isSuccess = vh.sned_S13(sned_gpp, out receive_gpp);
            isSuccess = isSuccess && receive_gpp.ReplyCode == 0;
            return isSuccess;
        }
        public bool SectionDataReport(string vh_id)
        {
            bool isSuccess = false;
            AVEHICLE vh = scApp.getEQObjCacheManager().getVehicletByVHID(vh_id);
            DateTime crtTime = DateTime.Now;
            List<VSECTION_100> vSecs = scApp.DataSyncBLL.loadReleaseVSections();

            ID_15_SECTION_DATA_REP send_gpp = new ID_15_SECTION_DATA_REP();
            ID_115_SECTION_DATA_RESPONSE receive_gpp = null;
            foreach (VSECTION_100 vSec in vSecs)
            {
                var secInfo = new ID_15_SECTION_DATA_REP.Types.Section()
                {
                    DriveDir = (UInt16)vSec.DIRC_DRIV,
                    GuideDir = (UInt16)vSec.DIRC_GUID,
                    AeraSecsor = (UInt16)(UInt16)(vSec.AREA_SECSOR ?? 0),
                    SectionID = vSec.SEC_ID,
                    FromAddr = vSec.FROM_ADR_ID,
                    ToAddr = vSec.TO_ADR_ID,
                    ControlTable = convertvSec2ControlTable(vSec),
                    Speed = (UInt32)vSec.SEC_SPD,
                    Distance = (UInt32)vSec.SEC_DIS,
                    ChangeAreaSensor1 = (UInt16)vSec.CHG_AREA_SECSOR_1,
                    ChangeGuideDir1 = (UInt16)vSec.CDOG_1,
                    ChangeSegNum1 = vSec.CHG_SEG_NUM_1,

                    ChangeAreaSensor2 = (UInt16)vSec.CHG_AREA_SECSOR_2,
                    ChangeGuideDir2 = (UInt16)vSec.CDOG_2,
                    ChangeSegNum2 = vSec.CHG_SEG_NUM_2,
                    AtSegment = vSec.SEG_NUM
                };
                send_gpp.Sections.Add(secInfo);

            }
            isSuccess = vh.sned_S15(send_gpp, out receive_gpp);
            // isSuccess = isSuccess && receive_gpp.ReplyCode == 0;
            return isSuccess;
        }
        private UInt16 convertvSec2ControlTable(VSECTION_100 vSec)
        {
            System.Collections.BitArray bitArray = new System.Collections.BitArray(16);
            bitArray[0] = SCUtility.int2Bool(vSec.PRE_BLO_REQ);
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

        public bool AddressDataReport(string vh_id)
        {
            bool isSuccess = false;
            AVEHICLE vh = scApp.getEQObjCacheManager().getVehicletByVHID(vh_id);
            //List<AADDRESS_DATA> adrs = scApp.DataSyncBLL.loadReleaseADDRESS_DATAs(vh_id);
            List<AADDRESS_DATA> adrs = scApp.DataSyncBLL.loadReleaseADDRESS_DATAs(sc.BLL.DataSyncBLL.COMMON_ADDRESS_DATA_INDEX);
            List<string> hid_leave_adr = scApp.HIDBLL.loadAllHIDLeaveAdr();
            string rtnMsg = string.Empty;
            ID_17_ADDRESS_DATA_REP send_gpp = new ID_17_ADDRESS_DATA_REP();
            ID_117_ADDRESS_DATA_RESPONSE receive_gpp = null;
            foreach (AADDRESS_DATA adr in adrs)
            {
                var block_master = scApp.MapBLL.loadBZMByAdrID(adr.ADR_ID.Trim());
                var adrInfo = new ID_17_ADDRESS_DATA_REP.Types.Address()
                {
                    Addr = adr.ADR_ID,
                    Resolution = adr.RESOLUTION,
                    Loaction = adr.LOACTION,
                    BlockRelease = (block_master != null && block_master.Count > 0) ? 1 : 0,
                    HIDRelease = hid_leave_adr.Contains(adr.ADR_ID.Trim()) ? 1 : 0
                };
                send_gpp.Addresss.Add(adrInfo);
            }
            isSuccess = vh.sned_S17(send_gpp, out receive_gpp);
            // isSuccess = isSuccess && receive_gpp.ReplyCode == 0;
            return isSuccess;
        }

        public bool ScaleDataReport(string vh_id)
        {
            bool isSuccess = false;
            AVEHICLE vh = scApp.getEQObjCacheManager().getVehicletByVHID(vh_id);
            SCALE_BASE_DATA data = scApp.DataSyncBLL.getReleaseSCALE_BASE_DATA();

            ID_119_SCALE_DATA_RESPONSE receive_gpp = null;
            ID_19_SCALE_DATA_REP sned_gpp = new ID_19_SCALE_DATA_REP()
            {
                Resolution = (UInt32)data.RESOLUTION,
                InposArea = (UInt32)data.INPOSITION_AREA,
                InposStability = (UInt32)data.INPOSITION_STABLE_TIME,
                ScalePulse = (UInt32)data.TOTAL_SCALE_PULSE,
                ScaleOffset = (UInt32)data.SCALE_OFFSET,
                ScaleReset = (UInt32)data.SCALE_RESE_DIST,
                ReadDir = (UInt16)data.READ_DIR

            };
            isSuccess = vh.sned_S19(sned_gpp, out receive_gpp);
            isSuccess = isSuccess && receive_gpp.ReplyCode == 0;
            return isSuccess;
        }

        public bool ControlDataReport(string vh_id)
        {
            bool isSuccess = false;
            AVEHICLE vh = scApp.getEQObjCacheManager().getVehicletByVHID(vh_id);

            CONTROL_DATA data = scApp.DataSyncBLL.getReleaseCONTROL_DATA();
            string rtnMsg = string.Empty;
            ID_121_CONTROL_DATA_RESPONSE receive_gpp;
            ID_21_CONTROL_DATA_REP sned_gpp = new ID_21_CONTROL_DATA_REP()
            {
                TimeoutT1 = (UInt32)data.T1,
                TimeoutT2 = (UInt32)data.T2,
                TimeoutT3 = (UInt32)data.T3,
                TimeoutT4 = (UInt32)data.T4,
                TimeoutT5 = (UInt32)data.T5,
                TimeoutT6 = (UInt32)data.T6,
                TimeoutT7 = (UInt32)data.T7,
                TimeoutT8 = (UInt32)data.T8,
                TimeoutBlock = (UInt32)data.BLOCK_REQ_TIME_OUT
            };
            isSuccess = vh.sned_S21(sned_gpp, out receive_gpp);
            isSuccess = isSuccess && receive_gpp.ReplyCode == 0;
            return isSuccess;
        }

        public bool GuideDataReport(string vh_id)
        {
            bool isSuccess = false;
            AVEHICLE vh = scApp.getEQObjCacheManager().getVehicletByVHID(vh_id);
            AVEHICLE_CONTROL_100 data = scApp.DataSyncBLL.getReleaseVehicleControlData_100(vh_id);
            ID_123_GUIDE_DATA_RESPONSE receive_gpp;
            ID_23_GUIDE_DATA_REP sned_gpp = new ID_23_GUIDE_DATA_REP()
            {
                StartStopSpd = (UInt32)data.GUIDE_START_STOP_SPEED,
                MaxSpeed = (UInt32)data.GUIDE_MAX_SPD,
                AccelTime = (UInt32)data.GUIDE_ACCEL_DECCEL_TIME,
                SCurveRate = (UInt16)data.GUIDE_S_CURVE_RATE,
                NormalSpd = (UInt32)data.GUIDE_RUN_SPD,
                ManualHSpd = (UInt32)data.GUIDE_MANUAL_HIGH_SPD,
                ManualLSpd = (UInt32)data.GUIDE_MANUAL_LOW_SPD,
                LFLockPos = (UInt32)data.GUIDE_LF_LOCK_POSITION,
                LBLockPos = (UInt32)data.GUIDE_LB_LOCK_POSITION,
                RFLockPos = (UInt32)data.GUIDE_RF_LOCK_POSITION,
                RBLockPos = (UInt32)data.GUIDE_RB_LOCK_POSITION,
                ChangeStabilityTime = (UInt32)data.GUIDE_CHG_STABLE_TIME,
            };
            isSuccess = vh.sned_S23(sned_gpp, out receive_gpp);
            isSuccess = isSuccess && receive_gpp.ReplyCode == 0;
            return isSuccess;
        }

        public bool doDataSysc(string vh_id)
        {
            bool isSyscCmp = false;
            DateTime ohtDataVersion = new DateTime(2017, 03, 27, 10, 30, 00);
            if (BasicInfoReport(vh_id) &&
                TavellingDataReport(vh_id) &&
                SectionDataReport(vh_id) &&
                AddressDataReport(vh_id) &&
                ScaleDataReport(vh_id) &&
                ControlDataReport(vh_id) &&
                GuideDataReport(vh_id))
            {
                isSyscCmp = true;
            }
            return isSyscCmp;
        }

        //public bool CSTIDRenameRequest(string vh_id, string new_cst_id)
        //{


        //}

        public bool IndividualUploadRequest(string vh_id)
        {
            bool isSuccess = false;
            AVEHICLE vh = scApp.getEQObjCacheManager().getVehicletByVHID(vh_id);
            ID_161_INDIVIDUAL_UPLOAD_RESPONSE receive_gpp;
            ID_61_INDIVIDUAL_UPLOAD_REQ sned_gpp = new ID_61_INDIVIDUAL_UPLOAD_REQ()
            {

            };
            isSuccess = vh.sned_S61(sned_gpp, out receive_gpp);
            //TODO Set info 2 DB
            if (isSuccess)
            {

            }
            return isSuccess;
        }

        public bool IndividualChangeRequest(string vh_id)
        {
            bool isSuccess = false;
            AVEHICLE vh = scApp.getEQObjCacheManager().getVehicletByVHID(vh_id);
            ID_163_INDIVIDUAL_CHANGE_RESPONSE receive_gpp;
            ID_63_INDIVIDUAL_CHANGE_REQ sned_gpp = new ID_63_INDIVIDUAL_CHANGE_REQ()
            {
                OffsetGuideFL = 1,
                OffsetGuideRL = 2,
                OffsetGuideFR = 3,
                OffsetGuideRR = 4
            };
            isSuccess = vh.sned_S63(sned_gpp, out receive_gpp);
            return isSuccess;
        }

        /// <summary>
        /// 與Vehicle進行資料同步。(通常使用剛與Vehicle連線時)
        /// </summary>
        /// <param name="vh_id"></param>
        public void VehicleInfoSynchronize(string vh_id)
        {
            /*與Vehicle進行狀態同步*/
            VehicleStatusRequest(vh_id, true);
            /*要求Vehicle進行Alarm的Reset，如果成功後會將OHxC上針對該Vh的Alarm清除*/
            if (AlarmResetRequest(vh_id))
            {
                //scApp.AlarmBLL.resetAllAlarmReport(vh_id);
                //scApp.AlarmBLL.resetAllAlarmReport2Redis(vh_id);
            }
            AVEHICLE vh = scApp.getEQObjCacheManager().getVehicletByVHID(vh_id);
            //if (vh.MODE_STATUS == VHModeStatus.Manual &&
            //    !SCUtility.isEmpty(vh.CUR_ADR_ID) &&
            //    !SCUtility.isMatche(vh.CUR_ADR_ID, MTLService.MTL_ADDRESS))
            var check_is_in_maintain_device = scApp.EquipmentBLL.cache.IsInMaintainDevice(vh.CUR_ADR_ID);
            if (vh.MODE_STATUS == VHModeStatus.Manual &&
                !check_is_in_maintain_device.isIn)
            {
                ModeChangeRequest(vh_id, OperatingVHMode.OperatingAuto);
                if (SpinWait.SpinUntil(() => vh.MODE_STATUS == VHModeStatus.AutoRemote, 5000))
                {
                    ASEGMENT vh_current_seg_obj = scApp.SegmentBLL.cache.GetSegment(vh.CUR_SEG_ID);
                    vh_current_seg_obj?.Entry(vh, scApp.SectionBLL, true);
                }
            }
        }

        public bool VehicleStatusRequest(string vh_id, bool isSync = false)
        {
            bool isSuccess = false;
            string reason = string.Empty;
            AVEHICLE vh = scApp.getEQObjCacheManager().getVehicletByVHID(vh_id);
            ID_143_STATUS_RESPONSE receive_gpp;
            ID_43_STATUS_REQUEST send_gpp = new ID_43_STATUS_REQUEST()
            {
                SystemTime = DateTime.Now.ToString(SCAppConstants.TimestampFormat_16)
            };
            SCUtility.RecodeReportInfo(vh.VEHICLE_ID, 0, send_gpp);
            isSuccess = vh.send_S43(send_gpp, out receive_gpp);
            SCUtility.RecodeReportInfo(vh.VEHICLE_ID, 0, receive_gpp, isSuccess.ToString());
            if (isSync && isSuccess)
            {
                string current_adr_id = receive_gpp.CurrentAdrID;
                VHModeStatus modeStat = DecideVhModeStatus(vh.VEHICLE_ID, current_adr_id, receive_gpp.ModeStatus);
                VHActionStatus actionStat = receive_gpp.ActionStatus;
                VhPowerStatus powerStat = receive_gpp.PowerStatus;
                string cstID = receive_gpp.CSTID;
                VhStopSingle obstacleStat = receive_gpp.ObstacleStatus;
                VhStopSingle blockingStat = receive_gpp.BlockingStatus;
                VhStopSingle pauseStat = receive_gpp.PauseStatus;
                VhStopSingle hidStat = receive_gpp.HIDStatus;
                VhStopSingle errorStat = receive_gpp.ErrorStatus;
                VhLoadCSTStatus loadCSTStatus = receive_gpp.HasCST;
                //VhGuideStatus leftGuideStat = recive_str.LeftGuideLockStatus;
                //VhGuideStatus rightGuideStat = recive_str.RightGuideLockStatus;


                int obstacleDIST = receive_gpp.ObstDistance;
                string obstacleVhID = receive_gpp.ObstVehicleID;


                scApp.VehicleBLL.setAndPublishPositionReportInfo2Redis(vh.VEHICLE_ID, receive_gpp);
                scApp.VehicleBLL.getAndProcPositionReportFromRedis(vh.VEHICLE_ID);
                if (!scApp.VehicleBLL.doUpdateVehicleStatus(vh, cstID,
                                       modeStat, actionStat,
                                       blockingStat, pauseStat, obstacleStat, hidStat, errorStat, loadCSTStatus))
                {
                    isSuccess = false;
                }
            }
            vhCommandExcuteStatusCheck(vh.VEHICLE_ID);
            return isSuccess;
        }

        /// <summary>
        /// 如果在車子已有回報是無命令狀態下，但在OHXC的AVEHICLE欄位"CMD_OHTC"卻還有命令時，
        /// 則需要在檢查在ACMD_OHTC是否已無命令，如果也沒有的話，則要將AVEHICLE改成正確的
        /// </summary>
        /// <param name="vh"></param>
        public void vhCommandExcuteStatusCheck(string vhID)
        {
            try
            {
                AVEHICLE vh = scApp.VehicleBLL.cache.getVhByID(vhID);
                VHActionStatus actionStat = vh.ACT_STATUS;
                bool has_ohtc_cmd = !SCUtility.isEmpty(vh.OHTC_CMD);
                if (has_ohtc_cmd &&
                    actionStat == VHActionStatus.NoCommand)
                {
                    bool has_excuted_cmd_in_cmd_table = scApp.CMDBLL.isCMD_OHTCExcutedByVh(vh.VEHICLE_ID);
                    if (has_excuted_cmd_in_cmd_table)
                    {
                        LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                           Data: $"[AVEHICLE - Act Status:{actionStat}] with [AVEHICLE - OHTC_CMD:{SCUtility.Trim(vh.OHTC_CMD, true)}] status mismatch," +
                                 $"but in Table: CMD_OHTC has cmd excuted, pass this one check",
                           VehicleID: vh?.VEHICLE_ID,
                           CarrierID: vh?.CST_ID);
                        //Not thing...
                    }
                    else
                    {
                        LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                           Data: $"[AVEHICLE - Act Status:{actionStat}] with [AVEHICLE - OHTC_CMD:{SCUtility.Trim(vh.OHTC_CMD, true)}] status mismatch," +
                                 $"force update vehicle excute status",
                           VehicleID: vh?.VEHICLE_ID,
                           CarrierID: vh?.CST_ID);
                        scApp.VehicleBLL.updateVehicleExcuteCMD(vh.VEHICLE_ID, string.Empty, string.Empty);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception:");
            }
        }

        public bool ModeChangeRequest(string vh_id, OperatingVHMode mode)
        {
            bool isSuccess = false;
            AVEHICLE vh = scApp.getEQObjCacheManager().getVehicletByVHID(vh_id);
            ID_141_MODE_CHANGE_RESPONSE receive_gpp;
            ID_41_MODE_CHANGE_REQ sned_gpp = new ID_41_MODE_CHANGE_REQ()
            {
                OperatingVHMode = mode
            };
            SCUtility.RecodeReportInfo(vh_id, 0, sned_gpp);
            isSuccess = vh.sned_S41(sned_gpp, out receive_gpp);
            SCUtility.RecodeReportInfo(vh_id, 0, receive_gpp, isSuccess.ToString());
            return isSuccess;
        }

        public bool PowerOperatorRequest(string vh_id, OperatingPowerMode mode)
        {
            bool isSuccess = false;
            AVEHICLE vh = scApp.getEQObjCacheManager().getVehicletByVHID(vh_id);
            ID_145_POWER_OPE_RESPONSE receive_gpp;
            ID_45_POWER_OPE_REQ sned_gpp = new ID_45_POWER_OPE_REQ()
            {
                OperatingPowerMode = mode
            };
            isSuccess = vh.sned_S45(sned_gpp, out receive_gpp);
            return isSuccess;
        }

        public bool AlarmResetRequest(string vh_id)
        {
            bool isSuccess = false;
            AVEHICLE vh = scApp.getEQObjCacheManager().getVehicletByVHID(vh_id);
            ID_191_ALARM_RESET_RESPONSE receive_gpp;
            ID_91_ALARM_RESET_REQUEST sned_gpp = new ID_91_ALARM_RESET_REQUEST()
            {

            };
            isSuccess = vh.sned_S91(sned_gpp, out receive_gpp);
            if (isSuccess)
            {
                isSuccess = receive_gpp?.ReplyCode == 0;
            }
            return isSuccess;
        }


        public bool PauseRequest(string vh_id, PauseEvent pause_event, OHxCPauseType ohxc_pause_type)
        {
            bool isSuccess = false;
            AVEHICLE vh = scApp.getEQObjCacheManager().getVehicletByVHID(vh_id);
            PauseType pauseType = convert2PauseType(ohxc_pause_type);
            ID_139_PAUSE_RESPONSE receive_gpp;
            ID_39_PAUSE_REQUEST send_gpp = new ID_39_PAUSE_REQUEST()
            {
                PauseType = pauseType,
                EventType = pause_event
            };
            SCUtility.RecodeReportInfo(vh.VEHICLE_ID, 0, send_gpp);
            isSuccess = vh.sned_Str39(send_gpp, out receive_gpp);
            SCUtility.RecodeReportInfo(vh.VEHICLE_ID, 0, receive_gpp, isSuccess.ToString());
            return isSuccess;
        }
        public bool OHxCPauseRequest(string vh_id, PauseEvent pause_event, OHxCPauseType ohxc_pause_type)
        {
            bool isSuccess = false;
            AVEHICLE vh = scApp.getEQObjCacheManager().getVehicletByVHID(vh_id);
            using (TransactionScope tx = SCUtility.getTransactionScope())
            {
                using (DBConnection_EF con = DBConnection_EF.GetUContext())
                {

                    switch (ohxc_pause_type)
                    {
                        case OHxCPauseType.Earthquake:
                            scApp.VehicleBLL.updateVehiclePauseStatus
                                (vh_id, earthquake_pause: pause_event == PauseEvent.Pause);
                            break;
                        case OHxCPauseType.Obstacle:
                            scApp.VehicleBLL.updateVehiclePauseStatus
                                (vh_id, obstruct_pause: pause_event == PauseEvent.Pause);
                            break;
                        case OHxCPauseType.Safty:
                            scApp.VehicleBLL.updateVehiclePauseStatus
                                (vh_id, safyte_pause: pause_event == PauseEvent.Pause);
                            break;
                    }
                    PauseType pauseType = convert2PauseType(ohxc_pause_type);
                    ID_139_PAUSE_RESPONSE receive_gpp;
                    ID_39_PAUSE_REQUEST send_gpp = new ID_39_PAUSE_REQUEST()
                    {
                        PauseType = pauseType,
                        EventType = pause_event
                    };
                    SCUtility.RecodeReportInfo(vh.VEHICLE_ID, 0, send_gpp);
                    isSuccess = vh.sned_Str39(send_gpp, out receive_gpp);
                    SCUtility.RecodeReportInfo(vh.VEHICLE_ID, 0, receive_gpp, isSuccess.ToString());

                    if (isSuccess)
                    {
                        tx.Complete();
                        vh.NotifyVhStatusChange();
                    }
                }
            }
            return isSuccess;
        }



        private PauseType convert2PauseType(OHxCPauseType ohxc_pauseType)
        {
            switch (ohxc_pauseType)
            {
                case OHxCPauseType.Normal:
                case OHxCPauseType.Obstacle:
                    return PauseType.OhxC;
                case OHxCPauseType.Block:
                    return PauseType.Block;
                case OHxCPauseType.Hid:
                    return PauseType.Hid;
                case OHxCPauseType.Earthquake:
                    return PauseType.EarthQuake;
                //case OHxCPauseType.Obstruct:
                //    return PauseType.;
                case OHxCPauseType.Safty:
                    return PauseType.Safety;
                case OHxCPauseType.ManualBlock:
                    return PauseType.ManualBlock;
                case OHxCPauseType.ManualHID:
                    return PauseType.ManualHid;
                case OHxCPauseType.ALL:
                    return PauseType.All;
                default:
                    throw new AggregateException($"enum arg not exist!value: {ohxc_pauseType}");
            }
        }


        public bool doSendOHxCCmdToVh(AVEHICLE assignVH, ACMD_OHTC cmd)
        {
            bool isSuccess = false;
            try
            {
                ActiveType activeType = default(ActiveType);
                string[] routeSections = null;
                string[] cycleRunSections = null;
                string[] minRouteSeg_Vh2From = null;
                string[] minRouteSeg_From2To = null;
                //如果失敗會將命令改成abonormal End
                if (scApp.CMDBLL.tryGenerateCmd_OHTC_Details(cmd, out activeType, out routeSections, out cycleRunSections
                                                                             , out minRouteSeg_Vh2From, out minRouteSeg_From2To))
                {
                    //若下達的命令為Park、CycleRun時,會一併更新Table:AVEHICLE、APARKZONEDETAIL或ACYCLEZONEDETAIL來確保停車數量、在途量的正確性。
                    isSuccess = sendTransferCommandToVh(cmd, assignVH, activeType, routeSections, cycleRunSections);

                    if (isSuccess)
                    {
                        //if (!SCUtility.isEmpty(cmd.CMD_ID_MCS))
                        //{
                        //    scApp.CMDBLL.updateCMD_MCS_TranStatus2Transferring(cmd.CMD_ID_MCS);
                        //}
                        assignVH.VehicleAssign();
                        //TODO 在進行命令的改派後SysExecQity的資料要重新判斷一下要怎樣計算
                        scApp.SysExcuteQualityBLL.updateSysExecQity_PassSecInfo(cmd.CMD_ID_MCS, assignVH.VEHICLE_ID, assignVH.CUR_SEC_ID,
                                                minRouteSeg_Vh2From, minRouteSeg_From2To);
                        scApp.CMDBLL.setVhExcuteCmdToShow(cmd, assignVH, routeSections, cycleRunSections);
                        assignVH.sw_speed.Restart();

                        //在設備確定接收該筆命令，把它從PreInitial改成Initial狀態並上報給MCS
                        if (!SCUtility.isEmpty(cmd.CMD_ID_MCS))
                        {
                            isSuccess &= scApp.CMDBLL.updateCMD_MCS_TranStatus2Initial(cmd.CMD_ID_MCS);
                            isSuccess &= scApp.ReportBLL.newReportTransferInitial(cmd.CMD_ID_MCS, null);
                        }
                    }
                    else
                    {
                        AbnormalEndCMD_OHT(cmd, E_CMD_STATUS.AbnormalEndByOHT);
                    }
                }
                else
                {
                    AbnormalEndCMD_OHT(cmd, E_CMD_STATUS.AbnormalEndByOHTC);//A0.02 
                }
            }
            catch (Exception ex)
            {
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Warn, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: ex,
                   VehicleID: assignVH?.VEHICLE_ID,
                   CarrierID: assignVH?.CST_ID,
                   Details: $"doSendOHxCCmdToVh fail.vh id:{cmd.VH_ID} ,cmd id:{cmd.CMD_ID} ,source:{cmd.SOURCE} destination:{cmd.DESTINATION}");
                isSuccess = false;
            }
            return isSuccess;
        }

        private void AbnormalEndCMD_OHT(ACMD_OHTC cmd, E_CMD_STATUS endStatus)
        {
            if (!SCUtility.isEmpty(cmd.CMD_ID_MCS))
            {
                scApp.CMDBLL.updateCMD_MCS_TranStatus2Queue(cmd.CMD_ID_MCS);
            }
            scApp.CMDBLL.updateCommand_OHTC_StatusByCmdID(cmd.CMD_ID, endStatus);
        }

        public bool doSendOHxCOverrideCmdToVh(AVEHICLE assignVH, ACMD_OHTC cmd, bool isNeedPauseFirst)
        {
            ActiveType activeType = default(ActiveType);
            string[] routeSections = null;
            string[] cycleRunSections = null;
            string[] minRouteSeg_Vh2From = null;
            string[] minRouteSeg_From2To = null;
            bool isSuccess = false;
            //如果失敗會將命令改成abonormal End
            if (scApp.CMDBLL.tryGenerateCmd_OHTC_Details(cmd, out activeType, out routeSections, out cycleRunSections
                                                                         , out minRouteSeg_Vh2From, out minRouteSeg_From2To))
            {
                //若下達的命令為Park、CycleRun時,會一併更新Table:AVEHICLE、APARKZONEDETAIL或ACYCLEZONEDETAIL來確保停車數量、在量的正確性。
                isSuccess = sendTransferCommandToVh(cmd, assignVH, ActiveType.Override, routeSections, cycleRunSections);

                if (isSuccess)
                {

                    //TODO 在進行命令的改派後SysExecQity的資料要重新判斷一下要怎樣計算
                    //scApp.SysExcuteQualityBLL.updateSysExecQity_PassSecInfo(cmd.CMD_ID_MCS, assignVH.VEHICLE_ID, assignVH.CUR_SEC_ID,
                    //                        minRouteSeg_Vh2From, minRouteSeg_From2To);
                    scApp.CMDBLL.setVhExcuteCmdToShow(cmd, assignVH, routeSections, cycleRunSections);
                    if (isNeedPauseFirst)
                        PauseRequest(assignVH.VEHICLE_ID, PauseEvent.Continue, OHxCPauseType.Normal);
                    assignVH.sw_speed.Restart();
                }
                else
                {
                    //if (!SCUtility.isEmpty(cmd.CMD_ID_MCS))
                    //{
                    //    scApp.CMDBLL.updateCMD_MCS_TranStatus2Queue(cmd.CMD_ID_MCS);
                    //}
                    //scApp.CMDBLL.updateCommand_OHTC_StatusByCmdID(cmd.CMD_ID, E_CMD_STATUS.AbnormalEndByOHT);
                }
            }
            return isSuccess;
        }

        public bool doCancelCommandByMCSCmdIDWithNoReport(string cancel_abort_mcs_cmd_id, CMDCancelType actType, out string ohtc_cmd_id)
        {
            ACMD_MCS mcs_cmd = scApp.CMDBLL.getCMD_MCSByID(cancel_abort_mcs_cmd_id);
            bool is_success = true;
            ohtc_cmd_id = string.Empty;
            switch (actType)
            {
                case CMDCancelType.CmdCancel:
                    //scApp.ReportBLL.newReportTransferCancelInitial(mcs_cmd, null);
                    if (mcs_cmd.TRANSFERSTATE == E_TRAN_STATUS.Queue)
                    {
                        return false;
                    }
                    else if (mcs_cmd.TRANSFERSTATE >= E_TRAN_STATUS.Queue && mcs_cmd.TRANSFERSTATE < E_TRAN_STATUS.Transferring)
                    {
                        AVEHICLE assign_vh = null;
                        assign_vh = scApp.VehicleBLL.getVehicleByExcuteMCS_CMD_ID(cancel_abort_mcs_cmd_id);
                        ohtc_cmd_id = assign_vh.OHTC_CMD;
                        is_success = doAbortCommand(assign_vh, ohtc_cmd_id, actType);
                        return is_success;
                    }
                    else if (mcs_cmd.TRANSFERSTATE >= E_TRAN_STATUS.Transferring) //當狀態變為Transferring時，即代表已經是Load complete
                    {
                        return false;
                    }
                    break;
                case CMDCancelType.CmdAbort:
                    //do nothing
                    break;
            }
            return is_success;
        }

        public bool doCancelOrAbortCommandByMCSCmdID(string cancel_abort_mcs_cmd_id, CMDCancelType actType)
        {
            ACMD_MCS mcs_cmd = scApp.CMDBLL.getCMD_MCSByID(cancel_abort_mcs_cmd_id);
            bool is_success = true;
            switch (actType)
            {
                case CMDCancelType.CmdCancel:
                    if (mcs_cmd.TRANSFERSTATE == E_TRAN_STATUS.Queue)
                    {
                        scApp.CMDBLL.updateCMD_MCS_TranStatus2Canceling(cancel_abort_mcs_cmd_id);
                        scApp.ReportBLL.newReportTransferCancelInitial(mcs_cmd, null);
                        //S6F11SendTransferCancelInitial(mcs_cmd, null);
                        scApp.CMDBLL.updateCMD_MCS_TranStatus2Canceled(cancel_abort_mcs_cmd_id);
                        scApp.ReportBLL.newReportTransferCancelCompleted(mcs_cmd, null);
                        //S6F11SendTransferCancelCompleted(mcs_cmd, null);
                    }
                    else
                    {
                        scApp.ReportBLL.newReportTransferCancelInitial(cancel_abort_mcs_cmd_id, null);
                        //S6F11SendTransferCancelInitial(cancel_abort_mcs_cmd_id, null);
                        is_success = scApp.VehicleService.cancleOrAbortCommandByMCSCmdID(cancel_abort_mcs_cmd_id, ProtocolFormat.OHTMessage.CMDCancelType.CmdCancel);
                        if (!is_success)
                        {
                            scApp.ReportBLL.newReportTransferCancelFailed(cancel_abort_mcs_cmd_id, null);
                            //S6F11SendTransferCancelFailed(cancel_abort_mcs_cmd_id, null);
                        }
                    }
                    break;
                case CMDCancelType.CmdAbort:
                    scApp.ReportBLL.newReportTransferAbortInitial(cancel_abort_mcs_cmd_id, null);
                    is_success = scApp.VehicleService.cancleOrAbortCommandByMCSCmdID(cancel_abort_mcs_cmd_id, ProtocolFormat.OHTMessage.CMDCancelType.CmdAbort);
                    if (!is_success)
                    {
                        scApp.ReportBLL.newReportTransferAbortFailed(cancel_abort_mcs_cmd_id, null);
                        //S6F11SendTransferAbortFailed(cancel_abort_cmd_id, null);
                    }
                    break;
            }
            return is_success;
        }

        public bool cancleOrAbortCommandByMCSCmdID(string mcsCmdID, CMDCancelType actType)
        {
            bool isSuccess = true;
            AVEHICLE assign_vh = null;
            try
            {
                assign_vh = scApp.VehicleBLL.getVehicleByExcuteMCS_CMD_ID(mcsCmdID);
                if (assign_vh == null)
                {
                    LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                       Data: $"command interrupt by mcs command id:{mcsCmdID} fail. current no vh in excute",
                       VehicleID: assign_vh?.VEHICLE_ID,
                       CarrierID: assign_vh?.CST_ID);
                    return false;
                }
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: $"command interrupt by mcs command id:{mcsCmdID},vh:{assign_vh.VEHICLE_ID},ohtc cmd:{assign_vh.OHTC_CMD},interrupt type:{actType}",
                   VehicleID: assign_vh?.VEHICLE_ID,
                   CarrierID: assign_vh?.CST_ID);
                switch (actType)
                {
                    case CMDCancelType.CmdAbort:
                        scApp.CMDBLL.updateCMD_MCS_TranStatus2Aborting(mcsCmdID);
                        //scApp.CMDBLL.updateCMD_MCS_TranStatus2Aborting(ohtc_cmd_id);
                        break;
                    case CMDCancelType.CmdCancel:
                        scApp.CMDBLL.updateCMD_MCS_TranStatus2Canceling(mcsCmdID);
                        //scApp.CMDBLL.updateCMD_MCS_TranStatus2Canceling(ohtc_cmd_id);
                        break;
                }
                string ohtc_cmd_id = SCUtility.Trim(assign_vh.OHTC_CMD);
                //A0.01 isSuccess = doAbortCommand(assign_vh, mcsCmdID, actType);
                isSuccess = doAbortCommand(assign_vh, ohtc_cmd_id, actType); //A0.01
            }
            catch (Exception ex)
            {
                isSuccess = false;
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Warn, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: ex,
                   VehicleID: assign_vh?.VEHICLE_ID,
                   CarrierID: assign_vh?.CST_ID,
                   Details: $"abort command fail mcs command id:{mcsCmdID}");
            }
            return isSuccess;
        }
        public bool doAbortCommand(AVEHICLE assign_vh, string cmd_id, CMDCancelType actType)
        {
            return assign_vh.sned_Str37(cmd_id, actType);
        }

        private bool sendTransferCommandToVh(ACMD_OHTC cmd, AVEHICLE assignVH, ActiveType activeType, string[] routeSections, string[] cycleRunSections)
        {
            bool isSuccess = true;
            try
            {
                List<AMCSREPORTQUEUE> reportqueues = new List<AMCSREPORTQUEUE>();
                using (var tx = SCUtility.getTransactionScope())
                {
                    using (DBConnection_EF con = DBConnection_EF.GetUContext())
                    {
                        switch (cmd.CMD_TPYE)
                        {
                            case E_CMD_TYPE.Move_Park:
                                //APARKZONEDETAIL aPARKZONEDETAIL = scApp.ParkBLL.getParkDetailByAdr(cmd.DESTINATION);
                                //scApp.VehicleBLL.setVhIsParkingOnWay(cmd.VH_ID, cmd.DESTINATION);
                                //if (assignVH.IS_PARKING)
                                //{
                                //scApp.ParkBLL.resetParkAdr(assignVH.PARK_ADR_ID);
                                scApp.ParkBLL.resetParkAdrByVhID(assignVH.VEHICLE_ID);
                                //scApp.VehicleBLL.resetVhIsInPark(assignVH.VEHICLE_ID);
                                //}
                                scApp.VehicleBLL.setVhIsParkingOnWay(cmd.VH_ID, cmd.DESTINATION);

                                break;
                            case E_CMD_TYPE.Round:
                                if (assignVH.IS_PARKING)
                                {
                                    scApp.ParkBLL.resetParkAdr(assignVH.PARK_ADR_ID);

                                    //scApp.VehicleBLL.resetVhIsInPark(assignVH.VEHICLE_ID);
                                }
                                scApp.VehicleBLL.setVhIsCycleRunOnWay(cmd.VH_ID, cmd.DESTINATION);
                                break;
                            default:
                                //這件事要搬到前面去做(每次多要判斷)
                                //改成每次都去清空這兩個欄位
                                //if (assignVH.IS_PARKING
                                //    || !SCUtility.isEmpty(assignVH.PARK_ADR_ID))
                                //{
                                //改成找出該VH是停在哪個位置，並更新狀態
                                //scApp.ParkBLL.resetParkAdr(assignVH.PARK_ADR_ID);


                                scApp.ParkBLL.resetParkAdrByVhID(assignVH.VEHICLE_ID);
                                scApp.VehicleBLL.resetVhIsInPark(assignVH.VEHICLE_ID);


                                if (assignVH.IS_CYCLING)
                                {
                                    scApp.VehicleBLL.resetVhIsCycleRun(assignVH.VEHICLE_ID);
                                }
                                break;
                        }

                        isSuccess &= scApp.CMDBLL.updateCommand_OHTC_StatusByCmdID(cmd.CMD_ID, E_CMD_STATUS.Execution);
                        if (activeType != ActiveType.Override)
                        {
                            isSuccess &= scApp.VehicleBLL.updateVehicleExcuteCMD(cmd.VH_ID, cmd.CMD_ID, cmd.CMD_ID_MCS);

                            if (!SCUtility.isEmpty(cmd.CMD_ID_MCS))
                            {
                                isSuccess &= scApp.VIDBLL.upDateVIDCommandInfo(cmd.VH_ID, cmd.CMD_ID_MCS);
                                isSuccess &= scApp.ReportBLL.newReportBeginTransfer(assignVH.VEHICLE_ID, reportqueues);
                                scApp.ReportBLL.insertMCSReport(reportqueues);
                            }
                        }

                        if (isSuccess)
                        {
                            isSuccess &= TransferRequset
                                (cmd.VH_ID, cmd.CMD_ID, activeType, cmd.CARRIER_ID, routeSections, cycleRunSections
                                , cmd.SOURCE, cmd.DESTINATION);
                            //isSuccess &= assignVH.sned_Str31(cmd.CMD_ID, activeType, cmd.CARRIER_ID, routeSections, cycleRunSections
                            //    , cmd.SOURCE, cmd.DESTINATION, out Reason);
                        }
                        if (isSuccess)
                        {
                            tx.Complete();
                        }
                        else
                        {
                            //scApp.getEQObjCacheManager().restoreVhDataFromDB(assignVH);
                        }
                    }
                }

                if (isSuccess)
                {
                    scApp.ReportBLL.newSendMCSMessage(reportqueues);
                    Task.Run(() => scApp.FlexsimCommandDao.setCommandToFlexsimDB(cmd));
                }
                else
                {
                    //scApp.getEQObjCacheManager().restoreVhDataFromDB(assignVH);
                }

            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exection:");
                isSuccess = false;
                //scApp.getEQObjCacheManager().restoreVhDataFromDB(assignVH);
            }
            return isSuccess;
        }


        public bool TransferRequset(string vh_id, string cmd_id, ActiveType activeType, string cst_id,
            string[] passSections, string[] cycleSections, string fromAdr, string toAdr)
        {
            bool isSuccess = false;
            string reason = string.Empty;
            ID_131_TRANS_RESPONSE receive_gpp = null;
            AVEHICLE vh = scApp.getEQObjCacheManager().getVehicletByVHID(vh_id);
            isSuccess = TransferCommandCheck(activeType, cst_id, passSections, cycleSections, fromAdr, toAdr, out reason);
            if (isSuccess)
            {
                ID_31_TRANS_REQUEST send_gpb = new ID_31_TRANS_REQUEST()
                {
                    CmdID = cmd_id,
                    ActType = activeType,
                    CSTID = cst_id ?? string.Empty,
                    LoadAdr = fromAdr,
                    ToAdr = toAdr
                };
                if (passSections != null)
                    send_gpb.GuideSections.AddRange(passSections);
                if (cycleSections != null)
                    send_gpb.CycleSections.AddRange(cycleSections);
                SCUtility.RecodeReportInfo(vh.VEHICLE_ID, 0, send_gpb);
                isSuccess = vh.sned_Str31(send_gpb, out receive_gpp, out reason);
                SCUtility.RecodeReportInfo(vh.VEHICLE_ID, 0, receive_gpp, isSuccess.ToString());
            }
            if (isSuccess)
            {
                int reply_code = receive_gpp.ReplyCode;
                if (reply_code != 0)
                {
                    isSuccess = false;
                    var return_code_map = scApp.CMDBLL.getReturnCodeMap(vh.NODE_ID, reply_code.ToString());
                    if (return_code_map != null)
                        reason = return_code_map.DESC;
                    bcf.App.BCFApplication.onWarningMsg(string.Format("發送命令失敗,VH ID:{0}, CMD ID:{1}, Reason:{2}",
                                                              vh_id,
                                                              cmd_id,
                                                              reason));
                }
            }
            else
            {
                bcf.App.BCFApplication.onWarningMsg(string.Format("發送命令失敗,VH ID:{0}, CMD ID:{1}, Reason:{2}",
                                          vh_id,
                                          cmd_id,
                                          reason));
                VehicleStatusRequest(vh_id, true);
            }

            return isSuccess;
        }

        public bool CarrierIDRenameRequset(string vh_id, string oldCarrierID, string newCarrierID)
        {
            bool isSuccess = true;

            AVEHICLE vh = scApp.getEQObjCacheManager().getVehicletByVHID(vh_id);
            ID_135_CST_ID_RENAME_RESPONSE receive_gpp;
            ID_35_CST_ID_RENAME_REQUEST send_gpp = new ID_35_CST_ID_RENAME_REQUEST()
            {
                OLDCSTID = oldCarrierID ?? string.Empty,
                NEWCSTID = newCarrierID ?? string.Empty,
            };
            SCUtility.RecodeReportInfo(vh.VEHICLE_ID, 0, send_gpp);
            isSuccess = vh.sned_Str35(send_gpp, out receive_gpp);
            SCUtility.RecodeReportInfo(vh.VEHICLE_ID, 0, receive_gpp, isSuccess.ToString());
            return isSuccess;
        }

        private bool TransferCommandCheck(ActiveType activeType, string cst_id, string[] passSections, string[] cycleSections, string fromAdr, string toAdr, out string reason)
        {
            reason = "";
            if (activeType == ActiveType.Home || activeType == ActiveType.Mtlhome)
            {
                return true;
            }

            if (activeType == ActiveType.Load || activeType == ActiveType.Unload ||
                (activeType == ActiveType.Loadunload && SCUtility.isMatche(fromAdr, toAdr)))
            {
                //not thing...
            }
            else
            {
                if (passSections == null || passSections.Count() == 0)
                {
                    reason = "Pass section is empty !";
                    return false;
                }
            }

            //if (activeType != ActiveType.Load && activeType != ActiveType.Unload &&
            //(passSections == null || passSections.Count() == 0))
            //{
            //    reason = "Pass section is empty !";
            //    return false;
            //}

            bool isOK = true;
            switch (activeType)
            {
                case ActiveType.Load:
                    if (SCUtility.isEmpty(fromAdr))
                    {
                        isOK = false;
                        reason = $"Transfer type[{activeType},from adr is empty!]";
                    }
                    break;
                case ActiveType.Unload:
                    if (SCUtility.isEmpty(toAdr))
                    {
                        isOK = false;
                        reason = $"Transfer type[{activeType},from adr is empty!]";
                    }
                    break;
                case ActiveType.Loadunload:
                    if (SCUtility.isEmpty(fromAdr))
                    {
                        isOK = false;
                        reason = $"Transfer type[{activeType},from adr is empty!]";
                    }
                    else if (SCUtility.isEmpty(toAdr))
                    {
                        isOK = false;
                        reason = $"Transfer type[{activeType},toAdr adr is empty!]";
                    }
                    break;
                    //case ActiveType.Round:
                    //    if (cycleSections == null || cycleSections.Count() == 0)
                    //    {
                    //        isOK = false;
                    //        reason = $"Transfer type[{activeType},cycleSections is empty!]";
                    //    }
                    //    break;
            }

            return isOK;
        }

        public bool TeachingRequest(string vh_id, string from_adr, string to_adr)
        {
            bool isSuccess = false;
            AVEHICLE vh = scApp.getEQObjCacheManager().getVehicletByVHID(vh_id);
            ID_171_RANGE_TEACHING_RESPONSE receive_gpp;
            ID_71_RANGE_TEACHING_REQUEST send_gpp = new ID_71_RANGE_TEACHING_REQUEST()
            {
                FromAdr = from_adr,
                ToAdr = to_adr
            };

            SCUtility.RecodeReportInfo(vh.VEHICLE_ID, 0, send_gpp);
            isSuccess = vh.send_Str71(send_gpp, out receive_gpp);
            SCUtility.RecodeReportInfo(vh.VEHICLE_ID, 0, receive_gpp, isSuccess.ToString());

            return isSuccess;
        }


        #endregion Tcp/Ip
        #region PLC
        public void PLC_Control_TrunOn(string vh_id)
        {
            AVEHICLE vh = scApp.getEQObjCacheManager().getVehicletByVHID(vh_id);
            vh.PLC_Control_TrunOn();
        }
        public void PLC_Control_TrunOff(string vh_id)
        {
            AVEHICLE vh = scApp.getEQObjCacheManager().getVehicletByVHID(vh_id);
            vh.PLC_Control_TrunOff();
        }


        public bool SetVehicleControlItemForPLC(string vh_id, Boolean[] items)
        {
            AVEHICLE vh = scApp.getEQObjCacheManager().getVehicletByVHID(vh_id);
            return vh.setVehicleControlItemForPLC(items);
        }
        #endregion PLC

        #endregion Send Message To Vehicle

        #region Position Report
        [ClassAOPAspect]
        public void PositionReport(BCFApplication bcfApp, AVEHICLE eqpt, ID_134_TRANS_EVENT_REP recive_str)
        {
            if (scApp.getEQObjCacheManager().getLine().ServerPreStop)
                return;
            //LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
            //   seq_num: 0, //由於Position Report的資料可能從很多地方來，例如143、144、PLC、136 因此在此先不考慮其seq_num
            //   Data: recive_str,
            //   VehicleID: eqpt.VEHICLE_ID,
            //   CarrierID: eqpt.CST_ID);
            SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, 0, recive_str);
            EventType eventType = recive_str.EventType;
            string current_adr_id = SCUtility.isEmpty(recive_str.CurrentAdrID) ? string.Empty : recive_str.CurrentAdrID;
            string current_sec_id = SCUtility.isEmpty(recive_str.CurrentSecID) ? string.Empty : recive_str.CurrentSecID;
            ASECTION current_sec = scApp.SectionBLL.cache.GetSection(current_sec_id);
            string current_seg_id = current_sec == null ? string.Empty : current_sec.SEG_NUM;

            string last_adr_id = eqpt.CUR_ADR_ID;
            string last_sec_id = eqpt.CUR_SEC_ID;
            ASECTION lase_sec = scApp.SectionBLL.cache.GetSection(last_sec_id);
            string last_seg_id = lase_sec == null ? string.Empty : lase_sec.SEG_NUM;
            uint sec_dis = recive_str.SecDistance;

            doUpdateVheiclePositionAndCmdSchedule(eqpt, current_adr_id, current_sec_id, current_seg_id, last_adr_id, last_sec_id, last_seg_id, sec_dis, eventType);

            switch (eventType)
            {
                case EventType.AdrPass:
                case EventType.AdrOrMoveArrivals:
                    PositionReport_AdrPassArrivals(bcfApp, eqpt, recive_str, last_adr_id, last_sec_id);
                    break;
            }
        }
        [ClassAOPAspect]
        public void PositionReport_100(BCFApplication bcfApp, AVEHICLE eqpt, ID_134_TRANS_EVENT_REP recive_str, int seq_num)
        {
            if (scApp.getEQObjCacheManager().getLine().ServerPreStop)
                return;

            SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, seq_num, recive_str);
            EventType eventType = recive_str.EventType;
            string current_adr_id = recive_str.CurrentAdrID;
            string current_sec_id = recive_str.CurrentSecID;
            ASECTION current_sec = scApp.SectionBLL.cache.GetSection(current_sec_id);
            string current_seg_id = current_sec == null ? string.Empty : current_sec.SEG_NUM;

            string last_adr_id = eqpt.CUR_ADR_ID;
            string last_sec_id = eqpt.CUR_SEC_ID;
            ASECTION lase_sec = scApp.SectionBLL.cache.GetSection(last_sec_id);
            string last_seg_id = lase_sec == null ? string.Empty : lase_sec.SEG_NUM;
            uint sec_dis = recive_str.SecDistance;

            if (eventType == EventType.AdrOrMoveArrivals)
            {
                List<string> adrs = new List<string>();
                adrs.Add(SCUtility.Trim(current_adr_id));
                List<ASECTION> Secs = scApp.MapBLL.loadSectionByToAdrs(adrs);
                if (Secs.Count > 0)
                {
                    current_sec_id = Secs[0].SEC_ID.Trim();
                }
            }
            doUpdateVheiclePositionAndCmdSchedule(eqpt, current_adr_id, current_sec_id, current_seg_id, last_adr_id, last_sec_id, last_seg_id, sec_dis, eventType);

            switch (eventType)
            {
                case EventType.AdrPass:
                case EventType.AdrOrMoveArrivals:
                    PositionReport_AdrPassArrivals(bcfApp, eqpt, recive_str, last_adr_id, last_sec_id);
                    break;
            }
        }

        public void doUpdateVheiclePositionAndCmdSchedule(AVEHICLE vh,
            string current_adr_id, string current_sec_id, string current_seg_id,
            string last_adr_id, string last_sec_id, string last_seg_id,
            uint sec_dis, EventType vhPassEvent)
        {
            try
            {
                lock (vh.PositionRefresh_Sync)
                {
                    ALINE line = scApp.getEQObjCacheManager().getLine();
                    scApp.VehicleBLL.updateVheiclePosition_CacheManager(vh, current_adr_id, current_sec_id, current_seg_id, sec_dis);

                    if (line.ServiceMode == SCAppConstants.AppServiceMode.Active)
                    {
                        if (!SCUtility.isMatche(current_seg_id, last_seg_id))
                        {
                            vh.onSegmentChange(current_seg_id, last_seg_id);
                        }

                        if (!SCUtility.isMatche(last_sec_id, current_sec_id))
                        {
                            vh.onLocationChange(current_sec_id, last_sec_id);
                            //TODO 要改成查一次CMD出來然後直接帶入CMD ID
                            if (!SCUtility.isEmpty(vh.OHTC_CMD))
                            {
                                scApp.CMDBLL.update_CMD_DetailEntryTime(vh.OHTC_CMD, current_adr_id, current_sec_id);
                                scApp.CMDBLL.update_CMD_DetailLeaveTime(vh.OHTC_CMD, last_adr_id, last_sec_id);
                                List<string> willPassSecID = null;
                                vh.procProgress_Percen = scApp.CMDBLL.getAndUpdateVhCMDProgress(vh.VEHICLE_ID, out willPassSecID);
                                var will_pass_tmp = willPassSecID.Select(route => SCUtility.Trim(route, true));
                                vh.WillPassSectionID = will_pass_tmp.ToList();
                                //scApp.VehicleBLL.NetworkQualityTest(vh.VEHICLE_ID, current_adr_id, current_sec_id, 0);
                            }
                            //vh.onLocationChange(current_sec_id, last_sec_id);
                        }
                        //if (!SCUtility.isMatche(current_seg_id, last_seg_id))
                        //{
                        //    vh.onSegmentChange(current_seg_id, last_seg_id);
                        //}
                        //if (!SCUtility.isMatche(current_adr_id, last_adr_id) || !SCUtility.isMatche(current_sec_id, last_sec_id))
                        //    scApp.VehicleBLL.updateVheiclePosition(vh.VEHICLE_ID, current_adr_id, current_sec_id, sec_dis, vhPassEvent);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception:");
            }
        }

        //[ClassAOPAspect]
        //public void TranEventReport(BCFApplication bcfApp, AVEHICLE eqpt, ID_136_TRANS_EVENT_REP recive_str, int seq_num)
        //{
        //    if (scApp.getEQObjCacheManager().getLine().ServerPreStop)
        //        return;

        //    LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
        //       seq_num: seq_num,
        //       Data: recive_str,
        //       VehicleID: eqpt.VEHICLE_ID,
        //       CarrierID: eqpt.CST_ID);

        //    SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, seq_num, recive_str);
        //    EventType eventType = recive_str.EventType;
        //    string current_adr_id = recive_str.CurrentAdrID;
        //    string current_sec_id = recive_str.CurrentSecID;
        //    string carrier_id = recive_str.CSTID;
        //    string last_adr_id = eqpt.CUR_ADR_ID;
        //    string last_sec_id = eqpt.CUR_SEC_ID;
        //    string req_block_id = recive_str.RequestBlockID;
        //    //lock (eqpt.PositionRefresh_Sync)
        //    //{
        //    //    switch (eventType)
        //    //    {
        //    //        case EventType.LoadArrivals:
        //    //        case EventType.UnloadArrivals:
        //    //        case EventType.VhmoveArrivals:
        //    //            scApp.VehicleBLL.deleteRedisOfPositionReport(eqpt.VEHICLE_ID); //為了確保在PositionReportTimerAction要更新位置時，不會拿到舊的
        //    //            break;
        //    //    }
        //    //    doUpdateVheiclePositionAndCmdSchedule(eqpt, current_adr_id, current_sec_id, last_adr_id, last_sec_id, (uint)eqpt.ACC_SEC_DIST, eventType, loadCSTStatus);
        //    //}
        //    scApp.VehicleBLL.updateVehicleActionStatus(eqpt, eventType);

        //    switch (eventType)
        //    {
        //        case EventType.BlockReq:
        //            PositionReport_BlockReq_New(bcfApp, eqpt, seq_num, recive_str.RequestBlockID);
        //            break;
        //        case EventType.Hidreq:
        //            PositionReport_HIDRequest(bcfApp, eqpt, seq_num, recive_str.RequestBlockID);
        //            break;
        //        case EventType.LoadArrivals:
        //        case EventType.LoadComplete:
        //        case EventType.UnloadArrivals:
        //        case EventType.UnloadComplete:
        //        case EventType.VhmoveArrivals:
        //        case EventType.AdrOrMoveArrivals:
        //            PositionReport_ArriveAndComplete(bcfApp, eqpt, seq_num, recive_str.EventType, recive_str.CurrentAdrID, recive_str.CurrentSecID, carrier_id);
        //            break;
        //        case EventType.Vhloading:
        //        case EventType.Vhunloading:
        //            PositionReport_LoadingUnloading(bcfApp, eqpt, recive_str, seq_num, eventType);

        //            break;
        //        case EventType.BlockRelease:
        //            PositionReport_BlockRelease(bcfApp, eqpt, recive_str, seq_num);
        //            break;
        //        case EventType.Hidrelease:
        //            PositionReport_HIDRelease(bcfApp, eqpt, recive_str, seq_num);
        //            break;
        //    }
        //}

        private void PositionReport_LoadingUnloading(BCFApplication bcfApp, AVEHICLE eqpt, ID_136_TRANS_EVENT_REP recive_str, int seq_num, EventType eventType)
        {
            LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
               Data: $"Process report {eventType}",
               VehicleID: eqpt.VEHICLE_ID,
               CarrierID: eqpt.CST_ID);

            if (!SCUtility.isEmpty(eqpt.MCS_CMD))
            {
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: $"do report {eventType} to mcs.",
                   VehicleID: eqpt.VEHICLE_ID,
                   CarrierID: eqpt.CST_ID);

                List<AMCSREPORTQUEUE> reportqueues = new List<AMCSREPORTQUEUE>();
                using (TransactionScope tx = SCUtility.getTransactionScope())
                {
                    using (DBConnection_EF con = DBConnection_EF.GetUContext())
                    {
                        bool isSuccess = true;
                        switch (eventType)
                        {
                            case EventType.Vhloading:
                                scApp.CMDBLL.updateCMD_MCS_TranStatus2Transferring(eqpt.MCS_CMD);
                                scApp.CMDBLL.updateCMD_MCS_CmdStatus2Loading(eqpt.MCS_CMD);
                                scApp.ReportBLL.newReportLoading(eqpt.VEHICLE_ID, reportqueues);
                                break;
                            case EventType.Vhunloading:
                                scApp.CMDBLL.updateCMD_MCS_CmdStatus2Unloading(eqpt.MCS_CMD);
                                scApp.ReportBLL.newReportUnloading(eqpt.VEHICLE_ID, reportqueues);
                                break;
                        }
                        scApp.ReportBLL.insertMCSReport(reportqueues);

                        if (isSuccess)
                        {
                            if (replyTranEventReport(bcfApp, recive_str.EventType, eqpt, seq_num))
                            {
                                //scApp.VehicleBLL.updateVehicleStatus_CacheMangerForAct(eqpt, actionStat);
                                tx.Complete();
                                scApp.ReportBLL.newSendMCSMessage(reportqueues);
                            }
                        }
                    }
                }
            }
            else
            {
                replyTranEventReport(bcfApp, recive_str.EventType, eqpt, seq_num);
            }
            if (eventType == EventType.Vhloading)
            {
                scApp.VehicleBLL.doLoading(eqpt.VEHICLE_ID);
            }
            else if (eventType == EventType.Vhunloading)
            {
                scApp.VehicleBLL.doUnloading(eqpt.VEHICLE_ID);
            }
            Task.Run(() => scApp.FlexsimCommandDao.setVhEventTypeToFlexsimDB(eqpt.VEHICLE_ID, eventType));
        }

        [ClassAOPAspect]
        //public void TranEventReport_100(BCFApplication bcfApp, AVEHICLE eqpt, ID_136_TRANS_EVENT_REP recive_str, int seq_num)
        public void TranEventReport(BCFApplication bcfApp, AVEHICLE eqpt, ID_136_TRANS_EVENT_REP recive_str, int seq_num)
        {
            if (scApp.getEQObjCacheManager().getLine().ServerPreStop)
                return;

            LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
               seq_num: seq_num,
               Data: recive_str,
               VehicleID: eqpt.VEHICLE_ID,
               CarrierID: eqpt.CST_ID);

            SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, seq_num, recive_str);
            EventType eventType = recive_str.EventType;
            string current_adr_id = recive_str.CurrentAdrID;
            string current_sec_id = recive_str.CurrentSecID;
            string carrier_id = recive_str.CSTID;
            string last_adr_id = eqpt.CUR_ADR_ID;
            string last_sec_id = eqpt.CUR_SEC_ID;
            string req_block_id = recive_str.RequestBlockID;
            BCRReadResult bCRReadResult = recive_str.BCRReadResult;

            //lock (eqpt.PositionRefresh_Sync)
            //{
            //switch (eventType)
            //{
            //    case EventType.LoadArrivals:
            //    case EventType.UnloadArrivals:
            //    case EventType.VhmoveArrivals:
            //    case EventType.AdrOrMoveArrivals:
            //        if (eventType == EventType.AdrOrMoveArrivals)
            //        {
            //            List<string> adrs = new List<string>();
            //            adrs.Add(SCUtility.Trim(current_adr_id));
            //            List<ASECTION> Secs = scApp.MapBLL.loadSectionByToAdrs(adrs);
            //            if (Secs.Count > 0)
            //            {
            //                current_sec_id = Secs[0].SEC_ID.Trim();
            //            }
            //        }
            //        scApp.VehicleBLL.deleteRedisOfPositionReport(eqpt.VEHICLE_ID); //為了確保在PositionReportTimerAction要更新位置時，不會拿到舊的
            //        break;
            //}
            //doUpdateVheiclePositionAndCmdSchedule(eqpt, current_adr_id, current_sec_id, last_adr_id, last_sec_id, (uint)eqpt.ACC_SEC_DIST, eventType, loadCSTStatus);
            //}
            scApp.VehicleBLL.updateVehicleActionStatus(eqpt, eventType);


            switch (eventType)
            {
                case EventType.BlockReq:
                case EventType.Hidreq:
                case EventType.BlockHidreq:
                    //PositionReport_BlockReq_HIDReq(bcfApp, eqpt, seq_num, recive_str.RequestBlockID, recive_str.RequestHIDID);
                    ProcessBlockOrHIDReq(bcfApp, eqpt, eventType, seq_num, recive_str.RequestBlockID, recive_str.RequestHIDID);
                    break;
                case EventType.LoadArrivals:
                case EventType.UnloadArrivals:
                case EventType.UnloadComplete:
                case EventType.AdrOrMoveArrivals:
                case EventType.LoadComplete:
                    PositionReport_ArriveAndComplete(bcfApp, eqpt, seq_num, recive_str.EventType, recive_str.CurrentAdrID, recive_str.CurrentSecID, carrier_id);
                    break;
                case EventType.Vhloading:
                case EventType.Vhunloading:
                    PositionReport_LoadingUnloading(bcfApp, eqpt, recive_str, seq_num, eventType);
                    break;
                case EventType.BlockRelease:
                    PositionReport_BlockRelease(bcfApp, eqpt, recive_str, seq_num);
                    replyTranEventReport(bcfApp, recive_str.EventType, eqpt, seq_num);
                    break;
                case EventType.Hidrelease:
                    PositionReport_HIDRelease(bcfApp, eqpt, recive_str, seq_num);
                    replyTranEventReport(bcfApp, recive_str.EventType, eqpt, seq_num);
                    break;
                case EventType.BlockHidrelease:
                    PositionReport_BlockRelease(bcfApp, eqpt, recive_str, seq_num);
                    PositionReport_HIDRelease(bcfApp, eqpt, recive_str, seq_num);
                    replyTranEventReport(bcfApp, recive_str.EventType, eqpt, seq_num);
                    break;
                case EventType.Bcrread:
                    TransferReportBCRRead(bcfApp, eqpt, seq_num, eventType, carrier_id, bCRReadResult);
                    PositionReport_ArriveAndComplete(bcfApp, eqpt, seq_num, EventType.LoadComplete, recive_str.CurrentAdrID, recive_str.CurrentSecID, carrier_id);
                    break;
            }
        }

        private void TransferReportBCRRead(BCFApplication bcfApp, AVEHICLE eqpt, int seqNum,
                                             EventType eventType, string read_carrier_id, BCRReadResult bCRReadResult)
        {
            scApp.VehicleBLL.updateVehicleBCRReadResult(eqpt, bCRReadResult);
            AVIDINFO vid_info = scApp.VIDBLL.getVIDInfo(eqpt.VEHICLE_ID);
            string old_carrier_id = SCUtility.Trim(vid_info.CARRIER_ID, true);

            var port_station = scApp.PortStationBLL.OperateCatch.getPortStationByID(eqpt.CUR_ADR_ID);
            string port_station_id = port_station == null ? "" : port_station.PORT_ID;
            LogHelper.LogBCRReadInfo
                (eqpt.VEHICLE_ID, port_station_id, eqpt.MCS_CMD, eqpt.OHTC_CMD, old_carrier_id, read_carrier_id, bCRReadResult, SystemParameter.IsEnableIDReadFailScenario);

            bool is_need_report_install = CheckIsNeedReportInstall2MCS(eqpt, vid_info);

            scApp.VIDBLL.upDateVIDCarrierLocInfo(eqpt.VEHICLE_ID, eqpt.Real_ID);
            switch (bCRReadResult)
            {
                case BCRReadResult.BcrMisMatch:
                    LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                       Data: $"BCR miss match happend,start abort command id:{eqpt.OHTC_CMD?.Trim()} and rename cst id:{old_carrier_id}...",
                       VehicleID: eqpt.VEHICLE_ID,
                       CarrierID: eqpt.CST_ID);
                    if (!checkHasDuplicateHappend(bcfApp, eqpt, seqNum, eventType, read_carrier_id, old_carrier_id))
                    {
                        scApp.VehicleBLL.updataVehicleCSTID(eqpt.VEHICLE_ID, read_carrier_id);
                        replyTranEventReport(bcfApp, eventType, eqpt, seqNum,
                            renameCarrierID: read_carrier_id,
                            cancelType: CMDCancelType.CmdCancelIdMismatch);
                        LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                           Data: $"BCR miss match happend,start abort command id:{eqpt.OHTC_CMD?.Trim()} and rename cst id:{old_carrier_id} to {read_carrier_id}",
                           VehicleID: eqpt.VEHICLE_ID,
                           CarrierID: eqpt.CST_ID);
                    }
                    // Task.Run(() => doAbortCommand(eqpt, eqpt.OHTC_CMD, CMDCancelType.CmdCancelIdMismatch));
                    break;
                case BCRReadResult.BcrReadFail:
                    string new_carrier_id = "";
                    CMDCancelType cancelType = CMDCancelType.CmdNone;
                    if (SystemParameter.IsEnableIDReadFailScenario)
                    {
                        ALINE line = scApp.getEQObjCacheManager().getLine();
                        LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                           Data: $"BCR read fail happend,start abort command id:{eqpt.OHTC_CMD?.Trim()} and rename cst id...",
                           VehicleID: eqpt.VEHICLE_ID,
                           CarrierID: eqpt.CST_ID);
                        //string old_carrier_id = SCUtility.Trim(vid_info.CARRIER_ID, true);
                        bool is_unknow_old_name_cst = SCUtility.isEmpty(old_carrier_id);
                        //string new_carrier_id = string.Empty;
                        if (is_unknow_old_name_cst)
                        {
                            new_carrier_id =
                                $"UNKNOWN-{old_carrier_id}-{line.LINE_ID}{eqpt.Real_ID}-{DateTime.Now.ToString(SCAppConstants.TimestampFormat_12)}001";//固定加入001的Sequence
                            scApp.VIDBLL.upDateVIDCarrierID(eqpt.VEHICLE_ID, new_carrier_id);
                        }
                        else
                        {
                            bool was_renamed = old_carrier_id.StartsWith("UNKNOWN");
                            new_carrier_id = was_renamed ?
                                old_carrier_id :
                                $"UNKNOWN-{old_carrier_id}-{line.LINE_ID}{eqpt.Real_ID}-{DateTime.Now.ToString(SCAppConstants.TimestampFormat_12)}001";//固定加入001的Sequence
                        }
                        scApp.VehicleBLL.updataVehicleCSTID(eqpt.VEHICLE_ID, new_carrier_id);
                        cancelType = CMDCancelType.CmdCancelIdReadFailed;
                        LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                           Data: $"BCR read fail happend,start abort command id:{eqpt.OHTC_CMD?.Trim()} and rename cst id:{old_carrier_id} to {new_carrier_id} ",
                           VehicleID: eqpt.VEHICLE_ID,
                           CarrierID: eqpt.CST_ID);
                    }
                    else
                    {
                        LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                           Data: $"BCR read fail happend,continue excute command.",
                           VehicleID: eqpt.VEHICLE_ID,
                           CarrierID: eqpt.CST_ID);
                        cancelType = CMDCancelType.CmdNone;
                        if (!SCUtility.isEmpty(eqpt.MCS_CMD))
                        {
                            ACMD_MCS mcs_cmd = scApp.CMDBLL.getCMD_MCSByID(eqpt.MCS_CMD);
                            if (mcs_cmd != null)
                            {
                                new_carrier_id = SCUtility.Trim(mcs_cmd.CARRIER_ID);
                            }
                            else
                            {
                                new_carrier_id = "";
                                is_need_report_install = false;
                            }
                        }
                        else
                        {
                            new_carrier_id = "";
                            is_need_report_install = false;
                        }
                    }

                    //replyTranEventReport(bcfApp, eventType, eqpt, seqNum,
                    //    renameCarrierID: new_carrier_id, cancelType: CMDCancelType.CmdCancelIdReadFailed);
                    replyTranEventReport(bcfApp, eventType, eqpt, seqNum,
                        renameCarrierID: new_carrier_id,
                        cancelType: cancelType);
                    //     Task.Run(() => doAbortCommand(eqpt, eqpt.OHTC_CMD, CMDCancelType.CmdCancelIdReadFailed));

                    break;
                case BCRReadResult.BcrNormal:
                    if (!checkHasDuplicateHappend(bcfApp, eqpt, seqNum, eventType, read_carrier_id, old_carrier_id))
                    {
                        scApp.VehicleBLL.updataVehicleCSTID(eqpt.VEHICLE_ID, read_carrier_id);
                        replyTranEventReport(bcfApp, eventType, eqpt, seqNum);
                    }
                    break;
            }

            if (is_need_report_install)
            {
                List<AMCSREPORTQUEUE> reportqueues = new List<AMCSREPORTQUEUE>();
                scApp.ReportBLL.newReportCarrierIDReadReport(eqpt.VEHICLE_ID, reportqueues);
                //收到LoadComplete先不上報給Host，等BCRRead才報。
                scApp.ReportBLL.newReportLoadComplete(eqpt.VEHICLE_ID, eqpt.BCRReadResult, reportqueues);
                scApp.ReportBLL.insertMCSReport(reportqueues);
                scApp.ReportBLL.newSendMCSMessage(reportqueues);
            }
        }

        private static bool CheckIsNeedReportInstall2MCS(AVEHICLE eqpt, AVIDINFO vid_info)
        {
            //bool is_need_report_install = false;
            bool is_need_report_install = true;
            if (SCUtility.isEmpty(eqpt.MCS_CMD))
                return false;
            //if (vid_info != null)
            //{
            //    if (!SCUtility.isMatche(eqpt.Real_ID, vid_info.CARRIER_LOC))
            //    {
            //        is_need_report_install = true;
            //    }
            //}
            return is_need_report_install;
        }


        private bool checkHasDuplicateHappend(BCFApplication bcfApp, AVEHICLE eqpt, int seqNum, EventType eventType, string read_carrier_id, string oldCarrierID)
        {
            bool is_happend = false;
            //AVEHICLE vh = scApp.VehicleBLL.cache.getVhByCSTID(read_carrier_id);
            int has_carry_this_cst_of_vh = scApp.VehicleBLL.cache.getVhByHasCSTIDCount(read_carrier_id);
            if (DebugParameter.TestDuplicate || has_carry_this_cst_of_vh >= 2)
            {
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: $" Carrier duplicate happend,start abort command id:{eqpt.OHTC_CMD?.Trim()},and check is need rename cst id:{oldCarrierID}...",
                   VehicleID: eqpt.VEHICLE_ID,
                   CarrierID: eqpt.CST_ID);
                bool was_renamed = oldCarrierID.StartsWith("UNKNOWNDUP");

                string rename_duplicate_carrier_id = was_renamed ?
                    oldCarrierID :
                    $"UNKNOWNDUP-{read_carrier_id}-{DateTime.Now.ToString(SCAppConstants.TimestampFormat_12)}001";//固定加入001的Sequence
                scApp.VehicleBLL.updataVehicleCSTID(eqpt.VEHICLE_ID, rename_duplicate_carrier_id);
                replyTranEventReport(bcfApp, eventType, eqpt, seqNum,
                            renameCarrierID: rename_duplicate_carrier_id, cancelType: CMDCancelType.CmdCancelIdReadDuplicate);
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: $" Carrier duplicate happend,start abort command id:{eqpt.OHTC_CMD?.Trim()},and check is need rename cst id:{oldCarrierID} to {rename_duplicate_carrier_id}",
                   VehicleID: eqpt.VEHICLE_ID,
                   CarrierID: eqpt.CST_ID);

                is_happend = true;
            }
            return is_happend;
        }

        //private void PositionReport_BlockReq(BCFApplication bcfApp, AVEHICLE eqpt, int seqNum, string req_block_id)
        //{
        //    bool isSucess = true;
        //    bool canPass = false;
        //    LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
        //       Data: $"Process block request,request block id:{req_block_id}",
        //       VehicleID: eqpt.VEHICLE_ID,
        //       CarrierID: eqpt.CST_ID);

        //    //判斷是否有重覆要這個Block
        //    //if (!isReqBlockAgain)
        //    //{
        //    //    List<BLOCKZONEQUEUE> sameVhNotReleaseblockZoneQueues = null;
        //    //    //判斷是否有要了其他的Block未釋放

        //    //    if (checkHasOrtherBolckZoneQueueNonRelease(eqpt, out sameVhNotReleaseblockZoneQueues))
        //    //    {
        //    //        //using (TransactionScope tx = new TransactionScope())
        //    //        //using (TransactionScope tx = new
        //    //        //    TransactionScope(TransactionScopeOption.Required, new TransactionOptions { IsolationLevel = IsolationLevel.ReadUncommitted }))
        //    //        List<KeyValuePair<string, string>> ReleaseBlocks = new List<KeyValuePair<string, string>>();
        //    //        using (TransactionScope tx = SCUtility.getTransactionScope())
        //    //        {
        //    //            using (DBConnection_EF con = DBConnection_EF.GetUContext())
        //    //            {
        //    //                //con.BeginTransaction();
        //    //                isSucess = true;
        //    //                foreach (BLOCKZONEQUEUE queue in sameVhNotReleaseblockZoneQueues)
        //    //                {
        //    //                    isSucess &= scApp.MapBLL.updateBlockZoneQueue_AbnormalEnd(queue,
        //    //                        SCAppConstants.BlockQueueState.Abnormal_Release_OrtherNonRelease);
        //    //                    isSucess &= scApp.MapBLL.NoticeBlockVhPassByEntrySecID(queue.ENTRY_SEC_ID);
        //    //                    ReleaseBlocks.Add(new KeyValuePair<string, string>(queue.CAR_ID, queue.ENTRY_SEC_ID));
        //    //                }
        //    //                if (isSucess)
        //    //                {
        //    //                    tx.Complete();
        //    //                    foreach (var keyValue in ReleaseBlocks)
        //    //                    {
        //    //                        scApp.MapBLL.DeleteBlockControlKeyWordToRedis(keyValue.Key);
        //    //                    }
        //    //                }
        //    //                else
        //    //                {
        //    //                    //return;
        //    //                }
        //    //            }
        //    //        }
        //    //    }
        //    //}
        //    if (DebugParameter.isForcedPassBlockControl)
        //    {
        //        LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
        //           Data: "test flag: Force pass block control is open, will driect reply to vh can pass block",
        //           VehicleID: eqpt.VEHICLE_ID,
        //           CarrierID: eqpt.CST_ID);

        //        reply_Trans_Event_Report(bcfApp, EventType.BlockReq, eqpt, seqNum, canBlockPass: true);
        //        return;
        //    }
        //    if (DebugParameter.isForcedRejectBlockControl)
        //    {
        //        LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
        //           Data: "test flag: Force reject block control is open, will driect reply to vh can't pass block",
        //           VehicleID: eqpt.VEHICLE_ID,
        //           CarrierID: eqpt.CST_ID);

        //        reply_Trans_Event_Report(bcfApp, EventType.BlockReq, eqpt, seqNum, canBlockPass: false);
        //        return;
        //    }

        //    string current_block_id_status = string.Empty;
        //    bool hasAskOrtherBlock =
        //        scApp.MapBLL.HasOrtherBlockControlAskedFromRedis(eqpt.VEHICLE_ID, req_block_id, out current_block_id_status);
        //    if (hasAskOrtherBlock)
        //    {
        //        LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
        //           Data: "this vh has ask orther block,so can't request current block",
        //           VehicleID: eqpt.VEHICLE_ID,
        //           CarrierID: eqpt.CST_ID);

        //        LogCollection.BlockControlLogger.Trace($"vh id:{eqpt.VEHICLE_ID} has ask orther block.");
        //        reply_Trans_Event_Report(bcfApp, EventType.BlockReq, eqpt, seqNum, canBlockPass: false);
        //        return;
        //    }

        //    //bool isBlocking = scApp.MapBLL.isBlockingBlockZoneByVhIDAndCrtBlockSecID(eqpt.VEHICLE_ID, req_block_id);
        //    bool isBlocking = SCUtility.isMatche(current_block_id_status, SCAppConstants.BlockQueueState.Blocking)
        //                      || SCUtility.isMatche(current_block_id_status, SCAppConstants.BlockQueueState.Through);
        //    if (isBlocking)
        //    {
        //        LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
        //           Data: $"vh get block id:{req_block_id} again!",
        //           VehicleID: eqpt.VEHICLE_ID,
        //           CarrierID: eqpt.CST_ID);
        //        reply_Trans_Event_Report(bcfApp, EventType.BlockReq, eqpt, seqNum, canBlockPass: true);
        //        return;
        //    }
        //    else
        //    {
        //        //using (TransactionScope tx = new
        //        //    TransactionScope(TransactionScopeOption.Required, new TransactionOptions { IsolationLevel = SCAppConstants.ISOLATION_LEVEL }))
        //        using (TransactionScope tx = SCUtility.getTransactionScope())
        //        {
        //            using (DBConnection_EF con = DBConnection_EF.GetUContext())
        //            {
        //                //if (!isReqBlockAgain)
        //                if (!SCUtility.isMatche(current_block_id_status, SCAppConstants.BlockQueueState.Request))
        //                {
        //                    //先確認他所上報的SEC ID 是Block的SEC ID
        //                    ABLOCKZONEMASTER block_master = scApp.MapBLL.getBlockZoneMasterByEntrySecID(req_block_id);
        //                    if (block_master != null)
        //                    {
        //                        //確認VH是否可以通過
        //                        DateTime reqest_time = DateTime.Now;
        //                        canPass = canPassBlockZone(req_block_id);

        //                        LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
        //                           Data: $"Vh:{eqpt.VEHICLE_ID} ask block:{req_block_id},ask result:{canPass}",
        //                           VehicleID: eqpt.VEHICLE_ID,
        //                           CarrierID: eqpt.CST_ID);

        //                        scApp.MapBLL.doCreatBlockZoneQueueByReqStatus(eqpt.VEHICLE_ID, req_block_id, canPass, reqest_time);
        //                        scApp.MapBLL.CreatBlockControlKeyWordToRedis(eqpt.VEHICLE_ID, req_block_id, canPass, reqest_time);
        //                    }
        //                    else
        //                    {
        //                        LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
        //                           Data: $"Vh:{eqpt.VEHICLE_ID} ask block:{req_block_id},but this block id not exist!",
        //                           VehicleID: eqpt.VEHICLE_ID,
        //                           CarrierID: eqpt.CST_ID);

        //                        logger.Warn("vh:{0} req block id not exist {1}", eqpt.VEHICLE_ID, req_block_id);

        //                        canPass = false;
        //                    }
        //                }
        //                else
        //                {
        //                    canPass = canPassBlockZone(req_block_id);

        //                    LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
        //                       Data: $"Vh:{eqpt.VEHICLE_ID} ask again block:{req_block_id},ask result:{canPass}",
        //                       VehicleID: eqpt.VEHICLE_ID,
        //                       CarrierID: eqpt.CST_ID);
        //                    if (canPass)
        //                    {
        //                        if (scApp.MapBLL.IsBlockControlStatus
        //                            (eqpt.VEHICLE_ID, SCAppConstants.BlockQueueState.Request))
        //                        {
        //                            scApp.MapBLL.updateBlockZoneQueue_BlockTime(eqpt.VEHICLE_ID, req_block_id);
        //                            scApp.MapBLL.ChangeBlockControlStatus_Blocking(eqpt.VEHICLE_ID);
        //                        }
        //                    }
        //                }

        //                Boolean resp_cmp = reply_Trans_Event_Report(bcfApp, EventType.BlockReq, eqpt, seqNum, canPass);

        //                if (resp_cmp)
        //                {
        //                    tx.Complete();
        //                }
        //                else
        //                {
        //                    //con.Rollback();
        //                    return;
        //                }
        //            }
        //        }
        //    }
        //}



        private void ProcessBlockOrHIDReq(BCFApplication bcfApp, AVEHICLE eqpt, EventType eventType, int seqNum, string req_block_id, string req_hid_secid)
        {
            bool can_block_pass = true;
            bool can_hid_pass = true;
            bool isSuccess = false;
            using (TransactionScope tx = SCUtility.getTransactionScope())
            {
                if (eventType == EventType.BlockReq || eventType == EventType.BlockHidreq)
                    can_block_pass = ProcessBlockReqNew(bcfApp, eqpt, req_block_id);
                if (eventType == EventType.Hidreq || eventType == EventType.BlockHidreq)
                    can_hid_pass = ProcessHIDRequest(bcfApp, eqpt, req_hid_secid);
                isSuccess = replyTranEventReport(bcfApp, eventType, eqpt, seqNum, canBlockPass: can_block_pass, canHIDPass: can_hid_pass);
                if (isSuccess)
                {
                    tx.Complete();
                }
            }

            //if (isSuccess &&
            //    (eventType == EventType.Hidreq || eventType == EventType.BlockHidreq))
            //{
            //    scApp.HIDBLL.VHEntryHIDZone(req_hid_secid);
            //    Task.Run(() => checkHIDSpaceIsSufficient(eqpt, req_hid_secid));
            //}
        }

        //private bool ProcessBlockReq(BCFApplication bcfApp, AVEHICLE eqpt, string req_block_id)
        //{
        //    bool canBlockPass = false;
        //    LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
        //       Data: $"Process block request,request block id:{req_block_id}",
        //       VehicleID: eqpt.VEHICLE_ID,
        //       CarrierID: eqpt.CST_ID);
        //    if (DebugParameter.isForcedPassBlockControl)
        //    {
        //        LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
        //           Data: "test flag: Force pass block control is open, will driect reply to vh can pass block",
        //           VehicleID: eqpt.VEHICLE_ID,
        //           CarrierID: eqpt.CST_ID);
        //        canBlockPass = true;
        //    }
        //    else if (DebugParameter.isForcedRejectBlockControl)
        //    {
        //        LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
        //           Data: "test flag: Force reject block control is open, will driect reply to vh can't pass block",
        //           VehicleID: eqpt.VEHICLE_ID,
        //           CarrierID: eqpt.CST_ID);
        //        canBlockPass = false;
        //    }
        //    else
        //    {
        //        using (DBConnection_EF con = DBConnection_EF.GetUContext())
        //        {
        //            //先確認在Redis上是否該台VH 已經有要過的Block
        //            bool hasAskedBlock = scApp.MapBLL.HasBlockControlAskedFromRedis
        //                (eqpt.VEHICLE_ID, out string current_asked_block_id, out string current_asked_block_status);
        //            if (hasAskedBlock)
        //            {
        //                bool isBlocking = SCUtility.isMatche(current_asked_block_status, SCAppConstants.BlockQueueState.Blocking)
        //                               || SCUtility.isMatche(current_asked_block_status, SCAppConstants.BlockQueueState.Through);
        //                //確認當前要的Block與目前Redis上存放的是不是同一個
        //                if (SCUtility.isMatche(req_block_id, current_asked_block_id))
        //                {
        //                    //如果要的是同一個，則確認是否已經給該台VH
        //                    if (isBlocking)
        //                    {
        //                        //如果已經給過該台VH通行權，則直接讓它通過。
        //                        canBlockPass = true;
        //                        LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
        //                           Data: $"Vh:{eqpt.VEHICLE_ID} ask again block:{req_block_id},but it is the owner so ask result:{canBlockPass}",
        //                           VehicleID: eqpt.VEHICLE_ID,
        //                           CarrierID: eqpt.CST_ID);
        //                    }
        //                    else
        //                    {
        //                        //如果還沒有給過該台VH通行權，則需再判斷一次該Vh是否已經可以通過
        //                        canBlockPass = canPassBlockZone(eqpt, req_block_id);
        //                        LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
        //                           Data: $"Vh:{eqpt.VEHICLE_ID} ask again block:{req_block_id},ask result:{canBlockPass}",
        //                           VehicleID: eqpt.VEHICLE_ID,
        //                           CarrierID: eqpt.CST_ID);
        //                        if (canBlockPass)
        //                        {
        //                            scApp.MapBLL.updateBlockZoneQueue_BlockTime(eqpt.VEHICLE_ID, req_block_id);
        //                            scApp.MapBLL.ChangeBlockControlStatus_Blocking(eqpt.VEHICLE_ID);
        //                        }
        //                    }
        //                }
        //                else
        //                {
        //                    //如果不是同一個，則要判斷目前asked的Block狀態是否已經是Blocking或Through，                           
        //                    if (isBlocking)
        //                    {
        //                        //如果是的話才可以再進行新的BlockControlRequest的建立流程
        //                        canBlockPass = tryCreatBlockControlRequest(eqpt, req_block_id);
        //                        LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
        //                           Data: $"Vh:{eqpt.VEHICLE_ID} already has a block:{current_asked_block_id}," +
        //                           $"asking for another one at a time ,block:{req_block_id}, ask result:{canBlockPass}",
        //                           VehicleID: eqpt.VEHICLE_ID,
        //                           CarrierID: eqpt.CST_ID);
        //                    }
        //                    else
        //                    {
        //                        //如果不是，則不可以再給他另外一個Block
        //                        canBlockPass = false;
        //                        LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
        //                           Data: $"Vh:{eqpt.VEHICLE_ID} already has a block:{current_asked_block_id}," +
        //                           $"but the status is Request,so ask block:{req_block_id} result:{canBlockPass}",
        //                           VehicleID: eqpt.VEHICLE_ID,
        //                           CarrierID: eqpt.CST_ID);
        //                        DateTime reqest_time = DateTime.Now;
        //                        //scApp.MapBLL.doCreatBlockZoneQueueByReqStatus(eqpt.VEHICLE_ID, req_block_id, canBlockPass, reqest_time);
        //                        //scApp.MapBLL.CreatBlockControlKeyWordToRedis(eqpt.VEHICLE_ID, req_block_id, canBlockPass, reqest_time);
        //                    }
        //                }
        //            }
        //            else
        //            {
        //                //如果目前Redis上沒有要求的Block的話，則可以嘗試建立新的BlocControlRequest，
        //                //並判斷是否可以給其通行權
        //                canBlockPass = tryCreatBlockControlRequest(eqpt, req_block_id);
        //                LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
        //                   Data: $"Vh:{eqpt.VEHICLE_ID} ask block:{req_block_id},ask result:{canBlockPass}",
        //                   VehicleID: eqpt.VEHICLE_ID,
        //                   CarrierID: eqpt.CST_ID);
        //            }
        //        }
        //    }
        //    return canBlockPass;
        //}


        public bool ProcessBlockReqTest(BCFApplication bcfApp, AVEHICLE eqpt, string req_block_id)
        {
            return ProcessBlockReqNew(bcfApp, eqpt, req_block_id);
        }


        private bool ProcessBlockReqNew(BCFApplication bcfApp, AVEHICLE eqpt, string req_block_id)
        {
            bool canBlockPass = false;
            LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
               Data: $"Process block request,request block id:{req_block_id}",
               VehicleID: eqpt.VEHICLE_ID,
               CarrierID: eqpt.CST_ID);
            if (DebugParameter.isForcedPassBlockControl)
            {
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: "test flag: Force pass block control is open, will driect reply to vh can pass block",
                   VehicleID: eqpt.VEHICLE_ID,
                   CarrierID: eqpt.CST_ID);
                canBlockPass = true;
            }
            else if (DebugParameter.isForcedRejectBlockControl)
            {
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: "test flag: Force reject block control is open, will driect reply to vh can't pass block",
                   VehicleID: eqpt.VEHICLE_ID,
                   CarrierID: eqpt.CST_ID);
                canBlockPass = false;
            }
            else
            {
                lock (block_control_lock_obj)
                {
                    using (DBConnection_EF con = DBConnection_EF.GetUContext())
                    {
                        //先確認在Redis上是否該台VH 已經有要過的Block
                        //bool hasAskedBlock = scApp.MapBLL.HasBlockControlAskedFromRedis
                        //    (eqpt.VEHICLE_ID, out string current_asked_block_id, out string current_asked_block_status);
                        List<BLOCKZONEQUEUE> ask_block_queues = scApp.MapBLL.loadNonReleaseBlockQueueByCarID(eqpt.VEHICLE_ID);
                        bool hasAskedBlock = ask_block_queues != null && ask_block_queues.Count > 0;
                        if (hasAskedBlock)
                        {
                            //bool isBlocking = SCUtility.isMatche(current_asked_block_status, SCAppConstants.BlockQueueState.Blocking)
                            //               || SCUtility.isMatche(current_asked_block_status, SCAppConstants.BlockQueueState.Through);

                            //確認當前要的Block是否有存在目前的DB中。
                            BLOCKZONEQUEUE current_request_again_block_queue = ask_block_queues.
                                                                         Where(queue => SCUtility.isMatche(queue.ENTRY_SEC_ID, req_block_id)).
                                                                         FirstOrDefault();
                            //if (SCUtility.isMatche(req_block_id, current_asked_block_id))
                            if (current_request_again_block_queue != null)
                            {
                                //如果要的是同一個，則確認是否已經給該台VH
                                //if (isBlocking)
                                if (SCUtility.isMatche(current_request_again_block_queue.STATUS, SCAppConstants.BlockQueueState.Blocking) ||
                                    SCUtility.isMatche(current_request_again_block_queue.STATUS, SCAppConstants.BlockQueueState.Through))
                                {
                                    //如果已經給過該台VH通行權，則直接讓它通過。
                                    canBlockPass = true;
                                    LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                                       Data: $"Vh:{eqpt.VEHICLE_ID} ask again block:{req_block_id},but it is the owner so ask result:{canBlockPass}",
                                       VehicleID: eqpt.VEHICLE_ID,
                                       CarrierID: eqpt.CST_ID);
                                }
                                else
                                {
                                    //如果還沒有給過該台VH通行權，則需再判斷一次該Vh是否已經可以通過
                                    canBlockPass = canPassBlockZone(eqpt, req_block_id);
                                    LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                                       Data: $"Vh:{eqpt.VEHICLE_ID} ask again block:{req_block_id},ask result:{canBlockPass}",
                                       VehicleID: eqpt.VEHICLE_ID,
                                       CarrierID: eqpt.CST_ID);
                                    if (canBlockPass)
                                    {
                                        scApp.MapBLL.updateBlockZoneQueue_BlockTime(eqpt.VEHICLE_ID, req_block_id);
                                        scApp.MapBLL.ChangeBlockControlStatus_Blocking(eqpt.VEHICLE_ID, req_block_id, DateTime.Now);
                                    }
                                }
                            }
                            else
                            {
                                bool has_in_request = ask_block_queues.Where(queue => SCUtility.isMatche(queue.STATUS, SCAppConstants.BlockQueueState.Request))
                                                                      .Count() > 0;
                                string[] current_using_block_ids = ask_block_queues.Select(queue => queue.ENTRY_SEC_ID).ToArray();
                                //如果不是同一個，則要判斷目前asked的Blocks狀態是否沒有在Request中的                           
                                //if (isBlocking)
                                if (!has_in_request)
                                {
                                    //如果是的話才可以再進行新的BlockControlRequest的建立流程
                                    canBlockPass = tryCreatBlockControlRequest(eqpt, req_block_id);
                                    LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                                       Data: $"Vh:{eqpt.VEHICLE_ID} already has a block:{string.Join(",", current_using_block_ids)}," +
                                       $"asking for another one at a time ,block:{req_block_id}, ask result:{canBlockPass}",
                                       VehicleID: eqpt.VEHICLE_ID,
                                       CarrierID: eqpt.CST_ID);
                                }
                                else
                                {
                                    //如果不是，則不可以再給他另外一個Block
                                    canBlockPass = false;
                                    LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                                       Data: $"Vh:{eqpt.VEHICLE_ID} already has a block:{string.Join(",", current_using_block_ids)}," +
                                       $"but the status is Request,so ask block:{req_block_id} result:{canBlockPass}",
                                       VehicleID: eqpt.VEHICLE_ID,
                                       CarrierID: eqpt.CST_ID);
                                    DateTime reqest_time = DateTime.Now;
                                    //scApp.MapBLL.doCreatBlockZoneQueueByReqStatus(eqpt.VEHICLE_ID, req_block_id, canBlockPass, reqest_time);
                                    //scApp.MapBLL.CreatBlockControlKeyWordToRedis(eqpt.VEHICLE_ID, req_block_id, canBlockPass, reqest_time);
                                }
                            }
                        }
                        else
                        {
                            //如果目前Redis上沒有要求的Block的話，則可以嘗試建立新的BlocControlRequest，
                            //並判斷是否可以給其通行權
                            canBlockPass = tryCreatBlockControlRequest(eqpt, req_block_id);
                            LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                               Data: $"Vh:{eqpt.VEHICLE_ID} ask block:{req_block_id},ask result:{canBlockPass}",
                               VehicleID: eqpt.VEHICLE_ID,
                               CarrierID: eqpt.CST_ID);
                        }
                    }
                }
            }
            return canBlockPass;
        }

        private bool ProcessHIDRequest(BCFApplication bcfApp, AVEHICLE eqpt, string req_hid_secid)
        {
            bool isSuccess = true;
            using (DBConnection_EF con = DBConnection_EF.GetUContext())
            {
                DateTime req_dateTime = DateTime.Now;

                scApp.HIDBLL.doCreatHIDZoneQueueByReqStatus(eqpt.VEHICLE_ID, req_hid_secid, true, req_dateTime);
            }
            return isSuccess;
        }


        private bool tryCreatBlockControlRequest(AVEHICLE eqpt, string req_block_id)
        {
            bool canPass;
            ABLOCKZONEMASTER block_master = scApp.MapBLL.getBlockZoneMasterByEntrySecID(req_block_id);
            if (block_master != null)
            {
                //確認VH是否可以通過
                DateTime reqest_time = DateTime.Now;
                canPass = canPassBlockZone(eqpt, req_block_id);
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: $"Vh:{eqpt.VEHICLE_ID} ask block:{req_block_id},ask result:{canPass}",
                   VehicleID: eqpt.VEHICLE_ID,
                   CarrierID: eqpt.CST_ID);
                scApp.MapBLL.doCreatBlockZoneQueueByReqStatus(eqpt.VEHICLE_ID, req_block_id, canPass, reqest_time);
                scApp.MapBLL.CreatBlockControlKeyWordToRedis(eqpt.VEHICLE_ID, req_block_id, canPass, reqest_time);
            }
            else
            {
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: $"Vh:{eqpt.VEHICLE_ID} ask block:{req_block_id},but this block id not exist!",
                   VehicleID: eqpt.VEHICLE_ID,
                   CarrierID: eqpt.CST_ID);
                logger.Warn("vh:{0} req block id not exist {1}", eqpt.VEHICLE_ID, req_block_id);
                canPass = false;
            }

            return canPass;
        }

        private void PositionReport_HIDRequest(BCFApplication bcfApp, AVEHICLE eqpt, int seqNum, string req_hid_secid)
        {
            using (TransactionScope tx = SCUtility.getTransactionScope())
            {
                using (DBConnection_EF con = DBConnection_EF.GetUContext())
                {
                    bool canHIDPass = true;
                    DateTime req_dateTime = DateTime.Now;
                    //canPass = scApp.HIDBLL.hasEnoughSeat(req_hid_id);
                    //if (canPass)
                    //{
                    //    scApp.HIDBLL.VHEntryHIDZone(req_hid_id);
                    //}

                    scApp.HIDBLL.doCreatHIDZoneQueueByReqStatus(eqpt.VEHICLE_ID, req_hid_secid, canHIDPass, req_dateTime);

                    Boolean resp_cmp = replyTranEventReport(bcfApp, EventType.Hidreq, eqpt, seqNum, canHIDPass: canHIDPass);
                    if (resp_cmp)
                    {
                        tx.Complete();
                        scApp.HIDBLL.VHEntryHIDZone(req_hid_secid);
                    }
                    else
                    {
                        return;
                    }
                }
            }
            //Boolean resp_cmp = reply_Trans_Event_Report(bcfApp, EventType.Hidrelease, eqpt, seqNum, true);

            Task.Run(() => checkHIDSpaceIsSufficient(eqpt, req_hid_secid));
        }

        private void checkHIDSpaceIsSufficient(AVEHICLE eqpt, string req_hid_secid)
        {
            try
            {
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                             Data: $"Start check HID:{req_hid_secid},has enough seat...",
                             VehicleID: eqpt.VEHICLE_ID,
                             CarrierID: eqpt.CST_ID);
                bool isEnough = scApp.HIDBLL.hasEnoughSeat(req_hid_secid, out long current_vh_count, out int hid_zone_max_load_count);
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: $"Check HID:{req_hid_secid} has enough seat,{nameof(current_vh_count)}:{current_vh_count},{nameof(hid_zone_max_load_count)}:{hid_zone_max_load_count},result:{isEnough}",
                   VehicleID: eqpt.VEHICLE_ID,
                   CarrierID: eqpt.CST_ID);
                if (!isEnough)
                {
                    using (TransactionScope tx = SCUtility.getTransactionScope())
                    {
                        using (DBConnection_EF con = DBConnection_EF.GetUContext())
                        {
                            scApp.HIDBLL.updateHIDZoneQueue_Pasue(eqpt.VEHICLE_ID, req_hid_secid, true);
                            //if (eqpt.sned_Str39(PauseEvent.Pause, PauseType.Hid))
                            if (PauseRequest(eqpt.VEHICLE_ID, PauseEvent.Pause, OHxCPauseType.ManualHID))
                            {
                                tx.Complete();
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        private void PositionReport_ArriveAndComplete(BCFApplication bcfApp, AVEHICLE eqpt, int seqNum
                                                    , EventType eventType, string current_adr_id, string current_sec_id, string carrier_id)
        {
            LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
               Data: $"Process report {eventType}",
               VehicleID: eqpt.VEHICLE_ID,
               CarrierID: eqpt.CST_ID);
            //using (TransactionScope tx = new TransactionScope())
            //using (TransactionScope tx = new
            //    TransactionScope(TransactionScopeOption.Required, new TransactionOptions { IsolationLevel = IsolationLevel.ReadUncommitted }))


            switch (eventType)
            {
                case EventType.LoadArrivals:
                    if (!SCUtility.isEmpty(eqpt.MCS_CMD))
                    {
                        scApp.CMDBLL.updateCMD_MCS_CmdStatus2LoadArrivals(eqpt.MCS_CMD);
                    }
                    scApp.VIDBLL.upDateVIDPortID(eqpt.VEHICLE_ID, eqpt.CUR_ADR_ID);
                    break;
                case EventType.UnloadArrivals:
                    if (!SCUtility.isEmpty(eqpt.MCS_CMD))
                    {
                        scApp.CMDBLL.updateCMD_MCS_CmdStatus2UnloadArrive(eqpt.MCS_CMD);
                    }
                    scApp.VIDBLL.upDateVIDPortID(eqpt.VEHICLE_ID, eqpt.CUR_ADR_ID);
                    break;
                case EventType.LoadComplete:
                    scApp.VIDBLL.upDateVIDCarrierLocInfo(eqpt.VEHICLE_ID, eqpt.Real_ID);
                    if (!SCUtility.isEmpty(eqpt.MCS_CMD))
                    {
                        using (DBConnection_EF con = DBConnection_EF.GetUContext())
                        {
                            scApp.CMDBLL.updateCMD_MCS_TranStatus2Transferring(eqpt.MCS_CMD);
                            scApp.CMDBLL.updateCMD_MCS_CmdStatus2LoadComplete(eqpt.MCS_CMD);
                        }
                    }
                    scApp.PortBLL.OperateCatch.updatePortStationCSTExistStatus(eqpt.CUR_ADR_ID, string.Empty);
                    //CarrierInterfaceSim_LoadComplete(eqpt);
                    break;
                case EventType.UnloadComplete:
                    if (!SCUtility.isEmpty(eqpt.MCS_CMD))
                    {
                        scApp.CMDBLL.updateCMD_MCS_CmdStatus2UnloadComplete(eqpt.MCS_CMD);
                    }
                    var port_station = scApp.MapBLL.getPortByAdrID(current_adr_id);//要考慮到一個Address會有多個Port的問題
                    if (port_station != null)
                    {
                        scApp.VIDBLL.upDateVIDCarrierLocInfo(eqpt.VEHICLE_ID, port_station.PORT_ID);
                    }
                    //scApp.PortBLL.OperateCatch.updatePortStationCSTExistStatus(eqpt.CUR_ADR_ID, carrier_id);
                    // CarrierInterfaceSim_UnloadComplete(eqpt, eqpt.CST_ID);
                    break;
            }

            List<AMCSREPORTQUEUE> reportqueues = new List<AMCSREPORTQUEUE>();
            using (TransactionScope tx = SCUtility.getTransactionScope())
            {
                using (DBConnection_EF con = DBConnection_EF.GetUContext())
                {
                    //using (TransactionScope tx = new
                    //    TransactionScope(TransactionScopeOption.Required, new TransactionOptions { IsolationLevel = SCAppConstants.ISOLATION_LEVEL }))
                    //con.BeginTransaction();
                    if (!SCUtility.isEmpty(eqpt.MCS_CMD))
                    {
                        LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                           Data: $"do report {eventType} to mcs.",
                           VehicleID: eqpt.VEHICLE_ID,
                           CarrierID: eqpt.CST_ID);
                        bool isCreatReportInfoSuccess = false;
                        switch (eventType)
                        {
                            case EventType.LoadArrivals:
                                isCreatReportInfoSuccess = scApp.ReportBLL.newReportLoadArrivals(eqpt.VEHICLE_ID, reportqueues);
                                break;
                            case EventType.LoadComplete:
                                //收到LoadComplete先不上報給Host，等BCRRead才報。
                                //isCreatReportInfoSuccess = scApp.ReportBLL.newReportLoadComplete(eqpt.VEHICLE_ID, eqpt.BCRReadResult, reportqueues);
                                isCreatReportInfoSuccess = true;
                                break;
                            case EventType.UnloadArrivals:
                                isCreatReportInfoSuccess = scApp.ReportBLL.newReportUnloadArrivals(eqpt.VEHICLE_ID, reportqueues);
                                break;
                            case EventType.UnloadComplete:
                                isCreatReportInfoSuccess = scApp.ReportBLL.newReportUnloadComplete(eqpt.VEHICLE_ID, reportqueues);
                                break;
                            default:
                                isCreatReportInfoSuccess = true;
                                break;
                        }
                        if (!isCreatReportInfoSuccess)
                        {
                            return;
                        }
                        scApp.ReportBLL.insertMCSReport(reportqueues);
                    }

                    Boolean resp_cmp = replyTranEventReport(bcfApp, eventType, eqpt, seqNum);

                    if (resp_cmp)
                    {
                        tx.Complete();
                    }
                    else
                    {
                        //con.Rollback();
                        return;
                    }
                }
            }
            scApp.ReportBLL.newSendMCSMessage(reportqueues);
            SpinWait.SpinUntil(() => false, 2000);
            switch (eventType)
            {
                case EventType.LoadArrivals:
                    scApp.VehicleBLL.doLoadArrivals(eqpt.VEHICLE_ID, current_adr_id, current_sec_id);
                    break;
                case EventType.LoadComplete:
                    scApp.VehicleBLL.doLoadComplete(eqpt.VEHICLE_ID, current_adr_id, current_sec_id, carrier_id);
                    Task.Run(() => scApp.FlexsimCommandDao.setVhEventTypeToFlexsimDB(eqpt.VEHICLE_ID, eventType));
                    break;
                case EventType.UnloadArrivals:
                    scApp.VehicleBLL.doUnloadArrivals(eqpt.VEHICLE_ID, current_adr_id, current_sec_id);
                    break;
                case EventType.UnloadComplete:
                    scApp.VehicleBLL.doUnloadComplete(eqpt.VEHICLE_ID);
                    Task.Run(() => scApp.FlexsimCommandDao.setVhEventTypeToFlexsimDB(eqpt.VEHICLE_ID, eventType));
                    break;
            }
        }

        private void PositionReport_AdrPassArrivals(BCFApplication bcfApp, AVEHICLE eqpt, ID_134_TRANS_EVENT_REP recive_str, string last_adr_id, string last_sec_id)
        {
            string current_adr_id = recive_str.CurrentAdrID;
            string current_sec_id = recive_str.CurrentSecID;
            switch (recive_str.EventType)
            {
                case EventType.AdrPass:
                    //updateCMDDetailEntryAndLeaveTime(eqpt, current_adr_id, current_sec_id, last_adr_id, last_sec_id);
                    //TODO 要改成直接查詢Queue的Table就好，不用再帶SEC ID進去。
                    if (!SCUtility.isEmpty(current_sec_id))
                    {
                        lock (eqpt.BlockControl_SyncForRedis)
                        {
                            if (scApp.MapBLL.IsBlockControlStatus
                                (eqpt.VEHICLE_ID, current_sec_id, SCAppConstants.BlockQueueState.Blocking))
                            {
                                BLOCKZONEQUEUE throuBlockQueue = null;
                                if (scApp.MapBLL.updateBlockZoneQueue_ThrouTime(eqpt.VEHICLE_ID, out throuBlockQueue))
                                {
                                    scApp.MapBLL.ChangeBlockControlStatus_Through(eqpt.VEHICLE_ID, current_sec_id, DateTime.Now);
                                }
                            }
                        }
                    }
                    //BLOCKZONEQUEUE throuBlockQueue = null;
                    //scApp.MapBLL.updateBlockZoneQueue_ThrouTime(eqpt.VEHICLE_ID, out throuBlockQueue);
                    //if (throuBlockQueue != null)
                    //    return;
                    break;
                case EventType.AdrOrMoveArrivals:
                    scApp.VehicleBLL.doAdrArrivals(eqpt.VEHICLE_ID, current_adr_id, current_sec_id);
                    break;
            }
        }
        private bool replyTranEventReport(BCFApplication bcfApp, EventType eventType, AVEHICLE eqpt, int seq_num, bool canBlockPass = true, bool canHIDPass = true,
                                          string renameCarrierID = "", CMDCancelType cancelType = CMDCancelType.CmdNone)
        {
            ID_36_TRANS_EVENT_RESPONSE send_str = new ID_36_TRANS_EVENT_RESPONSE
            {
                IsBlockPass = canBlockPass ? PassType.Pass : PassType.Block,
                IsHIDPass = canHIDPass ? PassType.Pass : PassType.Block,
                ReplyCode = 0,
                RenameCarrierID = renameCarrierID,
                ReplyActiveType = cancelType
            };
            WrapperMessage wrapper = new WrapperMessage
            {
                SeqNum = seq_num,
                ImpTransEventResp = send_str
            };
            //Boolean resp_cmp = ITcpIpControl.sendGoogleMsg(bcfApp, eqpt.TcpIpAgentName, wrapper, true);

            LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
              seq_num: seq_num, Data: send_str,
              VehicleID: eqpt.VEHICLE_ID,
              CarrierID: eqpt.CST_ID);
            Boolean resp_cmp = eqpt.sendMessage(wrapper, true);
            SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, seq_num, send_str, resp_cmp.ToString());
            return resp_cmp;
        }
        private bool checkHasOrtherBolckZoneQueueNonRelease(AVEHICLE eqpt, out List<BLOCKZONEQUEUE> blockZoneQueues)
        {
            blockZoneQueues = scApp.MapBLL.loadNonReleaseBlockQueueByCarID(eqpt.VEHICLE_ID);
            if (blockZoneQueues != null && blockZoneQueues.Count > 0)
            {
                //foreach (BLOCKZONEQUEUE queue in blockZoneQueues)
                //{
                //    scApp.MapBLL.updateBlockZoneQueue_AbnormalEnd(queue,
                //        SCAppConstants.BlockQueueState.Abnormal_Release_TimerCheck);
                //    string entry_sec_id = queue.ENTRY_SEC_ID;
                //    Task.Run(() => scApp.MapBLL.CheckAndNoticeBlockVhPassByEntrySecID(entry_sec_id));
                //}
                return true;
            }
            else
            {
                blockZoneQueues = null;
                return false;
            }
        }

        /// <summary>
        /// 用來確保Block Request、Block Release處理的先後順序
        /// </summary>
        private object block_control_lock_obj = new object();
        private bool canPassBlockZone(AVEHICLE vh, string block_sec_id)
        {
            //lock (block_control_lock_obj)
            //{
            //要透過VH的Curent Segment來確定他是否為該Segment的當前第一台VH
            //ASEGMENT current_segment = scApp.SegmentBLL.cache.GetSegment(vh.CUR_SEG_ID);
            //var check_first_vh_is_in_segment = current_segment.IsFirst(vh);
            //if (!check_first_vh_is_in_segment.isFirst)
            //{
            //    AVEHICLE first_vh = check_first_vh_is_in_segment.firstVh;
            //    string first_vh_id = first_vh == null ? string.Empty : first_vh.VEHICLE_ID;
            //    LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
            //       Data: $"Vh:{vh.VEHICLE_ID} ask block:{block_sec_id},but not first vh in segment id:{vh.CUR_SEG_ID}," +
            //             $"current first vh id:{first_vh_id}",
            //       VehicleID: vh.VEHICLE_ID,
            //       CarrierID: vh.CST_ID);
            //    return false;
            //}



            if (!isNextPassVh(vh, block_sec_id))
            {
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: $"Vh:{vh.VEHICLE_ID} request block:{block_sec_id},but it not next pass vh",
                   VehicleID: vh.VEHICLE_ID,
                   CarrierID: vh.CST_ID);
                return false;
            }


            //if (!isRepeatReqBlock)
            //{
            //    //0-1 先確認他所上報的SEC ID 是Block的SEC ID
            //    ABLOCKZONEMASTER block_master = scApp.MapBLL.getBlockZoneMasterByEntrySecID(block_sec_id);
            //    if (block_master == null)
            //    {
            //        Console.WriteLine("not block sec id {0}", block_sec_id);
            //        return false;
            //    }
            //    //0.更新BLOCKZONEQUEUE這個Table，新增一筆ENTRY_SEC_ID並填入CAR_ID、REQ_TIME
            //    scApp.MapBLL.addBlockZoneQueue(eqpt.VEHICLE_ID, block_sec_id);
            //}
            //1.查詢VH所上報的block_sec_id 是哪一個，並利用他去查詢跟他同一組的SEC_ID，
            //  ABLOCKZONEDETAIL 會記錄。
            List<string> lstSecid = scApp.MapBLL.loadBlockZoneDetailSecIDsByEntrySecID(block_sec_id);
            bool hasBlocking = false;
            string orther_vh = "";
            //1.2找出BLOCKZONEQUEUE中是否有這兩個EntrySecID
            //foreach (string sec_id in lstSecid)
            //{
            //    if (scApp.MapBLL.checkBlockZoneQueueIsBlockingByEntrySecID(sec_id))
            //    {
            //        hasBlocking = true;
            //        break;
            //    }
            //}
            //TODO 因為沒有檢查Requestung 所以 可能會有一次給兩台的疑慮
            //if (scApp.MapBLL.checkBlockZoneQueueIsBlockingByEntrySecID(lstSecid))
            if (scApp.MapBLL.checkBlockZoneQueueIsBlockingByEntrySecID(lstSecid, out List<BLOCKZONEQUEUE> queues))
            {
                foreach (var queue in queues)
                {
                    if (SCUtility.isMatche(queue.CAR_ID, vh.VEHICLE_ID))
                    {
                        bool isBlocking = SCUtility.isMatche(queue.STATUS, SCAppConstants.BlockQueueState.Blocking)
                                       || SCUtility.isMatche(queue.STATUS, SCAppConstants.BlockQueueState.Through);
                        if (isBlocking)
                        {
                            hasBlocking = false;
                        }
                        else
                        {
                            hasBlocking = true;
                            orther_vh = queue.CAR_ID;
                            break;
                        }
                    }
                    else
                    {
                        hasBlocking = true;
                        orther_vh = queue.CAR_ID;
                        break;
                    }
                }
                if (hasBlocking)
                {
                    LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                       Data: $"Vh:{vh.VEHICLE_ID} ask block:{block_sec_id},but queue has orther vh:{orther_vh} request",
                       VehicleID: vh.VEHICLE_ID,
                       CarrierID: vh.CST_ID);
                }
            }


            if (!hasBlocking)
            {
                //2.查詢AVEHICLE中的所有VH的SEC ID 是不是有在這組之中，
                //  有 - 回復的IS_BLOCK_PASS要填入 1 
                //  無 - 回復的IS_BLOCK_PASS要填入 0 
                foreach (string sec_id in lstSecid)
                {
                    List<AVEHICLE> vehicles = scApp.VehicleBLL.loadVehicleBySEC_ID(sec_id);
                    if (vehicles != null)
                    {
                        if (vehicles.Count == 1)
                        {
                            if (SCUtility.isMatche(vh.VEHICLE_ID, vehicles[0].VEHICLE_ID))
                            {
                                //如果進來問的是自己已經在Block上的話，還是可以給他通行權。
                            }
                            else
                            {
                                //如果不是則代表有其他車輛在Block內，就不可以給他通行權
                                LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                                   Data: $"Vh:{vh.VEHICLE_ID} ask block:{block_sec_id},but has vh:[{vehicles[0].VEHICLE_ID}] in current block",
                                   VehicleID: vh.VEHICLE_ID,
                                   CarrierID: vh.CST_ID);
                                hasBlocking = true;
                                break;
                            }
                        }
                        else if (vehicles.Count > 1)
                        {
                            LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                               Data: $"Vh:{vh.VEHICLE_ID} ask block:{block_sec_id},but has more vh in current block",
                               VehicleID: vh.VEHICLE_ID,
                               CarrierID: vh.CST_ID);
                            //通常Block內部最多只能有一台車，大於1台車的話一定是有問題，所以就不給他通行權
                            hasBlocking = true;
                            break;
                        }
                    }
                    //if (vehicles != null && vehicles.Count > 0)
                    //{
                    //    hasBlocking = true;
                    //    break;
                    //}
                }
                //foreach (string sec_id in lstSecid)
                //{
                //    List<AVEHICLE> vehicles = scApp.VehicleBLL.loadVehicleBySEC_ID(sec_id);
                //    if (vehicles != null && vehicles.Count > 0)
                //    {
                //        foreach (AVEHICLE vh in vehicles)
                //        {
                //            if (scApp.MapBLL.HasBlockControlAskedFromRedis
                //            (vh.VEHICLE_ID))
                //            {
                //                hasBlocking = true;
                //                break;
                //            }

                //        }
                //        if (hasBlocking)
                //        {
                //            break;
                //        }
                //    }
                //}
            }
            return !hasBlocking;
            //}
        }

        private bool isNextPassVh(AVEHICLE vh, string currentRequestBlockID)
        {
            try
            {
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: $"Start check is next pass vh...",
                   VehicleID: vh.VEHICLE_ID,
                   CarrierID: vh.CST_ID);
                bool is_next_pass_vh = false;
                ABLOCKZONEMASTER request_block_master = scApp.BlockControlBLL.cache.getBlockZoneMaster(currentRequestBlockID);
                //先判斷是不是最接近該Block的第一台車，不然會有後車先要到該Block的問題
                //  a.要先判斷在同一段Section是否有其他車輛且的他的距離在前面
                //  b.判斷是否自己已經是在該Block的前一段Section上，如果是則即為該Block的第一台Vh
                //  c.如果不是在前一段Section，則需要去找出從vh目前所在位置到該Block的Entry section中，
                //    是否有其他車輛在
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: $"Start check is closest block vh...",
                   VehicleID: vh.VEHICLE_ID,
                   CarrierID: vh.CST_ID);
                is_next_pass_vh = IsClosestBlockOfVh(vh, request_block_master);
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: $"End check is closest block vh,result:{is_next_pass_vh}",
                   VehicleID: vh.VEHICLE_ID,
                   CarrierID: vh.CST_ID);

                //如果判斷出來是該Block下一台要通過的vh，接著就要開始判斷同一組的Block
                //1.先判斷該Block是否為合流點，如果是，則需要判斷可以讓哪一邊的先走，如果不是則不用
                //2.合流點，需依照
                //  a.在等待的車子，所載的CST數量
                //  b.在等待的車子，MCS Command的Prioruty Sum的數值
                //來決定要先放行哪邊的車子
                if (is_next_pass_vh)
                {
                    if (request_block_master.BLOCK_ZONE_TYPE == E_BLOCK_ZONE_TYPE.Merge)
                    {
                        LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                           Data: $"Start check is next pass block...,block id:{currentRequestBlockID}",
                           VehicleID: vh.VEHICLE_ID,
                           CarrierID: vh.CST_ID);
                        bool is_next_pass_block = isNextPassBlock(vh, currentRequestBlockID);
                        LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                           Data: $"End check is next pass block,block id:{currentRequestBlockID}, result:{is_next_pass_block}",
                           VehicleID: vh.VEHICLE_ID,
                           CarrierID: vh.CST_ID);
                        return is_next_pass_block;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Warn, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: ex,
                   VehicleID: vh.VEHICLE_ID,
                   CarrierID: vh.CST_ID);
                return false;
            }
        }

        private bool isNextPassBlock(AVEHICLE currentRequestVH, string currentRequestBlockID)
        {
            ABLOCKZONEMASTER request_block_master = scApp.BlockControlBLL.cache.getBlockZoneMaster(currentRequestBlockID);
            List<string> block_zone_detail = request_block_master.GetBlockZoneDetailSectionIDs();
            List<BLOCKZONEQUEUE> block_queues = scApp.MapBLL.loadRequestingBlockQueueBySecIds(block_zone_detail);
            if (block_queues != null && block_queues.Count > 0)
            {
                //要確認當前的Queue是否已經有包含該次來要的vh，沒有的話也要把自己加進入作權重計算
                bool block_queues_is_include_current_req_vh = block_queues.Where(queue => SCUtility.isMatche(queue.CAR_ID, currentRequestVH.VEHICLE_ID)).Count() > 0;
                if (!block_queues_is_include_current_req_vh)
                {
                    block_queues.Add(new BLOCKZONEQUEUE()
                    { ENTRY_SEC_ID = currentRequestBlockID, CAR_ID = currentRequestVH.VEHICLE_ID, REQ_TIME = DateTime.Now });
                }
                var group_result = block_queues.GroupBy(queue => queue.ENTRY_SEC_ID);
                var group_list = group_result.ToList();
                List<string> group_block_ids = group_list.Select(group => group.Key).ToList();
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: $"start sort group block:{string.Join(",", group_block_ids)}...",
                   VehicleID: currentRequestVH.VEHICLE_ID,
                   CarrierID: currentRequestVH.CST_ID);
                group_list.Sort(BlockZoneQueueGroupCompare);
                group_block_ids = group_list.Select(group => group.Key).ToList();
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: $"end sort group block,result:{string.Join(",", group_block_ids)}.",
                   VehicleID: currentRequestVH.VEHICLE_ID,
                   CarrierID: currentRequestVH.CST_ID);
                string next_pass_block_id = group_list.First().Key;
                if (SCUtility.isMatche(currentRequestBlockID, next_pass_block_id))
                {
                    return true;
                }
                else
                {
                    LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                       Data: $"In this group ,block id:{currentRequestBlockID} not next pass block. so return flase",
                       VehicleID: currentRequestVH.VEHICLE_ID,
                       CarrierID: currentRequestVH.CST_ID);
                    return false;
                }
            }
            else
            {
                return true;
            }
        }
        private bool IsClosestBlockOfVh(AVEHICLE vh, ABLOCKZONEMASTER blockMaster)
        {
            string vh_current_section_id = SCUtility.Trim(vh.CUR_SEC_ID, true);
            string block_entry_section_id = blockMaster.ENTRY_SEC_ID;
            ASECTION vh_current_section = scApp.SectionBLL.cache.GetSection(vh_current_section_id);
            ASECTION block_entry_section = scApp.SectionBLL.cache.GetSection(block_entry_section_id);

            //a.要先判斷在同一段Section是否有其他車輛且的他的距離在前面
            var on_same_section_of_vhs = scApp.VehicleBLL.cache.loadVhsBySectionID(vh_current_section_id);
            foreach (AVEHICLE same_section_vh in on_same_section_of_vhs)
            {
                if (same_section_vh == vh) continue;
                if (same_section_vh.ACC_SEC_DIST > vh.ACC_SEC_DIST)
                {
                    LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                       Data: $"Has vh:{same_section_vh.VEHICLE_ID} in same section:{vh_current_section_id} and infront of the request vh:{vh.VEHICLE_ID}," +
                             $"request vh distance:{vh.ACC_SEC_DIST} orther vh distance:{same_section_vh.ACC_SEC_DIST},so request vh:{vh.VEHICLE_ID} it not closest block vh",
                       VehicleID: vh.VEHICLE_ID,
                       CarrierID: vh.CST_ID);
                    return false;
                }
            }

            //b.經過"a"的判斷後，如果自己已經是在該Block的前一段Section上，則即為該Block的下一台將要通過的Vh
            List<string> entry_section_of_previous_section_id =
                scApp.SectionBLL.cache.GetSectionsByToAddress(block_entry_section.FROM_ADR_ID).
                Select(section => SCUtility.Trim(section.SEC_ID)).
                ToList();
            if (entry_section_of_previous_section_id.Contains(vh_current_section_id))
            {
                return true;
            }

            //  c.如果不是在前一段Section，則需要去找出從vh目前所在位置到該Block的Entry section中，
            //    將經過的Vh，是否有其他車輛在
            string vh_current_section_of_to_adr = vh_current_section.TO_ADR_ID;
            string block_entry_section_of_from_adr = block_entry_section.FROM_ADR_ID;
            string[] will_pass_section_ids = scApp.CMDBLL.
                                                  getShortestRouteSection(vh_current_section_of_to_adr, block_entry_section_of_from_adr).
                                                  routeSection;
            if (will_pass_section_ids == null || will_pass_section_ids.Count() == 0)
            {
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: $"vh:{vh.VEHICLE_ID} at section:{vh_current_section_id} ,it to address:{vh_current_section_of_to_adr} to block entry section of from adr:{block_entry_section_of_from_adr}," +
                         $"can't find the path, not sure if it's the closest vh.",
                   VehicleID: vh.VEHICLE_ID,
                   CarrierID: vh.CST_ID);
                return false;
            }
            foreach (string will_pass_section_id in will_pass_section_ids)
            {
                var on_will_pass_section_of_vhs = scApp.VehicleBLL.cache.loadVhsBySectionID(will_pass_section_id);
                if (on_will_pass_section_of_vhs != null && on_will_pass_section_of_vhs.Count > 0)
                {
                    LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                       Data: $"Has vhs:{string.Join(",", on_will_pass_section_of_vhs.Select(v => v.VEHICLE_ID))} on section:{will_pass_section_id},from adr:{vh_current_section_of_to_adr} to adr:{block_entry_section_of_from_adr}," +
                             $"will pass section ids:{string.Join(",", will_pass_section_ids)},so request vh:{vh.VEHICLE_ID} it not closest block vh",
                       VehicleID: vh.VEHICLE_ID,
                       CarrierID: vh.CST_ID);
                    return false;
                }
            }
            //如果都沒有則該Vh也是下一台將要通過的vh
            return true;
        }

        private int BlockZoneQueueGroupCompare(IGrouping<string, BLOCKZONEQUEUE> group1, IGrouping<string, BLOCKZONEQUEUE> group2)
        {
            int sort_result = 0;
            //1.加總有載著CST的車子數量數量多的那邊先過。
            //2.如果CST數量一樣是，則加總執行的MCS搬送命令，PRIORITY_SUM高的先過
            int group1_cst_count = 0;
            int group1_total_priority_sum = 0;
            long group1_request_time = 0;
            int group2_cst_count = 0;
            int group2_total_priority_sum = 0;
            long group2_request_time = 0;

            List<string> griup1_vh_ids = group1.Select(queue => queue.CAR_ID).ToList();
            List<string> griup2_vh_ids = group2.Select(queue => queue.CAR_ID).ToList();

            group1.ToList().ForEach(queue =>
            {
                BlockPriorityCalculation(queue, ref group1_request_time, ref group1_cst_count, ref group1_total_priority_sum);
            });
            group2.ToList().ForEach(queue =>
            {
                BlockPriorityCalculation(queue, ref group2_request_time, ref group2_cst_count, ref group2_total_priority_sum);
            });
            LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
               Data: $"Calculation block group priority result," +
                     $"{Environment.NewLine}group1-block id:{group1.Key}, carry cst count:{group1_cst_count}, total priority :{group1_total_priority_sum} ,vhs:{string.Join(",", griup1_vh_ids)}" +
                     $"{Environment.NewLine}group2-block id:{group2.Key}, carry cst count:{group2_cst_count}, total priority :{group2_total_priority_sum} ,vhs:{string.Join(",", griup2_vh_ids)}");
            //1.先依照CST的數量排序，哪一組CST數量多的就先通過
            if (group1_cst_count > group2_cst_count)
            {
                sort_result = 1;
            }
            else if (group1_cst_count < group2_cst_count)
            {
                sort_result = -1;
            }
            else
            {
                //2.如果CST數量一樣的話就換比較執行MCS命令的優先權，哪一組高的就先通過
                if (group1_total_priority_sum > group2_total_priority_sum)
                {
                    sort_result = 1;
                }
                else if (group1_total_priority_sum < group2_total_priority_sum)
                {
                    sort_result = -1;
                }
                else
                {
                    //3.如果優先權一樣的時候換比較看哪一組比較早來要該Block
                    if (group1_request_time > group2_request_time) //加總後的Ticks如果比較大，就代表比較晚來要
                    {
                        sort_result = -1;
                    }
                    else if (group1_request_time < group2_request_time)//加總後的Ticks如果比較小，就代表比較早來要
                    {
                        sort_result = 1;
                    }
                    else
                    {
                        sort_result = 0;
                    }
                }
            }
            //由於要用降冪排列，所以回傳時要加上'-'
            return -sort_result;
        }

        private void BlockPriorityCalculation(BLOCKZONEQUEUE queue, ref long groupRequestTime, ref int group_cst_count, ref int group_total_priority_sum)
        {
            AVEHICLE vh = scApp.VehicleBLL.cache.getVhByID(queue.CAR_ID);
            if (vh != null)
            {
                if (vh.HAS_CST == 1)
                    group_cst_count++;

                group_total_priority_sum += vh.CMD_Priority;

            }
            groupRequestTime += queue.REQ_TIME.Ticks;
        }

        public void blockZoneReleaseScript(string blockmaster_id)
        {
            ABLOCKZONEMASTER blockmaster = scApp.MapBLL.getBlockZoneMasterByEntrySecID(blockmaster_id);
            if (blockmaster != null)
            {
                blockZoneReleaseScript(blockmaster);
            }
        }
        public void blockZoneReleaseScript(ABLOCKZONEMASTER blockmaster)
        {
            //if (System.Threading.Interlocked.Exchange(ref blockZoneScriptSyncPoint, 1) == 0)
            //{
            lock (block_control_lock_obj)
            {
                try
                {
                    //using (DBConnection_EF con = new DBConnection_EF())
                    using (DBConnection_EF con = DBConnection_EF.GetUContext())
                    {
                        //if (releaseBlockZone(vh_id, current_adr_id))
                        //{
                        //    Console.WriteLine("release block,leave adr:{0}",
                        //                       current_adr_id);
                        //    scApp.MapBLL.CheckAndNoticeBlockVhPassByAdrID(current_adr_id);
                        //}
                        (bool has_find, BLOCKZONEQUEUE wait_block_queue) = scApp.MapBLL.NoticeBlockVhPassByEntrySecID(blockmaster.ENTRY_SEC_ID);
                        if (has_find)
                        {
                            AVEHICLE vh = scApp.VehicleBLL.getVehicleByID(wait_block_queue.CAR_ID);
                            string block_zone_id = wait_block_queue.ENTRY_SEC_ID;
                            bool canPass = canPassBlockZone(vh, block_zone_id);
                            if (!vh.IsBlocking)
                            {
                                LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                                   Data: $"Vh:{vh.VEHICLE_ID} ask block:{block_zone_id} check can pass result:{canPass}," +
                                         $"vh of blocking single:{vh.IsBlocking},not notice vh pass",
                                   VehicleID: vh.VEHICLE_ID,
                                   CarrierID: vh.CST_ID);
                                return;
                            }
                            if (canPass)
                            {
                                //scApp.VehicleBLL.noticeVhPass(wait_block_queue);
                                noticeVhPass(wait_block_queue);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Exception");
                }
                finally
                {
                    //System.Threading.Interlocked.Exchange(ref blockZoneScriptSyncPoint, 0);
                }
            }
            //}
        }

        public void noticeVhPass(string vh_id)
        {
            BLOCKZONEQUEUE usingBlockQueue = scApp.MapBLL.getUsingBlockZoneQueueByVhID(vh_id);
            if (usingBlockQueue != null)
            {
                noticeVhPass(usingBlockQueue);
            }
            else
            {
                string reason = string.Empty;
                PauseRequest(vh_id, PauseEvent.Continue, SCAppConstants.OHxCPauseType.Block);
            }
        }

        public bool noticeVhPass(BLOCKZONEQUEUE blockZoneQueue)
        {
            string notice_vh_id = blockZoneQueue.CAR_ID.Trim();
            string req_block_id = blockZoneQueue.ENTRY_SEC_ID.Trim();

            bool isSuccess = false;
            using (TransactionScope tx = SCUtility.getTransactionScope())
            {
                using (DBConnection_EF con = DBConnection_EF.GetUContext())
                {
                    scApp.MapBLL.updateBlockZoneQueue_BlockTime(notice_vh_id, req_block_id);
                    scApp.MapBLL.ChangeBlockControlStatus_Blocking(notice_vh_id, req_block_id, DateTime.Now);

                    isSuccess = PauseRequest(notice_vh_id, PauseEvent.Continue, SCAppConstants.OHxCPauseType.Block);
                    if (isSuccess)
                    {
                        tx.Complete();
                    }
                }
            }

            //bool isSuccess = scApp.VehicleService.PauseRequest(notice_vh_id, PauseEvent.Continue, SCAppConstants.OHxCPauseType.Block);
            //if (isSuccess)
            //{
            //    if (scApp.MapBLL.IsBlockControlStatus
            //          (notice_vh_id, SCAppConstants.BlockQueueState.Request))
            //    {
            //        scApp.MapBLL.updateBlockZoneQueue_BlockTime(notice_vh_id, req_block_id);
            //        scApp.MapBLL.ChangeBlockControlStatus_Blocking(notice_vh_id);
            //    }
            //}
            //else
            //{
            //}
            return isSuccess;
        }


        public void hidZoneReleaseScript(AHIDZONEMASTER hidmaster)
        {
            try
            {
                using (TransactionScope tx = SCUtility.getTransactionScope())
                {
                    using (DBConnection_EF con = DBConnection_EF.GetUContext())
                    {
                        AHIDZONEQUEUE hid_queue = scApp.HIDBLL.getHIDZoneQueue_FirstReqInPasue(hidmaster.ENTRY_SEC_ID);
                        if (hid_queue != null)
                        {
                            scApp.HIDBLL.updateHIDZoneQueue_Pasue(hid_queue.VEHICLE_ID, hid_queue.ENTRY_SEC_ID, false);
                            string notice_vh_id = hid_queue.VEHICLE_ID;
                            //if (scApp.VehicleBLL.noticeVhPass(hid_queue))
                            if (PauseRequest(notice_vh_id, PauseEvent.Continue, OHxCPauseType.ManualHID))
                            {
                                tx.Complete();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception");
            }
        }



        public void CheckBlockControlByVehicleView()
        {
            try
            {
                List<AVEHICLE> lstVH = scApp.getEQObjCacheManager().getAllVehicle();
                //1.先在Redis找出有Req BlockZone的
                foreach (AVEHICLE vh in lstVH)
                {
                    if (vh.isTcpIpConnect &&
                        (vh.MODE_STATUS == VHModeStatus.AutoLocal ||
                        vh.MODE_STATUS == VHModeStatus.AutoRemote) &&
                        vh.IsBlocking
                        )
                    {
                        string block_zone_id = string.Empty;
                        string block_status = string.Empty;
                        //var non_release_block = scApp.MapBLL.loadNonReleaseBlockQueueByCarID(vh.VEHICLE_ID);
                        var requesting_block_zone = scApp.MapBLL.getBlockQueueInRequestByCarID(vh.VEHICLE_ID);
                        if (requesting_block_zone != null)
                        {

                            //if (scApp.MapBLL.tryGetInRequest(vh.VEHICLE_ID, out block_zone_id, out block_status))
                            //{
                            block_zone_id = requesting_block_zone.ENTRY_SEC_ID;
                            //2.透過該BlockZone去找出能否通過
                            bool canPass = canPassBlockZone(vh, block_zone_id);
                            if (canPass)
                            {
                                //3.若可以則再嘗試通知
                                blockZoneReleaseScript(block_zone_id);
                                logger.Warn($"vh id [{vh.VEHICLE_ID}] ,block notice pass by timer-block check. block id [{block_zone_id}]");
                            }
                            //}
                        }

                    }

                }



            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception:");
            }
        }

        private long obstacle_SyncPoint = 0;
        public void CheckObstacleStatusByVehicleView()
        {
            if (System.Threading.Interlocked.Exchange(ref obstacle_SyncPoint, 1) == 0)
            {

                try
                {
                    List<AVEHICLE> lstVH = scApp.VehicleBLL.cache.loadVhs();
                    foreach (var vh in lstVH)
                    {
                        //if (vh.isTcpIpConnect &&
                        //    (vh.MODE_STATUS == VHModeStatus.AutoLocal ||
                        //    vh.MODE_STATUS == VHModeStatus.AutoRemote) &&
                        //    vh.IsObstacle
                        //    )
                        if (vh.isTcpIpConnect &&
                            (vh.MODE_STATUS != VHModeStatus.Manual) &&
                            vh.IsObstacle
                            )
                        {
                            ASEGMENT seg = scApp.SegmentBLL.cache.GetSegment(vh.CUR_SEG_ID);
                            AVEHICLE next_vh_on_seg = seg.GetNextVehicle(vh);
                            if (next_vh_on_seg != null)
                            {
                                //scApp.VehicleBLL.whenVhObstacle(next_vh_on_seg.VEHICLE_ID);
                                scApp.VehicleBLL.whenVhObstacle(next_vh_on_seg.VEHICLE_ID, vh.VEHICLE_ID);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Exception:");
                }
                finally
                {
                    System.Threading.Interlocked.Exchange(ref obstacle_SyncPoint, 0);
                }
            }
        }

        public void ProcessErrorVehicleOnTheWayScenario()
        {
            try
            {
                //1.歷遍所有的Segment，如果
                //  a.該Segment是Enable則判斷是否有故障車在上面，若是則將其Disable(System)
                //  b.該Segment是Disable則判斷是否已經沒有故障車在上面，若是則需要將其Enable(System)
                var segments = scApp.SegmentBLL.cache.GetSegments();
                foreach (var segment in segments)
                {
                    if (segment.DISABLE_FLAG_SYSTEM == false)
                    {
                        var vhs = scApp.VehicleBLL.cache.loadVhsBySegmentID(segment.SEG_NUM);
                        var on_segment_error_vhs = vhs.Where(vh => vh.IsError).ToList();
                        if (on_segment_error_vhs.Count != 0)
                        {
                            scApp.RoadControlService.doEnableDisableSegment
                                (segment.SEG_NUM,
                                 E_PORT_STATUS.OutOfService,
                                 ASEGMENT.DisableType.System,
                                 SECSConst.LANECUTTYPE_LaneCutVehicle);
                        }
                    }
                    else
                    {
                        var vhs = scApp.VehicleBLL.cache.loadVhsBySegmentID(segment.SEG_NUM);
                        var on_segment_error_vhs = vhs.Where(vh => vh.IsError).ToList();
                        if (on_segment_error_vhs.Count == 0)
                        {
                            scApp.RoadControlService.doEnableDisableSegment
                                (segment.SEG_NUM,
                                 E_PORT_STATUS.InService,
                                 ASEGMENT.DisableType.System,
                                 SECSConst.LANECUTTYPE_LaneCutVehicle);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception:");
            }
        }

        public bool tryReleaseBlockZone(string vh_id, string release_adr, bool isThrowException, out ABLOCKZONEMASTER releaseBlockMaster)
        {
            bool hasRelease = false;
            //if (SCUtility.isEmpty(eqpt.currentBlockID))
            //    return;
            //1.用VHID 與CurrentBlockID 查詢BLOCKZONEQUEUE 找出為2或3的
            //BLOCKZONEQUEUE blockZoneQueue = scApp.MapBLL.getBlockZoneQueueByVhIDAndCrtBlockSecID(eqpt.VEHICLE_ID, eqpt.currentBlockID);
            //若有 再用Adr找出ABLOCKZONEMASTER是否有符合的
            //if (blockZoneQueue != null)
            AVEHICLE vh_vo = scApp.getEQObjCacheManager().getVehicletByVHID(vh_id);
            lock (vh_vo.BlockControl_SyncForRedis)
            {
                //if (scApp.MapBLL.IsBeforeBlockControlStatus(vh_id, SCAppConstants.BlockQueueState.Release))
                //{
                LogCollection.BlockControlLogger.Trace($"vh[{vh_id}],Release Block Control Step 1");
                //BLOCKZONEQUEUE blockZoneQueue = scApp.MapBLL.getUsingBlockZoneQueueByVhID(vh_id);
                List<BLOCKZONEQUEUE> blockZoneQueues = scApp.MapBLL.loadNonReleaseBlockQueueByCarID(vh_id);
                if (blockZoneQueues == null)
                {
                    if (isThrowException)
                        throw new NullReferenceException($"error function [{nameof(tryReleaseBlockZone)}],msg[{nameof(blockZoneQueues)} is null]");
                    else
                    {
                        releaseBlockMaster = null;
                        return false;
                    }
                }
                List<string> block_zone_ids = blockZoneQueues.Select(queue => queue.ENTRY_SEC_ID.Trim()).ToList();
                LogCollection.BlockControlLogger.Trace($"vh[{vh_id}],Release Block Control Step 2,queue entry sec id[{string.Join(",", block_zone_ids)}");
                //releaseBlockMaster = scApp.MapBLL.getBlockZoneMasterByBlockIDAndAdrID(blockZoneQueue.ENTRY_SEC_ID.Trim(), release_adr);
                releaseBlockMaster = scApp.MapBLL.getCurrentReleaseBlock(block_zone_ids, release_adr);
                if (releaseBlockMaster == null)
                {
                    if (isThrowException)
                        throw new NullReferenceException($"error function [{nameof(tryReleaseBlockZone)}],msg[{nameof(releaseBlockMaster)} is null]");
                    else
                    {
                        releaseBlockMaster = null;
                        return false;
                    }
                }
                if (releaseBlockMaster != null)
                {
                    LogCollection.BlockControlLogger.Trace($"vh[{vh_id}],Release Block Control Step 3,master entry sec id[{releaseBlockMaster.ENTRY_SEC_ID.Trim()}]");
                    LogCollection.BlockControlLogger.Trace($"vh[{vh_id}],Beging Relsase,entry sec id[{releaseBlockMaster.ENTRY_SEC_ID.Trim()}]");
                    using (TransactionScope tx = SCUtility.getTransactionScope())
                    {
                        using (DBConnection_EF con = DBConnection_EF.GetUContext())
                        {
                            scApp.MapBLL.updateBlockZoneQueue_ReleasTime(vh_id, releaseBlockMaster.ENTRY_SEC_ID.Trim());
                            //scApp.MapBLL.DeleteBlockControlKeyWordToRedis(vh_id);
                            scApp.MapBLL.DeleteBlockControlKeyWordToRedisAsync(vh_id, releaseBlockMaster.ENTRY_SEC_ID.Trim());
                            tx.Complete();
                        }
                    }
                    hasRelease = true;
                    LogCollection.BlockControlLogger.Trace($"vh[{vh_id}],End Relsase,entry sec id[{releaseBlockMaster.ENTRY_SEC_ID.Trim()}]");
                }
                //}
                //else
                //{
                //    releaseBlockMaster = null;
                //}
            }
            return hasRelease;
        }

        public bool tryReleaseHIDZone(string vh_id, string release_adr, out AHIDZONEMASTER releaseHIDMaster)
        {
            bool hasRelease = false;
            releaseHIDMaster = null;
            AVEHICLE vh_vo = scApp.getEQObjCacheManager().getVehicletByVHID(vh_id);
            AHIDZONEQUEUE hidZoneQueue = scApp.HIDBLL.getUsingHIDZoneQueueByVhID(vh_id);
            if (hidZoneQueue == null) { return true; }
            releaseHIDMaster = scApp.HIDBLL.GetHidZoneMaster(hidZoneQueue.ENTRY_SEC_ID, release_adr);
            if (releaseHIDMaster == null) { return true; }

            using (TransactionScope tx = SCUtility.getTransactionScope())
            {
                using (DBConnection_EF con = DBConnection_EF.GetUContext())
                {
                    scApp.HIDBLL.updateHIDZoneQueue_ReleasTime(vh_id, hidZoneQueue.ENTRY_SEC_ID.Trim());
                    scApp.HIDBLL.VHLeaveHIDZone(hidZoneQueue.ENTRY_SEC_ID.Trim());
                    tx.Complete();
                    hasRelease = true;
                }
            }
            return hasRelease;
        }

        private void PositionReport_BlockRelease(BCFApplication bcfApp, AVEHICLE eqpt, ID_136_TRANS_EVENT_REP recive_str, int seq_num)
        {
            string release_adr = recive_str.ReleaseBlockAdrID;
            LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
               Data: $"Process block release,release address id:{release_adr}",
               VehicleID: eqpt.VEHICLE_ID,
               CarrierID: eqpt.CST_ID);
            doBlockRelease(eqpt, release_adr);
            //replyTranEventReport(bcfApp, recive_str.EventType, eqpt, seq_num);
        }

        private (bool hasRelease, ABLOCKZONEMASTER releaseBlockMaster) doBlockRelease(AVEHICLE eqpt, string release_adr)
        {
            return doBlockRelease(eqpt, release_adr, true);
        }
        public (bool hasRelease, ABLOCKZONEMASTER releaseBlockMaster) doBlockRelease(string vhID, string release_adr, bool isThrowException)
        {
            AVEHICLE vh = scApp.VehicleBLL.cache.getVhByID(vhID);
            if (vh == null)
            {
                throw new NullReferenceException($"vh:{vhID} not exist.");
            }
            return doBlockRelease(vh, release_adr, isThrowException);
        }
        private (bool hasRelease, ABLOCKZONEMASTER releaseBlockMaster) doBlockRelease(AVEHICLE eqpt, string release_adr, bool isThrowException)
        {
            ABLOCKZONEMASTER releaseBlockMaster = null;
            bool hasRelease = false;
            try
            {
                hasRelease = tryReleaseBlockZone(eqpt.VEHICLE_ID, release_adr, isThrowException, out releaseBlockMaster);
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: $"Process block release, release address id:{release_adr}, release result:{hasRelease}",
                   VehicleID: eqpt.VEHICLE_ID,
                   CarrierID: eqpt.CST_ID);
                if (hasRelease)
                {
                    //主動block release功能取消，由OHT定時每0.5秒來詢問是否可通過
                    //Task.Run(() => blockZoneReleaseScript(releaseBlockMaster));
                }
            }
            catch (Exception ex)
            {
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Warn, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: ex,
                   VehicleID: eqpt.VEHICLE_ID,
                   CarrierID: eqpt.CST_ID);
                logger.Warn(ex, "Warn");
            }
            return (hasRelease, releaseBlockMaster);
        }

        private void PositionReport_HIDRelease(BCFApplication bcfApp, AVEHICLE eqpt, ID_136_TRANS_EVENT_REP recive_str, int seq_num)
        {
            try
            {
                string release_adr = recive_str.ReleaseHIDAdrID;
                AHIDZONEMASTER releaseHIDMaster = null;
                if (tryReleaseHIDZone(eqpt.VEHICLE_ID, release_adr, out releaseHIDMaster))
                {
                    if (scApp.HIDBLL.hasEnoughSeat(releaseHIDMaster.ENTRY_SEC_ID.Trim(), out long current_vh_count, out int hid_zone_max_load_count))
                    {
                        Task.Run(() => hidZoneReleaseScript(releaseHIDMaster));
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Warn, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: ex,
                   VehicleID: eqpt.VEHICLE_ID,
                   CarrierID: eqpt.CST_ID);
            }
            // replyTranEventReport(bcfApp, recive_str.EventType, eqpt, seq_num);
        }

        public bool VhPositionReset(string vhID)
        {
            bool is_success = true;
            try
            {
                scApp.VehicleBLL.clearAndPublishPositionReportInfo2Redis(vhID);
            }
            catch (Exception ex)
            {
                is_success = false;
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Warn, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: ex,
                   VehicleID: vhID);
            }
            return is_success;
        }

        #endregion Position Report

        #region Status Report
        const string VEHICLE_ERROR_REPORT_DESCRIPTION = "Vehicle:{0} ,error happend.";
        [ClassAOPAspect]
        public void StatusReport(BCFApplication bcfApp, AVEHICLE eqpt, ID_144_STATUS_CHANGE_REP recive_str, int seq_num)
        {
            if (scApp.getEQObjCacheManager().getLine().ServerPreStop)
                return;
            LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
               seq_num: seq_num,
               Data: recive_str,
               VehicleID: eqpt.VEHICLE_ID,
               CarrierID: eqpt.CST_ID);

            SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, seq_num, recive_str);
            lock (eqpt.StatusUpdate_Sync)
            {
                string current_adr = recive_str.CurrentAdrID;
                VHModeStatus modeStat = DecideVhModeStatus(eqpt.VEHICLE_ID, current_adr, recive_str.ModeStatus);
                VHActionStatus actionStat = recive_str.ActionStatus;
                VhPowerStatus powerStat = recive_str.PowerStatus;
                string cstID = recive_str.CSTID;
                VhStopSingle obstacleStat = recive_str.ObstacleStatus;
                VhStopSingle blockingStat = recive_str.BlockingStatus;
                VhStopSingle pauseStat = recive_str.PauseStatus;
                VhStopSingle hidStat = recive_str.HIDStatus;
                VhStopSingle errorStat = recive_str.ErrorStatus;
                VhLoadCSTStatus loadCSTStatus = recive_str.HasCST;

                //VhGuideStatus leftGuideStat = recive_str.LeftGuideLockStatus;
                //VhGuideStatus rightGuideStat = recive_str.RightGuideLockStatus;

                bool hasdifferent =
                        !SCUtility.isMatche(eqpt.CST_ID, cstID) ||
                        eqpt.MODE_STATUS != modeStat ||
                        eqpt.ACT_STATUS != actionStat ||
                        eqpt.ObstacleStatus != obstacleStat ||
                        eqpt.BlockingStatus != blockingStat ||
                        eqpt.PauseStatus != pauseStat ||
                        eqpt.HIDStatus != hidStat ||
                        eqpt.ERROR != errorStat ||
                        eqpt.HAS_CST != (int)loadCSTStatus;

                if (eqpt.ERROR != errorStat)
                {
                    //todo 在error flag 有變化時，上報S5F1 alarm set/celar
                    //string alarm_desc = string.Format(VEHICLE_ERROR_REPORT_DESCRIPTION, eqpt.Real_ID);
                    //string alarm_code = $"000{eqpt.Num}";
                    //ErrorStatus error_status =
                    //    errorStat == VhStopSingle.StopSingleOn ? ErrorStatus.ErrSet : ErrorStatus.ErrReset;
                    //scApp.ReportBLL.ReportAlarmHappend(error_status, alarm_code, alarm_desc);
                    if (!SCUtility.isEmpty(eqpt.MCS_CMD))
                    {
                        scApp.ReportBLL.newReportTransferCommandPaused(eqpt.MCS_CMD, null);
                    }
                }


                int obstacleDIST = recive_str.ObstDistance;
                string obstacleVhID = recive_str.ObstVehicleID;
                if (hasdifferent && !scApp.VehicleBLL.doUpdateVehicleStatus(eqpt, cstID,
                                       modeStat, actionStat,
                                       blockingStat, pauseStat, obstacleStat, hidStat, errorStat, loadCSTStatus))
                {
                    LogHelper.Log(logger: logger, LogLevel: LogLevel.Warn, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                       Data: $"update vhicle status fail!",
                       VehicleID: eqpt.VEHICLE_ID,
                       CarrierID: eqpt.CST_ID);
                    return;
                }
            }
            vhCommandExcuteStatusCheck(eqpt.VEHICLE_ID);

            //if (modeStat == VHModeStatus.AutoMtl)
            //{
            //    var check_is_in_maintain_device = scApp.EquipmentBLL.cache.IsInMaintainDevice(eqpt.CUR_ADR_ID);
            //    if (check_is_in_maintain_device.isIn)
            //    {
            //        var device = check_is_in_maintain_device.device;
            //        if (device is MaintainLift)
            //            scApp.MTLService.carInSafetyAndVehicleStatusCheck(device as MaintainLift);

            //    }
            //}

            //UpdateVehiclePositionFromStatusReport(eqpt, recive_str);

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
            //        if (reply_status_event_report(bcfApp, eqpt, seq))
            //        {
            //            tx.Complete();
            //        }
            //    }
            //}
            //reply_status_event_report(bcfApp, eqpt, seq_num);

            //if (actionStat == VHActionStatus.Stop)
            //{

            //if (obstacleStat == VhStopSingle.StopSingleOn)
            //{
            //    ASEGMENT seg = scApp.SegmentBLL.cache.GetSegment(eqpt.CUR_SEG_ID);
            //    AVEHICLE next_vh_on_seg = seg.GetNextVehicle(eqpt);
            //    //if (!SCUtility.isEmpty(obstacleVhID))
            //    if (next_vh_on_seg != null)
            //    {
            //        //scApp.VehicleBLL.whenVhObstacle(obstacleVhID);
            //        scApp.VehicleBLL.whenVhObstacle(next_vh_on_seg.VEHICLE_ID);
            //    }
            //}
            //}
        }

        private VHModeStatus DecideVhModeStatus(string vh_id, string current_adr, VHModeStatus vh_current_mode_status)
        {
            AVEHICLE eqpt = scApp.VehicleBLL.getVehicleByID(vh_id);

            LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
              Data: $"current vh mode is:{eqpt.MODE_STATUS} and vh report mode:{vh_current_mode_status}",
              VehicleID: eqpt.VEHICLE_ID,
              CarrierID: eqpt.CST_ID);
            VHModeStatus modeStat = default(VHModeStatus);
            if (vh_current_mode_status == VHModeStatus.AutoRemote)
            {
                if (eqpt.MODE_STATUS == VHModeStatus.AutoLocal ||
                         eqpt.MODE_STATUS == VHModeStatus.AutoMtl ||
                         eqpt.MODE_STATUS == VHModeStatus.AutoMts)
                {
                    modeStat = eqpt.MODE_STATUS;
                }
                else if (scApp.EquipmentBLL.cache.IsInMatainLift(current_adr))
                {
                    modeStat = VHModeStatus.AutoMtl;
                }
                else if (scApp.EquipmentBLL.cache.IsInMatainSpace(current_adr))
                {
                    modeStat = VHModeStatus.AutoMts;
                }
                else
                {
                    modeStat = vh_current_mode_status;
                }
            }
            else
            {
                modeStat = vh_current_mode_status;
            }
            return modeStat;
        }





        //private void whenVhObstacle(string obstacleVhID)
        //{
        //    AVEHICLE obstacleVh = scApp.VehicleBLL.getVehicleByID(obstacleVhID);
        //    if (obstacleVh != null)
        //    {
        //        if (obstacleVh.IS_PARKING &&
        //            !SCUtility.isEmpty(obstacleVh.PARK_ADR_ID))
        //        {
        //            scApp.VehicleBLL.FindParkZoneOrCycleRunZoneForDriveAway(obstacleVh);
        //        }
        //        else if (SCUtility.isEmpty(obstacleVh.OHTC_CMD))
        //        {
        //            string[] nextSections = scApp.MapBLL.loadNextSectionIDBySectionID(obstacleVh.CUR_SEC_ID);
        //            if (nextSections != null && nextSections.Count() > 0)
        //            {
        //                ASECTION nextSection = scApp.MapBLL.getSectiontByID(nextSections[0]);
        //                bool isSuccess = scApp.CMDBLL.doCreatTransferCommand(obstacleVhID
        //                         , string.Empty
        //                         , string.Empty
        //                         , E_CMD_TYPE.Move
        //                         , obstacleVh.CUR_ADR_ID
        //                         , nextSection.TO_ADR_ID, 0, 0);

        //            }
        //        }
        //    }
        //}
        private bool reply_status_event_report(BCFApplication bcfApp, AVEHICLE eqpt, int seq_num)
        {
            ID_44_STATUS_CHANGE_RESPONSE send_str = new ID_44_STATUS_CHANGE_RESPONSE
            {
                ReplyCode = 0
            };
            WrapperMessage wrapper = new WrapperMessage
            {
                SeqNum = seq_num,
                StatusChangeResp = send_str
            };

            //Boolean resp_cmp = ITcpIpControl.sendGoogleMsg(bcfApp, eqpt.TcpIpAgentName, wrapper, true);
            Boolean resp_cmp = eqpt.sendMessage(wrapper, true);
            LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
              seq_num: seq_num, Data: send_str,
              VehicleID: eqpt.VEHICLE_ID,
              CarrierID: eqpt.CST_ID);
            SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, seq_num, send_str, resp_cmp.ToString());
            return resp_cmp;
        }
        #endregion Status Report

        #region Command Complete Report
        [ClassAOPAspect]
        public void CommandCompleteReport(string tcpipAgentName, BCFApplication bcfApp, AVEHICLE eqpt, ID_132_TRANS_COMPLETE_REPORT recive_str, int seq_num)
        {
            if (scApp.getEQObjCacheManager().getLine().ServerPreStop)
                return;
            SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, seq_num, recive_str);
            string finish_ohxc_cmd = eqpt.OHTC_CMD;
            string finish_mcs_cmd = eqpt.MCS_CMD;
            string cmd_id = recive_str.CmdID;
            int travel_dis = recive_str.CmdDistance;
            CompleteStatus completeStatus = recive_str.CmpStatus;
            string cur_sec_id = recive_str.CurrentSecID;
            string cur_adr_id = recive_str.CurrentAdrID;
            string cst_id = SCUtility.Trim(recive_str.CSTID, true);
            VhLoadCSTStatus vhLoadCSTStatus = recive_str.HasCST;
            string car_cst_id = recive_str.CarCSTID;
            bool isSuccess = true;

            if (scApp.CMDBLL.isCMCD_OHTCFinish(cmd_id))
            {
                replyCommandComplete(eqpt, seq_num, finish_ohxc_cmd, finish_mcs_cmd);

                LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: $"commnad id:{cmd_id} has already process. well pass this report.",
                   VehicleID: eqpt.VEHICLE_ID,
                   CarrierID: eqpt.CST_ID);
                return;
            }





            string mcs_cmd_result = SECSConst.convert2MCS(completeStatus);
            scApp.VIDBLL.upDateVIDResultCode(eqpt.VEHICLE_ID, mcs_cmd_result);

            //switch (completeStatus)
            //{
            //    case CompleteStatus.CmpStatusIdmisMatch:
            //    case CompleteStatus.CmpStatusIdreadFailed:
            //    case CompleteStatus.CmpStatusIdreadDuplicate:
            //        scApp.VIDBLL.upDateVIDCarrierLocInfo(eqpt.VEHICLE_ID, "");
            //        break;
            //}


            List<AMCSREPORTQUEUE> reportqueues = new List<AMCSREPORTQUEUE>();
            if (!SCUtility.isEmpty(finish_mcs_cmd))
            {
                using (TransactionScope tx = SCUtility.getTransactionScope())
                {
                    using (DBConnection_EF con = DBConnection_EF.GetUContext())
                    {
                        switch (completeStatus)
                        {
                            case CompleteStatus.CmpStatusCancel:
                                isSuccess = scApp.ReportBLL.newReportTransferCommandCancelFinish(eqpt.VEHICLE_ID, reportqueues);
                                break;
                            case CompleteStatus.CmpStatusAbort:
                                isSuccess = scApp.ReportBLL.newReportTransferCommandAbortFinish(eqpt.VEHICLE_ID, reportqueues);
                                break;
                            case CompleteStatus.CmpStatusLoad:
                            case CompleteStatus.CmpStatusUnload:
                            case CompleteStatus.CmpStatusLoadunload:
                            case CompleteStatus.CmpStatusInterlockError:
                            case CompleteStatus.CmpStatusVehicleAbort:
                                isSuccess = scApp.ReportBLL.newReportTransferCommandNormalFinish(eqpt.VEHICLE_ID, reportqueues);
                                break;
                            case CompleteStatus.CmpStatusIdmisMatch:
                            case CompleteStatus.CmpStatusIdreadFailed:
                            case CompleteStatus.CmpStatusIdreadDuplicate:
                                isSuccess = scApp.ReportBLL.newReportTransferCommandIDReadErrorFinish(eqpt.VEHICLE_ID, reportqueues);
                                break;
                            case CompleteStatus.CmpStatusMove:
                            case CompleteStatus.CmpStatusHome:
                            case CompleteStatus.CmpStatusOverride:
                            case CompleteStatus.CmpStatusCstIdrenmae:
                            case CompleteStatus.CmpStatusMtlhome:
                            case CompleteStatus.CmpStatusMoveToMtl:
                            case CompleteStatus.CmpStatusSystemOut:
                            case CompleteStatus.CmpStatusSystemIn:
                                //Nothing...
                                break;
                            //當TechingMove Complete的時候，OHxC將會進行Auto Teching的動作
                            case CompleteStatus.CmpStatusTechingMove:
                                AutoTeaching(eqpt.VEHICLE_ID);
                                break;
                            default:
                                logger.Info($"Proc func:CommandCompleteReport, but completeStatus:{completeStatus} notimplemented ");
                                break;
                        }
                        if (isSuccess)
                        {
                            tx.Complete();
                        }
                        else
                        {
                            return;
                        }
                    }
                    scApp.ReportBLL.newSendMCSMessage(reportqueues);
                }
            }

            using (TransactionScope tx = SCUtility.getTransactionScope())
            {
                using (DBConnection_EF con = DBConnection_EF.GetUContext())
                {
                    //isSuccess = scApp.VehicleBLL.doTransferCommandFinish(eqpt.VEHICLE_ID, cmd_id);
                    isSuccess &= scApp.VehicleBLL.doTransferCommandFinish(eqpt.VEHICLE_ID, cmd_id, completeStatus);
                    isSuccess &= scApp.VIDBLL.initialVIDCommandInfo(eqpt.VEHICLE_ID);


                    //當發生Vehicle Abort的時候要確認是否有預下給該Vh的命令，
                    //有的話要將他取消，並把原本的MCS命令切回Queue
                    if (completeStatus == CompleteStatus.CmpStatusVehicleAbort)
                    {
                        var check_result = scApp.CMDBLL.hasCMD_OHTCInQueue(eqpt.VEHICLE_ID);
                        if (check_result.has)
                        {
                            ACMD_OHTC queue_cmd = check_result.cmd_ohtc;
                            scApp.CMDBLL.updateCommand_OHTC_StatusByCmdID(queue_cmd.CMD_ID, E_CMD_STATUS.AbnormalEndByOHTC);
                            if (!SCUtility.isEmpty(queue_cmd.CMD_ID_MCS))
                            {
                                ACMD_MCS pre_initial_cmd_mcs = scApp.CMDBLL.getCMD_MCSByID(queue_cmd.CMD_ID_MCS);
                                if (pre_initial_cmd_mcs != null &&
                                    pre_initial_cmd_mcs.TRANSFERSTATE == E_TRAN_STATUS.PreInitial)
                                {
                                    scApp.CMDBLL.updateCMD_MCS_TranStatus2Queue(pre_initial_cmd_mcs.CMD_ID);
                                }
                            }
                        }
                    }

                    if (isSuccess)
                    {
                        tx.Complete();
                    }
                    else
                    {
                        //scApp.getEQObjCacheManager().restoreVhDataFromDB(eqpt);
                        return;
                    }
                }
            }

            replyCommandComplete(eqpt, seq_num, finish_ohxc_cmd, finish_mcs_cmd);
            //釋放尚未Release的Block
            releaseBlockControl(eqpt.VEHICLE_ID);
            //回復結束後，若該筆命令是Mismatch、IDReadFail結束的話則要把原本車上的那顆CST Installed回來。
            if (vhLoadCSTStatus == VhLoadCSTStatus.Exist)
            {
                scApp.VIDBLL.upDateVIDCarrierID(eqpt.VEHICLE_ID, car_cst_id);
                scApp.VIDBLL.upDateVIDCarrierLocInfo(eqpt.VEHICLE_ID, eqpt.Real_ID);
            }

            switch (completeStatus)
            {
                case CompleteStatus.CmpStatusIdmisMatch:
                case CompleteStatus.CmpStatusIdreadFailed:
                case CompleteStatus.CmpStatusIdreadDuplicate:
                    //scApp.VIDBLL.upDateVIDCarrierID(eqpt.VEHICLE_ID, eqpt.CST_ID);
                    //scApp.VIDBLL.upDateVIDCarrierID(eqpt.VEHICLE_ID, car_cst_id);
                    //scApp.VIDBLL.upDateVIDCarrierLocInfo(eqpt.VEHICLE_ID, eqpt.Real_ID);
                    if (!SCUtility.isEmpty(finish_mcs_cmd))
                    {
                        reportqueues.Clear();
                        scApp.ReportBLL.newReportCarrierIDReadReport(eqpt.VEHICLE_ID, reportqueues);
                        scApp.ReportBLL.insertMCSReport(reportqueues);
                        scApp.ReportBLL.newSendMCSMessage(reportqueues);
                    }
                    break;
                case CompleteStatus.CmpStatusUnload:
                case CompleteStatus.CmpStatusLoadunload:
                    scApp.PortBLL.OperateCatch.updatePortStationCSTExistStatus(eqpt.CUR_ADR_ID, cst_id);
                    break;
                    //case CompleteStatus.CmpStatusVehicleAbort:
                    //case CompleteStatus.CmpStatusInterlockError:
                    //    if (vhLoadCSTStatus == VhLoadCSTStatus.Exist)
                    //    {
                    //        scApp.VIDBLL.upDateVIDCarrierID(eqpt.VEHICLE_ID, car_cst_id);
                    //        scApp.VIDBLL.upDateVIDCarrierLocInfo(eqpt.VEHICLE_ID, eqpt.Real_ID);
                    //    }
                    //    break;
            }

            //TODO 要改抓命令的Table來更新
            //switch (recive_str.ActType)
            //{
            //    case ActiveType.Unload:
            //    case ActiveType.Loadunload:
            //        scApp.CMDBLL.update_CMD_Detail_UnloadEndTime(eqpt.VEHICLE_ID);
            //        break;
            //}
            if (DebugParameter.IsDebugMode && DebugParameter.IsCycleRun)
            {
                SpinWait.SpinUntil(() => false, 3000);
                TestCycleRun(eqpt, cmd_id);
            }
            else
            {
                MaintainLift maintainLift = null;
                MaintainSpace maintainSpace = null;
                switch (completeStatus)
                {
                    case CompleteStatus.CmpStatusSystemOut:
                        LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                           Data: $"Process vh:{eqpt.VEHICLE_ID} system out complete, current address:{cur_adr_id},current mode:{eqpt.MODE_STATUS}",
                           VehicleID: eqpt.VEHICLE_ID,
                           CarrierID: eqpt.CST_ID);
                        if (eqpt.MODE_STATUS == VHModeStatus.AutoMtl)
                        {
                            //在收到OHT的ID:132-SystemOut完成後，創建一個Transfer command，讓Vh移至移動至MTL上
                            //doAskVhToMaintainsAddress(eqpt.VEHICLE_ID, MTLService.MTL_ADDRESS);
                            maintainLift = scApp.EquipmentBLL.cache.GetMaintainLiftBySystemOutAdr(cur_adr_id);
                            if (maintainLift != null)
                                doAskVhToMaintainsAddress(eqpt.VEHICLE_ID, maintainLift.MTL_ADDRESS);
                        }
                        else if (eqpt.MODE_STATUS == VHModeStatus.AutoMts)
                        {
                            maintainSpace = scApp.EquipmentBLL.cache.GetMaintainSpaceBySystemOutAdr(cur_adr_id);
                            if (maintainSpace != null)
                            {
                                LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                                   Data: $"Process vh:{eqpt.VEHICLE_ID} system out complete, notify mtx:{maintainSpace.DeviceID} is complete",
                                   VehicleID: eqpt.VEHICLE_ID,
                                   CarrierID: eqpt.CST_ID);
                                scApp.MTLService.carOutComplete(maintainSpace);
                            }
                        }
                        scApp.ReportBLL.newReportVehicleRemoved(eqpt.VEHICLE_ID, null);
                        break;
                    case CompleteStatus.CmpStatusMoveToMtl:
                        maintainLift = scApp.EquipmentBLL.cache.GetMaintainLiftByMTLAdr(cur_adr_id);
                        LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                           Data: $"Process vh:{eqpt.VEHICLE_ID} move to mtl complete, current address:{cur_adr_id},current mode:{eqpt.MODE_STATUS}",
                           VehicleID: eqpt.VEHICLE_ID,
                           CarrierID: eqpt.CST_ID);
                        if (maintainLift != null)
                        {
                            LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                               Data: $"Process vh:{eqpt.VEHICLE_ID} move to mtl complete, notify mtx:{maintainLift.DeviceID} is complete",
                               VehicleID: eqpt.VEHICLE_ID,
                               CarrierID: eqpt.CST_ID);
                            //1.通知MTL Car out完成
                            scApp.MTLService.carOutComplete(maintainLift);
                            //2.將該VH上報 Remove
                            Remove(eqpt.VEHICLE_ID);
                        }
                        break;
                    case CompleteStatus.CmpStatusMtlhome:
                        maintainLift = scApp.EquipmentBLL.cache.GetMaintainLiftByMTLHomeAdr(cur_adr_id);
                        if (maintainLift != null)
                            doAskVhToSystemInAddress(eqpt.VEHICLE_ID, maintainLift.MTL_SYSTEM_IN_ADDRESS);
                        //doAskVhToSystemInAddress(eqpt.VEHICLE_ID, MTLService.MTL_SYSTEM_IN_ADDRESS);
                        break;
                    case CompleteStatus.CmpStatusSystemIn:
                        var maintain_device = scApp.EquipmentBLL.cache.GetMaintainDeviceBySystemInAdr(cur_adr_id);
                        if (maintain_device != null)
                        {
                            scApp.MTLService.carInComplete(maintain_device, eqpt.VEHICLE_ID);
                            if (maintain_device is MaintainLift)
                            {
                                Install(eqpt.VEHICLE_ID);
                            }
                        }
                        break;
                    default:
                        //if (eqpt.MODE_STATUS == VHModeStatus.AutoMtl && eqpt.HAS_CST == 0)
                        //{
                        //    maintainLift = scApp.EquipmentBLL.cache.GetExcuteCarOutMTL(eqpt.VEHICLE_ID);
                        //    if (maintainLift != null)
                        //        doAskVhToSystemOutAddress(eqpt.VEHICLE_ID, maintainLift.MTL_SYSTEM_OUT_ADDRESS);
                        //}
                        //else if (eqpt.MODE_STATUS == VHModeStatus.AutoMts && eqpt.HAS_CST == 0)
                        //{
                        //    maintainSpace = scApp.EquipmentBLL.cache.GetExcuteCarOutMTS(eqpt.VEHICLE_ID);
                        //    if (maintainSpace != null)
                        //        doAskVhToSystemOutAddress(eqpt.VEHICLE_ID, maintainSpace.MTS_ADDRESS);
                        //}
                        //else if ((eqpt.MODE_STATUS == VHModeStatus.AutoRemote) && eqpt.HAS_CST == 0)
                        //{
                        scApp.VehicleBLL.DoIdleVehicleHandle_NoAction(eqpt.VEHICLE_ID);
                        //}
                        break;
                }
            }


            if (scApp.getEQObjCacheManager().getLine().SCStats == ALINE.TSCState.PAUSING)
            {
                List<ACMD_MCS> cmd_mcs_lst = scApp.CMDBLL.loadACMD_MCSIsUnfinished();
                if (cmd_mcs_lst.Count == 0)
                {
                    scApp.LineService.TSCStateToPause("");
                }
            }
            eqpt.onCommandComplete(completeStatus);
        }

        private bool replyCommandComplete(AVEHICLE eqpt, int seq_num, string finish_ohxc_cmd, string finish_mcs_cmd)
        {
            ID_32_TRANS_COMPLETE_RESPONSE send_str = new ID_32_TRANS_COMPLETE_RESPONSE
            {
                ReplyCode = 0
            };
            WrapperMessage wrapper = new WrapperMessage
            {
                SeqNum = seq_num,
                TranCmpResp = send_str
            };
            //Boolean resp_cmp = ITcpIpControl.sendGoogleMsg(bcfApp, tcpipAgentName, wrapper, true);
            Boolean resp_cmp = eqpt.sendMessage(wrapper, true);
            SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, seq_num, send_str, finish_ohxc_cmd, finish_mcs_cmd, resp_cmp.ToString());
            return resp_cmp;
        }

        private void TestCycleRun(AVEHICLE vh, string cmd_id)
        {
            ACMD_OHTC cmd = scApp.CMDBLL.getCMD_OHTCByID(cmd_id);
            if (cmd == null) return;
            if (!(cmd.CMD_TPYE == E_CMD_TYPE.LoadUnload || cmd.CMD_TPYE == E_CMD_TYPE.Move)) return;

            string result = string.Empty;
            string cst_id = cmd.CARRIER_ID?.Trim();
            string from_port_id = cmd.DESTINATION.Trim();
            string to_port_id = cmd.SOURCE.Trim();
            string from_adr = "";
            string to_adr = "";
            switch (cmd.CMD_TPYE)
            {
                case E_CMD_TYPE.LoadUnload:
                    scApp.MapBLL.getAddressID(from_port_id, out from_adr);
                    scApp.MapBLL.getAddressID(to_port_id, out to_adr);
                    break;
                case E_CMD_TYPE.Move:
                    to_adr = vh.startAdr.Trim();
                    break;
            }
            scApp.CMDBLL.doCreatTransferCommand(cmd.VH_ID,
                                            carrier_id: cst_id,
                                            cmd_type: cmd.CMD_TPYE,
                                            source: from_adr,
                                            destination: to_adr,
                                            gen_cmd_type: SCAppConstants.GenOHxCCommandType.Auto);
        }
        #endregion Command Complete Report

        #region Range Teach

        public void RangeTeachingCompleteReport(string tcpipAgentName, BCFApplication bcfApp, AVEHICLE eqpt, ID_172_RANGE_TEACHING_COMPLETE_REPORT recive_str, int seq_num)
        {
            SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, seq_num, recive_str);

            string from_adr = recive_str.FromAdr;
            string to_adr = recive_str.ToAdr;
            uint sec_distance = recive_str.SecDistance;
            int cmp_code = recive_str.CompleteCode;
            ID_72_RANGE_TEACHING_COMPLETE_RESPONSE response = null;
            if (cmp_code == 0)
            {
                if (scApp.MapBLL.updateSecDistance(from_adr, to_adr, sec_distance, out ASECTION section))
                {
                    scApp.updateCatchData_Section(section);
                    scApp.VehicleBLL.setAndPublishPositionReportInfo2Redis(eqpt.VEHICLE_ID, recive_str, section.SEC_ID);
                }
            }
            response = new ID_72_RANGE_TEACHING_COMPLETE_RESPONSE()
            {
                ReplyCode = 0
            };

            WrapperMessage wrapper = new WrapperMessage
            {
                SeqNum = seq_num,
                RangeTeachingCmpResp = response
            };
            Boolean resp_cmp = eqpt.sendMessage(wrapper, true);
            SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, seq_num, response, resp_cmp.ToString());

            AutoTeaching(eqpt.VEHICLE_ID);
        }

        public void AutoTeaching(string vh_id)
        {
            if (!sc.App.SystemParameter.AutoTeching) return;
            //1.找出VH，並得到他目前所在的Address。
            scApp.VehicleBLL.getAndProcPositionReportFromRedis(vh_id);
            AVEHICLE vh = scApp.VehicleBLL.getVehicleByID(vh_id);
            string vh_current_adr = vh.CUR_ADR_ID;
            List<string> base_address = new List<string> { vh.CUR_ADR_ID };
            HashSet<string> choess_sation = new HashSet<string>();

            do
            {
                //接著透過這個Address查詢哪些Section是該Address的From Adr.且還沒有Teching過的(LAST_TECH_TIME = null)
                List<ASECTION> sections = scApp.MapBLL.loadSectionByFromAdrs(base_address);
                base_address.Clear();
                foreach (var section in sections)
                {
                    if (section.SEC_TYPE == SectionType.Mtl) continue;

                    if (section.LAST_TECH_TIME.HasValue)
                    {
                        if (section.DIRC_DRIV == 0)
                            base_address.Add(section.TO_ADR_ID.Trim());
                    }
                    else
                    {
                        TechingAction(vh_id, vh_current_adr, section);
                        base_address.Clear();
                        break;
                    }
                }
                if (!scApp.MapBLL.hasNotYetTeachingSection())
                {
                    sc.App.SystemParameter.AutoTeching = false;
                    bcf.App.BCFApplication.onInfoMsg("All section teching complete.");
                    return;
                }
                //if (sections.Count == 1)
                //{
                //    ASECTION section = sections[0];
                //    //如果該Section已經Teching過，則繼續往下找
                //    if (section.LAST_TECH_TIME.HasValue)
                //    {
                //        base_address = section.TO_ADR_ID;
                //    }
                //    //如果該Section還沒有Teching過，則下達Teching的指令
                //    else
                //    {
                //        TechingAction(vh_id, vh_current_adr, base_address, section);
                //        break;
                //    }
                //}
                //else
                //{
                //    sections = sections.Where(sec => !choess_sation.Contains(sec.SEC_ID)).ToList();
                //    foreach (ASECTION section in sections)
                //    {
                //        if (section.LAST_TECH_TIME.HasValue)
                //        {
                //            if (sections.Last() == section)
                //            {
                //                base_address = section.TO_ADR_ID;
                //            }
                //            continue;
                //        }
                //        else
                //        {
                //            TechingAction(vh_id, vh_current_adr, base_address, section);
                //            break;
                //        }
                //    }
                //}
            } while (base_address.Count != 0);



        }

        private void TechingAction(string vh_id, string vh_current_adr, ASECTION section)
        {
            if (SCUtility.isMatche(section.FROM_ADR_ID, vh_current_adr))
            {
                TeachingRequest(vh_id, section.FROM_ADR_ID, section.TO_ADR_ID);
            }
            else
            {
                string[] ReutrnFromAdr2ToAdr = scApp.RouteGuide.DownstreamSearchSection
                    (vh_current_adr, section.FROM_ADR_ID, 1, true);
                string route = ReutrnFromAdr2ToAdr[0].Split('=')[0];
                string[] routeSection = route.Split(',');
                ASECTION first_sec = scApp.MapBLL.getSectiontByID(routeSection[0]);
                TeachingRequest(vh_id, vh_current_adr, first_sec.TO_ADR_ID);
                //scApp.CMDBLL.doCreatTransferCommand(vh_id
                //                              , string.Empty
                //                              , string.Empty
                //                              , E_CMD_TYPE.Move_Teaching
                //                              , vh_current_adr
                //                              , section.FROM_ADR_ID, 0, 0);
            }
        }
        #endregion Range Teach

        #region Receive Message
        public void BasicInfoVersionReport(BCFApplication bcfApp, AVEHICLE eqpt, ID_102_BASIC_INFO_VERSION_REP recive_str, int seq_num)
        {
            ID_2_BASIC_INFO_VERSION_RESPONSE send_str = new ID_2_BASIC_INFO_VERSION_RESPONSE
            {
                ReplyCode = 0
            };
            WrapperMessage wrapper = new WrapperMessage
            {
                SeqNum = seq_num,
                BasicInfoVersionResp = send_str
            };
            Boolean resp_cmp = eqpt.sendMessage(wrapper, true);
            //SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, seqNum, send_str, resp_cmp.ToString());
        }
        public void GuideDataUploadRequest(BCFApplication bcfApp, AVEHICLE eqpt, ID_162_GUIDE_DATA_UPLOAD_REP recive_str, int seq_num)
        {
            LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
               seq_num: seq_num,
               Data: recive_str,
               VehicleID: eqpt.VEHICLE_ID,
               CarrierID: eqpt.CST_ID);

            ID_62_GUID_DATA_UPLOAD_RESPONSE send_str = new ID_62_GUID_DATA_UPLOAD_RESPONSE
            {
                ReplyCode = 0
            };
            WrapperMessage wrapper = new WrapperMessage
            {
                SeqNum = seq_num,
                GUIDEDataUploadResp = send_str
            };
            LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
               seq_num: seq_num,
               Data: send_str,
               VehicleID: eqpt.VEHICLE_ID,
               CarrierID: eqpt.CST_ID);

            Boolean resp_cmp = eqpt.sendMessage(wrapper, true);
            //SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, seqNum, send_str, resp_cmp.ToString());
        }
        public void AddressTeachReport(BCFApplication bcfApp, AVEHICLE eqpt, ID_174_ADDRESS_TEACH_REPORT recive_str, int seq_num)
        {
            try
            {
                string adr_id = recive_str.Addr;
                int resolution = recive_str.Position;

                scApp.DataSyncBLL.updateAddressData(eqpt.VEHICLE_ID, adr_id, resolution);

                ID_74_ADDRESS_TEACH_RESPONSE send_str = new ID_74_ADDRESS_TEACH_RESPONSE
                {
                    ReplyCode = 0
                };
                WrapperMessage wrapper = new WrapperMessage
                {
                    SeqNum = seq_num,
                    AddressTeachResp = send_str
                };
                Boolean resp_cmp = eqpt.sendMessage(wrapper, true);
                //SCUtility.RecodeReportInfo(eqpt.VEHICLE_ID, seqNum, send_str, resp_cmp.ToString());
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception:");
            }
        }
        [ClassAOPAspect]
        public void AlarmReport(BCFApplication bcfApp, AVEHICLE eqpt, ID_194_ALARM_REPORT recive_str, int seq_num)
        {
            LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
              seq_num: seq_num, Data: recive_str,
              VehicleID: eqpt.VEHICLE_ID,
              CarrierID: eqpt.CST_ID);
            try
            {
                string node_id = eqpt.NODE_ID;
                string eq_id = eqpt.VEHICLE_ID;
                string current_adr = eqpt.CUR_ADR_ID;
                string err_code = recive_str.ErrCode;
                ErrorStatus status = recive_str.ErrStatus;

                List<ALARM> alarms = null;
                AlarmMap alarmMap = scApp.AlarmBLL.GetAlarmMap(node_id, err_code);
                //在設備上報Alarm時，如果是第一次上報(之前都沒有Alarm發生時，則要上報S6F11 CEID=51 Alarm Set)
                bool processBeferHasErrorExist = scApp.AlarmBLL.hasAlarmErrorExist();
                if (alarmMap != null &&
                    alarmMap.ALARM_LVL == E_ALARM_LVL.Error &&
                    status == ErrorStatus.ErrSet &&
                    //!scApp.AlarmBLL.hasAlarmErrorExist())
                    !processBeferHasErrorExist)
                {
                    scApp.ReportBLL.newReportAlarmSet();
                }
                scApp.getRedisCacheManager().BeginTransaction();
                using (TransactionScope tx = SCUtility.getTransactionScope())
                {
                    using (DBConnection_EF con = DBConnection_EF.GetUContext())
                    {
                        LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                           Data: $"Process vehicle alarm report.alarm code:{err_code},alarm status{status}",
                           VehicleID: eqpt.VEHICLE_ID,
                           CarrierID: eqpt.CST_ID);
                        ALARM alarm = null;
                        switch (status)
                        {
                            case ErrorStatus.ErrSet:
                                //將設備上報的Alarm填入資料庫。
                                alarm = scApp.AlarmBLL.setAlarmReport(node_id, eq_id, err_code, current_adr);
                                //將其更新至Redis，保存目前所發生的Alarm
                                scApp.AlarmBLL.setAlarmReport2Redis(alarm);
                                alarms = new List<ALARM>() { alarm };
                                break;
                            case ErrorStatus.ErrReset:
                                if (SCUtility.isMatche(err_code, "0"))
                                {
                                    alarms = scApp.AlarmBLL.resetAllAlarmReport(eq_id);
                                    scApp.AlarmBLL.resetAllAlarmReport2Redis(eq_id);
                                }
                                else
                                {
                                    //將設備上報的Alarm從資料庫刪除。
                                    alarm = scApp.AlarmBLL.resetAlarmReport(eq_id, err_code);
                                    //將其更新至Redis，保存目前所發生的Alarm
                                    scApp.AlarmBLL.resetAlarmReport2Redis(alarm);
                                    alarms = new List<ALARM>() { alarm };
                                }
                                break;
                        }
                        tx.Complete();
                    }
                }
                scApp.getRedisCacheManager().ExecuteTransaction();
                //通知有Alarm的資訊改變。
                scApp.getNatsManager().PublishAsync(SCAppConstants.NATS_SUBJECT_CURRENT_ALARM, new byte[0]);


                foreach (ALARM report_alarm in alarms)
                {
                    if (report_alarm == null) continue;
                    if (report_alarm.ALAM_LVL == E_ALARM_LVL.Warn ||
                        report_alarm.ALAM_LVL == E_ALARM_LVL.None) continue;
                    //需判斷Alarm是否存在如果有的話則需再判斷MCS是否有Disable該Alarm的上報
                    int ialarm_code = 0;
                    int.TryParse(report_alarm.ALAM_CODE, out ialarm_code);
                    string alarm_code = (ialarm_code < 0 ? ialarm_code * -1 : ialarm_code).ToString();
                    if (scApp.AlarmBLL.IsReportToHost(alarm_code))
                    {
                        //scApp.ReportBLL.ReportAlarmHappend(eqpt.VEHICLE_ID, alarm.ALAM_STAT, alarm.ALAM_CODE, alarm.ALAM_DESC, out reportqueues);
                        List<AMCSREPORTQUEUE> reportqueues = new List<AMCSREPORTQUEUE>();
                        if (report_alarm.ALAM_STAT == ErrorStatus.ErrSet)
                        {
                            scApp.ReportBLL.ReportAlarmHappend(report_alarm.ALAM_STAT, alarm_code, report_alarm.ALAM_DESC);
                            scApp.ReportBLL.newReportUnitAlarmSet(eqpt.Real_ID, alarm_code, report_alarm.ALAM_DESC, eqpt.CUR_ADR_ID, reportqueues);
                        }
                        else
                        {
                            scApp.ReportBLL.ReportAlarmHappend(report_alarm.ALAM_STAT, alarm_code, report_alarm.ALAM_DESC);
                            scApp.ReportBLL.newReportUnitAlarmClear(eqpt.Real_ID, alarm_code, report_alarm.ALAM_DESC, eqpt.CUR_ADR_ID, reportqueues);
                        }
                        scApp.ReportBLL.newSendMCSMessage(reportqueues);

                        LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                           Data: $"do report alarm to mcs,alarm code:{err_code},alarm status{status}",
                           VehicleID: eqpt.VEHICLE_ID,
                           CarrierID: eqpt.CST_ID);
                    }
                }
                //在設備上報取消Alarm，如果已經沒有Alarm(Alarm都已經消除，則要上報S6F11 CEID=52 Alarm Clear)
                bool processAfterHasErrorExist = scApp.AlarmBLL.hasAlarmErrorExist();
                scApp.getEQObjCacheManager().getLine().HasSeriousAlarmHappend = processAfterHasErrorExist;
                if (status == ErrorStatus.ErrReset &&
                    //!scApp.AlarmBLL.hasAlarmErrorExist())
                    processBeferHasErrorExist &&
                    !processAfterHasErrorExist)
                {
                    scApp.ReportBLL.newReportAlarmClear();
                }



                ID_94_ALARM_RESPONSE send_str = new ID_94_ALARM_RESPONSE
                {
                    ReplyCode = 0
                };
                WrapperMessage wrapper = new WrapperMessage
                {
                    SeqNum = seq_num,
                    AlarmResp = send_str
                };
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                  seq_num: seq_num, Data: send_str,
                  VehicleID: eqpt.VEHICLE_ID,
                  CarrierID: eqpt.CST_ID);

                Boolean resp_cmp = eqpt.sendMessage(wrapper, true);
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: $"do reply alarm report ,{resp_cmp}",
                   VehicleID: eqpt.VEHICLE_ID,
                   CarrierID: eqpt.CST_ID);

                scApp.LineService.SpecifySeriousAlarmCheck();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception:");
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Warn, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: ex,
                   VehicleID: eqpt.VEHICLE_ID,
                   CarrierID: eqpt.CST_ID);
            }
        }
        #endregion Receive Message

        #region MTL Handle
        public bool doReservationVhToMaintainsBufferAddress(string vhID, string mtlBufferAdtID)
        {
            bool isSuccess = true;
            using (TransactionScope tx = SCUtility.getTransactionScope())
            {
                using (DBConnection_EF con = DBConnection_EF.GetUContext())
                {
                    isSuccess = isSuccess && VehicleAutoModeCahnge(vhID, VHModeStatus.AutoMtl);
                    if (isSuccess)
                    {
                        tx.Complete();
                    }
                }
            }
            return isSuccess;
        }
        public bool doReservationVhToMaintainsSpace(string vhID)
        {
            bool isSuccess = true;
            using (TransactionScope tx = SCUtility.getTransactionScope())
            {
                using (DBConnection_EF con = DBConnection_EF.GetUContext())
                {
                    isSuccess = isSuccess && VehicleAutoModeCahnge(vhID, VHModeStatus.AutoMts);
                    if (isSuccess)
                    {
                        tx.Complete();
                    }
                }
            }
            return isSuccess;
        }
        public bool doAskVhToSystemOutAddress(string vhID, string carOutBufferAdr)
        {
            bool isSuccess = true;
            isSuccess = scApp.CMDBLL.doCreatTransferCommand(vh_id: vhID, cmd_type: E_CMD_TYPE.SystemOut, destination: carOutBufferAdr);
            return isSuccess;
        }

        public bool doAskVhToMaintainsAddress(string vhID, string mtlAdtID)
        {
            bool isSuccess = true;
            isSuccess = isSuccess && scApp.CMDBLL.doCreatTransferCommand(vh_id: vhID, cmd_type: E_CMD_TYPE.MoveToMTL, destination: mtlAdtID);
            return isSuccess;
        }
        public bool doAskVhToCarInBufferAddress(string vhID, string carInBufferAdr)
        {
            bool isSuccess = true;
            isSuccess = scApp.CMDBLL.doCreatTransferCommand(vh_id: vhID, cmd_type: E_CMD_TYPE.MTLHome, destination: carInBufferAdr);
            return isSuccess;
        }

        public bool doAskVhToSystemInAddress(string vhID, string systemInAdr)
        {
            bool isSuccess = true;
            isSuccess = scApp.CMDBLL.doCreatTransferCommand(vh_id: vhID, cmd_type: E_CMD_TYPE.SystemIn, destination: systemInAdr);
            return isSuccess;
        }

        public bool doRecoverModeStatusToAutoRemote(string vh_id)
        {
            return VehicleAutoModeCahnge(vh_id, VHModeStatus.AutoRemote);
        }


        #endregion MTL Handle

        #region Vehicle Change The Path
        public void VhicleChangeThePath(string vh_id, bool isNeedPauseFirst)
        {
            string ohxc_cmd_id = "";
            try
            {
                bool isSuccess = true;
                AVEHICLE need_change_path_vh = scApp.getEQObjCacheManager().getVehicletByVHID(vh_id);
                if (need_change_path_vh.VhRecentTranEvent == EventType.Vhloading ||
                    need_change_path_vh.VhRecentTranEvent == EventType.Vhunloading)
                    return;
                //1.先下暫停給該台VH
                if (isNeedPauseFirst)
                    isSuccess = PauseRequest(vh_id, PauseEvent.Pause, OHxCPauseType.Normal);
                //2.送出31執行命令的Override
                //  a.取得執行中的命令
                //  b.重新將該命令改成Ready to rewrite
                ACMD_OHTC cmd_ohtc = null;
                using (TransactionScope tx = SCUtility.getTransactionScope())
                {
                    using (DBConnection_EF con = DBConnection_EF.GetUContext())
                    {
                        isSuccess &= scApp.CMDBLL.updateCMD_OHxC_Status2ReadyToReWirte(need_change_path_vh.OHTC_CMD, out cmd_ohtc);
                        isSuccess &= scApp.CMDBLL.update_CMD_Detail_2AbnormalFinsh(need_change_path_vh.OHTC_CMD, need_change_path_vh.WillPassSectionID);
                        if (isSuccess)
                            tx.Complete();
                    }
                }
                ohxc_cmd_id = cmd_ohtc.CMD_ID.Trim();
                scApp.VehicleService.doSendOHxCOverrideCmdToVh(need_change_path_vh, cmd_ohtc, isNeedPauseFirst);
            }
            catch (BLL.VehicleBLL.BlockedByTheErrorVehicleException blockedExecption)
            {
                logger.Warn(blockedExecption, "BlockedByTheErrorVehicleException:");
                //VehicleBlockedByTheErrorVehicle();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception:");
            }
        }
        private void VehicleBlockedByTheErrorVehicle()
        {
            ALARM alarm = scApp.AlarmBLL.setAlarmReport(SCAppConstants.System_ID, SCAppConstants.System_ID, MainAlarmCode.OHxC_BOLCKED_BY_THE_ERROR_VEHICLE_0_1, "");
            if (alarm != null)
            {
                //scApp.AlarmBLL.onMainAlarm(SCAppConstants.MainAlarmCode.OHxC_BOLCKED_BY_THE_ERROR_VEHICLE_0_1,
                //                           vh_id,
                //                           ohxc_cmd_id);
                List<AMCSREPORTQUEUE> reportqueues = null;
                //scApp.ReportBLL.ReportAlarmHappend(alarm.EQPT_ID, alarm.ALAM_STAT, alarm.ALAM_CODE, alarm.ALAM_DESC, out reportqueues);
                scApp.LineBLL.updateHostControlState(LineHostControlState.HostControlState.On_Line_Local);
            }
        }
        #endregion Vehicle Change The Path
        public bool VehicleAutoModeCahnge(string vh_id, VHModeStatus mode_status)
        {
            AVEHICLE vh = scApp.VehicleBLL.getVehicleByID(vh_id);
            lock (vh.StatusUpdate_Sync)
            {
                if (vh.MODE_STATUS != VHModeStatus.Manual)
                {
                    scApp.VehicleBLL.updataVehicleMode(vh_id, mode_status);
                    vh.NotifyVhStatusChange();
                    return true;
                }
            }
            return false;
        }
        #region Vh connection / disconnention
        [ClassAOPAspect]
        public void Connection(BCFApplication bcfApp, AVEHICLE vh)
        {
            //scApp.getEQObjCacheManager().refreshVh(eqpt.VEHICLE_ID);
            vh.VhRecentTranEvent = EventType.AdrPass;

            vh.isTcpIpConnect = true;

            LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
               Data: "Connection ! Begin synchronize with vehicle...",
               VehicleID: vh.VEHICLE_ID,
               CarrierID: vh.CST_ID);
            VehicleInfoSynchronize(vh.VEHICLE_ID);
            LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
               Data: "Connection ! End synchronize with vehicle.",
               VehicleID: vh.VEHICLE_ID,
               CarrierID: vh.CST_ID);
            SCUtility.RecodeConnectionInfo
                (vh.VEHICLE_ID,
                SCAppConstants.RecodeConnectionInfo_Type.Connection.ToString(),
                vh.getDisconnectionIntervalTime(bcfApp));
        }
        [ClassAOPAspect]
        public void Disconnection(BCFApplication bcfApp, AVEHICLE vh)
        {
            vh.isTcpIpConnect = false;

            LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
               Data: "Disconnection !",
               VehicleID: vh.VEHICLE_ID,
               CarrierID: vh.CST_ID);
            SCUtility.RecodeConnectionInfo
                (vh.VEHICLE_ID,
                SCAppConstants.RecodeConnectionInfo_Type.Disconnection.ToString(),
                vh.getConnectionIntervalTime(bcfApp));
        }
        #endregion Vh Connection / disconnention

        #region Vehicle Install/Remove
        public void Install(string vhID)
        {
            try
            {

                bool is_success = true;

                is_success = is_success && scApp.VehicleBLL.updataVehicleInstall(vhID);
                if (is_success)
                {
                    AVEHICLE vh_vo = scApp.VehicleBLL.cache.getVhByID(vhID);
                    vh_vo.VehicleInstall();
                }
                List<AMCSREPORTQUEUE> reportqueues = new List<AMCSREPORTQUEUE>();
                is_success = is_success && scApp.ReportBLL.newReportVehicleInstalled(vhID, reportqueues);
                scApp.ReportBLL.newSendMCSMessage(reportqueues);

            }
            catch (Exception ex)
            {
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Warn, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: ex,
                   VehicleID: vhID);
            }
        }
        public void Remove(string vhID)
        {
            try
            {
                bool is_success = true;
                is_success = is_success && scApp.VehicleBLL.updataVehicleRemove(vhID);
                if (is_success)
                {
                    AVEHICLE vh_vo = scApp.VehicleBLL.cache.getVhByID(vhID);
                    vh_vo.VechileRemove();
                }
                List<AMCSREPORTQUEUE> reportqueues = new List<AMCSREPORTQUEUE>();
                is_success = is_success && scApp.ReportBLL.newReportVehicleRemoved(vhID, reportqueues);
                scApp.ReportBLL.newSendMCSMessage(reportqueues);
            }
            catch (Exception ex)
            {
                LogHelper.Log(logger: logger, LogLevel: LogLevel.Warn, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                   Data: ex,
                   VehicleID: vhID);
            }
        }
        #endregion Vehicle Install/Remove
        public DynamicMetaObject GetMetaObject(Expression parameter)
        {

            return new AspectWeaver(parameter, this);
        }

        #region Specially Control
        public void forceReleaseBlockControl(string vh_id = "")
        {
            List<BLOCKZONEQUEUE> queues = null;
            using (DBConnection_EF con = DBConnection_EF.GetUContext())
            {

                if (SCUtility.isEmpty(vh_id))
                {
                    queues = scApp.MapBLL.loadAllNonReleaseBlockQueue();
                }
                else
                {
                    queues = scApp.MapBLL.loadNonReleaseBlockQueueByCarID(vh_id);
                }


                foreach (var queue in queues)
                {
                    LogHelper.Log(logger: logger, LogLevel: LogLevel.Info, Class: nameof(VehicleService), Device: DEVICE_NAME_OHx,
                       Data: $"force relase block ,block info: {queue.ToString()}");

                    scApp.MapBLL.updateBlockZoneQueue_AbnormalEnd(queue, SCAppConstants.BlockQueueState.Abnormal_Release_ForceRelease);
                    scApp.MapBLL.DeleteBlockControlKeyWordToRedisAsync(queue.CAR_ID.Trim(), queue.ENTRY_SEC_ID);

                }
            }
        }

        public void reCheckBlockControl(BLOCKZONEQUEUE blockZoneQueue)
        {
            ABLOCKZONEMASTER blockmaster = scApp.MapBLL.getBlockZoneMasterByEntrySecID(blockZoneQueue.ENTRY_SEC_ID);
            if (blockmaster != null)
            {
                List<string> lstSecid = scApp.MapBLL.loadBlockZoneDetailSecIDsByEntrySecID(blockZoneQueue.ENTRY_SEC_ID);
                if (!scApp.VehicleBLL.hasVehicleOnSections(lstSecid))
                {
                    using (TransactionScope tx = SCUtility.getTransactionScope())
                    {
                        using (DBConnection_EF con = DBConnection_EF.GetUContext())
                        {
                            scApp.MapBLL.updateBlockZoneQueue_AbnormalEnd(blockZoneQueue, SCAppConstants.BlockQueueState.Abnormal_Release_ForceRelease);
                            scApp.MapBLL.DeleteBlockControlKeyWordToRedisAsync(blockZoneQueue.CAR_ID, blockZoneQueue.ENTRY_SEC_ID);
                            tx.Complete();
                        }
                    }
                    blockZoneReleaseScript(blockmaster);
                }
            }
        }
        public void releaseBlockControl(string vh_id)
        {

            List<BLOCKZONEQUEUE> queueList = scApp.MapBLL.loadNonReleaseBlockQueueByCarID(vh_id);
            if (queueList != null && queueList.Count > 0)
            {
                foreach(BLOCKZONEQUEUE queue in queueList)
                {
                    ABLOCKZONEMASTER blockmaster = scApp.MapBLL.getBlockZoneMasterByEntrySecID(queue.ENTRY_SEC_ID);
                    if (blockmaster != null)
                    {
                        List<string> lstSecid = scApp.MapBLL.loadBlockZoneDetailSecIDsByEntrySecID(queue.ENTRY_SEC_ID);
                        if (!scApp.VehicleBLL.hasVehicleOnSections(lstSecid))
                        {
                            using (TransactionScope tx = SCUtility.getTransactionScope())
                            {
                                using (DBConnection_EF con = DBConnection_EF.GetUContext())
                                {
                                    scApp.MapBLL.updateBlockZoneQueue_AbnormalEnd(queue, SCAppConstants.BlockQueueState.Release);
                                    scApp.MapBLL.DeleteBlockControlKeyWordToRedisAsync(queue.CAR_ID, queue.ENTRY_SEC_ID);
                                    tx.Complete();
                                }
                            }
                        }
                    }
                }
            }
            

        }

        public void PauseAllVehicleByOHxCPause()
        {
            List<AVEHICLE> vhs = scApp.getEQObjCacheManager().getAllVehicle();
            foreach (var vh in vhs)
            {
                PauseRequest(vh.VEHICLE_ID, PauseEvent.Pause, OHxCPauseType.Earthquake);
            }
        }
        public void ResumeAllVehicleByOhxCPause()
        {
            List<AVEHICLE> vhs = scApp.getEQObjCacheManager().getAllVehicle();
            foreach (var vh in vhs)
            {
                PauseRequest(vh.VEHICLE_ID, PauseEvent.Continue, OHxCPauseType.Earthquake);
            }
        }

        #endregion Specially Control


        #region RoadService Mark
        //public ASEGMENT doEnableDisableSegment(string segment_id, E_PORT_STATUS port_status, string laneCutType)
        //{
        //    ASEGMENT segment = null;
        //    try
        //    {
        //        List<APORTSTATION> port_stations = scApp.MapBLL.loadAllPortBySegmentID(segment_id);

        //        using (TransactionScope tx = SCUtility.getTransactionScope())
        //        {
        //            using (DBConnection_EF con = DBConnection_EF.GetUContext())
        //            {
        //                switch (port_status)
        //                {
        //                    case E_PORT_STATUS.InService:
        //                        segment = scApp.RouteGuide.OpenSegment(segment_id);
        //                        break;
        //                    case E_PORT_STATUS.OutOfService:
        //                        segment = scApp.RouteGuide.CloseSegment(segment_id);
        //                        break;
        //                }
        //                foreach (APORTSTATION port_station in port_stations)
        //                {
        //                    scApp.MapBLL.updatePortStatus(port_station.PORT_ID, port_status);
        //                    scApp.getEQObjCacheManager().getPortStation(port_station.PORT_ID).PORT_STATUS = port_status;
        //                }
        //                tx.Complete();
        //            }
        //        }
        //        List<AMCSREPORTQUEUE> reportqueues = new List<AMCSREPORTQUEUE>();
        //        List<ASECTION> sections = scApp.MapBLL.loadSectionsBySegmentID(segment_id);
        //        string segment_start_adr = sections.First().FROM_ADR_ID;
        //        string segment_end_adr = sections.Last().TO_ADR_ID;
        //        switch (port_status)
        //        {
        //            case E_PORT_STATUS.InService:
        //                scApp.ReportBLL.newReportLaneInService(segment_start_adr, segment_end_adr, laneCutType, reportqueues);
        //                break;
        //            case E_PORT_STATUS.OutOfService:
        //                scApp.ReportBLL.newReportLaneOutOfService(segment_start_adr, segment_end_adr, laneCutType, reportqueues);
        //                break;
        //        }
        //        foreach (APORTSTATION port_station in port_stations)
        //        {
        //            switch (port_status)
        //            {
        //                case E_PORT_STATUS.InService:
        //                    scApp.ReportBLL.newReportPortInServeice(port_station.PORT_ID, reportqueues);
        //                    break;
        //                case E_PORT_STATUS.OutOfService:
        //                    scApp.ReportBLL.newReportPortOutOfService(port_station.PORT_ID, reportqueues);
        //                    break;
        //            }
        //        }
        //        scApp.ReportBLL.newSendMCSMessage(reportqueues);
        //    }
        //    catch (Exception ex)
        //    {
        //        segment = null;
        //        logger.Error(ex, "Exception:");
        //    }
        //    return segment;
        //}
        #endregion RoadService

        #region TEST
        private void CarrierInterfaceSim_LoadComplete(AVEHICLE vh)
        {
            //vh.CatchPLCCSTInterfacelog();
            bool[] bools_01 = new bool[16];
            bool[] bools_02 = new bool[16];
            bool[] bools_03 = new bool[16];
            bool[] bools_04 = new bool[16];
            bool[] bools_05 = new bool[16];
            bool[] bools_06 = new bool[16];
            bool[] bools_07 = new bool[16];
            bool[] bools_08 = new bool[16];
            bool[] bools_09 = new bool[16];
            bool[] bools_10 = new bool[16];

            bools_01[3] = true;

            bools_02[03] = true; bools_02[08] = true; bools_02[12] = true; bools_02[14] = true;
            bools_02[15] = true;

            bools_03[3] = true; bools_03[8] = true; bools_03[10] = true; bools_03[12] = true;
            bools_03[14] = true; bools_03[15] = true;

            bools_04[3] = true; bools_04[4] = true; bools_04[8] = true; bools_04[10] = true;
            bools_04[12] = true; bools_04[14] = true; bools_04[15] = true;

            bools_05[3] = true; bools_05[4] = true; bools_05[8] = true; bools_05[10] = true;
            bools_05[11] = true; bools_05[12] = true; bools_05[14] = true; bools_05[15] = true;

            bools_06[3] = true; bools_06[4] = true; bools_06[5] = true; bools_06[8] = true;
            bools_06[10] = true; bools_06[11] = true; bools_06[12] = true; bools_06[14] = true;
            bools_06[15] = true;

            bools_07[3] = true; bools_07[4] = true; bools_07[5] = true; bools_07[10] = true;
            bools_07[11] = true; bools_07[12] = true; bools_07[14] = true; bools_07[15] = true;

            bools_08[3] = true; bools_08[6] = true; bools_08[10] = true; bools_08[11] = true;
            bools_08[12] = true; bools_08[14] = true; bools_08[15] = true;

            bools_09[3] = true; bools_09[6] = true; bools_09[10] = true; bools_09[12] = true;
            bools_09[14] = true; bools_09[15] = true;

            bools_10[3] = true;

            List<bool[]> lst_bools = new List<bool[]>()
            {
                bools_01,bools_02,bools_03,bools_04,bools_05,bools_06,bools_07,bools_08,bools_09,bools_10,
            };
            if (DebugParameter.isTestCarrierInterfaceError)
            {
                RandomSetCSTInterfaceBool(bools_03);
                RandomSetCSTInterfaceBool(bools_04);
                RandomSetCSTInterfaceBool(bools_05);
                RandomSetCSTInterfaceBool(bools_06);
                RandomSetCSTInterfaceBool(bools_07);
                RandomSetCSTInterfaceBool(bools_08);
                RandomSetCSTInterfaceBool(bools_09);
                //lst_bools[6][11] = false;
            }
            string port_id = "";
            scApp.MapBLL.getPortID(vh.CUR_ADR_ID, out port_id);

            scApp.PortBLL.OperateCatch.updatePortStationCSTExistStatus(port_id, string.Empty);

            CarrierInterface_LogOut(vh.VEHICLE_ID, port_id, lst_bools);
        }

        private static void RandomSetCSTInterfaceBool(bool[] bools_03)
        {
            Random rnd_Index = new Random(Guid.NewGuid().GetHashCode());
            int rnd_value_1 = rnd_Index.Next(bools_03.Length - 1);
            int rnd_value_2 = rnd_Index.Next(bools_03.Length - 1);
            int rnd_value_3 = rnd_Index.Next(bools_03.Length - 1);
            int rnd_value_4 = rnd_Index.Next(bools_03.Length - 1);
            int rnd_value_5 = rnd_Index.Next(bools_03.Length - 1);
            int rnd_value_6 = rnd_Index.Next(bools_03.Length - 1);
            bools_03[rnd_value_1] = true;
            bools_03[rnd_value_2] = true;
            bools_03[rnd_value_3] = true;
            bools_03[rnd_value_4] = true;
            bools_03[rnd_value_5] = true;
            bools_03[rnd_value_6] = true;
        }

        private void CarrierInterfaceSim_UnloadComplete(AVEHICLE vh, string carrier_id)
        {
            //vh.CatchPLCCSTInterfacelog();
            VehicleCSTInterface vehicleCSTInterface = new VehicleCSTInterface();
            bool[] bools_01 = new bool[16];
            bool[] bools_02 = new bool[16];
            bool[] bools_03 = new bool[16];
            bool[] bools_04 = new bool[16];
            bool[] bools_05 = new bool[16];
            bool[] bools_06 = new bool[16];
            bool[] bools_07 = new bool[16];
            bool[] bools_08 = new bool[16];
            bool[] bools_09 = new bool[16];
            bool[] bools_10 = new bool[16];

            bools_01[3] = true;

            bools_02[03] = true; bools_02[9] = true; bools_02[12] = true; bools_02[14] = true;
            bools_02[15] = true;

            bools_03[3] = true; bools_03[9] = true; bools_03[10] = true; bools_03[12] = true;
            bools_03[14] = true; bools_03[15] = true;

            bools_04[3] = true; bools_04[4] = true; bools_04[9] = true; bools_04[10] = true;
            bools_04[12] = true; bools_04[14] = true; bools_04[15] = true;

            bools_05[3] = true; bools_05[4] = true; bools_05[9] = true; bools_05[10] = true;
            bools_05[11] = true; bools_05[12] = true; bools_05[14] = true; bools_05[15] = true;

            bools_06[3] = true; bools_06[4] = true; bools_06[5] = true; bools_06[9] = true;
            bools_06[10] = true; bools_06[11] = true; bools_06[12] = true; bools_06[14] = true;
            bools_06[15] = true;

            bools_07[3] = true; bools_07[4] = true; bools_07[5] = true; bools_07[10] = true;
            bools_07[11] = true; bools_07[12] = true; bools_07[14] = true; bools_07[15] = true;

            bools_08[3] = true; bools_08[6] = true; bools_08[10] = true; bools_08[11] = true;
            bools_08[12] = true; bools_08[14] = true; bools_08[15] = true;

            bools_09[3] = true; bools_09[6] = true; bools_09[10] = true; bools_09[12] = true;
            bools_09[14] = true; bools_09[15] = true;

            bools_10[3] = true;
            List<bool[]> lst_bools = new List<bool[]>()
            {
                bools_01,bools_02,bools_03,bools_04,bools_05,bools_06,bools_07,bools_08,bools_09,bools_10,
            };
            if (DebugParameter.isTestCarrierInterfaceError)
            {
                RandomSetCSTInterfaceBool(bools_03);
                RandomSetCSTInterfaceBool(bools_04);
                RandomSetCSTInterfaceBool(bools_05);
                RandomSetCSTInterfaceBool(bools_06);
                RandomSetCSTInterfaceBool(bools_07);
                RandomSetCSTInterfaceBool(bools_08);
                RandomSetCSTInterfaceBool(bools_09);
            }
            string port_id = "";
            scApp.MapBLL.getPortID(vh.CUR_ADR_ID, out port_id);
            scApp.PortBLL.OperateCatch.updatePortStationCSTExistStatus(port_id, carrier_id);

            CarrierInterface_LogOut(vh.VEHICLE_ID, port_id, lst_bools);
        }

        private static void CarrierInterface_LogOut(string vh_id, string port_id, List<bool[]> lst_bools)
        {
            VehicleCSTInterface vehicleCSTInterface = new VehicleCSTInterface();
            foreach (var bools in lst_bools)
            {
                DateTime now_time = DateTime.Now;
                vehicleCSTInterface.Details.Add(new VehicleCSTInterface.CSTInterfaceDetail()
                {
                    EQ_ID = vh_id,
                    //PORT_ID = port_id,
                    LogIndex = $"Recode{nameof(VehicleCSTInterface)}",
                    CSTInterface = bools,
                    Year = (ushort)now_time.Year,
                    Month = (ushort)now_time.Month,
                    Day = (ushort)now_time.Day,
                    Hour = (ushort)now_time.Hour,
                    Minute = (ushort)now_time.Minute,
                    Second = (ushort)now_time.Second,
                    Millisecond = (ushort)now_time.Millisecond,
                });
                SpinWait.SpinUntil(() => false, 100);
            }
            foreach (var detail in vehicleCSTInterface.Details)
            {
                LogManager.GetLogger("RecodeVehicleCSTInterface").Info(detail.ToString());
            }
        }

        #endregion TEST
    }
}
