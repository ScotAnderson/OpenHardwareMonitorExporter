﻿using Microsoft.Win32;
using Prometheus;
using Prometheus.Advanced;
using System;
using System.Reflection;
using Topshelf;

namespace OpenHardwareMonitorExporter
{
    public class OpenHardwareMonitorService
    {
        private MetricServer _metricServer;

        public OpenHardwareMonitorService(Uri url)
        {
            DefaultCollectorRegistry.Instance.Clear();
            DefaultCollectorRegistry.Instance.RegisterOnDemandCollectors(new MetricsCollector());

            _metricServer = new MetricServer(
                hostname: url.Host,
                port: url.Port,
                url: url.AbsolutePath.Trim('/')+'/',
                useHttps: url.Scheme == "https");
        }

        public void Start()
        {
            _metricServer.Start();
        }

        public void Stop()
        {
            _metricServer.Stop();
        }
    }

    class Program
    {
        static int Main(string[] args)
        {
            var exitCode = HostFactory.Run(hc =>
            {
                var url = new Uri("http://localhost:9398/metrics"); // see https://github.com/prometheus/prometheus/wiki/Default-port-allocations
                hc.AddCommandLineDefinition("url", value => url = new Uri(value));
                hc.AfterInstall((ihs) =>
                {
                    // add -url to the service parameters.
                    using (var services = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services"))
                    using (var service = services.OpenSubKey(ihs.ServiceName, true))
                    {
                        service.SetValue(
                            "ImagePath",
                            string.Format(
                                "{0} -url {1}",
                                service.GetValue("ImagePath"),
                                url));
                    }
                });
                hc.Service<OpenHardwareMonitorService>(sc =>
                {
                    sc.ConstructUsing(settings => new OpenHardwareMonitorService(url));
                    sc.WhenStarted(service => service.Start());
                    sc.WhenStopped(service => service.Stop());
                });
                hc.EnableServiceRecovery(rc =>
                {
                    rc.RestartService(1); // first failure: restart after 1 minute
                    rc.RestartService(1); // second failure: restart after 1 minute
                    rc.RestartService(1); // subsequent failures: restart after 1 minute
                });
                hc.StartAutomatically();
                hc.RunAsLocalSystem();
                hc.SetDescription(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyDescriptionAttribute>().Description);
                hc.SetDisplayName(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyTitleAttribute>().Title);
                hc.SetServiceName(Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyProductAttribute>().Product);
            });
            return (int)Convert.ChangeType(exitCode, exitCode.GetTypeCode());
        }
    }
}
