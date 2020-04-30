using DJMaxEditor.DJMax;

namespace DJMaxEditor.Files
{
    interface ISaveFile : IFile
    {
        bool Save(string filename, PlayerData playerData);
    }
}
