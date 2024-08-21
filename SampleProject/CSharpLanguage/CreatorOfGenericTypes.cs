using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpLanguage
{
    internal class ProjectFile :  XmlFile<Project>
    {

    }


    internal class CreatorOfGenericTypes
    {
        ProjectFile _file;

        void Create()
        {
            var x = new List<Project>();
            var y = new XmlFile<Project>();

          
        }
    }
}
