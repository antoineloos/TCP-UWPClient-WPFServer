using Prism.Mvvm;
using StreamSocketUniversalApp.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Core;

namespace StreamSocketUniversalApp
{
    public sealed class SocketClient : BindableBase
    {
        const int BUFFER_SIZE = 4096;

        const string OPEN = "[open]";
        const string EXIT = "[exit]";
        const string PUSH = "[push]";
        const string LIST = "[list]";
        const string PULL = "[pull]";
        const string ACK = "[ack]";

        private StreamSocket _socket;
        //private BackgroundWorker bgw;
        //private DataWriter _writer;
        private Random rnd;
        public bool IsAlive;

        private string numClt;
        public string NumClt
        {
            get { return numClt; }
            set { SetProperty(ref numClt, value); }
        }

        private string message;
        public string Message
        {
            get { return message; }
            set { SetProperty(ref message, value); }
        }

        private string received;
        public string Received
        {
            get { return received; }
            set { SetProperty(ref received, value); }
        }


        public delegate void Error(string message);
        public event Error OnError;


        private bool isConnected;
        public bool IsConnected
        {
            get { return isConnected; }
            set { SetProperty(ref isConnected, value); }
        }

        public string Ip { get; }
        public int Port { get; }

        public SocketClient(string ip, int port)
        {
            Ip = ip;
            Port = port;
            rnd = new Random();
            NumClt = "Client " + rnd.Next(1000, 9000).ToString();
            Message = "";
            Received = "";
            //bgw = new BackgroundWorker();
            //bgw.DoWork += Bgw_DoWork;

            CreateInputDbFolder();
        }



        private async void Listen()
        //private async void Bgw_DoWork(object sender, DoWorkEventArgs e)
        {
            using (DataReader reader = new DataReader(_socket.InputStream) { InputStreamOptions = InputStreamOptions.Partial, ByteOrder = ByteOrder.LittleEndian, UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8 })
            {
                string result = "";
                bool IsFile = false;
                // reader.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;
                try
                {
                    while (IsConnected)
                    {
                        await reader.LoadAsync(BUFFER_SIZE);
                        while (reader.UnconsumedBufferLength > 0 && IsConnected)
                        {
                            result = reader.ReadString(reader.UnconsumedBufferLength);

                            //if (result.Contains("[")) { IsFile = true; result = ""; }
                            //if (result.Contains("]")) IsFile = false;
                            //if (IsFile)
                            //{
                            //    var folder = await Windows.Storage.ApplicationData.Current.LocalFolder.GetFolderAsync("InputDB");
                            //    var file = await folder.CreateFileAsync("1User" + ".json", Windows.Storage.CreationCollisionOption.ReplaceExisting);
                            //    var data = await file.OpenStreamForWriteAsync();

                            //    using (var r = new StreamWriter(data))
                            //    {
                            //        r.Write("[\n" + result + "\n]");
                            //    }
                            //}
                            //else
                            //{
                            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                            {
                                Received = result;
                            });
                            //}

                            await reader.LoadAsync(BUFFER_SIZE);
                        }

                        reader.DetachStream();
                        if (Received.Contains("Exit")) IsConnected = false;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.ToString());
                }

                // Nico : Done by using(..) statement
                //finally
                //{
                //    reader.DetachStream();
                //    reader.Dispose();
                //}
            }
        }

        public async void CreateInputDbFolder()
        {
            var folder = Windows.Storage.ApplicationData.Current.LocalFolder;
            await folder.CreateFolderAsync("InputDB", Windows.Storage.CreationCollisionOption.OpenIfExists);
        }

        public async void Connect()
        {
            try
            {
                IsAlive = true;
                IsConnected = true;
                var hostName = new HostName(Ip);
                _socket = new StreamSocket();
                await _socket.ConnectAsync(hostName, Port.ToString());

                await Task.Factory.StartNew(Listen);
                //bgw.RunWorkerAsync();
                Send(OPEN);
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex.Message);
            }
        }

        /// <summary>
        /// Protocole PUSH
        /// Envoi d'un fichier en binaire
        /// </summary>
        /// <param name="file"></param>
        public async Task Send(StorageFile file, string path = "")
        {
            try
            {
                // Envoi du Message
                Send(PUSH);

                var readTotal = SendHandler(new Tuple<StorageFile, string>(file, path)).Result;

                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Received = $"Sent [{readTotal}] {Received}";
                });
            }
            catch (Exception e)
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    Received = $"Error [{file.DisplayName}] {e.Message}";
                });
            }
        }

        private Task<ulong> SendHandler(Tuple<StorageFile, string> state)
        {
            return Task.Factory.StartNew(async () =>
            {
                StorageFile file = state.Item1;
                string path = state.Item2;

                try
                {
                    var socket = new StreamSocket();
                    await socket.ConnectAsync(new HostName(Ip), $"{Port + 1}");
                    var outputStream = socket.OutputStream;
                    byte[] buffer = new byte[BUFFER_SIZE];

                    // Envoi du nom du fichier et de la taille du fichier
                    var filename = !string.IsNullOrEmpty(path) ?
                                    Path.Combine(path, file.Name) :
                                    file.Name;
                    var fileProp = await file.GetBasicPropertiesAsync();
                    var header = $"{filename}|{fileProp.Size}";
                    var read = Encoding.UTF8.GetBytes(header, 0, header.Length, buffer, 0);
                    await outputStream.WriteAsync(buffer.AsBuffer(0, read));
                    await outputStream.FlushAsync();

                    // Envoi
                    int readCount = 0;
                    ulong readTotal = 0;
                    var pendingWrites = new List<IAsyncOperationWithProgress<uint, uint>>();

                    using (var readStream = await file.OpenStreamForReadAsync())
                    {
                        while (readTotal < fileProp.Size)
                        {
                            readTotal += (ulong)(readCount = readStream.Read(buffer, 0, BUFFER_SIZE));
                            pendingWrites.Add(outputStream.WriteAsync(buffer.AsBuffer(0, readCount)));
                        }
                        await outputStream.FlushAsync();
                    }
                    socket.OutputStream.Dispose();
                    socket.Dispose();
                    return readTotal;
                }
                catch (Exception e)
                {
                    await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        Received = $"Error [{file.DisplayName}] {e.Message}";
                    });
                    return (ulong)0;
                }
            }).Result;
        }

        public async void Send(string message)
        {
            try
            {
                StreamWriter writer = new StreamWriter(_socket.OutputStream.AsStreamForWrite());

                await writer.WriteLineAsync(message);
                await writer.FlushAsync();
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex.Message);
            }
        }

        public async void Send()
        {
            await Task.Run(() => Send(Message));
        }

        public void Close()
        {
            try
            {
                IsConnected = false;
                IsAlive = false;

                Send(EXIT);

                _socket.Dispose();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }
    }
}
