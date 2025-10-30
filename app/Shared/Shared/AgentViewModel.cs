using System;

namespace MinimalApi.Models
{
    public class AgentViewModel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Instructions { get; set; }
        public string Description { get; set; }
        public string Model { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
