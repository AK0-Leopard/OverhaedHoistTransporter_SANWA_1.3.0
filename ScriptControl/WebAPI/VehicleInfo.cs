using com.mirle.ibg3k0.sc.App;
using com.mirle.ibg3k0.sc.Common;
using com.mirle.ibg3k0.sc.ProtocolFormat.OHTMessage;
using Nancy;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace com.mirle.ibg3k0.sc.WebAPI
{
    public class VehicleInfo : NancyModule
    {
        SCApplication app = null;
        const string restfulContentType = "application/json; charset=utf-8";
        const string urlencodedContentType = "application/x-www-form-urlencoded; charset=utf-8";
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        public VehicleInfo()
        {
            //app = SCApplication.getInstance();
            RegisterVehilceEvent();
            RegisterMapEvent();
            RegisterPortStationEvent();
            RegisterSegmentEvent();
            After += ctx => ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        }
        private void RegisterPortStationEvent()
        {
            Post["PortStation/PriorityUpdate"] = (p) =>
            {
                var scApp = SCApplication.getInstance();
                bool isSuccess = true;
                string result = string.Empty;

                string port_id = Request.Query.port_id.Value ?? Request.Form.port_id.Value ?? string.Empty;
                string priority = Request.Query.priority.Value ?? Request.Form.priority.Value ?? string.Empty;
                try
                {
                    int i_priority = 0;
                    isSuccess = int.TryParse(priority, out i_priority);
                    if (isSuccess)
                    {
                        isSuccess = scApp.PortStationService.doUpdatePortStationPriority(port_id, i_priority);
                    }
                }
                catch (Exception ex)
                {
                    isSuccess = false;
                    result = "Execption happend!";
                    logger.Error(ex, "Execption:");
                }

                var response = (Response)result;
                response.ContentType = restfulContentType;
                return response;
            };
            Post["PortStation/StatusChange"] = (p) =>
            {
                var scApp = SCApplication.getInstance();
                bool isSuccess = true;
                string result = string.Empty;

                string port_id = Request.Query.port_id.Value ?? Request.Form.port_id.Value ?? string.Empty;
                string status = Request.Query.status.Value ?? Request.Form.status.Value ?? string.Empty;
                com.mirle.ibg3k0.sc.ProtocolFormat.OHTMessage.PortStationServiceStatus service_status =
                default(com.mirle.ibg3k0.sc.ProtocolFormat.OHTMessage.PortStationServiceStatus);
                try
                {
                    isSuccess = Enum.TryParse(status, out service_status);
                    if (isSuccess)
                    {
                        isSuccess = scApp.PortStationService.doUpdatePortStationServiceStatus(port_id, (int)service_status);
                    }
                }
                catch (Exception ex)
                {
                    isSuccess = false;
                    result = "Execption happend!";
                    logger.Error(ex, "Execption:");
                }

                var response = (Response)result;
                response.ContentType = restfulContentType;
                return response;
            };


        }

        private void RegisterSegmentEvent()
        {
            Post["Segment/StatusUpdate"] = (p) =>
            {
                var scApp = SCApplication.getInstance();
                bool isSuccess = true;
                string result = string.Empty;

                string seg_id = Request.Query.seg_id.Value ?? Request.Form.seg_id.Value ?? string.Empty;
                string type = Request.Query.type.Value ?? Request.Form.type.Value ?? string.Empty;
                string satus = Request.Query.satus.Value ?? Request.Form.satus.Value ?? string.Empty;
                try
                {
                    Enum.TryParse(type, out sc.ASEGMENT.DisableType disableType);
                    Enum.TryParse(satus, out sc.E_SEG_STATUS e_status);

                    E_PORT_STATUS port_status = e_status == E_SEG_STATUS.Active ?
                                                E_PORT_STATUS.InService : E_PORT_STATUS.OutOfService;
                    switch (disableType)
                    {
                        case ASEGMENT.DisableType.Safety:
                            var enable_result = scApp.RoadControlService.RecoverCVEnable(seg_id);
                            isSuccess = enable_result.isSuccess;
                            result = enable_result.reason;
                            break;
                        default:
                            isSuccess = scApp.RoadControlService.doEnableDisableSegment(seg_id, port_status, disableType, Data.SECS.CSOT.SECSConst.LANECUTTYPE_LaneCutOnHMI);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    isSuccess = false;
                    result = "Execption happend!";
                    logger.Error(ex, "Execption:");
                }

                var response = (Response)result;
                response.ContentType = restfulContentType;
                return response;
            };
        }

        private void RegisterMapEvent()
        {
            Get["MapInfo/{MapInfoDataType}"] = (p) =>
            {
                bool isSuccess = false;
                SCApplication scApp = SCApplication.getInstance();
                string map_data_type = p.MapInfoDataType;
                SCAppConstants.MapInfoDataType dataType = default(SCAppConstants.MapInfoDataType);
                isSuccess = Enum.TryParse(map_data_type, out dataType);
                string query_data = null;
                switch (dataType)
                {
                    case SCAppConstants.MapInfoDataType.MapID:
                        query_data = scApp.BC_ID;
                        break;
                    case SCAppConstants.MapInfoDataType.EFConnectionString:
                        string connectionName = "OHTC_DevEntities";
                        query_data = System.Configuration.ConfigurationManager.ConnectionStrings[connectionName].ConnectionString;
                        break;
                    case SCAppConstants.MapInfoDataType.Rail:
                        query_data = JsonConvert.SerializeObject(scApp.MapBLL.loadAllRail());
                        break;
                    case SCAppConstants.MapInfoDataType.Point:
                        query_data = JsonConvert.SerializeObject(scApp.MapBLL.loadAllPoint());
                        break;
                    case SCAppConstants.MapInfoDataType.GroupRails:
                        query_data = JsonConvert.SerializeObject(scApp.MapBLL.loadAllGroupRail());
                        break;
                    case SCAppConstants.MapInfoDataType.Address:
                        query_data = JsonConvert.SerializeObject(scApp.MapBLL.loadAllAddress());
                        break;
                    case SCAppConstants.MapInfoDataType.Section:
                        query_data = JsonConvert.SerializeObject(scApp.MapBLL.loadAllSection());
                        break;
                    case SCAppConstants.MapInfoDataType.Segment:
                        //query_data = JsonConvert.SerializeObject(scApp.MapBLL.loadAllSegments());
                        query_data = JsonConvert.SerializeObject(scApp.getCommObjCacheManager().getSegments());
                        break;
                    case SCAppConstants.MapInfoDataType.Port:
                        query_data = JsonConvert.SerializeObject(scApp.MapBLL.loadAllPort());
                        break;
                    case SCAppConstants.MapInfoDataType.PortIcon:
                        query_data = JsonConvert.SerializeObject(scApp.MapBLL.loadAllPortIcon());
                        break;
                    case SCAppConstants.MapInfoDataType.Vehicle:
                        query_data = JsonConvert.SerializeObject(scApp.getEQObjCacheManager().getAllVehicle());
                        break;
                    case SCAppConstants.MapInfoDataType.Line:
                        query_data = JsonConvert.SerializeObject(scApp.getEQObjCacheManager().getLine());
                        break;
                    case SCAppConstants.MapInfoDataType.BlockZoneDetail:
                        query_data = JsonConvert.SerializeObject(scApp.MapBLL.loadAllBlockZoneDetail());
                        break;
                    case SCAppConstants.MapInfoDataType.MTL:
                        query_data = JsonConvert.SerializeObject(scApp.getEQObjCacheManager().getAllEquipment().Where(eq => eq.EQPT_ID.StartsWith("MTL")).ToList());
                        break;
                    case SCAppConstants.MapInfoDataType.MTS:
                        query_data = JsonConvert.SerializeObject(scApp.getEQObjCacheManager().getAllEquipment().Where(eq => eq.EQPT_ID.StartsWith("MTS")).ToList());
                        break;
                    case SCAppConstants.MapInfoDataType.Eqpt:
                        query_data = JsonConvert.SerializeObject(scApp.getEQObjCacheManager().getAllEquipment());
                        break;
                    case SCAppConstants.MapInfoDataType.AlarmMap:
                        query_data = JsonConvert.SerializeObject(scApp.AlarmBLL.loadAlarmMaps());
                        break;

                }
                var response = (Response)query_data;
                response.ContentType = restfulContentType;

                return response;
            };

            Get["SystemExcuteInfo/{SystemExcuteInfoType}"] = (p) =>
            {
                bool isSuccess = false;
                SCApplication scApp = SCApplication.getInstance();
                string map_data_type = p.SystemExcuteInfoType;
                SCAppConstants.SystemExcuteInfoType dataType = default(SCAppConstants.SystemExcuteInfoType);
                isSuccess = Enum.TryParse(map_data_type, out dataType);
                string query_data = "";
                switch (dataType)
                {
                    case SCAppConstants.SystemExcuteInfoType.CommandInQueueCount:
                        query_data = scApp.CMDBLL.getCMD_MCSIsQueueCount().ToString();
                        break;
                    case SCAppConstants.SystemExcuteInfoType.CommandInExcuteCount:
                        query_data = scApp.CMDBLL.getCMD_MCSIsRunningCount().ToString();
                        break;
                }
                var response = (Response)query_data;
                response.ContentType = restfulContentType;

                return response;
            };
        }

        private void RegisterVehilceEvent()
        {
            Get["AVEHICLES/{ID}"] = (p) =>
            {
                string vh_id = p.ID;
                AVEHICLE vh = SCApplication.getInstance().VehicleBLL.getVehicleByID(vh_id);
                var response = (Response)vh.ToString();
                response.ContentType = restfulContentType;

                return response;
            };
            Get["AVEHICLES"] = (p) =>
            {

                string vh_id = p.ID;
                List<AVEHICLE> vhs = SCApplication.getInstance().getEQObjCacheManager().getAllVehicle();
                var response = (Response)JsonConvert.SerializeObject(vhs);
                response.ContentType = restfulContentType;

                return response;
            };
            //Get["AVEHICLES/(?<all>)"] = (p) =>
            Get["AVEHICLES/_search"] = (p) =>
            {
                List<AVEHICLE> vhs = null;

                foreach (string name in Request.Query)
                {
                    switch (name)
                    {
                        case "SectionID":
                            string sec_id = Request.Query[name] ?? string.Empty;
                            vhs = SCApplication.getInstance().VehicleBLL.loadVehicleBySEC_ID(sec_id);
                            break;
                    }
                }
                var response = (Response)JsonConvert.SerializeObject(vhs);
                response.ContentType = restfulContentType;

                return response;
            };

            Get["metrics"] = (p) =>
            {
                int total_idle_vh_clean = SCApplication.getInstance().VehicleBLL.getNoExcuteMcsCmdVhCount(E_VH_TYPE.Clean);
                int total_idle_vh_Dirty = SCApplication.getInstance().VehicleBLL.getNoExcuteMcsCmdVhCount(E_VH_TYPE.Dirty);
                int total_cmd_is_queue_count = SCApplication.getInstance().CMDBLL.getCMD_MCSIsQueueCount();
                int total_cmd_is_running_count = SCApplication.getInstance().CMDBLL.getCMD_MCSIsRunningCount();

                string ohxc_excute_info = string.Empty;

                StringBuilder sb = new StringBuilder();
                setOhxCContent(sb, nameof(total_idle_vh_clean), total_idle_vh_clean, "current idle clean car");
                setOhxCContent(sb, nameof(total_idle_vh_Dirty), total_idle_vh_Dirty, "current idle dirty car");
                setOhxCContent(sb, nameof(total_cmd_is_queue_count), total_cmd_is_queue_count, "cmd number being queued");
                setOhxCContent(sb, nameof(total_cmd_is_running_count), total_cmd_is_running_count, "cmd number being executed");

                var response = (Response)sb.ToString();
                response.ContentType = restfulContentType;
                return response;
            };

            Post["AVEHICLES/ViewerUpdate"] = (p) =>
            {
                SCApplication scApp = SCApplication.getInstance();
                List<AVEHICLE> vhs = scApp.getEQObjCacheManager().getAllVehicle();

                //foreach (AVEHICLE vh in vhs)
                //{
                //    scApp.VehicleService.PublishVhInfo(vh, null);
                //    SpinWait.SpinUntil(() => false, 10);
                //}

                var response = (Response)"OK";
                response.ContentType = restfulContentType;
                return response;
            };

            //Post["api/io/T2STK100T01/waitin/CST01"] = (p) =>
            //{

            //    var response = (Response)"OK";
            //    response.ContentType = restfulContentType;
            //    return response;
            //};


            Post["AVEHICLES/SendCommand"] = (p) =>
            {
                var scApp = SCApplication.getInstance();
                bool isSuccess = true;
                string vh_id = Request.Query.vh_id.Value ?? Request.Form.vh_id.Value ?? string.Empty;
                string carrier_id = Request.Query.carrier_id.Value ?? Request.Form.carrier_id.Value ?? string.Empty;
                string from_port_id = Request.Query.from_port_id.Value ?? Request.Form.from_port_id.Value ?? string.Empty;
                string to_port_id = Request.Query.to_port_id.Value ?? Request.Form.to_port_id.Value ?? string.Empty;
                E_CMD_TYPE e_cmd_type = default(E_CMD_TYPE);
                string cmd_type = Request.Query.cmd_type.Value ?? Request.Form.cmd_type.Value ?? string.Empty;

                string result = string.Empty;
                try
                {
                    ACMD_OHTC cmd_obj = null;
                    AVEHICLE assignVH = null;

                    assignVH = scApp.VehicleBLL.getVehicleByID(vh_id);
                    isSuccess = assignVH != null;
                    if (isSuccess)
                    {
                        isSuccess = Enum.TryParse(cmd_type, out e_cmd_type);
                        if (isSuccess)
                        {
                            switch (e_cmd_type)
                            {
                                case E_CMD_TYPE.Move:
                                case E_CMD_TYPE.Load:
                                case E_CMD_TYPE.Unload:
                                case E_CMD_TYPE.LoadUnload:
                                case E_CMD_TYPE.MoveToMTL:
                                case E_CMD_TYPE.SystemOut:
                                    string from_adr = from_port_id;
                                    string to_adr = to_port_id;
                                    //scApp.MapBLL.getAddressID(from_port_id, out from_adr);
                                    //scApp.MapBLL.getAddressID(to_port_id, out to_adr);
                                    scApp.CMDBLL.doCreatTransferCommand(vh_id, out cmd_obj,
                                                                    cmd_type: e_cmd_type,
                                                                    source: from_adr,
                                                                    destination: to_adr,
                                                                    carrier_id: carrier_id,
                                                                    gen_cmd_type: SCAppConstants.GenOHxCCommandType.Manual);
                                    sc.BLL.CMDBLL.OHTCCommandCheckResult check_result_info =
                                                        sc.BLL.CMDBLL.getCallContext<sc.BLL.CMDBLL.OHTCCommandCheckResult>
                                                       (sc.BLL.CMDBLL.CALL_CONTEXT_KEY_WORD_OHTC_CMD_CHECK_RESULT);
                                    isSuccess = check_result_info.IsSuccess;
                                    result = check_result_info.ToString();
                                    if (isSuccess)
                                    {
                                        isSuccess = scApp.VehicleService.doSendOHxCCmdToVh(assignVH, cmd_obj);
                                        if (isSuccess)
                                        {
                                            result = "OK";
                                        }
                                        else
                                        {
                                            result = "Send command to vehicle failed!";
                                        }
                                    }
                                    else
                                    {
                                        result = "Command create failed!";
                                        //bcf.App.BCFApplication.onWarningMsg(this, new bcf.Common.LogEventArgs("Command create fail.", check_result_info.Num));
                                    }
                                    break;
                                case E_CMD_TYPE.Home:
                                    string cmd_id = scApp.SequenceBLL.getCommandID(SCAppConstants.GenOHxCCommandType.Manual);
                                    isSuccess = scApp.VehicleService.TransferRequset(vh_id, cmd_id, ActiveType.Home,
                                                                        "", new string[0], new string[0], "", "");
                                    break;
                                case E_CMD_TYPE.Teaching:
                                    isSuccess = scApp.VehicleService.TeachingRequest(vh_id, from_port_id, to_port_id);
                                    break;
                            }
                        }
                        else
                        {
                            result = $"Try parse Command Type:[{cmd_type}] failed!";
                        }
                    }
                    else
                    {
                        result = $"Vehicle :[{vh_id}] not found!";
                    }

                }
                catch (Exception ex)
                {
                    isSuccess = false;
                    result = "Execption happend!";
                    logger.Error(ex, "Execption:");
                }

                var response = (Response)result;
                response.ContentType = restfulContentType;
                return response;
            };

            Post["AVEHICLES/SendReset"] = (p) =>
            {
                var scApp = SCApplication.getInstance();
                bool isSuccess = true;
                string vh_id = Request.Query.vh_id.Value ?? Request.Form.vh_id.Value ?? string.Empty;
                string result = string.Empty;
                try
                {
                    LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleInfo), Device: "OHTC",
                       Data: $"Process vh:{vh_id} position reset request from viewer...",
                       VehicleID: vh_id);
                    AVEHICLE assignVH = null;
                    assignVH = scApp.VehicleBLL.getVehicleByID(vh_id);
                    isSuccess = assignVH != null;
                    //確認要求的VH，是否存在
                    if (isSuccess)
                    {
                        if (assignVH == null)
                        {
                            isSuccess = false;
                            result = $"vh:{vh_id} is not exist.";
                        }
                    }
                    //確認是否還是連線著，如果是，則不能夠進行位置的重置
                    if (isSuccess)
                    {
                        if (assignVH.isTcpIpConnect)
                        {
                            isSuccess = false;
                            result = $"vh:{vh_id} current is connect. can't excute reset.";
                        }
                    }
                    //確認該Vh是否還有Block，如果有也不能重置
                    if (isSuccess)
                    {
                        var non_release_block = scApp.MapBLL.loadNonReleaseBlockQueueByCarID(vh_id);
                        if (non_release_block != null && non_release_block.Count > 0)
                        {
                            isSuccess = false;
                            result = $"vh:{vh_id} current has blocking zone, can't excute. please check block management.";
                        }
                    }
                    if (isSuccess)
                    {
                        //isSuccess = scApp.VehicleService.VehicleStatusRequest(vh_id, true);
                        isSuccess = scApp.VehicleService.VhPositionReset(vh_id);
                        if (isSuccess)
                        {
                            result = "OK";
                        }
                        else
                        {
                            result = "excute reset failed.";
                        }
                    }
                    LogHelper.Log(logger: logger, LogLevel: LogLevel.Debug, Class: nameof(VehicleInfo), Device: "OHTC",
                       Data: $"End process vh:{vh_id} position reset request from viewer. is success:{isSuccess} result:{result}",
                       VehicleID: vh_id);
                }
                catch (Exception ex)
                {
                    isSuccess = false;
                    result = "Execption happend!";
                    logger.Error(ex, "Execption:");
                }

                var response = (Response)result;
                response.ContentType = restfulContentType;
                return response;
            };

            Post["AVEHICLES/SendCancelAbort"] = (p) =>
            {
                var scApp = SCApplication.getInstance();
                bool isSuccess = true;
                string vh_id = Request.Query.vh_id.Value ?? Request.Form.vh_id.Value ?? string.Empty;

                string result = string.Empty;
                try
                {
                    AVEHICLE assignVH = null;

                    assignVH = scApp.VehicleBLL.getVehicleByID(vh_id);

                    isSuccess = assignVH != null;

                    if (isSuccess)
                    {
                        string mcs_cmd_id = assignVH.MCS_CMD;
                        if (!string.IsNullOrWhiteSpace(mcs_cmd_id))
                        {
                            ACMD_MCS mcs_cmd = scApp.CMDBLL.getCMD_MCSByID(mcs_cmd_id);
                            if (mcs_cmd == null)
                            {
                                result = $"Can't find MCS command:[{mcs_cmd_id}] in database.";
                            }
                            else
                            {
                                CMDCancelType actType = default(CMDCancelType);
                                if (mcs_cmd.TRANSFERSTATE < sc.E_TRAN_STATUS.Transferring)
                                {
                                    actType = CMDCancelType.CmdCancel;
                                    isSuccess = scApp.VehicleService.doCancelOrAbortCommandByMCSCmdID(mcs_cmd_id, actType);
                                    if (isSuccess) result = "OK";
                                    else result = "Send command cancel/abort failed.";
                                }
                                else if (mcs_cmd.TRANSFERSTATE < sc.E_TRAN_STATUS.Canceling)
                                {
                                    actType = CMDCancelType.CmdAbort;
                                    isSuccess = scApp.VehicleService.doCancelOrAbortCommandByMCSCmdID(mcs_cmd_id, actType);
                                    if (isSuccess) result = "OK";
                                    else result = "Send command cancel/abort failed.";
                                }
                                else
                                {
                                    result = $"MCS command:[{mcs_cmd_id}] can't excute cancel / abort,\r\ncurrent state:{mcs_cmd.TRANSFERSTATE}";
                                }
                            }
                        }
                        else
                        {
                            string ohtc_cmd_id = assignVH.OHTC_CMD;
                            if (string.IsNullOrWhiteSpace(ohtc_cmd_id))
                            {
                                result = $"Vehicle:[{vh_id}] do not have command.";
                            }
                            else
                            {
                                ACMD_OHTC ohtc_cmd = scApp.CMDBLL.getCMD_OHTCByID(ohtc_cmd_id);
                                if (ohtc_cmd == null)
                                {
                                    result = $"Can't find vehicle command:[{ohtc_cmd_id}] in database.";
                                }
                                else
                                {
                                    CMDCancelType actType = ohtc_cmd.CMD_STAUS >= E_CMD_STATUS.Execution ? CMDCancelType.CmdAbort : CMDCancelType.CmdCancel;
                                    isSuccess = scApp.VehicleService.doAbortCommand(assignVH, ohtc_cmd_id, actType);
                                    if (isSuccess)
                                    {
                                        result = "OK";
                                    }
                                    else
                                    {
                                        result = "Send vehicle status request failed.";
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        result = $"Vehicle :[{vh_id}] not found!";
                    }
                }
                catch (Exception ex)
                {
                    isSuccess = false;
                    result = "Execption happend!";
                    logger.Error(ex, "Execption:");
                }

                var response = (Response)result;
                response.ContentType = restfulContentType;
                return response;
            };

            Post["AVEHICLES/PauseEvent"] = (p) =>
            {
                bool isSuccess = false;
                SCApplication scApp = SCApplication.getInstance();
                string vh_id = Request.Query.vh_id.Value ?? Request.Form.vh_id.Value ?? string.Empty;
                string event_type = Request.Query.event_type.Value ?? Request.Form.event_type.Value ?? string.Empty;
                PauseEvent pauseEvent = default(PauseEvent);
                isSuccess = Enum.TryParse(event_type, out pauseEvent);
                if (isSuccess)
                {
                    isSuccess = scApp.VehicleService.PauseRequest
                   (vh_id, pauseEvent, SCAppConstants.OHxCPauseType.Normal);
                }

                var response = (Response)(isSuccess ? "OK" : "NG");
                response.ContentType = restfulContentType;
                return response;
            };


            Post["AVEHICLES/PauseStatusChange"] = (p) =>
            {
                bool isSuccess = false;
                string result = string.Empty;
                SCApplication scApp = SCApplication.getInstance();
                string vh_id = Request.Query.vh_id.Value ?? Request.Form.vh_id.Value ?? string.Empty;
                string pauseType = Request.Query.pauseType.Value ?? Request.Form.pauseType.Value ?? string.Empty;
                string event_type = Request.Query.event_type.Value ?? Request.Form.event_type.Value ?? string.Empty;
                SCAppConstants.OHxCPauseType pause_type = default(SCAppConstants.OHxCPauseType);
                PauseEvent pauseEvent = default(PauseEvent);
                isSuccess = Enum.TryParse(pauseType, out pause_type);

                if (isSuccess)
                {
                    isSuccess = Enum.TryParse(event_type, out pauseEvent);

                    if (isSuccess)
                    {
                        isSuccess = scApp.VehicleService.PauseRequest
                       (vh_id, pauseEvent, pause_type);
                        if (isSuccess)
                        {
                            //AVEHICLE vh = scApp.VehicleBLL.getVehicleByID(vh_id);
                            //vh.NotifyVhStatusChange();
                            result = "OK";
                        }
                        else
                        {
                            result = $"Send pause request to vehicle:{vh_id} failed.";
                        }
                    }
                    else
                    {
                        result = $"Can't recognize Pause Event:{event_type}.";

                    }

                }
                else
                {
                    result = $"Can't recognize Pause Type:{pauseType}.";
                }

                var response = (Response)result;
                response.ContentType = restfulContentType;
                return response;
            };

            Post["AVEHICLES/ModeStatusChange"] = (p) =>
            {
                bool isSuccess = false;
                string result = string.Empty;
                SCApplication scApp = SCApplication.getInstance();
                string vh_id = Request.Query.vh_id.Value ?? Request.Form.vh_id.Value ?? string.Empty;
                string modeStatus = Request.Query.modeStatus.Value ?? Request.Form.modeStatus.Value ?? string.Empty;
                VHModeStatus mode_status = default(VHModeStatus);
                isSuccess = Enum.TryParse(modeStatus, out mode_status);

                if (isSuccess)
                {

                    if (isSuccess)
                    {
                        isSuccess = scApp.VehicleBLL.updataVehicleMode(vh_id, mode_status);
                        if (isSuccess)
                        {
                            AVEHICLE vh = scApp.VehicleBLL.getVehicleByID(vh_id);
                            vh.NotifyVhStatusChange();
                            result = "OK";
                        }
                        else
                        {
                            result = $"Update vehicle:{vh_id} mode status failed.";
                        }
                    }

                }
                else
                {
                    result = $"Can't recognize mode status:{modeStatus}.";
                }

                var response = (Response)result;
                response.ContentType = restfulContentType;
                return response;
            };

            Post["AVEHICLES/ResetAlarm"] = (p) =>
            {
                var scApp = SCApplication.getInstance();
                string result = string.Empty;
                bool isSuccess = true;
                string vh_id = Request.Query.vh_id.Value ?? Request.Form.vh_id.Value ?? string.Empty;
                try
                {

                    isSuccess = scApp.VehicleService.AlarmResetRequest(vh_id);
                    if (isSuccess)
                    {
                        result = "OK";
                    }
                    else
                    {
                        result = "Reset alarm failed.";
                    }
                }
                catch (Exception ex)
                {
                    result = "Reset alarm failedwith exception happened.";
                }

                var response = (Response)result;
                response.ContentType = restfulContentType;
                return response;
            };

            Post["Engineer/ForceCmdFinish"] = (p) =>
            {
                var scApp = SCApplication.getInstance();
                string vh_id = Request.Query.vh_id.Value ?? Request.Form.vh_id.Value ?? string.Empty;
                bool isSuccess = scApp.CMDBLL.forceUpdataCmdStatus2FnishByVhID(vh_id);
                if (isSuccess)
                {
                    var vh = scApp.VehicleBLL.getVehicleByID(vh_id);
                    vh.NotifyVhExcuteCMDStatusChange();
                }
                var response = (Response)(isSuccess ? "OK" : "NG");
                response.ContentType = restfulContentType;
                return response;
            };
        }

        private static StringBuilder setOhxCContent(StringBuilder sb, string key, int value, string description)
        {
            sb.AppendLine($"#{PROMETHEUS_TOKEN_HELP} ohxc_{key} {description}");
            sb.AppendLine($"#{PROMETHEUS_TOKEN_TYPE} ohxc_{key} gauge");
            sb.AppendLine($"ohxc_{key} {value}");
            return sb;
        }
        const string PROMETHEUS_TOKEN_HELP = "HELP";
        const string PROMETHEUS_TOKEN_TYPE = "TYPE";
    }
}
