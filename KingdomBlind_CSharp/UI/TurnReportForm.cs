using System;
using System.Drawing;
using System.Windows.Forms;
using KingdomBlind_CSharp.Audio;
using KingdomBlind_CSharp.Models;
using KingdomBlind_CSharp.Systems;

namespace KingdomBlind_CSharp.UI
{
    public class TurnReportForm : Form
    {
        private SapiEngine sapi;
        private INarrationService narration;
        private GameState state;
        
        public TurnReportForm(string title, string reportText, SapiEngine sapiEngine)
            : this(title, reportText, null, null, sapiEngine)
        {
        }

        public TurnReportForm(string title, string reportText, INarrationService narration, GameState state)
            : this(title, reportText, narration, state, null)
        {
        }

        private TurnReportForm(string title, string reportText, INarrationService narration, GameState state, SapiEngine sapiEngine)
        {
            sapi = sapiEngine;
            this.narration = narration;
            this.state = state;
            
            this.Text = title;
            this.Size = new Size(500, 400);
            this.RightToLeft = RightToLeft.Yes;
            this.RightToLeftLayout = true;
            this.StartPosition = FormStartPosition.CenterParent;
            
            TextBox txtReport = new TextBox();
            txtReport.Multiline = true;
            txtReport.ReadOnly = true;
            txtReport.ScrollBars = ScrollBars.Vertical;
            txtReport.Text = reportText;
            txtReport.Font = new Font("Arial", 12);
            txtReport.Dock = DockStyle.Fill;
            txtReport.TabStop = true;
            txtReport.AccessibleName = "نص التقرير";
            txtReport.AccessibleRole = AccessibleRole.Text;
            this.Controls.Add(txtReport);
            
            Button btnClose = new Button();
            btnClose.Text = "إغلاق التقرير (Enter)";
            btnClose.AccessibleName = "إغلاق التقرير";
            btnClose.Dock = DockStyle.Bottom;
            btnClose.Height = 50;
            btnClose.Font = new Font("Arial", 12, FontStyle.Bold);
            btnClose.Click += (s, e) => this.Close();
            this.Controls.Add(btnClose);
            
            this.AcceptButton = btnClose;
            
            this.Shown += (s, e) => {
                if (this.narration != null && this.state != null)
                    this.narration.Speak(this.state, "التقرير العام: " + reportText);
                else
                    sapi?.Speak("التقرير العام: " + reportText);
            };
            
            this.FormClosing += (s, e) => {
                if (this.narration != null) this.narration.Stop();
                else sapi?.Stop();
            };
        }
    }
}
