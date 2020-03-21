using EasyFileTransfer.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace EasyFileTransfer
{
    public class EftClient
    {
        /// <summary>
        /// Percentage of file send progress
        /// </summary>
        public static int ProgressValue;

        /// <summary>
        /// send file
        /// </summary>
        /// <param name="FilePath">file location that you want to send</param>
        /// <param name="TargetIP">IP Address of target system</param>
        /// <param name="Port">Server listen to this port</param>
        /// <returns>An object with status and description</returns>
        public static Response Send(string FilePath, string TargetIP, int Port)
        {
            try
            {
                string Selected_file = FilePath;
                string File_name = Path.GetFileName(Selected_file);
                FileStream fs = new FileStream(Selected_file, FileMode.Open);
                TcpClient tc = new TcpClient(TargetIP, Port);
                NetworkStream ns = tc.GetStream();
                byte[] data_tosend = CreateDataPacket(Encoding.UTF8.GetBytes("125"), Encoding.UTF8.GetBytes(File_name));
                ns.Write(data_tosend, 0, data_tosend.Length);
                ns.Flush();
                bool loop_break = false;
                while (true)
                {
                    if (ns.ReadByte() == 2)
                    {
                        byte[] cmd_buffer = new byte[3];
                        ns.Read(cmd_buffer, 0, cmd_buffer.Length);
                        byte[] recv_data = ReadStream(ns);
                        switch (Convert.ToInt32(Encoding.UTF8.GetString(cmd_buffer)))
                        {
                            case 126:
                                long recv_file_pointer = long.Parse(Encoding.UTF8.GetString(recv_data));
                                if (recv_file_pointer != fs.Length)
                                {
                                    fs.Seek(recv_file_pointer, SeekOrigin.Begin);
                                    int temp_buffer_length = (int)(fs.Length - recv_file_pointer < 20000 ? fs.Length - recv_file_pointer : 20000);
                                    byte[] temp_buffer = new byte[temp_buffer_length];
                                    fs.Read(temp_buffer, 0, temp_buffer.Length);
                                    byte[] data_to_send = CreateDataPacket(Encoding.UTF8.GetBytes("127"), temp_buffer);
                                    ns.Write(data_to_send, 0, data_to_send.Length);
                                    ns.Flush();
                                    ProgressValue = (int)Math.Ceiling((double)recv_file_pointer / (double)fs.Length * 100);
                                }
                                else
                                {
                                    byte[] data_to_send = CreateDataPacket(Encoding.UTF8.GetBytes("128"), Encoding.UTF8.GetBytes("Close"));
                                    ns.Write(data_to_send, 0, data_to_send.Length);
                                    ns.Flush();
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
                        return new Response { status = 1, description = "send successfully."};
                    }

                }
            }
            catch (Exception e)
            {
                return new Response { status = -1, description = "Error: " + e.Message};
            }
        }

        private static byte[] ReadStream(NetworkStream ns)
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

        private static byte[] CreateDataPacket(byte[] cmd, byte[] data)
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
