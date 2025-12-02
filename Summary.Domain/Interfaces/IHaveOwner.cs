using System;
using System.Collections.Generic;
using System.Text;

namespace Summary.Domain.Interfaces
{
    public interface IHaveOwner<T>
    {
        T CreatorId { get; set; }
    }
}
