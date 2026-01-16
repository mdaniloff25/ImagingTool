namespace ImagingTool.Models
{
    public class Driver
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Path { get; set; }
        public string InstallCmd { get; set; }
        public string UninstallCmd { get; set; }
        public bool RebootRequired { get; set; }
    }
}