using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImagingTool.Models
{
    public static class ManifestLoader
    {
        public static Manifest LoadManifest(string filePath)
        {
            string json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<Manifest>(json);
        }
    }
}
