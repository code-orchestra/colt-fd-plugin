namespace ColtPlugin.Rpc
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Timers;

    public delegate void AppStarterDelegate(Boolean param);

    public class AppStarter
    {
        private AppStarterDelegate callback;
        private Boolean callbackParam;
        private String tempColtFile;
        private Timer timer;
        private int count;

        public AppStarter(AppStarterDelegate onConnected, Boolean onConnectedParam)
        {
            if (COLTIsRunning())
            {
                onConnected(onConnectedParam);
            }

            else
            {
                callback = onConnected;
                callbackParam = onConnectedParam;
                tempColtFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".colt");

                File.CreateText(tempColtFile).Close();

                try
                {
                    Process.Start(tempColtFile);

                    // now we wait for user to close evaluation notice
                    timer = new Timer();
                    timer.SynchronizingObject = (System.Windows.Forms.Form)PluginCore.PluginBase.MainForm;
                    timer.Interval = 1000;
                    timer.Elapsed += OnTimer;
                    count = 0;
                    OnTimer();
                }

                catch (Exception)
                {
                    PluginCore.Managers.TraceManager.Add(Resources.LocaleHelper.GetString("Error.StartingCOLTFailure"), -1);
                    CleanUp();
                }
            }
        }

        private void OnTimer(Object sender = null, EventArgs e = null)
        {
            timer.Stop();

            if (COLTIsRunning())
            {
                // we are good to go
                callback(callbackParam);

                CleanUp();
                return;
            }

            if (count++ > 7)
            {
                PluginCore.Managers.TraceManager.Add(Resources.LocaleHelper.GetString("Error.StartingCOLTTimedOut"), -1);

                CleanUp();
                return;
            }

            timer.Start();
        }

        private void CleanUp()
        {
            File.Delete(tempColtFile);

            callback = null;
            tempColtFile = null;
            timer = null;
        }

        private Boolean COLTIsRunning ()
        {
            try
            {
                JsonRpcClient client = new JsonRpcClient();
                client.Invoke("ping", new Object[] { });
                return true;
            }

            catch (Exception)
            {
            }

            return false;
        }
    }
}
