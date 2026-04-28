using System;

namespace duzeypromosyonn.Models
{
    public class XmlUpdateStatus
    {
        public DateTime? LastSuccessAt { get; set; }
        public DateTime? LastAttemptAt { get; set; }
        public int LastProductCount { get; set; }
        public string LastError { get; set; }
        public string SourceUrl { get; set; }
    }
}
