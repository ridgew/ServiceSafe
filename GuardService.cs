using System;
using System.Collections.Generic;
using System.Configuration;
using System.Management;
using System.ServiceProcess;

namespace ServiceSafe
{
    /// <summary>
    /// 服务守护服务（防止特定服务被停止）
    /// </summary>
    public class GuardService : ServiceBase
    {
        /// <summary>
        /// 初始化一个 <see cref="GuardService"/> class 实例。
        /// </summary>
        /// <param name="svrName">轮询的服务名称，多个服务名称使用,、;、 分隔即可。</param>
        /// <param name="msDuration">轮询毫秒数</param>
        public GuardService(string svrName, double msDuration)
        {
            _serviceNameList = svrName;
            _defaultDuration = msDuration;
        }

        string _serviceNameList = null;
        double _defaultDuration = 1000;

        /// <summary>
        /// 检查列表
        /// </summary>
        List<ServiceCheck> checkList = new List<ServiceCheck>();

        /// <summary>
        /// 当在派生类中实现时，在下列情况下执行：在“服务控制管理器”(SCM) 向服务发送“开始”命令时，或者在操作系统启动时（对于自动启动的服务）。指定服务启动时采取的操作。
        /// </summary>
        /// <param name="args">启动命令传递的数据。</param>
        protected override void OnStart(string[] args)
        {
            StartService();
        }

        /// <summary>
        /// 在派生类中实现时，该方法于系统即将关闭时执行。该方法指定应在系统即将关闭前执行的处理。
        /// </summary>
        protected override void OnShutdown()
        {
            StopService();
        }

        public void StartService()
        {
            string[] svrList = _serviceNameList.Split(new char[] { ' ', ';', ',' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (string svr in svrList)
            {
                if (!ExistService(svr)) continue;
                ServiceCheck chk = new ServiceCheck(svr, Convert.ToDouble(ConfigurationManager.AppSettings[svr + ".Duration"] ?? _defaultDuration.ToString()));
                checkList.Add(chk);
                chk.Start();
            }
        }

        /// <summary>
        /// 在派生类中实现时，该方法于“服务控制管理器”(SCM) 将“停止”命令发送到服务时执行。指定服务停止运行时采取的操作。
        /// </summary>
        protected override void OnStop()
        {
            StopService();
        }

        public void StopService()
        {
            if (!ExistService(_serviceNameList) || checkList.Count == 0) return;
            foreach (ServiceCheck chk in checkList)
            {
                chk.Stop();
                chk.Dispose();
            }
            checkList.Clear();
        }

        /// <summary>
        /// 守护
        /// </summary>
        internal static ThreadSafeDictionary<string, DateTime> MonitorServDict = new ThreadSafeDictionary<string, DateTime>(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Exists the service.
        /// </summary>
        /// <param name="servcieName">Name of the servcie.</param>
        /// <returns></returns>
        public static bool ExistService(string servcieName)
        {
            string sql = "SELECT PathName from Win32_Service where Name =\"" + servcieName + "\"";
            string svrPath = null;
            using (ManagementObjectSearcher Searcher = new ManagementObjectSearcher(sql))
            {
                foreach (ManagementObject service in Searcher.Get())
                {
                    svrPath = service["PathName"].ToString();
                    break;
                }
            }
            return svrPath != null;
        }

    }


}
