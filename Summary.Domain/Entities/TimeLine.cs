using System;
using System.Collections.Generic;
using Summary.Domain.Interfaces;

namespace Summary.Domain.Entities
{
    public class TimeLine : IModifyEntity<Guid, Guid, Guid>
    {
        public Guid Id { get; set; }
        public string ProjectName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<Milestone> Milestones { get; set; } = [];
        public DateTime? ModifiedTime { get; set; }
        public Guid CreationUserId { get; set; }
        public DateTime CreationTime { get; set; }
        public Guid ModifyEntityId { get; set; }
    }

    public class Milestone : IEntity<int>
    {
        //[Key]
        public int Id { get; set; }

        public string EventName { get; set; } = string.Empty;

        public string EventDescription { get; set; } = string.Empty;

        public DateTime DeadLine { get; set; }
    }
}