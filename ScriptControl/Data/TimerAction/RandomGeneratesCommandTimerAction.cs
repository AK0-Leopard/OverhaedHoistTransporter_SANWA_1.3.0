// ***********************************************************************
// Assembly         : ScriptControl
// Author           : 
// Created          : 03-31-2016
//
// Last Modified By : 
// Last Modified On : 03-24-2016
// ***********************************************************************
// <copyright file="BCSystemStatusTimer.cs" company="">
//     Copyright ©  2014
// </copyright>
// <summary></summary>
// ***********************************************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using com.mirle.ibg3k0.bcf.Data.TimerAction;
using com.mirle.ibg3k0.sc.App;
using com.mirle.ibg3k0.sc.Common;
using com.mirle.ibg3k0.sc.Data.DAO;
using com.mirle.ibg3k0.sc.Data.SECS;
using NLog;

namespace com.mirle.ibg3k0.sc.Data.TimerAction
{
    /// <summary>
    /// Class BCSystemStatusTimer.
    /// </summary>
    /// <seealso cref="com.mirle.ibg3k0.bcf.Data.TimerAction.ITimerAction" />
    public class RandomGeneratesCommandTimerAction : ITimerAction
    {
        /// <summary>
        /// The logger
        /// </summary>
        private static Logger logger = LogManager.GetCurrentClassLogger();
        /// <summary>
        /// The sc application
        /// </summary>
        protected SCApplication scApp = null;
        private List<TranTask> tranTasks = null;

        public Dictionary<string, List<TranTask>> dicTranTaskSchedule_Clear_Dirty = null;
        public List<String> SourcePorts_Clear = null;
        public List<String> SourcePorts_Dirty = null;


        Random rnd_Index = new Random(Guid.NewGuid().GetHashCode());

        /// <summary>
        /// Initializes a new instance of the <see cref="BCSystemStatusTimer"/> class.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="intervalMilliSec">The interval milli sec.</param>
        public RandomGeneratesCommandTimerAction(string name, long intervalMilliSec)
            : base(name, intervalMilliSec)
        {

        }
        /// <summary>
        /// Initializes the start.
        /// </summary>
        public override void initStart()
        {
            scApp = SCApplication.getInstance();
            tranTasks = scApp.CMDBLL.loadTranTasks();

            if (scApp.BC_ID == SCAppConstants.WorkVersion.VERSION_NAME_OHS100)
            {
                dicTranTaskSchedule_Clear_Dirty = scApp.CMDBLL.loadTranTaskSchedule_Clear_Dirty();
                SourcePorts_Clear = dicTranTaskSchedule_Clear_Dirty["C"].Select(task => task.SourcePort).Distinct().ToList();
                SourcePorts_Dirty = dicTranTaskSchedule_Clear_Dirty["D"].Select(task => task.SourcePort).Distinct().ToList();
            }

        }
        /// <summary>
        /// Timer Action的執行動作
        /// </summary>
        /// <param name="obj">The object.</param>
        private long syncPoint = 0;
        public override void doProcess(object obj)
        {
            if (!DebugParameter.CanAutoRandomGeneratesCommand) return;
            if (System.Threading.Interlocked.Exchange(ref syncPoint, 1) == 0)
            {
                try
                {
                    if (scApp.BC_ID == SCAppConstants.WorkVersion.VERSION_NAME_TAICHUNG6F ||
                        scApp.BC_ID == SCAppConstants.WorkVersion.VERSION_NAME_TAICHUNG ||
                        scApp.BC_ID == SCAppConstants.WorkVersion.VERSION_NAME_CSOT_T4)
                    {
                        Taichung();
                    }
                    else if (scApp.BC_ID == SCAppConstants.WorkVersion.VERSION_NAME_OHS100)
                    {
                        OHS100();
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Exception");
                }
                finally
                {
                    System.Threading.Interlocked.Exchange(ref syncPoint, 0);
                }
            }
            //scApp.BCSystemBLL.reWriteBCSystemRunTime();
        }

        private void OHS100()
        {
            if (scApp.VehicleBLL.getNoExcuteMcsCmdVhCount(E_VH_TYPE.Clean) > 0)
                RandomGenerates_TranTask_Clear_Drity("C");
            Thread.Sleep(1000);
            if (scApp.VehicleBLL.getNoExcuteMcsCmdVhCount(E_VH_TYPE.Dirty) > 0)
                RandomGenerates_TranTask_Clear_Drity("D");
        }

        private void RandomGenerates_TranTask_Clear_Drity(string car_type)
        {
            List<TranTask> lstTranTask = dicTranTaskSchedule_Clear_Dirty[car_type];
            int task_RandomIndex = rnd_Index.Next(lstTranTask.Count - 1);
            Console.WriteLine(string.Format("Car Type:{0},Index:{1}", car_type, task_RandomIndex));
            TranTask tranTask = lstTranTask[task_RandomIndex];
            //Task.Run(() => mcsManager.sendTranCmd(tranTask.SourcePort, tranTask.DestinationPort));
            sendTranCmd(tranTask.SourcePort, tranTask.DestinationPort);
        }

        private void Taichung()
        {
            bool isMCS_CmdInQueue = scApp.CMDBLL.getCMD_MCSIsQueueCount() > 0;
            if (isMCS_CmdInQueue) return;
            int task_RandomIndex = rnd_Index.Next(tranTasks.Count - 1);
            TranTask tranTask = tranTasks[task_RandomIndex];
            //if (SCUtility.isMatche(tranTask.SourcePort, tranTask.DestinationPort))
            //    return;
            sendTranCmd(tranTask.SourcePort, tranTask.DestinationPort);
        }
        int cst_id_seq = 1;
        public void sendTranCmd(string source_port, string destn_port)
        {
            APORTSTATION port_station = scApp.getEQObjCacheManager().getPortStation(source_port);
            //if (port_station != null && !port_station.HAS_CST)
            //{
            //    return;
            //}
            string cst_id = $"CST{(++cst_id_seq).ToString("000")}";
            string cmdType = string.Concat(source_port, "To", destn_port);
            string cmdID = DateTime.Now.ToString("yyyyMMddHHmmssfffff");
            scApp.CMDBLL.doCreatMCSCommand(cmdID, "10", "0", cst_id, source_port, destn_port, SECSConst.HCACK_Confirm);
            scApp.SysExcuteQualityBLL.creatSysExcuteQuality(cmdID, cst_id, source_port, destn_port);
        }
    }

}

