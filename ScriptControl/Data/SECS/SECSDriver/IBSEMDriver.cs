using com.mirle.ibg3k0.bcf.App;
using com.mirle.ibg3k0.bcf.Controller;
using com.mirle.ibg3k0.sc.App;
using com.mirle.ibg3k0.sc.Common;
using com.mirle.ibg3k0.sc.Data.SECS;
using com.mirle.ibg3k0.stc.Common;
using com.mirle.ibg3k0.stc.Data.SecsData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;

namespace com.mirle.ibg3k0.sc.Data.SECSDriver
{
    public abstract class IBSEMDriver : SEMDriver
    {
        NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();


        #region Receive
        protected abstract void S2F49ReceiveEnhancedRemoteCommandExtension(object sender, SECSEventArgs e);
        protected abstract void S2F41ReceiveHostCommand(object sender, SECSEventArgs e);
        #endregion Receive

        #region Send
        #region Transfer Event
        public abstract bool S6F11SendTransferAbortCompleted(string vhID, List<AMCSREPORTQUEUE> reportQueues = null);
        public abstract bool S6F11SendTransferAbortFailed(string cmdID, List<AMCSREPORTQUEUE> reportQueues = null);
        public abstract bool S6F11SendTransferAbortInitial(string cmdID, List<AMCSREPORTQUEUE> reportQueues = null);
        public abstract bool S6F11SendTransferCancelCompleted(string vhID, List<AMCSREPORTQUEUE> reportQueues = null);
        public abstract bool S6F11SendTransferCancelCompleted(ACMD_MCS cmd, List<AMCSREPORTQUEUE> reportQueues = null);
        public abstract bool S6F11SendTransferCancelFailed(string cmdID, List<AMCSREPORTQUEUE> reportQueues = null);
        public abstract bool S6F11SendTransferCancelInitial(string cmdID, List<AMCSREPORTQUEUE> reportQueues = null);
        public abstract bool S6F11SendTransferCancelInitial(ACMD_MCS cmd, List<AMCSREPORTQUEUE> reportQueues = null);
        public abstract bool S6F11SendTransferPaused(string cmdID, List<AMCSREPORTQUEUE> reportQueues = null);
        public abstract bool S6F11SendTransferResumed(string cmdID, List<AMCSREPORTQUEUE> reportQueues = null);

        public abstract bool S6F11SendTransferInitial(string cmdID, List<AMCSREPORTQUEUE> reportQueues = null);
        public abstract bool S6F11SendTransferring(string vhID, List<AMCSREPORTQUEUE> reportQueues = null);
        public abstract bool S6F11SendVehicleArrived(string vhID, List<AMCSREPORTQUEUE> reportQueues = null);
        public abstract bool S6F11SendVehicleAcquireStarted(string vhID, List<AMCSREPORTQUEUE> reportQueues = null);
        public abstract bool S6F11SendVehicleAcquireCompleted(string vhID, List<AMCSREPORTQUEUE> reportQueues = null);
        public abstract bool S6F11SendVehicleAssigned(string vhID, List<AMCSREPORTQUEUE> reportQueues = null);
        public abstract bool S6F11SendVehicleDeparted(string vhID, List<AMCSREPORTQUEUE> reportQueues = null);
        public abstract bool S6F11SendVehicleDepositStarted(string vhID, List<AMCSREPORTQUEUE> reportQueues = null);
        public abstract bool S6F11SendVehicleDepositCompleted(string vhID, List<AMCSREPORTQUEUE> reportQueues = null);
        public abstract bool S6F11SendCarrierInstalled(string vhID, List<AMCSREPORTQUEUE> reportQueues = null);
        public abstract bool S6F11SendCarrierInstalled(string vhID, string carrierID, string transferPort, List<AMCSREPORTQUEUE> reportQueues = null);
        public abstract bool S6F11SendCarrierRemoved(string vhID, List<AMCSREPORTQUEUE> reportQueues = null);
        public abstract bool S6F11SendCarrierRemoved(string vhID, string carrierID, string transferPort, List<AMCSREPORTQUEUE> reportQueues = null);
        public abstract bool S6F11SendVehicleUnassinged(string vhID, List<AMCSREPORTQUEUE> reportQueues = null);
        public abstract bool S6F11SendTransferCompleted(string vhID, List<AMCSREPORTQUEUE> reportQueues = null);
        public abstract bool S6F11SendTransferCompleted(ACMD_MCS CMD_MCS, AVEHICLE vh, string resultCode, List<AMCSREPORTQUEUE> reportQueues = null);

        public abstract bool S6F11SendVehicleInstalled(string vhID, List<AMCSREPORTQUEUE> reportQueues = null);
        public abstract bool S6F11SendVehicleRemoved(string vhID, List<AMCSREPORTQUEUE> reportQueues = null);
        #endregion Transfer Event
        #region Port Event
        public abstract bool S6F11PortEventStateChanged(string vhID, List<AMCSREPORTQUEUE> reportQueues = null);
        public abstract bool sendS6F11_PortOutOfService(string port_id, List<AMCSREPORTQUEUE> reportQueues = null);
        public abstract bool sendS6F11_PortInService(string port_id, List<AMCSREPORTQUEUE> reportQueues = null);
        #endregion Port Event

        #region TSC State Transition Event
        public abstract bool S6F11SendTSCAutoCompleted();
        public abstract bool S6F11SendTSCAutoInitiated();
        public abstract bool S6F11SendTSCPauseCompleted();
        public abstract bool S6F11SendTSCPaused();
        public abstract bool S6F11SendTSCPauseInitiated(string pausrReason);
        #endregion TSC State Transition Event


        #endregion Send

    }

    public class IBSEMDriverEmpty : IBSEMDriver
    {
        public override AMCSREPORTQUEUE S6F11BulibMessage(string ceid, object Vids)
        {
            return null;
        }

        public override bool S6F11PortEventStateChanged(string vhID, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendAlarmCleared()
        {
            return true;
        }

        public override bool S6F11SendAlarmSet()
        {
            return true;

        }

        public override bool S6F11SendCarrierInstalled(string vhID, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendCarrierInstalled(string vhID, string carrierID, string transferPort, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendCarrierRemoved(string vhID, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendCarrierRemoved(string vhID, string carrierID, string transferPort, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendControlStateLocal()
        {
            return true;

        }

        public override bool S6F11SendControlStateRemote()
        {
            return true;

        }

        public override bool S6F11SendEquiptmentOffLine()
        {
            return true;

        }


        public override bool S6F11SendMessage(AMCSREPORTQUEUE queue)
        {
            return true;

        }

        public override bool S6F11SendTransferAbortCompleted(string vhID, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendTransferAbortFailed(string cmdID, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendTransferAbortInitial(string cmdID, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendTransferCancelCompleted(string vhID, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendTransferCancelCompleted(ACMD_MCS cmd, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendTransferCancelFailed(string cmdID, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendTransferCancelInitial(string cmdID, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendTransferCancelInitial(ACMD_MCS cmd, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendTransferCompleted(string vhID, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendTransferCompleted(ACMD_MCS CMD_MCS, AVEHICLE vh, string resultCode, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendTransferInitial(string cmdID, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendTransferPaused(string cmdID, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendTransferResumed(string cmdID, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendTransferring(string vhID, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendTSCAutoCompleted()
        {
            return true;
        }

        public override bool S6F11SendTSCAutoInitiated()
        {
            return true;
        }

        public override bool S6F11SendTSCPauseCompleted()
        {
            return true;
        }

        public override bool S6F11SendTSCPaused()
        {
            return true;
        }

        public override bool S6F11SendTSCPauseInitiated(string pausrReason)
        {
            return true;
        }

        public override bool S6F11SendVehicleAcquireCompleted(string vhID, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendVehicleAcquireStarted(string vhID, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendVehicleArrived(string vhID, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendVehicleAssigned(string vhID, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendVehicleDeparted(string vhID, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendVehicleDepositCompleted(string vhID, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendVehicleDepositStarted(string vhID, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendVehicleInstalled(string vhID, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendVehicleRemoved(string vhID, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool S6F11SendVehicleUnassinged(string vhID, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool sendS6F11_PortInService(string port_id, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        public override bool sendS6F11_PortOutOfService(string port_id, List<AMCSREPORTQUEUE> reportQueues = null)
        {
            return true;
        }

        protected override void S2F41ReceiveHostCommand(object sender, SECSEventArgs e)
        {
        }

        protected override void S2F49ReceiveEnhancedRemoteCommandExtension(object sender, SECSEventArgs e)
        {
        }
    }
}