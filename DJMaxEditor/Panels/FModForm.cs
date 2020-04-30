using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;

namespace DJMaxEditor.Panels 
{
    public partial class FModForm : DockContent 
    {
        public class DebugInfo : INotifyPropertyChanged 
        {
            public event PropertyChangedEventHandler PropertyChanged;

            public string Key {
                get {
                    return _key;
                }
                set {
                    _key = value;
                    NotifyPropertyChanged("Value");
                } 
            }

            public string Value {
                get {
                    return _value;
                }
                set {
                    _value = value;
                    NotifyPropertyChanged("Value");
                }
            }

            private string _value;

            private string _key;

            private void NotifyPropertyChanged(String propertyName = "") 
            {
                if (PropertyChanged != null) {
                    PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
            }

        }

        public BindingList<DebugInfo> debugInfo = new BindingList<DebugInfo>();

        public FModForm() 
        {
            InitializeComponent();
            debugInfo.Add(_dsp);
            debugInfo.Add(_geometry);
            debugInfo.Add(_stream);
            debugInfo.Add(_update);
            debugInfo.Add(_total);
            DebugInfoList.DataSource = debugInfo;
        }

        public void UpdateDebugInfo(object debugInfo) 
        {
            if (debugInfo == null) { return; }

            if (debugInfo is AudioPlayerFmodExDebugInfo) {
                AudioPlayerFmodExDebugInfo debug = (AudioPlayerFmodExDebugInfo)debugInfo;
                _dsp.Value = debug.Dsp + "";
                _geometry.Value = debug.Geometry + "";
                _stream.Value = debug.Stream + "";
                _update.Value = debug.Update + "";
                _total.Value = debug.Total + "";
            }
        }

        private DebugInfo _dsp = new DebugInfo() { Key = "DSP", Value = "0" };

        private DebugInfo _geometry = new DebugInfo() { Key = "Geometry", Value = "0" };

        private DebugInfo _stream = new DebugInfo() { Key = "Stream", Value = "0" };

        private DebugInfo _update = new DebugInfo() { Key = "Update", Value = "0" };

        private DebugInfo _total = new DebugInfo() { Key = "Total", Value = "0" };
    }
}
