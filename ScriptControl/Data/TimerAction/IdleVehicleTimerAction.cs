//*********************************************************************************
//      IdleVehicleTimerAction.cs
//*********************************************************************************
// File Name: IdleVehicleTimerAction.cs
// Description: 
//
//(c) Copyright 2014, MIRLE Automation Corporation
//
// Date          Author         Request No.    Tag     Description
// ------------- -------------  -------------  ------  -----------------------------
//**********************************************************************************
using com.mirle.ibg3k0.bcf.App;
using com.mirle.ibg3k0.bcf.Controller;
using com.mirle.ibg3k0.bcf.Data.TimerAction;
using com.mirle.ibg3k0.sc.App;
using com.mirle.ibg3k0.sc.Common;
using com.mirle.ibg3k0.sc.Data.VO;
using com.mirle.ibg3k0.sc.ProtocolFormat.OHTMessage;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.mirle.ibg3k0.sc.Data.TimerAction
{
    /// <summary>
    /// Class IdleVehicleTimerAction.
    /// </summary>
    /// <seealso cref="com.mirle.ibg3k0.bcf.Data.TimerAction.ITimerAction" />
    class IdleVehicleTimerAction : ITimerAction
    {
        /// <summary>
        /// The logger
        /// </summary>
        private static Logger logger = LogManager.GetCurrentClassLogger();
        /// <summary>
        /// The sc application
        /// </summary>
        protected SCApplication scApp = null;


        /// <summary>
        /// Initializes a new instance of the <see cref="IdleVehicleTimerAction"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="intervalMilliSec">The interval milli sec.</param>
        public IdleVehicleTimerAction(string name, long intervalMilliSec)
            : base(name, intervalMilliSec)
        {

        }

        /// <summary>
        /// Initializes the start.
        /// </summary>
        public override void initStart()
        {
            //do nothing
            scApp = SCApplication.getInstance();

        }

        private long syncPoint = 0;

        /// <summary>
        /// Timer Action的執行動作
        /// </summary>
        /// <param name="obj">The object.</param>
        public override void doProcess(object obj)
        {
            if (System.Threading.Interlocked.Exchange(ref syncPoint, 1) == 0)
            {
                //lock (scApp.pack_lock_obj)
                //{
                try
                {
                    //0.先判斷各區域的停車位是否皆已移到最高順位
                    scApp.ParkBLL.tryAdjustTheVhParkingPositionByParkZoneAndPrio();
                    //1.各PackZone水位檢查
                    //  a.找出是否有低於目前水位的PackZone
                    //  b.找出最近的PackZone至少高於水位下限一台，若有則派至此處。
                    List<APARKZONEMASTER> vhNotEnoughParkZones = null;
                    APARKZONEDETAIL nearbyZoneDetail = null;
                    if (!scApp.ParkBLL.checkParkZoneLowerBorder(out vhNotEnoughParkZones))
                    {
                        foreach (APARKZONEMASTER vhNotEnoughParkZone in vhNotEnoughParkZones)
                        {
                            if (scApp.ParkBLL.tryFindNearbyParkZoneHasVhToSupport(vhNotEnoughParkZone, out nearbyZoneDetail))
                            {
                                APARKZONEDETAIL vhNotEnoughParkDeatil = null;
                                switch (vhNotEnoughParkZone.PARK_TYPE)
                                {
                                    case E_PARK_TYPE.OrderByAsc:
                                        vhNotEnoughParkDeatil = scApp.ParkBLL.
                                        getParkDetailByParkZoneIDPrioAscAndCanParkingAdr(vhNotEnoughParkZone.PARK_ZONE_ID);
                                        break;
                                    case E_PARK_TYPE.OrderByDes:
                                        vhNotEnoughParkDeatil = scApp.ParkBLL.
                                        getParkDetailByParkZoneIDPrioDes(vhNotEnoughParkZone.PARK_ZONE_ID);
                                        break;
                                }
                                //bool isSccess = false;
                                //isSccess = scApp.CMDBLL.creatCommand_OHTC(nearbyZoneDetail.CAR_ID, string.Empty, string.Empty, E_CMD_TYPE.Move_Pack
                                //     , nearbyZoneDetail.ADR_ID, vhNotEnoughPackDeatil.ADR_ID, 0, 0);
                                //if (isSccess)
                                //{
                                //    isSccess = scApp.CMDBLL.generateCmd_OHTC_Details();
                                //    if (isSccess)
                                //    {
                                //        if (nearbyZoneDetail != null)
                                //        {
                                //            APACKZONEMASTER nearbyZoneMaster = scApp.PackBLL.
                                //                getPackZoneMasterByPackZoneID(nearbyZoneDetail.PACK_ZONE_ID);
                                //            if (nearbyZoneMaster.PACK_TYPE == E_PACK_TYPE.OrderByAsc)
                                //            {
                                //                scApp.PackBLL.tryAdjustTheVhPackingPositionByPackZoneAndPrio(nearbyZoneMaster);
                                //            }
                                //        }
                                //        if (vhNotEnoughPackZone != null &&
                                //            vhNotEnoughPackZone.PACK_TYPE == E_PACK_TYPE.OrderByDes)
                                //        {
                                //            scApp.PackBLL.tryAdjustTheVhPackingPositionByPackZoneAndPrio(vhNotEnoughPackZone);
                                //        }
                                //    }
                                //}

                                if (vhNotEnoughParkDeatil == null || nearbyZoneDetail == null)
                                {
                                    continue;
                                }
                                //if (scApp.CMDBLL.hasExcuteCMDWantToAdr(vhNotEnoughPackDeatil.ADR_ID))
                                //{
                                //    continue;
                                //}
                                bool isSccess = false;
                                isSccess = scApp.CMDBLL.doCreatTransferCommand(nearbyZoneDetail.CAR_ID, string.Empty, string.Empty, E_CMD_TYPE.Move_Park
                                      , nearbyZoneDetail.ADR_ID, vhNotEnoughParkDeatil.ADR_ID, 0, 0);
                                if (isSccess)
                                {
                                    if (nearbyZoneDetail != null)
                                    {
                                        APARKZONEMASTER nearbyZoneMaster = scApp.ParkBLL.
                                            getParkZoneMasterByParkZoneID(nearbyZoneDetail.PARK_ZONE_ID);
                                        if (nearbyZoneMaster.PARK_TYPE == E_PARK_TYPE.OrderByAsc)
                                        {
                                            scApp.ParkBLL.tryAdjustTheVhParkingPositionByParkZoneAndPrio(nearbyZoneMaster);
                                        }
                                    }
                                    if (vhNotEnoughParkZone != null &&
                                        vhNotEnoughParkZone.PARK_TYPE == E_PARK_TYPE.OrderByDes)
                                    {
                                        scApp.ParkBLL.tryAdjustTheVhParkingPositionByParkZoneAndPrio(vhNotEnoughParkZone);
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        APARKZONEDETAIL emptyParkZoneDetail = null;
                        AVEHICLE cyclingVh = null;
                        if (scApp.ParkBLL.tryFindCycleRunVhToParking(out cyclingVh, out emptyParkZoneDetail))
                        {
                            string cyclingvh_id = cyclingVh.VEHICLE_ID;
                            string cyclingvh_crtAdr = cyclingVh.CUR_ADR_ID;
                            //bool isSccess = false;
                            //isSccess = scApp.CMDBLL.creatCommand_OHTC(cyclingVh.VEHICLE_ID, string.Empty, string.Empty, E_CMD_TYPE.Move_Pack
                            //     , cyclingVh.CUR_ADR_ID, emptyPackZoneDetail.ADR_ID, 0, 0);
                            //isSccess = scApp.CMDBLL.generateCmd_OHTC_Details();

                            scApp.CMDBLL.doCreatTransferCommand(cyclingvh_id,
                                                                string.Empty,
                                                                string.Empty,
                                                                E_CMD_TYPE.Move_Park,
                                                                cyclingvh_crtAdr,
                                                                emptyParkZoneDetail.ADR_ID,
                                                                0, 0);
                            APARKZONEMASTER emptyZoneMaster = scApp.ParkBLL.
                                  getParkZoneMasterByParkZoneID(emptyParkZoneDetail.PARK_ZONE_ID);
                            if (emptyZoneMaster != null &&
                                  emptyZoneMaster.PARK_TYPE == E_PARK_TYPE.OrderByDes)
                            {
                                scApp.ParkBLL.tryAdjustTheVhParkingPositionByParkZoneAndPrio(emptyZoneMaster);
                            }
                        }
                    }
                    //2.車子目前停車位是否在該PackZone的最高順位
                    //  a.找出所有在Packing的VH
                    //  b.foreach 每台車 目前所在的Adr 他隸屬的 PackZone-PackType
                    //     i.OrderByAsc,找出是否有比該台車目前所停放的Adr更前面的 PRIO 且有空位。
                    //    ii.OrderByDes,找出目前所停放的Adr是否已經被預約要停放
                    //if (nearbyZoneDetail != null)
                    //{
                    //    scApp.PackBLL.tryAdjustTheVhPackingPositionByPackZoneAndPrio(nearbyZoneDetail);
                    //}
                    //if (vhNotEnoughPackZone != null &&
                    //    vhNotEnoughPackZone.PACK_TYPE == E_PACK_TYPE.OrderByDes)
                    //{
                    //    scApp.PackBLL.tryAdjustTheVhPackingPositionByPackZoneAndPrio(vhNotEnoughPackZone);
                    //}
                    //APACKZONEDETAIL crtPackZoneDetail = null;
                    //APACKZONEDETAIL nextPackZoneDetail = null;
                    //if (scApp.PackBLL.tryAdjustTheVhPackingPositionByPrio
                    //    (out crtPackZoneDetail, out nextPackZoneDetail))
                    //{
                    //    scApp.CMDBLL.creatCommand_OHTC(crtPackZoneDetail.CAR_ID, string.Empty, E_CMD_TYPE.Move_Pack
                    //           , crtPackZoneDetail.ADR_ID, nextPackZoneDetail.ADR_ID, 0, 0);
                    //    //Task.Run(() => scApp.CMDBLL.generateCmd_OHTC_Detals());
                    //    scApp.CMDBLL.generateCmd_OHTC_Detals();
                    //}
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Exception");
                }
                finally
                {
                    System.Threading.Interlocked.Exchange(ref syncPoint, 0);
                }
                //}
            }


        }
    }
}