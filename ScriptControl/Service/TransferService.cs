using com.mirle.ibg3k0.bcf.Common;
using com.mirle.ibg3k0.sc.App;
using com.mirle.ibg3k0.sc.BLL;
using com.mirle.ibg3k0.sc.Common;
using com.mirle.ibg3k0.sc.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Transactions;
using static com.mirle.ibg3k0.sc.ALINE;
using static com.mirle.ibg3k0.sc.AVEHICLE;

namespace com.mirle.ibg3k0.sc.Service
{
    public class TransferService
    {
        NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private SCApplication scApp = null;
        private ReportBLL reportBLL = null;
        private LineBLL lineBLL = null;
        private ALINE line = null;
        public TransferService()
        {

        }
        public void start(SCApplication _app)
        {
            scApp = _app;
            reportBLL = _app.ReportBLL;
            lineBLL = _app.LineBLL;
            line = scApp.getEQObjCacheManager().getLine();

            line.addEventHandler(nameof(ConnectionInfoService), nameof(line.MCSCommandAutoAssign), PublishTransferInfo);


            initPublish(line);
        }
        private void initPublish(ALINE line)
        {
            PublishTransferInfo(line, null);
            //PublishOnlineCheckInfo(line, null);
            //PublishPingCheckInfo(line, null);
        }

        private void PublishTransferInfo(object sender, PropertyChangedEventArgs e)
        {
            try
            {
                ALINE line = sender as ALINE;
                if (sender == null) return;
                byte[] line_serialize = BLL.LineBLL.Convert2GPB_TransferInfo(line);
                scApp.getNatsManager().PublishAsync
                    (SCAppConstants.NATS_SUBJECT_TRANSFER, line_serialize);


                //TODO 要改用GPP傳送
                //var line_Serialize = ZeroFormatter.ZeroFormatterSerializer.Serialize(line);
                //scApp.getNatsManager().PublishAsync
                //    (string.Format(SCAppConstants.NATS_SUBJECT_LINE_INFO), line_Serialize);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception:");
            }
        }


        public bool doChangeLinkStatus(string linkStatus, out string result)
        {
            bool isSuccess = true;
            result = string.Empty;
            try
            {
                if (isSuccess)
                {
                    using (TransactionScope tx = SCUtility.getTransactionScope())
                    {
                        using (DBConnection_EF con = DBConnection_EF.GetUContext())
                        {
                            if (linkStatus == SCAppConstants.LinkStatus.LinkOK.ToString())
                            {
                                if (scApp.getEQObjCacheManager().getLine().Secs_Link_Stat == SCAppConstants.LinkStatus.LinkOK)
                                {
                                    result = "Selected already!";
                                }
                                else
                                {
                                    Task.Run(() => scApp.LineService.startHostCommunication());
                                    result = "OK";
                                }

                                tx.Complete();

                            }
                            else if (linkStatus == SCAppConstants.LinkStatus.LinkFail.ToString())
                            {
                                if (scApp.getEQObjCacheManager().getLine().Secs_Link_Stat == SCAppConstants.LinkStatus.LinkFail)
                                {
                                    result = "Not selected already!";
                                }
                                else
                                {
                                    Task.Run(() => scApp.LineService.stopHostCommunication());
                                    result = "OK";
                                }

                                tx.Complete();

                            }
                            else
                            {
                                result = linkStatus + " Not Defined";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                isSuccess = false;
                logger.Error(ex, "Execption:");
            }
            return isSuccess;
        }


        public bool doChangeHostMode(string host_mode, out string result)
        {
            bool isSuccess = true;
            result = string.Empty;
            try
            {
                if (isSuccess)
                {
                    using (TransactionScope tx = SCUtility.getTransactionScope())
                    {
                        using (DBConnection_EF con = DBConnection_EF.GetUContext())
                        {
                            if (host_mode == SCAppConstants.LineHostControlState.HostControlState.On_Line_Remote.ToString())
                            {
                                if (!scApp.LineService.canOnlineWithHost())
                                {
                                    //MessageBox.Show("Has vh not ready");
                                    //回報當無法連線
                                    result = "Has vh not ready";
                                }
                                else if (scApp.getEQObjCacheManager().getLine().Host_Control_State == SCAppConstants.LineHostControlState.HostControlState.On_Line_Remote)
                                {
                                    //MessageBox.Show("On line ready");
                                    result = "OnlineRemote ready";
                                }
                                else
                                {
                                    line.resetOnlieCheckItem();
                                    Task.Run(() => scApp.LineService.OnlineRemoteWithHostOp());
                                    result = "OK";
                                }
                                //isSuccess = scApp.PortStationBLL.OperateDB.updatePriority(portID, priority);
                                //if (isSuccess)
                                //{
                                tx.Complete();
                                //    scApp.PortStationBLL.OperateCatch.updatePriority(portID, priority);
                                //}
                            }
                            else if (host_mode == SCAppConstants.LineHostControlState.HostControlState.On_Line_Local.ToString())
                            {
                                if (!scApp.LineService.canOnlineWithHost())
                                {
                                    //MessageBox.Show("Has vh not ready");
                                    //回報當無法連線
                                    result = "Has vh not ready";
                                }
                                else if (scApp.getEQObjCacheManager().getLine().Host_Control_State == SCAppConstants.LineHostControlState.HostControlState.On_Line_Local)
                                {
                                    //MessageBox.Show("On line ready");
                                    result = "OnlineLocal ready";
                                }
                                else
                                {
                                    line.resetOnlieCheckItem();
                                    Task.Run(() => scApp.LineService.OnlineLocalWithHostOp());
                                    result = "OK";
                                }
                                //isSuccess = scApp.PortStationBLL.OperateDB.updatePriority(portID, priority);
                                //if (isSuccess)
                                //{
                                tx.Complete();
                                //    scApp.PortStationBLL.OperateCatch.updatePriority(portID, priority);
                                //}
                            }
                            else
                            {
                                if (scApp.getEQObjCacheManager().getLine().SCStats != TSCState.PAUSED)
                                {
                                    //MessageBox.Show("Please change tsc state to pause first.");
                                    result = "Please change TSC Status to pause first.";
                                }
                                else if (scApp.getEQObjCacheManager().getLine().Host_Control_State == SCAppConstants.LineHostControlState.HostControlState.EQ_Off_line)
                                {
                                    //MessageBox.Show("Current is off line");
                                    result = "Current is off line";
                                }
                                else
                                {
                                    line.resetOnlieCheckItem();
                                    Task.Run(() => scApp.LineService.OfflineWithHostByOp());
                                    result = "OK";
                                }
                                //isSuccess = scApp.PortStationBLL.OperateDB.updatePriority(portID, priority);
                                //if (isSuccess)
                                //{
                                tx.Complete();
                                //    scApp.PortStationBLL.OperateCatch.updatePriority(portID, priority);
                                //}
                            }

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                isSuccess = false;
                logger.Error(ex, "Execption:");
            }
            return isSuccess;
        }

        public bool doChangeTSCstate(string tscstate, out string result)
        {
            bool isSuccess = true;
            result = string.Empty;
            try
            {
                if (isSuccess)
                {
                    using (TransactionScope tx = SCUtility.getTransactionScope())
                    {
                        using (DBConnection_EF con = DBConnection_EF.GetUContext())
                        {
                            if (tscstate == ALINE.TSCState.AUTO.ToString())
                            {
                                if (scApp.getEQObjCacheManager().getLine().SCStats == ALINE.TSCState.AUTO)
                                {
                                    result = "AUTO ready";
                                }
                                else
                                {
                                    Task.Run(() => scApp.getEQObjCacheManager().getLine().ResumeToAuto(scApp.ReportBLL));
                                    result = "OK";
                                }
                                //isSuccess = scApp.PortStationBLL.OperateDB.updatePriority(portID, priority);
                                //if (isSuccess)
                                //{
                                tx.Complete();
                                //    scApp.PortStationBLL.OperateCatch.updatePriority(portID, priority);
                                //}
                            }
                            else if (tscstate == ALINE.TSCState.PAUSED.ToString())
                            {
                                if (scApp.getEQObjCacheManager().getLine().SCStats == ALINE.TSCState.PAUSED)
                                {
                                    //MessageBox.Show("Has vh not ready");
                                    //回報當無法連線
                                    result = "PAUSED ready";
                                }
                                else
                                {
                                    Task.Run(() => scApp.LineService.TSCStateToPause(""));
                                    result = "OK";
                                }
                                //isSuccess = scApp.PortStationBLL.OperateDB.updatePriority(portID, priority);
                                //if (isSuccess)
                                //{
                                tx.Complete();
                                //    scApp.PortStationBLL.OperateCatch.updatePriority(portID, priority);
                                //}
                            }
                            else
                            {
                                result = tscstate + " Not Defined";
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                isSuccess = false;
                logger.Error(ex, "Execption:");
            }
            return isSuccess;
        }





    }
}
