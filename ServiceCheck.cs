using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.ServiceProcess;
using System.Threading;
using System.Timers;

namespace ServiceSafe
{
    public class ServiceCheck : IDisposable
    {
        System.Timers.Timer queryTimer = null;

        /// <summary>
        /// 当前是否正在执行查询
        /// </summary>
        volatile bool _isPinging = false;

        public ServiceCheck(string _serviceNameName, double duration)
        {
            _serviceName = _serviceNameName;
            _duration = duration;

            //限制至少1秒
            if (duration < 1000) duration = 1000;
            queryTimer = new System.Timers.Timer(_duration);
            queryTimer.AutoReset = true;
            queryTimer.Enabled = true;
            queryTimer.Elapsed += new ElapsedEventHandler(queryTimer_Elapsed);
        }

        string _serviceName;
        double _duration = 1000;


        public void Start()
        {
            if (queryTimer != null)
            {
                queryTimer.Start();
            }
        }

        public void Stop()
        {
            if (queryTimer != null) queryTimer.Stop();
        }


        DateTime getSeriveLastRestartTime(string serviceName)
        {
            lock (this)
            {
                DateTime lastRestartTime = default(DateTime);
                if (!GuardService.MonitorServDict.TryGetValue(serviceName, out lastRestartTime))
                {
                    lastRestartTime = default(DateTime);
                }
                return lastRestartTime;
            }
        }

        void setServiceLastRestartTime(string serviceName, DateTime restartAt)
        {
            lock (this)
            {
                GuardService.MonitorServDict.MergeSafe(serviceName, restartAt);
            }
        }

        void queryTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (_isPinging) return;

            lock (this)
            {
                _isPinging = true;

                string execCmdPath = string.Empty;
                if (GetServiceStatus(_serviceName, ref execCmdPath) != ServiceControllerStatus.Running)
                {
                    //防止太密集的重启
                    if (System.DateTime.Now.Subtract(getSeriveLastRestartTime(_serviceName)).TotalSeconds <
                        Convert.ToDouble(ConfigurationManager.AppSettings["ServiceSafe.RestartSeconds"] ?? "10"))
                    {
                        _isPinging = false;
                        return;
                    }

                    try
                    {
                        if (string.IsNullOrEmpty(execCmdPath))
                        {
                            //重启服务
                            RestartService(_serviceName);
                        }
                        else
                        {
                            //运行相关命令
                            string cmdFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, execCmdPath);
                            if (File.Exists(cmdFilePath))
                            {
                                RunCmd(cmdFilePath, Path.GetDirectoryName(cmdFilePath), "", 20, ref cmdFilePath);
                            }
                        }
                        setServiceLastRestartTime(_serviceName, DateTime.Now);
                    }
                    catch (Exception restartEx)
                    {
                        LogWriter.Error(restartEx);
                    }
                }

                _isPinging = false;
            }
        }

        /// <summary>
        /// 在限制秒数内的执行相关操作，并返回是否超时(默认20秒)。
        /// </summary>
        /// <param name="timeoutSeconds">超时秒数</param>
        /// <param name="act">相关方法操作</param>
        /// <returns>操作是否超时</returns>
        public static bool ExecTimeoutMethod(int? timeoutSeconds, Action act)
        {
            bool isTimeout = false;
            Thread workThread = new Thread(new ThreadStart(act));
            workThread.Start();
            if (!workThread.Join((timeoutSeconds.HasValue && timeoutSeconds.Value > 0) ? timeoutSeconds.Value * 1000 : 20000))
            {
                workThread.Abort();
                isTimeout = true;
            }
            return isTimeout;
        }

        /// <summary>
        /// 运行控制台命令程序并获取运行结果
        /// </summary>
        /// <param name="cmdPath">命令行程序完整路径</param>
        /// <param name="workDir">命令行程序的工作目录</param>
        /// <param name="strArgs">命令行参数</param>
        /// <param name="timeoutSeconds">执行超时秒数，至少为30秒以上。</param>
        /// <param name="output">命令行输出</param>
        /// <returns>命令行程序的状态退出码</returns>
        public static int RunCmd(string cmdPath, string workDir, string strArgs, int timeoutSeconds, ref string output)
        {
            int exitCode = -1;
            string strOutput = "";
            int newProcessID = 0;

            bool hasTimeout = ExecTimeoutMethod(timeoutSeconds, () =>
            {
                #region 限制时间运行
                using (Process proc = new Process())
                {
                    ProcessStartInfo psInfo = new ProcessStartInfo(cmdPath, strArgs);
                    psInfo.UseShellExecute = false;
                    psInfo.RedirectStandardError = true;
                    psInfo.RedirectStandardOutput = true;
                    psInfo.RedirectStandardInput = true;
                    psInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    psInfo.WorkingDirectory = workDir;
                    proc.StartInfo = psInfo;
                    if (proc.Start())
                    {
                        newProcessID = proc.Id;
                    }
                    DateTime lastAccessDatetime = DateTime.Now;
                    while (!proc.HasExited)
                    {
                        strOutput += proc.StandardOutput.ReadToEnd().Replace("\r", "");
                        System.Threading.Thread.Sleep(100);
                    }
                    exitCode = proc.ExitCode;
                    proc.Close();
                }
                #endregion
            });

            if (hasTimeout)
            {
                if (newProcessID > 0)
                {
                    Process fp = null;
                    try
                    {
                        fp = Process.GetProcessById(newProcessID);
                        if (fp != null)
                        {
                            fp.Kill(); fp.Close();
                        }
                    }
                    catch (Exception) { }
                    finally
                    {
                        if (fp != null) fp.Dispose();
                    }
                }
                strOutput += "* 在指定时间内(" + timeoutSeconds + ")秒执行超时！";
            }
            output = strOutput;
            return exitCode;
        }


        /// <summary>
        /// 获取特定服务的状态
        /// </summary>
        /// <param name="serviceName">服务名称</param>
        /// <param name="execCmdPath">该服务相关保护的命令行地址</param>
        /// <returns></returns>
        public static ServiceControllerStatus GetServiceStatus(string serviceName, ref string execCmdPath)
        {
            DiagnosticType dType = DiagnosticType.ServiceController;
            string strTemp = ConfigurationManager.AppSettings[string.Format("{0}.DiagnosticType", serviceName)];
            if (!string.IsNullOrEmpty(strTemp))
            {
                try
                {
                    dType = (DiagnosticType)Enum.Parse(typeof(DiagnosticType), strTemp);
                }
                catch { }
            }

            if (dType == DiagnosticType.ServiceController)
            {
                return new ServiceController(serviceName).Status;
            }
            else
            {
                strTemp = ConfigurationManager.AppSettings[string.Format("{0}.DiagnosticArgument", serviceName)];
                if (string.IsNullOrEmpty(strTemp))
                {
                    throw new System.Configuration.ConfigurationErrorsException("* 配置错误:需要在AppSettings中配置键值为[" + strTemp + "]的进程镜像名称！");
                }
                else
                {
                    execCmdPath = ConfigurationManager.AppSettings[string.Format("{0}.RescueCmdPath", serviceName)];
                    if (dType == DiagnosticType.HttpRequest)
                    {
                        ServiceControllerStatus status = ServiceControllerStatus.Stopped;

                        #region HTTP请求Ping
                        try
                        {

                            HttpWebRequest pingRequest = (HttpWebRequest)HttpWebRequest.Create(strTemp);
                            pingRequest.ProtocolVersion = System.Net.HttpVersion.Version10;
                            pingRequest.Timeout = Convert.ToInt32(ConfigurationManager.AppSettings["HttpWebRequest.Timeout"] ?? "2000");
                            pingRequest.Method = "GET";
                            pingRequest.KeepAlive = false;
                            pingRequest.UserAgent = "ServiceSafe/1.0 (HttpPing 1.0)";
                            pingRequest.Headers.Set(HttpRequestHeader.CacheControl, "no-cache");

                            HttpWebResponse resp = null;
                            try
                            {
                                resp = pingRequest.GetResponse() as HttpWebResponse;
                            }
                            catch (Exception httpEx)
                            {
                                if (!(httpEx is WebException)) { LogWriter.Error(httpEx); }
                            }

                            if (resp != null)
                            {
                                if (resp.StatusCode == HttpStatusCode.OK || resp.StatusCode == HttpStatusCode.NotModified)
                                {
                                    status = ServiceControllerStatus.Running;
                                }
                                else
                                {
                                    LogWriter.Infor("* 本次因状态码返回为({0}){1}重启[{2}]。", resp.StatusCode.GetHashCode(), resp.StatusCode, serviceName);
                                }
                                resp.Close();
                            }
                            else
                            {
                                LogWriter.Infor("* 本次因未能获取服务状态重启[{1}](HttpPing Timeout : {0}ms)。", pingRequest.Timeout, serviceName);
                            }
                        }
                        catch (Exception pingEx)
                        {
                            LogWriter.Error(pingEx);
                        }
                        #endregion

                        return status;
                    }
                    else
                    {
                        Process[] allProcess = Process.GetProcessesByName(strTemp);
                        if (allProcess == null || allProcess.Length < 1)
                        {
                            return ServiceControllerStatus.Stopped;
                        }
                        else
                        {
                            return ServiceControllerStatus.Running;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 重新启动服务
        /// </summary>
        public static void RestartService(string serviceName)
        {
            ServiceController controller = new ServiceController(serviceName);
            if (controller == null) return;

            bool doNext = false;
            if (controller.Status == ServiceControllerStatus.Running)
            {
                LogWriter.Infor("* 服务[{0}]正在运行，准备关闭...", serviceName);
                try
                {
                    controller.Stop();
                    controller.WaitForStatus(ServiceControllerStatus.Stopped);
                    doNext = true;
                }
                catch (Exception stopEx)
                {
                    LogWriter.Error(stopEx);
                }
            }
            if (!doNext) return;

            LogWriter.Infor("* 正在重新开始运行服务[{0}]...", serviceName);
            try
            {
                controller.Start();
                controller.WaitForStatus(ServiceControllerStatus.Running);
                LogWriter.Infor("* 重启服务[{0}]完成！", serviceName);
            }
            catch (Exception restartEx)
            {
                LogWriter.Error(restartEx);
            }
        }


        #region IDisposable 成员

        public void Dispose()
        {
            try
            {
                if (queryTimer != null)
                {
                    queryTimer.Stop();
                    queryTimer.Dispose();
                }
            }
            catch { }
            finally
            {
                queryTimer = null;
            }
        }

        #endregion
    }

    /// <summary>
    /// 诊断类型
    /// </summary>
    public enum DiagnosticType
    {
        /// <summary>
        /// 通过服务控制台
        /// </summary>
        ServiceController,
        /// <summary>
        /// 通过进程名称
        /// </summary>
        Process,
        /// <summary>
        /// HTTP请求状态
        /// </summary>
        HttpRequest
    }
}
