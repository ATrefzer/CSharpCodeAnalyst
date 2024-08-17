using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

namespace ModuleLevel2
{
    public sealed class SelfReferencingClass
    {
        public SelfReferencingClass(string commitHash)
        {
            CommitHash = commitHash;
        }


        public object Commit { get; set; }

        
        public string CommitHash { get; }


        public List<SelfReferencingClass> Parents { get; } = new List<SelfReferencingClass>();
        public List<SelfReferencingClass> Children { get; } = new List<SelfReferencingClass>();

    }
}
