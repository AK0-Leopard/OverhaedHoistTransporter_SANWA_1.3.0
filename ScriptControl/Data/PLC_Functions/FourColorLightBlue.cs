﻿using com.mirle.ibg3k0.sc.Common;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace com.mirle.ibg3k0.sc.Data.PLC_Functions
{
    class FourColorLightBlue : PLC_FunBase
    {
        [PLCElement(ValueName = "BLUE_LIGHT")]
        public Boolean BlueLight;
    }
}
