using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;

namespace Server
{


    public partial class SGSserverForm : Form
    {

        Server server;
        public SGSserverForm()
        {

            server = new Server();
            server.SendIt += server_SendIt;
            InitializeComponent();
        }

        void server_SendIt(string message)
        {
            SetText(message);
        }

        private delegate void UICallerDelegate(string message);

        private void SetText(string message)
        {
            if (this.InvokeRequired == true)
            {
                UICallerDelegate dlg = new UICallerDelegate(SetText);
                BeginInvoke(dlg, new object[] { message });
            }
            else
            {
                this.txtLog.Text += message;
            }

        }
        private void Form1_Load(object sender, EventArgs e)
        {            
              
        }
    }


}