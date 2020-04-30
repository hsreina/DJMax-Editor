using System.Windows.Forms;

namespace DJMaxEditor.Files
{
    internal interface IFile
    {
        string GetName();

        string GetDescription();

        string GetExtension();

        Form GetSettingsForm();
    }
}
