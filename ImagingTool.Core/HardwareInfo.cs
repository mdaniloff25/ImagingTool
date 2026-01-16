using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;

namespace ImagingTool.Core
{
    //public class CpuInfo
    //{
    //    internal bool HasMaxClockSpeed;

    //    public string Name { get; set; }
    //    public string Manufacturer { get; set; }
    //    public uint NumberOfCores { get; set; }
    //    public uint NumberOfLogicalProcessors { get; set; }
    //    public uint? MaxClockSpeed { get; set; }
    //}
    public static class HardwareInfo
    {
        
        public static string GetModel()
        {
            // Use WMI to get model
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem"))
            {
                foreach (var obj in searcher.Get())
                    return obj["Model"]?.ToString();
            }
            return string.Empty;
        }

        public static string GetCpu()
        {
            using (var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor"))
            {
                foreach (var obj in searcher.Get())
                    return obj["Name"]?.ToString();
            }
            return string.Empty;
        }

        //public static List<CpuInfo> GetCpuInfo()
        //{
        //    var cpus = new List<CpuInfo>();
        //    foreach (var o in new ManagementObjectSearcher(
        //                 "SELECT Name, Manufacturer, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed FROM Win32_Processor").Get())
        //    {
        //        var cpu = (ManagementObject)o;
        //        cpus.Add(new CpuInfo
        //        {
        //            Name = cpu["Name"]?.ToString(),
        //            Manufacturer = cpu["Manufacturer"]?.ToString(),
        //            NumberOfCores = cpu["NumberOfCores"] != null ? Convert.ToUInt32(cpu["NumberOfCores"]) : 0,
        //            NumberOfLogicalProcessors = cpu["NumberOfLogicalProcessors"] != null ? Convert.ToUInt32(cpu["NumberOfLogicalProcessors"]) : 0,
        //            MaxClockSpeed = cpu["MaxClockSpeed"] != null ? (uint?)Convert.ToUInt32(cpu["MaxClockSpeed"]) : null
        //        });
        //    }
        //    return cpus;
        //}
    }

}
