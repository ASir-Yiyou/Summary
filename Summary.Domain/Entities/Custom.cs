using System;
using Summary.Domain.Interfaces;

namespace Summary.Domain.Entities
{
    public class Custom : IEntity<Guid>, ISoftDelete
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Age { get; set; }
        public int Level { get; set; }
        public bool IsDeleted { get; set; } = false;
    }
}