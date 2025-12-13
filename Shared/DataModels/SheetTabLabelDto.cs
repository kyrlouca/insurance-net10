using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.DataModels;

public class SheetTabLabelDto
{
    public int SheetTablLabelId { get; set; }

    public int MemberId { get; set; }

    public string MemberXBRLCode { get; set; } = string.Empty;

    public string MemberLabel { get; set; } = string.Empty;

    public string HierarchyNodeLabel { get; set; } = string.Empty;

    public string ShortLabel { get; set; } = string.Empty;
}
