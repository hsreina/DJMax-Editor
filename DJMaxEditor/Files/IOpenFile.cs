using DJMaxEditor.DJMax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DJMaxEditor.Files
{
    interface IOpenFile : IFile
    {
        bool Open(string filename, out PlayerData playerData);
    }
}
