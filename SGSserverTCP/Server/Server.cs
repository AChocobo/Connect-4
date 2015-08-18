#region Usings
using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using BusinessObjectHelper;
#endregion
namespace Server
{
    #region Enums
    public enum PlayerTurn
    {
        Red,
        Black
    }

    public enum ChipColor
    {
        Black,
        Red,
        Blank
    }
    //The commands for interaction between the server and the client
    enum Command
    {
        Login,      //Log into the server
        Logout,     //Logout of the server
        Message,    //Send a text message to all the chat clients
        List,       //Get a list of users in the chat room from the server
        MakeMove,   //check for a valid move, and make one if possible
        SendMove,   //send a valid move to update both player's boards
        BeginGame,  //send players what colors they are and start the game
        Win,        //declare a win
        NewGame,    //make a new game
        Null        //No command
    }
    #endregion
    public class Server : Event
    {
        #region Global Variables / Structs
        public ManualResetEvent allDone = new ManualResetEvent(false);
        //The ClientInfo structure holds the required information about every
        //client connected to the server
        struct ClientInfo
        {
            public Socket socket;   //Socket of the client
            public string strName;  //Name by which the user logged into the chat room
        }
        //The collection of all clients logged into the room (an array of type ClientInfo)
        ArrayList clientList;
        //The main socket on which the server listens to the clients
        Socket serverSocket;
        string colorDif = string.Empty, winningCombination = string.Empty;
        byte[] byteData = new byte[1024];
        List<Thread> _threads = new List<Thread>();
        public string MoveToSend = string.Empty;
        public ChipColor[,] zonelist = new ChipColor[7, 6];
        private Boolean ValidorInvalidMove = true;
        public PlayerTurn turn = PlayerTurn.Black;
        public Socket playerRedSocket;
        public Socket playerBlackSocket;
        int newGameCount = 0;
        //column counters
        int one = 0, two = 0, three = 0, four = 0, five = 0, six = 0, seven = 0;
        #endregion
        #region Initial Connection Related
        public Server()
        {
            NewGame();
            clientList = new ArrayList();
            try
            {
                serverSocket = new Socket(AddressFamily.InterNetwork, //We are using TCP sockets
                                          SocketType.Stream,
                                          ProtocolType.Tcp);
                IPEndPoint ipEndPoint = new IPEndPoint(IPAddress.Any, 1000);//Assign the any IP of the machine and listen on port number 1000
                //Bind and listen on the given address
                serverSocket.Bind(ipEndPoint);
                serverSocket.Listen(4);
                //Accept the incoming clients
                //serverSocket.BeginAccept(new AsyncCallback(OnAccept), null);
                Thread ts = new Thread(new ThreadStart(Run));
                _threads.Add(ts);
                ts.Start();
            }
            catch (Exception ex)
            {
                RaiseEvent(ex.Message);
            }
        }
        private void NewGame()
        {
            for (int x = 0; x < zonelist.GetLength(0); ++x)
                for (int y = 0; y < zonelist.GetLength(1); ++y)
                    zonelist[x, y] = ChipColor.Blank; //setting all zones to none for initial game
            one = 0;
            two = 0;
            three = 0;
            four = 0;
            five = 0;
            six = 0;
            seven = 0;
        }
        public void Run()
        {
            do
            {
                allDone.Reset();
                //Accept the incoming clients
                serverSocket.BeginAccept(new AsyncCallback(OnAccept), null);

                allDone.WaitOne();

            } while (true);

        }
        private void OnAccept(IAsyncResult ar)
        {
            try
            {
                RaiseEvent("Someone is trying to connect");
                Socket clientSocket = serverSocket.EndAccept(ar);

                //Start listening for more clients
                serverSocket.BeginAccept(new AsyncCallback(OnAccept), null);

                //Once the client connects then start receiving the commands from her
                clientSocket.BeginReceive(byteData, 0, byteData.Length, SocketFlags.None,
                    new AsyncCallback(OnReceive), clientSocket);
            }
            catch (Exception ex)
            {
                RaiseEvent(ex.Message);
            }
        }
        #endregion
        private void OnReceive(IAsyncResult ar)
        {
            #region Local variables / converting received message
            try
            {
                Socket clientSocket = (Socket)ar.AsyncState;
                clientSocket.EndReceive(ar);
                //Transform the array of bytes received from the user into an
                //intelligent form of object Data
                Data msgReceived = new Data(byteData);
                //We will send this object in response the users request
                Data msgToSend = new Data();
                byte[] message;
                //If the message is to login, logout, or simple text message
                //then when send to others the type of the message remains the same
                msgToSend.cmdCommand = msgReceived.cmdCommand;
                msgToSend.strName = msgReceived.strName;
            #endregion
               
                
                    #region Command.NewGame
                
                if (msgReceived.cmdCommand == Command.NewGame)
                {
                    newGameCount = 0;
                    NewGame();
                    msgToSend.cmdCommand = Command.NewGame;
                    message = msgToSend.ToByte();
                            foreach (ClientInfo clientInfo in clientList)
                            {
                                if (clientInfo.socket != clientSocket ||
                                    msgToSend.cmdCommand != Command.Login)
                                {
                                    //Send the message to all users
                                    clientInfo.socket.BeginSend(message, 0, message.Length, SocketFlags.None,
                                        new AsyncCallback(OnSend), clientInfo.socket);
                                }
                            }
                            RaiseEvent("Both players have agreed to start a new game. \r\n");
                }
                    #endregion
                else
                {
                #region Make Move Command              
                if (msgReceived.cmdCommand == Command.MakeMove)//MAKE A METHOD HERE TO CHECK AN ARRAY LIST FOR VALID MOVES
                //IF IT IS A VALID MOVE THEN MAKE CHANGES TO ARRAY TO REFLECT THE MOVE THAT WAS MADE
                {
                   RaiseEvent("strMessage: " + msgReceived.strMessage + Environment.NewLine + "cmdCommand: " + msgReceived.cmdCommand.ToString() + Environment.NewLine + "strName: " + msgReceived.strName.ToString() + Environment.NewLine);               
                    msgReceived.cmdCommand = Command.Message;
                    if (CheckValidity(ColumnCheck(Convert.ToInt32(msgReceived.strMessage))))
                    {//if true, means that there is still space in the column to put a chip
                        int currentrow = RowCorrect(ColumnCheck(Convert.ToInt32(msgReceived.strMessage)));
                        int currentcolumn = Convert.ToInt32(msgReceived.strMessage) - 1;
                        MoveToSend = turn.ToString() + "," + currentcolumn + "," + currentrow;
                        if (turn == PlayerTurn.Black)
                        {
                            zonelist[currentcolumn, currentrow] = ChipColor.Black;
                            turn = PlayerTurn.Red;
                        }
                        else
                        {
                            zonelist[currentcolumn, currentrow] = ChipColor.Red;
                            turn = PlayerTurn.Black;
                        }                       
                        msgReceived.cmdCommand = Command.SendMove;
                        RowIncrease(currentcolumn);
                        if (CheckWin())
                        {
                            msgToSend.cmdCommand = Command.Win;
                            msgToSend.strMessage = MoveToSend + "," + winningCombination;
                            //make the last move of the game and announce the current player is the winner
                        }
                        else
                        {
                            if (turn == PlayerTurn.Black)
                                turn = PlayerTurn.Red;
                            else
                                turn = PlayerTurn.Black;
                            //make a command here to change the player's boards and also change current turn
                        }     
                        if (turn == PlayerTurn.Black)
                            turn = PlayerTurn.Red;
                        else
                            turn = PlayerTurn.Black;
                    }
                    else//need to add a command in here to return an invalid move message to the player
                    {
                        ValidorInvalidMove = false;
                        msgToSend.cmdCommand = Command.Message;
                        msgToSend.strMessage = "The column you selected is not a valid move. Try again.";
                    }
                }
                #endregion
                if (ValidorInvalidMove)//only relevant if player did not make a move or made a valid move
                {
                    if (msgToSend.cmdCommand != Command.Win)
                    {
                        switch (msgReceived.cmdCommand)
                        {
                            #region Login Command
                            case Command.Login:
                                //When a user logs in to the server then we add her to our
                                //list of clients
                                ClientInfo clientInfo = new ClientInfo();
                                clientInfo.socket = clientSocket;
                                clientInfo.strName = msgReceived.strName;
                                Random rnd = new Random();
                                if (clientList.Count == 0)
                                {
                                    if (rnd.Next(0, 2) == 0)
                                    {
                                        clientInfo.strName = "Black";
                                        playerBlackSocket = clientSocket;
                                    }
                                    else
                                    {
                                        clientInfo.strName = "Red";
                                        playerRedSocket = clientSocket;
                                    }
                                    colorDif = clientInfo.strName;
                                }
                                else if (clientList.Count == 1)
                                {
                                    if (colorDif == "Black")
                                    {
                                        clientInfo.strName = "Red";
                                        playerRedSocket = clientSocket;
                                    }
                                    else
                                    {
                                        clientInfo.strName = "Black";
                                        playerBlackSocket = clientSocket;
                                    }
                                    msgToSend.cmdCommand = Command.BeginGame;
                                    msgToSend.strMessage = "first";
                                    message = msgToSend.ToByte();
                                    playerBlackSocket.BeginSend(message, 0, message.Length, SocketFlags.None,
                                            new AsyncCallback(OnSend), playerBlackSocket);
                                    msgToSend.strMessage = "second";
                                    message = msgToSend.ToByte();
                                    playerRedSocket.BeginSend(message, 0, message.Length, SocketFlags.None,
                                            new AsyncCallback(OnSend), playerRedSocket);

                                    
                                msgToSend.cmdCommand = Command.List;
                                msgToSend.strName = null;
                                msgToSend.strMessage = null;
                                //Collect the names of the user in the chat room
                                foreach (ClientInfo client in clientList)
                                {
                                    //To keep things simple we use asterisk as the marker to separate the user names
                                    msgToSend.strMessage += client.strName + "*";
                                    message = msgToSend.ToByte();
                                }
                                    
                                }
                                else if(clientList.Count ==2)
                                {
                                    clientSocket.Disconnect(false);
                                    break;
                                }
                                if (clientList.Count <= 2)
                                {
                                    msgReceived.strName = clientInfo.strName;
                                    clientList.Add(clientInfo);
                                    //Set the text of the message that we will broadcast to all users
                                    msgToSend.strMessage = "<<<" + msgReceived.strName + " has joined the room>>>";
                                }
                                break;
                            #endregion
                            #region Logout Command
                            case Command.Logout:
                                //When a user wants to log out of the server then we search for her 
                                //in the list of clients and close the corresponding connection
                                int nIndex = 0;
                                foreach (ClientInfo client in clientList)
                                {
                                    if (client.socket == clientSocket)
                                    {
                                        clientList.RemoveAt(nIndex);
                                        break;
                                    }
                                    ++nIndex;
                                }
                                clientSocket.Close();
                                msgToSend.strMessage = "<<<" + msgReceived.strName + " has left the room>>>";
                                break;
                            #endregion
                            #region Message Command
                            case Command.Message:
                                //Set the text of the message that we will broadcast to all users
                                msgToSend.strMessage = msgReceived.strName + ": " + msgReceived.strMessage;
                                break;
                            #endregion
                            #region Win Command
                            case Command.Win:
                                msgToSend.cmdCommand = Command.SendMove;
                                msgToSend.strMessage = MoveToSend + "," + winningCombination;
                                message = msgToSend.ToByte();
                                foreach (ClientInfo client in clientList)
                                {
                                 
                                        if (client.socket != clientSocket ||
                                        msgToSend.cmdCommand != Command.Login)
                                    {
                                        //Send the message to all users
                                        client.socket.BeginSend(message, 0, message.Length, SocketFlags.None,
                                            new AsyncCallback(OnSend), client.socket);
                                    }
                                }
                                break;
                            #endregion
                            #region Send Moves
                            case Command.SendMove:
                                msgToSend.cmdCommand = Command.SendMove;
                                msgToSend.strMessage = MoveToSend;
                                message = msgToSend.ToByte();
                                foreach (ClientInfo client in clientList)
                                {
                                    if (client.socket != clientSocket ||
                                        msgToSend.cmdCommand != Command.Login)
                                    {
                                        //Send the message to all users
                                        client.socket.BeginSend(message, 0, message.Length, SocketFlags.None,
                                            new AsyncCallback(OnSend), client.socket);
                                    }
                                }
                                break;
                            #endregion
                            #region List Command
                            case Command.List:
                                //Send the names of all users in the chat room to the new user
                                msgToSend.cmdCommand = Command.List;
                                msgToSend.strName = null;
                                msgToSend.strMessage = null;
                                //Collect the names of the user in the chat room
                                foreach (ClientInfo client in clientList)
                                {
                                    //To keep things simple we use asterisk as the marker to separate the user names
                                    msgToSend.strMessage += client.strName + "*";
                                    message = msgToSend.ToByte();
                                    //Send the name of the users in the chat room
                                    
                                    //clientSocket.BeginSend(message, 0, message.Length, SocketFlags.None,
                                          //  new AsyncCallback(OnSend), clientSocket);
                                }
                                foreach (ClientInfo client in clientList)
                                {
                                    message = msgToSend.ToByte();
                                    client.socket.BeginSend(message, 0, message.Length, SocketFlags.None,
                                                new AsyncCallback(OnSend), client.socket);

                                }

                                break;
                            #endregion
                        }
                        #region Send message to all clients
                        if (msgToSend.cmdCommand != Command.List && msgToSend.cmdCommand != Command.SendMove && msgToSend.cmdCommand != Command.BeginGame)   //List messages are not broadcasted
                        {
                            message = msgToSend.ToByte();
                            foreach (ClientInfo clientInfo in clientList)
                            {
                                if (clientInfo.socket != clientSocket ||
                                    msgToSend.cmdCommand != Command.Login)
                                {
                                    //Send the message to all users
                                    clientInfo.socket.BeginSend(message, 0, message.Length, SocketFlags.None,
                                        new AsyncCallback(OnSend), clientInfo.socket);
                                }
                            }
                            RaiseEvent(msgToSend.strMessage + "\r\n");
                        }
                        #endregion
                    } 
                }
                #region Win / Game Over
                if (msgToSend.cmdCommand == Command.Win)
                {
                    message = msgToSend.ToByte();
                    foreach (ClientInfo clientInfo in clientList)
                    {
                        if (clientInfo.socket != clientSocket ||
                            msgToSend.cmdCommand != Command.Login)
                        {
                            //Send the message to all users
                            clientInfo.socket.BeginSend(message, 0, message.Length, SocketFlags.None,
                                new AsyncCallback(OnSend), clientInfo.socket);
                        }
                    }
                    RaiseEvent(msgToSend.strMessage + "\r\n");
                }
                #endregion
                #region Made an Invalid Move
                if(!ValidorInvalidMove)
                {
                    ValidorInvalidMove = true;
                    message = msgToSend.ToByte();
                    clientSocket.BeginSend(message, 0, message.Length, SocketFlags.None,
                                    new AsyncCallback(OnSend), clientSocket);
                    RaiseEvent(msgReceived.strName + " just made an invalid move! \r\n");
                }
                #endregion
                #region Begin Receive, Happens every time on receive
                //If the user is logging out then we need not listen from her
            }
                if (msgReceived.cmdCommand != Command.Logout)
                {
                    //Start listening to the message send by the user
                    clientSocket.BeginReceive(byteData, 0, byteData.Length, SocketFlags.None, new AsyncCallback(OnReceive), clientSocket);
                }
            }
            catch (Exception ex)
            {
                RaiseEvent(ex.Message);
            }
        }
                #endregion
        #region On Send
        public void OnSend(IAsyncResult ar)
        {
            try
            {
                Socket client = (Socket)ar.AsyncState;
                client.EndSend(ar);
            }
            catch (Exception ex)
            {
                RaiseEvent(ex.Message);
            }
        }
        #endregion
        #region Column / Row checks / Win Check
        private bool CheckValidity(int column)
        {
            if (column >= 6)
                return false;
            else
                return true;
        }
        private int RowCorrect(int row)
        {
            switch (row)
            {
                case 0:
                    return 5;
                case 1:
                    return 4;
                case 2:
                    return 3;
                case 3:
                    return 2;
                case 4:
                    return 1;
                case 5:
                    return 0;
            }
            return row;
        }
        private int ColumnCheck(int column)
        {
            switch (column)
            {
                case 1:
                    return one;
                case 2:
                    return two;
                case 3:
                    return three;
                case 4:
                    return four;
                case 5:
                    return five;
                case 6:
                    return six;
                case 7:
                    return seven;
            }
            return 0;
        }
        private void RowIncrease(int column)
        {
            switch (column)
            {
                case 0:
                    one++;
                    break;
                case 1:
                    two++;
                    break;
                case 2:
                    three++;
                    break;
                case 3:
                    four++;
                    break;
                case 4:
                    five++;
                    break;
                case 5:
                    six++;
                    break;
                case 6:
                    seven++;
                    break;
            }
        }
        private bool CheckWin()
        {
            int count = 0, win = 3, increment = 1;
            #region Vertical Win Check
            try
            {
                for (int i = 0; i < 7; i++)      //column     
                    for (int j = 0; j < 6; j++)//row
                    {
                        if (MatrixPointCheck(i, j + 1))
                            if (zonelist[i, j] == zonelist[i, j + 1] && zonelist[i, j] != ChipColor.Blank)
                                count++;
                            else
                                count = 0;
                        if (count == win)
                        {
                            winningCombination = i + "," + j + "," + i + "," + (j - 1) + "," + i + "," + (j - 2) + "," + i + "," + (j + 1);
                            return true;
                        }

                    }
            }
            catch(Exception ex)
            { RaiseEvent(ex.Message); }
            #endregion
            #region Horizontal Win Check
            count = 0;
            try
            {
                for (int i = 0; i < 7; i++) //row
                    for (int j = 0; j < 6; j++)//column 
                    {
                        if (MatrixPointCheck(i + 1, j))
                        {
                            if (zonelist[i, j] == zonelist[i + 1, j] && zonelist[i, j] != ChipColor.Blank)
                                do
                                {
                                    if (MatrixPointCheck(i + increment, j))
                                        if (zonelist[i, j] == zonelist[i + increment, j] && zonelist[i, j] != ChipColor.Blank)
                                        {
                                            count++;
                                            increment++;
                                            if (count == 3)
                                            {
                                                winningCombination = i + "," + j + "," + (i + 1) + "," + j + "," + (i + 2) + "," + j + "," + (i + 3) + "," + j;
                                                return true;
                                            }
                                        }
                                        else
                                            break;
                                } while (MatrixPointCheck(i + increment, j));
                            increment = 1;
                            count = 0;
                        }
                        count = 0;
                        increment = 1;
                    }
            }
            catch(Exception ex)
            { RaiseEvent(ex.Message); }
            #endregion
            #region Diagonals Win check
            //diagonal right check
            increment = 1;
            count = 0;
            try
            {
                for (int i = 0; i < 7; i++) //row
                    for (int j = 0; j < 6; j++)//column NEED TO CHECK THE LOGIC OF THE DO WHILE LOOP HERE ALSO
                    {
                        if (MatrixPointCheck(i+1 , j -1))
                        {
                            if (zonelist[i, j] == zonelist[i + 1, j - 1] && zonelist[i, j] != ChipColor.Blank)
                                do
                                {
                                    if (MatrixPointCheck(i + increment, j - increment))
                                    {
                                        if (zonelist[i, j] == zonelist[i + increment, j - increment] && zonelist[i, j] != ChipColor.Blank)
                                        {
                                            count++;
                                            increment++;
                                            if (count == 3)
                                            {
                                                winningCombination = i + "," + j + "," + (i + 1) + "," + (j - 1) + "," + (i + 2) + "," + (j - 2) + "," + (i + 3) + "," + (j - 3);
                                                return true;
                                            }
                                        }
                                        else
                                        { break; }
                                    }
                                } while (MatrixPointCheck(i + increment, j - increment));
                                    //while (zonelist[i, j] == zonelist[i + increment, j + increment] && zonelist[i + increment, j + increment] != ChipColor.Blank);
                            increment = 1;
                            count = 0;
                        }
                        count = 0;
                        increment = 1;
                    }
            }
            catch(Exception ex)
            {
                RaiseEvent(ex.Message);
            }
            //diagonal left check
            increment = 1;
            count = 0;
            try
            {
                for (int i = 0; i < 7; i++) //row
                    for (int j = 0; j < 6; j++)//column NEED TO CHECK THE LOGIC OF THE DO WHILE LOOP HERE ALSO
                    {
                        if (MatrixPointCheck(i - 1, j - 1))
                        {
                            if (zonelist[i, j] == zonelist[i - 1, j - 1] && zonelist[i, j] != ChipColor.Blank)
                                do
                                {
                                    if (MatrixPointCheck(i - increment, j - increment))
                                    {
                                        if (zonelist[i, j] == zonelist[i - increment, j - increment] && zonelist[i, j] != ChipColor.Blank)
                                        {
                                            count++;
                                            increment++;
                                            if (count == 3)
                                            {
                                                winningCombination = i + "," + j + "," + (i - 1) + "," + (j - 1) + "," + (i - 2) + "," + (j - 2) + "," + (i - 3) + "," + (j - 3);
                                                return true;
                                            }
                                        }
                                        else
                                        { break; }
                                    }
                                } while (MatrixPointCheck(i - increment, j - increment));
                            increment = 1;
                            count = 0;
                        }
                        count = 0;
                        increment = 1;
                    }
            }
            catch(Exception ex)
            {
                RaiseEvent(ex.Message);
            }
            #endregion
            return false;
        }
        #endregion
        private void AddtoList()
        { 

        }
        #region Check Valid Position Method
        private bool MatrixPointCheck(int x, int y)
        {
            return x >= 0 && x < 7 && y >= 0 && y < 6;
        }   
    }
        #endregion
   
    #region Data Structure / To Byte
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
            //The first four bytes are for the Command
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
        public string strName;      //Name by which the client logs into the room
        public string strMessage;   //Message text
        public Command cmdCommand;  //Command type (login, logout, send message, etcetera)
    }
}
    #endregion