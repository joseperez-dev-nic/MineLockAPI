using System;

namespace RampaSegura.Api.Models.Requests
{
    public class SessionCloseRequest
    {
        public long PersonId { get; set; } 
        public DateTime? ExitTime { get; set; }
    }
}