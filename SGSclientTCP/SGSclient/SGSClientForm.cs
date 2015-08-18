#region Usings
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using Microsoft.VisualBasic.PowerPacks;

#endregion
namespace SGSclient
{
    #region Enumns
    //The commands for interaction between the server and the client
    enum Command
    {
        Login,      //Log into the server
        Logout,     //Logout of the server
        Message,    //Send a text message to all the chat clients
        List,       //Get a list of users in the chat room from the server
        MakeMove,   //make a move if possible  
        SendMove,   //update the player's board
        BeginGame,  //send players what colors they are and start the game
        Win,        //declare a win/make new game
        NewGame,    //make a new game
        Null        //No command
    }
    #endregion
    public partial class SGSClient : Form
    {
        #region Global Variables
        public Socket clientSocket; //The main client socket
        public string strName, colorDrop, myColor;  
        private byte[] byteData = new byte[1024];
        private OvalShape[,] zonelist = new OvalShape[7, 6];
        string[] MoveToMake;
        private bool currentTurn, makeNewGame = false;
        private int dropCount = 0, count =2;
        private System.Timers.Timer _timer;
        private bool boot = false;
        private string[] namehold;
        #endregion
        #region Initial Loading sequences
        public SGSClient()
        {
            InitializeComponent();
            PlaceOvals(zonelist);
            this.txtChatBox.TextChanged += new EventHandler(txtChatBox_TextChanged);            
        }

        void txtChatBox_TextChanged(object sender, EventArgs e)
        {
            this.txtChatBox.SelectionStart = this.txtChatBox.Text.Length;
            this.txtChatBox.ScrollToCaret();
        }
        #endregion
        #region Events - Column Arrow Creation
        void button1_EnabledChanged(object sender, EventArgs e)
        {
        
        }      
       
        #endregion
        #region Send Chat Button Click event
        //Broadcast the message typed by the user to everyone
        private void btnSend_Click(object sender, EventArgs e)
        {
            try
            {
                //Fill the info for the message to be send
                Data msgToSend = new Data();               
                msgToSend.strName = strName;
                msgToSend.strMessage = txtMessage.Text;
                msgToSend.cmdCommand = Command.Message;
                byte [] byteData = msgToSend.ToByte();
                //Send it to the server
                clientSocket.BeginSend (byteData, 0, byteData.Length, SocketFlags.None, new AsyncCallback(OnSend), null);
                txtMessage.Text = null;
            }
            catch (Exception)
            {
                MessageBox.Show("Unable to send message to the server.", "SGSclientTCP: " + strName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }  
        }
        #endregion
        #region On Send Event
        private void OnSend(IAsyncResult ar)
        {
            try
            {
                clientSocket.EndSend(ar);
            }
            catch (ObjectDisposedException)
            { }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "SGSclientTCP: " + strName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion
        #region Place Ovals/New Game
        private void PlaceOvals(OvalShape[,] zone)
        {
            var ovalShapes = new Microsoft.VisualBasic.PowerPacks.ShapeContainer()
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0),
                Padding = new Padding(0),
            };
            for (int x = 0; x < 7; x++)
            {            
                for (int y = 0; y < 6; y++)
                {
                    OvalShape ovl = new OvalShape();
                    ovl.Width = 20;
                    ovl.Height = 20;
                    
                    ovl.FillStyle = FillStyle.Solid;
                    ovl.FillColor = Color.White;
                    ovl.BorderWidth = 0;
                    ovl.Location = new Point(521 + (x * 34), 71 + (y * 24));
                    ovl.Visible = true;
                    ovalShapes.Shapes.Add(ovl);
                    zone[x, y] = ovl;                  
                }
            }
            this.Controls.Add(ovalShapes);
        }

        private void NewGame(OvalShape[,] zone)
        {
            foreach (OvalShape o in zone)
                o.FillColor = Color.White;
        }
        #endregion
        private void OnReceive(IAsyncResult ar)
        {
            try
            {
                clientSocket.EndReceive(ar);
                Data msgReceived = new Data(byteData);
                //Accordingly process the message received
                switch (msgReceived.cmdCommand)
                {
                    #region Commands Login, Logout, Message
                    case Command.Login:
                        lstChatters.Items.Add(msgReceived.strName);
                        break;
                    case Command.Logout:
                        lstChatters.Items.Remove(msgReceived.strName);
                        break;
                    case Command.Message:
                        break;
                    #endregion
                    #region Command.BeginGame
                    case Command.BeginGame:
                        if (msgReceived.strMessage == "first")
                        {
                            this.txtChatBox.Text += "Server: You get to move first (Black). \r\n";
                            
                            myColor = "Black";
                        }
                        else
                        {
                            ChangeTurns();
                            this.txtChatBox.Text += "Server: You will move second (Red). \r\n";
                            myColor = "Red";
                        }
                        Data msgToSend = new Data();               
                msgToSend.strName = strName;
                msgToSend.strMessage = txtMessage.Text;
                msgToSend.cmdCommand = Command.List;
                byte [] stuff = msgToSend.ToByte();
                //Send it to the server
                clientSocket.BeginSend(stuff, 0, stuff.Length, SocketFlags.None, new AsyncCallback(OnSend), null);
                        break;
                    #endregion
                    #region Command.NewGame
                    case Command.NewGame:
                        if (myColor == "Black")
                        {
                            this.txtChatBox.Text += "You will move first for the new game. \r\n";
                            if (button2.Enabled == false)
                                DisableMoveButtons();
                            currentTurn = true;
                        }
                        else
                        {
                            this.txtChatBox.Text += "You willl move second for the new game. \r\n";
                            if (button2.Enabled == true)
                                DisableMoveButtons();
                            currentTurn = false;
                        }
                        requestNewGame();
                        break;

                    #endregion
                    #region Command.SendMove
                    case Command.SendMove:
                        MoveToMake = msgReceived.strMessage.Split(',');
                        colorDrop = MoveToMake[0];
                        _timer = new System.Timers.Timer();
                        _timer.Interval = 100;
                        _timer.Elapsed += new System.Timers.ElapsedEventHandler(_timer_Elapsed);
                        _timer.Enabled = true;
                        _timer.Start();
                        txtChatBox.Text += MoveToMake[0] + " Made a move at [" + MoveToMake[1] + "," + MoveToMake[2] + "]." + "\r\n";
                        ChangeTurns();
                        break;
                    #endregion
                    #region Command.Win
                    case Command.Win:
                        MoveToMake = msgReceived.strMessage.Split(',');
                        if (MoveToMake[0] == "Black")
                        {
                            zonelist[Convert.ToInt32(MoveToMake[1]), Convert.ToInt32(MoveToMake[2])].FillColor = Color.Black;
                        }
                        else
                        {
                            zonelist[Convert.ToInt32(MoveToMake[1]), Convert.ToInt32(MoveToMake[2])].FillColor = Color.Red;
                        }
                        txtChatBox.Text += MoveToMake[0] + " Made a Winning move at [" + MoveToMake[1] + "," + MoveToMake[2] + "]." + "\r\n";
                        MessageBox.Show(MoveToMake[0] + " Made a Winning move at[" + MoveToMake[1] + "," + MoveToMake[2] + "]!");
                        if (button2.Enabled == true)
                            ChangeTurns();
                        txtChatBox.Text += "Press the 'New Game' button to initiate a new game. \r\n";
                        btnNewGame.Visible = true;
                        WinningColors();
                        break;
                    #endregion
                    #region Command.List
                    case Command.List:
                        lstChatters.Items.Clear();
                        namehold = msgReceived.strMessage.Split('*');
                        foreach (string s in namehold)
                        {
                            lstChatters.Items.Add(s);
                        }
                        break;
                    #endregion
                }
                #region Begin receive, happens every time on receive happens
                if (msgReceived.strMessage != null && msgReceived.cmdCommand != Command.List && msgReceived.cmdCommand != Command.SendMove && msgReceived.cmdCommand != Command.BeginGame)
                    txtChatBox.Text += msgReceived.strMessage + "\r\n";
                byteData = new byte[1024];
                clientSocket.BeginReceive(byteData,
                                          0,
                                          byteData.Length,
                                          SocketFlags.None,
                                          new AsyncCallback(OnReceive),
                                          null);

            }
            catch (ObjectDisposedException)
            { }
            catch (System.ArgumentNullException)
            {
                MessageBox.Show("There are already two players in this game.");
                boot = true;
                Application.Exit();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "SGSclientTCP: " + strName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void WinningColors()
        {
            zonelist[Convert.ToInt32(MoveToMake[3]), Convert.ToInt32(MoveToMake[4])].FillColor = Color.Gold;
            zonelist[Convert.ToInt32(MoveToMake[5]), Convert.ToInt32(MoveToMake[6])].FillColor = Color.Gold;
            zonelist[Convert.ToInt32(MoveToMake[7]), Convert.ToInt32(MoveToMake[8])].FillColor = Color.Gold;
            zonelist[Convert.ToInt32(MoveToMake[9]), Convert.ToInt32(MoveToMake[10])].FillColor = Color.Gold;             
        }           
                #endregion

        #region Timer Elasped Event
        void _timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (Convert.ToInt32(MoveToMake[2]) == dropCount)
            {
                _timer.Stop();
                count = 2;
                dropCount = 0;
                if (MoveToMake[0] == "Black")
                {
                    zonelist[Convert.ToInt32(MoveToMake[1]), Convert.ToInt32(MoveToMake[2])].FillColor = Color.Black;
                }
                else
                {
                    zonelist[Convert.ToInt32(MoveToMake[1]), Convert.ToInt32(MoveToMake[2])].FillColor = Color.Red;
                }
                if (Convert.ToInt32(MoveToMake[2]) != 0)
                {
                    zonelist[Convert.ToInt32(MoveToMake[1]), Convert.ToInt32(MoveToMake[2]) - 1].FillColor = Color.White;
                }
                if (makeNewGame == true)
                {
                    makeNewGame = false;
                    this.btnNewGame.Visible = true;
                    this.btnNewGame.Enabled = true;
                }
            }
            else
            {
                if (MoveToMake[0] == "Black")
                {
                    zonelist[Convert.ToInt32(MoveToMake[1]), dropCount].FillColor = Color.Black;
                }
                else
                {
                    zonelist[Convert.ToInt32(MoveToMake[1]), dropCount].FillColor = Color.Red;
                }
                if (dropCount == 0)
                { }
                else
                    zonelist[Convert.ToInt32(MoveToMake[1]), dropCount - 1].FillColor = Color.White;
                dropCount++;
                count++;
            }
        }
        #endregion
        #region Change Turns method
        private void ChangeTurns()
        {
            DisableMoveButtons();
            currentTurn = !currentTurn;
        }
        #endregion
        #region Form Load
        private void Form1_Load(object sender, EventArgs e)
        {           
            this.Text = "SGSclientTCP: " + strName;            
            //The user has logged into the system so we now request the server to send
            //the names of all users who are in the chat room
            Data msgToSend = new Data ();
            msgToSend.cmdCommand = Command.List;
            msgToSend.strName = strName;
            msgToSend.strMessage = null;
            
            byteData = msgToSend.ToByte();

            clientSocket.BeginSend(byteData, 0, byteData.Length, SocketFlags.None, new AsyncCallback(OnSend), null);
            
            byteData = new byte[1024];
            //Start listening to the data asynchronously
            clientSocket.BeginReceive(byteData,
                                       0, 
                                       byteData.Length,
                                       SocketFlags.None,
                                       new AsyncCallback(OnReceive),
                                       null);

        }
        #endregion
        #region Misc. Events
        private void txtMessage_TextChanged(object sender, EventArgs e)
        {
            if (txtMessage.Text.Length == 0)
                btnSend.Enabled = false;
            else
                btnSend.Enabled = true;
        }

        private void SGSClient_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (boot == false) 
            if (MessageBox.Show("Are you sure you want to leave the chat room?", "SGSclient: " + strName,
                MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.No)
            {
                e.Cancel = true;
                return;
            }

            try
            {
                //Send a message to logout of the server
                Data msgToSend = new Data ();
                msgToSend.cmdCommand = Command.Logout;
                msgToSend.strName = strName;
                msgToSend.strMessage = null;

                byte[] b = msgToSend.ToByte ();
                clientSocket.Send(b, 0, b.Length, SocketFlags.None);
                clientSocket.Close();
            }
            catch (ObjectDisposedException)
            { }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "SGSclientTCP: " + strName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void txtMessage_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                btnSend_Click(sender, null);
            }
        }
        #endregion
        #region Send Message Button Click
        private void button1_Click_1(object sender, EventArgs e)
        {
            try
            {
                //Fill the info for the message to be send
                Data msgToSend = new Data();
                msgToSend.strName = strName;
                msgToSend.strMessage = ((Button)sender).Text;
                msgToSend.cmdCommand = Command.MakeMove;
                byte[] byteData = msgToSend.ToByte();
                //Send it to the server
                clientSocket.BeginSend(byteData, 0, byteData.Length, SocketFlags.None, new AsyncCallback(OnSend), null);
                txtMessage.Text = null;
            }
            catch (Exception)
            {
                MessageBox.Show("Unable to send message to the server.", "SGSclientTCP: " + strName, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }                   
        }
        #endregion
        private void requestNewGame()
        {
            foreach (OvalShape o in zonelist)
            {
                o.FillColor = Color.White;
            }
            btnNewGame.Visible = false;
        }

        #region New Game Button Click
        private void btnNewGame_Click(object sender, EventArgs e)
        {
            
            Data msgToSend = new Data();
            msgToSend.strName = strName;
            msgToSend.strMessage = null;
            msgToSend.cmdCommand = Command.NewGame;
            byte[] byteData = msgToSend.ToByte();
            //Send it to the server
            clientSocket.BeginSend(byteData, 0, byteData.Length, SocketFlags.None, new AsyncCallback(OnSend), null);
            txtMessage.Text = null;
            
        }
        #endregion       
        private void DisableMoveButtons()
        {
            this.button2.Enabled = !button2.Enabled;
            this.button3.Enabled = !button3.Enabled;
            this.button4.Enabled = !button4.Enabled;
            this.button5.Enabled = !button5.Enabled;
            this.button6.Enabled = !button6.Enabled;
            this.button7.Enabled = !button7.Enabled;
            this.button8.Enabled = !button8.Enabled;

        }



    }      
    #region Data Structure / To Byte Methods
    //The data structure by which the server and the client interact with 
    //each other
    class Data
    {
        //Default constructor
        public Data()
        {
            this.cmdCommand = Command.Null;
            this.strMessage = null;
            this.strName = null;
        }
        //Converts the bytes into an object of type Data
        public Data(byte[] data)
        {
            this.cmdCommand = (Command)BitConverter.ToInt32(data, 0);
            //The next four store the length of the name
            int nameLen = BitConverter.ToInt32(data, 4);
            //The next four store the length of the message
            int msgLen = BitConverter.ToInt32(data, 8);
            //This check makes sure that strName has been passed in the array of bytes
            if (nameLen > 0)
                this.strName = Encoding.UTF8.GetString(data, 12, nameLen);
            else
                this.strName = null;
            //This checks for a null message field
            if (msgLen > 0)
                this.strMessage = Encoding.UTF8.GetString(data, 12 + nameLen, msgLen);
            else
                this.strMessage = null;
        }

        //Converts the Data structure into an array of bytes
        public byte[] ToByte()
        {
            List<byte> result = new List<byte>();

            //First four are for the Command
            result.AddRange(BitConverter.GetBytes((int)cmdCommand));

            //Add the length of the name
            if (strName != null)
                result.AddRange(BitConverter.GetBytes(strName.Length));
            else
                result.AddRange(BitConverter.GetBytes(0));

            //Length of the message
            if (strMessage != null)
                result.AddRange(BitConverter.GetBytes(strMessage.Length));
            else
                result.AddRange(BitConverter.GetBytes(0));

            //Add the name
            if (strName != null)
                result.AddRange(Encoding.UTF8.GetBytes(strName));

            //And, lastly we add the message text to our array of bytes
            if (strMessage != null)
                result.AddRange(Encoding.UTF8.GetBytes(strMessage));

            return result.ToArray();
        }
    #endregion
        #region Important Globals
        public string strName;      //Name by which the client logs into the room
        public string strMessage;   //Message text
        public Command cmdCommand;  //Command type (login, logout, send message, etcetera)
        #endregion
    }
}