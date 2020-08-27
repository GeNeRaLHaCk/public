using System;
using System.Runtime.InteropServices;
using System.Net;
using System.Windows.Forms;
using TS2TLB;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using COMLibr;
using System.Net.Sockets;
using System.Threading;
using System.Runtime.Serialization;

namespace COMLibrBAS
{
    [ComVisible(false)]
    public class WndWrapper : IWin32Window
    {
        private IntPtr _hwnd;
        public WndWrapper(IntPtr handle) { _hwnd = handle; }
        public IntPtr Handle
        {
            get { return _hwnd; }
        }
    }
    [Guid("9B3C11DF-BB31-4128-927B-E6E307B0FCFE")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComSourceInterfaces(typeof(ITS2DeviceEvents))]
    [ComDefaultInterface(typeof(ITS2Device))]
    [ComVisible(true)]
    [Serializable]
    public class DriverClass : IBuildSetupString, ITS2Device
    {
        public System.ComponentModel.BackgroundWorker backgroundWorker = 
            new System.ComponentModel.BackgroundWorker();
        public System.ComponentModel.BackgroundWorker backgroundWorker1 =
            new System.ComponentModel.BackgroundWorker();
        //public System.ComponentModel.BackgroundWorker backgroundCards =
        //    new System.ComponentModel.BackgroundWorker();
        private bool _active = false;
        private System.Windows.Forms.Timer _timerWD;
        private System.Windows.Forms.Timer _timerAuth;
        public HttpWebRequest request;
        public HttpWebResponse response;
        public string token;
        public int countItemsDoorFirst;
        public int countItemsDoorSecond;
        public int countItemsDoorAll;
        public int countItemsDoor0andAll;
        public string countItemsDoor1andAll;
        public string NameOfDevice;
        public string countItemsOfCardsBASMAP;
        public string countItemsOfCardsBASDevice;
        public string countPagesOfCards;
        public string countItemsOfJournal;
        public string countPagesOfJournal;
        List<ListItem> listOfCards = new List<ListItem>();
        List<string> cards = new List<string>();
        public string[] partsOfLine;
        public Form1 form;
        public TcpListener _listner;
        public Socket _sock;
        public string firstMessage = null;
        public string api = null;
        public string firmWare = null;
        public string deviceModel = null;
        public DriverClass()
        {
            _timerWD = new System.Windows.Forms.Timer
            {
                Interval = 5000,
                Enabled = false
            };//Watch Dog, чтобы драйвер подавал признак жизни 
            _timerAuth = new System.Windows.Forms.Timer
            {
                Interval = 600000,
                Enabled = false
            };//таймер для реавторизации 
            _timerWD.Tick += new EventHandler(_timer_Tick);
            _timerAuth.Tick += new EventHandler(_timer_Auth);
            backgroundWorker.DoWork += new System.ComponentModel.DoWorkEventHandler(_DoWork);
            backgroundWorker.RunWorkerCompleted +=
            new System.ComponentModel.RunWorkerCompletedEventHandler(_WorkCompleteReauth);
        }
        ~DriverClass()
        {
            GC.Collect();
        }

        public void _DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {//ассинхронное выполнение реавторизации 
            SetParametersOfCommand(SetupString, "auth");
            string passMD5 = Encrypt(passwordPart);
            AddDebugRow(DateTime.Now.ToString() + "." +
            DateTime.Now.Millisecond.ToString() + " Start ReAuthorization " + ipPart + ":" +
                    portPart + " | " + loginPart,
                true, "_log");
            Auth(ipPart, portPart, loginPart, passMD5);
        }

        private void ServerHandler(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            try
            {
                AddDebugRow("Start ServerHandler \r\n", true, "_log");
                _listner = new TcpListener(IPAddress.Any, 0);
                _listner.Start();
                _sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            }
            catch (SocketException ex)
            {
                AddDebugRow("ERR SocketInitializationEX: " + ex.Message + "\r\n", true, "dumphttp");
            }
            firstMessage = null;
            string endPoint = _listner.LocalEndpoint.ToString();
            string[] partsOfPoint = endPoint.Split(':');
            string port = partsOfPoint[1];
            string Host = Dns.GetHostName();
            string IP = Dns.GetHostByName(Host).AddressList[0].ToString();

            Remote remote = new Remote
            {
                link_url = IP + ":" + port,
                link_enable = true,
                link_password = "123456",
                realtime_logging = true,
                heartbeat = true
            };

            string jsonDes = JsonConvert.SerializeObject(remote);
            string Uri = "http://" + ipPart + ":" + portPart +
                "/api/v0/network/management/link";
            string receiveData = SendQuery(Uri, token, "POST", jsonDes);
            try
            {
                _sock = _listner.AcceptSocket();
            }
            catch (InvalidOperationException ex)
            {
                AddDebugRow("ERR FAccept SocketEX:" + ex.Message + "\r\n", true, "dumphttp");
            }
            catch (Exception ex)
            {
                AddDebugRow("ERR FAccept Unexpected error:" + ex.Message + "\r\n", true, "dumphttp");
            }
            string jsonSer;
            string Str = "HTTP/1.1 200 OK\r\nContent-Length:0\r\nContent-type: text/html\r\n\r\n";
            byte[] bufferResponse = Encoding.ASCII.GetBytes(Str);
            string _headerPong = "POST /api/v0/devices/pong";
            string _headerLogs = "PUT /api/v0/devices/logs";
            AddDebugRow("Server started for" + ipPart + " - Listening on port:" + port + "\r\n" +
            "Socket LocalEndPoint|RemoteEndPoint(User):" + _sock.LocalEndPoint.ToString() + "|" +
            _sock.RemoteEndPoint + "\r\n" +
            "Socket Available: " + _sock.Available + "\r\n" +
            "Socket Connected: " + _sock.Connected + "\r\n" +
            "Socket Send|Receive Timeout: " + _sock.SendTimeout + "|" + _sock.ReceiveTimeout + "\r\n",
            true, "dumphttp");
            if (_sock.Connected)
            {
                try
                {
                    while (true)
                    {
                        byte[] bytes = new byte[4096];
                        int bytesRec = _sock.Receive(bytes);
                        
                        //AddDebugRow(bytesRec + "\r\n", true, "dumphttp");

                        string _message = Encoding.UTF8.GetString(bytes, 0, bytesRec).Trim();
                        firstMessage += _message;
                        try
                        {
                            if (_message.Contains("}}]}"))
                            {
                                _sock.Send(bufferResponse);
                                break;
                            }
                            _sock.Send(bufferResponse);
                        }
                        catch (SocketException socketEx)
                        {
                            AddDebugRow("ERR SocketFSendEX:" + socketEx.Message + "\r\n", true, "dumphttp");
                        }
                        catch (ArgumentNullException nullEx)
                        {
                            AddDebugRow("ERR ArgumentNullFSend:" + nullEx.Message + "\r\n", true, "dumphttp");
                        }
                        catch (ObjectDisposedException objEx)
                        {
                            AddDebugRow("ERR ObjectDisposedFSend:" + objEx.Message + "\r\n", true, "dumphttp");
                        }
                        catch (Exception ex)
                        {
                            AddDebugRow("ERR FSendUnexpected error:" + ex.Message + "\r\n", true, "dumphttp");
                        }
                    }
                }
                catch (SocketException socketEx)
                {
                    AddDebugRow("ERR SocketEX:" + socketEx.Message + "\r\n", true, "dumphttp");
                }
                catch (ArgumentNullException nullEx)
                {
                    AddDebugRow("ERR ArgumentNull:" + nullEx.Message + "\r\n", true, "dumphttp");
                }
                catch (ObjectDisposedException objEx)
                {
                    AddDebugRow("ERR ObjectDisposed:" + objEx.Message + "\r\n", true, "dumphttp");
                }
                catch (Exception ex)
                {
                    AddDebugRow("ERR Unexpected error:" + ex.Message + "\r\n", true, "dumphttp");
                }
            }
            else
            {
                _sock.Close();
                _sock.Shutdown(SocketShutdown.Both);
                AddDebugRow("ERR First SOCK DISCONNECT \r\n", true, "dumphttp"); return;
            }
            try
            {
                _sock.Send(bufferResponse);
            }
            catch (SocketException socketEx)
            {
                AddDebugRow("ERR SocketSendEX:" + socketEx.Message + "\r\n", true, "dumphttp");
            }
            catch (ArgumentNullException nullEx)
            {
                AddDebugRow("ERR ArgumentNullSend:" + nullEx.Message + "\r\n", true, "dumphttp");
            }
            catch (ObjectDisposedException objEx)
            {
                AddDebugRow("ERR ObjectDisposedSend:" + objEx.Message + "\r\n", true, "dumphttp");
            }
            catch (Exception ex)
            {
                AddDebugRow("ERR SendUnexpected error:" + ex.Message + "\r\n", true, "dumphttp");
            }
            if (firstMessage.IndexOf(_headerPong) > 0)
            {
                firstMessage = firstMessage.Substring(firstMessage.IndexOf(_headerLogs),
                    firstMessage.IndexOf(_headerPong));
            }
            if (firstMessage.IndexOf(_headerPong) == 0)
            {
                //TO DO WatchDog
                firstMessage = firstMessage.Substring(firstMessage.IndexOf(_headerLogs));
            }
            else if (firstMessage.IndexOf(_headerLogs) == 0)
            {
                int startIndex = firstMessage.IndexOf("{\"events\":");
                if (startIndex < 0)
                {
                    AddDebugRow("Information not found \r\n", true, "dumphttp");
                }
                else
                {
                    try
                    {
                        //AddDebugRow("Start position:" + startIndex + "\r\n", true, "dumphttp");
                        jsonSer = firstMessage.Substring(startIndex);
                        AddDebugRow("Serialized\r\n" + jsonSer + "\r\n", true, "dumphttp");
                        EventsJournal receiveJsonDes = JsonConvert.DeserializeObject<EventsJournal>(jsonSer);
                        //AddDebugRow("Deserialize\r\n", true, "dumphttp");
                        var events = receiveJsonDes.events;
                        int i;
                        for (i = 0; i < events.Count(); i++)
                        {
                            if (events == null) break;
                            //ObjectEvent item = new ObjectEvent();
                            var item = events.ToList()[i];

                            if (item.code == "access_granted_by_api_call")
                            {
                                int door = 0;
                                if (item.info.@lock == 1)
                                {
                                    door = 0;
                                }
                                if (item.info.@lock == 2)
                                {
                                    door = 1;
                                }
                                OnData?.Invoke("EventCode=36, Door=" + door +
                                    ", DeviceDate=#" + UnixTimestampToDateTime(item.created_at) + "#");
                            }
                            else if (item.code == "lock_was_opened_by_exit_btn")
                            {
                                OnData?.Invoke("EventCode=33, Door=0, " +
                                    "DeviceDate=#" + UnixTimestampToDateTime(item.created_at) + "#");
                            }
                            else if (item.code == "access_granted_by_valid_identifier")
                            {
                                OnData?.Invoke("EventCode=0, Door=" + item.info.@lock + ", Card=\"" +
                                    item.info.number.Trim() + "\", DeviceDate=#" +
                                    UnixTimestampToDateTime(item.created_at) + "#");
                            }
                            else if (item.code == "access_denied_by_unknown_card")
                            {
                                OnData?.Invoke("EventCode=32, Door=0, Card=\"" +
                                    item.info.card.Trim() + "\", DeviceDate=#" +
                                    UnixTimestampToDateTime(item.created_at) + "#");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AddDebugRow("ERR First Deserialization or translation the information: " + ex.Message + "\r\n", true, "dumphttp"); return;
                    }
                }
            }


            AddDebugRow("------FIRST RECEIVE THE END------  \r\n", true, "dumphttp");

            try
            {
                //_sock = _listner.AcceptSocket();
                AddDebugRow("Socket LocalEndPoint|RemoteEndPoint(User):" + _sock.LocalEndPoint.ToString() + "|" +
                _sock.RemoteEndPoint + "\r\n" +
                "Socket Available: " + _sock.Available + "\r\n" +
                "Socket Connected: " + _sock.Connected + "\r\n" +
                "Socket Send|Receive Timeout: " + _sock.SendTimeout + "|" + _sock.ReceiveTimeout + "\r\n",
                true, "dumphttp");
            }
            catch (InvalidOperationException ex)
            {
                AddDebugRow("ERR SocketEX at reconnection: " + ex.Message + "\r\n", true, "dumphttp");
            }
            catch (Exception exx)
            {
                AddDebugRow("ERR SocketEX at reconnection: " + exx.Message + "\r\n", true, "dumphttp");
            }
            firstMessage = "";
            try
            {
                if (_sock.Connected)
                {
                    //Stream _stream;
                    while (_active)
                    {
                        byte[] _Buffer = new byte[4096];
                        int _DataReceived = _sock.Receive(_Buffer);
                        string _Message = Encoding.UTF8.GetString(_Buffer, 0, _DataReceived).Trim();

                        try
                        {
                            _sock.Send(bufferResponse);
                        }
                        catch (SocketException socketEx)
                        {
                            AddDebugRow("ERR SocketSendMainEX:" + socketEx.Message + "\r\n", true, "dumphttp");
                        }
                        catch (ArgumentNullException nullEx)
                        {
                            AddDebugRow("ERR ArgumentNullSendMain:" + nullEx.Message + "\r\n", true, "dumphttp");
                        }
                        catch (ObjectDisposedException objEx)
                        {
                            AddDebugRow("ERR ObjectDisposedSendMain:" + objEx.Message + "\r\n", true, "dumphttp");
                        }
                        catch (Exception ex)
                        {
                            AddDebugRow("ERR SendMainUnexpected error:" + ex.Message + "\r\n", true, "dumphttp");
                        }
                        if (_Message.IndexOf(_headerPong) > 0)
                        {
                            _Message = _Message.Substring(_Message.IndexOf(_headerLogs),
                                _Message.IndexOf(_headerPong));
                        }
                        else { }
                        if (_Message.IndexOf(_headerPong) == 0)
                        {
                            //TO DO WatchDog
                        }
                        else if (_Message.IndexOf(_headerLogs) == 0)
                        {
                            int startPosition = _Message.IndexOf("{\"events\":");
                            if (startPosition < 0)
                            {
                                AddDebugRow("Панель повторила отправку пакета событий \r\n"
                                    , true, "dumphttp");
                            }
                            else
                            {
                                try
                                {
                                    //AddDebugRow("Start position:" + startPosition + "\r\n", true, "dumphttp");
                                    jsonSer = _Message.Substring(startPosition);
                                    //AddDebugRow("Serialized\r\n" + jsonSer + "\r\n", true, "dumphttp");
                                    var receiveJsonDes = JsonConvert.DeserializeObject<EventsJournal>(jsonSer);
                                    //AddDebugRow("Deserialize\r\n", true, "dumphttp");
                                    var events = receiveJsonDes.events;
                                    int i;
                                    for (i = 0; i < events.Count(); i++)
                                    {
                                        //ObjectEvent item = new ObjectEvent();
                                        var item = events.ToList()[i];

                                        if (item.code == "access_granted_by_api_call")
                                        {
                                            OnData?.Invoke("EventCode=36, Door=0, DeviceDate=#" +
                                                UnixTimestampToDateTime(item.created_at) + "#");
                                        }
                                        else if (item.code == "lock_was_opened_by_exit_btn")
                                        {
                                            OnData?.Invoke("EventCode=33, Door=0, " +
                                                " DeviceDate=#" + UnixTimestampToDateTime(item.created_at) + "#");
                                        }
                                        else if (item.code == "access_granted_by_valid_identifier")
                                        {
                                            OnData?.Invoke("EventCode=0, Door=" + item.info.@lock + ",Card=\"" +
                                                item.info.number.Trim() + "\", DeviceDate=#" +
                                                UnixTimestampToDateTime(item.created_at) + "#");
                                        }
                                        else if (item.code == "access_denied_by_unknown_card")
                                        {
                                            OnData?.Invoke("EventCode=32, Door=0, Card=\"" +
                                                item.info.card.Trim() + "\", DeviceDate=#" +
                                                UnixTimestampToDateTime(item.created_at) + "#");
                                        }
                                        //using (_stream = File.Open(CreateNameOfLog(ipPart, "dumphttp"), FileMode.Append
                                        //    , FileAccess.Write, FileShare.Write))
                                        //{
                                        //    StreamWriter myWriter = new StreamWriter(_stream);
                                        //    myWriter.WriteLine("----------------------------------------------------------\r\n" +
                                        //    DateTime.Now + "." + DateTime.Now.Millisecond +
                                        //    " " + _Message + "\r\n" + "FROM: " + _sock.RemoteEndPoint + "\r\n\r\n");
                                        //}

                                        AddDebugRow("----------------------------------------------------------\r\n" +
                                            DateTime.Now + "." + DateTime.Now.Millisecond +
                                            " " + _Message + "\r\n" + "FROM: " + _sock.RemoteEndPoint + "\r\n\r\n",
                                        true, "dumphttp");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    AddDebugRow("Deserialization or translation ERROR : "
                                        + ex.Message + "\r\n", true, "dumphttp"); return;
                                }
                            }
                        }
                        else
                        {
                            AddDebugRow(_Message + "\r\n", true, "dumphttp");
                        }
                    }
                }
                else
                {
                    _sock.Close();
                    _sock.Shutdown(SocketShutdown.Both);
                }
            }
            catch (SocketException socketEx)
            {
                AddDebugRow("ERR SocketEX:" + socketEx.Message + "\r\n", true, "dumphttp");
            }
            catch (ArgumentNullException nullEx)
            {
                AddDebugRow("ERR ArgumentNull:" + nullEx.Message + "\r\n", true, "dumphttp");
            }
            catch (ObjectDisposedException objEx)
            {
                AddDebugRow("ERR ObjectDisposed:" + objEx.Message + "\r\n", true, "dumphttp");
            }
            catch (Exception ex)
            {
                AddDebugRow("ERR Unexpected error:" + ex.Message + "\r\n", true, "dumphttp");
            }
        }

        private void _WorkCompleteReauth(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {//завершение ассинхронного выполнения авторизации
            if (e.Cancelled)
            {
                AddDebugRow(DateTime.Now.ToString() + " ERR ReAuthorization was cancelled " + ipPart + ":" +
                portPart + " | " + loginPart,
                true, "_log");
            }
            else if (e.Error != null)
            {
                AddDebugRow(DateTime.Now.ToString() + " ERR " + e.Error.Message + " " + ipPart + ":" +
                portPart + " | " + loginPart,
                true, "_log");
            }
            else
            {
                AddDebugRow(DateTime.Now.ToString() + " Successfull ReAuthorization " + ipPart + ":" +
                portPart + " | " + loginPart,
                true, "_log");
            }
        }

        private void _WorkCompleteServer(object sender, System.ComponentModel.RunWorkerCompletedEventArgs e)
        {//остановка сервера
            if (e.Cancelled)
            {
                AddDebugRow(DateTime.Now.ToString() + "." +
            DateTime.Now.Millisecond.ToString() + " ERR Server was cancelled " + ipPart + "\r\n",
                true, "dumphttp");
                _sock.Close();
                _sock.Shutdown(SocketShutdown.Both);
                backgroundWorker1.Dispose();
            }
            else if (e.Error != null)
            {
                AddDebugRow(DateTime.Now.ToString() + "." +
            DateTime.Now.Millisecond.ToString() + " ERRWorker " + e.Error.Message + " " + ipPart  + "\r\n",
                true, "dumphttp");
                try
                {
                    _sock.Shutdown(SocketShutdown.Both);
                    _sock.Close();
                    _listner.Stop();
                }
                catch(SocketException ex)
                {
                    AddDebugRow("SHUTSock: " + ex.Message + "\r\n",
                    true, "dumphttp");
                }
                catch(ObjectDisposedException exx)
                {
                    AddDebugRow("SHUTObjDisp: " + exx.Message + "\r\n",
                true, "dumphttp");
                }

                backgroundWorker1.Dispose();
                GC.Collect();
                if (e.Error.Message.Contains("(401)"))
                {
                    AddDebugRow("ERR 401", true, "dumphttp");
                    string passMD5 = Encrypt(passwordPart);
                    string result = Auth(ipPart, portPart, loginPart, passMD5);
                    if (result == "OK")
                    {
                        backgroundWorker1.RunWorkerAsync();
                    }
                    else
                    {
                        AddDebugRow("ERR Неудачное переподключение", true, "dumphttp");
                    }
                }
                else backgroundWorker1.RunWorkerAsync();
            }
            else
            {
                AddDebugRow(DateTime.Now.ToString() + "." +
            DateTime.Now.Millisecond.ToString() + " Successfull start server for " + ipPart + "\r\n",
                true, "dumphttp");
                try
                {
                    _sock.Shutdown(SocketShutdown.Both);
                    _sock.Close();
                    _listner.Stop();
                }
                catch (SocketException ex)
                {
                    AddDebugRow("SHUTSock: " + ex.Message + "\r\n",
                true, "dumphttp");
                }
                catch (ObjectDisposedException exx)
                {
                    AddDebugRow("SHUTObjDisp: " + exx.Message + "\r\n",
                true, "dumphttp");
                }
                backgroundWorker1.Dispose();
                GC.Collect();
                backgroundWorker1.RunWorkerAsync();
            }
        }

        public string BuildSetupString(int hParent, string InitialString) //модальное окно с настройками 
            //конкретного экземпляра драйвера
        {
            form = new Form1();
            SetParametersOfCommand(InitialString, "auth");
            form._ip = ipPart;
            form._login = loginPart;
            form._password = passwordPart;
            form._port = portPart;
            form.ShowDialog();
            return "IP=\"" + form._ip + "\", port=" + form._port + ", login=\"" + form._login + "\", " +
            "password=\"" + form._password + "\"";
        }

        public string[] valuesOfPart;
        public string ipPart;
        public string portPart;
        public string loginPart;
        public string passwordPart;
        public string command;
        public string idOfCard;
        public string TZ;
        public string status;
        public string door;
        public string nameOfCard;

        public void SetParametersOfCommand(string Command, string type) //Парсер
        {
            try
            {
                string[] partsOfCommand = new string[64];
                string trimingCommand = Command.Trim();
                if (type == "execute")
                {
                    partsOfCommand = trimingCommand.Split(' ');
                    if (partsOfCommand.Length == 1)
                    {
                        command = trimingCommand;
                    }
                    else
                    {
                        command = partsOfCommand[0];
                    }
                    partsOfCommand[0] = "";
                    string str = string.Join("", partsOfCommand);
                    partsOfCommand = str.Split(',');
                }
                else if (type == "auth")
                {
                    partsOfCommand = trimingCommand.Split(',');
                }

                int i;
                for (i = 0; i < partsOfCommand.Count(); i++)
                {
                    valuesOfPart = partsOfCommand[i].Split('=');
                    if (valuesOfPart.Count() > 1)
                    {
                        int ii;
                        for (ii = 0; ii < 1; ii++)
                        {
                            string firstPart = valuesOfPart[0].Trim(' ').Trim('"').Trim(' ').ToLower();
                            string secondPart = valuesOfPart[1].Trim(' ').Trim('"').Trim(' ');
                            if (firstPart == "ip") ipPart = secondPart;
                            else if (firstPart == "port") portPart = secondPart;
                            else if (firstPart == "login") loginPart = secondPart;
                            else if (firstPart == "password") passwordPart = secondPart;
                            else if (firstPart == "key") idOfCard = secondPart;
                            else if (firstPart == "tz") TZ = secondPart;
                            else if (firstPart == "status") status = secondPart;
                            else if (firstPart == "door")
                            {
                                if (secondPart == "0")
                                {
                                    secondPart = "first";
                                }
                                else if (secondPart == "1")
                                {
                                    secondPart = "second";
                                }
                                door = secondPart;
                            }
                            else if (firstPart == "name") nameOfCard = secondPart;
                            else { }
                        }
                    }
                    else return;
                }
            }
            catch (Exception ex)
            {
                AddDebugRow(DateTime.Now.ToString() + " Ошибка парса:" + ex.Message,
                        true, "_log");
                return;
            }
        }

        public string Encrypt(string password) //шифрование пароля в MD5
        {
            MD5 md5 = MD5.Create();
            byte[] inputBytes = Encoding.ASCII.GetBytes(password);
            byte[] hash = md5.ComputeHash(inputBytes);
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("X2"));
            }
            return sb.ToString();
        }

        public static DateTime UnixTimestampToDateTime(double unixTime) //перевод времени с Unix в DateTime
        {
            DateTime origin = new DateTime(1970, 1, 1, 3, 0, 0);
            return origin.AddMilliseconds(unixTime);
        }

        public string HandleException(WebException ex) //функция обрабатывающая web Exception-ы
        {
            var exc = (HttpWebResponse)ex.Response;
            if (exc != null && ex.Status == WebExceptionStatus.ProtocolError)
            {
                if (exc.StatusCode == HttpStatusCode.RequestTimeout)
                {
                    return "ERR ErrDesc=\"Истекло время ожидания\"";
                }
                else
                {
                    StreamReader stream = new StreamReader(ex.Response.GetResponseStream());
                    var ErrorDesc = JsonConvert.DeserializeObject<Error>(stream.ReadLine());
                    stream.Close();
                    AddDebugRow(DateTime.Now.ToString() + " Error:" + ErrorDesc.error,
                            true, "_log");
                    if (exc.StatusCode == HttpStatusCode.RequestTimeout)
                    {
                        return "ERR ErrDesc=\"Истекло время ожидания\"";
                    }
                    else if (exc.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        return "ERR ErrDesc=\"" + ErrorDesc.error + "\"";
                    }
                    else if (exc.StatusCode == HttpStatusCode.BadRequest)
                    {
                        return "OK Desc=\"" + ErrorDesc.error + "\"";
                    }
                    else if (exc.StatusCode == HttpStatusCode.NotFound)
                    {
                        return "ERR ErrDesc=\"" + ErrorDesc.error + "\"";
                    }
                    else return "ERR ErrDesc=\"" + ErrorDesc.error + "\"";
                }
            }
            else if (exc != null && (ex.Status == WebExceptionStatus.Timeout))
            {
                return "ERR ErrDesc=\"" + ex.Message + "\"";
            }
            else
            {
                AddDebugRow(DateTime.Now.ToString() + " Error:" + ex.Message,
                        true, "_log");
                return "ERR ErrDesc=\"" + ex.Message + "\"";
            }
        }

        public string SendQuery(string Uri, string token, string method, string dataForSend)
        {//функция отправляющая GET, POST запросы
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(Uri);
            request.Timeout = 10000;
            request.PreAuthenticate = true;
            request.Headers.Add("Authorization", "Bearer " + token);
            if (method == "POST")
            {
                request.Method = method;
                request.ContentType = "application/json";
                //request.KeepAlive = true;
                byte[] data = Encoding.UTF8.GetBytes(dataForSend);
                request.ContentLength = data.Length;
                Stream write = request.GetRequestStream();
                write.Write(data, 0, data.Length);
            }
            else if (method == "GET")
            {
                request.Accept = "application/json";
            }
            else if (method == "PATCH")
            {
                request.Method = method;
                request.ContentType = "application/json";
                byte[] data = Encoding.UTF8.GetBytes(dataForSend);
                request.ContentLength = data.Length;
                Stream write = request.GetRequestStream();
                write.Write(data, 0, data.Length);
            }
            else return null;
            HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            using (StreamReader stream = new StreamReader(response.GetResponseStream()))
            {
                string receiveData = stream.ReadLine();
                return receiveData;
            }

        }

        public string GetDeviceTime(string ip, string port, string token)
        {
            try
            {
                string requestTime = "http://" + ip + ":" + port + "/api/v0/device/time";
                string receiveData = SendQuery(requestTime, token, "GET", null);

                var jsonDes = JsonConvert.DeserializeObject<DeviceTime>(receiveData);
                return UnixTimestampToDateTime(jsonDes.device_time_unix).ToString() + " " +
                jsonDes.device_timezone;
            }
            catch (WebException ex)
            {
                return HandleException(ex);
            }
        }

        public string GetDeviceName(string ip, string port, string token)
        {
            try
            {
                string requestVersion = "http://" + ip + ":" + port + "/api/info";
                string receiveData = SendQuery(requestVersion, token, "GET", null);
                var jsonDes = JsonConvert.DeserializeObject<ApiInfo>(receiveData);
                if (jsonDes.device_name == "AA-12B")
                {
                    deviceModel = jsonDes.device_name;
                    firmWare = jsonDes.firmware_version;
                    api = jsonDes.api_version;
                    NameOfDevice = "BAS-IP multi-apartment panel";
                    return NameOfDevice;
                }
                else return null;

            }
            catch (WebException ex)
            {
                string answer = HandleException(ex);
                if (answer.Contains("Not found"))
                {
                    string request = "http://" + ip + ":" + port + "/api/v0/device/name";
                    string receive = SendQuery(request, token, "GET", null);
                    var json = JsonConvert.DeserializeObject<DeviceName>(receive);
                    NameOfDevice = json.device_name;
                    return NameOfDevice;
                }
                else return answer;
            }
        }

        public void AddDebugRow(string text, bool dNotClear, string typeOfLog)
        {//функция записывающая строку в log файл
            using (StreamWriter file = new StreamWriter(CreateNameOfLog(ipPart,
                typeOfLog), dNotClear))
            {
                file.WriteLine(text);
            }
        }

        public string CreateNameOfLog(string ip, string typeOfLog)
        {//Build пути по которому будет лежать log
            typeOfLog.ToLower();
            if (typeOfLog == "dumphttp")
                return "C:\\ProgramData\\Artonit\\bas-ip\\TCP\\" + 
                    DateTime.Now.Day + "_" + DateTime.Now.Month + "_DUMPHTTP.log";
            else
            return "C:\\ProgramData\\Artonit\\bas-ip\\" + ip + typeOfLog + ".log";
        }

        public string AddCardBASMAP(string ip, string port, string id, string nameOfCard, string door)
        {
            try
            {
                string Uri = "http://" + ip + ":" + port + "/api/v0/access/identifiers/item";
                CardPes card = new CardPes
                {
                    identifier_owner = new IdentifierOwner() { name = nameOfCard, type = "owner" },
                    identifier_type = "card",
                    identifier_number = id,
                    @lock = door
                };
                string jsonSer = JsonConvert.SerializeObject(card);
                string receiveData = SendQuery(Uri, token, "POST", jsonSer);
                var uiD = JsonConvert.DeserializeObject<uID>(receiveData);
                AddDebugRow(DateTime.Now.ToString() + "." +
                DateTime.Now.Millisecond.ToString() + " AddCard:" +
                        card.identifier_number + " Ваш ID:" + uiD.uid,
                        true, "_log");
                ReturnTotalCards(ip, port, token);

                return "OK Cell=" + uiD.uid;
            }
            catch (WebException ex)
            {
                return HandleException(ex);
            }
        }

        public string AddCardBASDevice(string ip, string port, string id)
        {
            try
            {
                string Uri = "http://" + ip + ":" + port + "/api/v0/access/legacy/card/item/add";
                ListItemSmallForAdd card = new ListItemSmallForAdd
                {
                    card_id = id,
                    apartment = "9",
                    floor = "9",
                    building = "9",
                    unit = "9"
                };

                string jsonSer = JsonConvert.SerializeObject(card);
                string receiveData = SendQuery(Uri, token, "POST", jsonSer);
                var debug = JsonConvert.DeserializeObject<Error>(receiveData);
                if (debug.error == "")
                {
                    AddDebugRow(DateTime.Now.ToString() + "." +
                    DateTime.Now.Millisecond.ToString() + " AddCard:" + 
                    " Ваш ID:" + id,
                    true, "_log");
                    return "OK Cell=" + idOfCard; 
                }
                else return "ERR ErrDesc=\"" + debug.error + "\"";
            }
            catch (WebException ex)
            {
                return HandleException(ex);
            }
        }

        public string ResetCard(string ip, string port, string token)
        {
            try
            {
                if (NameOfDevice == "BAS-IP device")
                {
                    try
                    {
                        string UriGet = "http://" + ip + ":" + port + "/api/v0/access/legacy/card/items/list";
                        List<SmallCard> listOfCards = new List<SmallCard>();
                        string receiveDataGet = SendQuery(UriGet, token, "GET", null);
                        var jsonDes = JsonConvert.DeserializeObject<ListItemsSmall>(receiveDataGet);
                        IEnumerable<ListItemSmall> listItems = jsonDes.list;
                        int j;
                        for (j = 0; j < listItems.Count(); j++)
                        {
                            SmallCard smallCard = new SmallCard() { card_id = listItems.ToList()[j].card_id };
                            listOfCards.Add(smallCard);
                        }
                        string UriDevice = "http://" + ip + ":" + port + "/api/v0/access/legacy/card/items/delete";
                        DeleteSmall cards = new DeleteSmall()
                        {
                            count = listOfCards.Count(),
                            list = listOfCards
                        };
                        var jsonSer = JsonConvert.SerializeObject(cards);
                        string receiveDataDel = SendQuery(UriDevice, token, "POST", jsonSer);
                        var ErrDesc = JsonConvert.DeserializeObject<Error>(receiveDataDel);
                        if (ErrDesc.error == "")
                        {
                            AddDebugRow(DateTime.Now.ToString() + "." +
                            DateTime.Now.Millisecond.ToString() + " ClearKeys",
                            true, "_log");
                            return "OK";// id=" + id;
                        }
                        else return "ERR ErrDesc=\"" + ErrDesc.error + "\"";
                    }
                    catch (WebException ex)
                    {
                        return HandleException(ex);
                    }
                }
                else if (NameOfDevice == "BAS-IP multi-apartment")
                {
                    string Uri = "http://" + ip + ":" + port + "/api/v0/access/identifiers/items/delete"; // ПОСМОТРИ СЮДА
                    string maxUid = AddCardBASMAP(ipPart, portPart, "ABCDEF", null, "all");
                    if (maxUid.Contains("exist")) return "ERR ErrDesc=\"Error\"";
                    string[] partsOfLine = maxUid.Split('=');
                    string maxUiD = partsOfLine[1].Trim();
                    List<int> Items = new List<int>();
                    double countDouble = int.Parse(maxUiD) / 100;
                    double countRequests = Math.Ceiling(countDouble);
                    int i;
                    int ii = 0;
                    int iii = 0;
                    int page = 0;
                    for (i = 0; i < countRequests + 1; i++)
                    {
                        page = i + 1;
                        for (ii = iii; ii < page * 100; ii++)
                        {
                            if (ii == double.Parse(maxUiD)) break;
                            iii = ii + 1;
                            Items.Add(iii);
                        }
                        Delete UID = new Delete()
                        {
                            count = Items.Count(),
                            uid_items = Items
                        };
                        var jsonSer = JsonConvert.SerializeObject(UID);
                        string receiveData = SendQuery(Uri, token, "POST", jsonSer);
                        Items.Clear();
                    }
                    ReturnTotalCards(ip, port, token);
                    AddDebugRow(DateTime.Now.ToString() + "." +
                    DateTime.Now.Millisecond.ToString() + " ClearKeys",
                            true, "_log");
                    return "OK";//=" + uid;
                }
                else return "ERR ErrDesc=\"Возможно, вы работаете с устройством которое не поддерживается\"";
            }
            catch(WebException ex)
            {
                return HandleException(ex);
            }
            catch(Exception ex)
            {
                return "ERR ErrDesc=\"" + ex.Message + "123\"";
            }
        }

        public ListItem FindId(string ip, string port, string token, string id)
        {
            try
            {
                string Uri = "http://" + ip + ":" + port +
                                       "/api/v0/access/identifiers/items/list?limit=50&sort_field=identifier_number&sort_type=desc" +
                                       "&filter_field=identifier_number" +
                                       "&filter_type=equal&filter_format=string&filter_value=" + id;
                string receiveData = SendQuery(Uri, token, "GET", null);
                ListJSON json = JsonConvert.DeserializeObject<ListJSON>(receiveData);
                if (json.list_items == null) return null;
                IEnumerable<ListItem> listItems = json.list_items;//.OrderBy(Item => Item.identifier_uid);
                if (listItems.Count() > 1 || listItems == null) return null;
                return listItems.ToList()[0];
                //door = listItems.ToList()[0].@base.@lock;
                //uiD = listItems.ToList()[0].identifier_uid;
            }
            catch(Exception ex)
            {
                return null;
            }
        }

        public string DeleteCardBASMAP(string ip, string port, int uid)
        {
            try
            {
                string Uri = "http://" + ip + ":" + port + "/api/v0/access/identifiers/items/delete";
                Delete UID = new Delete()
                {
                    count = 1,
                    uid_items = new List<int>() {
                                                    uid
                                                }
                };
                var jsonSer = JsonConvert.SerializeObject(UID);
                string receiveData = SendQuery(Uri, token, "POST", jsonSer);
                ReturnTotalCards(ip, port, token);
                AddDebugRow(DateTime.Now.ToString() + "." +
                DateTime.Now.Millisecond.ToString() + " Карта: " + idOfCard +
                    " успешно удалена, uid=" + uid ,
                        true, "_log");
                return "OK";//=" + uid;
            }
            catch (WebException ex)
            {
                return HandleException(ex);
            }
        }

        public string DeleteCardBASDevice(string ip, string port, int id)
        {
            try
            {
                string Uri = "http://" + ip + ":" + port + "/api/v0/access/legacy/card/items/delete";
                DeleteSmall card = new DeleteSmall()
                {
                    count = 1,
                    list = new List<SmallCard>() {
                                                    new SmallCard 
                                                    { 
                                                        card_id = id 
                                                    }
                                                 }
                };
                var jsonSer = JsonConvert.SerializeObject(card);
                string receiveData = SendQuery(Uri, token, "POST", jsonSer);
                var ErrDesc = JsonConvert.DeserializeObject<Error>(receiveData);
                if (ErrDesc.error == "")
                {
                    AddDebugRow(DateTime.Now.ToString() + "." +
                    DateTime.Now.Millisecond.ToString() + " DelCard:" +
                    id ,
                    true, "_log");
                    return "OK";// id=" + id;
                }
                else return "ERR ErrDesc=\"" + ErrDesc.error + "\"";
            }
            catch (WebException ex)
            {
                return HandleException(ex);
            }
        }

        //public string ReadCard(string ip, string port, string uiD, string door)
        //{
        //    try
        //    {
        //        //List<DriverClass.ListItem> allCards;
        //        //string uidOfCard = "";
        //        //if (door != 0) { return "ERR ErrDesc=\"Argument \"door\" must be in the range 0 to 0\""; }
        //        //if (NameOfDevice == "BAS-IP multi-apartment panel")
        //        //{
        //        //    allCards = returnAllListOfCardsBASMAP(ipPart, portPart, token);
        //        //    uidOfCard = allCards.Select(Item => Item).Where(x => x.@base.identifier_number.
        //        //    Equals(idOfCard)).Where(y => y.@base.@lock.Equals(door)).
        //        //    Select(x => x.identifier_uid.ToString()).FirstOrDefault();
        //        //}
        //        List<ListItemSmall> allCards;

        //        if (NameOfDevice == "BAS-IP device")
        //        {
        //            //allCards = returnAllListOfCardsBASDevice(ipPart, portPart, token);
        //            //uiD = allCards.Select(Item => Item).Where(x => x.card_id.
        //            //Equals(uiD)).Select(x => x.card_id).FirstOrDefault().ToString();
        //            uiD = UpdateLog(ipPart, uiD, null, NameOfDevice, "read");
        //            if (uiD == "OK Desc=\'Значение не найдено в списке значений\'") return uiD;
        //            else return "OK id=\"" + uiD + "\"" + ", Access = \"yes\"";
        //        }
        //        else if (NameOfDevice == "BAS-IP multi-apartment panel")
        //        {

        //        }
        //        else return null;
        //        //if (uiD == null) return "ERR ErrDesc=\"Карта не найдена." + " | Access = \"no\"\"";
        //        //else
        //        //{
        //            //if (NameOfDevice == "BAS-IP multi-apartment panel")
        //            //{
        //            //    string concreteCardUri = "http://" + ip + ":" + port +
        //            //    "/api/v0/access/identifiers/item/" + uiD;
        //            //    string ConcreteCardJson = SendQuery(concreteCardUri, token, "GET", null);
        //            //    var jsonDes = JsonConvert.DeserializeObject<ListItem>(ConcreteCardJson);
        //            //    var numberCard = jsonDes.@base.identifier_number.ToString();
        //            //    string doorOfCard = jsonDes.@base.@lock.ToString();
        //            //        AddDebugRow(DateTime.Now.ToString() + " Карта: " + uiD + ", uid: "
        //            //        + uiD + " Access =\"yes\"",
        //            //        true, "_log");
        //            //    return "OK uid=\"" + uiD + "\"" + ", id=\"" + numberCard +
        //            //        "\"" + ", Access = \"yes\"";
        //            //}
        //            //else if (NameOfDevice == "BAS-IP device")
        //            //{
        //            //    uidOfCard = UpdateLog(ipPart, uiD, null, NameOfDevice, "read");
        //            //    if (uidOfCard == "OK Desc=\'Значение " + "не найдено в списке значений\'") return uidOfCard;
        //            //    else return "OK id=\"" + uidOfCard + "\"" + ", Access = \"yes\"";
        //            //}
        //            //else return null;
        //        //}
        //    }
        //    catch (WebException ex) { return HandleException(ex); }
        //}

        public string ReturnTotalCards(string ip, string port, string token)
        {
            try
            {
                if (NameOfDevice == "BAS-IP multi-apartment panel")
                {
                    string Uri = "http://" + ip + ":" + port +
                        "/api/v0/access/identifiers/items/list?limit=10";
                    string receiveData = SendQuery(Uri, token, "GET", null);
                    var json = JsonConvert.DeserializeObject<ListJSON>(receiveData);
                    countItemsOfCardsBASMAP = json.list_option.pagination.total_items.ToString();
                    double countCards = double.Parse(countItemsOfCardsBASMAP);
                    double countPages = countCards / 50;
                    countPages = Math.Ceiling(countPages);
                    countPagesOfCards = countPages.ToString();
                    //countPagesOfCards = json.list_option.pagination.total_pages.ToString();
                }
                else if (NameOfDevice == "BAS-IP device")
                {
                    string Uri = "http://" + ip + ":" + port + "/api/v0/access/legacy/card/items/list";
                    List<ListItemSmall> listOfCards = new List<ListItemSmall>();
                    string receiveData = SendQuery(Uri, token, "GET", null);
                    var jsonDes = JsonConvert.DeserializeObject<ListItemsSmall>(receiveData);
                    countItemsOfCardsBASDevice = jsonDes.count.ToString();
                }
                else return null;
                return HttpStatusCode.OK.ToString();
            }
            catch (WebException ex)
            {
                return HandleException(ex);
            }
        }

        public string ReturnTotalEvents(string ip, string port)
        {
            try
            {
                string Uri = "http://" + ip + ":" + port + "/api/v0/log/list?locale=ru&limit=" +
                        "50&page_number=1";
                string receiveData = SendQuery(Uri, token, "GET", null);
                var jsonDes = JsonConvert.DeserializeObject<Journal>(receiveData);
                countItemsOfJournal = jsonDes.list_option.pagination.total_items.ToString();
                countPagesOfJournal = jsonDes.list_option.pagination.total_pages.ToString();
                return HttpStatusCode.OK.ToString();
            }
            catch (WebException ex)
            {
                return HandleException(ex);
            }
        }

        public List<ListItem> returnAllListOfCardsBASMAP(string ip, string port, string token)
        {//Вернуть весь дамп карт
            ReturnTotalCards(ipPart, portPart, token);
            listOfCards.Clear();
            listOfCards = new List<ListItem>();
            int i = 0;
            for (i = 0; i < int.Parse(countPagesOfCards); i++)
            {
                int page = i + 1;
                string Uri = "http://" + ip + ":" + port +
                "/api/v0/access/identifiers/items/list?page_number=" + page + "&limit=50";
                string receiveData = SendQuery(Uri, token, "GET", null);
                var json = JsonConvert.DeserializeObject<ListJSON>(receiveData);
                IEnumerable<ListItem> listItems = json.list_items.OrderBy(Item => Item.identifier_uid);
                int ii;
                for (ii = 0; ii < listItems.Count(); ii++)
                {
                    listOfCards.Add(listItems.ToList()[ii]);
                }
            }
            AddDebugRow("", false, "_cards");
            int iii;
            for (iii = 0; iii < listOfCards.Count(); iii++)
            {
                AddDebugRow(
                listOfCards[iii].identifier_uid + " | " +
                listOfCards[iii].@base.identifier_owner.name + " | " +
                listOfCards[iii].@base.identifier_number + " | " +
                listOfCards[iii].@base.@lock + ";",
                        true, "_cards");
            }
            i = 0;
            return listOfCards;
        }

        public List<ListItemSmall> returnAllListOfCardsBASDevice(string ip, string port, string token)
        {
            string Uri = "http://" + ip + ":" + port + "/api/v0/access/legacy/card/items/list";
            List<ListItemSmall> listOfCards = new List<ListItemSmall>();
            string receiveData = SendQuery(Uri, token, "GET", null);
            var jsonDes = JsonConvert.DeserializeObject<ListItemsSmall>(receiveData);
            IEnumerable<ListItemSmall> listItems = jsonDes.list;
            int ii;
            for (ii = 0; ii < listItems.Count(); ii++)
            {
                listOfCards.Add(listItems.ToList()[ii]);
            }
            AddDebugRow(
                "idCard | apartment | building | floor | unit;" + "\r\n",
                        false, "_cards");
            int iii;
            for (iii = 0; iii < listOfCards.Count(); iii++)
            {
                AddDebugRow(
                    listOfCards[iii].card_id + " | " + listOfCards[iii].apartment
                    + " | " + listOfCards[iii].building + " | " + listOfCards[iii].floor
                    + " | " + listOfCards[iii].unit
                    + ";",
                        true, "_cards");
            }
            countItemsOfCardsBASDevice = jsonDes.count.ToString();
            return listOfCards;
        }

        public string UpdateLog(string ipPart, string uidOfCard, string door,
            string nameOfDevice, string typeOfCommand)
        {
            try
            {
                string path = CreateNameOfLog(ipPart, "_cards");
                string uidOfCardRes = null;
                partsOfLine = null;
                using (StreamReader file = new StreamReader(path))
                {
                    string data = file.ReadToEnd();
                    partsOfLine = data.Split(';');
                }
                if (typeOfCommand == "read")
                {
                    int ii;
                    for (ii = 0; ii < partsOfLine.Count(); ii++)
                    {
                        //8105295 | 4 | 1 | 0 | 1;    /BasDevice
                        //120 | 013 | 11624020 | all;   /BasMAP
                        string[] partOfLine = partsOfLine[ii].Split('|');
                        if (nameOfDevice == "BAS-IP multi-apartment panel")
                        {
                            if (partOfLine[0].Trim() != null
                                && (partOfLine[0].Trim() != "")
                                && (partOfLine[2].Trim() == uidOfCard))
                                //&& (partOfLine[3].Trim() == door))
                            {
                                uidOfCardRes = partOfLine[2].Trim() + 
                                    ";" + partOfLine[3].Trim() + ";" + partOfLine[0].Trim();
                                break;
                            }
                            else
                                //partOfLine = null; 
                                continue;
                        }
                        else if (nameOfDevice == "BAS-IP device")
                        {
                            partOfLine = partsOfLine[ii].Split('|');

                            if (partOfLine[0].Trim() != null
                                && (partOfLine[0].Trim() != "")
                                && (partOfLine[0].Trim() == uidOfCard))
                            {
                                uidOfCardRes = partOfLine[0].Trim();
                                break;
                            }
                            else
                                //partOfLine = null; 
                                continue;
                        }
                        else return null;
                    }
                    if (uidOfCardRes == "" || uidOfCardRes == null) return "OK Desc=\'Значение " +
                             "не найдено в списке значений\'";
                    else return uidOfCardRes;
                }
                else if (typeOfCommand == "write")
                {
                    if (nameOfCard == "") nameOfCard = null;
                    if (nameOfDevice == "BAS-IP multi-apartment panel")
                    { 
                        string item = uidOfCard + " | " + nameOfCard + " | " + idOfCard +
                            " | " + door + ";";
                        cards.Add(item);
                        AddDebugRow(item, true, "_cards");
                        return "OK";
                    }
                    else if (nameOfDevice == "BAS-IP device")
                    {
                        string item = uidOfCard + " | " + "9" + " | " + "9" + " | " + "9" + " | " + "9"
                            + ";";
                        cards.Add(item);
                        AddDebugRow(item, true, "_cards");
                        return "OK";
                    }
                    else return null;
                }
                else if (typeOfCommand == "delete") 
                {
                    try
                    {
                        //cards.Clear();
                        int i;
                        string deleteItem = null;
                        for (i = 0; i < partsOfLine.Count(); i++)
                        {
                            if (partsOfLine.Length != 0)
                            {
                                string[] partOfLine = partsOfLine[i].Split('|');
                                if (uidOfCard != partOfLine[0].Trim()) cards.Add(partsOfLine[i] + ";");
                                else deleteItem = partOfLine[0]; 
                            }
                            else continue;
                        }
                        AddDebugRow("", false, "_cards");
                        int ii;
                        for (ii = 0; ii < cards.Count(); ii++)
                        { AddDebugRow(cards[ii].Trim(), true, "_cards"); }
                        cards.Clear();
                        return deleteItem;
                    }
                    catch (Exception ex)
                    {
                        return "ERR ErrDesc=\'" + ex.Message + "22\'";
                    }
                }
                else if (typeOfCommand == "getkeycountMAP")
                {
                    int countFirst = 0;
                    int countSecond = 0;
                    int countAll = 0;
                    int ii;
                    for (ii = 0; ii < partsOfLine.Count(); ii++)
                    {
                        //8105295 | 4 | 1 | 0 | 1;    /BasDevice
                        //120 | 013 | 11624020 | all;   /BasMAP
                        string[] partOfLine = partsOfLine[ii].Split('|');
                        if (partOfLine[0].Trim() != null
                            && (partOfLine[0].Trim() != "")
                            && (partOfLine[3].Trim() == "all")) countAll++;
                        else if (partOfLine[0].Trim() != null
                            && (partOfLine[0].Trim() != "")
                            && (partOfLine[3].Trim() == "first")) countFirst++;
                        else if (partOfLine[0].Trim() != null
                            && (partOfLine[0].Trim() != "")
                            && (partOfLine[3].Trim() == "second")) countSecond++;
                        else continue;
                    }
                    return countFirst + ";" + countSecond + ";" + countAll;
                }
                else return null;
            }
            catch (Exception ex)
            {
                return "ERR ErrDesc=\'" + ex.Message + partsOfLine.Count() + " 123|\'";
            }
        }

        public string OpenDoor(string ip, string port, string door)
        {
            try
            {
                string request = "http://" + ip + ":" + port +
                    "/api/v0/access/general/lock/open/remote/accepted/" + door.Trim();
                string receiveData = SendQuery(request, token, "GET", null);
                return "OK";
            }
            catch (WebException ex)
            {
                return HandleException(ex);
            }
        }

        public string Reboot (string ipPart, string portPart)
        {
            try
            {
                string request = "http://" + ipPart + ":" + portPart +
                        "/api/v0/system/reboot/run";
                string receiveData = SendQuery(request, token, "GET", null);
                return "OK";
            }
            catch (WebException ex)
            {
                return HandleException(ex);
            }
        }

        public string UpdateIdentifier(string ip, string port, ListItem item)
        {
            try
            {
                string Uri = "http://" + ip + ":" + port +
                "/api/v0/access/identifiers/item/" + item.identifier_uid;
                var jsonSer = JsonConvert.SerializeObject(item);
                string receiveData = SendQuery(Uri, token, "PATCH", jsonSer);
                return "OK " + "Cell=" + item.identifier_uid;
            }
            catch(WebException ex)
            {
                return HandleException(ex);
            }
        }

        public string Execute(string AllCommand) //система реализованных команд 
        {
            string dateTime = DateTime.Now.ToString() + "." +
            DateTime.Now.Millisecond.ToString();
            if (token == "" || token == null ||
                NameOfDevice == "" || NameOfDevice == null)
                return "ERR ErrDesc = \"Online=No\"";
            if (_active)
            {
                NameOfDevice = GetDeviceName(ipPart, portPart, token);
                if(NameOfDevice != "BAS-IP device")
                {
                    string[] partsOfVersion = firmWare.Split('.');
                    if (int.Parse(partsOfVersion[0]) >= 3
                        && int.Parse(partsOfVersion[1]) >= 6
                        && int.Parse(partsOfVersion[2]) >= 0) { }
                    else return "ERR ErrDesc=\"Please update the firmWare to the version 3.6.0 and highter\"";
                }
                door = "";
                SetParametersOfCommand(AllCommand, "execute");
                AddDebugRow(dateTime +
                    "Command: " + AllCommand,
                true, "_log");
                string lowCommand = command.ToLower();
                string path = CreateNameOfLog(ipPart, "_cards");
                try
                {
                    if (lowCommand == "writekey")
                    {
                        if (File.Exists(path))
                        {
                            try
                            {
                                if (NameOfDevice == "BAS-IP multi-apartment panel")
                                {
                                    var item = FindId(ipPart, portPart, token, idOfCard);
                                    if (item == null)
                                    {
                                        string answer = AddCardBASMAP(ipPart, portPart,
                                            idOfCard, nameOfCard, door);
                                        if (answer.Contains("ERR")) return answer;
                                        else if (answer.Contains("OK"))
                                        {
                                            string[] partsOfAnswer = answer.Trim().Split('=');
                                            UpdateLog(ipPart, partsOfAnswer[1].Trim(),
                                                door, NameOfDevice, "write");
                                            return answer;
                                        }
                                        else return answer;
                                    }
                                    else
                                    {
                                        if (door == "first" && item.@base.@lock == "second"
                                            || door == "second" && item.@base.@lock == "first")
                                        {
                                            item.@base.@lock = "all";
                                            string ans = UpdateLog(ipPart, item.identifier_uid.ToString().Trim(),
                                                   null, NameOfDevice, "delete");
                                            var result = UpdateIdentifier(ipPart, portPart, item);
                                            UpdateLog(ipPart, item.identifier_uid.ToString().Trim(),
                                                null, NameOfDevice, "delete");
                                            UpdateLog(ipPart, item.identifier_uid.ToString().Trim(),
                                                "all", NameOfDevice, "write");
                                            if (result.Contains("ERR")) return result;
                                            else return "OK";
                                        }
                                        else if ((door == "second" || door == "first")
                                            && item.@base.@lock == "all") return "OK";
                                        else return "OK";
                                    }


                                    //    string iD = null;
                                    //    string uiD = null;
                                    //    string doorPart = null;
                                    //    string idAndDoor = null;
                                    //    idAndDoor = UpdateLog(ipPart, idOfCard,
                                    //        null, NameOfDevice, "read");
                                    //    if (idAndDoor.Contains("Значение не найдено"))
                                    //    {
                                    //        string answer = null;
                                    //        answer = AddCardBASMAP(ipPart, portPart,
                                    //            idOfCard, nameOfCard, door);
                                    //        if (answer.Contains("ERR")) return answer;
                                    //        else if (answer.Contains("OK"))
                                    //        {
                                    //            if (answer.Contains("already exist") ||
                                    //                answer.Contains("Wrong"))
                                    //            {
                                    //                UpdateLog(ipPart, "*",
                                    //                    door, NameOfDevice, "write");
                                    //                return "OK";//answer;
                                    //            }
                                    //            else
                                    //            {
                                    //                string[] partsOfAnswer = answer.Trim().Split('=');
                                    //                UpdateLog(ipPart, partsOfAnswer[1].Trim(),
                                    //                    door, NameOfDevice, "write");
                                    //                return answer;
                                    //            }
                                    //        }
                                    //        else return answer;
                                    //    }
                                    //    else if (idAndDoor.Contains("ERR")) return idAndDoor + "readErr";
                                    //    else
                                    //    {
                                    //        string[] partsOfAnsw = idAndDoor.Split(';');
                                    //        iD = partsOfAnsw[0].Trim();
                                    //        doorPart = partsOfAnsw[1].Trim();
                                    //        uiD = partsOfAnsw[2].Trim();
                                    //        if (door == "first" && doorPart == "second")
                                    //        {
                                    //            AddDebugRow("tyt1" + uiD + doorPart + door, true, "_log");
                                    //            string answerDel = DeleteCardBASMAP(ipPart,
                                    //                portPart, int.Parse(uiD));
                                    //            if (answerDel.Contains("OK"))
                                    //            {
                                    //                if (answerDel.Contains("Wrong"))
                                    //                {
                                    //                    UpdateLog(ipPart, uiD,
                                    //                    null, NameOfDevice, "delete");
                                    //                    return Execute(AllCommand);//"OK OKDesc=\"Value is not found in list of values'\"";
                                    //                }
                                    //                else
                                    //                {
                                    //                    //string[] partsOfDel = answerDel.Split('=');
                                    //                    string ans = UpdateLog(ipPart, uiD,
                                    //                        null, NameOfDevice, "delete");
                                    //                    AddDebugRow(ans.Trim(), true, "_log");
                                    //                    string answerAdd = AddCardBASMAP(ipPart, portPart,
                                    //                        idOfCard, nameOfCard, "all");
                                    //                    if (answerAdd.Contains("ERR")) return answerAdd + "AddErr";
                                    //                    else // if (answerAdd.Contains("OK"))
                                    //                    {
                                    //                        if (answerAdd.Contains("already exist") ||
                                    //                            answerAdd.Contains("Wrong")) return "OK";// answerAdd;
                                    //                        else
                                    //                        {
                                    //                            string[] partsOfAnswer = answerAdd.Trim().Split('=');
                                    //                            UpdateLog(ipPart, uiD,
                                    //                                null, NameOfDevice, "delete");
                                    //                            UpdateLog(ipPart, partsOfAnswer[1],
                                    //                                "all", NameOfDevice, "write");
                                    //                            return "OK";// answerAdd;
                                    //                        }
                                    //                    }
                                    //                }
                                    //            }
                                    //            else return answerDel;
                                    //        }
                                    //        else if (door == "second" && doorPart == "first")
                                    //        {
                                    //            AddDebugRow("tyt2" + uiD + doorPart + door, true, "_log");
                                    //            string answerDel = DeleteCardBASMAP(ipPart, portPart, int.Parse(uiD));
                                    //            if (answerDel.Contains("OK"))
                                    //            {
                                    //                if (answerDel.Contains("Wrong"))
                                    //                {
                                    //                    UpdateLog(ipPart, uiD,
                                    //                    null, NameOfDevice, "delete");
                                    //                    return Execute(AllCommand);//"OK OKDesc=\"Значение не обнаружено в БД'\"";
                                    //                }
                                    //                else
                                    //                {
                                    //                    //string[] partsOfAnswerDel = answerDel.Trim().Split('=');
                                    //                    string ans = UpdateLog(ipPart, uiD,
                                    //                        null, NameOfDevice, "delete");
                                    //                    AddDebugRow(ans, true, "_log");
                                    //                    string answerAdd = AddCardBASMAP(ipPart, portPart,
                                    //                        idOfCard, nameOfCard, "all");
                                    //                    if (answerAdd.Contains("ERR")) return answerAdd + "answerAddErr";
                                    //                    else //(answerAdd.Contains("OK"))
                                    //                    {
                                    //                        string[] partsOfAnswer = answerAdd.Trim().Split('=');
                                    //                        if (answerAdd.Contains("already exist") ||
                                    //                            answerAdd.Contains("Wrong")) return "OK";//answerAdd;
                                    //                        else
                                    //                        {
                                    //                            UpdateLog(ipPart, uiD, null,
                                    //                                NameOfDevice, "delete");
                                    //                            UpdateLog(ipPart, partsOfAnswer[1],
                                    //                                "all", NameOfDevice, "write");
                                    //                            return "OK";// answerAdd;
                                    //                        }
                                    //                    }
                                    //                }
                                    //            }
                                    //            else return answerDel;
                                    //        }
                                    //        else if ((door == "second" || door == "first")
                                    //            && doorPart == "all")
                                    //        {
                                    //            AddCardBASMAP(ipPart, portPart,
                                    //                    idOfCard, nameOfCard, "all");
                                    //            return "OK";
                                    //        }
                                    //        else
                                    //        {
                                    //            AddDebugRow("tyt3" + uiD + doorPart + door, true, "_log");
                                    //            AddCardBASMAP(ipPart, portPart,
                                    //                  idOfCard, nameOfCard, door);
                                    //            return "OK";
                                    //        }
                                    //    }
                                }
                                else if (NameOfDevice == "BAS-IP device")
                                {
                                    if (UpdateLog(ipPart, idOfCard, door, NameOfDevice, "read").
                                        Contains("Значение не найдено"))
                                    {
                                        string answer = AddCardBASDevice(ipPart, portPart, idOfCard);
                                        if (answer.Contains("ERR")) return answer;
                                        else if (answer.Contains("OK"))
                                        {
                                            if (answer.Contains("Bad Request") ||
                                                answer.Contains("Wrong"))
                                                return "OK";
                                            else
                                            {
                                                string[] partsOfAnswer = answer.Trim().Split('=');
                                                string id = UpdateLog(ipPart, partsOfAnswer[1],
                                                    door, NameOfDevice, "read");
                                                if (id.Contains("Значение не найдено"))
                                                {
                                                    UpdateLog(ipPart, partsOfAnswer[1],
                                                        door, NameOfDevice, "write");
                                                    return "OK";// answer;
                                                }
                                                return "OK";// Desc=\'" + "Card already exists" + "\'";
                                            }
                                        }
                                        else return null;
                                    }
                                    else return "OK";// Desc=\'Value in list already exists\'";
                                }
                                else return "ERR ErrDesc=\"Online: Not on line\"";
                            }
                            catch (Exception ex)
                            {
                                return "ERR ErrDesc=\"" + ex.Message + "exceptionAdd\"";
                            }
                        }
                        else
                        {
                            try
                            {
                                if (NameOfDevice == "BAS-IP multi-apartment panel")
                                {
                                    returnAllListOfCardsBASMAP(ipPart, portPart, token);
                                }
                                else if (NameOfDevice == "BAS-IP device")
                                {
                                    returnAllListOfCardsBASDevice(ipPart, portPart, token);
                                }
                                return Execute(AllCommand);
                            }
                            catch (Exception ex)
                            {
                                return "ERR ErrDesc=\"" + ex.Message + "ER\"";
                            }
                        }
                    }
                    else if (lowCommand == "deletekey")
                    {
                        if (File.Exists(path))
                        {
                            try
                            {
                                if (NameOfDevice == "BAS-IP multi-apartment panel")
                                {
                                    try
                                    {
                                        var item = FindId(ipPart, portPart, token, idOfCard);
                                        if (item == null) return "OK";
                                        else
                                        {
                                            if (door == "first" && item.@base.@lock == "all")
                                            {
                                                item.@base.@lock = "second";
                                                UpdateLog(ipPart, item.identifier_uid.ToString().Trim(),
                                                   null, NameOfDevice, "delete");
                                                var answer = UpdateIdentifier(ipPart, portPart, item);
                                                UpdateLog(ipPart, item.identifier_uid.ToString().Trim(),
                                                   null, NameOfDevice, "delete");
                                                UpdateLog(ipPart, item.identifier_uid.ToString().Trim(),
                                                    "second", NameOfDevice, "write");
                                                if (answer.Contains("ERR")) return answer;
                                                else return "OK";
                                            }
                                            else if (door == "second" && item.@base.@lock == "all")
                                            {
                                                item.@base.@lock = "first";
                                                UpdateLog(ipPart, item.identifier_uid.ToString().Trim(),
                                                   null, NameOfDevice, "delete");
                                                var answer = UpdateIdentifier(ipPart, portPart, item);
                                                UpdateLog(ipPart, item.identifier_uid.ToString().Trim(),
                                                   null, NameOfDevice, "delete");
                                                UpdateLog(ipPart, item.identifier_uid.ToString().Trim(),
                                                    "first", NameOfDevice, "write");
                                                if (answer.Contains("ERR")) return answer;
                                                else return "OK";
                                            }
                                            else if (door == "first" && item.@base.@lock == "second")
                                                return "OK";
                                            else if (door == "second" && item.@base.@lock == "first")
                                                return "OK";
                                            else
                                            {
                                                string answerDel = DeleteCardBASMAP(ipPart, portPart, item.identifier_uid);
                                                if (answerDel.Contains("OK"))
                                                {
                                                    //string[] partsOfLineDel = answerDel.Split('=');
                                                    UpdateLog(ipPart, item.identifier_uid.ToString().Trim(),
                                                        null, NameOfDevice, "delete");
                                                    if (answerDel.Contains("Wrong"))
                                                    {
                                                        UpdateLog(ipPart, item.identifier_uid.ToString().Trim(),
                                                            null, NameOfDevice, "delete");
                                                        return "OK";// OKDesc=\"Значение не обнаружено в базе данных\"";
                                                    }
                                                    else
                                                    {
                                                        string ans = UpdateLog(ipPart,
                                                            item.identifier_uid.ToString().Trim(),
                                                            null, NameOfDevice, "delete");
                                                        return answerDel;
                                                    }
                                                }
                                                else
                                                {
                                                    UpdateLog(ipPart, item.identifier_uid.ToString().Trim(),
                                                        null, NameOfDevice, "delete");
                                                    return answerDel;
                                                }
                                            }
                                        }


                                        //string iD = null;
                                        //string uiD = null;
                                        //string FindingDoorPart = null;
                                        //string answerExist = null;
                                        //answerExist = UpdateLog(ipPart, idOfCard.Trim(),
                                        //    null, NameOfDevice, "read");
                                        //if (answerExist.Contains("OK")) return "OK";
                                        //else if (answerExist.Contains("ERR")) return answerExist + "deleteErr";
                                        //else
                                        //{
                                        //    string[] partsOfAnswer = answerExist.Split(';');
                                        //    iD = partsOfAnswer[0];
                                        //    FindingDoorPart = partsOfAnswer[1].Trim();
                                        //    uiD = partsOfAnswer[2].Trim();
                                        //    if (door == "first" && FindingDoorPart == "all")
                                        //    {
                                        //        AddDebugRow(door + " " + FindingDoorPart + " " + uiD + "one", true, "_log");
                                        //        string answerDel = DeleteCardBASMAP(ipPart, portPart, int.Parse(uiD));
                                        //        if (answerDel.Contains("OK"))
                                        //        {
                                        //            AddDebugRow(answerDel + "(answerDel)", true, "_log");
                                        //            if (answerDel.Contains("Wrong"))
                                        //            {
                                        //                UpdateLog(ipPart, uiD, null, NameOfDevice, "delete");
                                        //                return "OK"; //OKDesc=\"Значение не обнаружено в базе данных\"";
                                        //            }
                                        //            else
                                        //            {
                                        //                UpdateLog(ipPart,
                                        //                    uiD, null, NameOfDevice, "delete");
                                        //                string answerAdd = AddCardBASMAP(ipPart, portPart,
                                        //                    idOfCard, nameOfCard, "second");
                                        //                AddDebugRow(answerAdd + "(answerAdd)", true, "_log");
                                        //                if (answerAdd.Contains("OK"))
                                        //                {
                                        //                    string[] partsOfLineAdd = answerAdd.Split('=');
                                        //                    UpdateLog(ipPart, partsOfLineAdd[1], "second",
                                        //                        NameOfDevice, "write");
                                        //                    string ans = UpdateLog(ipPart,
                                        //                    uiD, null, NameOfDevice, "delete");
                                        //                    AddDebugRow(ans, true, "_log");
                                        //                    return "OK";// Cell =" + uiD;
                                        //                }
                                        //                else return answerAdd + "ERr";
                                        //            }
                                        //        }
                                        //        else return answerDel;
                                        //    }
                                        //    else if (door == "second" && FindingDoorPart == "all")
                                        //    {
                                        //        AddDebugRow(door + " " + FindingDoorPart + " " + uiD + "two", true, "_log");
                                        //        string answerDel = DeleteCardBASMAP(ipPart, portPart, int.Parse(uiD));
                                        //        if (answerDel.Contains("OK"))
                                        //        {
                                        //            //string[] partsOfLineDel = answerDel.Split('=');
                                        //            if (answerDel.Contains("Wrong"))
                                        //            {
                                        //                UpdateLog(ipPart, uiD, null, NameOfDevice, "delete");
                                        //                return "OK";// OKDesc=\"Значение не обнаружено в базе данных\"";
                                        //            }
                                        //            else
                                        //            {
                                        //                UpdateLog(ipPart, uiD, null, NameOfDevice, "delete");
                                        //                string answeradd = AddCardBASMAP(ipPart, portPart,
                                        //                    idOfCard, nameOfCard, "first");
                                        //                if (answeradd.Contains("OK"))
                                        //                {
                                        //                    string[] partsOfLine = answeradd.Split('=');
                                        //                    UpdateLog(ipPart, partsOfLine[1], "first",
                                        //                        NameOfDevice, "write");
                                        //                    string ans = UpdateLog(ipPart,
                                        //                    uiD, null, NameOfDevice, "delete");
                                        //                    AddDebugRow(ans, true, "_log");
                                        //                    return "OK";// Cell=" + uiD;
                                        //                }
                                        //                else return answeradd + "ErrSecondAll";
                                        //            }
                                        //        }
                                        //        else return answerDel + "three";
                                        //    }
                                        //    else if (door == "first" && FindingDoorPart == "second")
                                        //        return "OK";// OKDesc=\"Карта существует на другом канале, " +
                                        //                    //"операция удаления с текущего канала невозможна\"";
                                        //    else if (door == "second" && FindingDoorPart == "first")
                                        //        return "OK";// OKDesc=\"Карта существует на другом канале, " +
                                        //                    //"операция удаления с текущего канала невозможна\"";
                                        //    else
                                        //    {
                                        //        AddDebugRow(door + " " + FindingDoorPart + " " + uiD + "oOo", true, "_log");
                                        //        string answerDel = DeleteCardBASMAP
                                        //                (ipPart, portPart, int.Parse(uiD));
                                        //        if (answerDel.Contains("OK"))
                                        //        {
                                        //            //string[] partsOfLineDel = answerDel.Split('=');
                                        //            UpdateLog(ipPart, uiD, null, NameOfDevice, "delete");
                                        //            if (answerDel.Contains("Wrong"))
                                        //            {
                                        //                UpdateLog(ipPart, uiD, null, NameOfDevice, "delete");
                                        //                return "OK";// OKDesc=\"Значение не обнаружено в базе данных\"";
                                        //            }
                                        //            else
                                        //            {
                                        //                string ans = UpdateLog(ipPart,
                                        //                    uiD, null, NameOfDevice, "delete");
                                        //                AddDebugRow(ans + " Del", true, "_log");
                                        //                return answerDel;
                                        //            }
                                        //        }
                                        //        else
                                        //        {
                                        //            UpdateLog(ipPart, uiD, null, NameOfDevice, "delete");
                                        //            return answerDel;
                                        //        }
                                        //    }
                                        //}
                                    }
                                    catch (Exception ex)
                                    {
                                        return "ERR ErrDesc=\'" + ex.Message + "Generally\'";
                                    }
                                }
                                else if (NameOfDevice == "BAS-IP device")
                                {
                                    try
                                    {
                                        string iD = UpdateLog(ipPart, idOfCard.Trim(), door, NameOfDevice, "read");
                                        if (iD.Contains("OK")) return "OK";//iD;
                                        else if (iD.Contains("ERR")) return iD;
                                        else
                                        {
                                            string answer = DeleteCardBASDevice(ipPart, portPart, int.Parse(iD));
                                            if (answer.Contains("ERR")) return answer;
                                            else if (answer.Contains("OK"))
                                            {
                                                if (answer.Contains("Wrong"))
                                                {
                                                    UpdateLog(ipPart, iD, null, NameOfDevice, "delete");
                                                    return answer;
                                                }
                                                else
                                                {
                                                    UpdateLog(ipPart, iD, null, NameOfDevice, "delete");
                                                    UpdateLog(ipPart, iD, null, NameOfDevice, "delete");
                                                    return answer;
                                                }
                                            }
                                            else return null;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        return "ERR ErrDesc=\"" + ex.Message + "\"";
                                    }
                                }
                                else return "ERR ErrDesc=\"Online: Not on line\"";
                            }
                            catch (WebException ex)
                            {
                                return HandleException(ex);
                            }
                        }
                        else
                        {
                            if (NameOfDevice == "BAS-IP multi-apartment panel")
                            {
                                returnAllListOfCardsBASMAP(ipPart, portPart, token);
                            }
                            else if (NameOfDevice == "BAS-IP device")
                            {
                                returnAllListOfCardsBASDevice(ipPart, portPart, token);
                            }
                            return Execute(AllCommand);
                        }
                    }
                    else if (lowCommand == "getdevicetime")
                    {
                        string _result = "OK DeviceTime=" + GetDeviceTime(ipPart, portPart, token);
                        AddDebugRow(dateTime +
                        "Result: " + _result,
                        true, "_log");
                        return _result;
                    }
                    else if (lowCommand == "reportstatus")
                    {
                        string passmd5 = Encrypt(passwordPart);
                        string answer = Auth(ipPart, portPart, loginPart, passmd5);
                        if (answer == "OK")
                        {
                            string _result = "OK Online=Yes, DeviceName=" +
                                GetDeviceName(ipPart, portPart, token);
                            AddDebugRow(dateTime +
                            "Result: " + _result,
                            true, "_log");
                            return _result;
                        }
                        else if (answer.Contains("Время ожидания")) return "OK Online=\'No\'";
                        else
                        {
                            AddDebugRow(dateTime +
                            "Result: " + "OK Online=No",
                            true, "_log");
                            return "OK Online=No";
                        }
                    }
                    else if (lowCommand == "getdevicename")
                    {
                        string _result = "OK DeviceName=" + GetDeviceName(ipPart, portPart, token);
                        AddDebugRow(dateTime +
                        "Result: " + _result,
                        true, "_log");
                        return _result;
                    }
                    else if (lowCommand == "getversion")
                    {
                        string _result = "OK Version=\"" + GetDeviceName(ipPart, portPart, token) + "\"";
                        AddDebugRow(dateTime +
                        "Result: " + _result,
                        true, "_log");
                        return _result;
                    }
                    else if (lowCommand == "readkey")
                    {
                        if (NameOfDevice == "BAS-IP multi-apartment panel")
                        {
                            var item = FindId(ipPart, portPart, token, idOfCard);
                            if (item == null) return "OK Cell=65535, Key=\"00000000\", Access = No, TZ=0x0000";
                            else
                            {
                                string doorPart = item.@base.@lock;
                                int FindingUID = item.identifier_uid;
                                if (doorPart == "first")
                                {
                                    doorPart = "10";
                                }
                                else if (doorPart == "second")
                                {
                                    doorPart = "01";
                                }
                                else if (doorPart == "all")
                                {
                                    doorPart = "11";
                                }
                                if (door == "first")
                                {
                                    if (doorPart == "10" || doorPart == "11")
                                    {
                                        string answer = "OK Cell=" + FindingUID + ", key=" + idOfCard + ", Access = Yes";
                                        AddDebugRow(dateTime + " " + answer, true, "_log");
                                        return answer;
                                    }
                                    else return "OK Cell=" + FindingUID + ", key=\"" + idOfCard + "\", Access = No";
                                }
                                else if (door == "second")
                                {
                                    if (doorPart == "01" || doorPart == "11")
                                    {
                                        string answer = "OK Cell=" + FindingUID + ", key=" + idOfCard + ", Access = Yes";
                                        AddDebugRow(dateTime + " " + answer, true, "_log");
                                        return answer;
                                    }
                                    else return "OK Cell=" + FindingUID + ", key=\"" + idOfCard + "\", Access = No";
                                }
                                else return "OK Answer=\"Такого канала не существует\"";
                            }
                        }
                        else if (NameOfDevice == "BAS-IP device")
                        {
                            if (File.Exists(path))
                            {
                                string iD = null;
                                string FindingUID = null;
                                string doorPart = null;
                                string idAndDoor = UpdateLog(ipPart, idOfCard, null, NameOfDevice, "read");
                                if (idAndDoor.Contains("OK")) return "OK Cell=65535, Key=\"00000000\", Access = No, TZ=0x0000";
                                else if (idAndDoor.Contains("ERR")) return idAndDoor + "readErr";
                                else
                                {
                                    string[] partsOfAnsw = idAndDoor.Split(';');
                                    iD = partsOfAnsw[0].Trim();
                                    if (NameOfDevice == "BAS-IP device")
                                    {
                                        doorPart = "10";
                                        if (door == "first" || door == "second")
                                        {
                                            if (doorPart == "10")
                                            {
                                                string answer = "OK Cell=" + FindingUID + ", key=" + iD + ", Access = Yes";
                                                AddDebugRow(dateTime + " " + answer, true, "_log");
                                                return answer;
                                            }
                                            else return "OK Cell=" + FindingUID + ", key=\"" + iD + "\", Access = No";
                                        }
                                        else return "OK Answer=\"Такого канала не существует\"";
                                    }
                                    else return "ERR ErrDesc=\"Данный тип устройства не поддерживается\"";
                                }
                            }
                            else
                            {
                                try
                                {
                                    if (NameOfDevice == "BAS-IP multi-apartment panel")
                                    {
                                        returnAllListOfCardsBASMAP(ipPart, portPart, token);
                                    }
                                    else if (NameOfDevice == "BAS-IP device")
                                    {
                                        returnAllListOfCardsBASDevice(ipPart, portPart, token);
                                    }
                                    return Execute(AllCommand);
                                }
                                catch (Exception ex)
                                {
                                    return "ERR ErrDesc=\"" + ex.Message + "\"";
                                }
                            }
                            //if (File.Exists(path))
                            //{
                            //    var item = FindId(ipPart, portPart, token, idOfCard);
                            //    if (item == null) return "OK Cell=65535, Key=\"00000000\", Access = No, TZ=0x0000";
                            //    else
                            //    {

                            //    }
                            //    string iD = null;
                            //    string FindingUID = null;
                            //    string doorPart = null;
                            //    string idAndDoor = UpdateLog(ipPart, idOfCard, null, NameOfDevice, "read");
                            //    if (idAndDoor.Contains("OK")) return "OK Cell=65535, Key=\"00000000\", Access = No, TZ=0x0000";
                            //    else if (idAndDoor.Contains("ERR")) return idAndDoor + "readErr";
                            //    else
                            //    {
                            //        string[] partsOfAnsw = idAndDoor.Split(';');
                            //        iD = partsOfAnsw[0].Trim();
                            //        if (NameOfDevice == "BAS-IP multi-apartment panel")
                            //        {
                            //            doorPart = partsOfAnsw[1].Trim();
                            //            FindingUID = partsOfAnsw[2].Trim();
                            //            if (doorPart == "first")
                            //            {
                            //                doorPart = "10";
                            //            }
                            //            else if (doorPart == "second")
                            //            {
                            //                doorPart = "01";
                            //            }
                            //            else if (doorPart == "all")
                            //            {
                            //                doorPart = "11";
                            //            }
                            //            if (door == "first")
                            //            {
                            //                if (doorPart == "10" || doorPart == "11")
                            //                {
                            //                    string answer = "OK Cell=" + FindingUID + ", key=" + iD + ", Access = Yes";
                            //                    AddDebugRow(dateTime + " " + answer, true, "_log");
                            //                    return answer;
                            //                }
                            //                else return "OK Cell=" + FindingUID + ", key=\"" + iD + "\", Access = No";
                            //            }
                            //            else if (door == "second")
                            //            {
                            //                if (doorPart == "01" || doorPart == "11")
                            //                {
                            //                    string answer = "OK Cell=" + FindingUID + ", key=" + iD + ", Access = Yes";
                            //                    AddDebugRow(dateTime + " " + answer, true, "_log");
                            //                    return answer;
                            //                }
                            //                else return "OK Cell=" + FindingUID + ", key=\"" + iD + "\", Access = No";
                            //            }
                            //            else return "OK Answer=\"Такого канала не существует\"";
                            //        }
                            //        else if (NameOfDevice == "BAS-IP device")
                            //        {
                            //            doorPart = "10";
                            //            if (door == "first" || door == "second")
                            //            {
                            //                if (doorPart == "10")
                            //                {
                            //                    string answer = "OK Cell=" + FindingUID + ", key=" + iD + ", Access = Yes";
                            //                    AddDebugRow(dateTime + " " + answer, true, "_log");
                            //                    return answer;
                            //                }
                            //                else return "OK Cell=" + FindingUID + ", key=\"" + iD + "\", Access = No";
                            //            }
                            //            else return "OK Answer=\"Такого канала не существует\"";
                            //        }
                            //        else return "ERR ErrDesc=\"Данный тип устройства не поддерживается\"";
                            //    }
                            //}
                            //else
                            //{
                            //    try
                            //    {
                            //        if (NameOfDevice == "BAS-IP multi-apartment panel")
                            //        {
                            //            returnAllListOfCardsBASMAP(ipPart, portPart, token);
                            //        }
                            //        else if (NameOfDevice == "BAS-IP device")
                            //        {
                            //            returnAllListOfCardsBASDevice(ipPart, portPart, token);
                            //        }
                            //        return Execute(AllCommand);
                            //    }
                            //    catch (Exception ex)
                            //    {
                            //        return "ERR ErrDesc=\"" + ex.Message + "\"";
                            //    }
                            //}
                        }
                        else return "ERR ErrDesc=\"Данный тип устройства не поддерживается\"";
                    }
                    else if (lowCommand == "getkeycount")
                    {
                        if (NameOfDevice == "BAS-IP multi-apartment panel")
                        {
                            string countOfCards = UpdateLog(ipPart, null, null, NameOfDevice, "getkeycountMAP");
                            string[] partsOfKeyCount = countOfCards.Split(';');
                            countItemsDoorFirst = int.Parse(partsOfKeyCount[0].Trim());
                            countItemsDoorSecond = int.Parse(partsOfKeyCount[1].Trim());
                            countItemsDoorAll = int.Parse(partsOfKeyCount[2].Trim());
                            countItemsDoor0andAll = countItemsDoorFirst + countItemsDoorAll;
                            countItemsDoor1andAll = (countItemsDoorSecond +
                                countItemsDoorAll).ToString();
                            //var allCards = returnAllListOfCardsBASMAP(ipPart, portPart, token);
                            //countItemsDoorFirst = listOfCards.Select(ListItem => ListItem).Where(x => x.@base.@lock.
                            //Equals("first")).Count();
                            //countItemsDoorSecond = listOfCards.Select(ListItem => ListItem).Where(x => x.@base.@lock.
                            //Equals("second")).Count();
                            //countItemsDoorAll = listOfCards.Select(ListItem => ListItem).Where(x => x.@base.@lock.
                            //Equals("all")).Count();
                            //countItemsDoor0andAll = countItemsDoorFirst + countItemsDoorAll;
                            //countItemsDoor1andAll = (countItemsDoorSecond +
                            //    countItemsDoorAll).ToString();
                        }
                        else if (NameOfDevice == "BAS-IP device")
                        {
                            returnAllListOfCardsBASDevice(ipPart, portPart, token);
                            countItemsDoor0andAll = int.Parse(countItemsOfCardsBASDevice);
                            countItemsDoor1andAll = "n/a";
                            AddDebugRow(dateTime +
                            " Result: " + "0Door: " + countItemsDoor0andAll + "; 1Door: " +
                            countItemsDoor1andAll + ";",
                            true, "_log");
                        }
                        if (door == "first")
                        {
                            string _result = ("OK KeyCount=" + countItemsDoor0andAll);
                            AddDebugRow(dateTime +
                            "Result: " + _result,
                            true, "_log");
                            //first and all
                            return _result;
                        }
                        else if (door == "second")
                        {
                            string _result = ("OK KeyCount=" + countItemsDoor1andAll);
                            AddDebugRow(dateTime +
                            "Result: " + _result,
                            true, "_log");
                            //second and all
                            return _result;
                        }
                        else return "ERR ErrDesc=\"Укажите дверь\"";
                    }
                    else if (lowCommand == "opendoor")
                    {
                        try
                        {
                            string doorPart = null;
                            if (door == "first") doorPart = "0";
                            else if (door == "second") doorPart = "1";
                            else return "ERR ErrDesc=\"Такого канала не существует\"";
                            return OpenDoor(ipPart, portPart, doorPart);
                        }
                        catch (Exception ex)
                        {
                            return "ERR ErrDesc=\"" + ex.Message + "er\"";
                        }
                    }
                    else if (lowCommand == "reboot")
                    {
                        try
                        {
                            return Reboot(ipPart, portPart);
                        }
                        catch (Exception ex)
                        {
                            return "ERR ErrDesc=\"" + ex.Message + "er\"";
                        }
                    }
                    else if (lowCommand == "unlockdoor")
                    {
                        return "ERR ErrDesc=\"in developing\"";
                    }
                    else if (lowCommand == "lockdoor")
                    {
                        return "ERR ErrDesc=\"in developing\"";
                    }
                    else if (lowCommand == "opendooralways")
                    {
                        return "ERR ErrDesc=\"in developing\"";
                    }
                    else if (lowCommand == "clearkeys")
                    {
                        return ResetCard(ipPart, portPart, token);
                    }
                    else
                    {
                        return "ERR ErrDesc=\"Unnamed command\"";
                    }
                }
                catch(WebException ex)
                {
                    if (ex.Response == null)
                    {
                        return "ERR ErrDesc=\"" + ex.Status + "main\"";
                    }
                    AddDebugRow(DateTime.Now.ToString() + "." +
                DateTime.Now.Millisecond.ToString() + " Error:" + ex.Message,
                            true, "_log");
                    return HandleException(ex);
                }
            }
            else return "OK Online=No";
        }

        private void _timer_Tick(object sender, EventArgs e)
        {
            WatchDog?.Invoke();
        }

        private void _timer_Auth(object sender, EventArgs e)
        {
            if (backgroundWorker.IsBusy != true)
            {
                backgroundWorker.WorkerReportsProgress = true;
                backgroundWorker.RunWorkerAsync();
            }
            else
            {

            }
        }

        public void Start()
        {
            _active = true;
            _sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            SetParametersOfCommand(SetupString, "auth");
            string passmd5 = Encrypt(passwordPart);
            AddDebugRow(DateTime.Now.ToString() + "." +
            DateTime.Now.Millisecond.ToString() + " Start", true, "_log");

            string result = Auth(ipPart, portPart, loginPart, passmd5);
            if (result == "OK")
            {
                _timerWD.Start();
                _timerAuth.Start();
                NameOfDevice = GetDeviceName(ipPart, portPart, token);
                ReturnTotalCards(ipPart, portPart, token);
                AddDebugRow(DateTime.Now.ToString() + "." +
            DateTime.Now.Millisecond.ToString() + " " + result + " Successfull Authorization",
                true, "_log");
                if (NameOfDevice == "BAS-IP multi-apartment panel")
                {
                    try
                    {
                        //ассинхронный процесс (сервер для приёма событий онлайн)
                        backgroundWorker1.DoWork += new System.ComponentModel.DoWorkEventHandler(ServerHandler);
                        backgroundWorker1.RunWorkerCompleted +=
                        new System.ComponentModel.RunWorkerCompletedEventHandler(_WorkCompleteServer);
                        if (backgroundWorker1.IsBusy != true)
                        {
                            backgroundWorker1.WorkerSupportsCancellation = true;
                            backgroundWorker1.RunWorkerAsync();
                        }
                        else
                        {

                        }
                    }
                    catch (Exception ex)
                    {
                        AddDebugRow("ERRStart" + ex.Message + "\r\n", true, "dumphttp");
                    }
                }
                else { }
            }
            else 
            {
                _timerWD.Start();
                AddDebugRow(DateTime.Now.ToString() + "." +
            DateTime.Now.Millisecond.ToString() + " ERR Unsuccessfull authorization " + result,
                    true, "_log");
            }

        }

        public void Stop()
        {
            AddDebugRow(DateTime.Now.ToString() + "." +
            DateTime.Now.Millisecond.ToString() + " Stop",
            true, "_log");
            _timerWD.Stop();
            _timerAuth.Stop();
            if (NameOfDevice == "BAS-IP multi-apartment panel")
            {
                try
                {
                    backgroundWorker1.DoWork -= new System.ComponentModel.DoWorkEventHandler(ServerHandler);
                    backgroundWorker1.RunWorkerCompleted -=
                    new System.ComponentModel.RunWorkerCompletedEventHandler(_WorkCompleteServer);
                    if (backgroundWorker1.IsBusy)
                    {
                        try
                        {
                            backgroundWorker1.CancelAsync();
                        }
                        catch (InvalidOperationException ex)
                        {
                            AddDebugRow("Cancel" + ex.Message + "\r\n", true, "dumphttp");
                        }
                    }
                   // _sock.Blocking = true;
                    _sock.Shutdown(SocketShutdown.Both);
                    _sock.Close();
                }
                catch (SocketException sockEx)
                {
                    AddDebugRow("Sock: " + sockEx.Message + "\r\n", true, "dumphttp");
                }
                catch(ObjectDisposedException objDispose)
                {
                    AddDebugRow("ObjectDispose: " + objDispose.Message + "\r\n", true, "dumphttp");
                }

                AddDebugRow("Client:" + ipPart + " Disconnected. \r\n", true, "dumphttp");
                try
                {
                    _listner.Stop();
                }
                catch (Exception exx)
                {
                    AddDebugRow("Sock already closed: " + exx.Message + "\r\n", true, "dumphttp");
                }
                //backgroundWorker1.Dispose();
                AddDebugRow("Server Stop. \r\n --------------CLOSE-------------- \r\n\r\n", true, "dumphttp");
                LogOut(ipPart, portPart, token);
            }
            else AddDebugRow(DateTime.Now.ToString() + "." +
            DateTime.Now.Millisecond.ToString() + " " + ipPart + ":" +
                    portPart + " Logout",
                    true, "_log");
            backgroundWorker.Dispose();
            cards.Clear();
            partsOfLine = null;
            firstMessage = null;
            _active = false;
        }

        public string Auth(string ip, string port, string login, string passwordmd5)
        {
            string requestUri = "http://" + ip + ":" + port + "/api/v0/login?username=" + 
                login + "&password=" + passwordmd5;
            try
            {
                request = (HttpWebRequest)WebRequest.Create(requestUri);
                request.Timeout = 5000;
                response = (HttpWebResponse)request.GetResponse();
                AddDebugRow(DateTime.Now.ToString() + "." +
            DateTime.Now.Millisecond.ToString() + " Query send to " + ip + ":" + port +
                    " | " + login,
                true, "_log");
                using (StreamReader stream = new StreamReader(response.GetResponseStream()))
                {
                    var jsonDes = JsonConvert.DeserializeObject<dataBIP>(stream.ReadLine());
                    stream.Close();
                    token = jsonDes.token;
                }
                _active = true;
                return "OK";
            }
            catch (WebException ex)
            {
                if(ex.Response == null)
                {
                    return "ERR ErrDesc=\"" + ex.Status + "\"";
                }
                AddDebugRow(DateTime.Now.ToString() + "." +
            DateTime.Now.Millisecond.ToString() + " Error:" + ex.Message,
                        true, "_log");
                return HandleException(ex);
            }
        }

        public void LogOut(string ip, string port, string token)
        {//закрытие ключа сессии
            try
            {
                AddDebugRow(DateTime.Now.ToString() + "." + DateTime.Now.Millisecond.ToString() +
                   " Start closing session;",
                        true, "_log");
                string requestUri = "http://" + ip + ":" + port + "/api/v0/logout";
                request = WebRequest.Create(requestUri) as HttpWebRequest;
                request.Timeout = 10000;
                request.Headers.Add("Authorization", token);
                response = (HttpWebResponse)request.GetResponse();
                AddDebugRow(DateTime.Now.ToString() + "." +
            DateTime.Now.Millisecond.ToString() + " Successfull closing session;",
                        true, "_log");
                token = "";
                AddDebugRow(DateTime.Now.ToString() + "." +
            DateTime.Now.Millisecond.ToString() + " Logout" ,
                        true, "_log");
            }
            catch (WebException ex)
            {
                AddDebugRow(DateTime.Now.ToString() + "." +
            DateTime.Now.Millisecond.ToString() + " Error:" + ex.Message,
                        true, "_log");
            }
        }

       [ComVisible(true)]
        public string SetupString { get; set; }

        [ComVisible(true)]
        public DateTime DateTime
        {
            get { return DateTime.Now; }
            set { }
        }
        
        [ComVisible(true)]
        public bool Active
        {
            get { return _active; }
            set { if (value) Start(); else Stop(); }
        }

        public event ITS2DeviceEvents_OnDataEventHandler   OnData;
        public event ITS2DeviceEvents_OnErrorEventHandler  OnError;
        public event ITS2DeviceEvents_WatchDogEventHandler WatchDog;

        [Serializable]
        [ComVisible(false)]
        public class LockDelay
        {
            public int lock_delay { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class LockTimeout
        {
            public int lock_timeout { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class ApiInfo
        {
            //public string device_model { get; set; }
            public string firmware_version { get; set; }
            public string framework_version { get; set; }
            public string frontend_version { get; set; }
            public string api_version { get; set; }
            public string device_name { get; set; }
            public string device_type { get; set; }
            public string device_serial_number { get; set; }
            public bool hybrid_enable { get; set; }
            public string hybrid_version { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class EventsJournal
        {
            public IList<ObjectEvent> events { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class ObjectEvent
        {
            public long created_at { get; set; }
            public string category { get; set; }
            public string code { get; set; }
            public string priority { get; set; }
            public Info info { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class Info
        {
            [DataMember(Name = "card")]
            public string card { get; set; }
            [JsonProperty("account_type", NullValueHandling = NullValueHandling.Ignore)]
            public string account_type { get; set; }
            [JsonProperty("apartment_address", NullValueHandling = NullValueHandling.Ignore)]
            public string apartment_address { get; set; }
            [JsonProperty("owner", NullValueHandling = NullValueHandling.Ignore)]
            public string owner { get; set; }
            [JsonProperty("type", NullValueHandling = NullValueHandling.Ignore)]
            public string type { get; set; }
            [JsonProperty("lock", NullValueHandling = NullValueHandling.Ignore)]
            public int @lock { get; set; }
            [JsonProperty("host", NullValueHandling = NullValueHandling.Ignore)]
            public string host { get; set; }
            [JsonProperty("number", NullValueHandling = NullValueHandling.Ignore)]
            public string number { get; set; }
            [JsonProperty("answered", NullValueHandling = NullValueHandling.Ignore)]
            public bool answered { get; set; }

        }
        [Serializable]
        [ComVisible(false)]
        public class Remote
        {
            public string link_password { get; set; }
            public bool link_enable { get; set; }
            public bool realtime_logging { get; set; }
            public bool heartbeat { get; set; }
            public string link_url { get; set; }
           // public string active { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class dataBIP
        {
            public string token { get; set; }
            public string account_type { get; set; }
        }//авторизация
        [Serializable]
        [ComVisible(false)]
        public class IdentifierOwner
        {
            public string name { get; set; }
            public string type { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class CardPes
        {
            public IdentifierOwner identifier_owner { get; set; }
            public string identifier_type { get; set; }
            public string identifier_number { get; set; }
            public string @lock { get; set; }
        } //Конкретная КАРТА для добавления
        [Serializable]
        [ComVisible(false)]
        public class uID
        {
            public string uid { get; set; }
        } //Возврат id добавленной карты
        [Serializable]
        [ComVisible(false)]
        public class Error
        {
            public string error { get; set; }
        } //возвращаемая ошибка
        [Serializable]
        [ComVisible(false)]
        public class Delete
        {
            public int count { get; set; }
            public List<int> uid_items { get; set; }
        }//Удаление карт
        [Serializable]
        [ComVisible(false)]
        public class ListJSON
        {
            public ListOption list_option { get; set; }
            public List<ListItem> list_items { get; set; }
        }// Тело возвращаемого листа всех карт
        [Serializable]
        [ComVisible(false)]
        public class ListOption
        {
            public Pagination pagination { get; set; }
            public Filter filter { get; set; }
            public Sort sort { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class Pagination
        {
            public int total_pages { get; set; }
            public int items_limit { get; set; }
            public int total_items { get; set; }
            public int current_page { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class Filter
        {
            public bool is_filtered { get; set; }
            public bool available_filtering { get; set; }
            public List<string> available_fields { get; set; }
            //public string filter_field { get; set; }
            //public string filter_type { get; set; }
            //public string filter_format { get; set; }
            //public string filter_value { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class Sort
        {
            public bool asc { get; set; }
            public string field { get; set; }
            public string available_fields { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class TimeProfiles
        {
            public int count { get; set; }
            public List<int> uid_items { get; set; }//????????
        }
        [Serializable]
        [ComVisible(false)]
        public class Base
        {
            public string @lock { get; set; }
            public string identifier_type { get; set; }
            public IdentifierOwner identifier_owner { get; set; }
            public string identifier_number { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class Time
        {
            public bool is_permanent { get; set; }
            public int from { get; set; }
            public int to { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class Passes
        {
            public bool is_permanent { get; set; }
            public int max_passes { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class Valid
        {
            public Time time { get; set; }
            public Passes passes { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class Apartment
        {
            public int uid { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class Additional
        {
            public int passes_left { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class ListItem
        {
            public Base @base { get; set; }
            public Valid valid { get; set; }
            public Apartment apartment { get; set; }
            public TimeProfiles time_profiles { get; set; }
            public int identifier_uid { get; set; }
            public string link_id { get; set; }
            public Additional additional { get; set; }
        }

        [Serializable]
        [ComVisible(false)]
        public class DeviceTime
        {
            public long device_time_unix { get; set; }
            public string device_timezone { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class DeviceName
        {
            public string device_name { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class PaginationJournal
        {
            public int total_pages { get; set; }
            public int items_limit { get; set; }
            public int total_items { get; set; }
            public int current_page { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class AvailableField
        {
            public List<string> available_types { get; set; }
            public string filter_field { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class NameJ
        {
            public string english { get; set; }
            public string key { get; set; }
            public string localized { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class AvailableValues
        {
            public List<string> category { get; set; }
            public List<string> priority { get; set; }
            public List<NameJ> name { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class FilterJournal
        {
            public bool available_filtering { get; set; }
            public List<AvailableField> available_fields { get; set; }
            public AvailableValues available_values { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class LocalJournal
        {
            public List<string> available_locales { get; set; }
            public string locale { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class SortJournal
        {
            public bool asc { get; set; }
            public List<string> available_values { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class ListOptionJournal
        {
            public PaginationJournal pagination { get; set; }
            public FilterJournal filter { get; set; }
            public LocalJournal locale { get; set; }
            public SortJournal sort { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class NameJournalItems
        {
            public string english { get; set; }
            public string key { get; set; }
            public string localized { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class ModelJournal
        {
            public string account_type { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class InfoJournal
        {
            public string english { get; set; }
            public ModelJournal model { get; set; }
            public string localized { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class ListItemsJournal
        {
            public long timestamp { get; set; }
            public string category { get; set; }
            public string priority { get; set; }
            public InfoJournal info { get; set; }
            public NameJournalItems name { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class Journal
        {
            public ListOptionJournal list_option { get; set; }
            public List<ListItemsJournal> list_items { get; set; }
        }

        //Small panel 
        [Serializable]
        [ComVisible(false)]
        public class ListItemsSmall //cards
        {
            public List<ListItemSmall> list { get; set; }
            public int count;
        }
        [Serializable]
        [ComVisible(false)]
        public class ListItemSmall
        {
            public int card_id { get; set; }
            public int building { get; set; }
            public int unit { get; set; }
            public int floor { get; set; }
            public int apartment { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class ListItemSmallForAdd
        {
            public string card_id { get; set; }
            public string building { get; set; }
            public string unit { get; set; }
            public string floor { get; set; }
            public string apartment { get; set; }
            //public string identifier_number { get; set; }
            //public string apartment_number { get; set; }
        }
        [Serializable]
        [ComVisible(false)]
        public class DeleteSmall
        {
            public int count { get; set; }
            public List<SmallCard> list { get; set; }
        }//Удаление карт
        [Serializable]
        [ComVisible(false)]
        public class SmallCard
        {
            public int card_id { get; set; }
        }//карта на удаление маленькой панели
    }                                                               
    [ComVisible(false)]
    [Serializable]
    public delegate void ITS2DeviceEvents_OnDataEventHandler(string Msg);
    [ComVisible(false)]
    [Serializable]
    public delegate void ITS2DeviceEvents_OnErrorEventHandler(string Msg);
    [ComVisible(false)]
    [Serializable]
    public delegate void ITS2DeviceEvents_WatchDogEventHandler();
}
//ЧТО КАСАТЕЛЬНО ОБОЛОЧЕК

//Чтобы использовать COM-компонент из среды NET, необходимо создать оболочку времени выполнения
//(runtime callable wrapper RCW) применяя RCW.клиент NET может видеть объект .NET вместо
//COM компонента (Если нужно обратиться к COM-компоненту из клиента.NET)
//RCW скрывает интерфейсы IUnknown и IDispatch
//RCW можно создать tlbimp COMServer.dll /out: Interop.COMServer.dll - создаст
//новый файл с классом оболочкой
//ССW - (COM Callable wrapper) вызываемые оболочки(Чтоб добраться до компонента NET из клиентского
//приложения COM)


//ЧТО КАСАТЕЛЬНО IConnectionPointContainer

//Для событий COM, компонент должен реализовывать интерфейс ICOnnectionPointContainer
//и один или более объектов точек подключения(Connection point object - CPO), реализующих интерфейс
//IConnectionPoint.Компонент так же определяет выходной интерфейс ICompletedEvents, который
//вызывается СРО
//Клиент должен реализовать этот выходной интерфейс в объекте-приёмнике, который сам является
//COM-объектом.Во время выполнения клиент запрашивает у сервера интерфейс IConnectionPointCOntainer.
//С помощью этого интерфейса клиент запрашивает СРО методом FindConnectionPoint, чтобы получить от него
//указатель на IConnectionPoint.Этот указатель на интерфейс применяется клиентом для вызова
//метода Advise(), где указатель на объект приемник передается серверу.В свою очередь компонент может


//ЧТО КАСАТЕЛЬНО ИНТЕРФЕЙСОВ ДЛЯ РАБОТЫ С COM
//Ком различает три вида интерфейсов
//3 типа интерфейсов: 1)заказной наследуется от IUnknown(базовый для всех других интерфейсов), 
//определяет порядок методов в виртуальной таблице
//(vtable) таким образом, что клиент может обращаться к ним непосредственно(QUeryInterface,
//AddRef, Release)
//2)диспетчеризуемый IDispatch клиенту он всегда доступен, наследуется от IUnknown
//и предоставляет 4 дополнительных метода 2 их них: GetIDsOfNames(), Invoke()
//3)и дуальный(диспетчеризуемые - медленные, заказные не могут быть использованы сценарными клиентами) =>
//решают проблему, наследуется от IDispatch, предоставляют дополнительные методы в vtable
//сценарные клиенты могут использовать IDispatch для вызова методоВ
//вызывать методы внтури клиентского объекта-приемника
//ИХ МОЖНО НАГЛЯДНО УВИДЕТЬ В OLEVIEW