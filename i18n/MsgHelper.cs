using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace com.mirle.iibg3k0.i18n
{
    public class MsgHelper
    {
        private static ResourceManager rm = new ResourceManager("OHSCMapCreater.Properties.Resources", Assembly.GetExecutingAssembly());

        public static string getMsg(string defaultMsg, string msgKey)
        {
            var str = rm.GetString(msgKey);
            if (str != null)
            {
                return str;
            }
            else
            {
                return "[" + defaultMsg + "]";
            }
        }

        public static void setTooltips(ToolTip tt ,Control c, string defaultMsg, string msgKey, string[] args)
        {
            string msg = "";
            var str = rm.GetString(msgKey);
            if (str != null)
            {
                msg = str;
            }
            else
            {
                msg = "[" + defaultMsg + "]";
            }

            if (args != null && args.Length > 0)
            {
                tt.SetToolTip(c, string.Format(msg, args));

            }
            else
            {
                tt.SetToolTip(c, msg);

            }


        }
    }
}
