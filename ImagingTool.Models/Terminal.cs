using System.Collections.Generic;

namespace ImagingTool.Models
{
    public class Terminal
    {
        public string Model { get; set; }
        public string Cpu { get; set; }
        public string Os { get; set; }
        public List<Driver> Drivers { get; set; }
    }
}