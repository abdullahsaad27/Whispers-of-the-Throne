using System;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading.Tasks;
using KingdomBlind_CSharp.Audio;

namespace KingdomBlind_CSharp.UI
{
    public class ProgressForm : Form
    {
        private ProgressBar progressBar;
        private Label lblStatus;
        private SapiEngine sapi;
        public bool Success { get; private set; } = false;

        public ProgressForm(string title, string initialStatus, SapiEngine sapiEngine)
        {
            sapi = sapiEngine;
            
            this.Text = title;
            this.Size = new Size(500, 200);
            this.RightToLeft = RightToLeft.Yes;
            this.RightToLeftLayout = true;
            this.StartPosition = FormStartPosition.CenterParent;
            this.ControlBox = false; // Prevent closing manually
            
            lblStatus = new Label();
            lblStatus.Text = initialStatus;
            lblStatus.Font = new Font("Arial", 12, FontStyle.Bold);
            lblStatus.Dock = DockStyle.Top;
            lblStatus.Height = 50;
            lblStatus.TextAlign = ContentAlignment.MiddleCenter;
            this.Controls.Add(lblStatus);
            
            progressBar = new ProgressBar();
            progressBar.Style = ProgressBarStyle.Marquee;
            progressBar.Dock = DockStyle.Fill;
            this.Controls.Add(progressBar);
            
            this.Shown += async (s, e) => {
                sapi.Speak(initialStatus);
                await RunInstallation();
            };
        }

        private async Task RunInstallation()
        {
            try
            {
                // Install SuperTonic using pip
                UpdateStatus("جاري تنزيل وتثبيت حزمة SuperTonic للذكاء الاصطناعي...");
                
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = "-m pip install supertonic",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using (Process process = Process.Start(psi))
                {
                    await Task.Run(() => process.WaitForExit());
                    
                    if (process.ExitCode == 0)
                    {
                        Success = true;
                        UpdateStatus("تم التثبيت بنجاح!");
                        sapi.Speak("تم التثبيت بنجاح. يتم الآن إغلاق شاشة التنزيل.");
                    }
                    else
                    {
                        Success = false;
                        UpdateStatus("فشل التثبيت! تأكد من وجود Python وتوصيل الإنترنت.");
                        sapi.Speak("فشل التثبيت.");
                    }
                }
            }
            catch (Exception ex)
            {
                Success = false;
                UpdateStatus("حدث خطأ: " + ex.Message);
            }
            finally
            {
                await Task.Delay(2000);
                this.DialogResult = Success ? DialogResult.OK : DialogResult.Abort;
                this.Close();
            }
        }

        private void UpdateStatus(string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => { lblStatus.Text = message; }));
            }
            else
            {
                lblStatus.Text = message;
            }
        }
    }
}
