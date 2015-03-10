﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Kinect;

namespace CURELab.SignLanguage.HandDetector
{
    public class SocketManager
    {
        private static SocketManager Instance;
        private TcpClient client;
        private NetworkStream ns;
        private StreamWriter sw;
        private string SPLIT = "#TERMINATOR#";
        public static SocketManager GetInstance(string addr, int port)
        {
            if (Instance == null)
            {
                Instance = new SocketManager(addr, port);
            }
            return Instance;
        }

        public static SocketManager GetInstance()
        {
            return Instance;
        }
        private SocketManager(string addr, int port)
        {
            client = new TcpClient();
            IPAddress ipa = IPAddress.Parse(addr);
            IPEndPoint ipe = new IPEndPoint(ipa, port);

            Console.WriteLine("connecting");
            client.Connect(ipe);

            if (client.Connected)
            {
                Console.WriteLine("connected");
                ns = client.GetStream();
                sw = new StreamWriter(ns);
            }

        }

        public string GetResponse()
        {
            if (ns != null)
            {
                try
                {
                    byte[] myReadBuffer = new byte[1024];
                    var numberOfBytesRead = ns.Read(myReadBuffer, 0, myReadBuffer.Length);
                    var s = Encoding.ASCII.GetString(myReadBuffer, 0, numberOfBytesRead);
                    return s.Trim();

                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    ns.Close();
                    ns = null;
                }
            }
            return null;
        }

        public string GetResponse(string msg)
        {
            if (ns != null)
            {
                msg += "#&";
                Byte[] data = System.Text.Encoding.ASCII.GetBytes(msg);
                ns.Write(data, 0, data.Length);
                // Buffer to store the response bytes.
                if (ns.CanRead)
                {
                    byte[] myReadBuffer = new byte[1024];
                    StringBuilder myCompleteMessage = new StringBuilder();
                    int numberOfBytesRead = 0;

                    // Incoming message may be larger than the buffer size. 
                    do
                    {
                        numberOfBytesRead = ns.Read(myReadBuffer, 0, myReadBuffer.Length);
                        myCompleteMessage.AppendFormat("{0}", Encoding.ASCII.GetString(myReadBuffer, 0, numberOfBytesRead));
                    }
                    while (ns.DataAvailable);
                    //return myCompleteMessage.ToString().Split(SPLIT, StringSplitOptions.RemoveEmptyEntries);
                    return myCompleteMessage.ToString();
                }
                else
                {
                    Console.WriteLine("Sorry.  You cannot read from this NetworkStream.");
                }

            }
            return null;

        }
        public delegate string AsyncBitmapCaller(Bitmap bmp);
        public delegate string AsyncMsgCaller(string bmp);

        public void GetResponseAsync(Bitmap img, AsyncCallback callback)
        {
            //var ac = new AsyncBitmapCaller(SendData);
            //ac.BeginInvoke(img, callback, "states");
        }

        public void GetResponseAsync(String msg, AsyncCallback callback)
        {
            var ac = new AsyncMsgCaller(GetResponse);
            ac.BeginInvoke(msg, callback, "states");
        }

        public string SendData(HandShapeModel model, Skeleton skeleton)
        {
            
            if (sw != null)
            {
                try
                {
                    var data = FrameConverter.Encode(model, skeleton);
                    sw.Write(data);
                    sw.Write(SPLIT);
                    sw.Flush();
                }
                catch (Exception)
                {
                    return null;
                }
               
            }
            return "TODO";
        }

        public string SendData(Bitmap bmp)
        {
            if (sw != null)
            {
                var data = FrameConverter.Encode(bmp);
                sw.Write(data);
                sw.Write(SPLIT);
                sw.Flush();
            }
            return "TODO";
        }

        public void SendEnd()
        {
            if (sw != null)
            {
                try
                {
                    sw.Write(FrameConverter.Encode("End"));
                    sw.Write(SPLIT);
                    sw.Flush();
                }
                catch (Exception)
                {
                }
            }
        }
        public string GetResponse(Bitmap img)
        {
            if (ns != null)
            {
                //msg += "#&";

                byte[] imageData;
                using (var stream = new MemoryStream())
                {
                    img.Save(stream, ImageFormat.Jpeg);
                    imageData = stream.ToArray();
                }
                StreamWriter sw = new StreamWriter(ns);
                var lengthData = BitConverter.GetBytes(imageData.Length);
                // ns.Write(lengthData, 0, lengthData.Length);
                ns.Write(imageData, 0, imageData.Length);
                sw.Write("SPLIT");
                sw.Flush();
                // Buffer to store the response bytes.
                if (ns.CanRead)
                {
                    byte[] myReadBuffer = new byte[1024];
                    StringBuilder myCompleteMessage = new StringBuilder();
                    int numberOfBytesRead = 0;
                    int total = 0;
                    // Incoming message may be larger than the buffer size. 
                    do
                    {
                        numberOfBytesRead = ns.Read(myReadBuffer, 0, myReadBuffer.Length);
                        total += numberOfBytesRead;
                        myCompleteMessage.AppendFormat("{0}", Encoding.ASCII.GetString(myReadBuffer, 0, numberOfBytesRead));
                    }
                    while (ns.DataAvailable);
                    Console.WriteLine(total.ToString() + " bytes received");
                    //return myCompleteMessage.ToString().Split(SPLIT, StringSplitOptions.RemoveEmptyEntries);
                    return myCompleteMessage.ToString();
                }
                else
                {
                    Console.WriteLine("Sorry.  You cannot read from this NetworkStream.");
                }

            }
            return null;

        }
    }
}