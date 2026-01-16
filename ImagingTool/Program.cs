using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;

namespace ImagingTool
{
    public class ComputerSystemInfo
    {
        public string Manufacturer { get; set; }
        public string Model { get; set; }
        public string SystemType { get; set; }
    }

    public class HardwareInfoProvider
    {
        public ComputerSystemInfo GetComputerSystemInfo()
        {
            foreach (ManagementObject cs in new ManagementObjectSearcher(
                         "SELECT Manufacturer, Model, SystemType FROM Win32_ComputerSystem").Get())
            {
                return new ComputerSystemInfo
                {
                    Manufacturer = cs["Manufacturer"]?.ToString(),
                    Model = cs["Model"]?.ToString(),
                    SystemType = cs["SystemType"]?.ToString()
                };
            }
            return null;
        }
    }


    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Computer System Info:");
            var provider = new HardwareInfoProvider();
            var cs = provider.GetComputerSystemInfo();
            if (cs != null)
            {
                Console.WriteLine($"  Manufacturer: {cs.Manufacturer}");
                Console.WriteLine($"  Model: {cs.Model}");
                Console.WriteLine($"  SystemType: {cs.SystemType}");
                Console.WriteLine();
            }
        }
    }
}
