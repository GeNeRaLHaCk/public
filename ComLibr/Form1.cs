using COMLibrBAS;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TrackBar;

namespace COMLibr
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            backgroundWorker1.WorkerReportsProgress = true;
            backgroundWorker1.WorkerSupportsCancellation = true;
            ToolTip t = new ToolTip();
            t.SetToolTip(textBox1, "Введите IP адрес панели BAS-IP");
            t.SetToolTip(textBox2, "Введите логин пользователя BAS-IP");
            t.SetToolTip(textBox3, "Введите пароль пользователя BAS-IP");
        }

        ~Form1()
        {
            Dispose();
            GC.Collect();
        }

        public string dateTime = (DateTime.Now + "." + DateTime.Now.Millisecond).ToString();
        public HttpWebRequest request;
        public HttpWebResponse response;
        public static Socket socket;
        public const int Port = 80;
        public string _ip;
        public string _port;
        public string _login;
        public string _password;
        public string isAuth;
        public string isConnection;
        public string nameOfDevice;
        DriverClass driver = new DriverClass();
        public TcpListener _listner;
        public Socket _sock;
        public string firstMessage = null;

        public void CheckStatus(string ip, string port)
        {
            try
            {
                isConnection = "Error";
                isAuth = "Error";
                string Uri = "http://" + ip + ":" + port + "/api/v0/sip/status";
                request = (HttpWebRequest)WebRequest.Create(Uri);
                string passmd5 = driver.Encrypt(textBox3.Text);
                driver.ipPart = ip;
                string result = driver.Auth(ip, port, textBox2.Text, passmd5);
                if (result.Contains("OK"))
                {
                    nameOfDevice = driver.GetDeviceName(ip, port, driver.token);
                    isConnection = "Yes";
                    isAuth = "Yes";
                }
                else
                {
                    nameOfDevice = driver.GetDeviceName(ip, port, driver.token);
                    if (nameOfDevice == "BAS-IP multi-apartment panel")
                    {
                        driver.LogOut(ip, port, driver.token);
                    }
                    request.Timeout = 100;
                    response = (HttpWebResponse)request.GetResponse();
                    response.Close();
                    isAuth = "Ok";
                    button1.Enabled = true;
                }

                MessageBox.Show("Панель: \"" + nameOfDevice + "\"" + Environment.NewLine +
                    Environment.NewLine + "Подключение:\"" + isConnection +
                    "\"" + Environment.NewLine + Environment.NewLine +
                    "Авторизация:\"" + isAuth + "\"", "");
                
            }
            
            catch (WebException ex)
            {
                string answer = driver.HandleException(ex);
                if (answer.Contains("Время ожидания"))
                {
                    isAuth = "Error";
                    isConnection = "Error";
                    MessageBox.Show("Панель: \"" + nameOfDevice + "\"" + Environment.NewLine +
                    Environment.NewLine + "Подключение:\"" + isConnection +
                    "\"" + Environment.NewLine + Environment.NewLine +
                    "Авторизация:\"" + isAuth + "\"", "");
                }
                else
                {
                    button1.Enabled = true;
                    MessageBox.Show("Панель: \"" + nameOfDevice + "\"" + Environment.NewLine +
                    Environment.NewLine + "Подключение:\"" + isConnection +
                    "\"" + Environment.NewLine + Environment.NewLine +
                    "Авторизация:\"" + isAuth + "\"", "");
                }
            }

        }

        public static DateTime UnixTimestampToDateTime(double unixTime)
        {
            DateTime origin = new DateTime(1970, 1, 1, 3, 0, 0);
            return origin.AddMilliseconds(unixTime);
        }

        public static long DateTimeToUnixTimestamp(DateTime dateTime)
        {
            var timeSpan = (dateTime - new DateTime(1970, 1, 1, 3, 0, 0));
            return (long)timeSpan.TotalMilliseconds;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            CheckStatus(textBox1.Text, textBox4.Text);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            _ip = textBox1.Text;
            _port = textBox4.Text;
            _login = textBox2.Text;
            _password = textBox3.Text;
            Close();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (_port == "")
            {
                _port = "80";
            }
            textBox1.Text = _ip;
            textBox2.Text = _login;
            textBox3.Text = _password;
            textBox4.Text = _port;
            dateTimePicker1.Value = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            dateTimePicker2.Value = DateTime.Now;
        }

        public void AddDebugRow(string text, bool dNotClear, string typeOfLog)
        {
            using (StreamWriter file = new StreamWriter(CreateNameOfLog(textBox1.Text,
                typeOfLog), dNotClear))
            {
                file.Write(text);
                file.Close();
            }
        }

        public string CreateNameOfLog(string ip, string typeOfLog)
        {
            typeOfLog.ToLower();
            return "C:\\ProgramData\\Artonit\\bas-ip\\" + ip + typeOfLog + ".log";
        }

        public List<DriverClass.ListItem> returnAllListOfCardsBASMAP
            (string ip, string port, string token) //Вернуть список всех карт BASMAP
        {
            try
            {
                int i;
                List<DriverClass.ListItem> listOfCards = new List<DriverClass.ListItem>();
                listOfCards.Clear();
                progressBar1.Maximum = int.Parse(driver.countPagesOfCards);
                for (i = 0; i < int.Parse(driver.countPagesOfCards); i++)
                {
                    backgroundWorker1.ReportProgress(i);
                    int page = i + 1;
                    string Uri = "http://" + ip + ":" + port +
                    "/api/v0/access/identifiers/items/list?page_number=" + page + "&limit=50";
                    string receiveData = driver.SendQuery(Uri, token, "GET", null);
                    var jsonDes = JsonConvert.DeserializeObject<DriverClass.ListJSON>(receiveData);
                    IEnumerable<DriverClass.ListItem> listItems = jsonDes.list_items;
                    int ii;
                    for (ii = 0; ii < listItems.Count(); ii++)
                    {
                        listOfCards.Add(listItems.ToList()[ii]);
                    }
                }
                progressBar1.Value = 0;
                textBox5.Text = "";
                progressBar1.Maximum = listOfCards.Count();
                driver.AddDebugRow("", false, "_cards");
                int iii;
                for (iii = 0; iii < listOfCards.Count(); iii++)
                {
                    driver.AddDebugRow(listOfCards[iii].identifier_uid + " | " +
                    listOfCards[iii].@base.identifier_owner.name + " | " +
                    listOfCards[iii].@base.identifier_number + " | " +
                    listOfCards[iii].@base.@lock + ";", 
                        true, "_cards");
                    backgroundWorker1.ReportProgress(i);
                    textBox5.Text += listOfCards[iii].identifier_uid + " | " +
                    listOfCards[iii].@base.identifier_owner.name + " | " +
                    listOfCards[iii].@base.identifier_number + " | " +
                    listOfCards[iii].@base.@lock + ";\r\n";
                }
                textBox5.Text += "Количество карт:" + driver.countItemsOfCardsBASMAP + Environment.NewLine;
                progressBar1.Value = 0;
                return listOfCards;
            }
            catch (WebException ex)
            {
                MessageBox.Show(ex.Message, "Ошибка");
                return null;
            }
        }

        public List<DriverClass.ListItemSmall> returnAllListOfCardsBASDevice
            (string ip, string port, string token)//Вернуть список всех карт BASDevice
        {
            string Uri = "http://" + ip + ":" + port + "/api/v0/access/legacy/card/items/list";
            List<DriverClass.ListItemSmall> listOfCards = new List<DriverClass.ListItemSmall>();
            string receiveData = driver.SendQuery(Uri, token, "GET", null);
            var jsonDes = JsonConvert.DeserializeObject<DriverClass.ListItemsSmall>(receiveData);
            IEnumerable<DriverClass.ListItemSmall> listItems = jsonDes.list;
            progressBar1.Maximum = listItems.Count();
            int ii;
            for (ii = 0; ii < listItems.Count(); ii++)
            {
                backgroundWorker1.ReportProgress(ii);
                listOfCards.Add(listItems.ToList()[ii]);
            }
            progressBar1.Value = 0;
            textBox5.Text = "";
            int iii;
            textBox5.Text += "idCard | apartment | building | floor | unit" + Environment.NewLine;
            progressBar1.Maximum = listOfCards.Count();
            driver.AddDebugRow("", false, "_cards");
            for (iii = 0; iii < listOfCards.Count(); iii++)
            {
                driver.AddDebugRow(listOfCards[iii].card_id + " | " + listOfCards[iii].apartment
                    + " | " + listOfCards[iii].building + " | " + listOfCards[iii].floor
                    + " | " + listOfCards[iii].unit + ";"
                    , true, "_cards");
                backgroundWorker1.ReportProgress(iii);
                textBox5.Text += listOfCards[iii].card_id + " | " + listOfCards[iii].apartment
                    + " | " + listOfCards[iii].building + " | " + listOfCards[iii].floor
                    + " | " + listOfCards[iii].unit + ";"
                    + Environment.NewLine;
            }
            progressBar1.Value = 0;
            textBox5.Text += "Количество карт:" + jsonDes.count + Environment.NewLine;
            return listOfCards;
        }

        public List<DriverClass.ListItemsJournal> returnAllListOfEvents
            (string ip, string port, string token) //Вернуть список всех событий
        {
            try
            {
                int i;
                List<DriverClass.ListItemsJournal> listOfEvents = new List<DriverClass.ListItemsJournal>();
                listOfEvents.Clear();
                progressBar1.Maximum = int.Parse(driver.countPagesOfJournal);
                for (i = 0; i < int.Parse(driver.countPagesOfJournal); i++)
                {
                    backgroundWorker1.ReportProgress(i);
                    int page = i + 1;
                    string Uri = "http://" + ip + ":" + port + "/api/v0/log/list?locale=ru&limit=" +
                        "50&page_number=" + page + "&sort_type=asc";
                    string receiveData = driver.SendQuery(Uri, token, "GET", null);
                    var jsonDes = JsonConvert.DeserializeObject<DriverClass.Journal>(receiveData);
                    IEnumerable<DriverClass.ListItemsJournal> listItems = jsonDes.list_items;
                    int ii;
                    for (ii = 0; ii < listItems.Count(); ii++)
                    {
                        listOfEvents.Add(listItems.ToList()[ii]);
                    }
                }
                progressBar1.Value = 0;
                AddDebugRow("", false, "events");
                int iii;
                progressBar1.Maximum = listOfEvents.Count();
                for (iii = 0; iii < listOfEvents.Count(); iii++)
                {
                    backgroundWorker1.ReportProgress(iii);
                    AddDebugRow(
                        UnixTimestampToDateTime(listOfEvents[iii].timestamp) +
                        " | " + listOfEvents[iii].category + " | " + listOfEvents[iii].name.localized +
                        " | " + listOfEvents[iii].priority + " | " + listOfEvents[iii].info.localized +
                        ";\r\n",
                        true, "events");
                }
                textBox5.Text += "Количество событий:" + driver.countItemsOfJournal + Environment.NewLine;
                progressBar1.Value = 0;
                return listOfEvents;
            }
            catch (WebException ex)
            {
                MessageBox.Show(ex.Message, "Ошибка");
                return null;
            }
        }

        private void button3_Click(object sender, EventArgs e) //Получить список событий/карт
        {
            if (backgroundWorker1.IsBusy != true)
            {
                button3.Enabled = false;
                backgroundWorker1.WorkerReportsProgress = true;
                backgroundWorker1.RunWorkerAsync();
            }
            else
            {

            }
        }

        private void textBox4_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= '0' && e.KeyChar <= '9') || e.KeyChar == (char)Keys.Back)
            {
            }
            else
            {
                e.Handled = true;
            }
        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if ((e.KeyChar >= 'A' && e.KeyChar <= 'Z') || (e.KeyChar >= 'a' && e.KeyChar <= 'z') ||
                (e.KeyChar >= '0' && e.KeyChar <= '9') || e.KeyChar == '.' || e.KeyChar == '/' ||
                 e.KeyChar == (char)Keys.Back)
            {
            }
            else
            {
                e.Handled = true;
            }
        }

        private void backgroundWorker1_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            try
            {
                button3.Enabled = false;
                textBox5.Text = "";
                label5.Text = "Авторизация";
                string passmd5 = driver.Encrypt(textBox3.Text);
                driver.ipPart = textBox1.Text;
                if (driver.Auth(textBox1.Text, textBox4.Text, textBox2.Text, passmd5) == "OK")
                {
                    nameOfDevice = driver.GetDeviceName(textBox1.Text, textBox4.Text, driver.token);
                    string responseTotalCards = driver.ReturnTotalCards(textBox1.Text,
                        textBox4.Text, driver.token);
                    if (nameOfDevice == "BAS-IP multi-apartment panel")
                    {
                        string responseTotalEvents = driver.ReturnTotalEvents(textBox1.Text, textBox4.Text);
                        label5.Text = "Выгрузка списка всех карт";
                        returnAllListOfCardsBASMAP(textBox1.Text, textBox4.Text, driver.token);
                        label5.Text = "Выгрузка списка всех событий";
                        //returnAllListOfEvents(textBox1.Text, textBox4.Text, driver.token);
                        textBox5.Text += GetRemotes(textBox1.Text, textBox4.Text, driver.token) +
                        Environment.NewLine;
                        progressBar1.Value = 0;
                    }
                    else if (nameOfDevice == "BAS-IP device")
                    {
                        label5.Text = "Выгрузка списка всех карт";
                        returnAllListOfCardsBASDevice(textBox1.Text, textBox4.Text, driver.token);
                        textBox5.Text += "Журнал событий этой серией домофонов не поддерживается " +
                            Environment.NewLine;
                    }
                    string time = driver.GetDeviceTime(textBox1.Text, textBox4.Text, driver.token);
                    textBox5.Text += "Time: " + time + Environment.NewLine + "Name: "
                        + nameOfDevice + Environment.NewLine;
                    //driver.LogOut(textBox1.Text, textBox4.Text, driver.token);
                    backgroundWorker1.ReportProgress(0);
                    button3.Enabled = true;
                    label5.Text = "";
                }
                else MessageBox.Show("Устройство не на линии");
            }
            catch (WebException ex)
            {
                button3.Enabled = true;
                string definition = driver.HandleException(ex);
                MessageBox.Show(ex.Message + Environment.NewLine +
                    Environment.NewLine + definition);
                backgroundWorker1.CancelAsync();
            }
        }

        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar1.Value = e.ProgressPercentage;
        }

        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {
                if (e.Cancelled)
                {
                    MessageBox.Show("Process was cancelled");
                    progressBar1.Value = 0;
                }
                else if (e.Error != null)
                {
                    MessageBox.Show(e.Error.Message + Environment.NewLine +
                        "There was an error running the process. The thread aborted");
                    button3.Enabled = true;
                    progressBar1.Value = 0;
                }
                else
                {
                    MessageBox.Show("Process was completed");
                    progressBar1.Value = 0;
                    button3.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public string GetRemotes(string ip, string port, string token)
        {
            string Uri = "http://" + ip + ":" + port + "/api/v0/network/management/remotes";
            string receiveData = driver.SendQuery(Uri, token, "GET", null);
            var remote = JsonConvert.DeserializeObject<DriverClass.Remote>(receiveData);
            return "Remote: link: " + remote.link_url + "; Password: " + 
                remote.link_password +
                "; RealtimeLogging: " + remote.realtime_logging + "; Heartbeat:" +
                remote.heartbeat + "; Enable:" + remote.link_enable +
                Environment.NewLine;
        }

        public List<DriverClass.ListItemsJournal> GetEventsFromNTo
            (string ip, string port, string token)  
        {
            try
            {
                label5.Text = "Загрузка событий с панели";
                DateTime dateFrom = dateTimePicker1.Value;
                DateTime dateTo = dateTimePicker2.Value;
                string Uri = @"http://" + ip + ":" + port +
                    "/api/v0/log/list?locale=ru&limit=50&sort_type=asc&from="
                    + DateTimeToUnixTimestamp(dateFrom) + "&to=" + 
                    DateTimeToUnixTimestamp(dateTo);
                var receiveData = driver.SendQuery(Uri, token, "GET", null);
                var jsonDes = JsonConvert.DeserializeObject<DriverClass.Journal>(receiveData);
                int totalPages = jsonDes.list_option.pagination.total_pages;
                List<DriverClass.ListItemsJournal> listOfEvents = 
                    new List<DriverClass.ListItemsJournal>();
                listOfEvents.Clear();
                progressBar1.Value = 0;
                if (totalPages > 1)
                {
                    progressBar1.Maximum = totalPages + 1;
                    backgroundWorker2.ReportProgress(0);
                    int i;
                    for (i = 0; i < totalPages + 1; i++)
                    {
                        int page = i + 1;
                        backgroundWorker2.ReportProgress(i);
                        string UriBrute = "http://" + ip + ":" + port +
                                        "/api/v0/log/list?locale=ru&limit=50&page_number=" +
                                        page + "&sort_type=asc&from=" + 
                                        DateTimeToUnixTimestamp(dateFrom) + "&to=" + 
                                        DateTimeToUnixTimestamp(dateTo);
                        string receiveDataBrute = driver.SendQuery(UriBrute, token, "GET",
                            null);
                        var jsonDesBrute = JsonConvert.DeserializeObject
                            <DriverClass.Journal>(receiveDataBrute);
                        IEnumerable<DriverClass.ListItemsJournal> listItems =
                            jsonDesBrute.list_items;
                        int ii;
                        for (ii = 0; ii < listItems.Count(); ii++)
                        {
                            listOfEvents.Add(listItems.ToList()[ii]);
                        }
                    }
                    progressBar1.Value = 0;
                    AddDebugRow(
                        "From:" + dateFrom + " To: " + dateTo + Environment.NewLine,
                        false, "eventsFromTo");
                    int iii;
                    label5.Text = "Запись событий в текстовый документ";
                    progressBar1.Maximum = listOfEvents.Count();
                    backgroundWorker2.ReportProgress(0);
                    for (iii = 0; iii < listOfEvents.Count(); iii++)
                    {
                        backgroundWorker2.ReportProgress(iii);
                        AddDebugRow(
                            UnixTimestampToDateTime(listOfEvents[iii].timestamp) +
                            " | " + listOfEvents[iii].category + " | " +
                            listOfEvents[iii].name.localized +
                            " | " + listOfEvents[iii].priority + " | " +
                            listOfEvents[iii].info.localized +
                            Environment.NewLine,
                            true, "eventsFromTo");
                    }
                    backgroundWorker2.ReportProgress(0);
                    label5.Text = "";
                    progressBar1.Value = 0;
                    return listOfEvents;
                }
                else if (totalPages == 0)
                {
                    backgroundWorker2.ReportProgress(0);
                    AddDebugRow(
                    "From:" + dateFrom + " To: " + dateTo + " null" + Environment.NewLine,
                    false, "eventsFromTo");
                    label5.Text = "";
                    progressBar1.Value = 0;
                    return null;
                }
                else
                {
                    label5.Text = "Загрузка событий с панели";
                    IEnumerable<DriverClass.ListItemsJournal> listItems = 
                        jsonDes.list_items;
                    int ii;
                    progressBar1.Maximum = listItems.Count();
                    for (ii = 0; ii < listItems.Count(); ii++)
                    {
                        backgroundWorker2.ReportProgress(ii);
                        listOfEvents.Add(listItems.ToList()[ii]);
                    }
                    backgroundWorker2.ReportProgress(0);
                    progressBar1.Value = 0;
                    AddDebugRow(
                    "From:" + dateFrom + " To: " + dateTo + Environment.NewLine,
                    false, "eventsFromTo");
                    label5.Text = "Запись событий в текстовый документ";
                    int iii;
                    progressBar1.Maximum = listOfEvents.Count();
                    for (iii = 0; iii < listOfEvents.Count(); iii++)
                    {
                        backgroundWorker2.ReportProgress(iii);
                        AddDebugRow(
                            UnixTimestampToDateTime(listOfEvents[iii].timestamp) +
                            " | " + listOfEvents[iii].category + " | " + 
                            listOfEvents[iii].name.localized +
                            " | " + listOfEvents[iii].priority + " | " + 
                            listOfEvents[iii].info.localized +
                            Environment.NewLine,
                            true, "eventsFromTo");
                    }
                    backgroundWorker2.ReportProgress(0);
                    progressBar1.Value = 0;
                    label5.Text = "";
                    return listOfEvents;
                }
            }
            catch (WebException ex)
            {
                label5.Text = "";
                MessageBox.Show(driver.HandleException(ex));
                progressBar1.Value = 0;
                backgroundWorker2.ReportProgress(0);
                return null;
            }
        }

        private void button4_Click(object sender, EventArgs e) // Получение событий от даты до даты
        {
            if (backgroundWorker2.IsBusy != true)
            {
                backgroundWorker2.WorkerReportsProgress = true;
                backgroundWorker2.RunWorkerAsync();
            }
            else
            {

            }
            backgroundWorker2.Dispose();
        }

        private void ServerHandler(object state, System.ComponentModel.DoWorkEventArgs e)
        {
            try
            {
                _listner = new TcpListener(IPAddress.Any, 0);
                _listner.Start();
            }
            catch (SocketException ex)
            {
                AddDebugRow("ERR SocketEX:" + ex.Message + "\r\n", true, "dumphttp");
            }
            firstMessage = null;
            string endPoint = _listner.LocalEndpoint.ToString();
            string[] partsOfPoint = endPoint.Split(':');
            string port = partsOfPoint[1];
            string Host = Dns.GetHostName();
            string IP = Dns.GetHostByName(Host).AddressList[0].ToString();

            DriverClass.Remote remote = new DriverClass.Remote
            {
                link_url = IP + ":" + port,
                link_enable = true,
                link_password = "123456",
                realtime_logging = true,
                heartbeat = true
            };

            MessageBox.Show("Удаленный: " + remote.link_url);
            string jsonDes = JsonConvert.SerializeObject(remote);
            AddText("Server started - Listening on port:" + port);
            string Uri = "http://" + textBox1.Text + ":" + textBox4.Text + 
                "/api/v0/network/management/link";
            string receiveData = driver.SendQuery(Uri, driver.token, "POST", jsonDes);
            try
            {
                _sock = _listner.AcceptSocket();
            }
            catch (InvalidOperationException ex)
            {
                AddDebugRow("ERR SocketEX:" + ex.Message + "\r\n", true, "dumphttp");
            }
            catch (Exception ex)
            {
                AddDebugRow("ERR Unexpected error:" + ex.Message + "\r\n", true, "dumphttp");
            }
            AddDebugRow("Socket LocalEndPoint|RemoteEndPoint:" + _sock.LocalEndPoint.ToString() + "|" + 
                        _sock.RemoteEndPoint + "\r\n" + 
                        "Socket Available: " + _sock.Available + "\r\n" +
                        "Socket Connected: " + _sock.Connected + "\r\n" +
                        "Socket Send|Receive Timeout: " + _sock.SendTimeout + "|" + _sock.ReceiveTimeout + "\r\n",
                        true, "dumphttp");
            string jsonSer;
            AddText("User from IP " + _sock.RemoteEndPoint);
            string Str = "HTTP/1.1 200 OK\r\nContent-Length:0\r\nContent-type: text/html\r\n\r\n";
            byte[] bufferResponse = Encoding.ASCII.GetBytes(Str);
            string _headerPong = "POST /api/v0/devices/pong";
            string _headerLogs = "PUT /api/v0/devices/logs";
            if (_sock.Connected)
            {
                try
                {
                    while (true)
                    {
                        Thread.Sleep(3000);
                        byte[] bytes = new byte[4096];
                        int bytesRec = _sock.Receive(bytes);
                        AddDebugRow(bytesRec + "\r\n", true, "dumphttp");

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
                        AddDebugRow("Start position:" + startIndex + "\r\n", true, "dumphttp");
                        jsonSer = firstMessage.Substring(startIndex);
                        AddDebugRow("Serialized\r\n" + jsonSer + "\r\n", true, "dumphttp");
                        DriverClass.EventsJournal receiveJsonDes = JsonConvert.DeserializeObject<DriverClass.EventsJournal>(jsonSer);
                        AddDebugRow("Deserialize\r\n", true, "dumphttp");
                        IEnumerable<DriverClass.ObjectEvent> events = receiveJsonDes.events;
                        int i;
                        for (i = 0; i < events.Count(); i++)
                        {
                            var item = events.ToList()[i];

                            AddDebugRow(UnixTimestampToDateTime(item.created_at) + " | " +
                                item.code + " | " + item.category + " | " +
                                item.info.account_type + "|" + item.info.answered +
                                "|" + item.info.apartment_address + "|" + item.info.host +
                                "|" + item.info.@lock + "|" + item.info.number +
                                "|" + item.info.owner + "|" + item.info.type + " | " + item.priority + "\r\n",
                                true, "dumphttp");
                        }
                        AddDebugRow("\r\n", true, "dumphttp");
                    }
                    catch(Exception ex)
                    {
                        AddDebugRow("ERR First Deserialization or translation the information: " + ex.Message + "\r\n", true, "dumphttp"); return;
                    }
                }
            }
            Thread.Sleep(2000);
            AddDebugRow("------FIRST RECEIVE THE END------  \r\n", true, "dumphttp");

            try
            {
                //_sock = _listner.AcceptSocket();
                AddDebugRow("Socket LocalEndPoint|RemoteEndPoint:" + _sock.LocalEndPoint.ToString() + "|" +
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
            firstMessage = "";
            try
            {
                if (_sock.Connected)
                {
                    while (true)
                    {
                        byte[] _Buffer = new byte[4096];
                        int _DataReceived = _sock.Receive(_Buffer);
                        AddDebugRow(_DataReceived + "\r\n", true, "dumphttp");
                        AddText("Message Received...");
                        string _Message = Encoding.UTF8.GetString(_Buffer, 0, _DataReceived).Trim();

                        _sock.Send(bufferResponse);
                        if (_Message.IndexOf(_headerPong) > 0)
                        {
                            _Message = _Message.Substring(_Message.IndexOf(_headerLogs),
                                _Message.IndexOf(_headerPong));
                        } else { }
                        if (_Message.IndexOf(_headerPong) == 0)
                        {
                            //TO DO WatchDog
                        }
                        else if (_Message.IndexOf(_headerLogs) == 0)
                        {
                            AddDebugRow("--------------------------------------------------------------------------\r\n",
                                true, "dumphttp");
                            AddDebugRow(dateTime + " " + _Message + "\r\n", true, "dumphttp");
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
                                    AddDebugRow("Start position:" + startPosition + "\r\n", true, "dumphttp");
                                    jsonSer = _Message.Substring(startPosition);
                                    AddDebugRow("Serialized\r\n" + jsonSer + "\r\n", true, "dumphttp");
                                    DriverClass.EventsJournal receiveJsonDes = JsonConvert.DeserializeObject<DriverClass.EventsJournal>(jsonSer);
                                    AddDebugRow("Deserialize\r\n", true, "dumphttp");
                                    IEnumerable<DriverClass.ObjectEvent> events = receiveJsonDes.events;
                                    int i;
                                    for (i = 0; i < events.Count(); i++)
                                    {
                                        var item = events.ToList()[i];

                                        AddDebugRow(UnixTimestampToDateTime(item.created_at) + " | " +
                                            item.code + " | " + item.category + " | " +
                                            item.info.account_type + "|" + item.info.answered +
                                            "|" + item.info.apartment_address + "|" + item.info.host +
                                            "|" + item.info.@lock + "|" + item.info.number +
                                            "|" + item.info.owner + "|" + item.info.type + " | " + item.priority + "\r\n",
                                            true, "dumphttp");
                                    }
                                    AddDebugRow("\r\n", true, "dumphttp");
                                }
                                catch (Exception ex)
                                {
                                    AddDebugRow("Deserialization or translation ERROR (несущественно): "
                                        + ex.Message + "\r\n", true, "dumphttp"); return;
                                }
                            }
                        }
                        else
                        {
                            AddDebugRow(_Message + "\r\n", true, "dumphttp");
                        }
                        AddText(_Message);
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

        private void AddText(string text)
        {
            if (listBox1.InvokeRequired)
            {
                AddTextCallback d = new AddTextCallback(AddText);
                Invoke(d, new object[] { text });
            }
            else
            {
                this.listBox1.Items.Add(text);
            }
        }

        private void button5_Click(object sender, EventArgs e) // Start Server
        {
            if (nameOfDevice == "BAS-IP multi-apartment panel")
            {
                if (backgroundWorker3.IsBusy != true)
                {
                    button5.Enabled = false;
                    string passmd5 = driver.Encrypt(textBox3.Text);
                    driver.ipPart = textBox1.Text;
                    driver.Auth(textBox1.Text, textBox4.Text, textBox2.Text, passmd5);
                    nameOfDevice = driver.GetDeviceName(textBox1.Text, textBox4.Text, driver.token);
                    backgroundWorker3.WorkerReportsProgress = true;
                    backgroundWorker3.RunWorkerAsync();
                }
                else
                {

                }
                //WaitCallback waitCallback = new WaitCallback(ServerHandler);
                //ThreadPool.QueueUserWorkItem(waitCallback);
            }
            else if (nameOfDevice == "BAS-IP device") MessageBox.Show("Функция для этой " +
                "серии домофонов недоступна", "Ошибка");
        }

        delegate void AddTextCallback(string text);

        private void button6_Click(object sender, EventArgs e) // Stop Server
        {
            try
            {
                button5.Enabled = true;
                _sock.Shutdown(SocketShutdown.Both);
                _sock.Close();

                AddText("Client Disconnected.");
                _listner.Stop();
                AddText("Server Stop.");
            }
            catch(SocketException sockEx)
            {
                MessageBox.Show("SockEX: " + sockEx.Message);
            }
            catch(ObjectDisposedException objDisp)
            {
                MessageBox.Show("Object Disposed: " + objDisp.Message);
            }
            catch(Exception ex)
            {
                MessageBox.Show("Unexpected exception:" + ex.Message);
            }
            driver.LogOut(textBox1.Text, textBox4.Text, driver.token);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            Dispose();
            GC.Collect();
        }

        private void backgroundWorker2_DoWork(object sender, DoWorkEventArgs e)
        {
            string passmd5 = driver.Encrypt(textBox3.Text);
            driver.ipPart = textBox1.Text;
            button4.Enabled = false;
            if (driver.Auth(textBox1.Text, textBox4.Text, textBox2.Text, passmd5) == "OK")
            {
                nameOfDevice = driver.GetDeviceName(textBox1.Text, textBox4.Text, driver.token);
                if (nameOfDevice == "BAS-IP multi-apartment panel")
                {
                    GetEventsFromNTo(textBox1.Text, textBox4.Text, driver.token);
                }
                else if (nameOfDevice == "BAS-IP device") MessageBox.Show("Функция для этой " +
                    "серии домофонов недоступна", "Ошибка");
            }
            else MessageBox.Show("Устройство не на линии");
        }

        private void backgroundWorker2_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            try
            {
                if (e.Cancelled)
                {
                    label5.Text = "";
                    MessageBox.Show("Process was cancelled");
                    button4.Enabled = true;
                    progressBar1.Value = 0;
                }
                else if (e.Error != null)
                {
                    label5.Text = "";
                    MessageBox.Show(e.Error.Message + Environment.NewLine +
                        "There was an error running the process. The thread aborted");
                    button4.Enabled = true;
                    progressBar1.Value = 0;
                }
                else
                {
                    MessageBox.Show("Process was completed");
                    button4.Enabled = true;
                    progressBar1.Value = 0;
                    //driver.LogOut(textBox1.Text, textBox4.Text, driver.token);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void backgroundWorker2_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar1.Value = e.ProgressPercentage;
        }

        private void button7_Click(object sender, EventArgs e)
        {
            try
            {
                string Uri = "http://" + textBox1.Text + ":" + textBox4.Text +
                        "/api/v0/access/identifiers/items/list?limit=50&sort_field=identifier_number&sort_type=desc" +
                        "&filter_field=identifier_number" +
                        "&filter_type=equal&filter_format=string&filter_value=" + textBox7.Text.Trim();
                List<DriverClass.ListItem> list = new List<DriverClass.ListItem>();
                string passmd5 = driver.Encrypt(textBox3.Text);
                driver.ipPart = textBox1.Text;
                driver.Auth(textBox1.Text, textBox4.Text, textBox2.Text, passmd5);
                string receiveData = driver.SendQuery(Uri, driver.token, "GET", null);
                DriverClass.ListJSON json = JsonConvert.DeserializeObject<DriverClass.ListJSON>(receiveData);
                IEnumerable<DriverClass.ListItem> listItems = json.list_items;//.OrderBy(Item => Item.identifier_uid);
                int ii;
                for (ii = 0; ii < listItems.Count(); ii++)
                {
                    list.Add(listItems.ToList()[ii]);
                }
                textBox5.Text = "";
                int i;
                for (i = 0; i < list.Count(); i++)
                {
                    textBox5.Text +=
                    list[i].identifier_uid + " | " +
                    list[i].@base.identifier_owner.name + " | " +
                    list[i].@base.identifier_number + " | " +
                    list[i].@base.@lock + ";" + "\r\n";
                }

            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void textBox7_TextChanged(object sender, EventArgs e)
        {

        }

        private void button8_Click(object sender, EventArgs e)
        {
            try
            {
                string passmd5 = driver.Encrypt(textBox3.Text);
                driver.ipPart = textBox1.Text;
                driver.Auth(textBox1.Text, textBox4.Text, textBox2.Text, passmd5);
                string iD = textBox6.Text;
                string valueID = textBox8.Text;
                string door = textBox9.Text;
                var item = driver.FindId(textBox1.Text, textBox4.Text, driver.token, iD);
                if (item == null)
                {
                    MessageBox.Show("null");
                    return;
                }
                string Uri = "http://" + textBox1.Text + ":" + textBox4.Text +
                            "/api/v0/access/identifiers/item/" + item.identifier_uid;
                item.@base.identifier_number = valueID;
                item.@base.@lock = door;
                var jsonSer = JsonConvert.SerializeObject(item);
                string receiveData = driver.SendQuery(Uri, driver.token, "PATCH", jsonSer);
                MessageBox.Show(receiveData);
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
    }

