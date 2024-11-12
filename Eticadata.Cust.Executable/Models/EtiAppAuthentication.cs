namespace Eticadata.Cust.Executable.Helpers
{
    public class EtiAppAuthentication
    {
        public string EtiServerURL { get; set; }
        public string SQLServerName { get; set; }
        public string SQLUser { get; set; }
        public string SQLPassword { get; set; }
        public string SystemDatabase { get; set; }
        public string EtiLogin { get; set; }
        public string EtiPassword { get; set; }
        public string EtiCompany { get; set; }
        public string FiscalYearCode { get; set; }
        public string SectionCode { get; set; }
        public string Language { get; set; } = "pt-PT";
    }
}
