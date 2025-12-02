using System;
using System.Collections.Generic;
using System.Text;

namespace Summary.Domain.Enums
{
    public enum DataScope
    {
        Self = 0,               // 仅自己
        Group = 10,             // 本组
        GroupAndChildren = 20,  // 本组及子组
        Tenant = 30             // 全部 (当前租户下)
    }
}
