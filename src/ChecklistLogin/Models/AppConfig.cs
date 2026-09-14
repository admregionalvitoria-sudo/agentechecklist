namespace ChecklistLogin.Models
{
    public class AppConfig
    {
        public string LogFolderPath { get; set; } = @"C:\Logs\Checklist";
        public string Location { get; set; } = "PORTO";
        public string GoogleWebhookUrl { get; set; } = "https://script.google.com/macros/s/AKfycbyvVnnAmbv_zVtjBilNd8qu5S4LWfN_K6QZga-aE5j3UKs3NOmSBHn1SKjaCCOeSrpA/exec";
    }
}


