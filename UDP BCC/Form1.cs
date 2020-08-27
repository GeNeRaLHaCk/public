using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.IO;

namespace WindowsFormsApp2
{
    public partial class UDPSender : Form
    {
        //создаем экземпляр класса UdpClient и сразу присваиваем удаленную точку
        public static IPAddress ip;
        public static IPEndPoint point;
        public UdpClient client = new UdpClient();
        
        public UDPSender()
        {
            InitializeComponent();
            toolStripStatusLabel4.Text = Application.ProductVersion.ToString();
            client.Client.ReceiveTimeout = 5000;
        }
        public void button1_Click(object sender, EventArgs e)
        {
            try
            {
                UDP.UDPSocket s = new UDP.UDPSocket();
                s.Server("127.0.0.1", 0);
                UDP.UDPSocket c = new UDP.UDPSocket();
                c.Client(textBox1.Text, int.Parse(textBox2.Text));
                byte[] getver = { 0x03, 0x56, 0x55 };
                c.Send(getver);
                textBox4.Text += s.StrokaSend + s.StrokaReceive;
                // byte[] receiveBytes = client.Receive(ref point);
                // label7.Text = string.Join(" ", receiveBytes.Select(i => i.ToString("X2")));
                // byte[] arrayOD0 = { 0x4, 0x4F, 0x31, 0x7A };
                //client.Send(arrayOD0, arrayOD0.Length, pointUDP);
                // EndPoint point = new IPEndPoint(ip, int.Parse(textBox2.Text));
                //Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                //socket.Bind(new IPEndPoint(IPAddress.Any, int.Parse(textBox2.Text)));
                //socket.BeginReceiveFrom(request, 0, request.Length, SocketFlags.None, ref point, new AsyncCallback(AcceptReceiveCallback), socket);
                button2.Enabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "");
            }
        }
        //Отправка UTF-8
        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                button3.Enabled = false;
                byte[] sendBytes = Encoding.UTF8.GetBytes(textBox3.Text);
                int res = client.Send(sendBytes, sendBytes.Length);
                button3.Enabled = true;
            }
            catch(Exception ex)
            {
                button3.Enabled = true;
                MessageBox.Show(ex.Message, "");
            }
        }
        //send text "Тест"
        private void button4_Click(object sender, EventArgs e)
        {
            try
            {
                byte[] arrayOD0 = { 0x0D, 0x46, 0x30, 0x35, 0xD0, 0xA2, 0xD0, 0xB5, 0xD1, 0x81, 0xD1, 0x82, 0x5A };
                int res = client.Send(arrayOD0, arrayOD0.Length);
                // теперь принимаем ответ от контроллера
                IPAddress ip = IPAddress.Parse(textBox1.Text);// указываю IP контроллера
                IPEndPoint point = new IPEndPoint(ip, 0);
                //byte[] receiveBytes = client.Receive(ref point);
                label7.Text = "123";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "");
                label7.Text = "Err";
            }
        }
        //send 123456789
        private void button5_Click(object sender, EventArgs e)
        {
            try
            {
                byte[] arrayOD0 = { 0x0E, 0x46, 0x30, 0x35, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x7C };
                int res = client.Send(arrayOD0, arrayOD0.Length);
                // теперь принимаем ответ от контроллера
                IPAddress ip = IPAddress.Parse(textBox1.Text);// указываю IP контроллера
                //IPEndPoint point2 = new IPEndPoint(ip, int.Parse(textBox2.Text));
                IPEndPoint point2 = new IPEndPoint(ip, 0);
                //byte[] receiveBytes = client.Receive(ref point);
                label7.Text = "123";
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "");
                label7.Text = "Err";
            }
        }

       
        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                button2.Enabled = false;
                client.Close();
                client = new UdpClient();
                button1.Enabled = true;
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message, "");
            }
        }
        
        private void button6_Click(object sender, EventArgs e)
        {
            byte summ8 = (byte)(2);
            try
            {
                int summ = textBox7.Text.Length + 2;
                //summ = 6;
                if (summ < 256)
                {
                     summ8 = (byte)(summ);
                }
                else
                {
                    summ8 = (byte)(7);
                }
                byte[] CommandToSend = Encoding.UTF8.GetBytes(textBox7.Text);
                byte[] CommandLenght = { summ8 };
                byte[] sendBytes = CommandLenght.Concat(CommandToSend).ToArray();
                byte[] send = { CalculateBCC(sendBytes, sendBytes.Length) };
                button6.Enabled = false;
                byte[] sendData = sendBytes.Concat(send).ToArray();
                IPAddress ip = IPAddress.Parse(textBox1.Text);
                IPEndPoint point = new IPEndPoint(ip, int.Parse(textBox2.Text));
                var a = client.Send(sendData, sendData.Length);
                byte[] receiveBytes = client.Receive(ref point);
                label7.Text = string.Join(" ", receiveBytes.Select(i => i.ToString("X2")));
                button6.Enabled = true;
            }
            catch (Exception ex)
            {
                button6.Enabled = true;
                MessageBox.Show(ex.Message, "");
            }
        }

        private void textBox7_TextChanged(object sender, EventArgs e)
        {
            byte summ8 = (byte)(0);
            //int summ = textBox7.Text.Length + 2;
            int summ = Encoding.UTF8.GetBytes(textBox7.Text).Length + 2;
                if (summ < 256)
                {
                    summ8 = (byte)(textBox7.Text.Length + 2);
                }
                else 
                {
                    summ8 = (byte)(0);
                }
            byte[] CommandToSend = Encoding.UTF8.GetBytes(textBox7.Text);
            textBox6.Text = summ8.ToString();
            byte[] CommandLenght = { summ8 };
            byte[] sendBytes = CommandLenght.Concat(CommandToSend).ToArray();
            textBox8.Text = CalculateBCC(sendBytes, sendBytes.Length).ToString();
            byte[] send = { byte.Parse(CalculateBCC(sendBytes, sendBytes.Length).ToString()) };
        }
        public byte CalculateBCC(byte[] dataBCC, int BCCcount)
        {
            byte result = (byte)(0);
            for(int i = 0; i < BCCcount; i++)
            {
                result ^= dataBCC[i];
            }
            return result;
        }

        private void button7_Click(object sender, EventArgs e)
        {
            try 
            {
                IPAddress ip = IPAddress.Parse(textBox1.Text);
                IPEndPoint point = new IPEndPoint(ip, int.Parse(textBox2.Text));
                UdpClient clientcl = new UdpClient();
                byte[] arrayOD0 = { 0x4, 0x4F, 0x31, 0x7A };
                clientcl.Send(arrayOD0, arrayOD0.Length, "192.168.0.177", 1985);
                //int res = client.Send(arrayOD0, arrayOD0.Length);
                //clientcl.Send(arrayOD0, arrayOD0.Length);
                //byte[] receiveBytes = receive.Receive(ref point);
                //label7.Text = Encoding.ASCII.GetString(receiveBytes);
                // теперь принимаем ответ от контроллера
                //IPAddress ip = IPAddress.Parse(textBox1.Text);// указываю IP контроллера
                //IPEndPoint point = new IPEndPoint(ip, 0);
                //client.Client.Bind(point);
                byte[] receiveBytes = clientcl.Receive(ref point);
                label7.Text = string.Join(" ", receiveBytes.Select(i => i.ToString("X2")));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "");
                label7.Text = "Err";
            }
        }

        private void button8_Click(object sender, EventArgs e)
        {
            try
            {
                byte[] arrayOD0 = { 0x4, 0x4f, 0x32, 0x79 };
                int res = client.Send(arrayOD0, arrayOD0.Length);
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message,"");
            }
        }

    }
}
