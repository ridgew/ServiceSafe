using System;
using System.Configuration;
using System.ServiceProcess;

namespace ServiceSafe
{
    class Program
    {
        static GuardService svr = null;

        static void Main(string[] args)
        {
            string svrName = null;
            double duration = 5000;
            if (args == null || args.Length < 1)
            {
                string strTemp = ConfigurationManager.AppSettings["ServiceSafe.ServiceName"];
                if (strTemp != null)
                {
                    svrName = strTemp;
                }

                strTemp = ConfigurationManager.AppSettings["ServiceSafe.Duration"];
                if (strTemp != null)
                {
                    duration = Convert.ToDouble(strTemp);
                }
            }
            else
            {
                if (svr != null && args[0].Equals("stop", StringComparison.InvariantCultureIgnoreCase))
                {
                    svr.StopService();
                }

                svrName = args[0];
                if (args.Length > 1)
                {
                    duration = Convert.ToDouble(args[1]);
                }
            }

            if (svrName == null)
            {
                Console.WriteLine("请指定要监控的服务名称及轮询时间毫秒数(可选)，如参数\"w3svc 5000\"表示每隔5秒轮询IIS的Web服务！");
            }
            else
            {
                if (svr != null) svr.StopService();
                if (Environment.UserInteractive)
                {
                    svr = new GuardService(svrName, duration);
                    svr.StartService();
                    Console.Read();
                }
                else
                {
                    ServiceBase[] ServicesToRun;
                    ServicesToRun = new ServiceBase[] { new GuardService(svrName, duration) };
                    ServiceBase.Run(ServicesToRun);
                }
            }
        }
    }
}
