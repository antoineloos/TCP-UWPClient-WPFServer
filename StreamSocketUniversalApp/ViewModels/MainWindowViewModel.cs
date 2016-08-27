using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace StreamSocketUniversalApp.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {


        private static readonly MainWindowViewModel instance = new MainWindowViewModel();

        private BackgroundWorker bgw;
        

        public static MainWindowViewModel Instance
        {
            get
            {
                return instance;
            }
        }

        private string port;
        public string Port
        {
            get { return port; }
            set { SetProperty(ref port, value); }
        }

        private string adresse;
        public string Adresse
        {
            get { return adresse; }
            set { SetProperty(ref adresse, value); }
        }

        private string connexionStatus;
        public string ConnexionStatus
        {
            get { return connexionStatus; }
            set { SetProperty(ref connexionStatus, value); }
        }

        private ObservableCollection<SocketClient> lstSocketClt;
        public ObservableCollection<SocketClient> LstSocketClt
        {
            get { return lstSocketClt; }
            set { SetProperty(ref lstSocketClt, value); }
        }

        public DelegateCommand<SocketClient> ConnectCommand { get; private set; }
        public DelegateCommand<SocketClient> SendCommand { get; private set; }
        public DelegateCommand<SocketClient> CloseCommand { get; private set; }
        public DelegateCommand AddCltCommand { get; private set; }
        public DelegateCommand<SocketClient> DeleteCommand { get; private set; }
        public DelegateCommand<SocketClient> OpenCommand { get; private set; } 

        private MainWindowViewModel()
        {
            Port = "5555";
            //Adresse = "10.0.0.2";
            Adresse = "127.0.0.1";
            LstSocketClt = new ObservableCollection<SocketClient>();
            ConnectCommand = new DelegateCommand<SocketClient>(ConnectFunc);
            SendCommand = new DelegateCommand<SocketClient>(SendFunc);
            CloseCommand = new DelegateCommand<SocketClient>(CloseFunc);
            AddCltCommand = new DelegateCommand(AddCltFunc);
            DeleteCommand = new DelegateCommand<SocketClient>(DeleteFunc);
            OpenCommand = new DelegateCommand<SocketClient>(OpenFunc);
            bgw = new BackgroundWorker();
            bgw.DoWork += Bgw_DoWork;
        }
        
        private void Bgw_DoWork(object sender, DoWorkEventArgs e)
        {
            var lstFiles = ((Tuple<IReadOnlyList<StorageFile>, SocketClient>)e.Argument).Item1;
            var client = ((Tuple<IReadOnlyList<StorageFile>, SocketClient>)e.Argument).Item2;

            //for (int i = 0; i < lstFiles.Count; i++)
            //{
            //    client.Send(lstFiles[i]);
            //}

            client.Send(lstFiles[0]);

            //foreach (StorageFile elem in lstFiles.Where(f => f.FileType == ".json" || f.FileType == ".JSON"))
            //{
            //    string result = "";
            //    result += await FileIO.ReadTextAsync(elem, UnicodeEncoding.Utf8);

            //    client.Send("FileName:" + elem.DisplayName + "\n");
            //    foreach (string line in result.Split('\n'))
            //    {
            //        client.Send(line + "\n");
            //    }
            //    client.Send("EndFile\n");
            //}
        }

        public async Task<StorageFolder> OpenDlgFolder()
        {
            var folderPicker = new Windows.Storage.Pickers.FolderPicker
            {
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop
            };
            folderPicker.FileTypeFilter.Add(".json");
            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {

                Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", folder);

                return folder;

            }
            else
            {
                Debug.WriteLine("Operation cancelled.");
                return null;
            }
        }

        private async void OpenFunc(SocketClient client)
        {
            var folder = await OpenDlgFolder();
            var lstFiles = await folder.GetFilesAsync();
            bgw.RunWorkerAsync(new Tuple<IReadOnlyList<StorageFile>,SocketClient>( lstFiles, client));
            
        }

        public void CloseAllClt()
        {
            foreach(SocketClient elem in LstSocketClt)
            {
                elem.Close();
            }
        }

        private void DeleteFunc(SocketClient client)
        {
            if (client.IsAlive)
            {
                client.Close();
            }
            LstSocketClt.Remove(client);
        }

        private void AddCltFunc()
        {
            LstSocketClt.Add(new SocketClient(Adresse, Convert.ToInt32(Port)));
        }

        private void CloseFunc(SocketClient client)
        {
            if (client.IsAlive)
            {
                client.Close();
            }  
        }

        private void SendFunc(SocketClient client)
        {
            client.Send();
        }

        private void ConnectFunc(SocketClient client)
        {
            client.Connect();
        }
    }
}
