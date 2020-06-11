using com.mirle.ibg3k0.sc.App;
using com.mirle.ibg3k0.sc.Common;
using com.mirle.ibg3k0.sc.ProtocolFormat.OHTMessage;
using Nancy;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using System.Threading;
using com.mirle.ibg3k0.sc.Data.VO;
using com.mirle.ibg3k0.sc.Data.ValueDefMapAction;

namespace com.mirle.ibg3k0.sc.WebAPI
{
    public class MTSMTLInfo : NancyModule
    {
        //SCApplication app = null;
        const string restfulContentType = "application/json; charset=utf-8";
        const string urlencodedContentType = "application/x-www-form-urlencoded; charset=utf-8";
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        Timer ThreadTimer = null;
        public MTSMTLInfo()
        {
            //app = SCApplication.getInstance();
            RegisterMTSMTLEvent();
            After += ctx => ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*");

        }

        const string DEFAULT_RESULT = "OK";
        private void RegisterMTSMTLEvent()
        {
            Post["MTSMTLInfo/InterlockRequest"] = (p) =>
            {
                var scApp = SCApplication.getInstance();
                string result = DEFAULT_RESULT;
                bool isSuccess = true;
                string station_id = Request.Query.station_id.Value ?? Request.Form.station_id.Value ?? string.Empty;
                string isSet = Request.Query.priority.Value ?? Request.Form.isSet.Value ?? string.Empty;
                try
                {
                    AEQPT MTLMTS = scApp.getEQObjCacheManager().getEquipmentByEQPTID(station_id);
                    bool setValue = Convert.ToBoolean(isSet);
                    if (MTLMTS != null)
                    {
                        if (MTLMTS.EQPT_ID.StartsWith("MTL"))
                        {
                            MTLMTS = MTLMTS as MaintainLift;
                            MTxValueDefMapActionBase MTLValueDefMapActionBase = MTLMTS.getMapActionByIdentityKey(nameof(MTLValueDefMapActionNew)) as MTxValueDefMapActionBase;
                            isSuccess = MTLValueDefMapActionBase.setOHxC2MTL_CarOutInterlock(setValue);
                        }
                        else if (MTLMTS.EQPT_ID.StartsWith("MTS"))
                        {
                            MTLMTS = MTLMTS as MaintainSpace;
                            MTxValueDefMapActionBase MTSValueDefMapActionBase = MTLMTS.getMapActionByIdentityKey(nameof(MTSValueDefMapActionNew)) as MTxValueDefMapActionBase;
                            isSuccess = MTSValueDefMapActionBase.setOHxC2MTL_CarOutInterlock(setValue);
                        }
                        else
                        {
                            isSuccess = false;
                        }

                        if (isSuccess)
                        {
                            result = "OK";
                        }
                        else
                        {
                            result = "Set interlock failed.";
                        }
                    }
                    else
                    {
                        result = $"Can not find station[{station_id}].";
                    }
                }
                catch (Exception ex)
                {
                    result = "Set interlock failed with exception happened.";
                }

                var response = (Response)result;
                response.ContentType = restfulContentType;
                return response;
            };
            Post["MTSMTLInfo/CarInInterlockRequest"] = (p) =>
            {
                var scApp = SCApplication.getInstance();
                string result = DEFAULT_RESULT;
                bool isSuccess = true;
                string station_id = Request.Query.station_id.Value ?? Request.Form.station_id.Value ?? string.Empty;
                string isSet = Request.Query.priority.Value ?? Request.Form.isSet.Value ?? string.Empty;
                try
                {
                    AEQPT MTLMTS = scApp.getEQObjCacheManager().getEquipmentByEQPTID(station_id);
                    bool setValue = Convert.ToBoolean(isSet);
                    if (MTLMTS != null)
                    {
                        if (MTLMTS.EQPT_ID.StartsWith("MTL"))
                        {
                            MTLMTS = MTLMTS as MaintainLift;
                            MTxValueDefMapActionBase MTLValueDefMapActionBase = MTLMTS.getMapActionByIdentityKey(nameof(MTLValueDefMapActionNew)) as MTxValueDefMapActionBase;
                            isSuccess = MTLValueDefMapActionBase.setOHxC2MTL_CarInMoving(setValue);
                        }
                        else if (MTLMTS.EQPT_ID.StartsWith("MTS"))
                        {
                            MTLMTS = MTLMTS as MaintainSpace;
                            MTxValueDefMapActionBase MTSValueDefMapActionBase = MTLMTS.getMapActionByIdentityKey(nameof(MTSValueDefMapActionNew)) as MTxValueDefMapActionBase;
                            isSuccess = MTSValueDefMapActionBase.setOHxC2MTL_CarInMoving(setValue);
                        }
                        else
                        {
                            isSuccess = false;
                        }

                        if (isSuccess)
                        {
                            result = "OK";
                        }
                        else
                        {
                            result = "Set interlock failed.";
                        }
                    }
                    else
                    {
                        result = $"Can not find station[{station_id}].";
                    }
                }
                catch (Exception ex)
                {
                    result = "Set interlock failed with exception happened.";
                }

                var response = (Response)result;
                response.ContentType = restfulContentType;
                return response;
            };

            Post["MTSMTLInfo/CarOutRequest"] = (p) =>
            {
                var scApp = SCApplication.getInstance();
                var r = default((bool isSuccess, string result));
                string result = DEFAULT_RESULT;
                bool isSuccess = true;
                string vh_id = Request.Query.vh_id.Value ?? Request.Form.vh_id.Value ?? string.Empty;
                string station_id = Request.Query.station_id.Value ?? Request.Form.station_id.Value ?? string.Empty;
                string isSet = Request.Query.priority.Value ?? Request.Form.isSet.Value ?? string.Empty;
                try
                {
                    AVEHICLE pre_car_out_vh = scApp.VehicleBLL.cache.getVhByID(vh_id);
                    Data.VO.Interface.IMaintainDevice maintainDevice = scApp.EquipmentBLL.cache.getMaintainDevice(station_id);
                    if (maintainDevice is sc.Data.VO.MaintainLift)
                    {
                        sc.Data.VO.Interface.IMaintainDevice dockingMTS = scApp.EquipmentBLL.cache.GetDockingMTLOfMaintainSpace();
                        r = scApp.MTLService.checkVhAndMTxCarOutStatus(maintainDevice, dockingMTS, pre_car_out_vh);
                        if (r.isSuccess)
                        {
                            r = scApp.MTLService.CarOurRequest(maintainDevice, pre_car_out_vh);
                        }
                        if (r.isSuccess)
                        {
                            r = scApp.MTLService.processCarOutScenario(maintainDevice as sc.Data.VO.MaintainLift, pre_car_out_vh);
                        }
                    }
                    else if (maintainDevice is sc.Data.VO.MaintainSpace)
                    {
                        r = scApp.MTLService.checkVhAndMTxCarOutStatus(maintainDevice, null, pre_car_out_vh);
                        if (r.isSuccess)
                        {
                            r = scApp.MTLService.CarOurRequest(maintainDevice, pre_car_out_vh);
                        }
                        if (r.isSuccess)
                        {
                            r = scApp.MTLService.processCarOutScenario(maintainDevice as sc.Data.VO.MaintainSpace, pre_car_out_vh);
                        }
                    }
                    if (!r.isSuccess)
                    {
                        result = r.result;
                    }
                    isSuccess = r.isSuccess;
                }
                catch (Exception ex)
                {
                    result = "excute car out request failed with exception happened.";
                    isSuccess = false;
                }

                var response = (Response)result;
                response.ContentType = restfulContentType;
                return response;
            };
            Post["MTSMTLInfo/CarOutCancel"] = (p) =>
            {
                var scApp = SCApplication.getInstance();
                string result = DEFAULT_RESULT;
                bool isSuccess = true;
                string station_id = Request.Query.station_id.Value ?? Request.Form.station_id.Value ?? string.Empty;
                string isSet = Request.Query.priority.Value ?? Request.Form.isSet.Value ?? string.Empty;
                try
                {
                    Data.VO.Interface.IMaintainDevice maintainDevice = scApp.EquipmentBLL.cache.getMaintainDevice(station_id);
                    scApp.MTLService.carOutRequestCancle(maintainDevice);

                    isSuccess = true;
                }
                catch (Exception ex)
                {
                    result = "excute car out cancel failed with exception happened.";
                    isSuccess = false;
                }

                var response = (Response)result;
                response.ContentType = restfulContentType;
                return response;
            };

        }
    }
}
