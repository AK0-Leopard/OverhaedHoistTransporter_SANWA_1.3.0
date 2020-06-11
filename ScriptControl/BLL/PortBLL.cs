using com.mirle.ibg3k0.sc.App;
using com.mirle.ibg3k0.sc.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.mirle.ibg3k0.sc.BLL
{
    public class PortBLL
    {
        public DB OperateDB { private set; get; }
        public Catch OperateCatch { private set; get; }

        public PortBLL()
        {
        }
        public void start(SCApplication _app)
        {
            OperateDB = new DB();
            OperateCatch = new Catch(_app.getEQObjCacheManager());
        }
        public class DB
        {

        }
        public class Catch
        {
            EQObjCacheManager CacheManager;
            public Catch(EQObjCacheManager _cache_manager)
            {
                CacheManager = _cache_manager;
            }

            public void updatePortStationCSTExistStatus(string portOrAdrID, string cst_id)
            {
                APORTSTATION port_station = CacheManager.getPortStation(portOrAdrID);
                if (port_station == null)
                    port_station = CacheManager.getPortStationByAdrID(portOrAdrID);
                if (port_station != null)
                {
                    port_station.CST_ID = cst_id;
                }
            }
        }
    }
}