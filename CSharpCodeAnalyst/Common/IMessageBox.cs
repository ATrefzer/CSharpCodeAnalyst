using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpCodeAnalyst.Common;

public interface IMessageBox
{
    void ShowError(string message);
}