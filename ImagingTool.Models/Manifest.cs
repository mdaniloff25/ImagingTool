using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImagingTool.Models
{
    public class Manifest
    {
        public string ManifestVersion { get; set; }
        
        // Renamed from Terminals to SystemDrivers
        public List<Terminal> SystemDrivers { get; set; }
        
        // Hardware-specific peripheral drivers
        public List<Terminal> PeripheralDrivers { get; set; }
        
        // Common drivers for all terminals
        public List<Driver> CommonDrivers { get; set; }
    }
}
