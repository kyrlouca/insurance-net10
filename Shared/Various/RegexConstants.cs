using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Shared.Various;

public partial class RegexConstants
{
    //R,AR,ER,AER plus four digits

    [GeneratedRegex(@"^(?:(AE)|A|E)?R\d{4}")]
    public static partial Regex RgxRow();

    //C,NC,ANC,AEC plus 4 digits
    [GeneratedRegex(@"^(?:(AN)|(AE)|N)?C\d{4}")]
    public static partial Regex RgxCol();
}
