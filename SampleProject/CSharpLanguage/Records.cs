using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpLanguage
{
    internal record RecordA
    {
        RecordB _recordB;
    }

    internal record RecordB
    {
        RecordA _recordA;
    }
}
