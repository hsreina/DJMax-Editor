using DJMaxEditor.DJMax;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace DJMaxEditor.Panels 
{
    public partial class EventsListForm : ToolWindow 
    {        public EventsListForm ()
        {
            InitializeComponent();
            List.DataSource = _dataProxy;
        }

        public void SetPlayerData(PlayerData playerData) 
        {
            _playerData = playerData;
            _dataProxy.SetPlayerData(playerData);
        }

        public DataGridView List {
            get {
                return EventsList;
            }
            set {
                EventsList = value;
            }
        }

        private PlayerData _playerData;

        private PlayerEventsListProxy _dataProxy = new PlayerEventsListProxy();
    }
}
