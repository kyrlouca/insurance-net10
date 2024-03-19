using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared.Various;
public class StringRoutines
{
    public static string JoinStringCreate(IReadOnlyList<string> strings, string separator = null)
    {
        //use this routine in place of the build in join because it is too slow        
        int totalSize = 0;
        for (int i = 0; i < strings.Count; i++)
            totalSize += strings[i].Length;

        if (!string.IsNullOrEmpty(separator))
            totalSize += (separator.Length * strings.Count - 1);

        //construct the resulting string
        return string.Create(totalSize, (strings, separator), (chars, state) =>
        {
            /*
            note that 'chars' parameter of the lambda
            is the Span<char> that is in fact a pointer to newly allocated string
            */
            var offset = 0;

            var separatorSpan = state.separator.AsSpan();
            for (int i = 0; i < state.strings.Count; i++)
            {
                var currentStr = state.strings[i];
                currentStr.AsSpan().CopyTo(chars.Slice(offset));
                offset += currentStr.Length;
                if (!string.IsNullOrEmpty(state.separator) && i < state.strings.Count - 1)
                {
                    separatorSpan.CopyTo(chars.Slice(offset));
                    offset += state.separator.Length;
                }
            }
        });

    }
}