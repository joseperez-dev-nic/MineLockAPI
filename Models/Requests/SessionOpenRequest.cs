using System;

namespace MineLock.Api.Models.Requests
{
    public class SessionOpenRequest
    {
        public long PersonId { get; set; }
        public int LevelId { get; set; }
 
        public DateTime? EntryTime { get; set; }
    }
}