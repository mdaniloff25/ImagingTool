using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ImagingTool.Models
{
    public class Manifest
    {
        public string ManifestVersion { get; set; }
        public List<Terminal> Terminals { get; set; }
        public List<Driver> CommonDrivers { get; set; } // For future 'common' section
    }
}
