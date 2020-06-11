using com.mirle.ibg3k0.sc.App;
using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.mirle.ibg3k0.sc.Common
{
    public static class StateMachineFactory
    {
        static public StateMachine<SCAppConstants.E_VH_STS, SCAppConstants.E_VH_EVENT> creatVHStateMachine(Func<SCAppConstants.E_VH_STS> stateAccessor, Action<SCAppConstants.E_VH_STS> stateMutator)
        {
            StateMachine<SCAppConstants.E_VH_STS, SCAppConstants.E_VH_EVENT> sm = new StateMachine<SCAppConstants.E_VH_STS, SCAppConstants.E_VH_EVENT>(stateAccessor, stateMutator);
            #region define transition
            sm.Configure(SCAppConstants.E_VH_STS.None).
                Permit(SCAppConstants.E_VH_EVENT.CompensationDataRep, SCAppConstants.E_VH_STS.Initial);

            sm.Configure(SCAppConstants.E_VH_STS.Initial).
                Permit(SCAppConstants.E_VH_EVENT.CompensationDataError, SCAppConstants.E_VH_STS.None).
                Permit(SCAppConstants.E_VH_EVENT.doDataSync, SCAppConstants.E_VH_STS.DataSyncing);

            sm.Configure(SCAppConstants.E_VH_STS.DataSyncing).
                Permit(SCAppConstants.E_VH_EVENT.DataSyncComplete, SCAppConstants.E_VH_STS.PowerOff).
                Permit(SCAppConstants.E_VH_EVENT.DataSyncFail, SCAppConstants.E_VH_STS.Initial);

            sm.Configure(SCAppConstants.E_VH_STS.PowerOff).
                Permit(SCAppConstants.E_VH_EVENT.VHPowerStatChg_PowerOn, SCAppConstants.E_VH_STS.PowerOn);

            sm.Configure(SCAppConstants.E_VH_STS.PowerOn).
                Permit(SCAppConstants.E_VH_EVENT.OperationToManual, SCAppConstants.E_VH_STS.Manual);

            sm.Configure(SCAppConstants.E_VH_STS.OperationMode).
                Permit(SCAppConstants.E_VH_EVENT.AlarmHappend, SCAppConstants.E_VH_STS.Alarm).
                Permit(SCAppConstants.E_VH_EVENT.ErrorHappend, SCAppConstants.E_VH_STS.Error);

            sm.Configure(SCAppConstants.E_VH_STS.Manual).
                SubstateOf(SCAppConstants.E_VH_STS.OperationMode).
                Permit(SCAppConstants.E_VH_EVENT.OperationToAuto, SCAppConstants.E_VH_STS.Auto).
                Permit(SCAppConstants.E_VH_EVENT.VHPowerStatChg_PowerOff, SCAppConstants.E_VH_STS.PowerOff);

            sm.Configure(SCAppConstants.E_VH_STS.Auto).
                SubstateOf(SCAppConstants.E_VH_STS.OperationMode).
                Permit(SCAppConstants.E_VH_EVENT.OperationToManual, SCAppConstants.E_VH_STS.Manual);

            sm.Configure(SCAppConstants.E_VH_STS.Alarm).
                Permit(SCAppConstants.E_VH_EVENT.AlarmClear, SCAppConstants.E_VH_STS.Manual);
            sm.Configure(SCAppConstants.E_VH_STS.Error).
                Permit(SCAppConstants.E_VH_EVENT.ErrorClear, SCAppConstants.E_VH_STS.PowerOff);
            #endregion
            //Console.Write(sm.ToDotGraph());//輸出一段文字，可以轉為Stata Machine的架構圖
            return sm;
        }

        static public StateMachine<SCAppConstants.E_Cmd_STS, SCAppConstants.E_Cmd_EVENT>
            creatCommandStateMachine(Func<SCAppConstants.E_Cmd_STS> stateAccessor, Action<SCAppConstants.E_Cmd_STS> stateMutator)
        {
            StateMachine<SCAppConstants.E_Cmd_STS, SCAppConstants.E_Cmd_EVENT> sm = new StateMachine<SCAppConstants.E_Cmd_STS, SCAppConstants.E_Cmd_EVENT>(stateAccessor, stateMutator);
        #region define transition
            sm.Configure(SCAppConstants.E_Cmd_STS.None).
                Permit(SCAppConstants.E_Cmd_EVENT.HasBeenSent, SCAppConstants.E_Cmd_STS.Sent);

            sm.Configure(SCAppConstants.E_Cmd_STS.Sent).
                Permit(SCAppConstants.E_Cmd_EVENT.CommandHasBeenAccepted, SCAppConstants.E_Cmd_STS.InExecution).
                Permit(SCAppConstants.E_Cmd_EVENT.CommandWasRejected, SCAppConstants.E_Cmd_STS.Rejected);

            sm.Configure(SCAppConstants.E_Cmd_STS.Rejected).
                Permit(SCAppConstants.E_Cmd_EVENT.Finish, SCAppConstants.E_Cmd_STS.None);

            sm.Configure(SCAppConstants.E_Cmd_STS.InExecution).
                Permit(SCAppConstants.E_Cmd_EVENT.HasBeenCompleted_Abort, SCAppConstants.E_Cmd_STS.Abort).
                Permit(SCAppConstants.E_Cmd_EVENT.HasBeenCompleted_Cancel, SCAppConstants.E_Cmd_STS.Cancel).
                Permit(SCAppConstants.E_Cmd_EVENT.HasBeenCompleted_ForcedCmp, SCAppConstants.E_Cmd_STS.ForcedCmp).
                Permit(SCAppConstants.E_Cmd_EVENT.HasBeenCompleted_NormalCmp, SCAppConstants.E_Cmd_STS.NormalCmp).
                Permit(SCAppConstants.E_Cmd_EVENT.HasBeenCompleted_Error, SCAppConstants.E_Cmd_STS.Error);

            sm.Configure(SCAppConstants.E_Cmd_STS.Abort).
                SubstateOf(SCAppConstants.E_Cmd_STS.Completed);
            sm.Configure(SCAppConstants.E_Cmd_STS.Cancel).
                SubstateOf(SCAppConstants.E_Cmd_STS.Completed);
            sm.Configure(SCAppConstants.E_Cmd_STS.ForcedCmp).
                SubstateOf(SCAppConstants.E_Cmd_STS.Completed);
            sm.Configure(SCAppConstants.E_Cmd_STS.NormalCmp).
                SubstateOf(SCAppConstants.E_Cmd_STS.Completed);
            sm.Configure(SCAppConstants.E_Cmd_STS.Error).
                SubstateOf(SCAppConstants.E_Cmd_STS.Completed);

            sm.Configure(SCAppConstants.E_Cmd_STS.Completed).
                Permit(SCAppConstants.E_Cmd_EVENT.Finish, SCAppConstants.E_Cmd_STS.None);
            #endregion
            //Console.Write(sm.ToDotGraph());//輸出一段文字，可以轉為Stata Machine的架構圖
            return sm;
        }
    }
}
