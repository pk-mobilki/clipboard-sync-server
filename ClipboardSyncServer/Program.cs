using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ClipboardSyncServer.Properties;

namespace ClipboardSyncServer
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [MTAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MyCustomApplicationContext());
        }
    }

    enum MessageType
    {
        Data,
        Init
    }

    public class MyCustomApplicationContext : ApplicationContext
    {
        private NotifyIcon trayIcon;

        public MyCustomApplicationContext()
        {
            ShowBaloonTip("ClipboardSync", "Clipboard Sync Server is running");


            RunAsSTAThread(Listen);
        }

        private void ShowBaloonTip(string title, string text)
        {
// Initialize Tray Icon
            trayIcon = new NotifyIcon()
            {
                ContextMenu = new ContextMenu(new MenuItem[]
                {
                    new MenuItem("Exit", Exit)
                }),
                Visible = true
            };
            var notification = new System.Windows.Forms.NotifyIcon()
            {
                Visible = true,
                Icon = System.Drawing.SystemIcons.Application,
                BalloonTipIcon = System.Windows.Forms.ToolTipIcon.Info,
                BalloonTipTitle = title,
                BalloonTipText = text,
            };

            // Display for 5 seconds.
            notification.ShowBalloonTip(5000);
        }

        static void RunAsSTAThread(Action goForIt)
        {
            AutoResetEvent @event = new AutoResetEvent(false);
            Thread thread = new Thread(
                () =>
                {
                    goForIt();
                    @event.Set();
                });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            @event.WaitOne();
        }
        [STAThread]
        void Listen()
        {
            Console.WriteLine("start");

            try
            {
                // Set the TcpListener on port 40004.
                Int32 port = 40004;
                IPAddress localAddr = IPAddress.Parse(GetLocalIPAddress());

                // TcpListener server = new TcpListener(port);
                TcpListener server = new TcpListener(localAddr, port);

                // Start listening for client requests.
                server.Start();

                // Buffer for reading data
                Byte[] bytes = new Byte[2048];
                String data = null;

                MessageType type = MessageType.Data;

                // Enter the listening loop.
                while (true)
                {
                    Console.Write($"Waiting for a connection on {GetLocalIPAddress()}, port: {port}... ");

                    // Perform a blocking call to accept requests.
                    // You could also user server.AcceptSocket() here.
                    TcpClient client = server.AcceptTcpClient();
                    Console.WriteLine("Connected!");

                    data = null;

                    // Get a stream object for reading and writing
                    NetworkStream stream = client.GetStream();

                    int i;

                    // Loop to receive all the data sent by the client.
                    try
                    {
                        while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                        {
                            // Translate data bytes to a ASCII string.
                            data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                            Console.WriteLine(String.Format("Received: {0}", data));

                            string decoded = "";
                            Regex regex = new Regex(@"(^&START&)(.*)(&STOP&$)");
                            Match match = regex.Match(data);
                            if (match.Success)
                            {
                                decoded = data.Substring(7);
                                decoded = decoded.Substring(0, decoded.Length - 7);
                                decoded = Base64Decode(decoded);

                                type = MessageType.Data;
                            }
                            regex = new Regex(@"(^&INIT&)(.*)(&STOP&$)");
                            match = regex.Match(data);
                            if (match.Success)
                            {
                                decoded = data.Substring(6);
                                decoded = decoded.Substring(0, decoded.Length - 7);
                                decoded = Base64Decode(decoded);

                                type = MessageType.Init;
                            }
                            if (!string.IsNullOrEmpty(decoded))
                            {
                                if (type == MessageType.Data)
                                {
                                    ShowBaloonTip("ClipboardSync", "Received: " + decoded);

                                    Clipboard.SetText(decoded);
                                }
                                else if (type == MessageType.Init)
                                {
                                    ShowBaloonTip("ClipboardSync", "New device connected: " + decoded);
                                }
                            }
                            // Process the data sent by the client.
                            data = data.ToUpper();

                            //byte[] msg = System.Text.Encoding.ASCII.GetBytes(data);

                            // Send back a response.
                            //stream.Write(msg, 0, msg.Length);
                            Console.WriteLine(String.Format("Ack", data));
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }

                    // Shutdown and end connection
                    client.Close();
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine("SocketException: {0}", e);
            }


            Console.WriteLine("stop");
        }

        public static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
            return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
        }

        public static string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("Local IP Address Not Found!");
        }
        void Exit(object sender, EventArgs e)
        {
            // Hide tray icon, otherwise it will remain shown until user mouses over it
            trayIcon.Visible = false;

            Application.Exit();
        }
    }
}
