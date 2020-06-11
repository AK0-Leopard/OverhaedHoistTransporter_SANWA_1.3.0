//*********************************************************************************
//      FourColorLightValueDefMapAction.cs
//*********************************************************************************
// File Name: FourColorLightValueDefMapAction.cs
// Description: 
//
//(c) Copyright 2018, MIRLE Automation Corporation
//
// Date          Author         Request No.    Tag     Description
// ------------- -------------  -------------  ------  -----------------------------
//**********************************************************************************
using com.mirle.ibg3k0.bcf.App;
using com.mirle.ibg3k0.bcf.Data.ValueDefMapAction;
using com.mirle.ibg3k0.bcf.Data.VO;
using com.mirle.ibg3k0.sc.App;
using com.mirle.ibg3k0.sc.Common;
using com.mirle.ibg3k0.sc.Data.PLC_Functions;
using com.mirle.ibg3k0.sc.Data.VO;
using KingAOP;
using NLog;
using System;
using System.Dynamic;
using System.Linq.Expressions;

namespace com.mirle.ibg3k0.sc.Data.ValueDefMapAction
{
    public class FourColorLightValueDefMapAction : IValueDefMapAction
    {
        public const string DEVICE_NAME_FOURCOLORLIGHT = "FOUR_COLOR_LIGHT";
        Logger logger = LogManager.GetCurrentClassLogger();
        AEQPT eqpt = null;
        bool IsFilckRedLight = false;
        FilckTimerAction RedLightFilckTimerAction = null;
        protected SCApplication scApp = null;
        protected BCFApplication bcfApp = null;
        public FourColorLightValueDefMapAction()
            : base()
        {
            scApp = SCApplication.getInstance();
            bcfApp = scApp.getBCFApplication();
        }
        public DynamicMetaObject GetMetaObject(Expression parameter)
        {
            return new AspectWeaver(parameter, this);
        }

        public virtual string getIdentityKey()
        {
            return this.GetType().Name;
        }
        public virtual void setContext(BaseEQObject baseEQ)
        {
            try
            {
                this.eqpt = baseEQ as AEQPT;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exection:");
            }
        }
        public virtual void unRegisterEvent()
        {
            //not implement
        }
        public virtual void doShareMemoryInit(BCFAppConstants.RUN_LEVEL runLevel)
        {
            try
            {
                switch (runLevel)
                {
                    case BCFAppConstants.RUN_LEVEL.ZERO:
                        initialValueWrite();
                        initialFilckTimerAction();
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

        private void initialFilckTimerAction()
        {
            RedLightFilckTimerAction = new FilckTimerAction(eqpt.EqptObjectCate, eqpt.EQPT_ID, "RED_LIGHT", this);
        }

        private void initialValueWrite()
        {
            sendFourColorLightRedWithBuzzerSignal(false, false);
            sendFourColorLightOrangeSignal(false);
            sendFourColorLightGreenSignal(false);
            sendFourColorLightBlueSignal(false);
        }


        public void sendFourColorLightRedWithBuzzerSignal(bool buzzer_signal, bool light_signal)
        {
            var function =
                scApp.getFunBaseObj<FourColorLightRedWithBuzzer>(eqpt.EQPT_ID) as FourColorLightRedWithBuzzer;
            try
            {
                if (DebugParameter.isForcePassFourColorLightRedWithBuzzerSignal)
                {
                    buzzer_signal = false;
                    light_signal = false;
                }

                //1.建立各個Function物件
                function.Buzzer = buzzer_signal;
                function.RedLight = light_signal;
                function.Write(bcfApp, eqpt.EqptObjectCate, eqpt.EQPT_ID);
                //2.write log
                LogManager.GetLogger("com.mirle.ibg3k0.sc.Common.LogHelper").Info(function.ToString());
                //3.logical (include db save)

            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception");
            }
            finally
            {
                scApp.putFunBaseObj<FourColorLightRedWithBuzzer>(function);
            }
        }

        public void sendFourColorLightRedToFlickWithBuzzerSignal(bool buzzer_signal, bool light_signal)
        {
            var buzzer_function =
                scApp.getFunBaseObj<FourColorBuzzer>(eqpt.EQPT_ID) as FourColorBuzzer;
            var red_light_function =
                scApp.getFunBaseObj<FourColorLightRed>(eqpt.EQPT_ID) as FourColorLightRed;
            try
            {
                if (DebugParameter.isForcePassFourColorLightRedWithBuzzerSignal)
                {
                    buzzer_signal = false;
                    light_signal = false;
                    IsFilckRedLight = false;
                }

                //1.建立各個Function物件
                buzzer_function.Buzzer = buzzer_signal;
                buzzer_function.Write(bcfApp, eqpt.EqptObjectCate, eqpt.EQPT_ID);

                if (light_signal)
                {
                    IsFilckRedLight = true;
                    RedLightFilckTimerAction.start();
                }
                else
                {
                    IsFilckRedLight = false;
                    RedLightFilckTimerAction.stop();
                    red_light_function.RedLight = false;
                    red_light_function.Write(bcfApp, eqpt.EqptObjectCate, eqpt.EQPT_ID);
                }
                //2.write log
                LogManager.GetLogger("com.mirle.ibg3k0.sc.Common.LogHelper").Info(buzzer_function.ToString());
                //3.logical (include db save)

            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception");
            }
            finally
            {
                scApp.putFunBaseObj<FourColorBuzzer>(buzzer_function);
                scApp.putFunBaseObj<FourColorLightRed>(red_light_function);
            }
        }

        public class FilckTimerAction : bcf.Data.TimerAction.ITimerAction
        {
            private static Logger logger = LogManager.GetCurrentClassLogger();
            SCApplication scApp = null;
            bcf.Controller.ValueWrite vw = null;
            FourColorLightValueDefMapAction mapAction;

            public FilckTimerAction(string eqObjIDCate, string eqID, string valueWriteName, FourColorLightValueDefMapAction mapAction)
                : base(eqID, 2000)
            {
                scApp = SCApplication.getInstance();
                vw = scApp.getBCFApplication().getWriteValueEvent(eqObjIDCate, eqID, valueWriteName);
                this.mapAction = mapAction;
            }

            public override void initStart()
            {
            }

            bool FlickFlag = false;
            private long syncPointFilck = 0;
            public override void doProcess(object obj)
            {
                if (!mapAction.IsFilckRedLight) return;
                if (System.Threading.Interlocked.Exchange(ref syncPointFilck, 1) == 0)
                {
                    try
                    {
                        if (vw != null)
                        {
                            if (FlickFlag)
                            {
                                FlickFlag = false;
                            }
                            else
                            {
                                FlickFlag = true;
                            }
                            vw.setWriteValue(FlickFlag ? "1" : "0");
                            bcf.Controller.ISMControl.writeDeviceBlock(scApp.getBCFApplication(), vw);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Log(logger: logger, LogLevel: LogLevel.Warn, Class: nameof(AVEHICLE), Device: "OHT",
                           Data: ex);
                    }
                    finally
                    {
                        System.Threading.Interlocked.Exchange(ref syncPointFilck, 0);
                    }
                }
            }
        }


        public void sendFourColorLightOrangeSignal(bool signal)
        {
            var function =
                scApp.getFunBaseObj<FourColorLightOrange>(eqpt.EQPT_ID) as FourColorLightOrange;
            try
            {
                //1.建立各個Function物件
                function.OrangeLight = signal;
                function.Write(bcfApp, eqpt.EqptObjectCate, eqpt.EQPT_ID);
                //2.write log
                LogManager.GetLogger("com.mirle.ibg3k0.sc.Common.LogHelper").Info(function.ToString());
                //3.logical (include db save)

            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception");
            }
            finally
            {
                scApp.putFunBaseObj<FourColorLightOrange>(function);
            }
        }
        public void sendFourColorLightGreenSignal(bool signal)
        {
            var function =
                scApp.getFunBaseObj<FourColorLightGreen>(eqpt.EQPT_ID) as FourColorLightGreen;
            try
            {
                //1.建立各個Function物件
                function.GreenLight = signal;
                function.Write(bcfApp, eqpt.EqptObjectCate, eqpt.EQPT_ID);
                //2.write log
                LogManager.GetLogger("com.mirle.ibg3k0.sc.Common.LogHelper").Info(function.ToString());
                //3.logical (include db save)

            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception");
            }
            finally
            {
                scApp.putFunBaseObj<FourColorLightGreen>(function);
            }
        }




        public void sendFourColorLightBlueSignal(bool signal)
        {
            var function =
                scApp.getFunBaseObj<FourColorLightBlue>(eqpt.EQPT_ID) as FourColorLightBlue;
            try
            {
                //1.建立各個Function物件
                function.BlueLight = signal;
                function.Write(bcfApp, eqpt.EqptObjectCate, eqpt.EQPT_ID);
                //2.write log
                LogManager.GetLogger("com.mirle.ibg3k0.sc.Common.LogHelper").Info(function.ToString());
                //3.logical (include db save)

            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exception");
            }
            finally
            {
                scApp.putFunBaseObj<FourColorLightBlue>(function);
            }
        }


        /// <summary>
        /// Does the initialize.
        /// </summary>
        public virtual void doInit()
        {
            try
            {
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Exection:");
            }

        }

    }
}
