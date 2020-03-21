using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace EasyFileTransfer
{
    public class EftServer
    {
        public string SaveTo;
        public int Port;
        TcpListener obj_server;
        public EftServer(string SaveTo, int Port)
        {
            this.SaveTo = SaveTo;
            this.Port = Port;
            obj_server = new TcpListener(IPAddress.Any, Port);
        }

        /// <summary>
        /// Start EftServer and listening on port
        /// </summary>
        public void StartServer()
        {
            obj_server.Start();
            while (true)
            {
                TcpClient tc = obj_server.AcceptTcpClient();
                SocketHandler obj_hadler = new SocketHandler(tc, SaveTo);
                System.Threading.Thread obj_thread = new System.Threading.Thread(obj_hadler.ProcessSocketRequest);
                obj_thread.Start();
            }
        }
    }

    class SocketHandler
    {
        NetworkStream ns;
        string SaveTo;
        public SocketHandler(TcpClient tc, string SaveTo)
        {
            this.SaveTo = SaveTo;
            ns = tc.GetStream();
        }

        public void ProcessSocketRequest()
        {
            FileStream fs = null;
            long current_file_pointer = 0;
            Boolean loop_break = false;
            while (true)
            {
                //byte[] readPbValue = ReadStream();
                if (ns.ReadByte() == 2)
                {
                    byte[] cmd_buffer = new byte[3];
                    ns.Read(cmd_buffer, 0, cmd_buffer.Length);
                    byte[] recv_data = ReadStream();
                    switch (Convert.ToInt32(Encoding.UTF8.GetString(cmd_buffer)))
                    {
                        case 101:
                            //download++;
                            break;
                        case 125:
                            {
                                fs = new FileStream(@"" + SaveTo + Encoding.UTF8.GetString(recv_data), FileMode.CreateNew);
                                byte[] data_to_send = CreateDataPacket(Encoding.UTF8.GetBytes("126"), Encoding.UTF8.GetBytes(Convert.ToString(current_file_pointer)));
                                ns.Write(data_to_send, 0, data_to_send.Length);
                                ns.Flush();
                            }
                            break;
                        case 127:
                            {
                                fs.Seek(current_file_pointer, SeekOrigin.Begin);
                                fs.Write(recv_data, 0, recv_data.Length);
                                current_file_pointer = fs.Position;
                                byte[] data_to_send = CreateDataPacket(Encoding.UTF8.GetBytes("126"), Encoding.UTF8.GetBytes(Convert.ToString(current_file_pointer)));
                                ns.Write(data_to_send, 0, data_to_send.Length);
                                ns.Flush();
                            }
                            break;
                        case 128:
                            {
                                fs.Close();
                                loop_break = true;
                            }
                            break;
                        default:
                            break;
                    }
                }
                if (loop_break == true)
                {
                    ns.Close();
                    break;
                }
            }
        }

        public byte[] ReadStream()
        {
            byte[] data_buff = null;

            int b = 0;
            string buff_Length = "";
            while ((b = ns.ReadByte()) != 4)
            {
                buff_Length += (char)b;
            }
            int data_Length = Convert.ToInt32(buff_Length);
            data_buff = new byte[data_Length];
            int byte_Read = 0;
            int byte_Offset = 0;
            while (byte_Offset < data_Length)
            {
                byte_Read = ns.Read(data_buff, byte_Offset, data_Length - byte_Offset);
                byte_Offset += byte_Read;
            }

            return data_buff;
        }

        private byte[] CreateDataPacket(byte[] cmd, byte[] data)
        {
            byte[] initialize = new byte[1];
            initialize[0] = 2;
            byte[] separator = new byte[1];
            separator[0] = 4;
            byte[] dataLength = Encoding.UTF8.GetBytes(Convert.ToString(data.Length));
            MemoryStream ms = new MemoryStream();
            ms.Write(initialize, 0, initialize.Length);
            ms.Write(cmd, 0, cmd.Length);
            ms.Write(dataLength, 0, dataLength.Length);
            ms.Write(separator, 0, separator.Length);
            ms.Write(data, 0, data.Length);

            return ms.ToArray();
        }
    }
}
