using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace UdpReader.Udp
{
    /// <summary>
    /// Класс для считывания udp пакетов
    /// </summary>
    class UdpReader
    {
        /// <summary>
        /// Ip адрес прослушивания
        /// </summary>
        public string Ip { get; set; }  
        
        /// <summary>
        /// Порт прослушивания
        /// </summary>
        public int Port { get; set; }

        /// <summary>
        /// Сокет для работы с подключением
        /// </summary>
        private Socket Socket { get; set; }

        /// <summary>
        /// Буфер приходящих данных
        /// </summary>
        private byte[] Buffer { get; set; } = new byte[512];

        /// <summary>
        /// Базовый конструктор
        /// </summary>
        /// <param name="Ip">Ip адрес прослушивания</param>
        /// <param name="Port">Порт прослушивания</param>
        public UdpReader(string Ip, int Port)
        {
            try
            {
                this.Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

                IPEndPoint IPEndPoint = new IPEndPoint(IPAddress.Parse(Ip), Port);

                this.Socket.Bind(IPEndPoint);

                Console.WriteLine("Сокет успешно создан!");
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }           
        }

        /// <summary>
        /// Запуск принятия покетов сокетом
        /// </summary>
        public void Listen()
        {
            Task ListeningTask = new Task(ListeningProcess);

            ListeningTask.Start();

            Console.WriteLine("Прослушивание сокета запущено");
        }

        /// <summary>
        /// Остановка принятия пакетов сокетом и его отключение
        /// </summary>
        public void Stop()
        {
            try
            {
                if (this.Socket != null)
                {
                    this.Socket.Shutdown(SocketShutdown.Both);
                    this.Socket.Close();
                }

                Console.WriteLine("Прослушивание сокета завершено");
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// Метод процесса чтения
        /// </summary>
        private void ListeningProcess()
        {
            try
            {
                while (true)
                {
                    StringBuilder StrBuilder = new StringBuilder();

                    //Кол-во принимаемых байтов
                    int BytesCount = 0;

                    //Получение Ip адреса с которого пришли данные
                    EndPoint RemoteIp = new IPEndPoint(IPAddress.Any, 0);

                    //Принятие данных с udp пакетов
                    do
                    {
                        BytesCount = this.Socket.ReceiveFrom(this.Buffer, ref RemoteIp);
                        StrBuilder.Append(Encoding.Unicode.GetString(this.Buffer, 0, BytesCount));
                    }
                    while (this.Socket.Available > 0);

                    IPEndPoint RemoteFullIp = RemoteIp as IPEndPoint;

                    /*for(int i = 0; i < 512; i++)
                    {
                        StrBuilder.Append(this.Buffer[i].ToString());
                    }*/

                    Console.WriteLine(RemoteFullIp.Address.ToString() + ":" + RemoteFullIp.Port.ToString() + "|" + StrBuilder.ToString());

                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                this.Stop();

                Console.WriteLine("Произошла принудительная остановка сокета");
            }
        }
    }
}
