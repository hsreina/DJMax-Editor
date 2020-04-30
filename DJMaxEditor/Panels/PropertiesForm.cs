using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DJMaxEditor 
{
    public partial class PropertiesForm : ToolWindow 
    {
        public PropertiesForm() 
        {
            InitializeComponent();
        }

        public object PropertyObject {
            get {
                return propertyGrid1.SelectedObject;
            }
            set {
                propertyGrid1.SelectedObject = value;
            }
        }
    }
}
