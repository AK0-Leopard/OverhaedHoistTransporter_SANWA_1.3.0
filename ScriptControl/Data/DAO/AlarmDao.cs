//*********************************************************************************
//      AlarmDao.cs
//*********************************************************************************
// File Name: AlarmDao.cs
// Description: AlarmDao類別
//
//(c) Copyright 2014, MIRLE Automation Corporation
//
// Date          Author         Request No.    Tag     Description
// ------------- -------------  -------------  ------  -----------------------------
// 2014/03/05    Hayes Chen     N/A            N/A     Initial Release
// 2014/04/02    Miles Chen     N/A            A0.01   Modify Functions for UI Use
// 
//**********************************************************************************
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using com.mirle.ibg3k0.bcf.Common;
using com.mirle.ibg3k0.bcf.Data;
using com.mirle.ibg3k0.sc.App;
using com.mirle.ibg3k0.sc.Common;
using com.mirle.ibg3k0.sc.Data.VO;
using NLog;

namespace com.mirle.ibg3k0.sc.Data.DAO
{
    /// <summary>
    /// Class AlarmDao.
    /// </summary>
    /// <seealso cref="com.mirle.ibg3k0.bcf.Data.DaoBase" />
    public class AlarmDao : DaoBase
    {
        /// <summary>
        /// The logger
        /// </summary>
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public ALARM getSetAlarm(DBConnection_EF conn, string eq_id, string code)
        {
            var alarm = from b in conn.ALARM
                        where b.ALAM_CODE == code.Trim() &&
                         b.EQPT_ID == eq_id.Trim() &&
                         b.ALAM_STAT == ProtocolFormat.OHTMessage.ErrorStatus.ErrSet
                        select b;
            return alarm.FirstOrDefault();
        }

        /// <summary>
        /// Inserts the alarm.
        /// </summary>
        /// <param name="conn">The connection.</param>
        /// <param name="alarm">The alarm.</param>
        public void insertAlarm(DBConnection_EF conn, ALARM alarm)
        {
            try
            {
                conn.ALARM.Add(alarm);
                conn.SaveChanges();
            }
            catch (Exception ex)
            {
                logger.Warn(ex);
                throw;
            }
        }

        /// <summary>
        /// Updates the alarm.
        /// </summary>
        /// <param name="conn">The connection.</param>
        /// <param name="alarm">The alarm.</param>
        public void updateAlarm(DBConnection_EF conn, ALARM alarm)
        {
            try
            {
                conn.SaveChanges();
            }
            catch (Exception ex)
            {
                logger.Warn(ex);
                throw;
            }
        }

        public void UpdateAllAlarmStatus2ClearByVhID(DBConnection_EF con, string vh_id)
        {
            string sql = "Update [ALARM] SET [ALAM_STAT] = {0} WHERE [EQPT_ID] = {1}";
            //con.Database.ExecuteSqlCommand(sql,, vh_id, seq_no);
        }

        public int getSetAlarmCountByEQAndCode(DBConnection_EF conn, string eq_id, string code)
        {
            try
            {
                var alarm = from b in conn.ALARM
                            where b.ALAM_CODE == code.Trim() &&
                                  b.EQPT_ID == eq_id &&
                                  b.ALAM_STAT == ProtocolFormat.OHTMessage.ErrorStatus.ErrSet
                            select b;
                return alarm.Count();
            }
            catch (Exception ex)
            {
                logger.Warn(ex);
                throw;
            }
        }

        public List<ALARM> loadSetAlarm(DBConnection_EF conn, string eq_id)
        {
            try
            {
                var alarm = from a in conn.ALARM
                            where a.EQPT_ID == eq_id &&
                                  a.ALAM_STAT == ProtocolFormat.OHTMessage.ErrorStatus.ErrSet
                            select a;
                return alarm.ToList();
            }
            catch (Exception ex)
            {
                logger.Warn(ex);
                throw;
            }
        }

        public List<ALARM> loadSetAlarm(DBConnection_EF conn)
        {
            try
            {
                var alarm = from a in conn.ALARM
                            where a.ALAM_STAT == ProtocolFormat.OHTMessage.ErrorStatus.ErrSet
                            select a;
                return alarm.ToList();
            }
            catch (Exception ex)
            {
                logger.Warn(ex);
                throw;
            }
        }
        public List<ALARM> loadAlarmByConditions(DBConnection_EF conn, DateTime startDatetime, DateTime endDatetime,
            bool includeSet = false, bool includeClear = false, string eqptID = null, string alarmCode = null)
        {
            try
            {
                var alarms = conn.ALARM.Where(a => a.RPT_DATE_TIME > startDatetime && a.RPT_DATE_TIME < endDatetime);
                if (includeSet && includeClear)
                {
                    alarms = alarms.Where(a => a.ALAM_STAT == ProtocolFormat.OHTMessage.ErrorStatus.ErrSet ||
                                               a.ALAM_STAT == ProtocolFormat.OHTMessage.ErrorStatus.ErrReset);
                }
                else if (includeSet)
                {
                    alarms = alarms.Where(a => a.ALAM_STAT == ProtocolFormat.OHTMessage.ErrorStatus.ErrSet);
                }
                else if (includeClear)
                {
                    alarms = alarms.Where(a => a.ALAM_STAT == ProtocolFormat.OHTMessage.ErrorStatus.ErrReset);
                }
                else
                {
                    //沒有限定的話就全撈
                }
                if (!SCUtility.isEmpty(eqptID))
                {
                    alarms = alarms.Where(a => a.EQPT_ID == eqptID.Trim());
                }
                if (!SCUtility.isEmpty(alarmCode))
                {
                    alarms = alarms.Where(a => a.ALAM_CODE == alarmCode.Trim());
                }
                alarms = alarms.OrderBy(a => a.RPT_DATE_TIME);
                return alarms.ToList();
            }
            catch (Exception ex)
            {
                logger.Warn(ex);
                throw;
            }
        }
        public int GetSetAlarmErrorCount(DBConnection_EF conn)
        {
            try
            {
                var alarm = from a in conn.ALARM
                            where a.ALAM_STAT == ProtocolFormat.OHTMessage.ErrorStatus.ErrSet &&
                                  a.ALAM_LVL == E_ALARM_LVL.Error
                            select a;
                return alarm.Count();
            }
            catch (Exception ex)
            {
                logger.Warn(ex);
                throw;
            }
        }

        public int GetSetSpecifySeriousAlarmCount(DBConnection_EF conn, (string eqID, string alarmCode) specifySeriousAlarmInfo)
        {
            try
            {
                var alarm = from a in conn.ALARM
                            where a.EQPT_ID.StartsWith(specifySeriousAlarmInfo.eqID) && a.ALAM_CODE == specifySeriousAlarmInfo.alarmCode &&
                                  a.ALAM_STAT == ProtocolFormat.OHTMessage.ErrorStatus.ErrSet
                            select a;
                return alarm.Count();
            }
            catch (Exception ex)
            {
                logger.Warn(ex);
                throw;
            }
        }








    }
}
