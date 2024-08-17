using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpLanguage.MissingInterface
{
    // The base class does not implement the IStorage interface.
    internal class Storage : BaseStorage, IStorage
    {
    }
}
